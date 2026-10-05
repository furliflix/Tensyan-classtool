using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Tensyan.Core;

namespace Tensyan.UI
{
    /// <summary>批量 / 全班加减分。</summary>
    public class BatchScoreDialog : DialogBase
    {
        private readonly App _app;
        private bool _add;
        private int _points = 1;
        private string _reason = "课堂表现";
        private RoundButton _btnAdd, _btnSub;
        private NumericUpDown _num;
        private Label _preview;
        private readonly List<RoundButton> _pts = new List<RoundButton>();
        private readonly List<RoundButton> _chips = new List<RoundButton>();
        private FlowLayoutPanel _chipPanel;

        public BatchScoreDialog(App app, string title, bool add)
            : base(title, 540, 520)
        {
            _app = app;
            _add = add;
            Build();
            SelectMode(add);
        }

        private void Build()
        {
            _btnAdd = new RoundButton
            {
                Text = "加分",
                Font = Theme.BodyStrong(),
                Size = Theme.PS(96, 36),
                Fill = Theme.Green,
                Location = Theme.PP(24, 54)
            };
            _btnAdd.Click += (s, e) => SelectMode(true);
            Body.Controls.Add(_btnAdd);

            if (_app.Session.CanSubScore)
            {
                _btnSub = new RoundButton
                {
                    Text = "扣分",
                    Font = Theme.BodyStrong(),
                    Size = Theme.PS(96, 36),
                    Soft = true,
                    Fill = Theme.Card,
                    OutlineColor = Theme.Red,
                    Location = Theme.PP(128, 54)
                };
                _btnSub.Click += (s, e) => SelectMode(false);
                Body.Controls.Add(_btnSub);
            }

            _preview = MkLabel("", 250, 62, Theme.BodyStrong(), Theme.Green);
            _preview.AutoSize = false;
            _preview.Size = new Size(Theme.P(266), Theme.LineHeight(Theme.BodyStrong()));
            _preview.TextAlign = ContentAlignment.MiddleRight;

            MkLabel("每人分值", 24, 108, Theme.BodyStrong(), Theme.TextSub);
            int px = 24;
            foreach (int p in new[] { 1, 2, 3, 5 })
            {
                var b = new RoundButton
                {
                    Text = "+" + p,
                    Size = Theme.PS(58, 34),
                    Soft = true,
                    Fill = Theme.Card,
                    OutlineColor = Theme.Green,
                    Location = Theme.PP(px, 130)
                };
                int v = p;
                b.Click += (s, e) => SetPoints(v);
                _pts.Add(b);
                Body.Controls.Add(b);
                px += 64;
            }
            _num = new NumericUpDown
            {
                Font = Theme.Body(),
                Minimum = 1,
                Maximum = 999,
                Value = 1,
                Location = Theme.PP(px + 6, 132),
                Width = Theme.P(80),
                TextAlign = HorizontalAlignment.Center
            };
            _num.ValueChanged += (s, e) => { _points = (int)_num.Value; UpdatePreview(); };
            Body.Controls.Add(_num);

            MkLabel("理由标签", 24, 186, Theme.BodyStrong(), Theme.TextSub);
            _chipPanel = new FlowLayoutPanel
            {
                Location = Theme.PP(24, 208),
                Size = Theme.PS(492, 150),
                AutoScroll = true,
                BackColor = Theme.Card
            };
            Body.Controls.Add(_chipPanel);
        }

