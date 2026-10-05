using System;
using System.Drawing;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using Tensyan.Core;

namespace Tensyan.Wpf
{
    /// <summary>
    /// 托盘常驻：关闭窗口只是收进托盘，局域网配对服务继续在后台等手机连接。
    /// 这是 WinForms 版早就有的能力，WPF 壳层重写时漏掉了 —— 一并补回。
    /// </summary>
    public static class TrayHost
    {
        private static NotifyIcon _icon;
        private static bool _balloonShown;

        public static void Install(Window main)
        {
            try
            {
                if (_icon != null) return;

                Icon ico = null;
                try
                {
                    var asm = Assembly.GetExecutingAssembly();
                    foreach (var n in asm.GetManifestResourceNames())
                    {
                        if (n.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                        {
                            using (var s = asm.GetManifestResourceStream(n)) ico = new Icon(s);
                            break;
                        }
                    }
                }
                catch { }
                if (ico == null) ico = SystemIcons.Application;

                var menu = new ContextMenuStrip { ShowImageMargin = false };
                menu.Items.Add("显示主界面", null, (s, e) => ShowMain(main));
                menu.Items.Add("配对手机", null, (s, e) => { ShowMain(main); App.OpenPairingPage(); });
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add("退出（停止后台服务）", null, (s, e) => App.ExitApp());

                _icon = new NotifyIcon
                {
                    Icon = ico,
                    Text = "Tensyan 班级积分工具（后台等待手机连接）",
                    Visible = true,
                    ContextMenuStrip = menu
                };
                _icon.DoubleClick += (s, e) => ShowMain(main);
                Paths.Log("托盘已就绪（Windows 11 默认折进 ^ 隐藏区，可拖到固定区）");
                Hint();
            }
            catch (Exception ex) { Paths.Log("托盘创建失败：" + ex.Message); }
        }

/// <summary>启动时就提示一次：程序会常驻托盘（Windows 11 默认把新图标折进 ^ 隐藏区）。</summary>
        private static void Hint()
        {
            try
            {
                if (_icon == null) return;
                _icon.BalloonTipTitle = "Tensyan 已在托盘常驻";
                _icon.BalloonTipText = "关闭窗口不会退出，仍在后台等待手机连接。\n若任务栏看不到图标：点右下角 ^ 展开，把 Tensyan 拖到固定区。";
                _icon.BalloonTipIcon = ToolTipIcon.Info;
                _icon.ShowBalloonTip(5000);
                _balloonShown = true;
            }
            catch { }
        }

        public static void ShowMain(Window main)
        {
            try
            {
                main.Dispatcher.Invoke(new Action(() =>
                {
                    main.Show();
                    if (main.WindowState == WindowState.Minimized) main.WindowState = WindowState.Normal;
                    main.Activate();
                    main.Topmost = true; main.Topmost = false;   // 抢回前台
                }));
            }
            catch { }
        }

        /// <summary>第一次收进托盘时弹个气泡，告诉用户程序还在后台跑。</summary>
        public static void NotifyHidden()
        {
            try
            {
                if (_icon == null || _balloonShown) return;
                _balloonShown = true;
                _icon.BalloonTipTitle = "Tensyan 仍在后台运行";
                _icon.BalloonTipText = "窗口已收进托盘，局域网配对服务继续等待手机连接。\n双击托盘图标可恢复窗口；右键 → 退出 才真正结束。";
                _icon.BalloonTipIcon = ToolTipIcon.Info;
                _icon.ShowBalloonTip(4000);
            }
            catch { }
        }

        public static void Dispose()
        {
            try { if (_icon != null) { _icon.Visible = false; _icon.Dispose(); _icon = null; } } catch { }
        }
    }
}