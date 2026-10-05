using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Tensyan.Core;

namespace Tensyan.UI.Pages
{
    /// <summary>积分排行（Fluent）：前三名领奖台 + 完整榜单。</summary>
    public class PageRank : PageBase
    {
        private FluentSelect _range;
        private Panel _rowsPanel;
        private PodiumPanel _podium;
        private readonly List<RankRow> _rows = new List<RankRow>();

        public PageRank(App app) : base(app)
        {
            TitleText = "积分排行";
            EnableTools(52);

            _range = new FluentSelect { Location = Theme.PP(0, 9), Size = Theme.PS(180, 34) };
            _range.AddItems("总积分排行", "本周积分排行", "本月积分排行", "今日积分排行");
            _range.SelectedIndex = 0;
            _range.SelectedIndexChanged += (s, e) => Reload();
            Tools.Controls.Add(_range);

            _podium = new PodiumPanel { BackColor = Theme.Bg };
            Body.Controls.Add(_podium);
            _rowsPanel = new Panel { AutoScroll = true, BackColor = Theme.Bg };
            Body.Controls.Add(_rowsPanel);
        }

        protected override void Layout3()
        {
            base.Layout3();
            _podium.SetBounds(0, 0, Body.Width, Theme.P(260));
            _rowsPanel.SetBounds(0, Theme.P(272), Body.Width, Math.Max(0, Body.Height - Theme.P(272)));
            LayoutRows();
        }

        private void LayoutRows()
        {
            if (_rowsPanel == null) return;
            int w = Math.Max(Theme.P(420), _rowsPanel.ClientSize.Width - Theme.P(26));
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].SetBounds(0, i * Theme.P(64), w, Theme.P(56));
        }

        private Dictionary<string, int> ComputeScores(out string rangeText)
        {
            var map = new Dictionary<string, int>();
            int idx = _range == null ? 0 : _range.SelectedIndex;
            rangeText = "总积分";
            if (idx == 0 || App.Current == null)
            {
                if (App.Current != null) foreach (var s in App.Current.Students) map[s.Id] = s.Score;
                return map;
            }

            DateTime from;
            if (idx == 1)
            {
                int diff = (int)DateTime.Now.DayOfWeek;
                if (diff == 0) diff = 7;
                from = DateTime.Today.AddDays(-(diff - 1));
                rangeText = "本周（自 " + from.ToString("MM-dd") + " 起）";
            }
            else if (idx == 2)
            {
                from = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                rangeText = "本月（自 " + from.ToString("MM-dd") + " 起）";
            }
            else
            {
                from = DateTime.Today;
                rangeText = "今日";
            }

            foreach (var s in App.Current.Students) map[s.Id] = 0;
            foreach (var e in App.Data.Events)
            {
                if (e.Undone || e.ClassId != App.Current.Id) continue;
                if (e.TimeUtc.ToLocalTime() < from) continue;
                if (!map.ContainsKey(e.StudentId)) continue;
                map[e.StudentId] += e.Delta;
            }
            return map;
        }

        public override void Reload()
        {
            if (App.Current == null) return;
            _rowsPanel.SuspendLayout();
            _rowsPanel.Controls.Clear();
            _rows.Clear();

            string rangeText;
            var scores = ComputeScores(out rangeText);

            var list = new List<Student>();
            foreach (var s in App.Current.Students) if (!s.Archived) list.Add(s);
            list.Sort((a, b) =>
            {
                int c = scores[b.Id].CompareTo(scores[a.Id]);
                return c != 0 ? c : string.CompareOrdinal(a.Name, b.Name);
            });

            _podium.Items = new List<PodiumItem>();
            for (int i = 0; i < list.Count && i < 3; i++)
                _podium.Items.Add(new PodiumItem { Name = list[i].Name, Seed = list[i].Id, Score = scores[list[i].Id], Rank = i + 1 });
            _podium.Invalidate();

            int max = 1;
            foreach (var s in list) if (Math.Abs(scores[s.Id]) > max) max = Math.Abs(scores[s.Id]);

            for (int i = 0; i < list.Count; i++)
            {
                var row = new RankRow { Rank = i + 1, Stu = list[i], Score = scores[list[i].Id], MaxScore = max };
                _rows.Add(row);
                _rowsPanel.Controls.Add(row);
            }
            _rowsPanel.ResumeLayout();
            LayoutRows();

            SetSub(App.Current.Name + "  ·  " + rangeText + "  ·  共 " + list.Count + " 名学生");
        }

        public class PodiumItem
        {
            public string Name;
            public string Seed;
            public int Score;
            public int Rank;
        }

        private class PodiumPanel : Control
        {
            public List<PodiumItem> Items = new List<PodiumItem>();

            public PodiumPanel()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                BackColor = Theme.Bg;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(BackColor);
                if (Items.Count == 0) return;

                var card = new Rectangle(0, 0, Width - 1, Height - 1);
                Theme.CardSurface(g, card, Theme.P(8));

