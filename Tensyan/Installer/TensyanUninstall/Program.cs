using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Tensyan.Setup
{
    internal static class Program
    {
        internal static bool Silent;
        internal static bool RemoveData;

        [STAThread]
        private static void Main(string[] args)
        {
            Ui.SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            foreach (var raw in args)
            {
                string a = raw.ToLowerInvariant();
                if (a == "--silent" || a == "/s") Silent = true;
                else if (a == "--remove-data") RemoveData = true;
            }

            string dir = InstallCore.ReadInstallDir();
            if (InstallCore.IsBlank(dir))
                dir = Path.GetDirectoryName(Application.ExecutablePath);

            InstallCore.Log("=== TensyanUninstall 启动 目录=" + dir + " 静默=" + Silent + " 删除数据=" + RemoveData);

            // 装在 Program Files / 注册在 HKLM 的安装，卸载需要管理员权限 → 自动提权重来一遍
            if (InstallCore.NeedsElevation(dir) && !InstallCore.IsElevated())
            {
                string elevArgs = (Silent ? "--silent" : "") + (RemoveData ? " --remove-data" : "");
                if (InstallCore.RestartElevated(elevArgs.Trim()))
                {
                    InstallCore.Log("已请求以管理员身份重新卸载");
                    return;
                }
                InstallCore.Log("提权失败，继续以当前权限尝试卸载");
            }

            if (Silent)
            {
                if (InstallCore.IsAppRunning()) InstallCore.KillApp();
                int code = InstallCore.Uninstall(dir, RemoveData, m => InstallCore.Log(m));
                Environment.Exit(code);
            }

            Application.Run(new UninstallForm(dir));
        }
    }

    /// <summary>卸载界面。</summary>
    public class UninstallForm : BaseForm
    {
        private readonly string _dir;
        private CheckBox _ckData;
        private FlatButton _btnMain, _btnCancel;
        private ProgressStrip _bar;
        private Label _status;
        private bool _running;

        public UninstallForm(string dir)
            : base("卸载 " + InstallCore.AppName, 620, 420)
        {
            _dir = dir;
            int w = ClientSize.Width;

            Add(new Label
            {
                Text = "卸载 " + InstallCore.AppName,
                Font = Ui.Title,
                ForeColor = Ui.TextMain,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(Ui.P(24), Ui.P(70))
            });

            Add(new Label
            {
                Text = "版本 " + InstallCore.Version + " · " + InstallCore.Publisher,
                Font = Ui.Caption,
                ForeColor = Ui.TextFaint,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(Ui.P(26), Ui.P(102))
            });

            Add(new Label
            {
                Text = "将从以下位置卸载：",
                Font = Ui.Caption,
                ForeColor = Ui.TextSub,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(Ui.P(24), Ui.P(140))
            });

            Add(new Label
            {
                Text = dir,
                Font = Ui.Body,
                ForeColor = Ui.TextMain,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(Ui.P(24), Ui.P(162)),
                Size = new Size(w - Ui.P(48), Ui.P(24))
            });

            _ckData = new CheckBox
            {
                Text = "同时删除班级数据（data 文件夹，建议保留）",
                Checked = false,
                Font = Ui.Caption,
                ForeColor = Ui.TextMain,
                BackColor = Color.Transparent,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(Ui.P(24), Ui.P(202))
            };
            Add(_ckData);

            Add(new Label
            {
                Text = "卸载会删除程序文件、快捷方式，并移除“应用和功能”里的条目。\r\n默认保留班级数据（学生名单、积分记录、账号），重装后可以直接继续使用。",
                Font = Ui.Caption,
                ForeColor = Ui.TextSub,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(Ui.P(24), Ui.P(236)),
                Size = new Size(w - Ui.P(48), Ui.P(48))
            });

            _bar = new ProgressStrip { Location = new Point(Ui.P(24), Ui.P(302)), Size = new Size(w - Ui.P(48), Ui.P(6)) };
            Add(_bar);

            _status = new Label
            {
                Text = "",
                Font = Ui.Caption,
                ForeColor = Ui.TextFaint,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(Ui.P(24), Ui.P(314)),
                Size = new Size(w - Ui.P(48), Ui.P(20))
            };
            Add(_status);

            _btnMain = new FlatButton("卸载", 132, 38, true) { Danger = true, Location = new Point(w - Ui.P(156), Ui.P(366)) };
            _btnMain.Click += (s, e) => StartUninstall();
            Add(_btnMain);

            _btnCancel = new FlatButton("取消", 116, 38, false) { Location = new Point(w - Ui.P(282), Ui.P(366)) };
            _btnCancel.Click += (s, e) => Close();
            Add(_btnCancel);

            if (BtnClose != null) Add(BtnClose);
        }

        private void StartUninstall()
        {
            if (_running) return;
            if (MessageBox.Show(this,
                    _ckData.Checked
                        ? "将卸载 Tensyan 并删除全部班级数据（不可恢复）。确定继续吗？"
                        : "将卸载 Tensyan，班级数据会保留。确定继续吗？",
                    "卸载确认", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
                return;

            _running = true;
            _btnMain.Enabled = false;
            _btnCancel.Text = "后台继续";
            _status.ForeColor = Ui.TextSub;
            _status.Text = "正在卸载…";
            _bar.Value = 15;

            var th = new Thread(() =>
            {
                if (InstallCore.IsAppRunning()) InstallCore.KillApp();
                int code = InstallCore.Uninstall(_dir, _ckData.Checked, m => Set(() =>
                {
                    _bar.Value = Math.Min(95, _bar.Value + 10);
                    _status.Text = m;
                }));
                Set(() =>
                {
                    _bar.Value = 100;
                    if (code == 0)
                    {
                        _status.ForeColor = Ui.Green;
                        _status.Text = _ckData.Checked
                            ? "卸载完成，数据已删除。"
                            : "卸载完成，数据保留在 " + Path.Combine(_dir, InstallCore.DataFolder);
                        _btnMain.Text = "关闭";
                        _btnMain.Enabled = true;
                        _btnMain.Click += (s, e) => Close();
                        _btnCancel.Visible = false;
                    }
                    else
                    {
                        _status.ForeColor = Ui.Red;
                        _status.Text = "卸载未完全成功，详见日志：" + InstallCore.LogPath;
                        _btnMain.Enabled = true;
                    }
                    _running = false;
                });
            }) { IsBackground = true };
            th.Start();
        }

        private void Set(Action a)
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
