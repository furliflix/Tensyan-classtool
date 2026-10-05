using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Tensyan.Core;

namespace Tensyan.UI.Pages
{
    /// <summary>登录U盘：检测U盘、制作 / 校验 / 删除登录密钥。</summary>
    public class PageUsb : PageBase
    {
        private ListView _list;
        private RoundButton _refresh, _verify, _delete, _write;
        private FluentSelect _accounts, _valid, _drives;
        private FluentInput _pin, _pin2;
        private Label _readme, _status, _result;
        private Panel _left, _right;

        public PageUsb(App app) : base(app)
        {
            TitleText = "登录U盘";
            EnableTools(52);

            _refresh = MkBtn("重新检测U盘", IconKind.Refresh, 132, null, true, Theme.Primary);
            _refresh.Click += (s, e) => { Reload(); Toast.Show(this, "已重新检测磁盘", Theme.Primary); };
            Tools.Controls.Add(_refresh);

            _verify = MkBtn("校验选中U盘", IconKind.Check, 138, null, true, Theme.TextSub);
            _verify.Click += (s, e) => VerifySelected();
            Tools.Controls.Add(_verify);

            _delete = MkBtn("删除密钥", IconKind.Trash, 108, null, true, Theme.Red);
            _delete.Click += (s, e) => DeleteSelected();
            Tools.Controls.Add(_delete);

            BuildLeft();
            BuildRight();
        }

        private void BuildLeft()
        {
            _left = new Panel { BackColor = Theme.Bg };
            Body.Controls.Add(_left);

            _list = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                Font = Theme.Caption(),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Theme.Card,
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };
            _list.Columns.Add("盘符", Theme.P(60));
            _list.Columns.Add("卷标", Theme.P(120));
            _list.Columns.Add("类型", Theme.P(90));
            _list.Columns.Add("容量 / 可用", Theme.P(150));
            _list.Columns.Add("登录密钥", Theme.P(90));
            _left.Controls.Add(_list);

            _status = new Label
            {
                Font = Theme.Caption(),
                ForeColor = Theme.TextSub,
                AutoSize = false,
                BackColor = Color.Transparent
            };
            _left.Controls.Add(_status);
        }

        private void BuildRight()
        {
            _right = new Panel { BackColor = Theme.Bg };
            _right.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Theme.Bg);
                var r = new Rectangle(0, 0, _right.Width - 1, _right.Height - 1);
                Theme.CardSurface(g, r, Theme.P(8));

                TextRenderer.DrawText(g, "制作登录U盘", Theme.Subtitle(),
                    new Rectangle(Theme.P(22), Theme.P(16), _right.Width - Theme.P(44), Theme.P(24)), Theme.TextMain,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(g, "把某个账号写入U盘作为免密登录钥匙。", Theme.Caption(),
                    new Rectangle(Theme.P(22), Theme.P(42), _right.Width - Theme.P(44), Theme.P(18)), Theme.TextSub,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            };
            Body.Controls.Add(_right);

            int y = 76;
            AddLabel("① 选择U盘", 22, y, true); y += 24;
            _drives = new FluentSelect { Location = Theme.PP(22, y), Size = Theme.PS(300, 34) };
            _right.Controls.Add(_drives);

            var rescan = new RoundButton
            {
                Text = "刷新",
                Icon = IconKind.Refresh,
                Size = Theme.PS(80, 34),
                Soft = true,
                Fill = Theme.Card,
                OutlineColor = Theme.BorderStrong,
                Location = Theme.PP(330, y)
            };
            rescan.Click += (s, e) => Reload();
            _right.Controls.Add(rescan);
            y += 44;

            AddLabel("② 绑定账号", 22, y, true); y += 24;
            _accounts = new FluentSelect { Location = Theme.PP(22, y), Size = Theme.PS(300, 34) };
            _right.Controls.Add(_accounts);
            y += 44;

            AddLabel("③ U盘口令（可空，设置后插入U盘仍需输入口令）", 22, y, true); y += 24;
            _pin = new FluentInput { Location = Theme.PP(22, y), Size = Theme.PS(146, 34), UseSystemPasswordChar = true, PlaceholderText = "口令" };
            _right.Controls.Add(_pin);
            _pin2 = new FluentInput { Location = Theme.PP(178, y), Size = Theme.PS(146, 34), UseSystemPasswordChar = true, PlaceholderText = "再次输入" };
            _right.Controls.Add(_pin2);
            y += 44;

            AddLabel("④ 有效期", 22, y, true); y += 24;
            _valid = new FluentSelect { Location = Theme.PP(22, y), Size = Theme.PS(300, 34) };
            _valid.AddItems("永久有效", "7 天", "30 天", "90 天", "180 天（一学期）");
            _valid.SelectedIndex = 0;
            _right.Controls.Add(_valid);
            y += 46;

            _write = new RoundButton
            {
                Text = "写入登录密钥",
                Icon = IconKind.Usb,
                Font = Theme.BodyStrong(),
                Size = Theme.PS(180, 40),
                Fill = Theme.Primary,
                Location = Theme.PP(22, y)
            };
            _write.Click += (s, e) => WriteKey();
            _right.Controls.Add(_write);
            y += 52;

            _result = new Label
            {
                Font = Theme.Caption(),
                ForeColor = Theme.TextMain,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = Theme.PP(22, y),
                Size = Theme.PS(340, 40)
            };
            _right.Controls.Add(_result);
            y += 46;

            _readme = new Label
            {
                Font = Theme.Caption(),
                ForeColor = Theme.TextFaint,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = Theme.PP(22, y),
                Size = Theme.PS(340, 110),
                Text = "说明\r\n· U盘根目录会生成隐藏文件 TensyanKey.tky，插上该U盘后，在登录界面点“U盘登录”即可免密进入。\r\n" +
                       "· 密钥带 HMAC 签名，复制或改动文件都会失效。\r\n" +
                       "· 密钥与这份 Tensyan 数据配套；U盘只保存登录信息，积分数据在本机。"
            };
            _right.Controls.Add(_readme);
        }

