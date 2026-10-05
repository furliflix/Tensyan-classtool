using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tensyan.Core;

namespace Tensyan.Wpf.Views
{
    /// <summary>卡片数据：直接包着 Core 的学生对象，界面只读展示。</summary>
    public class StudentCard
    {
        public Student St;
        public string Name { get { return St.Name + (string.IsNullOrEmpty(St.No) ? "" : "   " + St.No) + (St.Archived ? "（已归档）" : ""); } }
        public string ScoreText { get { return (St.Score > 0 ? "+" : "") + St.Score; } }
        public string Hint { get { return St.Score >= 0 ? "继续加油" : "需要改进"; } }
        public Brush ScoreBrush
        {
            get
            {
                return St.Score >= 0
                    ? new SolidColorBrush(Color.FromRgb(0x00, 0x5F, 0xB8))
                    : new SolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x1C));
            }
        }
    }

    public partial class StudentsView : UserControl
    {
        private readonly ObservableCollection<StudentCard> _cards = new ObservableCollection<StudentCard>();
        private bool _suppress;
        private bool _menuBuilding;   // 防止在 ContextMenuOpening 里重新赋值菜单导致递归

        private static Tensyan.Core.App Core { get { return App.Core; } }
        private FontFamily UiFont { get { return (FontFamily)FindResource("UiFont"); } }

        public StudentsView()
        {
            InitializeComponent();
            CardList.ItemsSource = _cards;
            Loaded += (s, e) => Reload();
        }

        // ---------------- 刷新 ----------------

        public void Reload()
        {
            if (Core == null || Core.Data == null) { ClassName.Text = "数据未加载"; return; }

            _suppress = true;
            var classes = Core.VisibleClasses();
            ClassBox.Items.Clear();
            foreach (var c in classes) ClassBox.Items.Add(c.Name);
            if (Core.Current != null)
            {
                int idx = classes.FindIndex(c => c.Id == Core.Current.Id);
                ClassBox.SelectedIndex = idx >= 0 ? idx : 0;
            }
            else if (classes.Count > 0) ClassBox.SelectedIndex = 0;

            var quick = (Core.Settings.QuickAdd != null && Core.Settings.QuickAdd.Count > 0)
                ? Core.Settings.QuickAdd : new List<int> { 1, 2, 3 };
            PointsBox.Items.Clear();
            foreach (var n in quick) PointsBox.Items.Add(n + " 分");
            if (PointsBox.SelectedIndex < 0) PointsBox.SelectedIndex = 0;
            _suppress = false;

            RefreshCards();
        }

        private void RefreshCards()
        {
            _cards.Clear();
            var cls = Core.Current;
            if (cls == null) { ClassName.Text = "还没有班级"; StatLine.Text = ""; return; }

            ClassName.Text = cls.Name;
            StatLine.Text = string.Format("{0} 名学生 · 总积分 {1} · 身份：{2}",
                cls.Students.Count(s => !s.Archived), cls.TotalScore(), RoleText.Name(Core.Session.Role));

            foreach (var st in cls.Students)
            {
                if (st.Archived && !Core.Session.IsAdmin) continue;
                _cards.Add(new StudentCard { St = st });
            }
        }

        private int CurrentPoints()
        {
            int p = 1;
            var s = PointsBox.SelectedItem as string;
            if (!string.IsNullOrEmpty(s))
            {
                s = s.Replace("分", "").Trim();
                if (!int.TryParse(s, out p) || p <= 0) p = 1;
            }
            return p;
        }

        // ---------------- 加减分 ----------------

        private void ClassBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppress || Core == null) return;
            int i = ClassBox.SelectedIndex;
            var classes = Core.VisibleClasses();
            if (i < 0 || i >= classes.Count) return;
            if (Core.Current != null && classes[i].Id == Core.Current.Id) return;
            Core.SwitchClass(classes[i]);
            RefreshCards();
        }

        private void Plus_Click(object sender, RoutedEventArgs e) { Score(sender, +1, null); }
        private void Minus_Click(object sender, RoutedEventArgs e) { Score(sender, -1, null); }

        private void Score(object sender, int sign, StudentCard direct)
        {
            try
            {
                var card = direct;
                var btn = sender as Button;
                if (card == null && btn != null) card = btn.Tag as StudentCard;
                if (card == null) return;

                int points = CurrentPoints();
                string reason = sign > 0 ? "表现良好" : "需要改进";
                var ev = Core.Score(card.St, sign * points, reason, "");
                if (ev == null) { Status.Text = sign > 0 ? "当前身份不能加分" : "当前身份不能扣分"; return; }

                Status.Text = string.Format("已记录：{0} {1}{2}（{3}）", card.St.Name, sign > 0 ? "+" : "", sign * points, reason);
                if (btn != null) Pulse(btn);
                RefreshCards();
            }
            catch (Exception ex) { Status.Text = "操作失败：" + ex.Message; }
        }

        private void QuickScore(StudentCard card, int delta)
        {
            var ev = Core.Score(card.St, delta, delta > 0 ? "表现良好" : "需要改进", "");
            Status.Text = ev == null ? "当前身份没有这个权限" :
                string.Format("已记录：{0} {1}{2}", card.St.Name, delta > 0 ? "+" : "", delta);
            RefreshCards();
        }

        /// <summary>加分 / 扣分对话框：分值可填负数表示扣分。</summary>
        private void OpenScore(StudentCard card)
        {
            var r = SimpleDialogs.Prompt("加分 / 扣分",
                "给 " + card.St.Name + " 记一次分（分值填负数表示扣分）：",
                new[] { "分值", "理由" },
                new[] { CurrentPoints().ToString(), "表现良好" });
            if (r == null) return;

            int delta;
            if (!int.TryParse(r[0], out delta) || delta == 0) { Status.Text = "分值无效"; return; }
            if (delta > 0 && !Core.Session.CanAddScore) { Status.Text = "当前身份不能加分"; return; }
            if (delta < 0 && !Core.Session.CanSubScore) { Status.Text = "当前身份不能扣分"; return; }

            var ev = Core.Score(card.St, delta, string.IsNullOrEmpty(r[1]) ? "课堂表现" : r[1], "");
            Status.Text = ev == null ? "记录失败" :
                string.Format("已记录：{0} {1}{2}（{3}）", card.St.Name, delta > 0 ? "+" : "", delta, ev.Reason);
            RefreshCards();
        }

        // ---------------- 右键菜单（与 WinForms 版功能一致）----------------

        private void Card_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var border = sender as Border;
            var card = border == null ? null : border.DataContext as StudentCard;
            if (border == null || card == null) return;

            var menu = new ContextMenu { FontFamily = UiFont, FontSize = 13.5 };
            if (Core.Session.CanAddScore)
            {
                menu.Items.Add(MenuItem("加分 / 扣分…", () => OpenScore(card)));
                menu.Items.Add(MenuItem("+1 分", () => QuickScore(card, 1)));
                menu.Items.Add(MenuItem("+2 分", () => QuickScore(card, 2)));
                if (Core.Session.CanSubScore) menu.Items.Add(MenuItem("-1 分", () => QuickScore(card, -1)));
            }
            menu.Items.Add(MenuItem("学生详情", () => StudentInfo(card)));
            if (Core.Session.IsAdmin)
            {
                menu.Items.Add(new Separator());
                menu.Items.Add(MenuItem("编辑资料", () => EditStudent(card)));
                menu.Items.Add(MenuItem(card.St.Archived ? "取消归档" : "归档（隐藏但保留分数）", () => ToggleArchive(card)));
                menu.Items.Add(MenuItem("删除学生", () => DeleteStudent(card)));
            }

            border.ContextMenu = menu;
            menu.PlacementTarget = border;
            menu.IsOpen = true;
            e.Handled = true;
        }

        private static MenuItem MenuItem(string text, Action action)
        {
            var mi = new MenuItem { Header = text };
            mi.Click += (s, e) => { try { action(); } catch (Exception ex) { MessageBox.Show(ex.Message, "Tensyan"); } };
            return mi;
        }

        private void StudentInfo(StudentCard card)
        {
            var st = card.St;
            var lines = new List<string>
            {
                "姓名：" + st.Name,
                "学号 / 座号：" + (string.IsNullOrEmpty(st.No) ? "（未填写）" : st.No),
                "性别：" + (string.IsNullOrEmpty(st.Gender) ? "（未填写）" : st.Gender),
                "当前积分：" + st.Score,
                "备注：" + (string.IsNullOrEmpty(st.Note) ? "（无）" : st.Note),
                "状态：" + (st.Archived ? "已归档（不参与展示）" : "在册"),
                ""
            };

            var history = Core.Data.Events
                .Where(v => v.StudentId == st.Id && !v.Undone)
                .OrderByDescending(v => v.TimeUtc)
                .Take(8).ToList();
            lines.Add(history.Count == 0 ? "最近记录：（暂无）" : "最近记录：");
            foreach (var v in history)
            {
                lines.Add(string.Format("　{0:MM-dd HH:mm}　{1}{2}　{3}",
                    v.TimeUtc.ToLocalTime(), v.Delta > 0 ? "+" : "", v.Delta, v.Reason));
            }
            SimpleDialogs.Info("学生详情 · " + st.Name, lines.ToArray());
        }

        private void EditStudent(StudentCard card)
        {
            var st = card.St;
            var r = SimpleDialogs.Prompt("编辑资料", "修改 " + st.Name + " 的资料：",
                new[] { "姓名", "学号 / 座号", "性别（男 / 女 / 留空）", "备注" },
                new[] { st.Name, st.No, st.Gender, st.Note });
            if (r == null) return;
            if (string.IsNullOrEmpty(r[0])) { Status.Text = "姓名不能为空"; return; }
            st.Name = r[0];
            st.No = r[1];
            st.Gender = r[2];
            st.Note = r[3];
            Core.Raise();
            Status.Text = "已更新：" + st.Name;
            RefreshCards();
        }

        private void ToggleArchive(StudentCard card)
        {
            card.St.Archived = !card.St.Archived;
            Core.Raise();
            Status.Text = card.St.Name + (card.St.Archived ? " 已归档（分数保留）" : " 已取消归档");
            RefreshCards();
        }

        private void DeleteStudent(StudentCard card)
        {
            var st = card.St;
            if (!SimpleDialogs.Confirm("删除学生",
                "确定删除学生 " + st.Name + " 吗？\n其积分记录仍会保留在历史中。", true)) return;
            Core.Current.Students.Remove(st);
            Core.Raise();
            Status.Text = "已删除：" + st.Name;
            RefreshCards();
        }

        // ---------------- 其它 ----------------

        /// <summary>从按钮往上找卡片外框，做一个 1 → 1.045 → 1 的缩放脉冲（合成线程动画）。</summary>
        private void Pulse(DependencyObject from)
        {
            try
            {
                DependencyObject node = from;
                while (node != null && !(node is Border)) node = VisualTreeHelper.GetParent(node);
                var border = node as Border;
                if (border == null) return;

                var st = border.RenderTransform as ScaleTransform;
                if (st == null) { st = new ScaleTransform(1, 1); border.RenderTransform = st; }
                var sb = new System.Windows.Media.Animation.Storyboard();
                var anim = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames();
                anim.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(1.0, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.Zero)));
                anim.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(1.045, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(90))));
                anim.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(1.0, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240))));
                System.Windows.Media.Animation.Storyboard.SetTarget(anim, st);
                System.Windows.Media.Animation.Storyboard.SetTargetProperty(anim, new PropertyPath(ScaleTransform.ScaleXProperty));
                var anim2 = anim.Clone();
                System.Windows.Media.Animation.Storyboard.SetTargetProperty(anim2, new PropertyPath(ScaleTransform.ScaleYProperty));
                sb.Children.Add(anim);
                sb.Children.Add(anim2);
                sb.Begin();
            }
            catch { }
        }

        private void AddStudent_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Core.Current == null) { Status.Text = "请先新建班级"; return; }
                string name = (NewNameBox.Text ?? "").Trim();
                if (name.Length == 0) { Status.Text = "请在左侧输入学生姓名"; NewNameBox.Focus(); return; }

                var st = new Student { Name = name, No = (Core.Current.Students.Count + 1).ToString("00") };
                Core.Current.Students.Add(st);
                Core.Raise();
                NewNameBox.Text = "";
                Status.Text = "已添加：" + st.Name;
                RefreshCards();
            }
            catch (Exception ex) { Status.Text = "添加失败：" + ex.Message; }
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string msg;
                if (Core.Undo(out msg)) { Status.Text = msg; RefreshCards(); }
                else Status.Text = msg;
            }
            catch (Exception ex) { Status.Text = "撤销失败：" + ex.Message; }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Core.Current == null) { Status.Text = "请先新建班级"; return; }
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Tensyan");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, Exporter.DefaultFileName("积分表"));
                string msg;
                if (Exporter.ExportClassScores(Core.Data, Core.Current, path, out msg))
                {
                    Status.Text = "已导出：" + path;
                    SimpleDialogs.Info("导出成功", new[] { "已导出到：", path });
                }
                else Status.Text = "导出失败：" + msg;
            }
            catch (Exception ex) { Status.Text = "导出异常：" + ex.Message; }
        }
    }
}
