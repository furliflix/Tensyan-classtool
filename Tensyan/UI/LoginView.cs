using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Tensyan.Core;

namespace Tensyan.UI
{
    /// <summary>
    /// 登录/注册视图（是主窗口里的一个面板，不再是单独窗体）：
    /// 账号密码 / U盘登录 / 注册新账号 + 访客只读；登录成功后抛 LoggedIn 事件交给主窗口切换状态。
    /// </summary>
    public class LoginView : Panel
    {
        private readonly App _app;
        public Session Result { get; private set; }

        /// <summary>登录成功（Result 已就绪）。</summary>
        public event EventHandler LoggedIn;

        private Panel _card;
        private SegmentedControl _tabs;
        private Panel[] _tabPanels;

        // 账号密码
        private FluentInput _user, _pwd;
        private RoundButton _login, _guest;
        private Label _msg;
        private bool _busy;
        private System.Windows.Forms.Timer _opTimeout;

        // U盘
        private Label _usbStatus;
        private RoundButton _usbLogin, _usbRefresh;

        // 注册
        private FluentInput _rUser, _rDisplay, _rPwd, _rPwd2;
        private RoundButton _register;
        private Label _rMsg;
        private Panel _authBox;
        private Label _authStatus;
        private RoundButton _authBtn;
        private bool _registerAuthorized;
        private string _authorizedBy = "";

        // 底部
        private RoundButton _makeUsb, _about;
        private Label _accountsHint;
        private FlowLayoutPanel _accountLinks;

        public LoginView(App app)
        {
            _app = app;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            Font = Theme.Body();

            _card = new Panel { BackColor = Theme.Bg };
            _card.Paint += CardPaint;
            Controls.Add(_card);

            BuildCard();
        }

        /// <summary>回车提交（面板本身没有 KeyPreview，所以挂在输入框上）。</summary>
        private void HookEnter(FluentInput input, Action action)
        {
            if (input == null) return;
            input.Box.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { action(); e.SuppressKeyPress = true; } };
        }

        /// <summary>每次显示时调用：刷新U盘检测与账号列表。</summary>
        public void RefreshState()
        {
            DetectUsbKey();
            BuildAccountLinks();
            UpdateRegisterGate();
            _msg.Text = "";
            _rMsg.Text = "";
            _registerAuthorized = false;
            _authorizedBy = "";
            SetBusy(false);
            if (_user != null) { _user.Text = ""; _pwd.Text = ""; }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutCardBox();
        }

        private int _tabIndex;

        /// <summary>卡片居中；注册页内容更高，所以按标签页取不同高度，避免留下大片空白。</summary>
        private void LayoutCardBox()
        {
            if (_card == null) return;
            int cardW = Theme.P(420);
            int cardH = Theme.P(_tabIndex == 2 ? 640 : 560);
            int x = Math.Max(Theme.P(12), (Width - cardW) / 2);
            int y = Math.Max(Theme.P(8), (Height - cardH) / 2);
            _card.SetBounds(x, y, cardW, cardH);
            LayoutCard();
        }

