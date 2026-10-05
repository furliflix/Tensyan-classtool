using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Tensyan.Setup
{
    /// <summary>安装 / 卸载界面（Fluent、无边框、免管理员、免运行库）。</summary>
    public class SetupForm : BaseForm
    {
        private readonly bool _uninstall;
        private TextBox _dirBox;
        private FlatButton _btnBrowse, _btnDefault, _btnDiag, _btnElevate, _btnMain, _btnCancel;
        private CheckBox _ckDesktop, _ckStartMenu, _ckLaunch, _ckData;
        private Label _info, _dirStatus;
        private ProgressStrip _bar;
        private Label _status;
        private Image _logo;
        private bool _running;
        private System.Windows.Forms.Timer _checkTimer;

        public SetupForm()
            : base(InstallCore.AppName + " 安装程序", 660, 512)
        {
            _uninstall = Program.UninstallMode;
            _logo = Program.ReadImage("logo.png");
            WindowTitle = _uninstall ? "卸载 " + InstallCore.AppName : InstallCore.AppName + " 安装程序";

            string defDir = Program.TargetDir;
            if (InstallCore.IsBlank(defDir)) defDir = InstallCore.ReadInstallDir();
            if (InstallCore.IsBlank(defDir)) defDir = InstallCore.DefaultDir;

            int w = ClientSize.Width;

            // 安装位置
            var lblDir = new Label
            {
                Text = _uninstall ? "卸载位置" : "安装位置",
                Font = Ui.Caption,
                ForeColor = Ui.TextSub,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(Ui.P(24), Ui.P(206))
            };
            Add(lblDir);

            var box = new Panel { BackColor = Ui.Card, Location = new Point(Ui.P(24), Ui.P(226)), Size = new Size(Ui.P(486), Ui.P(34)) };
            box.Paint += (s, e) => { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; Ui.Stroke(e.Graphics, new Rectangle(0, 0, box.Width - 1, box.Height - 1), Ui.P(4), Ui.BorderStrong); };
            _dirBox = new TextBox
            {
                Text = defDir,
                Font = Ui.Body,
                BorderStyle = BorderStyle.None,
                BackColor = Ui.Card,
                ForeColor = Ui.TextMain,
                Location = new Point(Ui.P(10), Ui.P(7)),
                Width = Ui.P(466)
            };
            if (!_uninstall) _dirBox.TextChanged += (s, e) => QueueDirCheck();
            box.Controls.Add(_dirBox);
            Add(box);

            _btnBrowse = new FlatButton("浏览…", 116, 34, false) { Location = new Point(Ui.P(520), Ui.P(226)) };
            _btnBrowse.Click += (s, e) => Browse();
            Add(_btnBrowse);

            // 边输入边校验，直接说清“能不能装、为什么不能装”
            _dirStatus = new Label
            {
                Font = Ui.Caption,
                ForeColor = Ui.TextFaint,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(Ui.P(24), Ui.P(264)),
                Size = new Size(Ui.P(486), Ui.P(34)),
                Text = ""
            };
            Add(_dirStatus);

            _btnDefault = new FlatButton("使用默认", 116, 30, false) { Location = new Point(Ui.P(520), Ui.P(266)) };
            _btnDefault.Click += (s, e) =>
            {
                _dirBox.Text = InstallCore.DefaultDir;
                QueueDirCheck();
            };
            if (!_uninstall) Add(_btnDefault);

            _btnDiag = new FlatButton("诊断", 116, 30, false) { Location = new Point(Ui.P(520), Ui.P(300)) };
            _btnDiag.Click += (s, e) => ShowDiagnosis();
            Add(_btnDiag);

            _btnElevate = new FlatButton("以管理员身份重试", 176, 38, true) { Location = new Point(Ui.P(24), Ui.P(462)), Visible = false };
            _btnElevate.Click += (s, e) => RetryElevated();
            Add(_btnElevate);

            if (!_uninstall)
            {
                _ckDesktop = Ck("创建桌面快捷方式", 24, 308, true);
                _ckStartMenu = Ck("创建开始菜单快捷方式", 232, 308, true);
                _ckLaunch = Ck("安装完成后启动 Tensyan", 24, 336, true);
            }
            else
            {
                _ckData = Ck("同时删除班级数据（data 文件夹，建议保留）", 24, 308, false);
            }

            _info = new Label
            {
                Font = Ui.Caption,
                ForeColor = Ui.TextSub,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(Ui.P(24), Ui.P(366)),
                Size = new Size(w - Ui.P(48), Ui.P(40)),
                Text = _uninstall
                    ? "卸载会删除程序文件、桌面/开始菜单快捷方式，并移除“应用和功能”里的条目。\r\n班级数据默认保留，方便以后重装继续使用。"
                    : (InstallCore.IsElevated()
                        ? "当前以管理员身份运行，可安装到任意目录。"
                        : "当前用户安装，不需要管理员。")
#if XP_PAYLOAD
                      + "本版本为 Windows XP 特别版（32 位 / .NET Framework 3.5）：目标电脑需已装 .NET Framework 3.5（XP SP3 一般都有）。"
#elif NETFX_PAYLOAD
                      + "本版本为 32 位（.NET Framework 4.8）：目标电脑需已装 .NET Framework 4.8——Win10 1903+/Win11 自带，Win7 SP1 / 8.1 需自行安装。"
#else
                      + "程序自带 .NET 运行库，目标电脑无需另装组件。"
#endif
            };
            Add(_info);

            _bar = new ProgressStrip { Location = new Point(Ui.P(24), Ui.P(412)), Size = new Size(w - Ui.P(48), Ui.P(6)) };
            Add(_bar);

            _status = new Label
            {
                Text = "",
                Font = Ui.Caption,
                ForeColor = Ui.TextFaint,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(Ui.P(24), Ui.P(424)),
                Size = new Size(w - Ui.P(48), Ui.P(36))
            };
            Add(_status);

            _btnMain = new FlatButton(_uninstall ? "卸载" : "安装", 132, 38, true) { Location = new Point(w - Ui.P(156), Ui.P(462)) };
            if (_uninstall) _btnMain.Danger = true;
            _btnMain.Click += (s, e) => StartWork();
            Add(_btnMain);

            _btnCancel = new FlatButton("取消", 116, 38, false) { Location = new Point(w - Ui.P(282), Ui.P(462)) };
            _btnCancel.Click += (s, e) => Close();
            Add(_btnCancel);

            if (BtnClose != null) Add(BtnClose);

            if (!_uninstall) BeginInvoke(new Action(RunDirCheck));
        }

        /// <summary>输入防抖后校验目标目录，并把“能不能装/为什么不能装”写在界面上。</summary>
        private void QueueDirCheck()
        {
            if (_checkTimer == null)
            {
                _checkTimer = new System.Windows.Forms.Timer { Interval = 350 };
                _checkTimer.Tick += (s, e) => { _checkTimer.Stop(); RunDirCheck(); };
            }
            _checkTimer.Stop();
            _checkTimer.Start();
        }

        private void RunDirCheck()
        {
            if (_dirStatus == null || _running) return;
            string dir = (_dirBox.Text ?? "").Trim();
            if (dir.Length == 0) { SetDirStatus("请输入安装位置", Ui.TextFaint); return; }

            string reason;
            bool willCreate;
            string text;
            if (InstallCore.ProbeTarget(dir, out reason, out willCreate))
                text = willCreate ? "✓ 可以安装（该目录会在安装时创建）" : "✓ 这个位置可以安装";
            else
                text = "✗ 不能安装到这里：" + reason;

#if XP_PAYLOAD
            // XP 特别版：把 .NET Framework 3.5 的检测结果写在说明行（状态栏只有两行位置）
            string fxDetail;
            bool hasFx = InstallCore.HasNetFx35(out fxDetail);
            if (_info != null)
                _info.Text = "本版本为 Windows XP 特别版（32 位 / .NET Framework 3.5）。\r\n运行库检查：" + (hasFx ? "" : "⚠ ") + fxDetail
                    + (hasFx ? "（可以安装）" : " —— 请先在目标电脑上安装 .NET Framework 3.5");
            SetDirStatus(text, text.StartsWith("✓") ? Ui.Green : Ui.Red);
#elif NETFX_PAYLOAD
            // .NET Framework 版：把运行库情况写在说明行
            string fxDetail;
            bool hasFx = InstallCore.HasNetFx48(out fxDetail);
            if (_info != null)
                _info.Text = "本版本为 32 位（.NET Framework 4.8）：Win10 1903+/Win11 自带运行库，Win7 SP1 / 8.1 需自行安装。\r\n运行库检查："
                    + (hasFx ? "" : "⚠ ") + fxDetail + (hasFx ? "（可以安装）" : " —— 请先安装 .NET Framework 4.8");
            SetDirStatus(text, text.StartsWith("✓") ? Ui.Green : Ui.Red);
#else
            SetDirStatus(text, text.StartsWith("✓") ? Ui.Green : Ui.Red);
#endif
        }

        private void SetDirStatus(string text, Color color)
        {
            _dirStatus.ForeColor = color;
            _dirStatus.Text = text;
        }

        /// <summary>显示诊断报告（分别探测临时目录/用户目录/目标目录，并给结论）。</summary>
        private void ShowDiagnosis()
        {
            string report = InstallCore.Diagnose((_dirBox.Text ?? "").Trim());
            using (var d = new ReportDialog("安装诊断", report))
                d.ShowDialog(this);
        }

        /// <summary>以管理员身份重新启动自己（UAC）。</summary>
        private void RetryElevated()
        {
            string args = "--dir \"" + (_dirBox.Text ?? "").Trim() + "\" --elevated";
            if (InstallCore.RestartElevated(args)) Close();
            else MessageBox.Show(this, "没能以管理员身份启动（可能取消了 UAC 提示）。\r\n可以改用“使用默认”装到用户目录。",
                "以管理员身份重试", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private CheckBox Ck(string text, int x, int y, bool @checked)
        {
            var ck = new CheckBox
            {
                Text = text,
                Checked = @checked,
                Font = Ui.Caption,
                ForeColor = Ui.TextMain,
                BackColor = Color.Transparent,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(Ui.P(x), Ui.P(y))
            };
            Add(ck);
            return ck;
        }

        private void Browse()
        {
            using (var d = new FolderBrowserDialog())
            {
                d.Description = "选择安装位置";
                d.SelectedPath = _dirBox.Text;
                if (d.ShowDialog(this) == DialogResult.OK) _dirBox.Text = d.SelectedPath;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (_logo != null)
            {
                int lw = Ui.P(206);
                int lh = (int)(lw * (double)_logo.Height / _logo.Width);
                g.DrawImage(_logo, new Rectangle(Ui.P(24), Ui.P(64), lw, lh));
                TextRenderer.DrawText(g, (_uninstall ? "卸载程序" : "安装程序") + " · v" + InstallCore.Version + " · " + InstallCore.Publisher
                    + (InstallCore.IsElevated() ? " · 管理员模式" : ""),
                    Ui.Caption, new Point(Ui.P(26), Ui.P(64) + lh + Ui.P(6)), Ui.TextFaint);
            }
            else
            {
                TextRenderer.DrawText(g, InstallCore.AppName, Ui.Title, new Point(Ui.P(24), Ui.P(72)), Ui.TextMain);
                TextRenderer.DrawText(g, (_uninstall ? "卸载程序" : "安装程序") + " · v" + InstallCore.Version,
                    Ui.Caption, new Point(Ui.P(26), Ui.P(102)), Ui.TextFaint);
            }

            using (var pen = new Pen(Ui.Border))
                g.DrawLine(pen, Ui.P(24), Ui.P(196), Width - Ui.P(24), Ui.P(196));
        }

        private void StartWork()
        {
            if (_running) return;
            string dir = _dirBox.Text.Trim();
            if (dir.Length == 0) { SetDirStatus("请输入安装位置", Ui.Red); return; }

            if (!_uninstall)
            {
                string reason;
                bool willCreate;
                if (!InstallCore.ProbeTarget(dir, out reason, out willCreate))
                {
                    SetDirStatus("✗ 不能安装到这里：" + reason, Ui.Red);
                    _status.ForeColor = Ui.Red;
                    _status.Text = "请换一个可写的位置，或点右侧“使用默认”装到 " + InstallCore.DefaultDir;
                    return;
                }
            }

            if (_uninstall)
            {
                if (!Directory.Exists(dir)) { _status.ForeColor = Ui.Red; _status.Text = "目录不存在：" + dir; return; }
                bool removeData = _ckData != null && _ckData.Checked;
                if (MessageBox.Show(this,
                        removeData
                            ? "将卸载 Tensyan，并删除全部班级数据（不可恢复）。确定继续吗？"
                            : "将卸载 Tensyan，班级数据会保留在 data 文件夹。确定继续吗？",
                        "卸载 " + InstallCore.AppName, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
                    return;
            }

            _running = true;
            _btnMain.Enabled = false;
            _btnBrowse.Enabled = false;
            if (_btnCancel != null) _btnCancel.Text = "后台继续";

            if (InstallCore.IsAppRunning())
            {
                InstallCore.KillApp();
                _status.Text = "已关闭正在运行的 Tensyan…";
            }

            var th = new Thread(() => Work(dir)) { IsBackground = true };
            th.Start();
        }

        private void Work(string dir)
        {
            try
            {
                if (_uninstall)
                {
                    bool removeData = _ckData != null && _ckData.Checked;
                    Ui2(() => { _bar.Value = 20; _status.ForeColor = Ui.TextSub; _status.Text = "正在卸载…"; });
                    int code = InstallCore.Uninstall(dir, removeData, m => Ui2(() => _status.Text = m));
                    Ui2(() =>
                    {
                        _bar.Value = 100;
                        if (code == 0)
                        {
                            _status.ForeColor = Ui.Green;
                            _status.Text = removeData ? "卸载完成，数据已删除。" : "卸载完成，数据保留在 " + Path.Combine(dir, InstallCore.DataFolder);
                            _btnMain.Text = "关闭";
                            _btnMain.Enabled = true;
                            _btnMain.Click += (s, e) => Close();
                            if (_btnCancel != null) _btnCancel.Visible = false;
                        }
                        else
                        {
                            _status.ForeColor = Ui.Red;
                            _status.Text = "卸载未完全成功，详见日志：" + InstallCore.LogPath;
                            _btnMain.Enabled = true;
                        }
                        _running = false;
                    });
                    return;
                }

                string writeReason;
                if (!InstallCore.CanWriteTo(dir, out writeReason))
                {
                    string report = InstallCore.Diagnose(dir);
                    bool confined = report.Contains("运行在受限环境");
                    Ui2(() =>
                    {
                        _status.ForeColor = Ui.Red;
                        _status.Text = confined
                            ? "✗ 任何位置都写不进去：本程序正运行在受限环境里。\r\n   请在 Windows 资源管理器里直接双击本安装程序再试（不要从其它工具/终端里代跑）。"
                            : "✗ 安装失败：" + writeReason + "\r\n   可点“使用默认”换位置，或点左下角“以管理员身份重试”。";
                        SetDirStatus("✗ 不能安装到这里：" + writeReason, Ui.Red);
                        _btnMain.Enabled = true;
                        _btnElevate.Visible = !confined && !Program.Elevated;
                        _running = false;
                    });
                    return;
                }

                Ui2(() => { _status.ForeColor = Ui.TextSub; _status.Text = "正在准备…"; _bar.Value = 3; });

                if (!Program.HasPayload)
                {
                    Ui2(() =>
                    {
                        _status.ForeColor = Ui.Red;
                        _status.Text = "安装包不完整：缺少应用文件。请重新下载安装包。";
                        _btnMain.Enabled = true;
                        _running = false;
                    });
                    return;
                }

                int n = Program.ExtractPayload(dir, (pct, msg) =>
                    Ui2(() =>
                    {
                        _bar.Value = Math.Min(88, 3 + (int)(pct * 0.85));
                        _status.Text = msg;
                    }));

                Ui2(() => { _bar.Value = 92; _status.Text = "正在写入卸载信息…"; });
                Program.WriteUninstaller(dir);
                bool registered = InstallCore.WriteUninstallEntry(dir, InstallCore.DirSizeKb(dir));
                if (!registered)
                    InstallCore.Log("提示：未能登记到“应用和功能”，可用安装目录里的 " + InstallCore.UninstallerName + " 卸载");
                InstallCore.GrantUserAccess(dir);

                if (!Program.NoShortcuts)
                {
                    bool desktop = _ckDesktop == null || _ckDesktop.Checked;
                    bool startMenu = _ckStartMenu == null || _ckStartMenu.Checked;
                    Ui2(() => { _bar.Value = 96; _status.Text = "正在创建快捷方式…"; });
                    InstallCore.CreateShortcuts(dir, desktop, startMenu);
                }

                Ui2(() =>
                {
                    _bar.Value = 100;
                    _status.ForeColor = Ui.Green;
                    _status.Text = "安装完成（" + n + " 个文件）→ " + dir;
                    _btnMain.Text = "完成";
                    _btnMain.Click += (s, e) => Close();
                    if (_btnCancel != null) _btnCancel.Visible = false;
                    _running = false;
                });

                bool launch = !Program.NoLaunch && (_ckLaunch == null || _ckLaunch.Checked);
                if (launch)
                {
                    Thread.Sleep(700);
                    try
                    {
                        Process.Start(new ProcessStartInfo(Path.Combine(dir, InstallCore.ExeName)) { WorkingDirectory = dir });
                        Ui2(() => { _status.Text = "安装完成，已启动 Tensyan。"; Close(); });
                    }
                    catch (Exception ex) { InstallCore.Log("启动失败: " + ex.Message); }
                }
            }
            catch (Exception ex)
            {
                InstallCore.Log("安装异常: " + ex);
                string report = InstallCore.Diagnose(dir);
                bool confined = report.Contains("运行在受限环境");
                bool denied = ex is UnauthorizedAccessException;
                Ui2(() =>
                {
                    _status.ForeColor = Ui.Red;
                    _status.Text = "出错了：" + ex.Message
                        + (confined ? "\r\n   诊断：本程序运行在受限环境里，请在资源管理器里直接双击运行。" : "\r\n   日志：" + InstallCore.LogPath);
                    _btnMain.Enabled = true;
                    _btnElevate.Visible = denied && !confined && !Program.Elevated;
                    _running = false;
                });
            }
        }

        private void Ui2(Action a)
        {
            try
            {
                if (IsDisposed) return;
                if (InvokeRequired) BeginInvoke(a);
                else a();
            }
            catch { }
        }
    }
}
