using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tensyan.Core;

namespace Tensyan.Wpf.Views
{
    // ============================================================
    //  积分排行
    // ============================================================
    public class RankView : UserControl
    {
        private static Tensyan.Core.App Core { get { return App.Core; } }
        private readonly StackPanel _body = new StackPanel();
        private ComboBox _classBox;

        public RankView()
        {
            Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _body };
            Loaded += (s, e) => Reload();
        }

        public void Reload()
        {
            _body.Children.Clear();
            _body.Children.Add(UiKit.Title("积分排行"));
            _body.Children.Add(UiKit.Sub("按总分从高到低。前三名用奖牌标出；归档学生不参与展示。"));

            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            _classBox = new ComboBox { Width = 200, Height = 34, FontSize = 13.5, VerticalContentAlignment = VerticalAlignment.Center };
            var classes = Core.VisibleClasses();
            foreach (var c in classes) _classBox.Items.Add(c.Name);
            _classBox.SelectedIndex = Core.Current != null ? Math.Max(0, classes.FindIndex(c => c.Id == Core.Current.Id)) : 0;
            _classBox.SelectionChanged += (s, e) =>
            {
                int i = _classBox.SelectedIndex;
                if (i >= 0 && i < classes.Count) { Core.SwitchClass(classes[i]); Reload(); }
            };
            bar.Children.Add(_classBox);
            bar.Children.Add(UiKit.Subtle("导出 Excel", Export, 0xF0281));
            _body.Children.Add(bar);

            var cls = Core.Current;
            if (cls == null) { _body.Children.Add(UiKit.Faint("还没有班级")); return; }

            var list = cls.Students.Where(s => !s.Archived).OrderByDescending(s => s.Score).ThenBy(s => s.Name).ToList();
            var rows = new List<string[]>();
            for (int i = 0; i < list.Count; i++)
            {
                string medal = i == 0 ? "① " : i == 1 ? "② " : i == 2 ? "③ " : "";
                rows.Add(new[]
                {
                    medal + (i + 1).ToString(),
                    list[i].Name,
                    string.IsNullOrEmpty(list[i].No) ? "-" : list[i].No,
                    (list[i].Score > 0 ? "+" : "") + list[i].Score,
                    Core.Data.Events.Count(v => v.StudentId == list[i].Id && !v.Undone).ToString()
                });
            }
            _body.Children.Add(new Border
            {
                Background = UiKit.Res("Card"),
                BorderBrush = UiKit.Res("Border"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(4, 6, 4, 6),
                Child = UiKit.Table(new[] { "名次", "姓名", "学号", "积分", "记录数" }, rows, new[] { 0.7, 2.0, 1.2, 0.9, 0.9 })
            });
        }

        private void Export()
        {
            string msg;
            string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Tensyan");
            System.IO.Directory.CreateDirectory(dir);
            string path = System.IO.Path.Combine(dir, "积分排行.csv");
            var cls = Core.Current;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("名次,姓名,学号,积分");
            int n = 0;
            foreach (var s in cls.Students.Where(x => !x.Archived).OrderByDescending(x => x.Score))
                sb.AppendLine((++n) + "," + s.Name + "," + s.No + "," + s.Score);
            try
            {
                System.IO.File.WriteAllText(path, sb.ToString(), new System.Text.UTF8Encoding(true));
                SimpleDialogs.Info("导出成功", new[] { "已导出排行榜（CSV，Excel 可直接打开）：", path });
            }
            catch (Exception ex) { msg = ex.Message; SimpleDialogs.Info("导出失败", new[] { msg }); }
        }
    }

    // ============================================================
    //  加分记录
    // ============================================================
    public class HistoryView : UserControl
    {
        private static Tensyan.Core.App Core { get { return App.Core; } }
        private readonly StackPanel _body = new StackPanel();
        private int _filter;   // 0 全部 1 仅加分 2 仅扣分

        public HistoryView()
        {
            Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _body };
            Loaded += (s, e) => Reload();
        }

        public void Reload()
        {
            _body.Children.Clear();
            _body.Children.Add(UiKit.Title("加分记录"));
            _body.Children.Add(UiKit.Sub("所有加减分事件，按时间倒序。撤销会回滚上一次操作并把分数重算。"));

            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            var bAll = UiKit.Subtle("全部", () => { _filter = 0; Reload(); });
            var bAdd = UiKit.Subtle("仅加分", () => { _filter = 1; Reload(); });
            var bSub = UiKit.Subtle("仅扣分", () => { _filter = 2; Reload(); });
            if (_filter == 0) bAll.FontWeight = FontWeights.Bold;
            if (_filter == 1) bAdd.FontWeight = FontWeights.Bold;
            if (_filter == 2) bSub.FontWeight = FontWeights.Bold;
            bar.Children.Add(bAll); bar.Children.Add(bAdd); bar.Children.Add(bSub);
            if (Core.Session.IsAdmin)
                bar.Children.Add(UiKit.Primary("撤销上一次", UndoAll, 0xF19A));
            _body.Children.Add(bar);

            IEnumerable<ScoreEvent> src = Core.Data.Events;
            if (Core.Current != null) src = src.Where(v => v.ClassId == Core.Current.Id);
            if (_filter == 1) src = src.Where(v => v.Delta > 0);
            if (_filter == 2) src = src.Where(v => v.Delta < 0);
            var list = src.OrderByDescending(v => v.TimeUtc).Take(400).ToList();

            var rows = new List<string[]>();
            foreach (var v in list)
            {
                rows.Add(new[]
                {
                    v.TimeUtc.ToLocalTime().ToString("MM-dd HH:mm"),
                    v.StudentName,
                    (v.Delta > 0 ? "+" : "") + v.Delta,
                    v.Reason,
                    string.IsNullOrEmpty(v.Operator) ? "-" : v.Operator,
                    v.Undone ? "已撤销" : ""
                });
            }
            _body.Children.Add(new Border
            {
                Background = UiKit.Res("Card"),
                BorderBrush = UiKit.Res("Border"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(4, 6, 4, 6),
                Child = UiKit.Table(new[] { "时间", "学生", "变动", "理由", "操作人", "状态" }, rows, new[] { 1.2, 1.2, 0.7, 2.2, 1.0, 0.8 })
            });
            _body.Children.Add(UiKit.Faint("共 " + list.Count + " 条（最多显示 400 条）"));
        }

        private void UndoAll()
        {
            string msg;
            if (Core.Undo(out msg)) { SimpleDialogs.Info("撤销", new[] { msg }); Reload(); }
            else SimpleDialogs.Info("撤销", new[] { msg });
        }
    }