        private void SelectMode(bool add)
        {
            _add = add;
            _btnAdd.Soft = !add;
            _btnAdd.Fill = add ? Theme.Green : Theme.Card;
            _btnAdd.OutlineColor = Theme.Green;
            _btnAdd.TextColor = add ? Color.White : Theme.Green;
            _btnAdd.Invalidate();

            if (_btnSub != null)
            {
                _btnSub.Soft = add;
                _btnSub.Fill = add ? Theme.Card : Theme.Red;
                _btnSub.OutlineColor = Theme.Red;
                _btnSub.TextColor = add ? Theme.Red : Color.White;
                _btnSub.Invalidate();
            }

            var list = add ? _app.Settings.QuickAdd : _app.Settings.QuickSub;
            for (int i = 0; i < _pts.Count; i++)
            {
                _pts[i].Text = (add ? "+" : "-") + (i < list.Count ? list[i] : i + 1);
                _pts[i].OutlineColor = add ? Theme.Green : Theme.Red;
                _pts[i].Invalidate();
            }

            _chipPanel.Controls.Clear();
            _chips.Clear();
            var tags = add ? _app.Settings.AddTags : _app.Settings.SubTags;
            foreach (var t in tags)
            {
                string text = t.Text;
                var b = new RoundButton
                {
                    Text = text,
                    Size = new Size(TextRenderer.MeasureText(text, Theme.Caption()).Width + Theme.P(30), Theme.P(32)),
                    Soft = true,
                    Fill = Theme.Card,
                    OutlineColor = Theme.BorderStrong,
                    Font = Theme.Caption(),
                    Radius = 16,
                    Margin = new Padding(0, 0, Theme.P(8), Theme.P(8))
                };
                var tag = t;
                b.Click += (s, e) =>
                {
                    _reason = tag.Text;
                    SetPoints(Math.Abs(tag.Points));
                    foreach (var c in _chips) { c.Fill = Theme.Card; c.OutlineColor = Theme.BorderStrong; c.Invalidate(); }
                    b.Fill = _add ? Theme.GreenSoft : Theme.RedSoft;
                    b.OutlineColor = _add ? Theme.Green : Theme.Red;
                    b.Invalidate();
                };
                _chips.Add(b);
                _chipPanel.Controls.Add(b);
            }
            UpdatePreview();
        }

        private void SetPoints(int p)
        {
            _points = Math.Max(1, Math.Min(999, p));
            _num.Value = _points;
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            _preview.Text = "每人 " + (_add ? "加 " + _points : "扣 " + _points) + " 分";
            _preview.ForeColor = _add ? Theme.Green : Theme.Red;
        }

        public int Delta { get { return _add ? _points : -_points; } }
        public string Reason { get { return _reason; } }
    }

    /// <summary>修改密码。</summary>
    public class ChangePasswordDialog : DialogBase
    {
        private readonly App _app;
        private readonly Account _acc;
        private readonly TextBox _old, _new1, _new2;
        public bool Changed;

        public ChangePasswordDialog(App app, Account acc, bool force = false)
            : base(force ? "请修改初始密码" : "修改密码", 460, force ? 360 : 340)
        {
            _app = app;
            _acc = acc;

            int y = 50;
            if (force)
            {
                MkLabel("首次登录建议立即修改密码，以保护班级数据。", 24, y, Theme.Caption(), Theme.Amber);
                y += 30;
            }

            MkLabel("当前密码", 24, y, Theme.BodyStrong(), Theme.TextSub);
            _old = MkInput(24, y + 22, 412);
            _old.UseSystemPasswordChar = true;
            y += 72;

            MkLabel("新密码（至少 3 位）", 24, y, Theme.BodyStrong(), Theme.TextSub);
            _new1 = MkInput(24, y + 22, 412);
            _new1.UseSystemPasswordChar = true;
            y += 72;

            MkLabel("确认新密码", 24, y, Theme.BodyStrong(), Theme.TextSub);
            _new2 = MkInput(24, y + 22, 412);
            _new2.UseSystemPasswordChar = true;

            BtnOk.Text = "保存";
            Shown += (s, e) => _old.Focus();
        }

        protected override bool OnOk()
        {
            if (!Auth.Verify(_acc, _old.Text)) { Toast.Show(this, "当前密码不正确", Theme.Red); return false; }
            if (_new1.Text.Length < 3) { Toast.Show(this, "新密码至少 3 位", Theme.Red); return false; }
            if (_new1.Text != _new2.Text) { Toast.Show(this, "两次输入的新密码不一致", Theme.Red); return false; }
            Auth.SetPassword(_acc, _new1.Text);
            _acc.MustChangePwd = false;
            _app.Store.Save();
            Changed = true;
            Toast.Show(this, "密码已修改", Theme.Green);
            return true;
        }
    }

    /// <summary>随机点名。</summary>
    public class RandomPickDialog : DialogBase
    {
        private readonly App _app;
        private Student _winner;
        private AvatarView _avatar;
        private Label _name, _tip;

