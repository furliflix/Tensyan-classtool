using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Tensyan.Core;
using Tensyan.UI.Pages;

namespace Tensyan.UI
{
    /// <summary>
    /// 唯一的主窗口（shell）：登录/注册与主界面都在这一个窗体里切换状态，
    /// 不再有“登录窗 + 主窗”两个顶层窗体。左侧栏在登录态 396px、主界面态 212px 之间动画过渡。
    /// </summary>
    public class MainForm : ChromeForm
    {
        private const int NavLogin = 396;
        private const int NavMain = 212;
        private const int TopH = 64;

        private readonly App _app;
        private NotifyIcon _tray;
        private Panel _nav, _top, _content;
        private LoginView _login;
        private UserCard _userCard;
        private FluentSelect _classBox;
        private Pill _badge;
        private Label _classInfo;
        private RoundButton _btnUndo, _btnRandom;
        private readonly List<NavItem> _navItems = new List<NavItem>();
        private readonly List<PageBase> _pages = new List<PageBase>();
        private readonly List<PageDef> _defs = new List<PageDef>();
        private readonly List<bool> _navAllowed = new List<bool>();
        private int _current = -1;
        private bool _switching;
        private bool _loggedIn;
        private double _navT = 1;          // 0 = 登录态宽度，1 = 主界面宽度
        private bool _navItemsVisible = true;

        private class PageDef
        {
            public string Title;
            public IconKind Icon;
            public Role MinRole;
            public Func<App, PageBase> Create;
        }

        /// <summary>关闭窗口 = 收进托盘继续驻留（只有托盘菜单的“退出”才真正结束）。</summary>
        private void OnClosingToTray(object sender, FormClosingEventArgs e)
        {
            if (Lan.WantsExit || e.CloseReason == CloseReason.WindowsShutDown || e.CloseReason == CloseReason.ApplicationExitCall) return;
            e.Cancel = true;
            Hide();
            try
            {
                if (_tray != null && _tray.Visible)
                    _tray.ShowBalloonTip(2000, "Tensyan 仍在后台运行",
                        "手机可以继续配对与同步。要完全退出：右键托盘图标 → 退出。", ToolTipIcon.Info);
            }
            catch { }
        }

