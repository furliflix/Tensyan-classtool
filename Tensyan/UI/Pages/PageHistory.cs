using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Tensyan.Core;

namespace Tensyan.UI.Pages
{
    /// <summary>加分记录：按时间 / 类型 / 学生筛选，并可导出 Excel。</summary>
    public class PageHistory : PageBase
    {
        private FluentSelect _range, _type;
        private SearchBox _search;
        private RoundButton _export, _undo;
        private ListView _list;
        private Label _summary;

        public PageHistory(App app) : base(app)
        {
            TitleText = "加分记录";
            EnableTools(52);

            _range = new FluentSelect { Location = Theme.PP(0, 9), Size = Theme.PS(140, 34) };
            _range.AddItems("今天", "最近 7 天", "最近 30 天", "全部记录");
            _range.SelectedIndex = 1;
            _range.SelectedIndexChanged += (s, e) => Reload();
            Tools.Controls.Add(_range);

            _type = new FluentSelect { Location = Theme.PP(150, 9), Size = Theme.PS(130, 34) };
            _type.AddItems("全部类型", "只看加分", "只看扣分");
            _type.SelectedIndex = 0;
            _type.SelectedIndexChanged += (s, e) => Reload();
            Tools.Controls.Add(_type);

            _search = new SearchBox { Placeholder = "搜索学生", Location = Theme.PP(290, 9), Size = Theme.PS(180, 34) };
            _search.TextChanged2 += (s, e) => Reload();
            Tools.Controls.Add(_search);

            _export = MkBtn("导出 Excel", IconKind.Export, 116, null, true, Theme.Green);
            _export.Click += (s, e) => ExportNow();
            Tools.Controls.Add(_export);

            if (App.Session.IsAdmin)
            {
                _undo = MkBtn("撤销上一步", IconKind.Undo, 116, null, true, Theme.TextSub);
                _undo.Click += (s, e) =>
                {
                    string msg;
                    if (App.Undo(out msg)) { Toast.Show(this, msg, Theme.Primary); Reload(); }
                    else Toast.Show(this, msg, Theme.Red);
                };
                Tools.Controls.Add(_undo);
            }

            _list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                Font = Theme.Caption(),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Theme.Card,
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };
            _list.Columns.Add("时间", Theme.P(140));
            _list.Columns.Add("班级", Theme.P(110));
            _list.Columns.Add("学生", Theme.P(100));
            _list.Columns.Add("变动", Theme.P(70));
            _list.Columns.Add("理由", Theme.P(150));
            _list.Columns.Add("操作人", Theme.P(100));
            _list.Columns.Add("身份", Theme.P(80));
            _list.Columns.Add("备注", Theme.P(160));
            Body.Controls.Add(_list);

            _summary = new Label
            {
                Dock = DockStyle.Bottom,
                Height = Theme.P(28),
                Font = Theme.Caption(),
                ForeColor = Theme.TextSub,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            Body.Controls.Add(_summary);
        }

        protected override void Layout3()
        {
            base.Layout3();
            int right = Tools.Width;
            if (_undo != null) { right -= _undo.Width; _undo.Location = new Point(right, Theme.P(9)); right -= Theme.P(10); }
            if (_export != null) { right -= _export.Width; _export.Location = new Point(right, Theme.P(9)); }
        }

        private DateTime? FromTime()
        {
            switch (_range.SelectedIndex)
            {
                case 0: return DateTime.Today;
                case 1: return DateTime.Today.AddDays(-6);
                case 2: return DateTime.Today.AddDays(-29);
                default: return null;
            }
        }

        private List<ScoreEvent> Filtered()
        {
            var res = new List<ScoreEvent>();
            DateTime? from = FromTime();
            string q = _search.Value.Trim();
            var all = App.Store.EventsOf(App.Current == null ? null : App.Current.Id);
            foreach (var e in all)
            {
                if (from.HasValue && e.TimeUtc.ToLocalTime() < from.Value) continue;
                if (_type.SelectedIndex == 1 && e.Delta <= 0) continue;
                if (_type.SelectedIndex == 2 && e.Delta >= 0) continue;
                if (q.Length > 0 && e.StudentName.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
                res.Add(e);
            }
            return res;
        }

        public override void Reload()
        {
            var list = Filtered();
            _list.BeginUpdate();
            _list.Items.Clear();
            int add = 0, sub = 0, shown = 0;
            foreach (var e in list)
            {
                if (e.Delta > 0) add += e.Delta; else sub += e.Delta;
                if (shown >= 2000) continue;
                shown++;
                var it = new ListViewItem(e.TimeUtc.ToLocalTime().ToString("MM-dd HH:mm:ss"));
                it.SubItems.Add(e.ClassName);
                it.SubItems.Add(e.StudentName);
                it.SubItems.Add((e.Delta > 0 ? "+" : "") + e.Delta);
                it.SubItems.Add(e.Reason);
                it.SubItems.Add(e.Operator);
                it.SubItems.Add(RoleText.Name(e.OperatorRole));
                it.SubItems.Add(e.Note);
                it.ForeColor = e.Delta > 0 ? Theme.Green : Theme.Red;
                _list.Items.Add(it);
            }
            _list.EndUpdate();

            _summary.Text = "  共 " + list.Count + " 条记录（显示前 " + shown + " 条）     加分合计 +" + add + "     扣分合计 " + sub
                + "     净变化 " + (add + sub > 0 ? "+" : "") + (add + sub);
            SetSub((App.Current == null ? "" : App.Current.Name + "  ·  ") + "记录随每次加减分自动生成，可用于期末讲评与家长沟通");
        }

        private void ExportNow()
        {
            var list = Filtered();
            if (list.Count == 0) { Toast.Show(this, "当前筛选条件下没有记录", Theme.Red); return; }

            using (var d = new SaveFileDialog
            {
                Filter = "Excel 工作簿 (*.xlsx)|*.xlsx",
                FileName = Exporter.DefaultFileName("Tensyan-积分记录"),
                Title = "导出积分记录"
            })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                string msg;
                if (Exporter.ExportEvents(App.Data, App.Current, list, d.FileName, out msg))
                {
                    Toast.Show(this, msg, Theme.Green);
                    TryOpenFolder(d.FileName);
                }
                else Toast.Show(this, msg, Theme.Red);
            }
        }

        public static void TryOpenFolder(string file)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "/select,\"" + file + "\"") { UseShellExecute = true });
            }
            catch { }
        }
    }
}
