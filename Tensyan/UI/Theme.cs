using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using Tensyan.Core;

namespace Tensyan.UI
{
    /// <summary>
    /// Fluent (Windows 11 / WinUI) 风格设计系统：配色、字号体系、圆角描边与 DPI 缩放。
    /// 所有几何常量按 96 DPI 设计，绘制/布局时用 Theme.P() 换算到实际像素。
    /// </summary>
    public static class Theme
    {
        // ---------------- DPI 缩放 ----------------

        /// <summary>几何缩放系数：96 DPI = 1.0；可通过 TENSYAN_SCALE 环境变量覆盖（用于自检）。</summary>
        public static float Scale { get; private set; } = 1f;

        /// <summary>字体额外倍数：真实 DPI 下磅值由 GDI 自动放大，仅在模拟缩放时补足。</summary>
        public static float FontBoost { get; private set; } = 1f;

        public static void InitScale()
        {
            float s = 1f;
            string env = Environment.GetEnvironmentVariable("TENSYAN_SCALE");
            float parsed = 0f;
            if (!Str.Blank(env) && float.TryParse(env, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed)
                && parsed >= 0.8f && parsed <= 3f)
            {
                s = parsed;
                FontBoost = parsed;      // 模拟缩放时字体也要一起放大
            }
            else
            {
                try
                {
                    using (var g = Graphics.FromHwnd(IntPtr.Zero)) s = g.DpiX / 96f;
                }
                catch { s = 1f; }
                if (s < 0.8f || s > 3f) s = 1f;
                FontBoost = 1f;          // 真实 DPI：磅值由 GDI 放大，几何由 P() 放大
            }
            Scale = s;
        }

        /// <summary>把 96 DPI 设计值换算为实际像素。</summary>
        public static int P(int v) { return (int)Math.Round(v * Scale); }
        public static float PF(float v) { return v * Scale; }
        public static Size PS(int w, int h) { return new Size(P(w), P(h)); }
        public static Point PP(int x, int y) { return new Point(P(x), P(y)); }

        // ---------------- Fluent 配色（浅色） ----------------

        public static readonly Color Bg = Hex("#F3F3F3");
        public static readonly Color BgDeep = Hex("#EAEAEA");
        public static readonly Color Card = Hex("#FFFFFF");
        public static readonly Color CardHover = Hex("#FAFAFA");
        public static readonly Color Layer = Hex("#FBFBFB");

        public static readonly Color Border = Hex("#E5E5E5");
        public static readonly Color BorderStrong = Hex("#D1D1D1");
        public static readonly Color BorderSoft = Hex("#EEEEEE");

        public static readonly Color TextMain = Hex("#1B1B1B");
        public static readonly Color TextSub = Hex("#5D5D5D");
        public static readonly Color TextFaint = Hex("#8A8A8A");
        public static readonly Color TextOnAccent = Hex("#FFFFFF");

        public static readonly Color Primary = Hex("#005FB8");
        public static readonly Color PrimaryDark = Hex("#003E75");
        public static readonly Color PrimaryHover = Hex("#1A6FC4");
        public static readonly Color PrimarySoft = Hex("#EFF6FC");
        public static readonly Color PrimarySoftBorder = Hex("#C7E0F4");

        public static readonly Color Green = Hex("#0F7B0F");
        public static readonly Color GreenDark = Hex("#0B5A0B");
        public static readonly Color GreenSoft = Hex("#E9F6E9");
        public static readonly Color GreenBorder = Hex("#BFE3BF");

        public static readonly Color Red = Hex("#C42B1C");
        public static readonly Color RedDark = Hex("#8E1F14");
        public static readonly Color RedSoft = Hex("#FDECEA");
        public static readonly Color RedBorder = Hex("#F3C0BA");

        public static readonly Color Amber = Hex("#9D5D00");
        public static readonly Color AmberSoft = Hex("#FFF4CE");
        public static readonly Color Purple = Hex("#6B4EA0");
        public static readonly Color PurpleSoft = Hex("#F0EBF8");

        // 导航（NavigationView 浅色）
        public static readonly Color NavBg = Hex("#F3F3F3");
        public static readonly Color NavSel = Hex("#E8E8E8");
        public static readonly Color NavHover = Hex("#EDEDED");

        // 兼容旧命名
        public static Color NavBg2 { get { return NavHover; } }
        public static Color Faint { get { return TextFaint; } }

        // ---------------- 字号体系（Fluent Type Ramp） ----------------

