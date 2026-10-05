using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Tensyan.Core;

namespace Tensyan.UI.Pages
{
    /// <summary>系统设置：班级管理、理由标签、偏好、数据与备份。</summary>
    public class PageSettings : PageBase
    {
        private SegmentedControl _tabs;
        private readonly List<Panel> _panes = new List<Panel>();
        private int _tab;

        private ListView _classList;
        private ListBox _addTags, _subTags;
        private FluentInput _school, _quickAdd, _quickSub;
        private CheckBox _sound, _anim, _regUsb;
        private NumericUpDown _keep;
        private Label _dataPath;

        public PageSettings(App app) : base(app)
        {
            TitleText = "系统设置";
            EnableTools(52);

            _tabs = new SegmentedControl { Location = Theme.PP(0, 9), Size = Theme.PS(440, 36) };
            _tabs.AddItems("班级管理", "理由标签", "偏好设置", "数据与备份");
            _tabs.SelectedIndexChanged += (s, e) => ShowTab(_tabs.SelectedIndex);
            Tools.Controls.Add(_tabs);

            _panes.Add(BuildClassPane());
            _panes.Add(BuildTagPane());
            _panes.Add(BuildPrefPane());
            _panes.Add(BuildDataPane());
            foreach (var p in _panes) Body.Controls.Add(p);

            ShowTab(0);
        }

        private void ShowTab(int index)
        {
            _tab = index;
            for (int i = 0; i < _panes.Count; i++) _panes[i].Visible = (i == index);
            Reload();
        }

        /// <summary>开发自检：直接切到某个设置分页。</summary>
        public void DebugShowTab(int index)
        {
            if (_tabs != null) _tabs.SelectedIndex = index;
            ShowTab(index);
        }

        protected override void Layout3()
        {
            base.Layout3();
            foreach (var p in _panes) p.SetBounds(0, 0, Body.Width, Body.Height);
            LayoutPanes();
        }

        private void LayoutPanes()
        {
            int w = Body.Width, h = Body.Height;
            if (_addTags != null)
            {
                int half = (w - Theme.P(30)) / 2;
                _addTags.SetBounds(0, Theme.P(40), half, Math.Max(Theme.P(120), h - Theme.P(160)));
                _subTags.SetBounds(half + Theme.P(30), Theme.P(40), half, Math.Max(Theme.P(120), h - Theme.P(160)));
            }
        }

        public override void Reload()
        {
            if (_tab == 0)
            {
                _classList.BeginUpdate();
                _classList.Items.Clear();
                foreach (var c in App.Data.Classes)
                {
                    var it = new ListViewItem(c.Name);
                    it.SubItems.Add(c.Grade);
                    it.SubItems.Add(c.Teacher);
                    int n = 0, total = 0;
                    foreach (var s in c.Students) { if (!s.Archived) { n++; total += s.Score; } }
                    it.SubItems.Add(n.ToString());
                    it.SubItems.Add(total.ToString());
                    it.SubItems.Add(c == App.Current ? "当前" : "");
                    if (c == App.Current) it.ForeColor = Theme.Primary;
                    _classList.Items.Add(it);
                }
                _classList.EndUpdate();
                SetSub("共 " + App.Data.Classes.Count + " 个班级  ·  学生数据与积分记录按班级分别保存");
            }
            else if (_tab == 1)
            {
                _addTags.Items.Clear();
                foreach (var t in App.Settings.AddTags) _addTags.Items.Add(t.Text + "   +" + t.Points);
                _subTags.Items.Clear();
                foreach (var t in App.Settings.SubTags) _subTags.Items.Add(t.Text + "   -" + t.Points);
                SetSub("这些标签会出现在加减分窗口里，点击即可带出分值");
            }
            else if (_tab == 2)
            {
                _school.Text = App.Settings.SchoolName;
                _sound.Checked = App.Settings.SoundEnabled;
                _anim.Checked = App.Settings.AnimationEnabled;
                _regUsb.Checked = App.Settings.RegisterNeedsAdminUsb;
                _quickAdd.Text = string.Join(",", App.Settings.QuickAdd.ConvertAll(v => v.ToString()).ToArray());
                _quickSub.Text = string.Join(",", App.Settings.QuickSub.ConvertAll(v => v.ToString()).ToArray());
                _keep.Value = Math.Max(3, Math.Min(100, App.Settings.BackupKeep));
                SetSub("界面与操作偏好，保存后立即生效");
            }
            else
            {
                string kind;
                string ap = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (Paths.PortableData) kind = "程序目录 · 绿色便携";
                else if (!string.IsNullOrEmpty(ap) && Paths.DataDir.StartsWith(ap, StringComparison.OrdinalIgnoreCase)) kind = "用户目录";
                else if (!string.IsNullOrEmpty(Paths.WarningReason)) kind = "兜底临时目录";
                else kind = "自定义位置";
                _dataPath.Text = "数据目录：" + Paths.DataDir + "（" + kind + "）";
                SetSub("备份、导出与数据清理，操作前请留意提示");
            }
        }

