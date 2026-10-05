using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Tensyan.Core;

namespace Tensyan.UI
{
    /// <summary>学生卡片（Fluent）：头像 + 姓名 + 积分，悬停出现加减分按钮。</summary>
    public class StudentCard : Control
    {
        public Student Student;
        public App App;
        public int RankNo;
        public bool Selected;
        public bool BatchMode;

        public event EventHandler<ScoreAskArgs> ScoreAsked;
        public event EventHandler OpenAsked;
        public event EventHandler ContextAsked;

        private bool _hover;
        private double _hoverT;
        private double _pulse;        // 0..1 鼓包
        private double _flash;        // 边框/描边高亮
        private double _dispScore;    // 平滑显示的分数
        private bool _dispInit;

        private const int CardRadius = 8;

        public StudentCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = Theme.PS(172, 210);
            Cursor = Cursors.Hand;
            Font = Theme.Body();
        }

        public void Pulse()
        {
            if (Student == null) return;
            if (!_dispInit) { _dispScore = Student.Score; _dispInit = true; }

            double from = _dispScore, to = Student.Score;
            bool animated = App == null || App.Settings.AnimationEnabled;
            if (!animated)
            {
                _dispScore = to; _pulse = 0; _flash = 0;
                Invalidate();
                return;
            }

            Anim.Run(this, 560, t =>
            {
                if (IsDisposed) return;
                _pulse = Anim.Bump(t, 0.30);
                _flash = 1 - Anim.EaseOutCubic(t);
                _dispScore = Anim.Lerp(from, to, Anim.EaseOutCubic(t));
                Invalidate();
            }, () =>
            {
                if (IsDisposed) return;
                _pulse = 0; _flash = 0; _dispScore = Student.Score;
                Invalidate();
            });
        }

        private void TweenHover(double target)
        {
            double from = _hoverT;
            if (Math.Abs(from - target) < 0.002) { _hoverT = target; Invalidate(); return; }
            Anim.Run(this, 150, t =>
            {
                if (IsDisposed) return;
                _hoverT = Anim.Lerp(from, target, Anim.EaseOutQuad(t));
                Invalidate();
            });
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; TweenHover(1); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; TweenHover(0); base.OnMouseLeave(e); }

        private bool CanAdd { get { return App != null && App.Session.CanAddScore; } }
        private bool CanSub { get { return App != null && App.Session.CanSubScore; } }

        private Rectangle PlusRect()
        {
            int bw = Theme.P(CanSub ? 44 : 60), bh = Theme.P(30), gap = Theme.P(10);
            int total = CanSub ? bw * 2 + gap : bw;
            int x = (Width - total) / 2;
            int y = Height - bh - Theme.P(14);
            return new Rectangle(x, y, bw, bh);
        }