        public MainForm(App app)
        {
            _app = app;
            TitleText = "Tensyan 班级积分工具";
            ShowBrandMark = true;
            ClientSize = Theme.PS(1380, 830);
            MinimumSize = Theme.PS(1120, 700);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            SetTitleBarColors(Theme.Bg, Theme.TextMain, Theme.TextSub);

            DefinePages();
            BuildNav();
            BuildTop();
            BuildContent();

            // ---- 托盘常驻：关窗口不退出，手机随时能配对/同步 ----
            try
            {
                _tray = new NotifyIcon { Text = "Tensyan 班级积分工具", Visible = true };
                try { _tray.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application; }
                catch { _tray.Icon = SystemIcons.Application; }
                var menu = new ContextMenuStrip();
                menu.Items.Add("显示主界面", null, (s2, e2) => { Show(); WindowState = FormWindowState.Normal; Activate(); });
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add("退出（停止后台服务）", null, (s2, e2) =>
                {
                    Lan.WantsExit = true;
                    Lan.Shutdown();
                    if (_tray != null) _tray.Visible = false;
                    Close();
                    Application.Exit();
                });
                _tray.ContextMenuStrip = menu;
                _tray.DoubleClick += (s2, e2) => { Show(); WindowState = FormWindowState.Normal; Activate(); };
                FormClosing += OnClosingToTray;
                Lan.EnsureStarted(_app);
            }
            catch (Exception ex) { Paths.Log("托盘初始化失败：" + ex); }

            Load += (s, e) =>
            {
                FitToScreen();
                try
                {
                    if (_app.Session != null && _app.Session.Account != null) ShowMain(_app.Session, false);
                    else ShowLogin(false);
                }
                catch (Exception ex)
                {
                    Paths.Log("主窗口初始化失败：" + ex);
                    ShowFatal(ex);
                }
            };

            // 首次使用（本机没有管理员）时，等窗口显示出来再弹创建向导，
            // 否则模态窗口会盖住还没绘制完成的主窗，看起来像“打不开”。
            Shown += (s, e) =>
            {
                Paths.Log("主窗口已显示：" + (_loggedIn ? "主界面" : "登录界面"));

                // 数据文件有问题（损坏并被备份）时明确告警，不能静默当空数据
                if (!string.IsNullOrEmpty(_app.Store.LoadWarning))
                {
                    Paths.Log("数据告警：" + _app.Store.LoadWarning);
                    Toast.Show(this, _app.Store.LoadWarning, Theme.Amber);
                }

                // 数据目录被降级/兜底时也要说清楚
                if (!string.IsNullOrEmpty(Paths.WarningReason))
                {
                    Paths.Log("数据目录告警：" + Paths.WarningReason);
                    Toast.Show(this, Paths.WarningReason, Theme.Amber);
                }

                if (_loggedIn || HasAdmin()) return;
                BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (TryFirstAdmin()) return;
                        Paths.Log("用户取消了创建管理员向导，停留在登录界面");
                    }
                    catch (Exception ex)
                    {
                        Paths.Log("创建管理员向导失败：" + ex);
                    }
                    ShowLogin(false);
                }));
            };

            FormClosed += (s, e) =>
            {
                _app.Store.Save();
                Paths.Log("主窗口已关闭，程序退出");
            };

            _app.DataChanged += (s, e) =>
            {
                UpdateTopInfo();
                ShowSaveErrorIfAny();
            };
            _app.ClassSwitched += (s, e) =>
            {
                if (_switching) return;
                _switching = true;
                try
                {
                    RefreshClassList();
                    UpdateTopInfo();
                    ReloadCurrent();
                }
                finally { _switching = false; }
            };
        }

        private void DefinePages()
        {
            _defs.Add(new PageDef { Title = "学生积分", Icon = IconKind.Students, MinRole = Role.Guest, Create = a => new PageStudents(a) });
            _defs.Add(new PageDef { Title = "积分排行", Icon = IconKind.Rank, MinRole = Role.Guest, Create = a => new PageRank(a) });
            _defs.Add(new PageDef { Title = "加分记录", Icon = IconKind.History, MinRole = Role.Guest, Create = a => new PageHistory(a) });
            _defs.Add(new PageDef { Title = "数据统计", Icon = IconKind.Stats, MinRole = Role.Guest, Create = a => new PageStats(a) });
            _defs.Add(new PageDef { Title = "账号权限", Icon = IconKind.Accounts, MinRole = Role.Admin, Create = a => new PageAccounts(a) });
            _defs.Add(new PageDef { Title = "登录U盘", Icon = IconKind.Usb, MinRole = Role.Admin, Create = a => new PageUsb(a) });
            _defs.Add(new PageDef { Title = "配对手机", Icon = IconKind.Phone, MinRole = Role.Admin, Create = a => new PagePairing(a) });
            _defs.Add(new PageDef { Title = "系统设置", Icon = IconKind.Settings, MinRole = Role.Admin, Create = a => new PageSettings(a) });
            foreach (var d in _defs) _navAllowed.Add(true);
        }

        private void BuildNav()
        {
            _nav = new BufferedPanel { BackColor = Theme.NavBg };
            _nav.Paint += NavPaint;
            Controls.Add(_nav);

            for (int i = 0; i < _defs.Count; i++)
            {
                var item = new NavItem { Text = _defs[i].Title, Icon = _defs[i].Icon };
                int idx = i;
                item.Click += (s, e) => SwitchPage(idx);
                _navItems.Add(item);
                _nav.Controls.Add(item);
            }

            _userCard = new UserCard();
            _userCard.Logout += (s, e) => Logout();
            _nav.Controls.Add(_userCard);
        }

        private void NavPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.NavBg);

            bool login = !_loggedIn;
            int logoW = (int)Anim.Lerp(Theme.P(250), Theme.P(122), _navT);
            int logoX = login ? Theme.P(40) : Theme.P(16);
            int logoY = login ? Theme.P(44) : Theme.P(10);
            int logoH = Brand.DrawLogo(g, logoX, logoY, logoW);
            if (logoH == 0)
            {
                Brand.DrawTile(g, new Rectangle(logoX, logoY, Theme.P(28), Theme.P(28)));
                logoH = Theme.P(28);
            }

            if (login)
            {
                int x = Theme.P(42);
                using (var f = Theme.Body())
                using (var b = new SolidBrush(Theme.TextSub))
                    g.DrawString("班级积分工具 · 让每一次进步都被看见", f, b, x, logoY + logoH + Theme.P(12));

                int ly = logoY + logoH + Theme.P(64);
                DrawRole(g, x, ly, Theme.Green, "访客模式", "只读查看积分、排行与统计");
                DrawRole(g, x, ly + Theme.P(74), Theme.Primary, "成员模式", "仅可给学生加分，不能扣分");
                DrawRole(g, x, ly + Theme.P(148), Theme.Amber, "管理员模式", "全部权限：加分 · 管理 · 导出 · U盘");

                using (var f = Theme.Caption())
                using (var b = new SolidBrush(Theme.TextFaint))
                {
                    g.DrawString("版本 1.1.0 · 绿色便携 · 数据保存在本机", f, b, x, Height - Theme.P(66));
                    g.DrawString("Clarusvita Studio", f, b, x, Height - Theme.P(46));
                    g.DrawString("furliflix", f, b, x, Height - Theme.P(26));
                }
            }
            else
            {
                using (var f = Theme.Caption())
                using (var b = new SolidBrush(Theme.TextFaint))
                    g.DrawString("班级积分工具", f, b, Theme.P(18), Theme.P(62));

                using (var pen = new Pen(Theme.Border))
                    g.DrawLine(pen, Theme.P(16), Theme.P(88), Theme.P(NavMain - 16), Theme.P(88));
            }

            using (var pen = new Pen(Theme.Border))
                g.DrawLine(pen, _nav.Width - 1, 0, _nav.Width - 1, _nav.Height);
        }

        private void DrawRole(Graphics g, int x, int y, Color color, string title, string desc)
        {
            int tile = Theme.P(28);
            Theme.FillRounded(g, new Rectangle(x, y, tile, tile), Theme.P(8), Theme.Mix(color, Color.White, 0.86));
            int iconSize = Theme.P(18);
            Icons.Draw(g, new Rectangle(x + (tile - iconSize) / 2, y + (tile - iconSize) / 2, iconSize, iconSize), IconKind.Person, color);

            using (var f = Theme.BodyStrong())
            using (var b = new SolidBrush(Theme.TextMain))
                g.DrawString(title, f, b, x + Theme.P(40), y + Theme.P(1));
            using (var f = Theme.Caption())
            using (var b = new SolidBrush(Theme.TextSub))
                g.DrawString(desc, f, b, x + Theme.P(40), y + Theme.P(20));
        }

        private void BuildTop()
        {
            _top = new BufferedPanel { BackColor = Theme.Bg };
            _top.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.Border))
                    e.Graphics.DrawLine(pen, 0, _top.Height - 1, _top.Width, _top.Height - 1);
            };
            Controls.Add(_top);

            _classBox = new FluentSelect { Size = Theme.PS(210, 34) };
            _classBox.SelectedIndexChanged += (s, e) => OnClassChanged();
            _top.Controls.Add(_classBox);

            _classInfo = new Label
            {
                Text = "",
                Font = Theme.Caption(),
                ForeColor = Theme.TextSub,
                AutoSize = false,
                Size = new Size(Theme.P(360), Theme.LineHeight(Theme.Caption())),
                BackColor = Color.Transparent
            };
            _top.Controls.Add(_classInfo);

            _badge = new Pill { Size = Theme.PS(140, 28) };
            _top.Controls.Add(_badge);

            _btnRandom = new RoundButton
            {
                Text = "随机点名",
                Icon = IconKind.Random,
                Size = Theme.PS(114, 34),
                Soft = true,
                Fill = Theme.Card,
                OutlineColor = Theme.BorderStrong
            };
            _btnRandom.Click += (s, e) => RandomPick();
            _top.Controls.Add(_btnRandom);

            _btnUndo = new RoundButton
            {
                Text = "撤销上一步",
                Icon = IconKind.Undo,
                Size = Theme.PS(122, 34),
                Soft = true,
                Fill = Theme.Card,
                OutlineColor = Theme.BorderStrong
            };
            _btnUndo.Click += (s, e) => DoUndo();
            _top.Controls.Add(_btnUndo);
        }

        private void BuildContent()
        {
            _content = new BufferedPanel { BackColor = Theme.Bg };
            _content.Paint += (s2, e2) => Theme.PaintBg(e2.Graphics, _content.ClientRectangle);
            Controls.Add(_content);

            _login = new LoginView(_app) { Dock = DockStyle.Fill };
            _login.LoggedIn += (s, e) =>
            {
                var session = _login.Result;
                if (session != null) ShowMain(session, true);
            };
            _content.Controls.Add(_login);

            for (int i = 0; i < _defs.Count; i++) _pages.Add(null);
        }

        private PageBase EnsurePage(int index)
        {
            if (index < 0 || index >= _defs.Count) return null;
            if (_pages[index] != null) return _pages[index];
            var page = _defs[index].Create(_app);
            page.Dock = DockStyle.Fill;
            page.Visible = false;
            _pages[index] = page;
            _content.Controls.Add(page);
            return page;
        }

        private void ReloadCurrent()
        {
            if (_current < 0 || _current >= _pages.Count) return;
            var page = _pages[_current];
            if (page != null) page.Reload();
        }

        // ---------------- 状态切换 ----------------

        /// <summary>显示登录/注册界面（同一个窗口内的状态，不是新开窗体）。</summary>
        public void ShowLogin(bool animate = true)
        {
            _loggedIn = false;
            _navItemsVisible = false;
            _app.Session = Session.Guest();
            _login.Visible = true;
            _login.RefreshState();
            for (int i = 0; i < _pages.Count; i++) if (_pages[i] != null) _pages[i].Visible = false;
            foreach (var item in _navItems) item.Visible = false;
            if (_userCard != null) _userCard.Visible = false;
            _top.Visible = false;

            TitleText = "Tensyan 班级积分工具 · 登录";
            TitleLabel.Text = "  Tensyan 班级积分工具 · 登录";
            AnimateNav(0, animate);
        }

        /// <summary>进入主界面。</summary>
        public void ShowMain(Session session, bool animate = true)
        {
            _app.Session = session;
            _app.Store.Save();
            Paths.Log("登录成功：" + session.User + " / " + RoleText.Name(session.Role) + " via " + session.LoginMethod);

            _loggedIn = true;
            _login.Visible = false;
            _top.Visible = true;
            if (_userCard != null) _userCard.Visible = true;

            ApplyPermissions();
            RefreshClassList();
            SwitchPage(0);

            TitleText = "Tensyan 班级积分工具";
            TitleLabel.Text = "  Tensyan 班级积分工具";
            AnimateNav(1, animate, () => { _navItemsVisible = true; LayoutShell(); });

            MaybeForcePasswordChange();
        }

        private void AnimateNav(double target, bool animate, Action done = null)
        {
            if (!animate || !Anim.Enabled)
            {
                _navT = target;
                _navItemsVisible = target > 0.5;
                LayoutShell();
                if (done != null) done();
                return;
            }
            double from = _navT;
            Anim.Run(this, 280, t =>
            {
                if (IsDisposed) return;
                _navT = Anim.Lerp(from, target, Anim.EaseOutCubic(t));
                LayoutShell();
            }, () =>
            {
                if (IsDisposed) return;
                _navT = target;
                LayoutShell();
                if (done != null) done();
            });
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutShell();
        }

        private void LayoutShell()
        {
            if (_nav == null) return;
            int top = TitleBar.Height;
            int h = Math.Max(0, ClientSize.Height - top);
            int navW = (int)Anim.Lerp(Theme.P(NavLogin), Theme.P(NavMain), _navT);
            navW = Math.Max(Theme.P(160), navW);

            _nav.SetBounds(0, top, navW, h);
            _top.SetBounds(navW, top, Math.Max(0, ClientSize.Width - navW), Theme.P(TopH));
            if (_loggedIn)
                _content.SetBounds(navW, top + Theme.P(TopH), Math.Max(0, ClientSize.Width - navW), Math.Max(0, h - Theme.P(TopH)));
            else
                _content.SetBounds(navW, top, Math.Max(0, ClientSize.Width - navW), h);

            for (int i = 0; i < _navItems.Count; i++)
            {
                _navItems[i].SetBounds(Theme.P(8), Theme.P(98) + i * Theme.P(44), navW - Theme.P(16), Theme.P(40));
                _navItems[i].Visible = _loggedIn && _navItemsVisible && _navAllowed[i];
            }
            if (_userCard != null)
                _userCard.SetBounds(Theme.P(8), h - Theme.P(108), navW - Theme.P(16), Theme.P(96));

            // 顶栏：一行排开（班级选择 → 班级概况 → 模式徽标 … 右侧命令按钮）
            if (_classBox != null) _classBox.Location = new Point(Theme.P(16), Theme.P(15));
            if (_classInfo != null) _classInfo.Location = new Point(Theme.P(238), Theme.P(22));

            int right = _top.Width - Theme.P(16);
            if (_btnRandom != null) { right -= _btnRandom.Width; _btnRandom.Location = new Point(right, Theme.P(15)); right -= Theme.P(10); }
            if (_btnUndo != null) { right -= _btnUndo.Width; _btnUndo.Location = new Point(right, Theme.P(15)); }
            if (_badge != null)
            {
                int bx = (_btnUndo != null && _btnUndo.Visible ? _btnUndo.Left : _top.Width - Theme.P(16)) - _badge.Width - Theme.P(16);
                _badge.Location = new Point(Math.Max(Theme.P(608), bx), Theme.P(18));
            }

            _nav.Invalidate();
        }

        // ---------------- 权限与刷新 ----------------

        private void RefreshClassList()
        {
            var list = _app.VisibleClasses();
            _classBox.ClearItems();
            foreach (var c in list) _classBox.Items.Add(c.Name);
            int idx = 0;
            for (int i = 0; i < list.Count; i++) if (list[i] == _app.Current) idx = i;
            if (list.Count > 0)
            {
                _classBox.SelectedIndex = idx;
                if (_app.Current != list[idx]) _app.SwitchClass(list[idx]);
            }
        }

        private void OnClassChanged()
        {
            var list = _app.VisibleClasses();
            int i = _classBox.SelectedIndex;
            if (i < 0 || i >= list.Count) return;
            _app.SwitchClass(list[i]);
            UpdateTopInfo();
            ReloadCurrent();
        }

        private void ApplyPermissions()
        {
            var role = _app.Session.Role;
            for (int i = 0; i < _defs.Count; i++)
            {
                _navAllowed[i] = role >= _defs[i].MinRole;
                _navItems[i].Visible = _loggedIn && _navItemsVisible && _navAllowed[i];
            }

            _btnUndo.Visible = role == Role.Admin;
            _btnRandom.Visible = _app.Session.CanAddScore;

            _userCard.Display = _app.Session.Display;
            _userCard.RoleText = RoleText.Name(role) + " · " + _app.Session.LoginMethod;
            _userCard.Seed = _app.Session.User;
            _userCard.Invalidate();

            UpdateTopInfo();
        }

        private void UpdateTopInfo()
        {
            if (_badge == null || _classBox == null) return;
            var role = _app.Session.Role;
            _badge.Text = role == Role.Admin ? "管理员 · 全部权限" : (role == Role.Member ? "成员 · 仅加分" : "访客 · 只读");
            _badge.Accent = role == Role.Admin ? Theme.Primary : (role == Role.Member ? Theme.Green : Theme.TextSub);
            _badge.Invalidate();

            var c = _app.Current;
            if (c != null)
            {
                int active = 0;
                foreach (var s in c.Students) if (!s.Archived) active++;
                _classInfo.Text = active + " 名学生  ·  总积分 " + c.TotalScore()
                    + (string.IsNullOrEmpty(c.Teacher) ? "" : "  ·  " + c.Teacher);
            }
        }

        /// <summary>保存失败必须让用户看到（否则“看起来保存了、其实没写进去”）。</summary>
        private string _lastSaveError = "";
        private void ShowSaveErrorIfAny()
        {
            string err = _app.Store.LastSaveError;
            if (string.IsNullOrEmpty(err) || err == _lastSaveError) return;
            _lastSaveError = err;
            Toast.Show(this, "数据没能保存：" + err + "\r\n数据目录：" + Paths.DataDir + "\r\n可在“系统设置 → 数据与备份 → 更改数据位置…”换一个可写的位置", Theme.Red);
        }

        private void SwitchPage(int index)
        {
            if (index < 0 || index >= _defs.Count) return;
            if (!_navAllowed[index]) return;
            _current = index;
            for (int i = 0; i < _navItems.Count; i++) _navItems[i].Selected = (i == index);
            for (int i = 0; i < _pages.Count; i++) if (_pages[i] != null) _pages[i].Visible = (i == index);
            _navItems[index].Invalidate();

            var page = EnsurePage(index);
            if (page != null)
            {
                page.Visible = true;
                page.Reload();
                page.PlayEnter();
            }
            UpdateTopInfo();
        }

        private void DoUndo()
        {
            string msg;
            if (_app.Undo(out msg))
            {
                Toast.Show(this, msg, Theme.Primary);
                ReloadCurrent();
            }
            else Toast.Show(this, msg, Theme.Red);
        }

        private void RandomPick()
        {
            if (_app.Current == null || _app.Current.Students.Count == 0)
            {
                Toast.Show(this, "当前班级还没有学生", Theme.Red);
                return;
            }
            using (var d = new RandomPickDialog(_app))
            {
                d.ShowDialog(this);
                ReloadCurrent();
            }
        }

        /// <summary>退出登录：回到同一个窗口的登录状态（不关窗口、不再开第二个窗体）。</summary>
        public void Logout()
        {
            if (!_loggedIn) return;
            if (MessageBox.Show(this, "确定要退出登录吗？", "Tensyan", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            _app.Store.Save();
            Paths.Log("退出登录：" + _app.Session.User);
            ShowLogin(true);
        }

        /// <summary>启动阶段出错时，别留一个空白窗口：直接把原因写在窗口里 + 记日志。</summary>
        private void ShowFatal(Exception ex)
        {
            try
            {
                var box = new Panel { BackColor = Theme.Bg, Dock = DockStyle.Fill };
                var lbl = new Label
                {
                    AutoSize = false,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = Theme.Body(),
                    ForeColor = Theme.TextMain,
                    BackColor = Color.Transparent,
                    Text = "启动时出错，界面无法正常显示。\r\n\r\n" + ex.Message
                        + "\r\n\r\n数据目录：" + Paths.DataDir
                        + "\r\n日志：" + Paths.LogFile
                        + "\r\n\r\n可以把日志发给开发者定位问题。"
                };
                box.Controls.Add(lbl);
                _content.Controls.Add(box);
                box.BringToFront();
            }
            catch { }
        }

        private bool HasAdmin()
        {
            foreach (var a in _app.Store.Accounts)
                if (a.Role == Role.Admin && a.Enabled) return true;
            return false;
        }

        /// <summary>首次使用：没有管理员时先创建管理员（不需要U盘）。</summary>
        private bool TryFirstAdmin()
        {
            if (HasAdmin()) return false;
            using (var d = new FirstRunDialog(_app))
            {
                if (d.ShowDialog(this) != DialogResult.OK || d.CreatedAccount == null) return false;
                d.CreatedAccount.LastLoginUtc = DateTime.UtcNow;
                _app.Store.Save();
                ShowMain(new Session
                {
                    Role = d.CreatedAccount.Role,
                    User = d.CreatedAccount.User,
                    Display = d.CreatedAccount.Display,
                    Account = d.CreatedAccount,
                    LoginMethod = "首次创建"
                }, true);
                return true;
            }
        }

        private void MaybeForcePasswordChange()
        {
            var acc = _app.Session.Account;
            if (acc == null || !acc.MustChangePwd) return;
            using (var d = new ChangePasswordDialog(_app, acc, true))
            {
                d.ShowDialog(this);
                if (!d.Changed) Toast.Show(this, "建议尽快在“账号权限”中修改初始密码", Theme.Amber);
            }
        }

        // ---------------- 开发自检入口 ----------------

        public void DebugSwitchPage(int index) { SwitchPage(index); }
        public int DebugPageCount { get { return _pages.Count; } }
        public PageBase DebugCurrentPage { get { return _current >= 0 && _current < _pages.Count ? _pages[_current] : null; } }
        public bool DebugLoggedIn { get { return _loggedIn; } }
        public LoginView DebugLoginView { get { return _login; } }
        public void DebugShowLogin() { ShowLogin(false); }
        public void DebugSetSession(Session s) { _app.Session = s; if (_loggedIn) { ApplyPermissions(); SwitchPage(_current < 0 ? 0 : _current); } }

        /// <summary>左侧底部的当前身份卡片。</summary>
        private class UserCard : Panel
        {
            public string Display = "";
            public string RoleText = "";
            public string Seed = "";
            public event EventHandler Logout;
            private RoundButton _btn;

            public UserCard()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.ResizeRedraw, true);
                BackColor = Theme.NavBg;
                _btn = new RoundButton
                {
                    Text = "退出登录",
                    Icon = IconKind.SignOut,
                    Size = Theme.PS(112, 30),
                    Soft = true,
                    Fill = Theme.Card,
                    OutlineColor = Theme.BorderStrong,
                    Font = Theme.Caption()
                };
                _btn.Click += (s, e) => { if (Logout != null) Logout(this, EventArgs.Empty); };
                Controls.Add(_btn);
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                if (_btn != null) _btn.Location = new Point((Width - _btn.Width) / 2, Height - Theme.P(38));
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Theme.PaintBg(g, ClientRectangle);
                var r = new Rectangle(0, 0, Width - 1, Height - 1);
                Theme.FillRounded(g, r, Theme.P(8), Theme.Card);
                Theme.StrokeRounded(g, r, Theme.P(8), Theme.Border);

                Avatar.Draw(g, new Rectangle(Theme.P(12), Theme.P(12), Theme.P(36), Theme.P(36)), Display, Seed, null);
                TextRenderer.DrawText(g, Display, Theme.BodyStrong(),
                    new Rectangle(Theme.P(56), Theme.P(12), Width - Theme.P(66), Theme.P(18)), Theme.TextMain,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(g, RoleText, Theme.Caption(),
                    new Rectangle(Theme.P(56), Theme.P(30), Width - Theme.P(66), Theme.P(16)), Theme.TextFaint,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
    }
}