        private void CardPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            var r = new Rectangle(0, 0, _card.Width - 1, _card.Height - 1);
            Theme.CardSurface(g, r, Theme.P(8), true);
        }

        // ---------------- 卡片内容 ----------------

        private void BuildCard()
        {
            var title = new Label
            {
                Text = "登录 Tensyan",
                Font = Theme.Title(),
                ForeColor = Theme.TextMain,
                Location = Theme.PP(24, 22),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            _card.Controls.Add(title);

            var sub = new Label
            {
                Text = "选择一种方式进入班级积分系统",
                Font = Theme.Caption(),
                ForeColor = Theme.TextSub,
                Location = Theme.PP(24, 52),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            _card.Controls.Add(sub);

            _tabs = new SegmentedControl { Location = Theme.PP(24, 84), Size = Theme.PS(372, 36) };
            _tabs.AddItems("账号密码", "U盘登录", "注册新账号");
            _tabs.SelectedIndexChanged += (s, e) => ShowTab(_tabs.SelectedIndex);
            _card.Controls.Add(_tabs);

            _tabPanels = new Panel[3];
            for (int i = 0; i < 3; i++)
            {
                var p = new Panel { BackColor = Color.Transparent, Location = Theme.PP(24, 128), Size = Theme.PS(372, 396), Visible = false };
                _card.Controls.Add(p);
                _tabPanels[i] = p;
            }
            BuildPasswordTab(_tabPanels[0]);
            BuildUsbTab(_tabPanels[1]);
            BuildRegisterTab(_tabPanels[2]);
            _tabPanels[0].Visible = true;

            _makeUsb = new RoundButton
            {
                Text = "制作登录U盘",
                Icon = IconKind.Usb,
                Size = Theme.PS(150, 34),
                Soft = true,
                Fill = Theme.Card,
                OutlineColor = Theme.BorderStrong
            };
            _makeUsb.Click += (s, e) => MakeUsbFromLogin();
            _card.Controls.Add(_makeUsb);

            _about = new RoundButton
            {
                Text = "使用说明",
                Icon = IconKind.Info,
                Size = Theme.PS(110, 34),
                Subtle = true,
                Fill = Theme.Bg,
                OutlineColor = Theme.TextSub
            };
            _about.Click += (s, e) => { using (var d = new AboutDialog()) d.ShowDialog(FindForm()); };
            _card.Controls.Add(_about);

            _accountsHint = new Label
            {
                Text = "",
                Font = Theme.Caption(),
                ForeColor = Theme.TextFaint,
                AutoSize = false,
                BackColor = Color.Transparent
            };
            _card.Controls.Add(_accountsHint);

            _accountLinks = new FlowLayoutPanel { BackColor = Color.Transparent, WrapContents = true, AutoScroll = false };
            _card.Controls.Add(_accountLinks);

            LayoutCard();
            _card.Resize += (s, e) => LayoutCard();
        }

        private void LayoutCard()
        {
            if (_makeUsb == null) return;
            int w = _card.Width, h = _card.Height;
            _makeUsb.Location = new Point(Theme.P(24), h - Theme.P(48));
            _about.Location = new Point(Theme.P(184), h - Theme.P(48));
            _accountsHint.Location = new Point(Theme.P(24), h - Theme.P(106));
            _accountsHint.Size = new Size(w - Theme.P(48), Theme.LineHeight(Theme.Caption()));
            _accountLinks.Location = new Point(Theme.P(24), h - Theme.P(84));
            _accountLinks.Size = new Size(w - Theme.P(48), Theme.P(32));
        }

        private void ShowTab(int index)
        {
            _tabIndex = index;
            for (int i = 0; i < _tabPanels.Length; i++) _tabPanels[i].Visible = (i == index);
            LayoutCardBox();
            if (index == 1) DetectUsbKey();
            if (index == 2) UpdateRegisterGate();
        }

        private FluentInput Field(Panel host, string label, int y, bool password = false, string placeholder = "")
        {
            host.Controls.Add(new Label
            {
                Text = label,
                Font = Theme.Caption(),
                ForeColor = Theme.TextSub,
                Location = Theme.PP(0, y),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            var input = new FluentInput { Location = Theme.PP(0, y + 20), Size = Theme.PS(372, 36) };
            if (password) input.UseSystemPasswordChar = true;
            if (!string.IsNullOrEmpty(placeholder)) input.PlaceholderText = placeholder;
            host.Controls.Add(input);
            return input;
        }

        private void BuildPasswordTab(Panel host)
        {
            _user = Field(host, "用户名", 0, false, "例如 admin");
            _pwd = Field(host, "密码", 64, true, "请输入密码");
            HookEnter(_user, DoPasswordLogin);
            HookEnter(_pwd, DoPasswordLogin);

            _login = new RoundButton
            {
                Text = "登 录",
                Font = Theme.BodyStrong(),
                Size = Theme.PS(372, 40),
                Fill = Theme.Primary,
                Location = Theme.PP(0, 126)
            };
            _login.Click += (s, e) => DoPasswordLogin();
            host.Controls.Add(_login);

            _msg = new Label
            {
                Text = "",
                Font = Theme.Caption(),
                ForeColor = Theme.Red,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = Theme.PP(0, 170),
                Size = Theme.PS(372, 20)
            };
            host.Controls.Add(_msg);

            _guest = new RoundButton
            {
                Text = "访客进入（只读）",
                Icon = IconKind.Person,
                Size = Theme.PS(372, 38),
                Soft = true,
                Fill = Theme.Card,
                OutlineColor = Theme.BorderStrong,
                Location = Theme.PP(0, 198)
            };
            _guest.Click += (s, e) => DoGuestLogin();
            host.Controls.Add(_guest);

            host.Controls.Add(new Label
            {
                Text = "访客只能查看积分、排行、记录与统计，不能修改任何数据。\r\n没有账号？切到“注册新账号”按提示创建。",
                Font = Theme.Caption(),
                ForeColor = Theme.TextFaint,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = Theme.PP(0, 244),
                Size = Theme.PS(372, 44)
            });
        }

        private void BuildUsbTab(Panel host)
        {
            var box = new Panel { Location = Theme.PP(0, 0), Size = Theme.PS(372, 96), BackColor = Theme.PrimarySoft };
            box.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Theme.PrimarySoft);
                Theme.StrokeRounded(g, new Rectangle(0, 0, box.Width - 1, box.Height - 1), Theme.P(6), Theme.PrimarySoftBorder);
            };
            host.Controls.Add(box);

            _usbStatus = new Label
            {
                Text = "正在检测…",
                Font = Theme.Caption(),
                ForeColor = Theme.TextMain,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = Theme.PP(14, 12),
                Size = Theme.PS(344, 74)
            };
            box.Controls.Add(_usbStatus);

            _usbLogin = new RoundButton
            {
                Text = "U盘登录",
                Icon = IconKind.Usb,
                Font = Theme.BodyStrong(),
                Size = Theme.PS(372, 40),
                Fill = Theme.Primary,
                Location = Theme.PP(0, 112)
            };
            _usbLogin.Click += (s, e) => DoUsbLogin();
            host.Controls.Add(_usbLogin);

            _usbRefresh = new RoundButton
            {
                Text = "重新检测U盘",
                Icon = IconKind.Refresh,
                Size = Theme.PS(140, 34),
                Soft = true,
                Fill = Theme.Card,
                OutlineColor = Theme.BorderStrong,
                Location = Theme.PP(0, 164)
            };
            _usbRefresh.Click += (s, e) => { DetectUsbKey(); Toast.Show(this, "已重新检测磁盘", Theme.Primary); };
            host.Controls.Add(_usbRefresh);

            host.Controls.Add(new Label
            {
                Text = "把已制作的登录U盘插到电脑上，点“U盘登录”即可免密进入。\r\n如果U盘设过口令，会要求输入口令；密钥与这份 Tensyan 数据配套。",
                Font = Theme.Caption(),
                ForeColor = Theme.TextFaint,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = Theme.PP(0, 212),
                Size = Theme.PS(372, 60)
            });
        }

        private void BuildRegisterTab(Panel host)
        {
            _rUser = Field(host, "用户名（登录用，至少 2 个字符）", 0, false, "例如 student01 或 张三");
            _rDisplay = Field(host, "显示名称（可空）", 58, false, "例如 学习委员");
            _rPwd = Field(host, "密码（至少 3 位）", 116, true);
            _rPwd2 = Field(host, "确认密码", 174, true);
            HookEnter(_rUser, DoRegister);
            HookEnter(_rDisplay, DoRegister);
            HookEnter(_rPwd, DoRegister);
            HookEnter(_rPwd2, DoRegister);

            _authBox = new Panel { Location = Theme.PP(0, 240), Size = Theme.PS(372, 64), BackColor = Theme.PrimarySoft };
            _authBox.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(_authBox.BackColor);
                Theme.StrokeRounded(g, new Rectangle(0, 0, _authBox.Width - 1, _authBox.Height - 1), Theme.P(6), Theme.PrimarySoftBorder);
            };
            host.Controls.Add(_authBox);

            _authStatus = new Label
            {
                Font = Theme.Caption(),
                ForeColor = Theme.TextSub,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = Theme.PP(12, 8),
                Size = Theme.PS(238, 48)
            };
            _authBox.Controls.Add(_authStatus);

            _authBtn = new RoundButton
            {
                Text = "U盘认证",
                Icon = IconKind.Usb,
                Size = Theme.PS(108, 34),
                Soft = true,
                Fill = Theme.Card,
                OutlineColor = Theme.Primary,
                Font = Theme.Caption(),
                Location = Theme.PP(252, 15)
            };
            _authBtn.Click += (s, e) => AuthorizeRegister();
            _authBox.Controls.Add(_authBtn);

            _register = new RoundButton
            {
                Text = "注册并登录",
                Icon = IconKind.Add,
                Font = Theme.BodyStrong(),
                Size = Theme.PS(372, 40),
                Fill = Theme.Primary,
                Location = Theme.PP(0, 312)
            };
            _register.Click += (s, e) => DoRegister();
            host.Controls.Add(_register);

            _rMsg = new Label
            {
                Text = "",
                Font = Theme.Caption(),
                ForeColor = Theme.Red,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = Theme.PP(0, 358),
                Size = Theme.PS(372, 20)
            };
            host.Controls.Add(_rMsg);
        }

        // ---------------- 登录流程 ----------------

        private void SetBusy(bool busy)
        {
            _busy = busy;
            if (_login != null)
            {
                _login.Enabled = !busy;
                _login.Text = busy ? "登录中…" : "登  录";
                _login.Invalidate();
            }
            if (_guest != null) _guest.Enabled = !busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private void StartOpTimeout(Action onTimeout)
        {
            StopOpTimeout();
            _opTimeout = new System.Windows.Forms.Timer { Interval = 6000 };
            _opTimeout.Tick += (s, e) => { StopOpTimeout(); try { onTimeout(); } catch { } };
            _opTimeout.Start();
        }

        private void StopOpTimeout()
        {
            if (_opTimeout != null) { _opTimeout.Stop(); _opTimeout.Dispose(); _opTimeout = null; }
        }

        private void DoPasswordLogin()
        {
            if (_busy) return;
            string u = _user.Text.Trim();
            string p = _pwd.Text;
            if (u.Length == 0) { Fail("请输入用户名"); return; }

            Account acc = FindAccount(u);
            if (acc == null) { Fail("用户名或密码不正确"); return; }
            if (!acc.Enabled) { Fail("该账号已被停用，请联系管理员"); return; }

            SetBusy(true);
            _msg.Text = "";
            StartOpTimeout(() => { SetBusy(false); Fail("登录超时，请重试"); });
            TaskCompat.Run(() =>
            {
                bool ok = false;
                try { ok = Auth.Verify(acc, p); } catch { }
                try
                {
                    if (IsHandleCreated && !IsDisposed)
                        BeginInvoke(new Action(() =>
                        {
                            StopOpTimeout();
                            SetBusy(false);
                            if (ok) FinishLogin(acc, "账号密码", null);
                            else Fail("用户名或密码不正确");
                        }));
                }
                catch (Exception ex) { Paths.Log("登录回调异常：" + ex.Message); }
            });
        }

        private void FinishLogin(Account acc, string method, string usbDrive)
        {
            StopOpTimeout();
            SetBusy(false);
            acc.LastLoginUtc = DateTime.UtcNow;
            SignIn(acc, method, usbDrive);
        }

        private void DoGuestLogin()
        {
            if (_busy) return;
            Result = Session.Guest();
            RaiseLoggedIn();
        }

        private void DoUsbLogin()
        {
            UsbKeyFile key;
            KeyCheck check;
            var drive = UsbKey.FindKeyDrive(out key, out check, _app.Store);

            if (drive == null || key == null)
            {
                _usbStatus.Text = "登录U盘状态：" + KeyCheckText.Msg(check);
                _usbStatus.ForeColor = Theme.Red;
                return;
            }

            Account acc = UsbKey.FindAccount(_app.Store, key.User);
            if (check == KeyCheck.NeedPin)
            {
                using (var dlg = new PinDialog(key.User, key.Display))
                {
                    if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                    if (!UsbKey.CheckPin(key, dlg.Pin)) { _usbStatus.Text = "U盘口令不正确"; _usbStatus.ForeColor = Theme.Red; return; }
                }
            }
            else if (check != KeyCheck.Ok)
            {
                _usbStatus.Text = KeyCheckText.Msg(check);
                _usbStatus.ForeColor = Theme.Red;
                return;
            }

            acc.LastLoginUtc = DateTime.UtcNow;
            SignIn(acc, "U盘登录", drive.Letter);
        }

        private void SignIn(Account acc, string method, string usbDrive)
        {
            Result = new Session
            {
                Role = acc.Role,
                User = acc.User,
                Display = Str.Blank(acc.Display) ? acc.User : acc.Display,
                Account = acc,
                LoginMethod = method,
                UsbDrive = usbDrive ?? ""
            };
            RaiseLoggedIn();
        }

        private void RaiseLoggedIn()
        {
            var h = LoggedIn;
            if (h != null) h(this, EventArgs.Empty);
        }

        private Account FindAccount(string user)
        {
            foreach (var a in _app.Store.Accounts)
                if (string.Equals(a.User, user, StringComparison.OrdinalIgnoreCase)) return a;
            return null;
        }

        private bool HasAdmin()
        {
            foreach (var a in _app.Store.Accounts)
                if (a.Role == Role.Admin && a.Enabled) return true;
            return false;
        }

        private void DetectUsbKey()
        {
            if (_usbStatus == null) return;
            UsbKeyFile key;
            KeyCheck check;
            var drive = UsbKey.FindKeyDrive(out key, out check, _app.Store);

            if (drive != null && key != null)
            {
                _usbStatus.ForeColor = Theme.Green;
                _usbStatus.Text = "已检测到登录U盘 " + drive.Letter + "\r\n"
                    + "账号：" + key.Display + "（" + key.Role + "）"
                    + (check == KeyCheck.NeedPin ? "，需要口令" : "，可直接登录") + "\r\n"
                    + (key.ExpireTime().HasValue ? "有效期至 " + key.ExpireTime().Value.ToString("yyyy-MM-dd") : "永久有效");
            }
            else
            {
                var list = UsbKey.Drives(true);
                _usbStatus.ForeColor = Theme.TextSub;
                _usbStatus.Text = list.Count == 0
                    ? "未检测到可移动磁盘。\r\n插入已制作的登录U盘后即可免密登录；\r\n也可以由管理员现场“制作登录U盘”。"
                    : "检测到 " + list.Count + " 个可移动磁盘，\r\n但其中没有本程序制作的登录密钥。";
            }
        }

        private void BuildAccountLinks()
        {
            _accountLinks.Controls.Clear();
            if (!HasAdmin())
            {
                _accountsHint.Text = _app.Store.Accounts.Count == 0
                    ? "本机还没有任何账号：请先创建管理员账号"
                    : "本机还没有管理员账号：请先创建管理员（无需U盘）";
                AddLink("创建管理员账号", true, () => CreateFirstAdmin());
                return;
            }

            _accountsHint.Text = "本机已注册账号（点一下填入用户名）";
            int shown = 0;
            foreach (var a in _app.Store.Accounts)
            {
                if (shown >= 6) break;
                shown++;
                var acc = a;
                AddLink(acc.User + " · " + RoleText.Name(acc.Role) + (acc.Enabled ? "" : "（已停用）"), false, () =>
                {
                    _tabs.SelectedIndex = 0;
                    ShowTab(0);
                    _user.Text = acc.User;
                    _pwd.Box.Focus();
                });
            }
        }

        private void AddLink(string text, bool accent, Action onClick)
        {
            var b = new RoundButton
            {
                Text = text,
                Size = Theme.PS(TextRenderer.MeasureText(text, Theme.Caption()).Width + 24, 26),
                Soft = true,
                Fill = accent ? Theme.PrimarySoft : Theme.Card,
                OutlineColor = accent ? Theme.Primary : Theme.BorderStrong,
                Font = Theme.Caption(),
                Radius = 13,
                Margin = new Padding(0, 0, Theme.P(8), 0)
            };
            b.Click += (s, e) => onClick();
            _accountLinks.Controls.Add(b);
        }

        private void CreateFirstAdmin()
        {
            using (var d = new FirstRunDialog(_app))
            {
                if (d.ShowDialog(FindForm()) != DialogResult.OK || d.CreatedAccount == null) return;
                d.CreatedAccount.LastLoginUtc = DateTime.UtcNow;
                _app.Store.Save();
                SignIn(d.CreatedAccount, "首次创建", null);
            }
        }

        private void MakeUsbFromLogin()
        {
            using (var auth = new ReAuthDialog(_app, "制作登录U盘需要管理员验证"))
            {
                if (auth.ShowDialog(FindForm()) != DialogResult.OK) return;
                using (var dlg = new UsbCreateDialog(_app))
                    dlg.ShowDialog(FindForm());
            }
            DetectUsbKey();
        }

        private void Fail(string msg)
        {
            _msg.Text = msg;
            _msg.ForeColor = Theme.Red;
        }

        private void RegFail(string msg)
        {
            _rMsg.Text = msg;
            _rMsg.ForeColor = Theme.Red;
        }

        // ---------------- 注册 ----------------

        private void UpdateRegisterGate()
        {
            if (_authBox == null) return;

            if (!HasAdmin())
            {
                _authBox.BackColor = Theme.PrimarySoft;
                _authStatus.ForeColor = Theme.Primary;
                _authStatus.Text = "本机还没有管理员账号，可直接创建管理员（无需U盘）。\r\n创建后，别人注册成员账号才需要管理员U盘认证。";
                _authBtn.Visible = false;
                if (_register != null) { _register.Text = "创建管理员账号"; _register.Enabled = true; }
                _authBox.Invalidate();
                return;
            }

            bool need = _app.Settings.RegisterNeedsAdminUsb;
            bool ok = !need || _registerAuthorized;
            if (_register != null) _register.Text = "注册并登录";

            if (!need)
            {
                _authBox.BackColor = Theme.Bg;
                _authStatus.ForeColor = Theme.TextFaint;
                _authStatus.Text = "当前设置允许直接注册。\r\n管理员可在“系统设置 → 偏好设置”里开启 U 盘认证。";
                _authBtn.Visible = false;
            }
            else if (ok)
            {
                _authBox.BackColor = Theme.GreenSoft;
                _authStatus.ForeColor = Theme.Green;
                _authStatus.Text = "已通过管理员U盘认证（" + _authorizedBy + "）。\r\n可以注册新账号了。";
                _authBtn.Text = "重新认证";
                _authBtn.Visible = true;
            }
            else
            {
                _authBox.BackColor = Theme.PrimarySoft;
                _authStatus.ForeColor = Theme.TextSub;
                var drives = UsbKey.Drives(true);
                _authStatus.Text = "注册成员账号需要管理员登录U盘认证：请插入管理员的登录U盘，\r\n点右侧“U盘认证”。" + (drives.Count == 0 ? "（当前未检测到可移动磁盘）" : "");
                _authBtn.Text = "U盘认证";
                _authBtn.Visible = true;
            }
            _authBox.Invalidate();
            if (_register != null) _register.Enabled = ok;
        }

        private void AuthorizeRegister()
        {
            UsbKeyFile key;
            KeyCheck check;
            var drive = UsbKey.FindKeyDrive(out key, out check, _app.Store);

            if (drive == null || key == null) { AuthFail("未检测到可用的登录U盘：" + KeyCheckText.Msg(check)); return; }
            if (check == KeyCheck.NeedPin)
            {
                using (var d = new PinDialog(key.User, key.Display))
                {
                    if (d.ShowDialog(FindForm()) != DialogResult.OK) return;
                    if (!UsbKey.CheckPin(key, d.Pin)) { AuthFail("U盘口令不正确"); return; }
                }
            }
            else if (check != KeyCheck.Ok)
            {
                AuthFail(KeyCheckText.Msg(check));
                return;
            }

            var acc = UsbKey.FindAccount(_app.Store, key.User);
            if (acc == null || acc.Role != Role.Admin || !acc.Enabled)
            {
                AuthFail("该U盘不是管理员钥匙（当前：" + (acc == null ? "未知账号" : RoleText.Name(acc.Role)) + "），不能用于注册认证");
                return;
            }

            _registerAuthorized = true;
            _authorizedBy = acc.User + (Str.Blank(acc.Display) ? "" : " · " + acc.Display);
            Paths.Log("注册认证通过：管理员U盘 " + drive.Letter + " -> " + acc.User);
            UpdateRegisterGate();
            Toast.Show(this, "管理员U盘认证通过：" + acc.User, Theme.Green);
        }

        private void AuthFail(string msg)
        {
            _registerAuthorized = false;
            _authorizedBy = "";
            UpdateRegisterGate();
            _authStatus.ForeColor = Theme.Red;
            _authStatus.Text = msg;
            _authBox.BackColor = Theme.RedSoft;
            _authBox.Invalidate();
        }

        private void DoRegister()
        {
            _rMsg.Text = "";

            bool firstAdmin = !HasAdmin();
            if (!firstAdmin && _app.Settings.RegisterNeedsAdminUsb && !_registerAuthorized)
            {
                RegFail("注册成员账号需要管理员U盘认证，请先点“U盘认证”");
                UpdateRegisterGate();
                return;
            }

            string u = _rUser.Text.Trim();
            if (u.Length < 2) { RegFail("用户名至少 2 个字符"); return; }
            foreach (char c in u)
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.')) { RegFail("用户名只能使用字母、数字、下划线"); return; }
            if (FindAccount(u) != null) { RegFail("该用户名已被注册，请换一个"); return; }
            if (_rPwd.Text.Length < 3) { RegFail("密码至少 3 位"); return; }
            if (_rPwd.Text != _rPwd2.Text) { RegFail("两次输入的密码不一致"); return; }

            var acc = new Account
            {
                User = u,
                Display = Str.Blank(_rDisplay.Text) ? (firstAdmin ? "管理员" : u) : _rDisplay.Text.Trim(),
                Role = firstAdmin ? Role.Admin : Role.Member,
                Enabled = true,
                Note = firstAdmin
                    ? "首次创建的管理员（无需U盘）"
                    : "自行注册（成员权限）" + (_registerAuthorized ? "，管理员U盘认证：" + _authorizedBy : "")
            };
            string pwd = _rPwd.Text;

            if (_register != null) { _register.Enabled = false; _register.Text = firstAdmin ? "创建中…" : "注册中…"; }
            Cursor = Cursors.WaitCursor;
            StartOpTimeout(() =>
            {
                Cursor = Cursors.Default;
                RegFail(firstAdmin ? "创建管理员超时，请重试" : "注册超时，请重试");
                UpdateRegisterGate();
            });
            TaskCompat.Run(() =>
            {
                try
                {
                    try { Auth.SetPassword(acc, pwd); }
                    catch (Exception ex) { Paths.Log("注册口令派生失败：" + ex.Message); }
                    if (IsHandleCreated && !IsDisposed)
                        BeginInvoke(new Action(() => CompleteRegister(acc)));
                }
                catch (Exception ex) { Paths.Log("注册线程异常：" + ex.Message); }
            });
        }

        private void CompleteRegister(Account acc)
        {
            StopOpTimeout();
            try
            {
                Cursor = Cursors.Default;
                acc.LastLoginUtc = DateTime.UtcNow;
                _app.Store.Accounts.Add(acc);
                _app.Store.Save();
                Paths.Log((acc.Role == Role.Admin ? "创建管理员：" : "新账号注册：") + acc.User);
                SignIn(acc, acc.Role == Role.Admin ? "创建管理员" : "注册", null);
            }
            catch (Exception ex)
            {
                Paths.Log("注册完成阶段异常：" + ex);
                RegFail("创建失败：" + ex.Message);
                Cursor = Cursors.Default;
                UpdateRegisterGate();
            }
        }

        // ---------------- 开发自检入口 ----------------

        public void DebugShowTab(int index) { _tabs.SelectedIndex = index; ShowTab(index); }

        public void DebugLogin(string user, string pwd)
        {
            DebugShowTab(0);
            _user.Text = user;
            _pwd.Text = pwd;
            DoPasswordLogin();
        }

        public void DebugGuest() { DoGuestLogin(); }

        public void DebugRegister(string user, string display, string pwd, string pwd2)
        {
            DebugShowTab(2);
            _rUser.Text = user;
            _rDisplay.Text = display;
            _rPwd.Text = pwd;
            _rPwd2.Text = pwd2;
            DoRegister();
        }

        public string DebugRegisterMessage { get { return _rMsg == null ? "" : _rMsg.Text; } }

        public bool DebugNeedsUsbForRegister
        {
            get { return HasAdmin() && _app.Settings.RegisterNeedsAdminUsb && !_registerAuthorized; }
        }
    }
}
