using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Tensyan.Core;

namespace Tensyan.UI
{
    /// <summary>加减分对话框（核心操作窗口）。</summary>
    public class ScoreDialog : DialogBase
    {
        private readonly App _app;
        private readonly Student _stu;
        private bool _add = true;
        private int _points = 1;
        private string _reason = "";

        private RoundButton _btnAdd, _btnSub;
        private NumericUpDown _num;
        private FlowLayoutPanel _chips;
        private readonly List<RoundButton> _pointBtns = new List<RoundButton>();
        private readonly List<RoundButton> _chipBtns = new List<RoundButton>();
        private TextBox _note;
        private Label _preview;

        public ScoreDialog(App app, Student stu)
            : base("积分操作 · " + stu.Name, 560, 640)
        {
            _app = app;
            _stu = stu;
            Build();
            SelectMode(true);
        }

        private void Build()
        {
            var av = new PictureBox { Location = Theme.PP(24, 20), Size = Theme.PS(52, 52), BackColor = Color.Transparent };
            av.Image = Avatar.Render(Theme.P(52), _stu.Name, _stu.Id, Theme.ScoreColor(_stu.Score));
            Body.Controls.Add(av);

            MkLabel(_stu.Name, 90, 22, Theme.Subtitle(), Theme.TextMain);
            MkLabel("当前积分 " + (_stu.Score > 0 ? "+" : "") + _stu.Score + " 分", 90, 50, Theme.Caption(), Theme.ScoreColor(_stu.Score));

            _btnAdd = new RoundButton
            {
                Text = "加分",
                Font = Theme.BodyStrong(),
                Size = Theme.PS(96, 36),
                Fill = Theme.Green,
                Location = Theme.PP(24, 88)
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
                    Location = Theme.PP(128, 88)
                };
                _btnSub.Click += (s, e) => SelectMode(false);
                Body.Controls.Add(_btnSub);
            }
            else
            {
                MkLabel("当前身份（" + RoleText.Name(_app.Session.Role) + "）只能加分", 136, 98, Theme.Caption(), Theme.TextFaint);
            }

            _preview = MkLabel("", 300, 96, Theme.BodyStrong(), Theme.Green);
            _preview.AutoSize = false;
            _preview.Size = new Size(Theme.P(236), Theme.LineHeight(Theme.BodyStrong()));
            _preview.TextAlign = ContentAlignment.MiddleRight;

            MkLabel("分值", 24, 142, Theme.BodyStrong(), Theme.TextSub);

            var pts = _app.Settings.QuickAdd;
            int px = 24;
            foreach (int p in pts)
            {
                var b = new RoundButton
                {
                    Text = "+" + p,
                    Size = Theme.PS(58, 34),
                    Soft = true,
                    Fill = Theme.Card,
                    OutlineColor = Theme.Green,
                    Location = Theme.PP(px, 166)
                };
                int val = p;
                b.Click += (s, e) => { if (!_add) SelectMode(true); SetPoints(val); };
                _pointBtns.Add(b);
                Body.Controls.Add(b);
                px += 64;
            }

            _num = new NumericUpDown
            {
                Font = Theme.Body(),
                Minimum = 1,
                Maximum = 999,
                Value = 1,
                Location = Theme.PP(px + 6, 168),
                Width = Theme.P(80),
                TextAlign = HorizontalAlignment.Center
            };
            _num.ValueChanged += (s, e) => { _points = (int)_num.Value; UpdatePreview(); };
            Body.Controls.Add(_num);
            MkLabel("自定义", px + 6, 202, Theme.Caption(), Theme.TextFaint);

            MkLabel("理由标签", 24, 228, Theme.BodyStrong(), Theme.TextSub);
            _chips = new FlowLayoutPanel
            {
                Location = Theme.PP(24, 250),
                Size = Theme.PS(512, 146),
                AutoScroll = true,
                BackColor = Theme.Card,
                Padding = new Padding(0)
            };
            Body.Controls.Add(_chips);
            RebuildChips();

            MkLabel("备注（可空）", 24, 406, Theme.BodyStrong(), Theme.TextSub);
            _note = MkInput(24, 428, 512);

            Shown += (s, e) => _num.Focus();
        }

