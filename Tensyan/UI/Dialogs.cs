using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Tensyan.Core;

namespace Tensyan.UI
{
    /// <summary>对话框基类：Fluent 标题栏 + 内容区 + 底部按钮（显式布局，尺寸按 DPI 缩放）。</summary>
    public class DialogBase : ChromeForm
    {
        protected Panel Body;
        protected Panel Footer;
        protected RoundButton BtnOk;
        protected RoundButton BtnCancel;

        protected DialogBase(string title, int width, int height, bool needOk = true)
        {
            TitleText = title;
            AsDialog(width, height);
            BackColor = Theme.Card;
            TitleBar.Height = Theme.P(42);
            SetTitleBarColors(Theme.Card, Theme.TextMain, Theme.TextSub);

            Body = new Panel { BackColor = Theme.Card };
            Controls.Add(Body);

            Footer = new Panel { BackColor = Theme.Card, Height = Theme.P(64) };
            Body.Controls.Add(Footer);

            if (needOk)
            {
                BtnOk = new RoundButton
                {
                    Text = "确定",
                    Font = Theme.BodyStrong(),
                    Size = Theme.PS(104, 36),
                    Fill = Theme.Primary
                };
                BtnOk.Click += (s, e) => { if (OnOk()) { DialogResult = DialogResult.OK; Close(); } };
                Footer.Controls.Add(BtnOk);
            }
            BtnCancel = new RoundButton
            {
                Text = "取消",
                Size = Theme.PS(88, 36),
                Soft = true,
                Fill = Theme.Card,
                OutlineColor = Theme.BorderStrong
            };
            BtnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Footer.Controls.Add(BtnCancel);

            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
                else if (e.KeyCode == Keys.Enter && e.Control) { if (BtnOk != null) BtnOk.PerformClick2(); }
            };
        }

        protected virtual bool OnOk() { return true; }

