using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Tensyan.Core;

namespace Tensyan.UI.Pages
{
    /// <summary>账号权限：账号列表 + 三种模式说明。</summary>
    public class PageAccounts : PageBase
    {
        private ListView _list;
        private RoundButton _add, _edit, _reset, _toggle, _del, _mypwd;
        private RoleLegend _legend;
        private Panel _footer;

        public PageAccounts(App app) : base(app)
        {
            TitleText = "账号权限";
            EnableTools(52);

            _add = MkBtn("新增账号", IconKind.Add, 112, Theme.Primary, false);
            _add.Click += (s, e) => AddAccount();
            Tools.Controls.Add(_add);

            _mypwd = MkBtn("修改我的密码", IconKind.Key, 134, null, true, Theme.TextSub);
            _mypwd.Click += (s, e) =>
            {
                var acc = App.Session.Account;
                if (acc == null) { Toast.Show(this, "访客身份没有账号", Theme.Red); return; }
                using (var d = new ChangePasswordDialog(App, acc)) d.ShowDialog(this);
            };
            Tools.Controls.Add(_mypwd);

            _legend = new RoleLegend();
            Body.Controls.Add(_legend);

            _list = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                Font = Theme.Caption(),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Theme.Card,
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };
            _list.Columns.Add("用户名", Theme.P(130));
            _list.Columns.Add("显示名称", Theme.P(130));
            _list.Columns.Add("角色", Theme.P(90));
            _list.Columns.Add("状态", Theme.P(80));
            _list.Columns.Add("可见班级", Theme.P(180));
            _list.Columns.Add("最近登录", Theme.P(150));
            _list.Columns.Add("备注", Theme.P(200));
            _list.DoubleClick += (s, e) => EditAccount();
            Body.Controls.Add(_list);

            _footer = new Panel { BackColor = Theme.Bg, Height = Theme.P(50) };
            Body.Controls.Add(_footer);

            _edit = MkBtn("编辑", IconKind.Edit, 92, null, true, Theme.Primary);
            _edit.Click += (s, e) => EditAccount();
            _footer.Controls.Add(_edit);

            _reset = MkBtn("重置密码", IconKind.Key, 112, null, true, Theme.Amber);
            _reset.Click += (s, e) => ResetPassword();
            _footer.Controls.Add(_reset);

            _toggle = MkBtn("停用 / 启用", IconKind.Check, 122, null, true, Theme.TextSub);
            _toggle.Click += (s, e) => ToggleAccount();
            _footer.Controls.Add(_toggle);

            _del = MkBtn("删除", IconKind.Trash, 92, null, true, Theme.Red);
            _del.Click += (s, e) => DeleteAccount();
            _footer.Controls.Add(_del);
        }

        protected override void Layout3()
        {
            base.Layout3();
            int right = Tools.Width;
            if (_mypwd != null) { right -= _mypwd.Width; _mypwd.Location = new Point(right, Theme.P(9)); right -= Theme.P(10); }
            if (_add != null) { right -= _add.Width; _add.Location = new Point(right, Theme.P(9)); }

            _legend.SetBounds(0, 0, Math.Min(Body.Width, Theme.P(980)), Theme.P(110));
            int fy = Math.Max(Theme.P(146), Body.Height - _footer.Height);
            _list.SetBounds(0, Theme.P(122), Body.Width, Math.Max(Theme.P(80), fy - Theme.P(130)));
            _footer.SetBounds(0, fy, Body.Width, _footer.Height);

            int x = 0;
            foreach (Control c in _footer.Controls)
            {
                c.Location = new Point(x, Theme.P(8));
                x += c.Width + Theme.P(10);
            }
        }

        private Account Selected()
        {
            if (_list.SelectedIndices.Count == 0) return null;
            int i = _list.SelectedIndices[0];
            if (i < 0 || i >= App.Store.Accounts.Count) return null;
            return App.Store.Accounts[i];
        }