        private void AddLabel(string text, int x, int y, bool bold)
        {
            _right.Controls.Add(new Label
            {
                Text = text,
                Font = bold ? Theme.BodyStrong() : Theme.Caption(),
                ForeColor = bold ? Theme.TextMain : Theme.TextSub,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = Theme.PP(x, y)
            });
        }

        protected override void Layout3()
        {
            base.Layout3();
            int x = 0;
            if (_refresh != null) { _refresh.Location = new Point(x, Theme.P(9)); x += _refresh.Width + Theme.P(10); }
            if (_verify != null) { _verify.Location = new Point(x, Theme.P(9)); x += _verify.Width + Theme.P(10); }
            if (_delete != null) { _delete.Location = new Point(x, Theme.P(9)); }

            if (_left == null) return;
            int leftW = Math.Min(Theme.P(560), Math.Max(Theme.P(360), Body.Width * 45 / 100));
            _left.SetBounds(0, 0, leftW, Body.Height);
            _right.SetBounds(leftW + Theme.P(16), 0, Math.Max(Theme.P(320), Body.Width - leftW - Theme.P(16)), Body.Height);

            _list.SetBounds(0, 0, _left.Width, Math.Max(Theme.P(120), _left.Height - Theme.P(70)));
            _status.SetBounds(0, _left.Height - Theme.P(58), _left.Width, Theme.P(54));
        }

        public override void Reload()
        {
            var drives = OrderedDrives();

            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var d in drives)
            {
                var it = new ListViewItem(d.Letter);
                it.SubItems.Add(Str.Blank(d.Label) ? "（无卷标）" : d.Label);
                it.SubItems.Add(d.DriveType);
                it.SubItems.Add(d.SizeText());
                it.SubItems.Add(d.HasKey ? "已有密钥" : "无");
                it.ForeColor = d.HasKey ? Theme.Green : Theme.TextMain;
                _list.Items.Add(it);
            }
            _list.EndUpdate();

            _drives.ClearItems();
            foreach (var d in drives) _drives.Items.Add(d.Describe() + (d.HasKey ? "  [已有密钥]" : ""));
            if (_drives.Items.Count > 0) _drives.SelectedIndex = 0;

            _accounts.ClearItems();
            foreach (var a in App.Store.Accounts)
                _accounts.Items.Add(a.User + "（" + RoleText.Name(a.Role) + "）" + (a.Enabled ? "" : " - 已停用"));
            if (_accounts.Items.Count > 0) _accounts.SelectedIndex = 0;

