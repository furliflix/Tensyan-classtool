using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Tensyan.UI
{
    public enum IconKind
    {
        Students, Rank, History, Stats, Accounts, Usb, Settings, Home, Star, Refresh, Add, Export, Trash, Edit, Random, Undo, Lock, Check,
        Search, Info, Warning, Key, More, Person, Chevron, SignOut, Filter, Calendar, Folder, Phone
    }

    /// <summary>自绘矢量小图标（不依赖图标字体，避免缺字）。</summary>
    public static class Icons
    {
        public static void Draw(Graphics g, Rectangle box, IconKind kind, Color color)
        {
            // 优先使用官方 Fluent 图标字体（Microsoft fluentui-system-icons，MIT）；不可用时退回自绘矢量图标
            if (FluentGlyphs.TryDraw(g, box, kind, color)) return;
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float s = Math.Min(box.Width, box.Height) / 20f;
            using (var m = new Matrix())
            {
                m.Translate(box.X + (box.Width - 20 * s) / 2f, box.Y + (box.Height - 20 * s) / 2f);
                m.Scale(s, s);
                g.Transform = m;
                Paint(g, kind, color);
                g.ResetTransform();
            }
            g.SmoothingMode = old;
        }

        private static void Paint(Graphics g, IconKind kind, Color c)
        {
            using (var pen = new Pen(c, 1.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (var b = new SolidBrush(c))
            {
                switch (kind)
                {
                    case IconKind.Phone:
                        // 手机外形 + 底部小点（配对手机用）
                        Tensyan.UI.Theme.StrokeRounded(g, new Rectangle(6, 2, 9, 16), 2, c, 1.7f);
                        g.FillEllipse(b, 9.4f, 15.2f, 2.2f, 2.2f);
                        break;
                                    case IconKind.Students:
                        g.FillEllipse(b, 3.5f, 3f, 7.4f, 7.4f);
                        g.FillEllipse(b, 12f, 5.5f, 5f, 5f);
                        using (var p = Theme.Rounded(new RectangleF(2f, 12f, 10.5f, 6.5f), 3f)) g.FillPath(b, p);
                        using (var p = Theme.Rounded(new RectangleF(12.6f, 11.6f, 6f, 5.6f), 2.6f)) g.FillPath(b, p);
                        break;

                    case IconKind.Rank:
                        using (var p = Theme.Rounded(new RectangleF(2.5f, 11f, 4.4f, 6.5f), 1.4f)) g.FillPath(b, p);
                        using (var p = Theme.Rounded(new RectangleF(7.8f, 6.5f, 4.4f, 11f), 1.4f)) g.FillPath(b, p);
                        using (var p = Theme.Rounded(new RectangleF(13.1f, 2.5f, 4.4f, 15f), 1.4f)) g.FillPath(b, p);
                        break;

                    case IconKind.History:
                        using (var p = Theme.Rounded(new RectangleF(2.5f, 3f, 15f, 14f), 2.6f)) g.DrawPath(pen, p);
                        g.DrawLine(pen, 5.6f, 7.2f, 14f, 7.2f);
                        g.DrawLine(pen, 5.6f, 10.4f, 14f, 10.4f);
                        g.DrawLine(pen, 5.6f, 13.4f, 10.6f, 13.4f);
                        break;

                    case IconKind.Stats:
                        g.DrawLine(pen, 2.8f, 2.6f, 2.8f, 17.2f);
                        g.DrawLine(pen, 2.8f, 17.2f, 17.4f, 17.2f);
                        g.DrawLines(pen, new[] { new PointF(5f, 13.4f), new PointF(8.6f, 9.4f), new PointF(12f, 11.6f), new PointF(16.4f, 5.4f) });
                        g.FillEllipse(b, 14.9f, 3.9f, 3f, 3f);
                        break;

                    case IconKind.Accounts:
                        g.FillEllipse(b, 3.2f, 3f, 7.6f, 7.6f);
                        using (var p = Theme.Rounded(new RectangleF(1.8f, 12.2f, 10.4f, 6f), 2.8f)) g.FillPath(b, p);
                        g.DrawLine(pen, 14.6f, 8.4f, 14.6f, 13.2f);
                        g.FillEllipse(b, 13.2f, 13.4f, 2.8f, 2.8f);
                        g.FillEllipse(b, 12.4f, 5.4f, 4.4f, 4.4f);
                        break;

                    case IconKind.Usb:
                        using (var p = Theme.Rounded(new RectangleF(7f, 5.4f, 6.4f, 12f), 1.8f)) g.DrawPath(pen, p);
                        g.FillRectangle(b, 9.1f, 2.6f, 2.2f, 3f);
                        g.FillRectangle(b, 8.8f, 9.6f, 2.8f, 2.8f);
                        break;

                    case IconKind.Settings:
                        using (var p = Theme.Rounded(new RectangleF(6.6f, 6.6f, 6.8f, 6.8f), 2f)) g.DrawPath(pen, p);
                        g.DrawEllipse(pen, 2.6f, 2.6f, 14.8f, 14.8f);
                        g.DrawLine(pen, 10f, 1.4f, 10f, 4.4f);
                        g.DrawLine(pen, 10f, 15.6f, 10f, 18.6f);
                        g.DrawLine(pen, 1.4f, 10f, 4.4f, 10f);
                        g.DrawLine(pen, 15.6f, 10f, 18.6f, 10f);
                        break;

                    case IconKind.Home:
                        g.DrawLines(pen, new[] { new PointF(3f, 9.6f), new PointF(10f, 3.4f), new PointF(17f, 9.6f) });
                        using (var p = Theme.Rounded(new RectangleF(5f, 9.4f, 10f, 7.6f), 1.6f)) g.DrawPath(pen, p);
                        break;

                    case IconKind.Star:
                        g.FillEllipse(b, 8.6f, 3f, 3f, 3f);
                        g.DrawLine(pen, 10f, 6.2f, 10f, 17f);
                        break;

                    case IconKind.Refresh:
                        g.DrawArc(pen, 3f, 3f, 14f, 14f, 40f, 280f);
                        using (var p = new GraphicsPath())
                        {
                            p.AddPolygon(new[] { new PointF(14.4f, 1.6f), new PointF(18.4f, 4.4f), new PointF(13.6f, 5.6f) });
                            g.FillPath(b, p);
                        }
                        break;

                    case IconKind.Add:
                        g.DrawLine(pen, 10f, 3.4f, 10f, 16.6f);
                        g.DrawLine(pen, 3.4f, 10f, 16.6f, 10f);
                        break;

                    case IconKind.Folder:
                        using (var p = Theme.Rounded(new RectangleF(3f, 5.4f, 14f, 11f), 1.8f)) g.DrawPath(pen, p);
                        g.DrawLines(pen, new[] { new PointF(3f, 8.6f), new PointF(7.6f, 8.6f), new PointF(9.4f, 5.4f) });
                        break;

                    case IconKind.Export:
                        g.DrawLine(pen, 10f, 3f, 10f, 13f);
                        g.DrawLines(pen, new[] { new PointF(6.2f, 6.4f), new PointF(10f, 2.6f), new PointF(13.8f, 6.4f) });
                        using (var p = Theme.Rounded(new RectangleF(3f, 12f, 14f, 5.4f), 1.6f)) g.DrawPath(pen, p);
                        break;

                    case IconKind.Trash:
                        g.DrawLine(pen, 3.4f, 5.6f, 16.6f, 5.6f);
                        g.DrawLine(pen, 7.6f, 3f, 12.4f, 3f);
                        using (var p = Theme.Rounded(new RectangleF(5f, 5.6f, 10f, 11.4f), 1.8f)) g.DrawPath(pen, p);
                        g.DrawLine(pen, 8.4f, 8.6f, 8.4f, 14f);
                        g.DrawLine(pen, 11.6f, 8.6f, 11.6f, 14f);
                        break;

                    case IconKind.Edit:
                        using (var p = Theme.Rounded(new RectangleF(3f, 13.6f, 12f, 4f), 1.2f)) g.FillPath(b, p);
                        g.DrawLine(pen, 4.6f, 12.6f, 14.4f, 3.6f);
                        g.DrawLine(pen, 4.6f, 12.6f, 4.6f, 9.4f);
                        break;

                    case IconKind.Random:
                        g.DrawRectangle(pen, 3f, 3f, 14f, 14f);
                        g.FillEllipse(b, 6.4f, 6.4f, 2.4f, 2.4f);
                        g.FillEllipse(b, 11.2f, 6.4f, 2.4f, 2.4f);
                        g.FillEllipse(b, 6.4f, 11.2f, 2.4f, 2.4f);
                        g.FillEllipse(b, 11.2f, 11.2f, 2.4f, 2.4f);
                        break;

                    case IconKind.Undo:
                        g.DrawArc(pen, 3.4f, 4.4f, 13.2f, 13.2f, 200f, 240f);
                        using (var p = new GraphicsPath())
                        {
                            p.AddPolygon(new[] { new PointF(2.6f, 6.2f), new PointF(7.4f, 5.6f), new PointF(4.4f, 9.6f) });
                            g.FillPath(b, p);
                        }
                        break;

                    case IconKind.Lock:
                        using (var p = Theme.Rounded(new RectangleF(4f, 9f, 12f, 8.4f), 2f)) g.DrawPath(pen, p);
                        g.DrawArc(pen, 6.4f, 3.4f, 7.2f, 8.4f, 180f, 180f);
                        break;

                    case IconKind.Check:
                        g.DrawLines(pen, new[] { new PointF(3.6f, 10.6f), new PointF(8.2f, 15f), new PointF(16.6f, 5.4f) });
                        break;

                    case IconKind.Search:
                        g.DrawEllipse(pen, 3.4f, 3.4f, 10.2f, 10.2f);
                        g.DrawLine(pen, 12.6f, 12.6f, 17f, 17f);
                        break;

                    case IconKind.Info:
                        g.DrawEllipse(pen, 2.6f, 2.6f, 14.8f, 14.8f);
                        g.DrawLine(pen, 10f, 9f, 10f, 14.2f);
                        g.FillEllipse(b, 9.1f, 5.4f, 1.9f, 1.9f);
                        break;

                    case IconKind.Warning:
                        g.DrawLines(pen, new[] { new PointF(10f, 2.8f), new PointF(18f, 17f), new PointF(2f, 17f), new PointF(10f, 2.8f) });
                        g.DrawLine(pen, 10f, 8f, 10f, 12.6f);
                        g.FillEllipse(b, 9.1f, 13.8f, 1.9f, 1.9f);
                        break;

                    case IconKind.Key:
                        g.DrawEllipse(pen, 2.6f, 6.4f, 7.2f, 7.2f);
                        g.DrawLine(pen, 9.4f, 10f, 17.4f, 10f);
                        g.DrawLine(pen, 14f, 10f, 14f, 13.4f);
                        g.DrawLine(pen, 17f, 10f, 17f, 12.6f);
                        break;

                    case IconKind.More:
                        g.FillEllipse(b, 3.6f, 8.8f, 2.4f, 2.4f);
                        g.FillEllipse(b, 8.8f, 8.8f, 2.4f, 2.4f);
                        g.FillEllipse(b, 14f, 8.8f, 2.4f, 2.4f);
                        break;

                    case IconKind.Person:
                        g.FillEllipse(b, 5.6f, 2.8f, 8.8f, 8.8f);
                        using (var p = Theme.Rounded(new RectangleF(2.8f, 12.6f, 14.4f, 7.4f), 3.4f)) g.FillPath(b, p);
                        break;

                    case IconKind.Chevron:
                        g.DrawLines(pen, new[] { new PointF(6.4f, 8f), new PointF(10f, 12f), new PointF(13.6f, 8f) });
                        break;

                    case IconKind.SignOut:
                        g.DrawLines(pen, new[] { new PointF(11.6f, 3f), new PointF(3.6f, 3f), new PointF(3.6f, 17f), new PointF(11.6f, 17f) });
                        g.DrawLine(pen, 9f, 10f, 17.4f, 10f);
                        g.DrawLines(pen, new[] { new PointF(14.2f, 6.6f), new PointF(17.6f, 10f), new PointF(14.2f, 13.4f) });
                        break;

                    case IconKind.Filter:
                        g.DrawLines(pen, new[] { new PointF(3f, 5f), new PointF(17f, 5f), new PointF(11.4f, 11f), new PointF(11.4f, 16.4f), new PointF(8.6f, 14.6f), new PointF(8.6f, 11f), new PointF(3f, 5f) });
                        break;

                    case IconKind.Calendar:
                        using (var p = Theme.Rounded(new RectangleF(2.6f, 4.4f, 14.8f, 13f), 2f)) g.DrawPath(pen, p);
                        g.DrawLine(pen, 2.6f, 8.6f, 17.4f, 8.6f);
                        g.DrawLine(pen, 6.6f, 2.4f, 6.6f, 5.4f);
                        g.DrawLine(pen, 13.4f, 2.4f, 13.4f, 5.4f);
                        break;
                }
            }
        }
    }
}
