using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Tensyan.Core;

namespace Tensyan.UI.Pages
{
    /// <summary>数据统计：概览数字 + 每日趋势 + 理由分布 + 积分段分布。</summary>
    public class PageStats : PageBase
    {
        private StatCard _c1, _c2, _c3, _c4;
        private ChartPanel _trend, _reasons, _levels;
        private FluentSelect _scope;

        public PageStats(App app) : base(app)
        {
            TitleText = "数据统计";
            EnableTools(52);

            _scope = new FluentSelect { Location = Theme.PP(0, 9), Size = Theme.PS(180, 34) };
            _scope.AddItems("当前班级", "最近 7 天", "最近 30 天", "本学期全部");
            _scope.SelectedIndex = 0;
            _scope.SelectedIndexChanged += (s, e) => Reload();
            Tools.Controls.Add(_scope);

            _c1 = new StatCard("学生总数", Theme.Primary, IconKind.Students);
            _c2 = new StatCard("班级总积分", Theme.Green, IconKind.Star);
            _c3 = new StatCard("今日加分", Theme.Amber, IconKind.Add);
            _c4 = new StatCard("人均积分", Theme.Purple, IconKind.Stats);
            Body.Controls.Add(_c1); Body.Controls.Add(_c2); Body.Controls.Add(_c3); Body.Controls.Add(_c4);

            _trend = new ChartPanel("每日加减分趋势（近 14 天）") { Render = DrawTrend };
            _reasons = new ChartPanel("加分理由分布") { Render = DrawReasons };
            _levels = new ChartPanel("积分段分布") { Render = DrawLevels };
            Body.Controls.Add(_trend); Body.Controls.Add(_reasons); Body.Controls.Add(_levels);
        }

        protected override void Layout3()
        {
            base.Layout3();
            int gap = Theme.P(14);
            int w = Body.Width;
            int cw = (w - gap * 3) / 4;
            int ch = Theme.P(102);
            _c1.SetBounds(0, 0, cw, ch);
            _c2.SetBounds(cw + gap, 0, cw, ch);
            _c3.SetBounds((cw + gap) * 2, 0, cw, ch);
            _c4.SetBounds((cw + gap) * 3, 0, cw, ch);

            int top = ch + gap;
            int h = Math.Max(Theme.P(150), (Body.Height - top - gap) / 2);
            int wide = (w - gap) * 2 / 3;
            _trend.SetBounds(0, top, wide, h);
            _reasons.SetBounds(wide + gap, top, w - wide - gap, h);
            _levels.SetBounds(0, top + h + gap, w, Math.Max(Theme.P(120), Body.Height - top - h - gap));
        }

        private DateTime ScopeFrom()
        {
            switch (_scope.SelectedIndex)
            {
                case 1: return DateTime.Today.AddDays(-6);
                case 2: return DateTime.Today.AddDays(-29);
                case 3: return DateTime.Today.AddDays(-180);
                default: return DateTime.MinValue;
            }
        }

        public override void Reload()
        {
            if (App.Current == null) return;
            var cls = App.Current;
            var from = ScopeFrom();

            int active = 0, total = 0;
            foreach (var s in cls.Students) if (!s.Archived) { active++; total += s.Score; }

            int todayAdd = 0, todaySub = 0, rangeAdd = 0, rangeSub = 0;
            foreach (var e in App.Data.Events)
            {
                if (e.Undone || e.ClassId != cls.Id) continue;
                var t = e.TimeUtc.ToLocalTime();
                if (t >= DateTime.Today)
                {
                    if (e.Delta > 0) todayAdd += e.Delta; else todaySub += e.Delta;
                }
                if (t >= from)
                {
                    if (e.Delta > 0) rangeAdd += e.Delta; else rangeSub += e.Delta;
                }
            }

            _c1.Value = active.ToString();
            _c1.Sub = cls.Name + (string.IsNullOrEmpty(cls.Teacher) ? "" : " · " + cls.Teacher);
            _c2.Value = total.ToString();
            _c2.Sub = "平均 " + (active == 0 ? 0.0 : Math.Round(total / (double)active, 1)) + " 分/人";
            _c3.Value = "+" + todayAdd;
            _c3.Sub = "今日扣分 " + todaySub;
            _c4.Value = (active == 0 ? 0.0 : Math.Round(total / (double)active, 1)).ToString();
            _c4.Sub = _scope.SelectedIndex == 0 ? "全部时间" : "区间内 " + (rangeAdd + rangeSub);
            _c1.Invalidate(); _c2.Invalidate(); _c3.Invalidate(); _c4.Invalidate();

            _trend.Invalidate(); _reasons.Invalidate(); _levels.Invalidate();
            SetSub(cls.Name + "  ·  统计范围：" + _scope.SelectedItem + "  ·  数据来源：每一次加减分记录");
        }