        private void RebuildChips()
        {
            _chips.Controls.Clear();
            _chipBtns.Clear();
            var tags = _add ? _app.Settings.AddTags : _app.Settings.SubTags;
            foreach (var t in tags)
            {
                string text = t.Text + " " + (t.Points > 0 ? "+" : "") + t.Points;
                var b = new RoundButton
                {
                    Text = text,
                    Size = new Size(ChipWidth(text), Theme.P(32)),
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
                    foreach (var c in _chipBtns) { c.Fill = Theme.Card; c.OutlineColor = Theme.BorderStrong; c.Invalidate(); }
                    b.Fill = _add ? Theme.GreenSoft : Theme.RedSoft;
                    b.OutlineColor = _add ? Theme.Green : Theme.Red;
                    b.Invalidate();
                };
                _chipBtns.Add(b);
                _chips.Controls.Add(b);
            }
        }

        private int ChipWidth(string text)
        {
            var sz = TextRenderer.MeasureText(text, Theme.Caption());
            return sz.Width + Theme.P(30);
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

            var pts = add ? _app.Settings.QuickAdd : _app.Settings.QuickSub;
            for (int i = 0; i < _pointBtns.Count; i++)
            {
                var b = _pointBtns[i];
                b.Visible = i < pts.Count;
                if (i < pts.Count) b.Text = (add ? "+" : "-") + pts[i];
                b.OutlineColor = add ? Theme.Green : Theme.Red;
                b.Invalidate();
            }
            RebuildChips();
            _reason = "";
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
            int delta = _add ? _points : -_points;
            _preview.Text = "即将 " + (delta > 0 ? "加 " + _points : "扣 " + _points) + " 分 → " + (_stu.Score + delta) + " 分";
            _preview.ForeColor = _add ? Theme.Green : Theme.Red;
        }

        public int Delta { get { return _add ? _points : -_points; } }
        public string Reason { get { return _reason; } }
        public string NoteText { get { return _note == null ? "" : _note.Text.Trim(); } }

