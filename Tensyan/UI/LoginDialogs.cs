using System;
using System.Drawing;
using System.Windows.Forms;
using Tensyan.Core;

namespace Tensyan.UI
{
    /// <summary>首次使用：创建管理员账号（注册向导，不需要U盘）。</summary>
    public class FirstRunDialog : DialogBase
    {
        private readonly App _app;
        private readonly FluentInput _school, _user, _pwd, _pwd2;
        public Account CreatedAccount;

        public FirstRunDialog(App app)
            : base("首次使用 · 创建管理员账号", 560, 560)
        {
            _app = app;
            BtnOk.Text = "创建并进入";
            BtnOk.Width = Theme.P(130);

            MkLabel("欢迎使用 Tensyan", 28, 54, Theme.Title(), Theme.TextMain);
            MkLabel("本机还没有管理员账号，先创建一个管理员（拥有全部权限）。", 28, 92, Theme.Body(), Theme.TextSub);

            _school = Field("学校 / 班级抬头（可空）", 26, 142, "例如 阳光小学");
            _user = Field("管理员用户名", 26, 212, "例如 admin");
            _pwd = Field("密码（至少 3 位）", 26, 282, "", true);
            _pwd2 = Field("确认密码", 26, 352, "", true);

            var tip = new Label
            {
                Text = "提示：本步骤不需要U盘。管理员可以加减分、管理班级与学生、创建成员账号、制作登录U盘。\r\n创建后可在“账号权限”里继续添加账号；别人注册成员账号时才需要管理员U盘认证。",
                Font = Theme.Caption(),
                ForeColor = Theme.TextFaint,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = Theme.PP(28, 424),
                Size = Theme.PS(504, 44)
            };
            Body.Controls.Add(tip);

            Shown += (s, e) => _user.Box.Focus();
        }

        private FluentInput Field(string label, int x, int y, string placeholder, bool password = false)
        {
            Body.Controls.Add(new Label
            {
                Text = label,
                Font = Theme.Caption(),
                ForeColor = Theme.TextSub,
                Location = Theme.PP(x, y),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            var input = new FluentInput { Location = Theme.PP(x, y + 20), Size = Theme.PS(504, 36) };
            if (password) input.UseSystemPasswordChar = true;
            if (!string.IsNullOrEmpty(placeholder)) input.PlaceholderText = placeholder;
            Body.Controls.Add(input);
            return input;
        }

        protected override bool OnOk()
        {
            string u = _user.Text.Trim();
            if (u.Length < 2) { Toast.Show(this, "用户名至少 2 个字符", Theme.Red); return false; }
            foreach (char c in u)
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.')) { Toast.Show(this, "用户名只能使用字母、数字、下划线", Theme.Red); return false; }
            foreach (var a in _app.Store.Accounts)
                if (string.Equals(a.User, u, StringComparison.OrdinalIgnoreCase)) { Toast.Show(this, "该用户名已存在", Theme.Red); return false; }
            if (_pwd.Text.Length < 3) { Toast.Show(this, "密码至少 3 位", Theme.Red); return false; }
            if (_pwd.Text != _pwd2.Text) { Toast.Show(this, "两次输入的密码不一致", Theme.Red); return false; }

            var acc = new Account
            {
                User = u,
                Display = "管理员",
                Role = Role.Admin,
                Enabled = true,
                Note = "首次使用创建的管理员账号（无需U盘）"
            };
            Auth.SetPassword(acc, _pwd.Text);
            _app.Store.Accounts.Add(acc);
            if (!Str.Blank(_school.Text)) _app.Settings.SchoolName = _school.Text.Trim();
            _app.Store.Save();
            CreatedAccount = acc;
            return true;
        }
    }

    /// <summary>管理员二次验证（制作U盘、敏感操作前使用）。</summary>
    public class ReAuthDialog : DialogBase
    {
        private readonly App _app;
        private readonly FluentInput _user, _pwd;

        public ReAuthDialog(App app, string subtitle)
            : base("管理员验证", 460, 300)
        {
            _app = app;
            MkLabel(subtitle, 24, 54, Theme.Body(), Theme.TextSub);

            MkLabel("管理员用户名", 24, 92, Theme.Caption(), Theme.TextSub);
            _user = new FluentInput { Location = Theme.PP(24, 114), Size = Theme.PS(412, 36), Text = "admin" };
            Body.Controls.Add(_user);

            MkLabel("管理员密码", 24, 162, Theme.Caption(), Theme.TextSub);
            _pwd = new FluentInput { Location = Theme.PP(24, 184), Size = Theme.PS(412, 36), UseSystemPasswordChar = true };
            Body.Controls.Add(_pwd);

            Shown += (s, e) => _pwd.Box.Focus();
        }

        protected override bool OnOk()
        {
            Account acc = null;
            foreach (var a in _app.Store.Accounts)
                if (string.Equals(a.User, _user.Text.Trim(), StringComparison.OrdinalIgnoreCase)) { acc = a; break; }

            if (acc == null || acc.Role != Role.Admin || !acc.Enabled || !Auth.Verify(acc, _pwd.Text))
            {
                Toast.Show(this, "管理员验证失败", Theme.Red);
                return false;
            }
            return true;
        }
    }
}