        protected override void OnWindowShown()
        {
            PlayOpenAnimation();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Body == null) return;
            int top = TitleBar.Height;
            Body.SetBounds(0, top, ClientSize.Width, Math.Max(0, ClientSize.Height - top));
            Footer.SetBounds(0, Math.Max(0, Body.Height - Footer.Height), Body.Width, Footer.Height);
            int x = Body.Width - Theme.P(24);
            if (BtnCancel != null) { x -= BtnCancel.Width; BtnCancel.Location = new Point(x, (Footer.Height - BtnCancel.Height) / 2); }
            if (BtnOk != null) { x -= Theme.P(10); x -= BtnOk.Width; BtnOk.Location = new Point(x, (Footer.Height - BtnOk.Height) / 2); }
        }

        // ---------- 常用小控件工厂 ----------

        protected Label MkLabel(string text, int x, int y, float size = 10.5f, Color? color = null, FontStyle style = FontStyle.Regular, int width = 0)
        {
            return MkLabel(text, x, y, Theme.F(size, style), color, width);
        }

        protected Label MkLabel(string text, int x, int y, Font font, Color? color = null, int width = 0)
        {
            var l = new Label
            {
                Text = text,
                Font = font,
                ForeColor = color ?? Theme.TextMain,
                BackColor = Color.Transparent,
                Location = Theme.PP(x, y)
            };
            if (width > 0)
            {
                l.AutoSize = false;
                l.Width = Theme.P(width);
                l.Height = Theme.LineHeight(font);
            }
            else l.AutoSize = true;
            Body.Controls.Add(l);
            return l;
        }

        /// <summary>一行输入框（外层 Fluent 描边，返回内部 TextBox 以便调用方读写文本）。</summary>
        protected TextBox MkInput(int x, int y, int width, string text = "", string placeholder = "")
        {
            var wrap = new FluentInput { Location = Theme.PP(x, y), Size = Theme.PS(width, 36) };
            if (!string.IsNullOrEmpty(placeholder)) wrap.PlaceholderText = placeholder;
            wrap.Text = text ?? "";
            Body.Controls.Add(wrap);
            return wrap.Box;
        }
    }

    /// <summary>单行输入对话框。</summary>
    public class InputDialog : DialogBase
    {
        private readonly TextBox _box;
        public string Value { get { return _box.Text.Trim(); } }

        public InputDialog(string title, string label, string initial = "", bool password = false, string hint = "")
            : base(title, 460, hint.Length > 0 ? 240 : 200)
        {
            MkLabel(label, 24, 56, Theme.Body(), Theme.TextMain);
            _box = MkInput(24, 84, 412, initial);
            if (password) _box.UseSystemPasswordChar = true;
            if (hint.Length > 0) MkLabel(hint, 24, 128, Theme.Caption(), Theme.TextFaint);
            Shown += (s, e) => { _box.Focus(); _box.SelectAll(); };
        }

        protected override bool OnOk()
        {
            if (Str.Blank(_box.Text)) { Toast.Show(this, "内容不能为空", Theme.Red); return false; }
            return true;
        }
    }

    /// <summary>U盘口令输入。</summary>
    public class PinDialog : DialogBase
    {
        private readonly TextBox _box;
        public string Pin { get { return _box.Text; } }

        public PinDialog(string user, string display)
            : base("U盘登录口令", 460, 240)
        {
            MkLabel("请输入该登录U盘的口令", 24, 54, Theme.Subtitle(), Theme.TextMain);
            MkLabel("账号：" + user + "（" + display + "）", 24, 86, Theme.Caption(), Theme.TextSub);
            _box = MkInput(24, 112, 412);
            _box.UseSystemPasswordChar = true;
            Shown += (s, e) => _box.Focus();
        }

        protected override bool OnOk()
        {
            if (string.IsNullOrEmpty(_box.Text)) { Toast.Show(this, "口令不能为空", Theme.Red); return false; }
            return true;
        }
    }

    /// <summary>关于 / 使用说明。</summary>
    public class AboutDialog : DialogBase
    {
        public AboutDialog()
            : base("关于 Tensyan", 600, 680, false)
        {
            BtnCancel.Text = "关闭";

            int logoW = 210;
            int logoH = Brand.LogoHeight(logoW);
            if (logoH <= 0) logoH = 44;

            var logoBox = new Panel { Location = Theme.PP(28, 42), Size = Theme.PS(logoW, logoH), BackColor = Color.Transparent };
            logoBox.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                if (Brand.DrawLogo(e.Graphics, 0, 0, logoBox.Width) == 0)
                    Brand.DrawTile(e.Graphics, new Rectangle(0, 0, Theme.P(40), Theme.P(40)));
            };
            Body.Controls.Add(logoBox);

            int ty = 42 + logoH + 14;
            MkLabel("Tensyan 班级积分工具", 28, ty, Theme.Title(), Theme.TextMain);
            MkLabel("版本 1.1.0 · Fluent 界面 · 参考希沃班级优化大师的加分体验", 28, ty + 34, Theme.Caption(), Theme.TextSub);

            int boxY = ty + 64;
            var box = new Panel { Location = Theme.PP(28, boxY), Size = Theme.PS(544, 272), BackColor = Theme.Bg };
            box.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Theme.Bg);
                Theme.StrokeRounded(g, new Rectangle(0, 0, box.Width - 1, box.Height - 1), Theme.P(8), Theme.Border);
            };
            Body.Controls.Add(box);

            string txt =
                "三种登录模式\r\n" +
                "  访客只读：看积分、排行、记录与统计，不能修改\r\n" +
                "  成员仅加分：可以给学生加分，不能扣分与班级管理\r\n" +
                "  管理员全部：加减分、班级与学生、账号与U盘、导出\r\n" +
                "注册与账号\r\n" +
                "  首次使用会引导创建管理员；登录页可直接注册成员账号\r\n" +
                "  管理员可改角色、重置密码、停用账号\r\n" +
                "登录U盘\r\n" +
                "  管理员可制作U盘登录钥匙（可设口令与有效期）\r\n" +
                "  插入U盘后在登录界面点“U盘登录”即可免密进入\r\n" +
                "数据位置\r\n" +
                "  " + Paths.DataDir;

            var lbl = new Label
            {
                Text = txt,
                Font = Theme.Caption(),
                ForeColor = Theme.TextMain,
                Location = Theme.PP(18, 14),
                AutoSize = false,
                Size = new Size(box.Width - Theme.P(36), box.Height - Theme.P(28))
            };
            box.Controls.Add(lbl);

            // 署名
            MkLabel("Clarusvita Studio", 28, boxY + 288, Theme.BodyStrong(), Theme.TextMain);
            MkLabel("作者：furliflix", 28, boxY + 310, Theme.Caption(), Theme.TextSub);
            MkLabel("显示缩放：" + Math.Round(Theme.Scale * 100) + "%　界面字体：" + Theme.Family + "　DPI 感知：" + (Theme.Scale > 1.01f || true ? "已启用" : "否"), 28, boxY + 332, Theme.Caption(), Theme.TextFaint);
        }
    }
}