        public RandomPickDialog(App app)
            : base("随机点名", 520, 470, false)
        {
            _app = app;
            BtnCancel.Text = "关闭";

            _avatar = new AvatarView { Location = Theme.PP(190, 40), Size = Theme.PS(140, 140), Seed = "roll" };
            Body.Controls.Add(_avatar);

            _name = new Label
            {
                Font = Theme.Display(),
                ForeColor = Theme.TextMain,
                AutoSize = false,
                Size = new Size(Theme.P(480), Theme.P(44)),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Location = Theme.PP(20, 188)
            };
            Body.Controls.Add(_name);

            _tip = new Label
            {
                Font = Theme.Body(),
                ForeColor = Theme.TextSub,
                AutoSize = false,
                Size = new Size(Theme.P(480), Theme.P(26)),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Location = Theme.PP(20, 234)
            };
            Body.Controls.Add(_tip);

            var again = new RoundButton
            {
                Text = "再抽一次",
                Icon = IconKind.Random,
                Size = Theme.PS(126, 40),
                Soft = true,
                Fill = Theme.Card,
                OutlineColor = Theme.BorderStrong,
                Location = Theme.PP(60, 288)
            };
            again.Click += (s, e) => Roll();
            Body.Controls.Add(again);

            var add1 = new RoundButton
            {
                Text = "加 1 分",
                Size = Theme.PS(110, 40),
                Fill = Theme.Green,
                Location = Theme.PP(200, 288)
            };
            add1.Click += (s, e) => Award(1);
            Body.Controls.Add(add1);

            var add2 = new RoundButton
            {
                Text = "加 2 分",
                Size = Theme.PS(110, 40),
                Fill = Theme.Green,
                Location = Theme.PP(320, 288)
            };
            add2.Click += (s, e) => Award(2);
            Body.Controls.Add(add2);

            MkLabel("抽中的同学会从当前班级未被归档的学生中随机产生。", 20, 344, Theme.Caption(), Theme.TextFaint, 480);

            Shown += (s, e) => Roll();
        }

        /// <summary>抽签：换人频率逐渐变慢（缓出），最后弹出中奖者。</summary>
        private void Roll()
        {
            var pool = new List<Student>();
            if (_app.Current != null)
                foreach (var s in _app.Current.Students) if (!s.Archived) pool.Add(s);
            if (pool.Count == 0) return;

            _winner = pool[new Random(Guid.NewGuid().GetHashCode()).Next(pool.Count)];
            _shown = 0;
            _tip.Text = "抽取中…";
            _tip.ForeColor = Theme.TextSub;
            ShowFace(pool[new Random(Guid.NewGuid().GetHashCode()).Next(pool.Count)], false);   // 立刻先显示一个人
            _avatar.Pop = 1.0;
            _name.ForeColor = Theme.TextFaint;

            Anim.Run(this, 1750, t =>
            {
                if (IsDisposed) return;
                double target = 30 * Anim.EaseOutCubic(t);      // 前快后慢
                if (target >= _shown + 1)
                {
                    _shown = Math.Floor(target);
                    var s2 = pool[new Random(Guid.NewGuid().GetHashCode()).Next(pool.Count)];
                    ShowFace(s2, false);
                }
            }, () =>
            {
                if (IsDisposed) return;
                ShowFace(_winner, true);
                _tip.Text = "本轮抽中 · 当前 " + _winner.Score + " 分";
                _tip.ForeColor = Theme.TextSub;

                // 中奖者弹出 + 名字上浮回位
                Anim.Run(_avatar, 430, s =>
                {
                    if (_avatar.IsDisposed) return;
                    _avatar.Pop = Anim.Lerp(1.24, 1.0, Anim.EaseOutBack(s));
                    _avatar.Invalidate();
                });
                int baseTop = Theme.P(188);
                Anim.Run(_name, 430, s =>
                {
                    if (_name.IsDisposed) return;
                    _name.Top = baseTop + (int)(Theme.P(10) * (1 - Anim.EaseOutBack(s)));
                });
            });
        }

        private double _shown;

        private void ShowFace(Student s, bool final)
        {
            _avatar.StudentName = s.Name;
            _avatar.Seed = s.Id;
            _avatar.Ring = final ? Theme.ScoreColor(s.Score) : Theme.TextFaint;
            _avatar.Invalidate();

            _name.Text = s.Name;
            _name.ForeColor = final ? Theme.TextMain : Theme.TextFaint;

            // 每次换人给头像一个轻微“弹一下”，避免生硬的逐帧切换
            double from = final ? 1.2 : 0.9;
            Anim.Run(_avatar, final ? 200 : 110, t =>
            {
                if (_avatar.IsDisposed) return;
                _avatar.Pop = Anim.Lerp(from, 1.0, Anim.EaseOutQuad(t));
                _avatar.Invalidate();
            });
        }