        // ---------------- 班级管理 ----------------

        private Panel BuildClassPane()
        {
            var p = new Panel { BackColor = Theme.Bg };

            _classList = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                Font = Theme.Caption(),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Theme.Card,
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };
            _classList.Columns.Add("班级名称", Theme.P(200));
            _classList.Columns.Add("年级", Theme.P(120));
            _classList.Columns.Add("教师", Theme.P(120));
            _classList.Columns.Add("学生数", Theme.P(80));
            _classList.Columns.Add("总积分", Theme.P(90));
            _classList.Columns.Add("状态", Theme.P(80));
            _classList.DoubleClick += (s, e) => EditClass();
            p.Controls.Add(_classList);

            var buttons = new List<RoundButton>
            {
                MkPaneBtn("新建班级", IconKind.Add, 108, Theme.Primary, false, () => AddClass()),
                MkPaneBtn("编辑", IconKind.Edit, 88, Theme.Primary, true, () => EditClass()),
                MkPaneBtn("设为当前", IconKind.Check, 108, Theme.Green, true, () => SetCurrentClass()),
                MkPaneBtn("删除班级", IconKind.Trash, 108, Theme.Red, true, () => DeleteClass())
            };
            foreach (var b in buttons) p.Controls.Add(b);

            var students = new List<RoundButton>
            {
                MkPaneBtn("批量添加学生", IconKind.Add, 130, Theme.Primary, true, () => ImportStudents()),
                MkPaneBtn("导出学生名单", IconKind.Export, 130, Theme.Green, true, () => ExportRoster()),
                MkPaneBtn("恢复归档学生", IconKind.Students, 140, Theme.TextSub, true, () => ManagedArchived())
            };
            foreach (var b in students) p.Controls.Add(b);

            p.Resize += (s, e) =>
            {
                int listH = Math.Min(Theme.P(330), Math.Max(Theme.P(120), p.Height - Theme.P(180)));
                _classList.SetBounds(0, Theme.P(40), Math.Min(Theme.P(880), Math.Max(Theme.P(300), p.Width - Theme.P(30))), listH);
                int y1 = Theme.P(40) + listH + Theme.P(18);
                int bx = 0;
                foreach (var b in buttons) { b.Location = new Point(bx, y1); bx += b.Width + Theme.P(10); }
                int sx = 0;
                foreach (var b in students) { b.Location = new Point(sx, y1 + Theme.P(44)); sx += b.Width + Theme.P(10); }
            };
            return p;
        }

        private RoundButton MkPaneBtn(string text, IconKind icon, int w, Color color, bool soft, Action act)
        {
            var b = new RoundButton
            {
                Text = text,
                Icon = icon,
                Size = Theme.PS(w, 34),
                Soft = soft,
                Fill = soft ? Theme.Card : color,
                OutlineColor = color,
                TextColor = soft ? color : Color.White,
                Font = Theme.Body()
            };
            b.Click += (s, e) => act();
            return b;
        }