    // ============================================================
    //  数据统计
    // ============================================================
    public class StatsView : UserControl
    {
        private static Tensyan.Core.App Core { get { return App.Core; } }
        private readonly StackPanel _body = new StackPanel();

        public StatsView()
        {
            Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _body };
            Loaded += (s, e) => Reload();
        }

        public void Reload()
        {
            _body.Children.Clear();
            _body.Children.Add(UiKit.Title("数据统计"));
            _body.Children.Add(UiKit.Sub("班级整体情况、积分分布与加减分理由排行。"));

            var cls = Core.Current;
            if (cls == null) { _body.Children.Add(UiKit.Faint("还没有班级")); return; }

            var live = cls.Students.Where(s => !s.Archived).ToList();
            var events = Core.Store.EventsOf(cls.Id).Where(v => !v.Undone).ToList();
            int addCount = events.Count(v => v.Delta > 0);
            int subCount = events.Count(v => v.Delta < 0);

            var stats = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
            stats.Children.Add(UiKit.Stat(live.Count.ToString(), "在册学生"));
            stats.Children.Add(UiKit.Stat(cls.TotalScore().ToString(), "总积分"));
            stats.Children.Add(UiKit.Stat(live.Count == 0 ? "0" : Math.Round((double)cls.TotalScore() / live.Count, 1).ToString(), "人均积分"));
            stats.Children.Add(UiKit.Stat(addCount.ToString(), "加分次数", UiKit.Res("Green")));
            stats.Children.Add(UiKit.Stat(subCount.ToString(), "扣分次数", UiKit.Res("Red")));
            _body.Children.Add(stats);

            // ---- 积分分布（每 10 分一档，用矩形画条形图）----
            _body.Children.Add(new TextBlock { Text = "积分分布（每 10 分一档）", FontFamily = UiKit.UiFont, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = UiKit.Res("TextMain"), Margin = new Thickness(0, 8, 0, 8) });
            var buckets = new List<int>();
            var labels = new List<string>();
            int min = live.Count == 0 ? 0 : live.Min(s => s.Score);
            int max = live.Count == 0 ? 0 : live.Max(s => s.Score);
            int start = (int)Math.Floor(min / 10.0) * 10;
            int end = (int)Math.Ceiling(max / 10.0) * 10;
            for (int b = start; b <= end; b += 10)
            {
                labels.Add(b + "~" + (b + 9));
                buckets.Add(live.Count(s => s.Score >= b && s.Score <= b + 9));
            }
            int peak = buckets.Count == 0 ? 1 : Math.Max(1, buckets.Max());
            var chart = new StackPanel { Orientation = Orientation.Horizontal, Height = 150, Margin = new Thickness(0, 0, 0, 14) };
            for (int i = 0; i < buckets.Count; i++)
            {
                var col = new StackPanel { Width = 66, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Bottom };
                var barHeight = buckets[i] == 0 ? 2 : Math.Max(6, 110.0 * buckets[i] / peak);
                col.Children.Add(new Border
                {
                    Height = barHeight,
                    CornerRadius = new CornerRadius(4),
                    Background = UiKit.Res("Accent"),
                    VerticalAlignment = VerticalAlignment.Bottom,
                    ToolTip = labels[i] + " 分：" + buckets[i] + " 人"
                });
                col.Children.Add(new TextBlock { Text = buckets[i].ToString(), FontFamily = UiKit.UiFont, FontSize = 12, Foreground = UiKit.Res("TextSub"), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) });
                col.Children.Add(new TextBlock { Text = labels[i], FontFamily = UiKit.UiFont, FontSize = 10.5, Foreground = UiKit.Res("TextFaint"), HorizontalAlignment = HorizontalAlignment.Center });
                chart.Children.Add(col);
            }
            _body.Children.Add(UiKit.Card(chart));

            // ---- 理由排行 ----
            _body.Children.Add(new TextBlock { Text = "加减分理由排行", FontFamily = UiKit.UiFont, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = UiKit.Res("TextMain"), Margin = new Thickness(0, 4, 0, 8) });
            var byReason = events.GroupBy(v => string.IsNullOrEmpty(v.Reason) ? "（未填理由）" : v.Reason)
                                 .Select(g => new { Reason = g.Key, Count = g.Count(), Sum = g.Sum(v => v.Delta) })
                                 .OrderByDescending(g => g.Count).Take(12).ToList();
            var rows = byReason.Select(g => new[] { g.Reason, g.Count.ToString(), (g.Sum > 0 ? "+" : "") + g.Sum }).ToList();
            _body.Children.Add(new Border
            {
                Background = UiKit.Res("Card"),
                BorderBrush = UiKit.Res("Border"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(4, 6, 4, 6),
                Child = UiKit.Table(new[] { "理由", "次数", "净积分" }, rows, new[] { 3.0, 1.0, 1.0 })
            });
        }
    }
}
