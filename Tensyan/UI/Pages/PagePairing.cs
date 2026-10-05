using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Tensyan.Core;

namespace Tensyan.UI.Pages
{
    /// <summary>
    /// 「配对手机」页：显示 6 位配对码、已配对设备与运行日志。
    /// 手机在「同步」页输入这串数字即可配对；**配对成功后手机自动登录**，以后打开就用，
    /// 电脑这边只要程序在托盘里常驻，手机随时可以同步。
    /// </summary>
    public class PagePairing : PageBase
    {
        private Panel _codeCard;
        private Label _status;
        private Label _hint;
        private FlowLayoutPanel _devices;
        private TextBox _log;
        private System.Windows.Forms.Timer _timer;

        public PagePairing(App app) : base(app)
        {
            TitleText = "配对手机";
            SubText = "手机打开「同步」页输入下方数字即可配对，配对后自动登录";
            EnableTools(56);

            Tools.Controls.Add(MkBtn("换一个配对码", IconKind.Refresh, 150, null, true));
            (Tools.Controls[Tools.Controls.Count - 1] as RoundButton).Click += (s, e) =>
            {
                if (Lan.Pairing != null) { Lan.Pairing.NewCode(); Reload(); }
            };
            Tools.Controls.Add(MkBtn("打开防火墙提示", IconKind.Info, 150, null, true));
            (Tools.Controls[Tools.Controls.Count - 1] as RoundButton).Click += (s, e) => ShowFirewallHelp();

            // 配对码大卡片
            _codeCard = new Panel { BackColor = Color.Transparent };
            _codeCard.Paint += CodeCardPaint;
            Body.Controls.Add(_codeCard);

            _status = new Label { AutoSize = false, Font = Theme.Body(), ForeColor = Theme.TextSub, BackColor = Color.Transparent };
            Body.Controls.Add(_status);

            _hint = new Label
            {
                AutoSize = false,
                Font = Theme.Caption(),
                ForeColor = Theme.TextFaint,
                BackColor = Color.Transparent,
                Text = "手机端：打开 Tensyan → 点「同步」→ 在「配对电脑」里输入上面 6 位数字。\n"
                     + "配对成功后手机会自动登录，不需要在手机上输账号密码；电脑这边不用时可直接最小化到托盘。"
            };
            Body.Controls.Add(_hint);

            _devices = new FlowLayoutPanel
            {
                AutoScroll = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent
            };
            Body.Controls.Add(_devices);

            _log = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
                BackColor = Theme.Card,
                ForeColor = Theme.TextMain,
                Font = new Font("Consolas", Theme.PF(9f))
            };
            Body.Controls.Add(_log);

            // 后台线程来的日志要回到 UI 线程
            if (Lan.Service != null)
            {
                Lan.Service.Log += OnServiceLog;
                Lan.Service.DataMerged += OnDataMerged;
            }

            _timer = new System.Windows.Forms.Timer { Interval = 1000 };
            _timer.Tick += (s, e) => UpdateCode();
            _timer.Start();

            Reload();
        }

        // ---------------- 绘制 ----------------