        private void AddClass()
        {
            var c = new SchoolClass();
            using (var d = new ClassEditDialog(c, true))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                c.Name = d.ClassName; c.Grade = d.Grade; c.Teacher = d.Teacher;
                App.Data.Classes.Add(c);
                App.SwitchClass(c);
                App.Raise();
                Reload();
                Toast.Show(this, "班级已创建：" + c.Name, Theme.Green);
            }
        }

        private SchoolClass SelectedClass()
        {
            if (_classList.SelectedIndices.Count == 0) return null;
            int i = _classList.SelectedIndices[0];
            if (i < 0 || i >= App.Data.Classes.Count) return null;
            return App.Data.Classes[i];
        }

        private void EditClass()
        {
            var c = SelectedClass();
            if (c == null) { Toast.Show(this, "请先选择一个班级", Theme.Red); return; }
            using (var d = new ClassEditDialog(c, false))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                c.Name = d.ClassName; c.Grade = d.Grade; c.Teacher = d.Teacher;
                App.Raise();
                Reload();
            }
        }

        private void SetCurrentClass()
        {
            var c = SelectedClass();
            if (c == null) { Toast.Show(this, "请先选择一个班级", Theme.Red); return; }
            App.SwitchClass(c);
            Reload();
        }

        private void DeleteClass()
        {
            var c = SelectedClass();
            if (c == null) { Toast.Show(this, "请先选择一个班级", Theme.Red); return; }
            if (App.Data.Classes.Count <= 1) { Toast.Show(this, "至少需要保留一个班级", Theme.Red); return; }
            if (MessageBox.Show(this, "确定删除班级 " + c.Name + " 吗？该班的 " + c.Students.Count + " 名学生与积分记录将一并删除，且不可恢复。", "Tensyan",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;

            App.Data.Classes.Remove(c);
            foreach (var e in new List<ScoreEvent>(App.Data.Events)) if (e.ClassId == c.Id) App.Data.Events.Remove(e);
            App.SwitchClass(App.Data.Classes[0]);
            App.Raise();
            Reload();
        }

        private void ImportStudents()
        {
            if (App.Current == null) return;
            using (var d = new ImportStudentsDialog())
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                int n = 0;
                foreach (var parts in d.Rows())
                {
                    if (parts.Length == 0 || Str.Blank(parts[0])) continue;
                    var s = new Student { Name = parts[0] };
                    if (parts.Length > 1) s.Gender = parts[1] == "男" || parts[1] == "女" ? parts[1] : "";
                    if (parts.Length > 2) s.No = parts[2];
                    App.Current.Students.Add(s);
                    n++;
                }
                App.Raise();
                Reload();
                Toast.Show(this, "已导入 " + n + " 名学生到 " + App.Current.Name, Theme.Green);
            }
        }

        private void ExportRoster()
        {
            if (App.Current == null) return;
            using (var d = new SaveFileDialog { Filter = "Excel 工作簿 (*.xlsx)|*.xlsx", FileName = Exporter.DefaultFileName("Tensyan-学生名单"), Title = "导出学生名单" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                string msg;
                if (Exporter.ExportRoster(App.Current, d.FileName, out msg)) { Toast.Show(this, msg, Theme.Green); PageHistory.TryOpenFolder(d.FileName); }
                else Toast.Show(this, msg, Theme.Red);
            }
        }

        private void ManagedArchived()
        {
            if (App.Current == null) return;
            var archived = new List<Student>();
            foreach (var s in App.Current.Students) if (s.Archived) archived.Add(s);
            if (archived.Count == 0) { Toast.Show(this, "当前班级没有已归档的学生", Theme.Primary); return; }
            if (MessageBox.Show(this, "当前班级有 " + archived.Count + " 名已归档学生。是否全部恢复显示？", "归档名单",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            foreach (var s in archived) s.Archived = false;
            App.Raise();
            Reload();
        }

        // ---------------- 理由标签 ----------------

        private Panel BuildTagPane()
        {
            var p = new Panel { BackColor = Theme.Bg };
            p.Paint += (s, e) =>
            {
                using (var f = Theme.BodyStrong())
                using (var b = new SolidBrush(Theme.TextMain))
                {
                    e.Graphics.DrawString("加分理由", f, b, 0, Theme.P(12));
                    e.Graphics.DrawString("扣分理由", f, b, (p.Width - Theme.P(30)) / 2 + Theme.P(30), Theme.P(12));
                }
            };
            p.Resize += (s, e) => p.Invalidate();

            _addTags = new ListBox { Font = Theme.Body(), BorderStyle = BorderStyle.FixedSingle, BackColor = Theme.Card, IntegralHeight = false };
            _subTags = new ListBox { Font = Theme.Body(), BorderStyle = BorderStyle.FixedSingle, BackColor = Theme.Card, IntegralHeight = false };
            p.Controls.Add(_addTags);
            p.Controls.Add(_subTags);

            var addA = MkPaneBtn("添加", IconKind.Add, 84, Theme.Green, true, () => EditTag(true, true));
            var editA = MkPaneBtn("编辑", IconKind.Edit, 84, Theme.TextSub, true, () => EditTag(true, false));
            var delA = MkPaneBtn("删除", IconKind.Trash, 84, Theme.Red, true, () => DeleteTag(true));
            var addS = MkPaneBtn("添加", IconKind.Add, 84, Theme.Red, true, () => EditTag(false, true));
            var editS = MkPaneBtn("编辑", IconKind.Edit, 84, Theme.TextSub, true, () => EditTag(false, false));
            var delS = MkPaneBtn("删除", IconKind.Trash, 84, Theme.Red, true, () => DeleteTag(false));

            var a = new List<RoundButton> { addA, editA, delA };
            var b2 = new List<RoundButton> { addS, editS, delS };
            foreach (var b in a) p.Controls.Add(b);
            foreach (var b in b2) p.Controls.Add(b);

            p.Resize += (s, e) =>
            {
                int half = (p.Width - Theme.P(30)) / 2;
                int listH = Math.Min(Theme.P(310), Math.Max(Theme.P(120), p.Height - Theme.P(170)));
                _addTags.SetBounds(0, Theme.P(40), half, listH);
                _subTags.SetBounds(half + Theme.P(30), Theme.P(40), half, listH);
                int by = Theme.P(40) + listH + Theme.P(16);
                int x = 0;
                foreach (var b in a) { b.Location = new Point(x, by); x += b.Width + Theme.P(8); }
                int x2 = half + Theme.P(30);
                foreach (var b in b2) { b.Location = new Point(x2, by); x2 += b.Width + Theme.P(8); }
            };
            return p;
        }

        private void EditTag(bool isAdd, bool create)
        {
            var list = isAdd ? App.Settings.AddTags : App.Settings.SubTags;
            ListBox box = isAdd ? _addTags : _subTags;
            ReasonTag tag;
            if (create) tag = new ReasonTag("新理由", 1);
            else
            {
                if (box.SelectedIndex < 0) { Toast.Show(this, "请先选择一个理由标签", Theme.Red); return; }
                tag = list[box.SelectedIndex];
            }

            using (var d = new TagEditDialog(tag.Text, tag.Points, isAdd))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                tag.Text = d.TagText;
                tag.Points = d.Points;
                if (create) list.Add(tag);
                App.Store.Save();
                Reload();
            }
        }

        private void DeleteTag(bool isAdd)
        {
            var list = isAdd ? App.Settings.AddTags : App.Settings.SubTags;
            ListBox box = isAdd ? _addTags : _subTags;
            if (box.SelectedIndex < 0) { Toast.Show(this, "请先选择一个理由标签", Theme.Red); return; }
            list.RemoveAt(box.SelectedIndex);
            App.Store.Save();
            Reload();
        }

        // ---------------- 偏好 ----------------

        private Panel BuildPrefPane()
        {
            var p = new Panel { BackColor = Theme.Bg };
            int y = Theme.P(8);

            p.Controls.Add(MkField("学校 / 班级抬头", 0, y));
            _school = new FluentInput { Location = Theme.PP(0, y + Theme.P(22)), Size = Theme.PS(320, 36) };
            p.Controls.Add(_school);
            y += Theme.P(72);

            p.Controls.Add(MkField("快捷加分分值（逗号分隔）", 0, y));
            _quickAdd = new FluentInput { Location = Theme.PP(0, y + Theme.P(22)), Size = Theme.PS(200, 36) };
            p.Controls.Add(_quickAdd);

            p.Controls.Add(MkField("快捷扣分值（逗号分隔）", 230, y));
            _quickSub = new FluentInput { Location = Theme.PP(230, y + Theme.P(22)), Size = Theme.PS(200, 36) };
            p.Controls.Add(_quickSub);
            y += Theme.P(72);

            p.Controls.Add(MkField("备份保留份数", 0, y));
            _keep = new NumericUpDown
            {
                Font = Theme.Body(),
                Location = Theme.PP(0, y + Theme.P(24)),
                Width = Theme.P(110),
                Minimum = 3,
                Maximum = 100,
                Value = 20,
                TextAlign = HorizontalAlignment.Center
            };
            p.Controls.Add(_keep);
            y += Theme.P(76);

            _sound = new CheckBox { Text = "加分时播放提示音", Font = Theme.Body(), Location = Theme.PP(0, y), AutoSize = true, Checked = true, BackColor = Color.Transparent };
            p.Controls.Add(_sound);
            _anim = new CheckBox { Text = "显示 +N 浮动动画", Font = Theme.Body(), Location = Theme.PP(0, y + Theme.P(28)), AutoSize = true, Checked = true, BackColor = Color.Transparent };
            p.Controls.Add(_anim);
            _regUsb = new CheckBox
            {
                Text = "注册新账号需要管理员登录U盘认证",
                Font = Theme.Body(),
                Location = Theme.PP(0, y + Theme.P(56)),
                AutoSize = true,
                Checked = true,
                BackColor = Color.Transparent
            };
            p.Controls.Add(_regUsb);
            y += Theme.P(98);

            var save = MkPaneBtn("保存设置", IconKind.Check, 120, Theme.Primary, false, () => SavePrefs());
            save.Location = Theme.PP(0, y);
            p.Controls.Add(save);

            var demo = MkPaneBtn("载入示例数据（覆盖现有班级）", IconKind.Refresh, 230, Theme.Amber, true, () =>
            {
                if (MessageBox.Show(this, "将用示例班级覆盖当前所有班级与记录，确定继续吗？", "Tensyan", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
                App.SeedDemo(28, 2);
                App.SwitchClass(App.Data.Classes[0]);
                Reload();
                Toast.Show(this, "示例数据已载入", Theme.Green);
            });
            demo.Location = Theme.PP(136, y);
            p.Controls.Add(demo);

            return p;
        }

        private Label MkField(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Font = Theme.BodyStrong(),
                ForeColor = Theme.TextSub,
                Location = Theme.PP(x, y),
                AutoSize = true,
                BackColor = Color.Transparent
            };
        }

        private void SavePrefs()
        {
            App.Settings.SchoolName = _school.Text.Trim();
            App.Settings.SoundEnabled = _sound.Checked;
            App.Settings.AnimationEnabled = _anim.Checked;
            Anim.Enabled = _anim.Checked;
            App.Settings.RegisterNeedsAdminUsb = _regUsb.Checked;
            App.Settings.BackupKeep = (int)_keep.Value;
            App.Settings.QuickAdd = ParseInts(_quickAdd.Text, new List<int> { 1, 2, 3, 5 });
            App.Settings.QuickSub = ParseInts(_quickSub.Text, new List<int> { 1, 2, 3, 5 });
            App.Store.Save();
            Toast.Show(this, "设置已保存", Theme.Green);
        }

        private static List<int> ParseInts(string text, List<int> fallback)
        {
            var list = new List<int>();
            foreach (var part in text.Split(new[] { ',', '，', ' ', ';', '；' }))
            {
                int v;
                if (int.TryParse(part.Trim(), out v) && v > 0 && v <= 999) list.Add(v);
            }
            if (list.Count == 0) return fallback;
            if (list.Count > 6) list.RemoveRange(6, list.Count - 6);
            return list;
        }

        /// <summary>更改数据位置：把现有数据复制到用户选定的可写目录，并记住该位置。</summary>
        private void ChangeDataDir()
        {
            using (var d = new FolderBrowserDialog())
            {
                d.Description = "选择一个可写的位置保存 Tensyan 数据（例如 D:\\TensyanData，或桌面/文档里的文件夹）";
                d.SelectedPath = Paths.DataDir;
                if (d.ShowDialog(this) != DialogResult.OK) return;

                string target = (d.SelectedPath ?? "").Trim();
                if (target.Length == 0) return;
                if (string.Equals(target.TrimEnd('\\'), Paths.DataDir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                {
                    Toast.Show(this, "这就是当前的数据位置", Theme.Amber);
                    return;
                }
                if (!Paths.CanWrite(target))
                {
                    MessageBox.Show(this, "这个位置不可写，请换一个（例如 D 盘、桌面或文档里的文件夹）。", "更改数据位置",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (MessageBox.Show(this,
                        "把数据迁移到：\r\n" + target + "\r\n\r\n现有数据会复制过去（原位置保留），然后程序需要重启。继续吗？",
                        "更改数据位置", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;

                int copied = 0;
                try
                {
                    Directory.CreateDirectory(target);
                    foreach (var f in Directory.GetFiles(Paths.DataDir))
                    {
                        string name = Path.GetFileName(f);
                        if (name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
                        try { File.Copy(f, Path.Combine(target, name), true); copied++; } catch { }
                    }
                    if (Directory.Exists(Paths.BackupDir))
                    {
                        string bdst = Path.Combine(target, "backups");
                        Directory.CreateDirectory(bdst);
                        foreach (var f in Directory.GetFiles(Paths.BackupDir))
                        {
                            try { File.Copy(f, Path.Combine(bdst, Path.GetFileName(f)), true); copied++; } catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Toast.Show(this, "迁移失败：" + ex.Message, Theme.Red);
                    return;
                }

                bool ok = Paths.WritePointer(target);
                MessageBox.Show(this,
                    "已迁移 " + copied + " 个文件到：\r\n" + target
                    + (ok ? "\r\n\r\n程序将重新启动，之后所有数据都保存在新位置。"
                          : "\r\n\r\n注意：无法写入位置记录文件，请改用启动参数 --data \"" + target + "\" 指定。"),
                    "更改数据位置", MessageBoxButtons.OK, MessageBoxIcon.Information);

                if (ok)
                {
                    try
                    {
                        // 等旧进程退出后再启动，避免单实例互斥把新实例挡掉
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                            "cmd.exe", "/c ping -n 3 127.0.0.1 > nul & start \"\" \"" + Application.ExecutablePath + "\"")
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                        });
                    }
                    catch { }
                    Application.Exit();
                }
                else
                {
                    Reload();
                }
            }
        }

        // ---------------- 数据与备份 ----------------
        private Panel BuildDataPane()
        {
            var p = new Panel { BackColor = Theme.Bg };
            _dataPath = new Label
            {
                Font = Theme.BodyStrong(),
                ForeColor = Theme.TextMain,
                AutoSize = false,
                Location = Theme.PP(0, Theme.P(6)),
                Size = new Size(Theme.P(900), Theme.LineHeight(Theme.BodyStrong())),
                BackColor = Color.Transparent
            };
            p.Controls.Add(_dataPath);

            var hint = new Label
            {
                Font = Theme.Caption(),
                ForeColor = string.IsNullOrEmpty(Paths.WarningReason) ? Theme.TextSub : Theme.Amber,
                AutoSize = false,
                Location = Theme.PP(0, Theme.P(32)),
                Size = new Size(Theme.P(900), Theme.LineHeight(Theme.Caption()) * 3 + Theme.P(6)),
                BackColor = Color.Transparent,
                Text = (string.IsNullOrEmpty(Paths.WarningReason) ? "" : "⚠ " + Paths.WarningReason + "\r\n")
                    + "程序默认把数据保存在自己所在目录的 data 文件夹里（绿色便携）；如果程序目录不可写，会自动改用用户目录。\r\n"
                    + "如果哪个位置都写不进去，可以点“更改数据位置…”自己指定一个可写文件夹（例如 D 盘或桌面）。"
            };
            p.Controls.Add(hint);

            int y = Theme.P(84);
            var b1 = MkPaneBtn("立即备份", IconKind.Check, 116, Theme.Primary, false, () =>
            {
                string path = App.Store.Backup(true);
                Toast.Show(this, path == null ? "备份失败" : "已备份到 backups 目录", path == null ? Theme.Red : Theme.Green);
            });
            b1.Location = Theme.PP(0, y);
            p.Controls.Add(b1);

            var b2 = MkPaneBtn("从备份恢复", IconKind.Refresh, 130, Theme.Amber, true, () => Restore());
            b2.Location = Theme.PP(128, y);
            p.Controls.Add(b2);

            var b3 = MkPaneBtn("打开数据目录", IconKind.Export, 130, Theme.TextSub, true, () =>
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "\"" + Paths.DataDir + "\"") { UseShellExecute = true }); } catch { }
            });
            b3.Location = Theme.PP(270, y);
            p.Controls.Add(b3);

            var b4 = MkPaneBtn("关于 Tensyan", IconKind.Info, 146, Theme.TextSub, true, () => { using (var d = new AboutDialog()) d.ShowDialog(this); });
            b4.Location = Theme.PP(412, y);
            p.Controls.Add(b4);

            var b5 = MkPaneBtn("更改数据位置…", IconKind.Folder, 170, Theme.Primary, true, () => ChangeDataDir());
            b5.Location = Theme.PP(570, y);
            p.Controls.Add(b5);

            y += Theme.P(56);
            p.Controls.Add(MkField("导出", 0, y));
            y += Theme.P(26);

            var e1 = MkPaneBtn("导出当前班级积分榜", IconKind.Export, 176, Theme.Green, true, () =>
            {
                if (App.Current == null) return;
                using (var d = new SaveFileDialog { Filter = "Excel 工作簿 (*.xlsx)|*.xlsx", FileName = Exporter.DefaultFileName("Tensyan-" + App.Current.Name + "-积分"), Title = "导出积分榜" })
                {
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                    string msg;
                    if (Exporter.ExportClassScores(App.Data, App.Current, d.FileName, out msg)) { Toast.Show(this, msg, Theme.Green); PageHistory.TryOpenFolder(d.FileName); }
                    else Toast.Show(this, msg, Theme.Red);
                }
            });
            e1.Location = Theme.PP(0, y);
            p.Controls.Add(e1);

            var e2 = MkPaneBtn("导出全部班级总表", IconKind.Export, 176, Theme.Green, true, () =>
            {
                using (var d = new SaveFileDialog { Filter = "Excel 工作簿 (*.xlsx)|*.xlsx", FileName = Exporter.DefaultFileName("Tensyan-全部班级积分"), Title = "导出总表" })
                {
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                    string msg;
                    if (Exporter.ExportAll(App.Data, d.FileName, out msg)) { Toast.Show(this, msg, Theme.Green); PageHistory.TryOpenFolder(d.FileName); }
                    else Toast.Show(this, msg, Theme.Red);
                }
            });
            e2.Location = Theme.PP(186, y);
            p.Controls.Add(e2);

            var e3 = MkPaneBtn("导出全部积分记录", IconKind.Export, 176, Theme.Green, true, () =>
            {
                using (var d = new SaveFileDialog { Filter = "Excel 工作簿 (*.xlsx)|*.xlsx", FileName = Exporter.DefaultFileName("Tensyan-积分记录"), Title = "导出记录" })
                {
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                    string msg;
                    if (Exporter.ExportEvents(App.Data, null, App.Store.EventsOf(null), d.FileName, out msg)) { Toast.Show(this, msg, Theme.Green); PageHistory.TryOpenFolder(d.FileName); }
                    else Toast.Show(this, msg, Theme.Red);
                }
            });
            e3.Location = Theme.PP(372, y);
            p.Controls.Add(e3);

            y += Theme.P(62);
            p.Controls.Add(MkField("危险操作（仅管理员，操作不可恢复）", 0, y));
            y += Theme.P(26);

            var d1 = MkPaneBtn("清零当前班级积分", IconKind.Refresh, 168, Theme.Red, true, () =>
            {
                if (App.Current == null) return;
                if (MessageBox.Show(this, "将把 " + App.Current.Name + " 所有学生的积分清零，并清除该班的积分记录。确定继续吗？", "Tensyan",
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
                foreach (var s in App.Current.Students) s.Score = 0;
                for (int i = App.Data.Events.Count - 1; i >= 0; i--) if (App.Data.Events[i].ClassId == App.Current.Id) App.Data.Events.RemoveAt(i);
                App.Raise();
                Reload();
                Toast.Show(this, "积分已清零", Theme.Green);
            });
            d1.Location = Theme.PP(0, y);
            p.Controls.Add(d1);

            var d2 = MkPaneBtn("清空全部积分记录", IconKind.Trash, 168, Theme.Red, true, () =>
            {
                if (MessageBox.Show(this, "将删除全部班级的积分记录（学生积分保留）。确定继续吗？", "Tensyan",
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
                App.Data.Events.Clear();
                App.Raise();
                Reload();
                Toast.Show(this, "记录已清空", Theme.Green);
            });
            d2.Location = Theme.PP(178, y);
            p.Controls.Add(d2);

            return p;
        }

        private void Restore()
        {
            using (var d = new OpenFileDialog { Filter = "Tensyan 备份 (*.json)|*.json", InitialDirectory = Paths.BackupDir, Title = "选择要恢复的备份文件" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var data = Json.Parse<AppData>(System.IO.File.ReadAllText(d.FileName));
                    if (data == null) { Toast.Show(this, "备份文件内容无效", Theme.Red); return; }
                    if (MessageBox.Show(this, "恢复后当前数据会被备份文件替换（当前数据会先自动备份一份）。确定继续吗？", "Tensyan",
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
                    App.Store.Backup(true);
                    App.Store.Data = data;
                    App.Current = App.Data.Classes.Count > 0 ? App.Data.Classes[0] : null;
                    App.Store.Save();
                    Reload();
                    Toast.Show(this, "已从备份恢复", Theme.Green);
                }
                catch (Exception ex)
                {
                    Toast.Show(this, "恢复失败：" + ex.Message, Theme.Red);
                }
            }
        }
    }

    /// <summary>编辑理由标签。</summary>
    public class TagEditDialog : DialogBase
    {
        private readonly TextBox _text;
        private readonly NumericUpDown _points;

        public TagEditDialog(string text, int points, bool isAdd)
            : base((isAdd ? "加分" : "扣分") + "理由标签", 440, 300)
        {
            MkLabel("理由文字", 24, 52, Theme.BodyStrong(), Theme.TextSub);
            _text = MkInput(24, 74, 392, text);

            MkLabel("分值（正数）", 24, 132, Theme.BodyStrong(), Theme.TextSub);
            _points = new NumericUpDown
            {
                Font = Theme.Body(),
                Location = Theme.PP(24, 154),
                Width = Theme.P(120),
                Minimum = 1,
                Maximum = 100,
                Value = Math.Max(1, Math.Min(100, Math.Abs(points))),
                TextAlign = HorizontalAlignment.Center
            };
            Body.Controls.Add(_points);
            Shown += (s, e) => { _text.Focus(); _text.SelectAll(); };
        }

        public string TagText { get { return _text.Text.Trim(); } }
        public int Points { get { return (int)_points.Value; } }

        protected override bool OnOk()
        {
            if (Str.Blank(_text.Text)) { Toast.Show(this, "理由文字不能为空", Theme.Red); return false; }
            return true;
        }
    }
}