                TextRenderer.DrawText(g, "前三名", Theme.Caption(),
                    new Rectangle(Theme.P(20), Theme.P(14), Theme.P(100), Theme.LineHeight(Theme.Caption())), Theme.TextFaint,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                int cx = Width / 2;
                int baseY = Height - Theme.P(30);
                int[] order = { 2, 1, 3 };
                int[] xs = { cx - Theme.P(190), cx, cx + Theme.P(190) };
                int[] heights = { Theme.P(58), Theme.P(84), Theme.P(46) };
                int[] avs = { Theme.P(46), Theme.P(54), Theme.P(42) };
                Color[] colors = { Theme.Hex("#6E6E6E"), Theme.Amber, Theme.Hex("#8A5A2B") };

                for (int k = 0; k < order.Length; k++)
                {
                    var it = Find(order[k]);
                    if (it == null) continue;

                    int x = xs[k];
                    int h = heights[k];
                    int av = avs[k];
                    var ped = new Rectangle(x - Theme.P(62), baseY - h, Theme.P(124), h);
                    Theme.FillRounded(g, ped, Theme.P(6), Theme.Mix(colors[k], Color.White, 0.86));
                    Theme.StrokeRounded(g, ped, Theme.P(6), Theme.Mix(colors[k], Color.White, 0.68));

                    TextRenderer.DrawText(g, order[k].ToString(), Theme.Title(), ped, colors[k],
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                    var ar = new Rectangle(x - av / 2, ped.Y - Theme.P(10) - av, av, av);
                    Avatar.Draw(g, ar, it.Name, it.Seed, colors[k]);

                    TextRenderer.DrawText(g, it.Name, Theme.BodyStrong(),
                        new Rectangle(x - Theme.P(80), ar.Y - Theme.P(42), Theme.P(160), Theme.LineHeight(Theme.BodyStrong())), Theme.TextMain,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                    string sc = (it.Score > 0 ? "+" : "") + it.Score + " 分";
                    TextRenderer.DrawText(g, sc, Theme.Caption(),
                        new Rectangle(x - Theme.P(80), ar.Y - Theme.P(20), Theme.P(160), Theme.LineHeight(Theme.Caption())), colors[k],
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }

            private PodiumItem Find(int rank)
            {
                foreach (var i in Items) if (i.Rank == rank) return i;
                return null;
            }
        }
    }

    /// <summary>排行榜中的一行（Fluent 卡片样式）。</summary>
    public class RankRow : Control
    {
        public int Rank;
        public Student Stu;
        public int Score;
        public int MaxScore = 1;
        private bool _hover;

        public RankRow()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            Height = Theme.P(56);
            Cursor = Cursors.Hand;
            Font = Theme.Body();
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (Stu == null) return;
            using (var d = new StudentDetailDialog(App.I, Stu, App.I.Session.CanAddScore))
                d.ShowDialog(this);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            if (Stu == null) return;

            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRounded(g, r, Theme.P(8), Theme.Card);
            Theme.StrokeRounded(g, r, Theme.P(8), _hover ? Theme.BorderStrong : Theme.Border);

            Color rc = Rank == 1 ? Theme.Amber : (Rank == 2 ? Theme.Hex("#6E6E6E") : (Rank == 3 ? Theme.Hex("#8A5A2B") : Theme.TextFaint));
            TextRenderer.DrawText(g, Rank.ToString(), Theme.BodyStrong(),
                new Rectangle(Theme.P(18), 0, Theme.P(40), Height), rc,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            Avatar.Draw(g, new Rectangle(Theme.P(56), (Height - Theme.P(36)) / 2, Theme.P(36), Theme.P(36)), Stu.Name, Stu.Id, null);

            TextRenderer.DrawText(g, Stu.Name, Theme.BodyStrong(),
                new Rectangle(Theme.P(104), 0, Theme.P(120), Height), Theme.TextMain,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            int barX = Theme.P(238);
            int barW = Math.Max(Theme.P(80), Width - barX - Theme.P(120));
            int barH = Theme.P(8);
            var barBg = new Rectangle(barX, (Height - barH) / 2, barW, barH);
            Theme.FillRounded(g, barBg, barH / 2, Theme.BgDeep);
            double ratio = MaxScore <= 0 ? 0 : Math.Min(1.0, Math.Abs(Score) / (double)MaxScore);
            int fillW = (int)(barW * ratio);
            if (fillW > Theme.P(4))
            {
                var bar = new Rectangle(barX, barBg.Y, fillW, barH);
                Theme.FillRounded(g, bar, barH / 2, Theme.ScoreColor(Score));
            }

            string sc = (Score > 0 ? "+" : "") + Score;
            TextRenderer.DrawText(g, sc, Theme.Subtitle(),
                new Rectangle(Width - Theme.P(116), 0, Theme.P(96), Height), Theme.ScoreColor(Score),
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }
    }
}