            int rem = 0;
            foreach (var d in drives) if (d.DriveType == "可移动磁盘") rem++;
            _status.Text = drives.Count == 0
                ? "未检测到任何可写磁盘。请插入U盘后点“重新检测U盘”。"
                : "共检测到 " + drives.Count + " 个磁盘，其中可移动磁盘 " + rem + " 个。选中一行后可校验或删除其中的登录密钥。";
            SetSub("三种身份都能用U盘登录  ·  密钥只能由管理员制作  ·  这里是管理U盘钥匙的地方");
        }

        private List<UsbDrive> OrderedDrives()
        {
            var all = UsbKey.Drives(false);
            var drives = new List<UsbDrive>();
            foreach (var d in all) if (d.DriveType == "可移动磁盘") drives.Add(d);
            foreach (var d in all) if (d.DriveType != "可移动磁盘") drives.Add(d);
            return drives;
        }

        private UsbDrive SelectedDrive()
        {
            var drives = OrderedDrives();
            if (_list.SelectedIndices.Count == 0) return null;
            int i = _list.SelectedIndices[0];
            if (i < 0 || i >= drives.Count) return null;
            return drives[i];
        }

        private void WriteKey()
        {
            var drives = OrderedDrives();
            int idx = _drives.SelectedIndex >= 0 ? _drives.SelectedIndex : _list.SelectedIndices.Count > 0 ? _list.SelectedIndices[0] : -1;
            if (idx < 0 || idx >= drives.Count) { Toast.Show(this, "请先选择目标U盘", Theme.Red); return; }
            if (_accounts.SelectedIndex < 0 || _accounts.SelectedIndex >= App.Store.Accounts.Count)
            {
                Toast.Show(this, "请选择要绑定的账号", Theme.Red);
                return;
            }
            if (_pin.Text != _pin2.Text) { Toast.Show(this, "两次输入的口令不一致", Theme.Red); return; }

            var drive = drives[idx];
            var acc = App.Store.Accounts[_accounts.SelectedIndex];
            int days = _valid.SelectedIndex == 0 ? 0 : (_valid.SelectedIndex == 1 ? 7 : (_valid.SelectedIndex == 2 ? 30 : (_valid.SelectedIndex == 3 ? 90 : 180)));

            var key = UsbKey.Build(App.Store, acc, _pin.Text, days);
            string path = UsbKey.Write(drive.Root, key);
            if (path == null)
            {
                _result.ForeColor = Theme.Red;
                _result.Text = "写入失败：U盘可能被写保护或空间不足";
                Toast.Show(this, "写入失败", Theme.Red);
                return;
            }
            _pin.Text = ""; _pin2.Text = "";
            _result.ForeColor = Theme.Green;
            _result.Text = "制作成功：" + acc.User + " @ " + drive.Letter + (string.IsNullOrEmpty(key.PinHash) ? "" : "（需口令）");
            Toast.Show(this, "登录U盘制作成功：" + acc.User + " @ " + drive.Letter, Theme.Green);
            Reload();
        }

        private void VerifySelected()
        {
            var drive = SelectedDrive();
            if (drive == null) { Toast.Show(this, "请先在列表中选择一个U盘", Theme.Red); return; }
            if (!drive.HasKey) { Toast.Show(this, "该U盘中没有 TensyanKey.tky", Theme.Red); return; }

            UsbKeyFile kf;
            var check = UsbKey.ReadAndCheck(drive.KeyPath, App.Store, out kf);
            if (kf == null)
            {
                MessageBox.Show(this, KeyCheckText.Msg(check), "U盘校验", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string info = "校验结果：" + KeyCheckText.Msg(check) + "\r\n\r\n"
                + "账号：" + kf.User + "（" + kf.Display + "）\r\n"
                + "角色：" + kf.Role + "\r\n"
                + "制作时间：" + kf.CreateTime().ToString("yyyy-MM-dd HH:mm") + "\r\n"
                + "有效期：" + (kf.ExpireTime().HasValue ? kf.ExpireTime().Value.ToString("yyyy-MM-dd HH:mm") : "永久") + "\r\n"
                + "口令：" + (kf.HasPin ? "需要" : "无");
            MessageBox.Show(this, info, "U盘校验", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void DeleteSelected()
        {
            var drive = SelectedDrive();
            if (drive == null) { Toast.Show(this, "请先在列表中选择一个U盘", Theme.Red); return; }
            if (!drive.HasKey) { Toast.Show(this, "该U盘中没有登录密钥", Theme.Red); return; }
            if (MessageBox.Show(this, "确定删除 " + drive.Letter + " 上的登录密钥吗？删除后该U盘不能再用于登录。", "Tensyan",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
            if (UsbKey.DeleteKey(drive.Root)) { Toast.Show(this, "已删除登录密钥", Theme.Green); Reload(); }
            else Toast.Show(this, "删除失败，请检查U盘是否被写保护", Theme.Red);
        }
    }
}