        private void Award(int points)
        {
            if (_winner == null) return;
            if (!_app.Session.CanAddScore) { Toast.Show(this, "当前身份不能加分", Theme.Red); return; }
            _app.Score(_winner, points, "随机点名", "");
            _tip.Text = "已为 " + _winner.Name + " 加 " + points + " 分，现在 " + _winner.Score + " 分";
            if (_app.Settings.SoundEnabled) { try { System.Media.SystemSounds.Asterisk.Play(); } catch { } }
        }
    }

    /// <summary>可缩放的头像控件（用于随机点名等动画）。</summary>
    public class AvatarView : Control
    {
        public string StudentName = "";
        public string Seed = "";
        public Color? Ring;
        public double Pop = 1;

        public AvatarView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Enabled = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Card);
            double pop = Pop <= 0 ? 1 : Pop;
            int size = (int)(Math.Min(Width, Height) * pop);
            if (size <= 2) return;
            var r = new Rectangle((Width - size) / 2, (Height - size) / 2, size, size);
            Avatar.Draw(g, r, StudentName, Seed, Ring);
        }
    }

    /// <summary>学生详情：积分、排名与最近记录。</summary>
    public class StudentDetailDialog : DialogBase
    {
        private readonly App _app;
        private readonly Student _stu;
        public bool ScoreChanged;

        public StudentDetailDialog(App app, Student stu, bool canScore)
            : base("学生详情 · " + stu.Name, 640, 540, false)
        {
            _app = app;
            _stu = stu;
            BtnCancel.Text = "关闭";

            var av = new PictureBox { Location = Theme.PP(24, 54), Size = Theme.PS(60, 60), BackColor = Color.Transparent };
            av.Image = Avatar.Render(Theme.P(60), stu.Name, stu.Id, Theme.ScoreColor(stu.Score));
            Body.Controls.Add(av);

            MkLabel(stu.Name, 98, 56, Theme.Subtitle(), Theme.TextMain);
            int rank = app.Rank(stu);
            MkLabel("积分 " + (stu.Score > 0 ? "+" : "") + stu.Score + " 分    班级排名第 " + rank + " 名"
                + (string.IsNullOrEmpty(stu.No) ? "" : "    学号 " + stu.No)
                + (string.IsNullOrEmpty(stu.Gender) ? "" : "    " + stu.Gender), 98, 86, Theme.Caption(), Theme.TextSub);

            if (canScore)
            {
                var b1 = new RoundButton
                {
                    Text = "加分 / 扣分",
                    Font = Theme.BodyStrong(),
                    Size = Theme.PS(126, 36),
                    Fill = Theme.Green,
                    Location = Theme.PP(486, 58)
                };
                b1.Click += (s, e) =>
                {
                    using (var d = new ScoreDialog(_app, stu))
                    {
                        if (d.ShowDialog(this) != DialogResult.OK) return;
                        if (_app.Score(stu, d.Delta, d.Reason, d.NoteText) != null)
                        {
                            ScoreChanged = true;
                            Toast.Show(this, (_stu.Name + " 现在 " + _stu.Score + " 分"), d.Delta > 0 ? Theme.Green : Theme.Red);
                        }
                    }
                };
                Body.Controls.Add(b1);
            }

            var list = new ListView
            {
                Location = Theme.PP(24, 136),
                Size = Theme.PS(592, 300),
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                Font = Theme.Caption(),
                BorderStyle = BorderStyle.FixedSingle,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                BackColor = Theme.Card
            };
            list.Columns.Add("时间", Theme.P(130));
            list.Columns.Add("变动", Theme.P(60));
            list.Columns.Add("理由", Theme.P(160));
            list.Columns.Add("操作人", Theme.P(100));
            list.Columns.Add("备注", Theme.P(120));
            Body.Controls.Add(list);

            var events = new List<ScoreEvent>();
            var all = _app.Store.EventsOf(null);
            foreach (var e in all)
                if (e.StudentId == stu.Id && events.Count < 200) events.Add(e);

            foreach (var e in events)
            {
                var it = new ListViewItem(e.TimeUtc.ToLocalTime().ToString("MM-dd HH:mm"));
                it.SubItems.Add((e.Delta > 0 ? "+" : "") + e.Delta);
                it.SubItems.Add(e.Reason);
                it.SubItems.Add(e.Operator);
                it.SubItems.Add(e.Note);
                it.ForeColor = e.Delta > 0 ? Theme.Green : Theme.Red;
                list.Items.Add(it);
            }
            if (events.Count == 0) MkLabel("暂无积分记录", 24, 444, Theme.Caption(), Theme.TextFaint);
        }
    }
}