        protected override bool OnOk()
        {
            if (!_add && !_app.Session.CanSubScore) { Toast.Show(this, "当前身份不能扣分", Theme.Red); return false; }
            if (_add && !_app.Session.CanAddScore) { Toast.Show(this, "当前身份不能修改积分", Theme.Red); return false; }
            return true;
        }
    }

    /// <summary>新增 / 编辑学生。</summary>
    public class StudentEditDialog : DialogBase
    {
        private readonly TextBox _name, _no, _note;
        private readonly FluentSelect _gender;
        private readonly NumericUpDown _score;

        public StudentEditDialog(Student stu, bool isNew)
            : base(isNew ? "添加学生" : "编辑学生", 460, 400)
        {
            MkLabel("姓名 *", 24, 52, Theme.BodyStrong(), Theme.TextSub);
            _name = MkInput(24, 74, 412, stu.Name, "学生姓名");

            MkLabel("性别", 24, 132, Theme.BodyStrong(), Theme.TextSub);
            _gender = new FluentSelect { Location = Theme.PP(24, 154), Size = Theme.PS(120, 36) };
            _gender.AddItems("男", "女", "不填");
            _gender.SelectedIndex = stu.Gender == "女" ? 1 : (stu.Gender == "男" ? 0 : 2);
            Body.Controls.Add(_gender);

            MkLabel("学号 / 座号", 160, 132, Theme.BodyStrong(), Theme.TextSub);
            _no = MkInput(160, 154, 120, stu.No);

            if (isNew)
            {
                MkLabel("初始积分", 296, 132, Theme.BodyStrong(), Theme.TextSub);
                _score = new NumericUpDown
                {
                    Font = Theme.Body(),
                    Location = Theme.PP(296, 156),
                    Width = Theme.P(140),
                    Minimum = -999,
                    Maximum = 999,
                    Value = 0,
                    TextAlign = HorizontalAlignment.Center
                };
                Body.Controls.Add(_score);
            }
            else
            {
                _score = null;
                MkLabel("当前积分：" + stu.Score + " 分（改动请用加减分功能）", 296, 158, Theme.Caption(), Theme.TextFaint);
            }

            MkLabel("备注", 24, 212, Theme.BodyStrong(), Theme.TextSub);
            _note = MkInput(24, 234, 412, stu.Note);

            Shown += (s, e) => { _name.Focus(); _name.SelectAll(); };
        }

        public string StudentName { get { return _name.Text.Trim(); } }
        public string Gender { get { return _gender.SelectedIndex == 2 ? "" : _gender.SelectedItem; } }
        public string No { get { return _no.Text.Trim(); } }
        public string NoteText { get { return _note.Text.Trim(); } }
        public int InitialScore { get { return _score == null ? 0 : (int)_score.Value; } }

        protected override bool OnOk()
        {
            if (Str.Blank(_name.Text)) { Toast.Show(this, "请填写学生姓名", Theme.Red); return false; }
            return true;
        }
    }

    /// <summary>新增 / 编辑班级。</summary>
    public class ClassEditDialog : DialogBase
    {
        private readonly TextBox _name, _grade, _teacher;

        public ClassEditDialog(SchoolClass cls, bool isNew)
            : base(isNew ? "新建班级" : "编辑班级", 460, 320)
        {
            MkLabel("班级名称 *", 24, 52, Theme.BodyStrong(), Theme.TextSub);
            _name = MkInput(24, 74, 412, cls.Name, "例如 三(2)班");

            MkLabel("年级 / 学部", 24, 132, Theme.BodyStrong(), Theme.TextSub);
            _grade = MkInput(24, 154, 190, cls.Grade, "例如 三年级");

            MkLabel("班主任 / 任课教师", 230, 132, Theme.BodyStrong(), Theme.TextSub);
            _teacher = MkInput(230, 154, 206, cls.Teacher);

            Shown += (s, e) => { _name.Focus(); _name.SelectAll(); };
        }

        public string ClassName { get { return _name.Text.Trim(); } }
        public string Grade { get { return _grade.Text.Trim(); } }
        public string Teacher { get { return _teacher.Text.Trim(); } }

        protected override bool OnOk()
        {
            if (Str.Blank(_name.Text)) { Toast.Show(this, "请填写班级名称", Theme.Red); return false; }
            return true;
        }
    }

    /// <summary>新增 / 编辑账号。</summary>
    public class AccountEditDialog : DialogBase
    {
        private readonly TextBox _user, _display, _pwd, _pwd2, _note;
        private readonly FluentSelect _role;
        private readonly CheckBox _enabled;
        private readonly CheckedListBox _classes;
        private readonly Account _acc;
        private readonly bool _isNew;
        private readonly Label _roleHint;

        public AccountEditDialog(App app, Account acc, bool isNew)
            : base(isNew ? "新增账号" : "编辑账号", 620, 620)
        {
            _acc = acc;
            _isNew = isNew;

            MkLabel("用户名 *（登录用，字母/数字）", 24, 50, Theme.BodyStrong(), Theme.TextSub);
            _user = MkInput(24, 72, 250, acc.User);
            _user.Enabled = isNew;

            MkLabel("显示名称", 300, 50, Theme.BodyStrong(), Theme.TextSub);
            _display = MkInput(300, 72, 296, string.IsNullOrEmpty(acc.Display) ? "" : acc.Display);

            MkLabel("登录角色 *", 24, 128, Theme.BodyStrong(), Theme.TextSub);
            _role = new FluentSelect { Location = Theme.PP(24, 150), Size = Theme.PS(250, 36) };
            _role.AddItems("管理员 · 全部权限", "成员 · 仅加分", "访客 · 只读");
            _role.SelectedIndex = acc.Role == Role.Admin ? 0 : (acc.Role == Role.Member ? 1 : 2);
            _role.SelectedIndexChanged += (s, e) => { _roleHint.Text = ShortRole(IndexRole()); };
            Body.Controls.Add(_role);

            _roleHint = MkLabel(ShortRole(acc.Role), 300, 156, Theme.Caption(), Theme.TextFaint, 296);
            _roleHint.AutoSize = false;
            _roleHint.Height = Theme.LineHeight(Theme.Caption()) * 2 + Theme.P(2);

            MkLabel(isNew ? "登录密码 *" : "新密码（留空表示不修改）", 24, 200, Theme.BodyStrong(), Theme.TextSub);
            _pwd = MkInput(24, 222, 250);
            _pwd.UseSystemPasswordChar = true;

            MkLabel("确认密码", 300, 200, Theme.BodyStrong(), Theme.TextSub);
            _pwd2 = MkInput(300, 222, 296);
            _pwd2.UseSystemPasswordChar = true;

            MkLabel("可管理的班级（不勾选 = 全部班级）", 24, 268, Theme.BodyStrong(), Theme.TextSub);
            _classes = new CheckedListBox
            {
                Font = Theme.Body(),
                Location = Theme.PP(24, 290),
                Size = Theme.PS(572, 118),
                CheckOnClick = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Theme.Card
            };
            foreach (var c in app.Data.Classes)
            {
                bool chk = acc.ClassIds != null && acc.ClassIds.Contains(c.Id);
                _classes.Items.Add(c.Name, chk);
            }
            Body.Controls.Add(_classes);

            _enabled = new CheckBox
            {
                Text = "启用该账号",
                Font = Theme.Body(),
                ForeColor = Theme.TextMain,
                Checked = acc.Enabled,
                Location = Theme.PP(24, 420),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            Body.Controls.Add(_enabled);

            MkLabel("备注", 24, 450, Theme.BodyStrong(), Theme.TextSub);
            _note = MkInput(24, 472, 572, acc.Note, "例如：数学老师 / 值日班长");
        }

        private Role IndexRole()
        {
            return _role.SelectedIndex == 0 ? Role.Admin : (_role.SelectedIndex == 1 ? Role.Member : Role.Guest);
        }

        private static string ShortRole(Role r)
        {
            switch (r)
            {
                case Role.Admin: return "全部权限：可管理一切";
                case Role.Member: return "仅可给学生加分";
                default: return "只读查看，不能修改";
            }
        }

        public string UserName { get { return _user.Text.Trim(); } }
        public string Display { get { return _display.Text.Trim(); } }
        public Role Role { get { return IndexRole(); } }
        public string Password { get { return _pwd.Text; } }
        public bool IsEnabledChecked { get { return _enabled.Checked; } }
        public string NoteText { get { return _note.Text.Trim(); } }
        public List<string> ClassIds
        {
            get
            {
                var list = new List<string>();
                for (int i = 0; i < _classes.Items.Count; i++)
                    if (_classes.GetItemChecked(i)) list.Add(App.I.Data.Classes[i].Id);
                return list;
            }
        }

        protected override bool OnOk()
        {
            if (_isNew && Str.Blank(_user.Text)) { Toast.Show(this, "请填写用户名", Theme.Red); return false; }
            foreach (char c in _user.Text.Trim())
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.'))
                { Toast.Show(this, "用户名只能使用字母、数字、下划线", Theme.Red); return false; }

            if (_isNew && _pwd.Text.Length < 3) { Toast.Show(this, "密码至少 3 位", Theme.Red); return false; }
            if (_pwd.Text != _pwd2.Text) { Toast.Show(this, "两次输入的密码不一致", Theme.Red); return false; }

            string u = _user.Text.Trim();
            foreach (var a in App.I.Store.Accounts)
                if (a != _acc && string.Equals(a.User, u, StringComparison.OrdinalIgnoreCase))
                { Toast.Show(this, "用户名已存在", Theme.Red); return false; }
            return true;
        }
    }

    /// <summary>批量导入学生名单。</summary>
    public class ImportStudentsDialog : DialogBase
    {
        private readonly TextBox _text;

        public ImportStudentsDialog()
            : base("批量添加学生", 520, 540)
        {
            MkLabel("每行一个学生，可用逗号补充信息：姓名,性别,学号", 24, 52, Theme.BodyStrong(), Theme.TextMain);
            MkLabel("例如：张三,男,01    也可以直接粘贴一列姓名", 24, 76, Theme.Caption(), Theme.TextFaint);

            var wrap = new Panel { Location = Theme.PP(24, 100), Size = Theme.PS(472, 300), BackColor = Theme.Card };
            wrap.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.Clear(Theme.Card);
                Theme.StrokeRounded(e.Graphics, new Rectangle(0, 0, wrap.Width - 1, wrap.Height - 1), Theme.P(4), Theme.BorderStrong);
            };
            Body.Controls.Add(wrap);

            _text = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = Theme.Body(),
                Location = Theme.PP(8, 8),
                Size = new Size(wrap.Width - Theme.P(16), wrap.Height - Theme.P(16)),
                BorderStyle = BorderStyle.None,
                AcceptsReturn = true,
                BackColor = Theme.Card
            };
            wrap.Controls.Add(_text);

            MkLabel("提示：从 Excel 复制一列姓名直接粘贴即可", 24, 410, Theme.Caption(), Theme.TextFaint);
            Shown += (s, e) => _text.Focus();
        }

        public List<string[]> Rows()
        {
            var list = new List<string[]>();
            string[] lines = _text.Text.Replace("\r", "").Split('\n');
            foreach (var raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                string[] parts = line.Split(new[] { ',', '，', '\t', ';', '；' });
                for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();
                list.Add(parts);
            }
            return list;
        }

        protected override bool OnOk()
        {
            if (Rows().Count == 0) { Toast.Show(this, "请输入学生名单", Theme.Red); return false; }
            return true;
        }
    }

    /// <summary>制作登录U盘。</summary>
    public class UsbCreateDialog : DialogBase
    {
        private readonly App _app;
        private FluentSelect _drives, _accounts, _valid;
        private FluentInput _pin, _pin2;
        private Label _driveHint, _result;
        private Panel _built;

        public UsbCreateDialog(App app)
            : base("制作登录U盘", 640, 660)
        {
            _app = app;
            BtnOk.Text = "写入U盘";
            BtnOk.Width = Theme.P(120);
            BuildBody();
        }

        private void BuildBody()
        {
            int y = 50;
            MkLabel("第 1 步 · 选择U盘", 24, y, Theme.BodyStrong(), Theme.TextMain);
            y += 26;

            _drives = new FluentSelect { Location = Theme.PP(24, y), Size = Theme.PS(430, 36) };
            Body.Controls.Add(_drives);

            var refresh = new RoundButton
            {
                Text = "刷新",
                Icon = IconKind.Refresh,
                Size = Theme.PS(84, 36),
                Soft = true,
                Fill = Theme.Card,
                OutlineColor = Theme.BorderStrong,
                Location = Theme.PP(464, y)
            };
            refresh.Click += (s, e) => LoadDrives();
            Body.Controls.Add(refresh);

            _driveHint = MkLabel("", 24, y + 40, Theme.Caption(), Theme.TextFaint);
            y += 68;

            MkLabel("第 2 步 · 选择账号与口令", 24, y, Theme.BodyStrong(), Theme.TextMain);
            y += 26;
            MkLabel("绑定账号", 24, y, Theme.Caption(), Theme.TextSub);
            MkLabel("U盘口令（可空）", 300, y, Theme.Caption(), Theme.TextSub);
            MkLabel("再次输入口令", 450, y, Theme.Caption(), Theme.TextSub);
            y += 20;

            _accounts = new FluentSelect { Location = Theme.PP(24, y), Size = Theme.PS(260, 36) };
            Body.Controls.Add(_accounts);
            _pin = new FluentInput { Location = Theme.PP(300, y), Size = Theme.PS(140, 36), UseSystemPasswordChar = true, PlaceholderText = "可空" };
            Body.Controls.Add(_pin);
            _pin2 = new FluentInput { Location = Theme.PP(450, y), Size = Theme.PS(146, 36), UseSystemPasswordChar = true, PlaceholderText = "再次输入" };
            Body.Controls.Add(_pin2);
            y += 52;

            MkLabel("第 3 步 · 有效期", 24, y, Theme.BodyStrong(), Theme.TextMain);
            y += 26;
            _valid = new FluentSelect { Location = Theme.PP(24, y), Size = Theme.PS(260, 36) };
            _valid.AddItems("永久有效", "7 天", "30 天", "90 天", "180 天（一学期）");
            _valid.SelectedIndex = 0;
            Body.Controls.Add(_valid);
            y += 52;

            _built = new Panel { Location = Theme.PP(24, y), Size = Theme.PS(572, 112), BackColor = Theme.PrimarySoft };
            _built.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Theme.PrimarySoft);
                Theme.StrokeRounded(g, new Rectangle(0, 0, _built.Width - 1, _built.Height - 1), Theme.P(6), Theme.PrimarySoftBorder);
            };
            Body.Controls.Add(_built);

            var hl = new Label
            {
                AutoSize = false,
                Location = Theme.PP(14, 10),
                Size = new Size(_built.Width - Theme.P(28), _built.Height - Theme.P(20)),
                Font = Theme.Caption(),
                ForeColor = Theme.TextSub,
                BackColor = Color.Transparent,
                Text = "制作后U盘根目录会生成隐藏文件 TensyanKey.tky，插上它并在登录界面点“U盘登录”即可免密进入。\r\n" +
                       "密钥带 HMAC 签名，复制或改动都会失效；它与这份 Tensyan 数据配套，换电脑需重新制作。\r\n" +
                       "U盘只保存登录信息，班级积分数据仍然保存在本机。"
            };
            _built.Controls.Add(hl);
            y += 124;

            _result = MkLabel("", 24, y, Theme.BodyStrong(), Theme.TextMain);
            _result.AutoSize = false;
            _result.Size = new Size(Theme.P(572), Theme.P(54));

            LoadDrives();
            LoadAccounts();
        }

        private void LoadDrives()
        {
            _drives.ClearItems();
            var list = Ordered();
            foreach (var d in list)
                _drives.Items.Add(d.Describe() + (d.HasKey ? "   [已有密钥]" : ""));
            if (_drives.Items.Count > 0) _drives.SelectedIndex = 0;
            _driveHint.Text = _drives.Items.Count == 0
                ? "未检测到可写磁盘，请插入U盘后点“刷新”。"
                : "提示：带 [已有密钥] 的U盘会被新密钥覆盖。";
        }

        private static List<UsbDrive> Ordered()
        {
            var all = UsbKey.Drives(false);
            var list = new List<UsbDrive>();
            foreach (var d in all) if (d.DriveType == "可移动磁盘") list.Add(d);
            foreach (var d in all) if (d.DriveType != "可移动磁盘") list.Add(d);
            return list;
        }

        private void LoadAccounts()
        {
            _accounts.ClearItems();
            foreach (var a in _app.Store.Accounts)
                _accounts.Items.Add(a.User + "（" + RoleText.Name(a.Role) + "）" + (a.Enabled ? "" : " - 已停用"));
            if (_accounts.Items.Count > 0) _accounts.SelectedIndex = 0;
        }

        protected override bool OnOk()
        {
            var drives = Ordered();
            if (drives.Count == 0 || _drives.SelectedIndex < 0)
            {
                SetResult("未检测到可写的磁盘，请插入U盘后点“刷新”", Theme.Red);
                return false;
            }
            if (_accounts.SelectedIndex < 0 || _accounts.SelectedIndex >= _app.Store.Accounts.Count)
            {
                SetResult("请选择要绑定的账号", Theme.Red);
                return false;
            }
            if (_pin.Text != _pin2.Text)
            {
                SetResult("两次输入的口令不一致", Theme.Red);
                return false;
            }

            var drive = drives[_drives.SelectedIndex];
            var acc = _app.Store.Accounts[_accounts.SelectedIndex];
            int days = _valid.SelectedIndex == 0 ? 0 : (_valid.SelectedIndex == 1 ? 7 : (_valid.SelectedIndex == 2 ? 30 : (_valid.SelectedIndex == 3 ? 90 : 180)));

            var key = UsbKey.Build(_app.Store, acc, _pin.Text, days);
            string path = UsbKey.Write(drive.Root, key);
            if (path == null)
            {
                SetResult("写入失败：U盘可能被写保护或空间不足", Theme.Red);
                return false;
            }
            SetResult("制作成功！密钥已写入 " + path + "\r\n账号：" + acc.User + "（" + RoleText.Name(acc.Role) + "）"
                + (string.IsNullOrEmpty(_pin.Text) ? "，无口令" : "，需要口令")
                + (days == 0 ? "，永久有效" : "，" + days + " 天有效"), Theme.Green);
            Toast.Show(this, "登录U盘制作成功", Theme.Green);
            LoadDrives();
            return false;   // 保持窗口打开，方便连续制作
        }

        private void SetResult(string text, Color color)
        {
            _result.Text = text;
            _result.ForeColor = color;
        }
    }
}
