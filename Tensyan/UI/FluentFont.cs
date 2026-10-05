using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Tensyan.UI
{
    /// <summary>
    /// 官方 Fluent 图标字体（Microsoft fluentui-system-icons，MIT）。
    /// 字体以资源形式内嵌在程序里，运行时加载，缺失时自动退回自绘矢量图标。
    /// </summary>
    public static class FluentFont
    {
        private static PrivateFontCollection _pfc;
        private static IntPtr _fontBuffer;   // 必须常驻：GDI+ 不会复制这份字体内存
        private static FontFamily _family;   // 私有字体必须用 FontFamily 对象构造 Font（用名字会退回系统字体）

        public static bool Available { get { return _family != null; } }
        public static string FamilyName { get { return _family == null ? "" : _family.Name; } }

        static FluentFont()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                foreach (var name in asm.GetManifestResourceNames())
                {
                    if (name.IndexOf("FluentSystemIcons", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    using (var s = asm.GetManifestResourceStream(name))
                    {
                        if (s == null) break;
                        var bytes = new byte[s.Length];
                        int off = 0, n;
                        while (off < bytes.Length && (n = s.Read(bytes, off, bytes.Length - off)) > 0) off += n;

                        _fontBuffer = Marshal.AllocCoTaskMem(bytes.Length);
                        Marshal.Copy(bytes, 0, _fontBuffer, bytes.Length);
                        _pfc = new PrivateFontCollection();
                        _pfc.AddMemoryFont(_fontBuffer, bytes.Length);
                        // 注意：这里**不能**释放 _fontBuffer —— 字体集合在整个进程生命周期内都需要它有效
                        if (_pfc.Families.Length > 0) _family = _pfc.Families[0];
                    }
                    break;
                }
            }
            catch
            {
                _family = null;
            }
        }

        public static Font Make(float pixelSize)
        {
            if (pixelSize < 4) pixelSize = 4;
            try
            {
                if (_family != null)
                    return new Font(_family, pixelSize, FontStyle.Regular, GraphicsUnit.Pixel);
            }
            catch { }
            return new Font(FontFamily.GenericSansSerif, pixelSize, FontStyle.Regular, GraphicsUnit.Pixel);
        }
    }

    /// <summary>IconKind → 官方 Fluent 图标的码点（取自 fluentui-system-icons 的 codepoints）。</summary>
    public static class FluentGlyphs
    {
        // 只画属于图标字体的那部分；未收录的（如自绘的品牌图标）继续用矢量绘制
        private static readonly Dictionary<IconKind, int> Map = new Dictionary<IconKind, int>
        {
            { IconKind.Students, 0xF5A9 },   // people
            { IconKind.Rank,     0xE2DE },   // chart / rank
            { IconKind.History,  0xF47F },   // history
            { IconKind.Stats,    0xF345 },   // data_pie
            { IconKind.Accounts, 0xF5C1 },   // person_accounts
            { IconKind.Usb,      0xEDD0 },   // usb_plug
            { IconKind.Settings, 0xF6AA },   // settings
            { IconKind.Home,     0xF481 },   // home
            { IconKind.Star,     0xF710 },   // star
            { IconKind.Refresh,  0xF191 },   // arrow_sync
            { IconKind.Add,      0xF10A },   // add
            { IconKind.Export,   0xF0281 },  // arrow_export
            { IconKind.Trash,    0xF34D },   // delete
            { IconKind.Edit,     0xF3DE },   // edit
            { IconKind.Random,   0xEF37 },   // arrow_shuffle
            { IconKind.Undo,     0xF19A },   // arrow_undo
            { IconKind.Lock,     0xE790 },   // lock_closed
            { IconKind.Check,    0xF295 },   // checkmark
            { IconKind.Search,   0xF690 },   // search
            { IconKind.Info,     0xF4A4 },   // info
            { IconKind.Warning,  0xF86A },   // warning
            { IconKind.Key,      0xF4B7 },   // key
            { IconKind.More,     0xE825 },   // more_horizontal
            { IconKind.Person,   0xF5B9 },   // people（另一形态）
            { IconKind.Chevron,  0xF2B1 },   // chevron_right
            { IconKind.SignOut,  0xF6DA },   // sign_out
            { IconKind.Filter,   0xF407 },   // filter
            { IconKind.Calendar, 0xF0285 },  // calendar（超出 BMP，用代理对）
            { IconKind.Folder,   0xF419 },   // folder
            { IconKind.Phone,    0xF5E1 }    // phone（配对手机）
        };


        private static readonly int[] Ladder = { 10, 12, 14, 16, 20, 24, 28, 32, 40, 48 };

        /// <summary>把任意盒子尺寸吸附到最接近的整数图标尺寸，保证清晰。</summary>
        public static int PickSize(float box)
        {
            int b = (int)Math.Round(box);
            if (b <= 0) return 12;
            int best = Ladder[0];
            foreach (var v in Ladder) if (v <= b) best = v;
            // 盒子明显比最小档还小就直接用整数盒子
            if (b < Ladder[0]) best = Math.Max(8, b);
            return best;
        }

        /// <summary>尝试用 Fluent 图标字体画；画成功返回 true。</summary>
        public static bool TryDraw(Graphics g, Rectangle box, IconKind kind, Color color)
        {
            if (!FluentFont.Available) return false;
            int cp;
            if (!Map.TryGetValue(kind, out cp)) return false;
            try
            {
                string glyph = cp > 0xFFFF ? char.ConvertFromUtf32(cp) : ((char)cp).ToString();
                // 关键：Fluent 图标字体是按 16/20/24/32 这类整数尺寸设计的，
                // 用小数尺寸（例如 16×0.86=13.76px）会被 GDI+ 网格拟合抹糊 —— 所以取整数阶梯。
                float box2 = Math.Min(box.Width, box.Height);
                float size = PickSize(box2);
                using (var f = FluentFont.Make(size))
                using (var b = new SolidBrush(color))
                using (var sf = new StringFormat(StringFormat.GenericTypographic)
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    FormatFlags = StringFormatFlags.NoWrap
                })
                {
                    var oldMode = g.SmoothingMode;
                    var oldHint = g.TextRenderingHint;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawString(glyph, f, b, box, sf);
                    g.SmoothingMode = oldMode;
                    g.TextRenderingHint = oldHint;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