        public override void Reload()
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var a in App.Store.Accounts)
            {
                var it = new ListViewItem(a.User);
                it.SubItems.Add(a.Display);
                it.SubItems.Add(RoleText.Name(a.Role));
                it.SubItems.Add(a.Enabled ? "启用" : "已停用");
                string cls = "全部班级";
                if (a.ClassIds != null && a.ClassIds.Count > 0)
                {
                    var names = new List<string>();
                    foreach (var id in a.ClassIds) names.Add(App.ClassNameOf(id));
                    cls = string.Join("、", names.ToArray());
                }
                it.SubItems.Add(cls);
                it.SubItems.Add(a.LastLoginUtc.HasValue ? a.LastLoginUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "从未登录");
                it.SubItems.Add(a.Note);
                it.ForeColor = a.Role == Role.Admin ? Theme.Primary : (a.Role == Role.Member ? Theme.Green : Theme.TextSub);
                if (!a.Enabled) it.ForeColor = Theme.TextFaint;
                _list.Items.Add(it);
            }
            _list.EndUpdate();
            SetSub("共 " + App.Store.Accounts.Count + " 个账号  ·  访客无需账号即可只读进入  ·  成员只能加分，管理员拥有全部权限");
        }

        private void AddAccount()
        {
            var acc = new Account { Role = Role.Member };
            using (var d = new AccountEditDialog(App, acc, true))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                acc.User = d.UserName;
                acc.Display = Str.Blank(d.Display) ? d.UserName : d.Display;
                acc.Role = d.Role;
                acc.Enabled = d.IsEnabledChecked;
                acc.Note = d.NoteText;
                acc.ClassIds = d.ClassIds;
                Auth.SetPassword(acc, d.Password);
                App.Store.Accounts.Add(acc);
                App.Store.Save();
                Reload();
                Toast.Show(this, "账号已创建：" + acc.User, Theme.Green);
            }
        }

        private void EditAccount()
        {
            var acc = Selected();
            if (acc == null) { Toast.Show(this, "请先选择一个账号", Theme.Red); return; }

            using (var d = new AccountEditDialog(App, acc, false))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                acc.Display = Str.Blank(d.Display) ? acc.User : d.Display;
                acc.Role = d.Role;
                acc.Enabled = d.IsEnabledChecked;
                acc.Note = d.NoteText;
                acc.ClassIds = d.ClassIds;
                if (!string.IsNullOrEmpty(d.Password)) { Auth.SetPassword(acc, d.Password); acc.MustChangePwd = false; }
                App.Store.Save();
                Reload();
            }
        }

        private void ResetPassword()
        {
            var acc = Selected();
            if (acc == null) { Toast.Show(this, "请先选择一个账号", Theme.Red); return; }
            using (var d = new InputDialog("重置密码", "为账号 " + acc.User + " 设置新密码（至少 3 位）", "", true))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                if (d.Value.Length < 3) { Toast.Show(this, "密码至少 3 位", Theme.Red); return; }
                Auth.SetPassword(acc, d.Value);
                App.Store.Save();
                Toast.Show(this, "已重置 " + acc.User + " 的密码", Theme.Green);
            }
        }

        private void ToggleAccount()
        {
            var acc = Selected();
            if (acc == null) { Toast.Show(this, "请先选择一个账号", Theme.Red); return; }
            acc.Enabled = !acc.Enabled;
            App.Store.Save();
            Reload();
            Toast.Show(this, acc.User + (acc.Enabled ? " 已启用" : " 已停用"), acc.Enabled ? Theme.Green : Theme.Amber);
        }

        private void DeleteAccount()
        {
            var acc = Selected();
            if (acc == null) { Toast.Show(this, "请先选择一个账号", Theme.Red); return; }
            if (acc == App.Session.Account) { Toast.Show(this, "不能删除当前登录的账号", Theme.Red); return; }
            int admins = 0;
            foreach (var a in App.Store.Accounts) if (a.Role == Role.Admin) admins++;
            if (acc.Role == Role.Admin && admins <= 1) { Toast.Show(this, "至少需要保留一个管理员账号", Theme.Red); return; }

            if (MessageBox.Show(this, "确定删除账号 " + acc.User + " 吗？该操作不可恢复。", "Tensyan",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
            App.Store.Accounts.Remove(acc);
            App.Store.Save();
            Reload();
        }

        /// <summary>三种登录模式的说明条（Fluent 卡片）。</summary>
        private class RoleLegend : Control
        {
            public RoleLegend()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                BackColor = Theme.Bg;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(BackColor);
                var r = new Rectangle(0, 0, Width - 1, Height - 1);
                Theme.CardSurface(g, r, Theme.P(8));

                var roles = new[] { Role.Guest, Role.Member, Role.Admin };
                var colors = new[] { Theme.TextSub, Theme.Green, Theme.Primary };
                int colW = Math.Max(Theme.P(200), (r.Width - Theme.P(40)) / 3);

                for (int i = 0; i < 3; i++)
                {
                    int x = r.X + Theme.P(20) + i * colW;
                    int tile = Theme.P(28);
                    Theme.FillRounded(g, new Rectangle(x, r.Y + Theme.P(18), tile, tile), Theme.P(6), Theme.Mix(colors[i], Color.White, 0.88));
                    int iconSize = Theme.P(16);
                    Icons.Draw(g, new Rectangle(x + (tile - iconSize) / 2, r.Y + Theme.P(18) + (tile - iconSize) / 2, iconSize, iconSize),
                        roles[i] == Role.Admin ? IconKind.Accounts : (roles[i] == Role.Member ? IconKind.Add : IconKind.Lock), colors[i]);

                    TextRenderer.DrawText(g, RoleText.Name(roles[i]) + "模式", Theme.BodyStrong(),
                        new Rectangle(x + Theme.P(38), r.Y + Theme.P(15), colW - Theme.P(46), Theme.LineHeight(Theme.BodyStrong())), Theme.TextMain,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                    var desc = ShortDesc(roles[i]);
                    TextRenderer.DrawText(g, desc, Theme.Caption(),
                        new Rectangle(x + Theme.P(38), r.Y + Theme.P(37), colW - Theme.P(48), Theme.LineHeight(Theme.Caption())), Theme.TextSub,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }

                TextRenderer.DrawText(g, "提示：成员账号可限定只看到某些班级；登录U盘只能由管理员制作。", Theme.Caption(),
                    new Rectangle(r.X + Theme.P(20), r.Bottom - Theme.P(26), r.Width - Theme.P(40), Theme.LineHeight(Theme.Caption())), Theme.TextFaint,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }

            private static string ShortDesc(Role r)
            {
                switch (r)
                {
                    case Role.Admin: return "加减分、班级与学生、账号与U盘、导出";
                    case Role.Member: return "可以给学生加分，不能扣分与管理";
                    default: return "只读：可看积分、排行、记录与统计";
                }
            }
        }
    }
}