        private void DrawTrend(Graphics g, Rectangle r)
        {
            var cls = App.Current;
            if (cls == null) return;

            var days = new List<DateTime>();
            for (int i = 13; i >= 0; i--) days.Add(DateTime.Today.AddDays(-i));
            var add = new int[14];
            var sub = new int[14];
            foreach (var e in App.Data.Events)
            {
                if (e.Undone || e.ClassId != cls.Id) continue;
                var t = e.TimeUtc.ToLocalTime().Date;
                int idx = days.IndexOf(t);
                if (idx < 0) continue;
                if (e.Delta > 0) add[idx] += e.Delta; else sub[idx] += -e.Delta;
            }

            int max = 1;
            for (int i = 0; i < 14; i++) max = Math.Max(max, Math.Max(add[i], sub[i]));

            int left = r.X + Theme.P(40), bottom = r.Y + r.Height - Theme.P(24), top = r.Y + Theme.P(10);
            int usable = bottom - top;
            int bw = Math.Max(Theme.P(8), (r.Width - Theme.P(60)) / 14 - Theme.P(8));
            int stepX = (r.Width - Theme.P(56)) / 14;

            using (var pen = new Pen(Theme.BorderSoft))
                for (int i = 0; i <= 4; i++)
                {
                    int y = top + usable * i / 4;
                    g.DrawLine(pen, left - Theme.P(6), y, r.Right - Theme.P(8), y);
                }
            using (var f = Theme.Caption())
            using (var b = new SolidBrush(Theme.TextFaint))
            {
                g.DrawString(max.ToString(), f, b, r.X + Theme.P(4), top - Theme.P(8));
                g.DrawString("0", f, b, r.X + Theme.P(18), bottom - Theme.P(8));
            }

            for (int i = 0; i < 14; i++)
            {
                int x = left + i * stepX + Theme.P(2);
                int ha = (int)(usable * (add[i] / (double)max));
                int hs = (int)(usable * (sub[i] / (double)max));
                if (add[i] > 0)
                {
                    var rect = new Rectangle(x, bottom - ha, bw, Math.Max(Theme.P(3), ha));
                    Theme.FillRounded(g, rect, Theme.P(3), Theme.Green);
                }
                if (sub[i] > 0)
                {
                    var rect = new Rectangle(x + bw + Theme.P(2), bottom - hs, bw, Math.Max(Theme.P(3), hs));
                    Theme.FillRounded(g, rect, Theme.P(3), Theme.Red);
                }
                if (i % 2 == 0)
                {
                    using (var f = Theme.Caption())
                    using (var b = new SolidBrush(Theme.TextFaint))
                    {
                        string lbl = days[i].ToString("MM-dd");
                        var sz = g.MeasureString(lbl, f);
                        float lx = x + bw - sz.Width / 2 + Theme.P(1);
                        if (lx + sz.Width > r.Right - Theme.P(2)) lx = r.Right - Theme.P(2) - sz.Width;
                        if (lx < r.X) lx = r.X;
                        g.DrawString(lbl, f, b, lx, bottom + Theme.P(4));
                    }
                }
            }
        }

        private void DrawReasons(Graphics g, Rectangle r)
        {
            var cls = App.Current;
            if (cls == null) return;
            var map = new Dictionary<string, int>();
            foreach (var e in App.Data.Events)
            {
                if (e.Undone || e.ClassId != cls.Id || e.Delta <= 0) continue;
                string k = Str.Blank(e.Reason) ? "未标注" : e.Reason;
                if (!map.ContainsKey(k)) map[k] = 0;
                map[k] += e.Delta;
            }
            var list = new List<KeyValuePair<string, int>>(map);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            if (list.Count > 8) list.RemoveRange(8, list.Count - 8);

            if (list.Count == 0)
            {
                using (var f = Theme.Body())
                using (var b = new SolidBrush(Theme.TextFaint))
                    g.DrawString("暂无加分记录", f, b, r.X, r.Y + Theme.P(10));
                return;
            }

            int max = Math.Max(1, list[0].Value);
            int rowH = Math.Max(Theme.P(20), Math.Min(Theme.P(32), r.Height / list.Count));
            int y = r.Y + Theme.P(4);
            foreach (var kv in list)
            {
                using (var f = Theme.Caption())
                using (var b = new SolidBrush(Theme.TextMain))
                    g.DrawString(kv.Key, f, b, r.X, y + Theme.P(6));

                int barX = r.X + Theme.P(104);
                int barW = Math.Max(Theme.P(40), r.Width - Theme.P(170));
                var barBg = new Rectangle(barX, y + Theme.P(8), barW, Theme.P(10));
                Theme.FillRounded(g, barBg, Theme.P(5), Theme.BgDeep);
                int w = (int)(barW * (kv.Value / (double)max));
                if (w > Theme.P(3))
                    Theme.FillRounded(g, new Rectangle(barX, barBg.Y, w, Theme.P(10)), Theme.P(5), Theme.Primary);

                using (var f = Theme.Caption())
                using (var b = new SolidBrush(Theme.TextSub))
                    g.DrawString("+" + kv.Value, f, b, r.Right - Theme.P(48), y + Theme.P(6));
                y += rowH;
            }
        }

