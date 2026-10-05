using System;
using System.Collections.Generic;
using System.Drawing;
using System.Media;
using System.Windows.Forms;
using Tensyan.Core;

namespace Tensyan.UI.Pages
{
    /// <summary>学生积分页：卡片墙 + 快速加减分 + 批量操作。</summary>
    public class PageStudents : PageBase
    {
        private FlowLayoutPanel _flow;
        private SearchBox _search;
        private FluentSelect _sort;
        private RoundButton _btnBatch, _btnAdd, _btnAllAdd;
        private Panel _batchBar;
        private Label _batchInfo;
        private RoundButton _batchAdd, _batchSub, _batchClear;
        private readonly List<StudentCard> _cards = new List<StudentCard>();

        public PageStudents(App app) : base(app)
        {
            TitleText = "学生积分";
            EnableTools(52);
            BuildTools();
            BuildBody();
            BuildBatchBar();
        }

        private void BuildTools()
        {
            _search = new SearchBox { Placeholder = "搜索学生姓名", Location = Theme.PP(0, 9), Size = Theme.PS(240, 34) };
            _search.TextChanged2 += (s, e) => Reload();
            Tools.Controls.Add(_search);

            _sort = new FluentSelect { Location = Theme.PP(252, 9), Size = Theme.PS(160, 34) };
            _sort.AddItems("默认顺序", "积分从高到低", "积分从低到高", "按姓名");
            _sort.SelectedIndex = 0;
            _sort.SelectedIndexChanged += (s, e) => Reload();
            Tools.Controls.Add(_sort);

            _btnBatch = MkBtn("批量选择", IconKind.Check, 108, null, true, Theme.TextSub);
            _btnBatch.Location = Theme.PP(424, 9);
            _btnBatch.Click += (s, e) => ToggleBatch();
            _btnBatch.Visible = App.Session.CanAddScore;
            Tools.Controls.Add(_btnBatch);

            if (App.Session.IsAdmin)
            {
                _btnAllAdd = MkBtn("全班加分", IconKind.Add, 104, null, true, Theme.Green);
                _btnAllAdd.Location = Theme.PP(542, 9);
                _btnAllAdd.Click += (s, e) => WholeClassScore();
                Tools.Controls.Add(_btnAllAdd);

                _btnAdd = MkBtn("添加学生", IconKind.Add, 104, Theme.Primary, false);
                _btnAdd.Click += (s, e) => AddStudent();
                Tools.Controls.Add(_btnAdd);
            }
            else
            {
                // 成员也保留“全班加分”（本质还是加分）；管理入口改为点击说明原因，而不是直接消失
                if (App.Session.CanAddScore)
                {
                    _btnAllAdd = MkBtn("全班加分", IconKind.Add, 104, null, true, Theme.Green);
                    _btnAllAdd.Click += (s, e) => WholeClassScore();
                    Tools.Controls.Add(_btnAllAdd);
                }
                _btnAdd = MkBtn("添加学生", IconKind.Lock, 104, null, true, Theme.TextSub);
                _btnAdd.Click += (s, e) => ShowManageHint();
                Tools.Controls.Add(_btnAdd);
            }
            LayoutTools();
        }

        /// <summary>非管理员点管理类按钮时，明确说明为什么不能操作。</summary>
        private void ShowManageHint()
        {
            string msg = App.Session.IsGuest
                ? "访客只读：不能添加或修改学生，请用管理员账号登录。"
                : "当前是成员身份（仅加分）：添加 / 编辑 / 删除学生需要用管理员账号登录。";
            Toast.Show(this, msg, Theme.Amber);
            SetSub((App.Current == null ? "" : App.Current.Name + "  ·  ") + "当前身份：" + RoleText.Name(App.Session.Role)
                + "  ·  " + msg);
        }

        private void LayoutTools()
        {
            int right = Tools.Width;
            if (_btnAdd != null) { right -= _btnAdd.Width; _btnAdd.Location = new Point(right, Theme.P(9)); right -= Theme.P(12); }
            if (_btnAllAdd != null) { right -= _btnAllAdd.Width; _btnAllAdd.Location = new Point(right, Theme.P(9)); }
        }

        private void BuildBody()
        {
            _flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Theme.Bg,
                Padding = new Padding(0, 0, Theme.P(18), 0),
                WrapContents = true
            };
            Body.Controls.Add(_flow);
        }

