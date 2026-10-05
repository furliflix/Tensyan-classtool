using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Tensyan.UI
{
    /// <summary>Fluent 风格按钮：强调色实心 / 标准描边 / 轻微底 Subtle 三种形态。</summary>
    public class RoundButton : Control
    {
        public Color Fill = Theme.Primary;
        public Color Fill2 = Color.Empty;          // 兼容旧代码：Fluent 下不再使用渐变
        public Color Hover = Color.Empty;
        public Color TextColor = Color.White;
        public bool Outline;
        public Color OutlineColor = Theme.BorderStrong;
        public int Radius = 4;
        public IconKind? Icon;
        public int IconSize = 16;
        public bool Soft;                          // 标准/Subtle 形态（浅底 + 描边）
        public bool Subtle;                        // 无边框，仅悬停变色

        private bool _down;
        private double _hoverT;

        public RoundButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.Body();
            Cursor = Cursors.Hand;
            Size = new Size(Theme.P(96), Theme.P(32));
        }

        /// <summary>以代码方式触发点击（等价于按钮的 PerformClick）。</summary>
        public void PerformClick2()
        {
            OnClick(EventArgs.Empty);
        }

        private void TweenHover(double target)
        {
            double from = _hoverT;
            if (Math.Abs(from - target) < 0.002) { _hoverT = target; Invalidate(); return; }
            Anim.Run(this, 130, t =>
            {
                if (IsDisposed) return;
                _hoverT = Anim.Lerp(from, target, Anim.EaseOutQuad(t));
                Invalidate();
            });
        }

        protected override void OnMouseEnter(EventArgs e) { TweenHover(1); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _down = false; TweenHover(0); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        private static bool IsPale(Color c)
        {
            int lum = (c.R * 299 + c.G * 587 + c.B * 114) / 1000;
            return lum > 190;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = Theme.P(Radius);

            bool standard = Soft || Outline || Subtle;

            Color surface, border, text;
            if (standard)
            {
                bool transparent = Subtle || Fill == Theme.Bg || Fill == Color.Transparent;
                Color baseSurface = transparent ? (Parent != null ? Parent.BackColor : Theme.Bg) : Fill;
                if (!transparent && Fill == Theme.Card) baseSurface = Theme.Card;
                Color hoverSurface = transparent ? Theme.NavHover : Theme.Mix(baseSurface, Color.Black, 0.05);
                surface = Theme.Mix(baseSurface, hoverSurface, _hoverT);
                border = Subtle ? Color.Empty : (OutlineColor == Theme.Bg ? Theme.Border : OutlineColor);
                text = IsPale(OutlineColor) ? Theme.TextMain : OutlineColor;
                if (_down) surface = Theme.Mix(surface, Color.Black, 0.08);
            }
            else
            {
                Color hoverSurface = Hover != Color.Empty ? Hover : Theme.Mix(Fill, Color.White, 0.14);
                surface = Theme.Mix(Fill, hoverSurface, _hoverT);
                border = Color.Empty;
                text = TextColor;
                if (_down) surface = Theme.Mix(Fill, Color.Black, 0.14);
            }

            if (!Enabled)
            {
                surface = standard ? Theme.Bg : Theme.Mix(Fill, Theme.Bg, 0.62);
                text = Theme.Mix(text, Theme.Bg, 0.55);
                border = standard ? Theme.BorderSoft : Color.Empty;
            }

            if (standard && !Subtle)
            {
                Theme.FillRounded(g, r, radius, surface);
                Theme.StrokeRounded(g, r, radius, border, Theme.PF(1f));
            }
            else
            {
                Theme.FillRounded(g, r, radius, surface);
            }

            int gap = Theme.P(6);
            int iconSize = Theme.P(IconSize);
            if (Icon.HasValue)
            {
                int tw = string.IsNullOrEmpty(Text) ? 0 : TextRenderer.MeasureText(g, Text, Font).Width;
                int total = iconSize + (tw > 0 ? gap + tw : 0);
                int startX = Math.Max(Theme.P(6), (Width - total) / 2);
                Icons.Draw(g, new Rectangle(startX, (Height - iconSize) / 2, iconSize, iconSize), Icon.Value, text);
                if (tw > 0)
                    TextRenderer.DrawText(g, Text, Font, new Rectangle(startX + iconSize + gap, 0, tw + 4, Height), text,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            else if (!string.IsNullOrEmpty(Text))
            {
                TextRenderer.DrawText(g, Text, Font, new Rectangle(Theme.P(8), 0, Width - Theme.P(16), Height), text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
    }

    /// <summary>Fluent NavigationView 导航项。</summary>
    public class NavItem : Control
    {
        public IconKind Icon = IconKind.Home;
        public bool Selected;
        private double _hoverT;

        public NavItem()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.Body();
            ForeColor = Theme.TextMain;
            Cursor = Cursors.Hand;
            Height = Theme.P(40);
        }

        private void TweenHover(double target)
        {
            double from = _hoverT;
            if (Math.Abs(from - target) < 0.002) { _hoverT = target; Invalidate(); return; }
            Anim.Run(this, 140, t =>
            {
                if (IsDisposed) return;
                _hoverT = Anim.Lerp(from, target, Anim.EaseOutQuad(t));
                Invalidate();
            });
        }

        protected override void OnMouseEnter(EventArgs e) { TweenHover(1); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { TweenHover(0); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.NavBg);

            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (Selected) Theme.FillRounded(g, r, Theme.P(4), Theme.NavSel);
            if (!Selected && _hoverT > 0.01 && Enabled)
            {
                using (var b = new SolidBrush(Color.FromArgb((int)(216 * _hoverT), Theme.NavHover)))
                using (var p = Theme.Rounded(r, Theme.P(4)))
                    g.FillPath(b, p);
            }

            Color tc = Enabled
                ? (Selected ? Theme.TextMain : Theme.Mix(Theme.TextSub, Theme.TextMain, _hoverT))
                : Theme.TextFaint;
            int iconSize = Theme.P(18);
            Icons.Draw(g, new Rectangle(Theme.P(12), (Height - iconSize) / 2, iconSize, iconSize), Icon, Selected ? Theme.Primary : tc);

            var textRect = new Rectangle(Theme.P(38), 0, Width - Theme.P(44), Height);
            TextRenderer.DrawText(g, Text, Selected ? Theme.BodyStrong() : Theme.Body(), textRect, tc,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

            if (Selected)
            {
                var pill = new Rectangle(Theme.P(2), (Height - Theme.P(16)) / 2, Theme.P(3), Theme.P(16));
                Theme.FillRounded(g, pill, Theme.P(2), Theme.Primary);
            }
        }
    }

    /// <summary>Fluent 文本输入框（外层画描边，内嵌真实 TextBox）。</summary>
    public class FluentInput : Control
    {
        public readonly TextBox Box = new TextBox();
        private bool _focus;

        public override string Text { get { return Box.Text; } set { Box.Text = value; } }
        public bool UseSystemPasswordChar { get { return Box.UseSystemPasswordChar; } set { Box.UseSystemPasswordChar = value; } }

        private string _placeholder = "";
        public string PlaceholderText
        {
            get { return _placeholder; }
            set
            {
                _placeholder = value ?? "";
                WinCompat.SetPlaceholder(Box, _placeholder);   // Vista+ 用系统 cue banner
                Invalidate();                                   // XP 上自己画
            }
        }

        public FluentInput()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = Theme.P(34);
            Box.BorderStyle = BorderStyle.None;
            Box.Font = Theme.Body();
            Box.BackColor = Theme.Card;
            Box.ForeColor = Theme.TextMain;
            Box.GotFocus += (s, e) => { _focus = true; Invalidate(); };
            Box.LostFocus += (s, e) => { _focus = false; Invalidate(); };
            Box.TextChanged += (s, e) => OnTextChanged(EventArgs.Empty);
            Controls.Add(Box);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Box.SetBounds(Theme.P(10), (Height - Box.PreferredHeight) / 2, Math.Max(Theme.P(20), Width - Theme.P(20)), Box.PreferredHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Card);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRounded(g, r, Theme.P(4), Theme.Card);
            Theme.StrokeRounded(g, r, Theme.P(4), _focus ? Theme.Primary : Theme.BorderStrong, Theme.PF(_focus ? 1.6f : 1f));

            // XP 上 EM_SETCUEBANNER 无效（那是 Vista+ 的功能），这里自己画占位提示
            if (Theme.IsXp && !_focus && Box.TextLength == 0 && _placeholder.Length > 0)
            {
                var tr = new Rectangle(Box.Left, 0, Math.Max(Theme.P(20), Width - Box.Left - Theme.P(6)), Height);
                TextRenderer.DrawText(g, _placeholder, Box.Font, tr, Theme.TextFaint,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
    }

    /// <summary>Fluent 下拉选择（替代原生 ComboBox，避免样式割裂）。</summary>
    public class FluentSelect : Control
    {
        private readonly List<string> _items = new List<string>();
        private int _index = -1;
        private bool _hover, _open;

        public event EventHandler SelectedIndexChanged;

        public List<string> Items { get { return _items; } }
        public int SelectedIndex
        {
            get { return _index; }
            set
            {
                int v = (value < -1 || value >= _items.Count) ? -1 : value;
                if (v == _index) return;
                _index = v;
                Invalidate();
                var h = SelectedIndexChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }
        public string SelectedItem { get { return _index >= 0 && _index < _items.Count ? _items[_index] : null; } }
        public string Placeholder = "请选择";

        public FluentSelect()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.Body();
            Cursor = Cursors.Hand;
            Height = Theme.P(34);
        }

        public void AddItems(params string[] items) { _items.AddRange(items); }
        public void ClearItems() { _items.Clear(); _index = -1; Invalidate(); }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            ShowMenu();
        }

        private void ShowMenu()
        {
            var menu = new ContextMenuStrip { Font = Theme.Body(), ShowImageMargin = false, Renderer = new FluentMenuRenderer() };
            for (int i = 0; i < _items.Count; i++)
            {
                int idx = i;
                var item = new ToolStripMenuItem(_items[i]);
                item.ForeColor = Theme.TextMain;
                item.Padding = new Padding(Theme.P(6), Theme.P(2), Theme.P(6), Theme.P(2));
                if (i == _index) item.Font = Theme.BodyStrong();
                item.Click += (s, e) => { SelectedIndex = idx; };
                menu.Items.Add(item);
            }
            if (_items.Count == 0) menu.Items.Add(new ToolStripMenuItem("（无可选项）") { Enabled = false });

            _open = true;
            Invalidate();
            menu.Closed += (s, e) => { _open = false; Invalidate(); };
            menu.Show(this, new Point(0, Height + Theme.P(2)));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);

            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRounded(g, r, Theme.P(4), _hover || _open ? Theme.CardHover : Theme.Card);
            Theme.StrokeRounded(g, r, Theme.P(4), _open ? Theme.Primary : Theme.BorderStrong,
                Theme.PF(_open ? 1.6f : 1f));

            string text = SelectedItem ?? Placeholder;
            Color tc = SelectedItem == null ? Theme.TextFaint : Theme.TextMain;
            TextRenderer.DrawText(g, text, Font, new Rectangle(Theme.P(10), 0, Width - Theme.P(34), Height), tc,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

            // chevron
            var pen = new Pen(Theme.TextSub, Theme.PF(1.4f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            int cx = Width - Theme.P(15), cy = Height / 2;
            g.DrawLine(pen, cx - Theme.P(4), cy - Theme.P(2), cx, cy + Theme.P(2));
            g.DrawLine(pen, cx, cy + Theme.P(2), cx + Theme.P(4), cy - Theme.P(2));
            pen.Dispose();
        }
    }

    /// <summary>Fluent 分段控件（替代标签页）。</summary>
    public class SegmentedControl : Control
    {
        private readonly List<string> _items = new List<string>();
        private int _index;
        private int _hoverIndex = -1;

        public event EventHandler SelectedIndexChanged;

        public int SelectedIndex
        {
            get { return _index; }
            set
            {
                if (value < 0 || value >= _items.Count || value == _index) return;
                _index = value;
                Invalidate();
                var h = SelectedIndexChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        public SegmentedControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.Body();
            Height = Theme.P(36);
            Cursor = Cursors.Hand;
        }

        public void AddItems(params string[] items) { _items.AddRange(items); Invalidate(); }

        private int IndexAt(Point p)
        {
            if (_items.Count == 0) return -1;
            int w = Math.Max(1, Width / _items.Count);
            int i = p.X / w;
            return i < 0 || i >= _items.Count ? -1 : i;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int i = IndexAt(e.Location);
            if (i != _hoverIndex) { _hoverIndex = i; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { _hoverIndex = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int i = IndexAt(e.Location);
            if (i >= 0) SelectedIndex = i;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            if (_items.Count == 0) return;

            var outer = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRounded(g, outer, Theme.P(4), Theme.BgDeep);
            Theme.StrokeRounded(g, outer, Theme.P(4), Theme.Border);

            int w = Width / _items.Count;
            for (int i = 0; i < _items.Count; i++)
            {
                var cell = new Rectangle(i * w + Theme.P(2), Theme.P(2), w - Theme.P(4), Height - Theme.P(5));
                if (i == _index)
                {
                    Theme.FillRounded(g, cell, Theme.P(3), Theme.Card);
                    Theme.StrokeRounded(g, cell, Theme.P(3), Theme.Border);
                }
                else if (i == _hoverIndex)
                {
                    Theme.FillRounded(g, cell, Theme.P(3), Theme.Mix(Theme.BgDeep, Color.Black, 0.03));
                }
                TextRenderer.DrawText(g, _items[i], i == _index ? Theme.BodyStrong() : Theme.Body(), cell,
                    i == _index ? Theme.TextMain : Theme.TextSub,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
    }

    /// <summary>Fluent 搜索框。</summary>
    public class SearchBox : Control
    {
        public readonly TextBox Box = new TextBox();
        public string Placeholder = "搜索";
        private bool _focus;

        public event EventHandler TextChanged2;
        public string Value { get { return Box.Text; } set { Box.Text = value; } }

        public SearchBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = Theme.P(34);
            Box.BorderStyle = BorderStyle.None;
            Box.Font = Theme.Body();
            Box.BackColor = Theme.Card;
            Box.ForeColor = Theme.TextMain;
            Box.TextChanged += (s, e) => { if (TextChanged2 != null) TextChanged2(this, EventArgs.Empty); Invalidate(); };
            Box.GotFocus += (s, e) => { _focus = true; Invalidate(); };
            Box.LostFocus += (s, e) => { _focus = false; Invalidate(); };
            Controls.Add(Box);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Box.SetBounds(Theme.P(32), (Height - Box.PreferredHeight) / 2, Math.Max(Theme.P(20), Width - Theme.P(44)), Box.PreferredHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRounded(g, r, Theme.P(4), Theme.Card);
            Theme.StrokeRounded(g, r, Theme.P(4), _focus ? Theme.Primary : Theme.BorderStrong, Theme.PF(_focus ? 1.6f : 1f));

            int iconSize = Theme.P(14);
            Icons.Draw(g, new Rectangle(Theme.P(10), (Height - iconSize) / 2, iconSize, iconSize), IconKind.Search, Theme.TextSub);
        }
    }

    /// <summary>Fluent 圆角标签（角色徽标等）。</summary>
    public class Pill : Control
    {
        public Color Accent = Theme.Primary;
        public bool Filled;
        private string _text = "";

        public override string Text { get { return _text; } set { _text = value ?? ""; Invalidate(); } }

        public Pill()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.Caption();
            Height = Theme.P(24);
            Size = new Size(Theme.P(110), Theme.P(24));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (Filled)
            {
                Theme.FillRounded(g, r, Theme.P(12), Accent);
                TextRenderer.DrawText(g, Text, Theme.BodyStrong(), r, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            else
            {
                Theme.FillRounded(g, r, Theme.P(12), Theme.Mix(Accent, Color.White, 0.88));
                Theme.StrokeRounded(g, r, Theme.P(12), Theme.Mix(Accent, Color.White, 0.6));
                TextRenderer.DrawText(g, Text, Theme.BodyStrong(), r, Accent,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }

    /// <summary>Fluent 卡片面板。</summary>
    public class CardPanel : Panel
    {
        public int Radius = 8;
        public bool DrawShadow = false;
        public Color FillColor = Theme.Card;

        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            Padding = new Padding(Theme.P(16));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.CardSurface(g, r, Theme.P(Radius), DrawShadow, FillColor);
        }
    }

    /// <summary>Fluent 菜单渲染（下拉、右键菜单）。</summary>
    public class FluentMenuRenderer : ToolStripProfessionalRenderer
    {
        public FluentMenuRenderer() : base(new FluentColorTable()) { RoundedEdges = false; }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Selected ? Theme.TextMain : (e.Item.ForeColor == Color.Empty ? Theme.TextMain : e.Item.ForeColor);
            base.OnRenderItemText(e);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (var pen = new Pen(Theme.Border))
                e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }
    }

    public class FluentColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Theme.Card; } }
        public override Color MenuItemSelected { get { return Theme.NavHover; } }
        public override Color MenuItemBorder { get { return Color.Transparent; } }
        public override Color MenuBorder { get { return Theme.Border; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Card; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Card; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Card; } }
        public override Color SeparatorDark { get { return Theme.Border; } }
        public override Color SeparatorLight { get { return Theme.Card; } }
    }

    /// <summary>提示条（Fluent InfoBar 风格）与飘字动画，全部走 Anim 缓动。</summary>
    public static class Toast
    {
        public static void Show(Control host, string text, Color color)
        {
            var form = host as Form ?? host.FindForm();
            if (form == null || form.IsDisposed) return;

            var view = new ToastView { Message = text, Accent = color };
            int width;
            using (var g = form.CreateGraphics())
            {
                var sz = TextRenderer.MeasureText(g, text, Theme.Body());
                width = Math.Min(Math.Max(Theme.P(220), form.Width - Theme.P(60)), sz.Width + Theme.P(78));
            }
            view.Size = new Size(width, Theme.P(46));

            int left = (form.Width - view.Width) / 2;
            int restY = form.Height - Theme.P(108);
            int startY = restY + Theme.P(22);
            view.Location = new Point(left, startY);
            view.Anchor = AnchorStyles.None;
            form.Controls.Add(view);
            view.BringToFront();

            Anim.Run(view, 260, t =>
            {
                if (view.IsDisposed) return;
                double e = Anim.EaseOutCubic(t);
                view.Alpha = e;
                view.Top = Anim.LerpInt(startY, restY, e);
                view.Invalidate();
            }, () =>
            {
                if (view.IsDisposed) return;
                var hold = new System.Windows.Forms.Timer { Interval = 2000 };
                hold.Tick += (s, e) =>
                {
                    hold.Stop();
                    hold.Dispose();
                    if (view.IsDisposed) return;
                    Anim.Run(view, 300, t2 =>
                    {
                        if (view.IsDisposed) return;
                        double e2 = Anim.EaseInCubic(t2);
                        view.Alpha = 1 - e2;
                        view.Top = Anim.LerpInt(restY, restY - Theme.P(14), e2);
                        view.Invalidate();
                    }, () => { if (!view.IsDisposed) view.Dispose(); });
                };
                hold.Start();
            });
        }

        /// <summary>在学生卡片上方飘出 +N / -N（上浮 + 横移 + 弹出缩放 + 淡出）。</summary>
        public static void Fly(Control host, string text, Color color, Point center)
        {
            var form = host as Form ?? host.FindForm();
            if (form == null || form.IsDisposed) return;

            var ft = new FloatingText { Text = text, Accent = color };
            var p = form.PointToClient(host.PointToScreen(center));
            ft.Location = new Point(p.X - ft.Width / 2, p.Y - ft.Height / 2);
            ft.Anchor = AnchorStyles.None;
            form.Controls.Add(ft);
            ft.BringToFront();

            Anim.Run(ft, 850, t =>
            {
                if (ft.IsDisposed) return;
                ft.T = t;
                ft.Invalidate();
            }, () => { if (!ft.IsDisposed) ft.Dispose(); });
        }

        /// <summary>InfoBar 风格浮层，Alpha 参与绘制以便平滑淡入淡出。</summary>
        private class ToastView : Control
        {
            public string Message = "";
            public Color Accent = Theme.Primary;
            public double Alpha = 1;

            public ToastView()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
                Font = Theme.Body();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                double a01 = Anim.Clamp01(Alpha);
                int a = (int)(255 * a01);
                if (a <= 2) return;

                var r = new Rectangle(0, 0, Width - 1, Height - 1);
                if (a > 200) Theme.SoftShadow(g, r, Theme.P(8));
                Theme.FillRounded(g, r, Theme.P(8), Color.FromArgb(Math.Min(252, a), Theme.Card));
                Theme.StrokeRounded(g, r, Theme.P(8), Color.FromArgb((int)(a * 0.85), Theme.Border));
                Theme.FillRounded(g, new Rectangle(0, 0, Theme.P(4), Height), Theme.P(2), Color.FromArgb(a, Accent));

                bool danger = Accent == Theme.Red;
                bool success = Accent == Theme.Green;
                int iconSize = Theme.P(16);
                Icons.Draw(g, new Rectangle(Theme.P(16), (Height - iconSize) / 2, iconSize, iconSize),
                    danger ? IconKind.Warning : (success ? IconKind.Check : IconKind.Info), Color.FromArgb(a, Accent));

                TextRenderer.DrawText(g, Message, Font,
                    new Rectangle(Theme.P(42), 0, Width - Theme.P(56), Height), Color.FromArgb(a, Theme.TextMain),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        /// <summary>飘字控件。</summary>
        private class FloatingText : Control
        {
            public Color Accent = Theme.Green;
            public double T;

            public FloatingText()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
                Size = Theme.PS(190, 92);
                Enabled = false;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                double t = Anim.Clamp01(T);
                if (t >= 1) return;

                double rise = Theme.P(54) * Anim.EaseOutCubic(t);
                double drift = Theme.P(9) * Math.Sin(Math.PI * t);
                double pop = Math.Sin(Math.PI * Math.Min(1, t * 1.30));
                float scale = (float)(0.78 + 0.38 * pop);
                int alpha = t < 0.52 ? 255 : (int)(255 * (1 - (t - 0.52) / 0.48));
                if (alpha <= 2) return;

                var old = g.Transform;
                g.TranslateTransform((float)drift, (float)(-rise));
                g.TranslateTransform(Width / 2f, Height / 2f);
                g.ScaleTransform(scale, scale);
                g.TranslateTransform(-Width / 2f, -Height / 2f);

                using (var f = Theme.Title())
                using (var b = new SolidBrush(Color.FromArgb(alpha, Accent)))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(Text, f, b, new RectangleF(0, 0, Width, Height), sf);

                g.Transform = old;
            }
        }
    }
}
    /// <summary>开启双缓冲的面板：大面积重绘（导航、内容区）不再出现撕裂/闪烁。</summary>
    public class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
        }
    }