        public static readonly string Family = ResolveFamily();

        /// <summary>是否运行在 Windows XP（5.x）：XP 没有 DWM、没有微软雅黑，占位提示 API 也无效，需要降级。
        /// 自检时可用环境变量 TENSYAN_FORCE_XP=1 强制打开这些分支。</summary>
        public static bool IsXp
        {
            get
            {
                try
                {
                    if (Environment.GetEnvironmentVariable("TENSYAN_FORCE_XP") == "1") return true;
                    return Environment.OSVersion.Version.Major <= 5;
                }
                catch { return false; }
            }
        }

        private static string ResolveFamily()
        {
            // XP 上没有 Segoe UI / 微软雅黑，依次退到黑体、宋体
            string[] wanted = { "Segoe UI Variable Text", "Segoe UI", "Microsoft YaHei UI", "微软雅黑", "Microsoft YaHei", "SimHei", "黑体", "SimSun", "宋体" };
            try
            {
                var installed = new InstalledFontCollection();
                foreach (var f in installed.Families)
                {
                    foreach (var w in wanted)
                        if (string.Equals(f.Name, w, StringComparison.OrdinalIgnoreCase)) return f.Name;
                }
            }
            catch { }
            return IsXp ? "SimSun" : "Microsoft YaHei UI";
        }

        public static readonly string IconFont = ResolveIconFont();

        /// <summary>优先用 Windows 11 自带的 Segoe Fluent Icons（官方 Fluent 图标），旧系统退回 MDL2。</summary>
        private static string ResolveIconFont()
        {
            try
            {
                string[] wanted = { "Segoe MDL2 Assets" };
                var installed = new InstalledFontCollection();
                foreach (var f in installed.Families)
                    foreach (var w in wanted)
                        if (string.Equals(f.Name, w, StringComparison.OrdinalIgnoreCase)) return f.Name;
            }
            catch { }
            return "Segoe MDL2 Assets";
        }

        /// <summary>磅值（96 DPI 下 1pt≈1.333px）。</summary>
        public static Font F(float size, FontStyle style = FontStyle.Regular)
        {
            try { return new Font(Family, size * FontBoost, style, GraphicsUnit.Point); }
            catch { return new Font(FontFamily.GenericSansSerif, size * FontBoost, style); }
        }

        public static Font FI(float size)
        {
            try { return new Font(IconFont, size * FontBoost, FontStyle.Regular, GraphicsUnit.Point); }
            catch { return F(size); }
        }

        // Fluent 字号：12 / 14 / 16 / 20 / 24 / 28 px
        public static Font Caption() { return F(9f); }                            // 12px
        public static Font Body() { return F(10.5f); }                            // 14px
        public static Font BodyStrong() { return F(10.5f, FontStyle.Bold); }      // 14px semibold
        public static Font Subtitle() { return F(12f, FontStyle.Bold); }          // 16px
        public static Font Title() { return F(15f, FontStyle.Bold); }             // 20px
        public static Font Display() { return F(20f, FontStyle.Bold); }           // 27px

        // ---------------- 颜色工具 ----------------

        public static Color Hex(string s)
        {
            s = s.TrimStart('#');
            return Color.FromArgb(
                Convert.ToInt32(s.Substring(0, 2), 16),
                Convert.ToInt32(s.Substring(2, 2), 16),
                Convert.ToInt32(s.Substring(4, 2), 16));
        }

        public static Color Mix(Color a, Color b, double t)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        public static Color Alpha(Color c, int a)
        {
            return Color.FromArgb(a, c);
        }

        // ---------------- 圆角 / 绘制 ----------------

        public static GraphicsPath Rounded(Rectangle r, int radius) { return Rounded(new RectangleF(r.X, r.Y, r.Width, r.Height), radius); }