        private void BuildBatchBar()
        {
            _batchBar = new Panel { BackColor = Theme.Card, Height = Theme.P(56), Visible = false };
            _batchBar.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
                var r = new Rectangle(0, 0, _batchBar.Width - 1, _batchBar.Height - 1);
                Theme.CardSurface(g, r, Theme.P(8), true);
            };
            Controls.Add(_batchBar);

            _batchInfo = new Label
            {
                Font = Theme.BodyStrong(),
                ForeColor = Theme.TextMain,
                AutoSize = false,
                BackColor = Color.Transparent,
                Size = Theme.PS(200, 28),
                Location = Theme.PP(18, 14)
            };
            _batchBar.Controls.Add(_batchInfo);

            _batchAdd = new RoundButton { Text = "批量加分", Size = Theme.PS(104, 34), Fill = Theme.Green };
            _batchAdd.Click += (s, e) => BatchScore(true);
            _batchBar.Controls.Add(_batchAdd);

            _batchSub = new RoundButton { Text = "批量扣分", Size = Theme.PS(104, 34), Fill = Theme.Red };
            _batchSub.Click += (s, e) => BatchScore(false);
            _batchBar.Controls.Add(_batchSub);

            _batchClear = new RoundButton
            {
                Text = "取消选择",
                Size = Theme.PS(96, 34),
                Soft = true,
                Fill = Theme.Card,
                OutlineColor = Theme.BorderStrong
            };
            _batchClear.Click += (s, e) => ToggleBatch();
            _batchBar.Controls.Add(_batchClear);
        }

        protected override void Layout3()
        {
            base.Layout3();
            LayoutTools();
            if (_batchBar != null)
            {
                int w = Math.Min(Theme.P(660), Math.Max(Theme.P(380), ClientSize.Width - Theme.P(48)));
                _batchBar.SetBounds(Theme.P(24), Math.Max(0, ClientSize.Height - Theme.P(76)), w, Theme.P(56));
                _batchClear.Location = new Point(w - _batchClear.Width - Theme.P(16), Theme.P(11));
                _batchSub.Location = new Point(_batchClear.Left - _batchSub.Width - Theme.P(10), Theme.P(11));
                _batchAdd.Location = new Point(_batchSub.Left - _batchAdd.Width - Theme.P(10), Theme.P(11));
                _batchBar.BringToFront();
            }
        }

        private bool BatchOn { get { return _batchBar.Visible; } }

        private void ToggleBatch()
        {
            bool on = !_batchBar.Visible;
            _batchBar.Visible = on;
            _batchBar.BringToFront();
            foreach (var c in _cards) { c.BatchMode = on; c.Selected = false; c.Invalidate(); }
            _btnBatch.Text = on ? "退出批量" : "批量选择";
            _btnBatch.Invalidate();
            UpdateBatchInfo();
        }

        private void UpdateBatchInfo()
        {
            int n = 0;
            foreach (var c in _cards) if (c.Selected) n++;
            _batchInfo.Text = "已选择 " + n + " 人";
        }

        public override void Reload()
        {
            if (App.Current == null) { _flow.Controls.Clear(); _cards.Clear(); SetSub("尚未创建班级"); return; }

            string q = _search == null ? "" : _search.Value.Trim();
            var list = new List<Student>();
            foreach (var s in App.Current.Students)
            {
                if (s.Archived && App.Session.IsGuest) continue;
                if (q.Length > 0 && s.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
                list.Add(s);
            }

            int sortIdx = _sort == null ? 0 : _sort.SelectedIndex;
            if (sortIdx == 1) list.Sort((a, b) => b.Score.CompareTo(a.Score));
            else if (sortIdx == 2) list.Sort((a, b) => a.Score.CompareTo(b.Score));
            else if (sortIdx == 3) list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

            _flow.SuspendLayout();
            _flow.Controls.Clear();
            _cards.Clear();

            if (list.Count == 0)
            {
                string emptyText;
                if (App.Current.Students.Count > 0) emptyText = "没有找到匹配的学生\r\n（清空右上角搜索框，或切换排序再看）";
                else if (App.Session.IsAdmin) emptyText = "这个班级还没有学生。\r\n点右上角“添加学生”，或到“系统设置 → 班级管理”批量导入名单。";
                else emptyText = "这个班级还没有学生。\r\n当前身份是" + RoleText.Name(App.Session.Role) + "，添加 / 导入学生需要管理员账号登录。";

                var empty = new Label
                {
                    Text = emptyText,
                    Font = Theme.Body(),
                    ForeColor = Theme.TextFaint,
                    AutoSize = false,
                    Size = new Size(Theme.P(560), Theme.P(90)),
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent
                };
                _flow.Controls.Add(empty);
            }

            foreach (var st in list)
            {
                var card = new StudentCard
                {
                    Student = st,
                    App = App,
                    RankNo = App.Rank(st),
                    BatchMode = BatchOn,
                    Margin = new Padding(0, 0, Theme.P(14), Theme.P(14))
                };
                card.ScoreAsked += (s, e) => QuickScore(card, e.Delta);
                card.OpenAsked += (s, e) => OpenScore(card);
                card.ContextAsked += (s, e) => ShowMenu(card);
                card.Click += (s, e) => { if (BatchOn) UpdateBatchInfo(); };
                _cards.Add(card);
                _flow.Controls.Add(card);
            }
            _flow.ResumeLayout();

            int active = 0, total = 0;
            foreach (var s in App.Current.Students) if (!s.Archived) { active++; total += s.Score; }
            string roleNote = App.Session.IsGuest
                ? "  ·  访客只读：不能加分，也不能添加 / 修改学生"
                : (!App.Session.CanSubScore ? "  ·  成员仅加分：不能扣分，添加 / 修改学生需要管理员账号" : "");
            SetSub(App.Current.Name + "  ·  " + active + " 名学生  ·  班级总积分 " + total + " 分" + roleNote);
            UpdateBatchInfo();
        }

        private void QuickScore(StudentCard card, int delta)
        {
            var st = card.Student;
            string reason = delta > 0 ? "课堂表现" : "需要改进";
            var ev = App.Score(st, delta, reason, "");
            if (ev == null) { Toast.Show(this, "当前身份不能执行该操作", Theme.Red); return; }

            card.RankNo = App.Rank(st);
            card.Pulse();
            card.Invalidate();

            if (App.Settings.AnimationEnabled)
                Toast.Fly(card, (delta > 0 ? "+" : "") + delta, delta > 0 ? Theme.Green : Theme.Red, new Point(card.Width / 2, Theme.P(60)));
            if (App.Settings.SoundEnabled) PlaySound(delta > 0);
            SetSub(App.Current.Name + "  ·  班级总积分 " + App.Current.TotalScore() + " 分");
        }

        private static void PlaySound(bool good)
        {
            try
            {
                if (good) SystemSounds.Asterisk.Play();
                else SystemSounds.Hand.Play();
            }
            catch { }
        }

        private void OpenScore(StudentCard card)
        {
            if (!App.Session.CanAddScore) { StudentInfo(card.Student); return; }
            using (var d = new ScoreDialog(App, card.Student))
            {
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    var ev = App.Score(card.Student, d.Delta, d.Reason, d.NoteText);
                    if (ev != null)
                    {
                        card.RankNo = App.Rank(card.Student);
                        card.Pulse();
                        if (App.Settings.AnimationEnabled)
                            Toast.Fly(card, (d.Delta > 0 ? "+" : "") + d.Delta, d.Delta > 0 ? Theme.Green : Theme.Red, new Point(card.Width / 2, Theme.P(60)));
                        if (App.Settings.SoundEnabled) PlaySound(d.Delta > 0);
                        card.Invalidate();
                        SetSub(App.Current.Name + "  ·  班级总积分 " + App.Current.TotalScore() + " 分");
                    }
                }
            }
        }

        private void WholeClassScore()
        {
            if (App.Current == null || App.Current.Students.Count == 0) return;
            using (var d = new BatchScoreDialog(App, "全班加分", true))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                int n = 0;
                foreach (var s in App.Current.Students)
                {
                    if (s.Archived) continue;
                    if (App.Score(s, d.Delta, d.Reason, "全班操作") != null) n++;
                }
                Toast.Show(this, "已为 " + n + " 名学生" + (d.Delta > 0 ? "加 " : "扣 ") + Math.Abs(d.Delta) + " 分", d.Delta > 0 ? Theme.Green : Theme.Red);
                Reload();
            }
        }

        private void BatchScore(bool add)
        {
            var chosen = new List<Student>();
            foreach (var c in _cards) if (c.Selected && c.Student != null) chosen.Add(c.Student);
            if (chosen.Count == 0) { Toast.Show(this, "请先点选要操作的学生", Theme.Red); return; }
            if (!add && !App.Session.CanSubScore) { Toast.Show(this, "当前身份不能扣分", Theme.Red); return; }

            using (var d = new BatchScoreDialog(App, (add ? "批量加分 · " : "批量扣分 · ") + chosen.Count + " 人", add))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                foreach (var s in chosen) App.Score(s, d.Delta, d.Reason, "批量操作");
                Toast.Show(this, "已" + (d.Delta > 0 ? "加 " : "扣 ") + Math.Abs(d.Delta) + " 分：" + chosen.Count + " 人", d.Delta > 0 ? Theme.Green : Theme.Red);
                ToggleBatch();
                Reload();
            }
        }

        private void AddStudent()
        {
            if (!App.Session.IsAdmin) { ShowManageHint(); return; }
            if (App.Current == null)
            {
                Toast.Show(this, "还没有班级：请先到“系统设置 → 班级管理”新建班级", Theme.Red);
                return;
            }

            var s = new Student();
            using (var d = new StudentEditDialog(s, true))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                s.Name = d.StudentName;
                s.Gender = d.Gender;
                s.No = d.No;
                s.Note = d.NoteText;
                s.Score = d.InitialScore;
                App.Current.Students.Add(s);
                App.Raise();

                // 清掉搜索/排序干扰，保证新学生立刻可见
                if (_search != null && _search.Value.Trim().Length > 0) _search.Value = "";
                if (_sort != null && _sort.SelectedIndex != 0 && _sort.SelectedIndex != 1 && _sort.SelectedIndex != 3) _sort.SelectedIndex = 0;
                Reload();
                Toast.Show(this, "已添加学生：" + s.Name + "（共 " + App.Current.Students.Count + " 人）", Theme.Green);
            }
        }

        /// <summary>开发自检：当前身份是否能添加学生。</summary>
        public bool DebugCanAddStudent { get { return App.Session.IsAdmin && App.Current != null; } }

        /// <summary>开发自检：卡片数量。</summary>
        public int DebugCardCount { get { return _cards.Count; } }

        /// <summary>开发自检：跳过对话框直接添加一名学生，返回结果说明。</summary>
        public string DebugAddStudent(string name)
        {
            if (!App.Session.IsAdmin) return "拒绝：当前身份 " + RoleText.Name(App.Session.Role) + " 不能添加学生";
            if (App.Current == null) return "拒绝：还没有班级";
            App.Current.Students.Add(new Student { Name = name });
            App.Raise();
            Reload();
            return "已添加 " + name + "，班级人数 " + App.Current.Students.Count + "，卡片 " + _cards.Count;
        }

        private void ShowMenu(StudentCard card)
        {
            var st = card.Student;
            var menu = new ContextMenuStrip { Font = Theme.Body(), Renderer = new FluentMenuRenderer(), ShowImageMargin = false };
            if (App.Session.CanAddScore)
            {
                menu.Items.Add("加分 / 扣分…", null, (s, e) => OpenScore(card));
                menu.Items.Add("+1 分", null, (s, e) => QuickScore(card, 1));
                menu.Items.Add("+2 分", null, (s, e) => QuickScore(card, 2));
                if (App.Session.CanSubScore)
                    menu.Items.Add("-1 分", null, (s, e) => QuickScore(card, -1));
            }
            menu.Items.Add("学生详情", null, (s, e) => StudentInfo(st));
            if (App.Session.IsAdmin)
            {
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add("编辑资料", null, (s, e) =>
                {
                    using (var d = new StudentEditDialog(st, false))
                    {
                        if (d.ShowDialog(this) != DialogResult.OK) return;
                        st.Name = d.StudentName; st.Gender = d.Gender; st.No = d.No; st.Note = d.NoteText;
                        App.Raise();
                        Reload();
                    }
                });
                menu.Items.Add(st.Archived ? "取消归档" : "归档（隐藏但保留分数）", null, (s, e) =>
                {
                    st.Archived = !st.Archived;
                    App.Raise();
                    Reload();
                });
                menu.Items.Add("删除学生", null, (s, e) =>
                {
                    if (MessageBox.Show(this, "确定删除学生 " + st.Name + " 吗？其积分记录会保留在历史中。", "Tensyan",
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
                    App.Current.Students.Remove(st);
                    App.Raise();
                    Reload();
                });
            }
            menu.Show(Cursor.Position);
        }

        private void StudentInfo(Student st)
        {
            using (var d = new StudentDetailDialog(App, st, App.Session.CanAddScore))
            {
                d.ShowDialog(this);
                if (d.ScoreChanged) Reload();
            }
        }
    }
}