        private Rectangle MinusRect()
        {
            var p = PlusRect();
            return new Rectangle(p.Right + Theme.P(10), p.Y, p.Width, p.Height);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (Student == null) return;

            if (e.Button == MouseButtons.Right)
            {
                var h = ContextAsked;
                if (h != null) h(this, EventArgs.Empty);
                return;
            }

            if (BatchMode)
            {
                Selected = !Selected;
                Invalidate();
                return;
            }

            if (CanAdd && PlusRect().Contains(e.Location))
            {
                var h = ScoreAsked; if (h != null) h(this, new ScoreAskArgs(1));
                return;
            }
            if (CanSub && MinusRect().Contains(e.Location))
            {
                var h = ScoreAsked; if (h != null) h(this, new ScoreAskArgs(-1));
                return;
            }

            var o = OpenAsked; if (o != null) o(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            if (Student == null) return;
            if (!_dispInit) { _dispScore = Student.Score; _dispInit = true; }

            int score = (int)Math.Round(_dispScore);
            Color sc = Theme.ScoreColor(score);
            int radius = Theme.P(CardRadius);

            var card = new Rectangle(Theme.P(1), Theme.P(1), Width - Theme.P(3), Height - Theme.P(3));

            if (_hoverT > 0.35 || Selected) Theme.SoftShadow(g, card, radius);
            Theme.FillRounded(g, card, radius, Theme.Card);

            Color border = Selected ? Theme.Primary : Theme.Mix(Theme.Border, Theme.BorderStrong, _hoverT);
            if (_flash > 0.01) border = Theme.Mix(border, sc, _flash * 0.75);
            Theme.StrokeRounded(g, card, radius, border, Theme.PF(Selected ? 1.6f : (float)(1 + 0.6 * _flash)));

            // 名次
            if (RankNo > 0)
            {
                if (RankNo <= 3)
                {
                    Color rc = RankNo == 1 ? Theme.Amber : (RankNo == 2 ? Theme.Hex("#6E6E6E") : Theme.Hex("#8A5A2B"));
                    int d = Theme.P(22);
                    var badge = new Rectangle(card.X + Theme.P(10), card.Y + Theme.P(10), d, d);
                    Theme.FillRounded(g, badge, d / 2, Theme.Mix(rc, Color.White, 0.82));
                    TextRenderer.DrawText(g, RankNo.ToString(), Theme.Caption(), badge, rc,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                else
                {
                    TextRenderer.DrawText(g, RankNo.ToString(), Theme.Caption(),
                        new Rectangle(card.X + Theme.P(10), card.Y + Theme.P(10), Theme.P(30), Theme.P(16)), Theme.TextFaint,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                }
            }

            // 头像
            int av = Theme.P(56);
            float scale = (float)(1f + _pulse * 0.13f);
            int avSize = (int)(av * scale);
            var ar = new Rectangle((Width - avSize) / 2, card.Y + Theme.P(18) - (avSize - av) / 2, avSize, avSize);
            Avatar.Draw(g, ar, Student.Name, Student.Id, _hoverT > 0.45 ? sc : (Color?)null);

            // 姓名
            var nameRect = new Rectangle(card.X + Theme.P(6), card.Y + Theme.P(82), card.Width - Theme.P(12), Theme.LineHeight(Theme.BodyStrong()));
            TextRenderer.DrawText(g, Student.Name, Theme.BodyStrong(), nameRect, Theme.TextMain,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            // 积分
            string scoreText = (score > 0 ? "+" : "") + score;
            var sRect = new Rectangle(card.X + Theme.P(6), card.Y + Theme.P(108), card.Width - Theme.P(12), Theme.LineHeight(Theme.Title()));
            TextRenderer.DrawText(g, scoreText, Theme.Title(), sRect, sc,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            TextRenderer.DrawText(g, ScoreLevelText(score), Theme.Caption(),
                new Rectangle(card.X + Theme.P(6), card.Y + Theme.P(140), card.Width - Theme.P(12), Theme.LineHeight(Theme.Caption())), Theme.TextFaint,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            // 加减分按钮
            if (CanAdd)
            {
                var pr = PlusRect();
                Color baseColor = pr.Contains(PointToClient(MousePosition)) && _hover ? Theme.GreenDark : Theme.Green;
                Theme.FillRounded(g, pr, Theme.P(4), baseColor);
                DrawPlus(g, pr, Color.White);
            }
            if (CanSub)
            {
                var mr = MinusRect();
                Color baseColor = mr.Contains(PointToClient(MousePosition)) && _hover ? Theme.RedDark : Theme.Red;
                Theme.FillRounded(g, mr, Theme.P(4), baseColor);
                DrawMinus(g, mr, Color.White);
            }
            if (!CanAdd && !CanSub)
            {
                var pill = new Rectangle(Width / 2 - Theme.P(30), Height - Theme.P(36), Theme.P(60), Theme.P(22));
                Theme.FillRounded(g, pill, Theme.P(11), Theme.BgDeep);
                TextRenderer.DrawText(g, "只读", Theme.Caption(), pill, Theme.TextSub,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            if (Student.Archived)
            {
                using (var b = new SolidBrush(Color.FromArgb(160, Theme.Card)))
                    using (var p = Theme.Rounded(card, radius))
                        g.FillPath(b, p);
                TextRenderer.DrawText(g, "已归档", Theme.BodyStrong(), card, Theme.TextFaint,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private static string ScoreLevelText(int s)
        {
            if (s >= 30) return "表现优异";
            if (s >= 15) return "表现良好";
            if (s > 0) return "继续加油";
            if (s == 0) return "暂未得分";
            return "需要努力";
        }

        private static void DrawPlus(Graphics g, Rectangle r, Color c)
        {
            using (var pen = new Pen(c, Theme.PF(1.8f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                int cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, len = Theme.P(5);
                g.DrawLine(pen, cx - len, cy, cx + len, cy);
                g.DrawLine(pen, cx, cy - len, cx, cy + len);
            }
        }

        private static void DrawMinus(Graphics g, Rectangle r, Color c)
        {
            using (var pen = new Pen(c, Theme.PF(1.8f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                int cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, len = Theme.P(5);
                g.DrawLine(pen, cx - len, cy, cx + len, cy);
            }
        }
    }

    public class ScoreAskArgs : EventArgs
    {
        public int Delta;
        public ScoreAskArgs(int delta) { Delta = delta; }
    }
}