        public static GraphicsPath Rounded(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = radius * 2f;
            if (d <= 0 || d > Math.Min(r.Width, r.Height)) d = Math.Min(r.Width, r.Height);
            if (d <= 1) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRounded(Graphics g, Rectangle r, int radius, Color c)
        {
            using (var p = Rounded(r, radius))
            using (var b = new SolidBrush(c))
                g.FillPath(b, p);
        }

        public static void FillRounded(Graphics g, Rectangle r, int radius, Color c1, Color c2)
        {
            using (var p = Rounded(r, radius))
            using (var b = new LinearGradientBrush(new Rectangle(r.X, r.Y, Math.Max(1, r.Width), Math.Max(1, r.Height)), c1, c2, 90f))
                g.FillPath(b, p);
        }

        public static void StrokeRounded(Graphics g, Rectangle r, int radius, Color c, float w = 1f)
        {
            using (var p = Rounded(r, radius))
            using (var pen = new Pen(c, w))
                g.DrawPath(pen, p);
        }

        /// <summary>Fluent 卡片：白底 + 细描边，可带极轻投影（用于对话框/浮层）。</summary>
        public static void CardSurface(Graphics g, Rectangle r, int radius = 8, bool shadow = false, Color? fill = null, Color? border = null)
        {
            if (shadow) SoftShadow(g, r, radius);
            FillRounded(g, r, radius, fill ?? Card);
            StrokeRounded(g, new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), radius, border ?? Border);
        }

        /// <summary>Fluent 极轻投影（仅浮层使用）。</summary>
        public static void SoftShadow(Graphics g, Rectangle r, int radius)
        {
            for (int i = 3; i >= 1; i--)
            {
                var rr = new Rectangle(r.X - i, r.Y - i + 1, r.Width + i * 2, r.Height + i * 2);
                using (var p = Rounded(rr, radius + i))
                using (var b = new SolidBrush(Color.FromArgb(6, 0, 0, 0)))
                    g.FillPath(b, p);
            }
        }

        /// <summary>兼容旧调用：现在只画非常轻的阴影。</summary>
        public static void Shadow(Graphics g, Rectangle r, int radius, int depth = 6, int alpha = 14)
        {
            SoftShadow(g, r, radius);
        }

        public static Color ScoreColor(int score)
        {
            if (score < 0) return Red;
            if (score == 0) return TextFaint;
            if (score >= 30) return Amber;
            if (score >= 15) return Green;
            return Primary;
        }

        public static string Initial(string name)
        {
            if (Str.Blank(name)) return "?";
            string n = name.Trim();
            char c = n[0];
            if (c >= 'a' && c <= 'z') return char.ToUpper(c).ToString();
            if (c >= 'A' && c <= 'Z') return c.ToString();
            return c.ToString();
        }

        public static TextFormatFlags Flags(ContentAlignment a)
        {
            var f = TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;
            if (a == ContentAlignment.MiddleCenter || a == ContentAlignment.TopCenter || a == ContentAlignment.BottomCenter) f |= TextFormatFlags.HorizontalCenter;
            else if (a == ContentAlignment.MiddleRight || a == ContentAlignment.TopRight || a == ContentAlignment.BottomRight) f |= TextFormatFlags.Right;
            if (a == ContentAlignment.MiddleLeft || a == ContentAlignment.MiddleCenter || a == ContentAlignment.MiddleRight) f |= TextFormatFlags.VerticalCenter;
            else if (a == ContentAlignment.TopLeft || a == ContentAlignment.TopCenter || a == ContentAlignment.TopRight) f |= TextFormatFlags.Top;
            else f |= TextFormatFlags.Bottom;
            return f;
        }


        // ---------------- 背景：类云母质感 ----------------

        /// <summary>顶部略亮、底部略深的极轻渐变，避免大面积纯平色块显得老旧。</summary>
        public static readonly Color BgTop = Hex("#F8F8F9");
        public static readonly Color BgBottom = Hex("#ECEDEF");

        public static void PaintBg(Graphics g, Rectangle r)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            try
            {
                using (var b = new System.Drawing.Drawing2D.LinearGradientBrush(
                    new Rectangle(r.X, r.Y, Math.Max(1, r.Width), Math.Max(2, r.Height + 1)), BgTop, BgBottom, 90f))
                    g.FillRectangle(b, r);
            }
            catch
            {
                using (var b = new SolidBrush(Bg)) g.FillRectangle(b, r);
            }
        }

