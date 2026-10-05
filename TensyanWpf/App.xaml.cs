using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Tensyan.Core;

namespace Tensyan.Wpf
{
    /// <summary>
    /// WPF 版入口。业务逻辑直接使用与 WinForms 版、手机端共用的 Tensyan.Core。
    /// DPI：感知方式由 app.manifest 声明为 PerMonitorV2，WPF 会用 DIP 布局 +
    /// GPU 合成按目标 DPI 渲染，因此教室白板 300% 缩放下文字与图标依然清晰。
    /// </summary>
    public partial class App : Application
    {
        /// <summary>是否真的要退出（否则关闭窗口只是收进托盘）。</summary>
        public static bool Exiting;

        public static Store Store;
        public static Tensyan.Core.App Core;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            try
            {
                Paths.Init();
                Paths.Log("=== Tensyan WPF 版启动，数据目录=" + Paths.DataDir + " ===");
                Store = Store.Load();
                Core = new Tensyan.Core.App(Store);
                Core.Session = new Session { Role = Role.Admin, User = "wpf", Display = "老师", LoginMethod = "本机" };
                if (Core.Data.Classes.Count == 0)
                {
                    var cls = new SchoolClass { Name = "我的班级" };
                    Core.Data.Classes.Add(cls);
                    Core.Current = cls;
                    Core.Settings.LastClassId = cls.Id;
                    Store.Save();
                }
            }
            catch (Exception ex)
            {
                Paths.Log("WPF 启动失败：" + ex);
                try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "tensyan-wpf-crash.txt"), ex.ToString()); } catch { }
            }

            // 启动即开局域网配对服务：不必先打开「配对手机」页，手机随时能连
            Views.PairingRuntime.EnsureStarted();

            ShutdownMode = ShutdownMode.OnExplicitShutdown;   // 收进托盘时不能自动退出

            var win = new MainWindow();
            MainWindow = win;
            TrayHost.Install(win);   // 装托盘：关闭窗口后仍在后台等手机连接
            // 自检用：--shot <文件> [--dpi 288]
            // 按指定 DPI 渲染整窗 —— 本机即可验证"白板 300% 缩放下是否清晰"
            string shot = null;
            double dpi = 96;
            var a = e.Args ?? new string[0];
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] == "--shot" && i + 1 < a.Length) shot = a[++i];
                else if (a[i] == "--selftest" && i + 1 < a.Length) { win.Show(); RunSelfTest(a[++i]); return; }
                else if (a[i] == "--mkadmin" && i + 2 < a.Length) { MakeAdmin(a[i + 1], a[i + 2]); return; }
                else if (a[i] == "--dpi" && i + 1 < a.Length) double.TryParse(a[++i], out dpi);
            }
            if (shot != null)
            {
                win.Show();
                win.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    try
                    {
                        win.UpdateLayout();
                        var bmp = new RenderTargetBitmap(
                            (int)Math.Ceiling(win.ActualWidth * dpi / 96.0),
                            (int)Math.Ceiling(win.ActualHeight * dpi / 96.0),
                            dpi, dpi, PixelFormats.Pbgra32);
                        bmp.Render(win);
                        var enc2 = new PngBitmapEncoder();
                        enc2.Frames.Add(BitmapFrame.Create(bmp));
                        using (var fs = File.Create(shot)) enc2.Save(fs);
                        Paths.Log("已出图：" + shot + "  DPI=" + dpi);
                    }
                    catch (Exception ex) { Paths.Log("出图失败：" + ex); }
                    Shutdown();
                }));
                return;
            }
            win.Show();
        }
        /// <summary>
        /// 自检：把 8 个页面逐个实例化、跑一遍数据绑定、各出一张图，并收集异常。
        /// 用于在没有人工点击的情况下发现运行期的资源/绑定错误。
        /// </summary>
        private void RunSelfTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir);
            var titles = new[] { "学生积分", "积分排行", "加分记录", "数据统计", "账号权限", "登录U盘", "配对手机", "系统设置" };
            var report = new System.Text.StringBuilder();
            int idx = 0;
            foreach (var title in titles)
            {
                idx++;
                try
                {
                    var view = Tensyan.Wpf.MainWindow.BuildView(title);
                    var host = new Window
                    {
                        Title = title,
                        Width = 1280,
                        Height = 800,
                        WindowStyle = WindowStyle.None,
                        ShowInTaskbar = false,
                        Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3)),
                        Content = new Border { Padding = new Thickness(24), Child = view }
                    };
                    host.Show();
                    host.UpdateLayout();
                    host.Dispatcher.Invoke(new Action(() => { }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);

                    var bmp = new RenderTargetBitmap((int)host.ActualWidth, (int)host.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    bmp.Render(host);
                    var enc3 = new PngBitmapEncoder();
                    enc3.Frames.Add(BitmapFrame.Create(bmp));
                    using (var fs = System.IO.File.Create(System.IO.Path.Combine(dir, "page" + idx + ".png"))) enc3.Save(fs);

                    host.Close();
                    report.AppendLine("OK   " + title);
                }
                catch (Exception ex)
                {
                    report.AppendLine("FAIL " + title + "  →  " + ex.GetType().Name + ": " + ex.Message);
                }
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "selftest.txt"), report.ToString(), new System.Text.UTF8Encoding(true));
            Paths.Log("自检完成：" + dir);
            Shutdown();
        }
        /// <summary>
        /// 本机救援：直接创建一个管理员账号（忘记管理员密码、或需要批量布置电脑时用）。
        /// 只能在本机执行 —— 数据文件本来就在本机，因此不降低安全性。
        /// 用法：Tensyan.exe --mkadmin 用户名 密码
        /// </summary>
        private void MakeAdmin(string user, string pwd)
        {
            try
            {
                var acc = Store.Accounts.Find(x => string.Equals(x.User, user, StringComparison.OrdinalIgnoreCase));
                if (acc == null)
                {
                    acc = new Account { User = user, Display = user, Role = Role.Admin };
                    Store.Accounts.Add(acc);
                }
                acc.Role = Role.Admin;
                acc.Enabled = true;
                Auth.SetPassword(acc, pwd);
                Store.Save();
                Paths.Log("已创建/更新管理员账号：" + user);
                MessageBox.Show("已创建管理员账号：" + user + "\n数据目录：" + Paths.DataDir, "Tensyan", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { Paths.Log("创建管理员失败：" + ex.Message); }
            Shutdown();
        }

        /// <summary>真正退出（托盘菜单用）：停掉后台服务再关。</summary>
        public static void ExitApp()
        {
            try { Exiting = true; Views.PairingRuntime.Shutdown(); TrayHost.Dispose(); } catch { }
            Current.Shutdown();
        }

        /// <summary>跳到「配对手机」页（托盘菜单用）。</summary>
        public static void OpenPairingPage()
        {
            try
            {
                var win = Current.MainWindow as MainWindow;
                if (win != null) win.SelectPage("配对手机");
            }
            catch { }
        }    }
}