        private void CodeCardPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, _codeCard.Width - 1, _codeCard.Height - 1);
            Theme.FillRounded(g, r, Theme.P(12), Theme.Card);
            Theme.StrokeRounded(g, r, Theme.P(12), Theme.Border, 1f);
            Theme.FillRounded(g, new Rectangle(0, 0, Theme.P(4), _codeCard.Height), 2, Theme.Primary);

            string code = Lan.Pairing == null ? "------" : Lan.Pairing.Code;
            int secs = Lan.Pairing == null ? 0 : Lan.Pairing.CodeSecondsLeft;

            using (var f = Theme.F(34f, FontStyle.Bold))
            using (var b = new SolidBrush(Theme.Primary))
            {
                var sz = g.MeasureString(code, f);
                float x = (_codeCard.Width - sz.Width) / 2f;
                float y = Theme.P(10);
                // 逐位画，位间距大一点更好念
                g.DrawString(code, f, b, x, y);
            }
            using (var f = Theme.Caption())
            using (var b = new SolidBrush(Theme.TextSub))
            {
                string s = "配对码有效期还剩 " + secs + " 秒";
                var sz = g.MeasureString(s, f);
                g.DrawString(s, f, b, (_codeCard.Width - sz.Width) / 2f, _codeCard.Height - Theme.P(26));
            }
        }

        private void UpdateCode()
        {
            try { _codeCard.Invalidate(); } catch { }
        }

        // ---------------- 布局 ----------------

        protected override void Layout3()
        {
            base.Layout3();
            int pad = Theme.P(16);
            int w = Body.ClientSize.Width - pad * 2;
            int y = pad;

            _codeCard.SetBounds(pad, y, w, Theme.P(92)); y += _codeCard.Height + pad;

            _status.SetBounds(pad, y, w, Theme.LineHeight(Theme.Body()) + Theme.P(4)); y += _status.Height + Theme.P(10);

            _hint.SetBounds(pad, y, w, Theme.LineHeight(Theme.Caption()) * 2 + Theme.P(10)); y += _hint.Height + Theme.P(10);

            int devH = Math.Max(Theme.P(120), Body.ClientSize.Height - y - Theme.P(190));
            _devices.SetBounds(pad, y, w, devH); y += devH + Theme.P(8);

            _log.SetBounds(pad, y, w, Math.Max(Theme.P(80), Body.ClientSize.Height - y - pad));
        }

        // ---------------- 数据 ----------------

        public override void Reload()
        {
            try
            {
                var svc = Lan.Service;
                string ip = LanService.LocalIp();
                bool running = svc != null && svc.Running;
                _status.Text = (running ? "● 正在等待手机连接" : "○ 服务未运行")
                    + "　端口 " + LanService.TcpPort
                    + "　本机地址 " + (string.IsNullOrEmpty(ip) ? "（未连接到局域网）" : ip)
                    + "　已配对 " + (Lan.Pairing == null ? 0 : Lan.Pairing.Data.Devices.Count) + " 台手机";
                _status.ForeColor = running ? Theme.Green : Theme.Amber;

                _devices.Controls.Clear();
                if (Lan.Pairing != null)
                {
                    foreach (var d in Lan.Pairing.Data.Devices)
                        _devices.Controls.Add(DeviceCard(d));
                    if (Lan.Pairing.Data.Devices.Count == 0)
                    {
                        var empty = new Label
                        {
                            Text = "还没有配对的手机。手机端输入上方数字即可配对。",
                            Font = Theme.Body(),
                            ForeColor = Theme.TextFaint,
                            AutoSize = true
                        };
                        _devices.Controls.Add(empty);
                    }
                }
                _codeCard.Invalidate();
            }
            catch (Exception ex)
            {
                Paths.Log("配对页刷新失败：" + ex);
            }
        }

        private Control DeviceCard(PairedDevice d)
        {
            var card = new Panel { BackColor = Color.Transparent, Width = Theme.P(320), Height = Theme.P(112), Margin = new Padding(0, 0, Theme.P(12), Theme.P(12)) };
            card.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                Theme.FillRounded(g, r, Theme.P(10), Theme.Card);
                Theme.StrokeRounded(g, r, Theme.P(10), Theme.Border, 1f);
                using (var b = new SolidBrush(Theme.TextMain))
                using (var f = Theme.BodyStrong())
                    g.DrawString(d.Name, f, b, Theme.P(14), Theme.P(12));
                using (var b = new SolidBrush(Theme.TextSub))
                using (var f = Theme.Caption())
                {
                    g.DrawString("绑定账号：" + d.AccountUser, f, b, Theme.P(14), Theme.P(40));
                    g.DrawString("最后同步：" + PrettyTime(d.LastSyncUtc), f, b, Theme.P(14), Theme.P(60));
                    g.DrawString("地址：" + (string.IsNullOrEmpty(d.LastIp) ? "—" : d.LastIp), f, b, Theme.P(14), Theme.P(80));
                }
            };
            var un = MkBtn("解除配对", IconKind.Trash, 110, Theme.Red, true);
            un.SetBounds(Theme.P(320) - Theme.P(122), Theme.P(12), Theme.P(110), Theme.P(32));
            un.Click += (s, e) =>
            {
                if (Lan.Pairing != null && Lan.Pairing.Unpair(d.Id))
                {
                    Append("已解除与「" + d.Name + "」的配对");
                    Reload();
                }
            };
            card.Controls.Add(un);
            return card;
        }

        private static string PrettyTime(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return "还没同步过";
            DateTime t;
            if (!DateTime.TryParse(iso, out t)) return iso;
            var span = DateTime.UtcNow - t;
            if (span.TotalSeconds < 60) return "刚刚";
            if (span.TotalMinutes < 60) return (int)span.TotalMinutes + " 分钟前";
            if (span.TotalHours < 24) return (int)span.TotalHours + " 小时前";
            return t.ToLocalTime().ToString("MM-dd HH:mm");
        }

        // ---------------- 日志 ----------------

        private void OnServiceLog(string msg)
        {
            try { BeginInvoke((Action)(() => Append(msg))); } catch { }
        }

        private void OnDataMerged()
        {
            try
            {
                BeginInvoke((Action)(() =>
                {
                    try { App.Raise(); } catch { }
                    Reload();
                }));
            }
            catch { }
        }

        private void Append(string msg)
        {
            try
            {
                _log.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + msg + "\r\n");
            }
            catch { }
        }

        private void ShowFirewallHelp()
        {
            MessageBox.Show(
                "手机连不上电脑时，多半是 Windows 防火墙挡了入站连接。\n\n"
                + "临时验证：控制面板 → Windows Defender 防火墙 → 允许应用通过防火墙，\n"
                + "把 Tensyan 的两个网络（专用/公用）都勾上。\n\n"
                + "需要放行的端口：\n"
                + "  · TCP " + LanService.TcpPort + "（同步与配对）\n"
                + "  · UDP " + LanService.UdpPort + "（让手机自动找到电脑）\n\n"
                + "手机与电脑必须在同一个 Wi-Fi（或电脑开热点给手机连）。",
                "手机连不上时怎么办", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