        /// <summary>一行文本所需高度（含少量余量），避免文字被裁切。</summary>
        public static int LineHeight(Font f)
        {
            return (int)Math.Ceiling(f.GetHeight()) + P(4);
        }
    }

    /// <summary>品牌资产：logo 图片（嵌入资源）与双圆标记的矢量绘制。</summary>
    public static class Brand
    {
        // 从 logo 中取样的配色
        public static readonly Color CircleA = Theme.Hex("#B7C3F4");
        public static readonly Color CircleB = Theme.Hex("#CCD1E7");
        public static readonly Color Ink = Theme.Hex("#383B8E");
        public static readonly Color TileEdge = Theme.Hex("#DEE3F7");

        private static Bitmap _logo;

        /// <summary>完整 logo（透明底），失败时返回 null，调用方需判空。</summary>
        public static Bitmap Logo
        {
            get
            {
                if (_logo == null)
                {
                    try
                    {
                        var asm = typeof(Brand).Assembly;
                        using (var s = asm.GetManifestResourceStream("Tensyan.Assets.Tensyan-logo.png"))
                            if (s != null) _logo = new Bitmap(s);
                    }
                    catch { _logo = null; }
                }
                return _logo;
            }
        }

        /// <summary>双圆标记（矢量绘制，小尺寸下保持清晰）。</summary>
        public static void DrawMark(Graphics g, Rectangle r, bool single = false)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float R = r.Width * 0.375f;
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
            float off = single ? 0f : r.Width * 0.145f;
            using (var b = new SolidBrush(CircleA))
                g.FillEllipse(b, cx - off - R, cy - R, R * 2, R * 2);
            if (!single)
            {
                using (var b = new SolidBrush(Color.FromArgb(215, CircleB)))
                    g.FillEllipse(b, cx + off - R, cy - R, R * 2, R * 2);
            }
        }

        /// <summary>品牌小方块（白底圆角 + 双圆），用于标题栏等很小的地方。</summary>
        public static void DrawTile(Graphics g, Rectangle r)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int radius = Math.Max(3, r.Width / 5);
            Theme.FillRounded(g, r, radius, Color.White);
            Theme.StrokeRounded(g, new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), radius, TileEdge);
            int inset = Math.Max(2, r.Width / 6);
            DrawMark(g, new Rectangle(r.X + inset, r.Y + inset, r.Width - inset * 2, r.Height - inset * 2), r.Width <= 22);
        }

        /// <summary>按宽度等比绘制 logo，返回绘制高度（没有资源时返回 0）。</summary>
        public static int DrawLogo(Graphics g, int x, int y, int width)
        {
            var bmp = Logo;
            if (bmp == null) return 0;
            int h = (int)Math.Round(width * bmp.Height / (double)bmp.Width);
            var old = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(bmp, new Rectangle(x, y, width, h));
            g.InterpolationMode = old;
            return h;
        }

        public static int LogoHeight(int width)
        {
            var bmp = Logo;
            if (bmp == null) return 0;
            return (int)Math.Round(width * bmp.Height / (double)bmp.Width);
        }
    }

    /// <summary>按姓名生成稳定的扁平头像（Fluent 配色）。</summary>
    public static class Avatar
    {
        private static readonly string[] Palette =
        {
            "#0F6CBD", "#0F7B0F", "#9D5D00", "#6B4EA0", "#C42B1C",
            "#00707F", "#B146C2", "#4F52B2", "#0E7878", "#8A5A00"
        };

        public static Color ColorFor(string key)
        {
            int h = 17;
            if (!string.IsNullOrEmpty(key))
                foreach (char c in key) h = unchecked(h * 31 + c);
            h = Math.Abs(h);
            return Theme.Hex(Palette[h % Palette.Length]);
        }

        public static void Draw(Graphics g, Rectangle r, string name, string seed, Color? ring)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color baseColor = ColorFor(string.IsNullOrEmpty(seed) ? name : seed);
            bool hastRing = ring.HasValue;
            var inner = hastRing ? new Rectangle(r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4) : r;

            if (hastRing)
            {
                using (var pen = new Pen(Theme.Mix(ring.Value, Color.White, 0.35), Math.Max(1.6f, Theme.PF(1.6f))))
                    g.DrawEllipse(pen, r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
            }

            using (var p = new GraphicsPath())
            {
                p.AddEllipse(inner);
                using (var b = new SolidBrush(baseColor))
                    g.FillPath(b, p);
            }

            string t = Theme.Initial(name);
            float fs = inner.Height * 0.42f / (Theme.Scale <= 0 ? 1f : 1f);
            using (var f = new Font(Theme.Family, fs, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var b = new SolidBrush(Color.FromArgb(245, 255, 255, 255)))
                g.DrawString(t, f, b, new RectangleF(inner.X, inner.Y, inner.Width, inner.Height), sf);
        }

        public static Bitmap Render(int size, string name, string seed, Color? ring = null)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                Draw(g, new Rectangle(0, 0, size, size), name, seed, ring);
            }
            return bmp;
        }
    }
}
