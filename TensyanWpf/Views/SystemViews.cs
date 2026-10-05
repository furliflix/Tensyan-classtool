using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Tensyan.Core;

namespace Tensyan.Wpf.Views
{
    /// <summary>局域网服务的常驻持有者：切页面不中断配对服务，退出程序才停。</summary>
    public static class PairingRuntime
    {
        public static PairingStore Pairing;
        public static LanService Lan;
        private static readonly List<string> _log = new List<string>();
        public static event Action<string> Logged;

        public static void EnsureStarted()
        {
            try
            {
                if (Lan != null && Lan.Running) return;
                if (Pairing == null) Pairing = new PairingStore();
                if (string.IsNullOrEmpty(Pairing.Code)) Pairing.NewCode();
                Pairing.Save();   // 让 pairing.json 立刻落盘，方便排查与复用
                Lan = new LanService(App.Core.Store, Pairing);
                Lan.Log += line =>
                {
                    lock (_log)
                    {
                        _log.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + line);
                        if (_log.Count > 400) _log.RemoveAt(0);
                    }
                    try { Paths.Log("[配对] " + line); } catch { }
                    var h = Logged;
                    if (h != null) h(line);
                };
                Lan.Start();
                Paths.Log("配对服务已启动：" + LanService.LocalIp() + "  TCP " + LanService.TcpPort + " / UDP " + LanService.UdpPort + "  配对码=" + Pairing.Code);
            }
            catch (Exception ex) { Paths.Log("配对服务启动失败：" + ex.Message); }
        }

        public static string[] Snapshot()
        {
            lock (_log) return _log.ToArray();
        }

