using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Tensyan.Setup
{
    /// <summary>安装程序的 Fluent 视觉令牌（与主程序同一套配色）。</summary>
    public static class Ui
    {
        public static readonly Color Bg = Color.FromArgb(243, 243, 243);
        public static readonly Color Card = Color.White;
        public static readonly Color Border = Color.FromArgb(229, 229, 229);
        public static readonly Color BorderStrong = Color.FromArgb(209, 209, 209);
        public static readonly Color TextMain = Color.FromArgb(27, 27, 27);
        public static readonly Color TextSub = Color.FromArgb(97, 97, 97);
        public static readonly Color TextFaint = Color.FromArgb(138, 138, 138);
        public static readonly Color Primary = Color.FromArgb(0, 95, 184);
        public static readonly Color PrimaryHover = Color.FromArgb(16, 110, 190);
        public static readonly Color Green = Color.FromArgb(15, 123, 15);
        public static readonly Color Amber = Color.FromArgb(157, 93, 0);
        public static readonly Color Red = Color.FromArgb(196, 43, 28);

        private static float _dpi = 96f;

        public static void InitDpi(Control c)
        {
            try { using (var g = c.CreateGraphics()) _dpi = g.DpiX; } catch { _dpi = 96f; }
            if (_dpi <= 0) _dpi = 96f;
        }

        public static int P(int v) { return (int)Math.Round(v * _dpi / 96.0); }

        public static Font Body { get { return new Font("Microsoft YaHei UI", 10f); } }
        public static Font BodyStrong { get { return new Font("Microsoft YaHei UI", 10f, FontStyle.Bold); } }
        public static Font Caption { get { return new Font("Microsoft YaHei UI", 8.5f); } }
        public static Font Title { get { return new Font("Microsoft YaHei UI", 13f, FontStyle.Bold); } }

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            int d = Math.Max(2, radius * 2);
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Fill(Graphics g, Rectangle r, int radius, Color c)
        {
            using (var path = Round(r, radius))
            using (var b = new SolidBrush(c)) g.FillPath(b, path);
        }

        public static void Stroke(Graphics g, Rectangle r, int radius, Color c, float w = 1f)
        {
            using (var path = Round(r, radius))
            using (var p = new Pen(c, w)) g.DrawPath(p, path);
        }

        public static void EnableRoundedCorners(IntPtr handle)
        {
            try
            {
                if (Environment.OSVersion.Version.Major < 6) return;   // XP 没有 DWM
                int pref = 2;   // DWMWCP_ROUND
                DwmSetWindowAttribute(handle, 33, ref pref, sizeof(int));
            }
            catch { }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();
    }

    /// <summary>自绘扁平按钮（Primary / Subtle / Outline）。</summary>
    public class FlatButton : Control
    {
        public bool Primary;
        public bool Danger;
        private bool _hover, _down;

        public FlatButton(string text, int w, int h, bool primary)
        {
            Text = text;
            Primary = primary;
            Font = Ui.Body;
            Size = new Size(Ui.P(w), Ui.P(h));
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Ui.Bg);

            Color fill, border, text;
            if (Primary)
            {
                Color baseC = Danger ? Ui.Red : Ui.Primary;
                fill = Enabled ? (_down ? Darken(baseC) : (_hover ? Lighten(baseC) : baseC)) : Color.FromArgb(200, 200, 200);
                border = fill;
                text = Color.White;
            }
            else
            {
                fill = Enabled ? (_down ? Color.FromArgb(238, 238, 238) : (_hover ? Color.FromArgb(246, 246, 246) : Ui.Card)) : Ui.Bg;
                border = Ui.BorderStrong;
                text = Enabled ? Ui.TextMain : Ui.TextFaint;
            }

            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.Fill(g, r, Ui.P(4), fill);
            Ui.Stroke(g, r, Ui.P(4), border);
            TextRenderer.DrawText(g, Text, Font, r, text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private static Color Lighten(Color c)
        {
            return Color.FromArgb(Math.Min(255, c.R + 16), Math.Min(255, c.G + 16), Math.Min(255, c.B + 16));
        }

        private static Color Darken(Color c)
        {
            return Color.FromArgb(Math.Max(0, c.R - 18), Math.Max(0, c.G - 18), Math.Max(0, c.B - 18));
        }
    }

    /// <summary>纯文本报告窗口（诊断报告、日志）。</summary>
    public class ReportDialog : BaseForm
    {
        public ReportDialog(string title, string text)
            : base(title, 700, 470)
        {
            var box = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = new Font("Courier New", 9f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Ui.Card,
                ForeColor = Ui.TextMain,
                Text = text ?? "",
                Location = new Point(Ui.P(20), Ui.P(60)),
                Size = new Size(ClientSize.Width - Ui.P(40), ClientSize.Height - Ui.P(124))
            };
            Add(box);

            var copy = new FlatButton("复制全部", 116, 34, false) { Location = new Point(Ui.P(20), ClientSize.Height - Ui.P(54)) };
            copy.Click += (s, e) => { try { Clipboard.SetText(box.Text); } catch { } };
            Add(copy);

            var ok = new FlatButton("关闭", 116, 34, true) { Location = new Point(ClientSize.Width - Ui.P(136), ClientSize.Height - Ui.P(54)) };
            ok.Click += (s, e) => Close();
            Add(ok);

            if (BtnClose != null) Add(BtnClose);
        }
    }

    /// <summary>细进度条。</summary>
    public class ProgressStrip : Control
    {
        private int _value;

        public ProgressStrip()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Ui.Caption;
        }

        public int Value
        {
            get { return _value; }
            set { _value = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Ui.Bg);
            var full = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.Fill(g, full, Math.Max(2, Height / 2), Color.FromArgb(225, 225, 225));
            if (_value > 0)
            {
                int w = Math.Max(Ui.P(4), (int)(Width * _value / 100.0));
                Ui.Fill(g, new Rectangle(0, 0, w - 1, Height - 1), Math.Max(2, Height / 2), Ui.Primary);
            }
        }
    }

    /// <summary>无边框 Fluent 外壳：自绘标题栏 + 可拖动 + 关闭按钮 + Win11 圆角。</summary>
    public class BaseForm : Form
    {
        protected FlatButton BtnClose;
        protected string WindowTitle = "";
        protected int ContentTop;

        public BaseForm(string title, int w, int h, bool showClose = true)
        {
            WindowTitle = title;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Ui.Bg;
            Font = Ui.Body;
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
            Ui.InitDpi(this);
            ClientSize = new Size(Ui.P(w), Ui.P(h));
            ContentTop = Ui.P(48);
            if (showClose)
            {
                BtnClose = new FlatButton("✕", 34, 30, false);
                BtnClose.Font = new Font("Segoe UI", 9f);
                BtnClose.Click += (s, e) => OnCloseClicked();
                PositionClose();
            }
            MinimumSize = ClientSize;
            MaximumSize = ClientSize;
        }

        /// <summary>关闭按钮贴右上角（构造后与每次尺寸变化都要定位）。</summary>
        private void PositionClose()
        {
            if (BtnClose == null) return;
            BtnClose.Location = new Point(ClientSize.Width - BtnClose.Width - Ui.P(6), Ui.P(9));
        }

        protected virtual void OnCloseClicked() { Close(); }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Ui.EnableRoundedCorners(Handle);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && e.Y < ContentTop)
            {
                Ui.ReleaseCapture();
                Ui.SendMessage(Handle, 0xA1, (IntPtr)0x2, IntPtr.Zero);
            }
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Ui.Bg);
            TextRenderer.DrawText(g, WindowTitle, Ui.BodyStrong,
                new Rectangle(Ui.P(20), 0, Width - Ui.P(80), ContentTop), Ui.TextMain,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            using (var pen = new Pen(Ui.Border))
                g.DrawLine(pen, 0, ContentTop - 1, Width, ContentTop - 1);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            PositionClose();
        }

        protected void Add(Control c)
        {
            Controls.Add(c);
            if (c == BtnClose) PositionClose();
        }
    }
}