        private void DrawLevels(Graphics g, Rectangle r)
        {
            var cls = App.Current;
            if (cls == null) return;
            string[] names = { "负分", "0 分", "1-9 分", "10-19 分", "20-29 分", "30 分以上" };
            int[] counts = new int[6];
            foreach (var s in cls.Students)
            {
                if (s.Archived) continue;
                if (s.Score < 0) counts[0]++;
                else if (s.Score == 0) counts[1]++;
                else if (s.Score < 10) counts[2]++;
                else if (s.Score < 20) counts[3]++;
                else if (s.Score < 30) counts[4]++;
                else counts[5]++;
            }
            int total = 0;
            foreach (int c in counts) total += c;
            if (total == 0) return;

            int max = 1;
            foreach (int c in counts) max = Math.Max(max, c);

            int colW = Math.Max(Theme.P(60), (r.Width - Theme.P(20)) / 6);
            int bottom = r.Bottom - Theme.P(24);
            int usable = Math.Max(Theme.P(40), r.Height - Theme.P(50));
            Color[] colors = { Theme.Red, Theme.TextFaint, Theme.Primary, Theme.Green, Theme.Amber, Theme.Purple };

            for (int i = 0; i < 6; i++)
            {
                int h = (int)(usable * (counts[i] / (double)max));
                var rect = new Rectangle(r.X + Theme.P(10) + i * colW, bottom - h, colW - Theme.P(22), Math.Max(Theme.P(2), h));
                Theme.FillRounded(g, rect, Theme.P(4), colors[i]);

                using (var f = Theme.Caption())
                using (var b = new SolidBrush(Theme.TextMain))
                {
                    var sz = g.MeasureString(counts[i] + " 人", f);
                    float lx = rect.X + rect.Width / 2f - sz.Width / 2f;
                    if (lx < r.X) lx = r.X;
                    if (lx + sz.Width > r.Right) lx = r.Right - sz.Width;
                    g.DrawString(counts[i] + " 人", f, b, lx, rect.Y - Theme.P(20));
                }

                using (var f = Theme.Caption())
                using (var b = new SolidBrush(Theme.TextSub))
                    g.DrawString(names[i], f, b, r.X + Theme.P(10) + i * colW + Theme.P(2), bottom + Theme.P(4));
            }
        }
    }

    /// <summary>概览数字卡片（Fluent）。</summary>
    public class StatCard : Control
    {
        public string Title;
        public string Value = "-";
        public string Sub = "";
        public Color Accent;
        public IconKind Icon;

        public StatCard(string title, Color accent, IconKind icon)
        {
            Title = title; Accent = accent; Icon = icon;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.CardSurface(g, r, Theme.P(8));

            int tile = Theme.P(32);
            var box = new Rectangle(r.X + Theme.P(14), r.Y + Theme.P(15), tile, tile);
            Theme.FillRounded(g, box, Theme.P(6), Theme.Mix(Accent, Color.White, 0.88));
            int iconSize = Theme.P(18);
            Icons.Draw(g, new Rectangle(box.X + (tile - iconSize) / 2, box.Y + (tile - iconSize) / 2, iconSize, iconSize), Icon, Accent);

            TextRenderer.DrawText(g, Title, Theme.Caption(),
                new Rectangle(box.Right + Theme.P(10), r.Y + Theme.P(13), r.Width - box.Right - Theme.P(20), Theme.LineHeight(Theme.Caption())), Theme.TextSub,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            TextRenderer.DrawText(g, Value, Theme.Title(),
                new Rectangle(box.Right + Theme.P(8), r.Y + Theme.P(32), r.Width - box.Right - Theme.P(18), Theme.LineHeight(Theme.Title())), Theme.TextMain,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            TextRenderer.DrawText(g, Sub, Theme.Caption(),
                new Rectangle(r.X + Theme.P(14), r.Bottom - Theme.P(30), r.Width - Theme.P(28), Theme.LineHeight(Theme.Caption())), Theme.TextFaint,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>带标题的图表卡片（Fluent）。</summary>
    public class ChartPanel : Control
    {
        public string Title;
        public Action<Graphics, Rectangle> Render;

        public ChartPanel(string title)
        {
            Title = title;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.CardSurface(g, r, Theme.P(8));

            TextRenderer.DrawText(g, Title, Theme.BodyStrong(),
                new Rectangle(r.X + Theme.P(18), r.Y + Theme.P(12), r.Width - Theme.P(36), Theme.LineHeight(Theme.BodyStrong())), Theme.TextMain,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            var inner = new Rectangle(r.X + Theme.P(18), r.Y + Theme.P(46), r.Width - Theme.P(36), r.Height - Theme.P(62));
            if (inner.Width > Theme.P(30) && inner.Height > Theme.P(30) && Render != null) Render(g, inner);
        }
    }
}
