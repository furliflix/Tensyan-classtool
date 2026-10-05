using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Tensyan.UI
{
    /// <summary>标题栏按钮（Fluent 图标按钮）。</summary>
    public class ChromeButton : Control
    {
        public enum Kind { Min, Max, Restore, Close }
        public Kind Mode = Kind.Min;
        public Color GlyphColor = Theme.TextSub;
        private bool _hover, _down;

        public ChromeButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(Theme.P(46), Theme.P(32));
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
            if (Parent != null) g.Clear(Parent.BackColor);

            var box = new Rectangle(Theme.P(2), Theme.P(2), Width - Theme.P(5), Height - Theme.P(5));
            if (_hover)
            {
                Color bg = Mode == Kind.Close ? (_down ? Theme.RedDark : Theme.Red) : (_down ? Theme.BgDeep : Theme.NavHover);
                Theme.FillRounded(g, box, Theme.P(4), bg);
            }

            Color c = Mode == Kind.Close && _hover ? Color.White : GlyphColor;
            using (var pen = new Pen(c, Theme.PF(1.1f)))
            {
                int cx = Width / 2, cy = Height / 2;
                int s = Theme.P(5);
                if (Mode == Kind.Min) g.DrawLine(pen, cx - s, cy, cx + s, cy);
                else if (Mode == Kind.Max) g.DrawRectangle(pen, cx - s, cy - s, s * 2, s * 2);
                else if (Mode == Kind.Restore)
                {
                    g.DrawRectangle(pen, cx - s - Theme.P(1), cy - s + Theme.P(2), s * 2 - Theme.P(2), s * 2 - Theme.P(2));
                    g.DrawRectangle(pen, cx - s + Theme.P(2), cy - s - Theme.P(1), s * 2 - Theme.P(2), s * 2 - Theme.P(2));
                }
                else
                {
                    g.DrawLine(pen, cx - s, cy - s, cx + s, cy + s);
                    g.DrawLine(pen, cx + s, cy - s, cx - s, cy + s);
                }
            }
        }
    }

    /// <summary>无系统边框、Fluent 自绘标题栏的窗口基类（支持拖动、四边缩放、Win11 圆角）。</summary>
    public class ChromeForm : Form
    {
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        [DllImport("dwmapi.dll", PreserveSig = true)] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;
        private const int WM_NCHITTEST = 0x84;
        private const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        public bool CanResize = true;
        public bool CanMaximize = true;
        public string TitleText = "Tensyan";
        public bool ShowBrandMark = false;

        protected Panel TitleBar;
        protected Label TitleLabel;
        protected ChromeButton BtnMin, BtnMax, BtnClose;

        public ChromeForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            BackColor = Theme.Bg;
            Font = Theme.Body();
            AutoScaleMode = AutoScaleMode.None;      // 由 Theme.P() 统一缩放
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            TitleBar = new Panel { Dock = DockStyle.Top, Height = Theme.P(44), BackColor = Theme.Bg };
            TitleBar.Paint += TitleBarPaint;
            TitleBar.MouseDown += DragMove;
            TitleBar.MouseDoubleClick += (s, e) => ToggleMax();

            TitleLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Left,
                Width = Theme.P(460),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(Theme.P(ShowBrandMark ? 40 : 16), 0, 0, 0),
                Font = Theme.BodyStrong(),
                ForeColor = Theme.TextMain,
                BackColor = Color.Transparent
            };
            TitleLabel.MouseDown += DragMove;
            TitleLabel.MouseDoubleClick += (s, e) => ToggleMax();

            BtnClose = new ChromeButton { Mode = ChromeButton.Kind.Close, Dock = DockStyle.Right, GlyphColor = Theme.TextSub };
            BtnMax = new ChromeButton { Mode = ChromeButton.Kind.Max, Dock = DockStyle.Right, GlyphColor = Theme.TextSub };
            BtnMin = new ChromeButton { Mode = ChromeButton.Kind.Min, Dock = DockStyle.Right, GlyphColor = Theme.TextSub };

            BtnClose.Click += (s, e) => Close();
            BtnMax.Click += (s, e) => ToggleMax();
            BtnMin.Click += (s, e) => WindowState = FormWindowState.Minimized;

            TitleBar.Controls.Add(TitleLabel);
            TitleBar.Controls.Add(BtnClose);
            TitleBar.Controls.Add(BtnMax);
            TitleBar.Controls.Add(BtnMin);
            Controls.Add(TitleBar);
        }

        private void TitleBarPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(TitleBar.BackColor);

            if (ShowBrandMark)
            {
                int size = Theme.P(22);
                var box = new Rectangle(Theme.P(15), (TitleBar.Height - size) / 2, size, size);
                Brand.DrawTile(g, box);
            }

            using (var pen = new Pen(Theme.Border))
                g.DrawLine(pen, 0, TitleBar.Height - 1, TitleBar.Width, TitleBar.Height - 1);
        }

        /// <summary>把窗口配置成固定尺寸对话框（尺寸按 DPI 缩放）。</summary>
        public void AsDialog(int width, int height)
        {
            CanResize = false;
            CanMaximize = false;
            BtnMin.Visible = false;
            BtnMax.Visible = false;
            ClientSize = Theme.PS(width, height);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MaximizeBox = false;
            MinimizeBox = false;
            FitToScreen();
        }

        /// <summary>高 DPI 下窗口可能大于屏幕：按工作区收窄，避免跑到屏幕外。</summary>
        protected void FitToScreen()
        {
            try
            {
                var wa = Screen.PrimaryScreen.WorkingArea;
                int maxW = (int)(wa.Width * 0.96), maxH = (int)(wa.Height * 0.96);
                var ms = MinimumSize;
                if (ms.Width > maxW || ms.Height > maxH)
                    MinimumSize = new Size(Math.Min(ms.Width, maxW), Math.Min(ms.Height, maxH));
                if (ClientSize.Width > maxW || ClientSize.Height > maxH)
                    ClientSize = new Size(Math.Min(ClientSize.Width, maxW), Math.Min(ClientSize.Height, maxH));
            }
            catch { }
        }

        public void SetTitleBarColors(Color bg, Color fg, Color buttonFg)
        {
            TitleBar.BackColor = bg;
            TitleLabel.ForeColor = fg;
            BtnClose.GlyphColor = fg;
            BtnMax.GlyphColor = buttonFg;
            BtnMin.GlyphColor = buttonFg;
            TitleBar.Invalidate();
        }

        private void DragMove(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (WindowState == FormWindowState.Maximized)
            {
                var screenPos = PointToScreen(e.Location);
                double ratio = (double)e.X / Math.Max(1, TitleBar.Width);
                WindowState = FormWindowState.Normal;
                Left = (int)(screenPos.X - Width * ratio);
                Top = Math.Max(0, screenPos.Y - Theme.P(22));
            }
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
        }

        protected void ToggleMax()
        {
            if (!CanMaximize) return;
            WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
            BtnMax.Mode = WindowState == FormWindowState.Maximized ? ChromeButton.Kind.Restore : ChromeButton.Kind.Max;
            BtnMax.Invalidate();
            OnWindowStateChanged();
        }

        protected virtual void OnWindowStateChanged() { }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            DoubleBuffered = true;   // 整窗双缓冲：消除自绘窗体的撕裂
            ApplyRoundCorners();
        }

        private void ApplyRoundCorners()
        {
            if (Theme.IsXp) return;      // XP 没有 DWM：不做圆角，窗口就是直角（已与用户确认可接受）
            try
            {
                int pref = DWMWCP_ROUND;                 // Win11 圆角窗口；旧系统会失败，忽略即可
                DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
            }
            catch { }
        }

        /// <summary>
        /// 打开动画：只做轻微上滑，**不使用窗口透明度**。
        /// 透明度会把窗口变成分层窗口，恢复时会引起重绘闪一下，也会丢掉 Win11 圆角，所以不用。
        /// </summary>
        protected void PlayOpenAnimation()
        {
            if (!Anim.Enabled || IsDisposed) return;
            // 2026-10-05：逐帧修改窗口 Top 会让自绘窗体反复搬迁重绘 → 表现为"撕裂成一块一块"。
            // 窗口位移动画已停用；入场动感改由控件级动画承担（导航栏宽度动画等）。
            return;
            if (WindowState != FormWindowState.Normal) return;
            int endTop = Top;
            int startTop = endTop + Theme.P(14);
            Top = startTop;
            Anim.Run(this, 200, t =>
            {
                if (IsDisposed) return;
                Top = Anim.LerpInt(startTop, endTop, Anim.EaseOutCubic(t));
            }, () =>
            {
                if (IsDisposed) return;
                Top = endTop;
            });
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            OnWindowShown();
        }

        protected virtual void OnWindowShown() { }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            try { MaximizedBounds = Screen.FromControl(this).WorkingArea; } catch { }
            TitleLabel.Text = (ShowBrandMark ? "  " : "") + TitleText;
            TitleLabel.Padding = new Padding(Theme.P(ShowBrandMark ? 42 : 16), 0, 0, 0);
        }

        /// <summary>
        /// WS_EX_COMPOSITED：让整个窗口（**包含所有子控件**）走双缓冲合成。
        /// WinForms 的撕裂几乎都来自"动画中移动/缩放面板时，子控件各自重绘"——
        /// 只给单个控件开 DoubleBuffered 是治不了的，必须整窗合成。
        /// 代价：拖动/缩放时 CPU 略高，换来动画不撕裂，值得。
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                if (!Theme.IsXp) cp.ExStyle |= 0x02000000;   // WS_EX_COMPOSITED
                return cp;
            }
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST && CanResize && WindowState == FormWindowState.Normal)
            {
                base.WndProc(ref m);
                if ((int)m.Result == 1)
                {
                    var p = PointToClient(new Point(m.LParam.ToInt32()));
                    int b = Theme.P(6);
                    bool left = p.X <= b, right = p.X >= ClientSize.Width - b;
                    bool top = p.Y <= b, bottom = p.Y >= ClientSize.Height - b;
                    if (top && left) m.Result = (IntPtr)HTTOPLEFT;
                    else if (top && right) m.Result = (IntPtr)HTTOPRIGHT;
                    else if (bottom && left) m.Result = (IntPtr)HTBOTTOMLEFT;
                    else if (bottom && right) m.Result = (IntPtr)HTBOTTOMRIGHT;
                    else if (left) m.Result = (IntPtr)HTLEFT;
                    else if (right) m.Result = (IntPtr)HTRIGHT;
                    else if (top) m.Result = (IntPtr)HTTOP;
                    else if (bottom) m.Result = (IntPtr)HTBOTTOM;
                }
                return;
            }
            base.WndProc(ref m);
        }
    }
}