        public static void Shutdown()
        {
            try { if (Lan != null) Lan.Stop(); } catch { }
        }
    }

    // ============================================================
    //  账号权限
    // ============================================================
    public class AccountsView : UserControl
    {
        private static Tensyan.Core.App Core { get { return App.Core; } }
        private readonly StackPanel _body = new StackPanel();

        public AccountsView()
        {
            Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _body };
            Loaded += (s, e) => Reload();
        }

        public void Reload()
        {
            _body.Children.Clear();
            _body.Children.Add(UiKit.Title("账号权限"));
            _body.Children.Add(UiKit.Sub("管理员拥有全部权限；成员默认只能加分（扣分需管理员）。"));

            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            if (Core.Session.IsAdmin) bar.Children.Add(UiKit.Primary("新增账号", AddAccount, 0xF10A));
            bar.Children.Add(UiKit.Subtle("刷新", Reload));
            _body.Children.Add(bar);

            if (Core.Store.Accounts.Count == 0)
            {
                _body.Children.Add(UiKit.Faint("还没有账号。点「新增账号」创建。"));
                return;
            }

            foreach (var acc in Core.Store.Accounts.ToList())
            {
                var a = acc;
                var sp = new StackPanel();
                var head = new StackPanel { Orientation = Orientation.Horizontal };
                head.Children.Add(UiKit.Icon(a.Role == Role.Admin ? 0xF5C1 : 0xF5B9, 18, UiKit.Res(a.Enabled ? "Accent" : "TextFaint")));
                head.Children.Add(new TextBlock
                {
                    Text = (string.IsNullOrEmpty(a.Display) ? a.User : a.Display) + "　（" + a.User + "）",
                    FontFamily = UiKit.UiFont, FontSize = 15, FontWeight = FontWeights.SemiBold,
                    Foreground = UiKit.Res("TextMain"), VerticalAlignment = VerticalAlignment.Center
                });
                sp.Children.Add(head);
                sp.Children.Add(new TextBlock
                {
                    Text = "角色：" + RoleText.Name(a.Role)
                        + "　状态：" + (a.Enabled ? "启用" : "停用")
                        + "　可见班级：" + (a.ClassIds == null || a.ClassIds.Count == 0 ? "全部" : a.ClassIds.Count + " 个")
                        + "　上次登录：" + (a.LastLoginUtc.HasValue ? a.LastLoginUtc.Value.ToLocalTime().ToString("MM-dd HH:mm") : "从未"),
                    FontFamily = UiKit.UiFont, FontSize = 12.5, Foreground = UiKit.Res("TextSub"), Margin = new Thickness(0, 4, 0, 8)
                });
                if (Core.Session.IsAdmin)
                {
                    var btns = new StackPanel { Orientation = Orientation.Horizontal };
                    btns.Children.Add(UiKit.Subtle("重置密码", () => ResetPwd(a)));
                    btns.Children.Add(UiKit.Subtle(a.Enabled ? "停用" : "启用", () => { a.Enabled = !a.Enabled; Core.Raise(); Core.Store.Save(); Reload(); }));
                    btns.Children.Add(UiKit.Subtle("切换角色", () =>
                    {
                        a.Role = a.Role == Role.Admin ? Role.Member : Role.Admin;
                        Core.Raise(); Core.Store.Save(); Reload();
                    }));
                    btns.Children.Add(UiKit.Subtle("删除", () =>
                    {
                        if (!SimpleDialogs.Confirm("删除账号", "确定删除账号 " + a.User + " 吗？", true)) return;
                        Core.Store.Accounts.Remove(a); Core.Raise(); Core.Store.Save(); Reload();
                    }));
                    sp.Children.Add(btns);
                }
                _body.Children.Add(UiKit.Card(sp));
            }
        }

        private void AddAccount()
        {
            var r = SimpleDialogs.Prompt("新增账号", "创建一个登录账号：",
                new[] { "账号（登录名）", "显示名", "密码", "角色（管理员 / 成员）" },
                new[] { "", "", "", "成员" });
            if (r == null) return;
            if (string.IsNullOrWhiteSpace(r[0]) || string.IsNullOrWhiteSpace(r[2]))
            {
                SimpleDialogs.Info("新增账号", new[] { "账号与密码不能为空。" });
                return;
            }
            if (Core.Store.Accounts.Any(a => string.Equals(a.User, r[0], StringComparison.OrdinalIgnoreCase)))
            {
                SimpleDialogs.Info("新增账号", new[] { "这个账号已存在。" });
                return;
            }
            var acc = new Account
            {
                User = r[0].Trim(),
                Display = string.IsNullOrWhiteSpace(r[1]) ? r[0].Trim() : r[1].Trim(),
                Role = r[3].Contains("管理") ? Role.Admin : Role.Member
            };
            Auth.SetPassword(acc, r[2]);
            Core.Store.Accounts.Add(acc);
            Core.Raise();
            Core.Store.Save();
            SimpleDialogs.Info("新增账号", new[] { "已创建账号：" + acc.User + "（" + RoleText.Name(acc.Role) + "）" });
            Reload();
        }

        private void ResetPwd(Account a)
        {
            var r = SimpleDialogs.Prompt("重置密码", "给 " + a.User + " 设置新密码：", new[] { "新密码" }, new[] { "" });
            if (r == null) return;
            if (string.IsNullOrWhiteSpace(r[0])) { SimpleDialogs.Info("重置密码", new[] { "密码不能为空。" }); return; }
            Auth.SetPassword(a, r[0]);
            Core.Raise();
            Core.Store.Save();
            SimpleDialogs.Info("重置密码", new[] { "已重置：" + a.User });
        }
    }

    // ============================================================
    //  登录U盘
    // ============================================================
    public class UsbView : UserControl
    {
        private static Tensyan.Core.App Core { get { return App.Core; } }
        private readonly StackPanel _body = new StackPanel();

        public UsbView()
        {
            Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _body };
            Loaded += (s, e) => Reload();
        }

        public void Reload()
        {
            _body.Children.Clear();
            _body.Children.Add(UiKit.Title("登录U盘"));
            _body.Children.Add(UiKit.Sub("把某个账号的密钥写入 U 盘：插入 U 盘后即可免密码登录（可选 PIN 与有效期）。"));

            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            bar.Children.Add(UiKit.Primary("刷新盘符", Reload, 0xF191));
            _body.Children.Add(bar);

            List<UsbDrive> drives;
            try { drives = UsbKey.Drives(true); }
            catch (Exception ex) { _body.Children.Add(UiKit.Faint("读取驱动器失败：" + ex.Message)); return; }

            if (drives.Count == 0)
            {
                _body.Children.Add(UiKit.Faint("没有检测到可移动磁盘。请插入 U 盘后点「刷新盘符」。"));
                return;
            }

            foreach (var d in drives)
            {
                var drive = d;
                var sp = new StackPanel();
                var head = new StackPanel { Orientation = Orientation.Horizontal };
                head.Children.Add(UiKit.Icon(0xEDD0, 18, UiKit.Res("Accent")));
                head.Children.Add(new TextBlock
                {
                    Text = drive.Letter + "\\　" + (string.IsNullOrEmpty(drive.Label) ? "（无卷标）" : drive.Label),
                    FontFamily = UiKit.UiFont, FontSize = 15, FontWeight = FontWeights.SemiBold,
                    Foreground = UiKit.Res("TextMain"), VerticalAlignment = VerticalAlignment.Center
                });
                if (drive.HasKey)
                {
                    head.Children.Add(new TextBlock
                    {
                        Text = "　已写入密钥",
                        FontFamily = UiKit.UiFont, FontSize = 12.5, Foreground = UiKit.Res("Green"), VerticalAlignment = VerticalAlignment.Center
                    });
                }
                sp.Children.Add(head);
                sp.Children.Add(new TextBlock
                {
                    Text = "可用 " + UsbDrive.FormatSize(drive.FreeBytes) + " / 共 " + UsbDrive.FormatSize(drive.TotalBytes),
                    FontFamily = UiKit.UiFont, FontSize = 12.5, Foreground = UiKit.Res("TextSub"), Margin = new Thickness(0, 4, 0, 8)
                });

                var btns = new StackPanel { Orientation = Orientation.Horizontal };
                btns.Children.Add(UiKit.Primary("写入密钥", () => WriteKey(drive), 0xF4B7));
                btns.Children.Add(UiKit.Subtle("检测密钥", () => CheckKey(drive)));
                if (drive.HasKey)
                    btns.Children.Add(UiKit.Subtle("删除密钥", () =>
                    {
                        if (!SimpleDialogs.Confirm("删除密钥", "确定删除 " + drive.Letter + "\\ 上的 Tensyan 密钥吗？", true)) return;
                        string err;
                        if (UsbKey.DeleteKey(drive.Root)) SimpleDialogs.Info("删除密钥", new[] { "已删除。" });
                        Reload();
                    }));
                sp.Children.Add(btns);
                _body.Children.Add(UiKit.Card(sp));
            }
            _body.Children.Add(UiKit.Faint("提示：密钥文件只包含账号标识与签名，不含明文密码。"));
        }

        private void WriteKey(UsbDrive drive)
        {
            var users = Core.Store.Accounts.Select(a => a.User).ToList();
            if (users.Count == 0) { SimpleDialogs.Info("写入密钥", new[] { "还没有账号，请先到「账号权限」创建。" }); return; }

            var r = SimpleDialogs.Prompt("写入登录密钥",
                "写入到 " + drive.Letter + "\\\n可用账号：" + string.Join("、", users),
                new[] { "账号", "PIN（可留空）", "有效天数（0 = 永久）" },
                new[] { users[0], "", "0" });
            if (r == null) return;

            var acc = UsbKey.FindAccount(Core.Store, r[0].Trim());
            if (acc == null) { SimpleDialogs.Info("写入密钥", new[] { "找不到账号：" + r[0] }); return; }

            int days = 0;
            int.TryParse(r[2], out days);
            try
            {
                var kf = UsbKey.Build(Core.Store, acc, r[1], days);
                string path = UsbKey.Write(drive.Root, kf);
                SimpleDialogs.Info("写入密钥", new[]
                {
                    "已写入：" + path,
                    "账号：" + acc.User + "（" + RoleText.Name(acc.Role) + "）",
                    days > 0 ? ("有效期：" + days + " 天") : "有效期：永久",
                    string.IsNullOrEmpty(r[1]) ? "PIN：未设置" : "PIN：已设置"
                });
                Reload();
            }
            catch (Exception ex) { SimpleDialogs.Info("写入失败", new[] { ex.Message }); }
        }

        private void CheckKey(UsbDrive drive)
        {
            try
            {
                UsbKeyFile kf;
                var check = UsbKey.ReadAndCheck(drive.KeyPath, Core.Store, out kf);
                var lines = new List<string> { "检测结果：" + KeyCheckText.Msg(check) };
                if (kf != null)
                {
                    lines.Add("账号：" + kf.User);
                    lines.Add("签发时间：" + kf.Created);
                    lines.Add("有效期至：" + (string.IsNullOrEmpty(kf.Expires) ? "永久" : kf.Expires));
                    lines.Add("需要 PIN：" + (string.IsNullOrEmpty(kf.PinHash) ? "否" : "是"));
                }
                SimpleDialogs.Info("检测密钥", lines.ToArray());
            }
            catch (Exception ex) { SimpleDialogs.Info("检测密钥", new[] { "检测失败：" + ex.Message }); }
        }
    }

    // ============================================================
    //  配对手机
    // ============================================================
    public class PairingView : UserControl
    {
        private static Tensyan.Core.App Core { get { return App.Core; } }
        private readonly StackPanel _body = new StackPanel();
        private readonly DispatcherTimer _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private TextBlock _codeText;
        private TextBlock _countdown;

        public PairingView()
        {
            Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _body };
            Loaded += (s, e) => { PairingRuntime.EnsureStarted(); Reload(); _timer.Start(); };
            Unloaded += (s, e) => _timer.Stop();
            _timer.Tick += (s, e) => { UpdateCode(); };
        }

        private void UpdateCode()
        {
            try
            {
                if (_codeText != null && PairingRuntime.Pairing != null)
                    _codeText.Text = PairingRuntime.Pairing.Code;
                if (_countdown != null && PairingRuntime.Pairing != null)
                {
                    int left = PairingRuntime.Pairing.CodeSecondsLeft;
                    _countdown.Text = left > 0 ? ("还剩 " + left + " 秒有效") : "已过期，点「换一个码」重新生成";
                }
            }
            catch { }
        }

        public void Reload()
        {
            _body.Children.Clear();
            _body.Children.Add(UiKit.Title("配对手机"));
            _body.Children.Add(UiKit.Sub("手机与电脑连同一个 Wi-Fi，在手机上输入下面这 6 位数字即可配对；配对后手机自动登录，无需再输账号密码。"));

            // ---- 配对码 ----
            var codePanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 8) };
            _codeText = new TextBlock
            {
                Text = PairingRuntime.Pairing != null ? PairingRuntime.Pairing.Code : "------",
                FontFamily = UiKit.UiFont,
                FontSize = 54,
                FontWeight = FontWeights.Bold,
                Foreground = UiKit.Res("Accent"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _countdown = new TextBlock
            {
                Text = "",
                FontFamily = UiKit.UiFont,
                FontSize = 13,
                Foreground = UiKit.Res("TextSub"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0)
            };
            codePanel.Children.Add(_codeText);
            codePanel.Children.Add(_countdown);
            var codeBtns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 0) };
            codeBtns.Children.Add(UiKit.Primary("换一个码", () =>
            {
                if (PairingRuntime.Pairing != null) PairingRuntime.Pairing.NewCode();
                UpdateCode();
            }, 0xF191));
            codePanel.Children.Add(codeBtns);
            _body.Children.Add(UiKit.Card(codePanel, 18));
            UpdateCode();

            // ---- 服务状态 ----
            var lan = PairingRuntime.Lan;
            var status = new StackPanel();
            status.Children.Add(new TextBlock
            {
                Text = (lan != null && lan.Running ? "● 局域网服务运行中" : "○ 局域网服务未运行")
                    + (lan != null && !string.IsNullOrEmpty(lan.LastError) ? "（" + lan.LastError + "）" : ""),
                FontFamily = UiKit.UiFont, FontSize = 14, FontWeight = FontWeights.SemiBold,
                Foreground = UiKit.Res(lan != null && lan.Running ? "Green" : "Red")
            });
            status.Children.Add(new TextBlock
            {
                Text = "本机地址：" + LanService.LocalIp() + "　端口：TCP " + LanService.TcpPort + " / UDP " + LanService.UdpPort
                    + "　设备名：" + (lan != null ? lan.DeviceName : "-")
                    + "　已配对：" + (PairingRuntime.Pairing != null ? PairingRuntime.Pairing.Data.Devices.Count : 0) + " 台",
                FontFamily = UiKit.UiFont, FontSize = 12.5, Foreground = UiKit.Res("TextSub"), Margin = new Thickness(0, 4, 0, 10)
            });
            var sBtns = new StackPanel { Orientation = Orientation.Horizontal };
            sBtns.Children.Add(UiKit.Primary("启动 / 重启服务", () => { PairingRuntime.EnsureStarted(); Reload(); }));
            sBtns.Children.Add(UiKit.Subtle("刷新", Reload));
            if (lan != null && lan.Running)
                sBtns.Children.Add(UiKit.Subtle("停止服务", () => { try { lan.Stop(); } catch { } Reload(); }));
            status.Children.Add(sBtns);
            status.Children.Add(new TextBlock
            {
                Text = "如果手机搜不到电脑：确认两者在同一 Wi-Fi；Windows 防火墙放行 TCP " + LanService.TcpPort + " 与 UDP " + LanService.UdpPort + "。",
                FontFamily = UiKit.UiFont, FontSize = 12, Foreground = UiKit.Res("TextFaint"),
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0)
            });
            _body.Children.Add(UiKit.Card(status));

            // ---- 已配对设备 ----
            var devices = PairingRuntime.Pairing != null ? PairingRuntime.Pairing.Data.Devices.ToList() : new List<PairedDevice>();
            _body.Children.Add(new TextBlock
            {
                Text = "已配对的手机（" + devices.Count + "）",
                FontFamily = UiKit.UiFont, FontSize = 15, FontWeight = FontWeights.SemiBold,
                Foreground = UiKit.Res("TextMain"), Margin = new Thickness(0, 8, 0, 8)
            });
            if (devices.Count == 0) _body.Children.Add(UiKit.Faint("还没有手机配对。"));
            foreach (var d in devices)
            {
                var dev = d;
                var sp = new StackPanel();
                var head = new StackPanel { Orientation = Orientation.Horizontal };
                head.Children.Add(UiKit.Icon(0xF5E1, 18, UiKit.Res("Accent")));
                head.Children.Add(new TextBlock
                {
                    Text = (string.IsNullOrEmpty(dev.Name) ? "手机" : dev.Name) + "　账号：" + dev.AccountUser,
                    FontFamily = UiKit.UiFont, FontSize = 15, FontWeight = FontWeights.SemiBold,
                    Foreground = UiKit.Res("TextMain"), VerticalAlignment = VerticalAlignment.Center
                });
                sp.Children.Add(head);
                sp.Children.Add(new TextBlock
                {
                    Text = "配对时间：" + dev.PairedUtc + "　最后同步：" + (string.IsNullOrEmpty(dev.LastSyncUtc) ? "从未" : dev.LastSyncUtc) + "　最近地址：" + dev.LastIp,
                    FontFamily = UiKit.UiFont, FontSize = 12.5, Foreground = UiKit.Res("TextSub"), Margin = new Thickness(0, 4, 0, 8)
                });
                sp.Children.Add(UiKit.Subtle("解除配对", () =>
                {
                    if (!SimpleDialogs.Confirm("解除配对", "确定解除与「" + dev.Name + "」的配对吗？该手机需要重新输入配对码。", true)) return;
                    PairingRuntime.Pairing.Unpair(dev.Id);
                    PairingRuntime.Pairing.Save();
                    Reload();
                }));
                _body.Children.Add(UiKit.Card(sp));
            }

            // ---- 日志 ----
            _body.Children.Add(new TextBlock
            {
                Text = "连接日志（最近 400 条）",
                FontFamily = UiKit.UiFont, FontSize = 15, FontWeight = FontWeights.SemiBold,
                Foreground = UiKit.Res("TextMain"), Margin = new Thickness(0, 8, 0, 8)
            });
            var logBox = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Height = 180,
                FontFamily = new FontFamily("Consolas, Microsoft YaHei UI"),
                FontSize = 12,
                Text = string.Join(Environment.NewLine, PairingRuntime.Snapshot()),
                Background = UiKit.Res("Card"),
                BorderBrush = UiKit.Res("Border"),
                Padding = new Thickness(8)
            };
            logBox.CaretIndex = logBox.Text.Length;
            logBox.ScrollToEnd();
            _body.Children.Add(logBox);
        }
    }

    // ============================================================
    //  系统设置
    // ============================================================
    public class SettingsView : UserControl
    {
        private static Tensyan.Core.App Core { get { return App.Core; } }
        private readonly StackPanel _body = new StackPanel();

        public SettingsView()
        {
            Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _body };
            Loaded += (s, e) => Reload();
        }

        public void Reload()
        {
            _body.Children.Clear();
            _body.Children.Add(UiKit.Title("系统设置"));
            _body.Children.Add(UiKit.Sub("班级、快捷分值、行为开关与数据位置。"));

            // ---- 班级 ----
            var clsPanel = new StackPanel();
            clsPanel.Children.Add(SectionTitle("班级管理"));
            foreach (var c in Core.Data.Classes.ToList())
            {
                var cc = c;
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 6) };
                row.Children.Add(new TextBlock
                {
                    Text = cc.Name + "（" + cc.Students.Count(s => !s.Archived) + " 人）",
                    FontFamily = UiKit.UiFont, FontSize = 14, Foreground = UiKit.Res("TextMain"),
                    VerticalAlignment = VerticalAlignment.Center, MinWidth = 200
                });
                if (Core.Session.IsAdmin)
                {
                    row.Children.Add(UiKit.Subtle("改名", () =>
                    {
                        var r = SimpleDialogs.Prompt("班级改名", "输入新的班级名称：", new[] { "班级名称" }, new[] { cc.Name });
                        if (r == null || string.IsNullOrWhiteSpace(r[0])) return;
                        cc.Name = r[0].Trim(); Core.Raise(); Core.Store.Save(); Reload();
                    }));
                    row.Children.Add(UiKit.Subtle("删除", () =>
                    {
                        if (!SimpleDialogs.Confirm("删除班级", "确定删除班级「" + cc.Name + "」吗？\n（学生与积分记录会一并从本班移除）", true)) return;
                        Core.Data.Classes.Remove(cc);
                        if (Core.Current == cc) { Core.Current = Core.Data.Classes.FirstOrDefault(); if (Core.Current != null) Core.Settings.LastClassId = Core.Current.Id; }
                        Core.Raise(); Core.Store.Save(); Reload();
                    }));
                }
                clsPanel.Children.Add(row);
            }
            clsPanel.Children.Add(UiKit.Primary("新增班级", () =>
            {
                var r = SimpleDialogs.Prompt("新增班级", "输入班级名称：", new[] { "班级名称" }, new[] { "新班级" });
                if (r == null || string.IsNullOrWhiteSpace(r[0])) return;
                var c = new SchoolClass { Name = r[0].Trim() };
                Core.Data.Classes.Add(c);
                Core.SwitchClass(c);
                Core.Raise(); Core.Store.Save(); Reload();
            }, 0xF10A));
            _body.Children.Add(UiKit.Card(clsPanel));

            // ---- 快捷分值 ----
            var quick = new StackPanel();
            quick.Children.Add(SectionTitle("快捷分值"));
            quick.Children.Add(new TextBlock
            {
                Text = "加分快捷项：" + string.Join("、", Core.Settings.QuickAdd)
                    + "　　扣分快捷项：" + string.Join("、", Core.Settings.QuickSub),
                FontFamily = UiKit.UiFont, FontSize = 13, Foreground = UiKit.Res("TextSub"), Margin = new Thickness(0, 0, 0, 8)
            });
            var qBtns = new StackPanel { Orientation = Orientation.Horizontal };
            qBtns.Children.Add(UiKit.Subtle("修改加分项", () =>
            {
                var r = SimpleDialogs.Prompt("快捷加分项", "用逗号分隔，例如：1,2,3,5", new[] { "分值列表" }, new[] { string.Join(",", Core.Settings.QuickAdd) });
                if (r == null) return;
                var list = ParseInts(r[0]);
                if (list.Count == 0) { SimpleDialogs.Info("快捷加分项", new[] { "至少填一个正整数。" }); return; }
                Core.Settings.QuickAdd = list; Core.Store.Save(); Reload();
            }));
            qBtns.Children.Add(UiKit.Subtle("修改扣分项", () =>
            {
                var r = SimpleDialogs.Prompt("快捷扣分项", "用逗号分隔，例如：1,2,3,5", new[] { "分值列表" }, new[] { string.Join(",", Core.Settings.QuickSub) });
                if (r == null) return;
                var list = ParseInts(r[0]);
                if (list.Count == 0) { SimpleDialogs.Info("快捷扣分项", new[] { "至少填一个正整数。" }); return; }
                Core.Settings.QuickSub = list; Core.Store.Save(); Reload();
            }));
            quick.Children.Add(qBtns);
            _body.Children.Add(UiKit.Card(quick));

            // ---- 行为开关 ----
            var sw = new StackPanel();
            sw.Children.Add(SectionTitle("行为与外观"));
            sw.Children.Add(Toggle("界面动画（开关窗口、切页面、图标抖动）", Core.Settings.AnimationEnabled, v => { Core.Settings.AnimationEnabled = v; Core.Store.Save(); }));
            sw.Children.Add(Toggle("音效", Core.Settings.SoundEnabled, v => { Core.Settings.SoundEnabled = v; Core.Store.Save(); }));
            sw.Children.Add(Toggle("成员可查看全部班级", Core.Settings.MemberCanSeeAllClasses, v => { Core.Settings.MemberCanSeeAllClasses = v; Core.Store.Save(); }));
            sw.Children.Add(Toggle("注册新账号需要管理员 U 盘认证", Core.Settings.RegisterNeedsAdminUsb, v => { Core.Settings.RegisterNeedsAdminUsb = v; Core.Store.Save(); }));
            _body.Children.Add(UiKit.Card(sw));

            // ---- 数据 ----
            var data = new StackPanel();
            data.Children.Add(SectionTitle("数据"));
            data.Children.Add(new TextBlock
            {
                Text = "数据目录：" + Paths.DataDir + "\n自动备份保留份数：" + Core.Settings.BackupKeep,
                FontFamily = UiKit.UiFont, FontSize = 12.5, Foreground = UiKit.Res("TextSub"),
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8)
            });
            var dBtns = new StackPanel { Orientation = Orientation.Horizontal };
            dBtns.Children.Add(UiKit.Primary("立即保存", () => { Core.Store.Save(); SimpleDialogs.Info("保存", new[] { "已保存到：" + Paths.DataDir }); }));
            dBtns.Children.Add(UiKit.Subtle("打开数据目录", () =>
            {
                try { Process.Start("explorer.exe", "\"" + Paths.DataDir + "\""); } catch { }
            }));
            dBtns.Children.Add(UiKit.Subtle("导出积分表", () =>
            {
                if (Core.Current == null) return;
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Tensyan");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, Exporter.DefaultFileName("积分表"));
                string msg;
                if (Exporter.ExportClassScores(Core.Data, Core.Current, path, out msg))
                    SimpleDialogs.Info("导出成功", new[] { "已导出到：", path });
                else SimpleDialogs.Info("导出失败", new[] { msg });
            }));
            data.Children.Add(dBtns);
            _body.Children.Add(UiKit.Card(data));

            // ---- 关于 ----
            var about = new StackPanel();
            about.Children.Add(SectionTitle("关于"));
            about.Children.Add(new TextBlock
            {
                Text = "Tensyan 班级积分工具 · WPF 版 " + (System.Reflection.Assembly.GetExecutingAssembly().GetName().Version)
                    + "\n公司：Clarusvita Studio　　作者：furliflix"
                    + "\n显示缩放：" + Math.Round(GetScale() * 100) + "%　渲染：WPF（GPU 合成，任意 DPI 清晰）",
                FontFamily = UiKit.UiFont, FontSize = 12.5, Foreground = UiKit.Res("TextSub"), TextWrapping = TextWrapping.Wrap
            });
            _body.Children.Add(UiKit.Card(about));
        }

        private static double GetScale()
        {
            try
            {
                var src = PresentationSource.FromVisual(Application.Current.MainWindow);
                if (src != null && src.CompositionTarget != null) return src.CompositionTarget.TransformToDevice.M11;
            }
            catch { }
            return 1;
        }

        private static TextBlock SectionTitle(string text)
        {
            return new TextBlock
            {
                Text = text, FontFamily = UiKit.UiFont, FontSize = 15, FontWeight = FontWeights.SemiBold,
                Foreground = UiKit.Res("TextMain"), Margin = new Thickness(0, 0, 0, 8)
            };
        }

        private static List<int> ParseInts(string s)
        {
            var list = new List<int>();
            foreach (var part in (s ?? "").Split(new[] { ',', '，', ' ', ';', '；' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int v;
                if (int.TryParse(part.Trim(), out v) && v > 0 && !list.Contains(v)) list.Add(v);
            }
            list.Sort();
            return list;
        }

        private static UIElement Toggle(string text, bool value, Action<bool> changed)
        {
            var cb = new CheckBox
            {
                Content = text,
                IsChecked = value,
                FontFamily = UiKit.UiFont,
                FontSize = 13.5,
                Foreground = UiKit.Res("TextMain"),
                Margin = new Thickness(0, 4, 0, 4)
            };
            cb.Checked += (s, e) => changed(true);
            cb.Unchecked += (s, e) => changed(false);
            return cb;
        }
    }
}
