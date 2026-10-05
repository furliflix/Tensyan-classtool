using System;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Tensyan.Core;

namespace Tensyan.Wpf
{
    /// <summary>导航项：标题 + 官方 Fluent 图标码点。</summary>
    public class NavEntry
    {
        public string Title { get; set; }
        public int Code { get; set; }
        public string Glyph { get { return Code > 0xFFFF ? char.ConvertFromUtf32(Code) : ((char)Code).ToString(); } }
    }

    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<NavEntry> _nav = new ObservableCollection<NavEntry>();
        private bool _closingAnimated;

        // ---------------- 亚克力（毛玻璃）相关 ----------------





        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);


        public MainWindow()
        {
            InitializeComponent();

            // ---- 导航（图标码点取自官方 fluentui-system-icons）----
            _nav.Add(new NavEntry { Title = "学生积分", Code = 0xF5A9 });
            _nav.Add(new NavEntry { Title = "积分排行", Code = 0xE2DE });
            _nav.Add(new NavEntry { Title = "加分记录", Code = 0xF47F });
            _nav.Add(new NavEntry { Title = "数据统计", Code = 0xF345 });
            _nav.Add(new NavEntry { Title = "账号权限", Code = 0xF5C1 });
            _nav.Add(new NavEntry { Title = "登录U盘", Code = 0xEDD0 });
            _nav.Add(new NavEntry { Title = "配对手机", Code = 0xF5E1 });
            _nav.Add(new NavEntry { Title = "系统设置", Code = 0xF6AA });
            Nav.ItemsSource = _nav;
            Nav.SelectedIndex = 0;

            // 300% 缩放的教室白板上，工作区换算成 DIP 可能比窗口还小 —— 直接最大化
            try
            {
                if (SystemParameters.WorkArea.Width < Width + 20 || SystemParameters.WorkArea.Height < Height + 20)
                    WindowState = WindowState.Maximized;
            }
            catch { }

            Loaded += (s, e) => PlayOpenAnimation();
        }

        // ---------------- 窗口材质 ----------------

protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;

                int corner = 2;   // DWMWCP_ROUND：Win11 圆角
                DwmSetWindowAttribute(hwnd, 33, ref corner, sizeof(int));

                bool win11 = Environment.OSVersion.Version.Build >= 22621;
                if (win11)
                {
                    // DWMSBT_TRANSIENTWINDOW = 亚克力（Win11 官方材质，走 DWM 合成，稳定且不卡）
                    int backdrop = 3;
                    DwmSetWindowAttribute(hwnd, 38, ref backdrop, sizeof(int));
                    // 70% 白色覆盖：在亚克力之上呈毛玻璃；即使材质失效也不会发暗
                    RootShell.Background = new LinearGradientBrush(Color.FromArgb(0xB8, 0xFB, 0xFC, 0xFF), Color.FromArgb(0xB4, 0xEF, 0xF2, 0xF6), 90);
                }
                else
                {
                    // Win10 及更早没有系统材质：用不透明浅底，绝不留透明（留透明会被合成成黑色 = "阴间"）
                    RootShell.Background = new LinearGradientBrush(Color.FromRgb(0xF7, 0xF8, 0xFA), Color.FromRgb(0xEC, 0xEE, 0xF1), 90);
                }
            }
            catch { }
        }



        // ---------------- 开 / 关动画 ----------------

        /// <summary>动画开关（系统设置里可关；关闭后所有动画瞬时完成）。</summary>
        private bool AnimOn { get { try { return App.Core == null || App.Core.Settings.AnimationEnabled; } catch { return true; } } }

        /// <summary>打开：整体从小放大 + 淡入，随后导航图标依次向上抖动一下。</summary>
        private void PlayOpenAnimation()
        {
            try
            {
                var scale = new ScaleTransform(0.94, 0.94);
                RootShell.RenderTransformOrigin = new Point(0.5, 0.5);
                RootShell.RenderTransform = scale;
                RootShell.Opacity = 0;

                var dur = TimeSpan.FromMilliseconds(240);
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

                var sb = new Storyboard();
                foreach (var prop in new[] { ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty })
                {
                    var a = new DoubleAnimation(1.0, dur) { EasingFunction = ease };
                    Storyboard.SetTarget(a, scale);
                    Storyboard.SetTargetProperty(a, new PropertyPath(prop));
                    sb.Children.Add(a);
                }
                var fade = new DoubleAnimation(1.0, dur) { EasingFunction = ease, BeginTime = TimeSpan.FromMilliseconds(40) };
                Storyboard.SetTarget(fade, RootShell);
                Storyboard.SetTargetProperty(fade, new PropertyPath(OpacityProperty));
                sb.Children.Add(fade);

                sb.Completed += (s, e) => ShakeNavIcons();
                sb.Begin();
            }
            catch { }
        }

        /// <summary>进入之后所有导航图标向上抖动一下（依次错开，形成波浪）。</summary>
        private void ShakeNavIcons()
        {
            for (int i = 0; i < _nav.Count; i++)
            {
                int idx = i;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(42 * idx) };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    try
                    {
                        var item = Nav.ItemContainerGenerator.ContainerFromIndex(idx) as ListBoxItem;
                        if (item == null) return;
                        var icon = FindIcon(item);
                        if (icon == null) return;

                        var tt = icon.RenderTransform as TranslateTransform;
                        if (tt == null) { tt = new TranslateTransform(0, 0); icon.RenderTransform = tt; }

                        var anim = new DoubleAnimationUsingKeyFrames();
                        anim.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                        anim.KeyFrames.Add(new EasingDoubleKeyFrame(-7, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(110)))
                        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                        anim.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300)))
                        { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 } });

                        var sb = new Storyboard();
                        Storyboard.SetTarget(anim, tt);
                        Storyboard.SetTargetProperty(anim, new PropertyPath(TranslateTransform.YProperty));
                        sb.Children.Add(anim);
                        sb.Begin();
                    }
                    catch { }
                };
                timer.Start();
            }
        }

        private static TextBlock FindIcon(DependencyObject root)
        {
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var tb = child as TextBlock;
                if (tb != null && tb.FontFamily != null && tb.FontFamily.Source != null
                    && tb.FontFamily.Source.IndexOf("FluentSystemIcons", StringComparison.OrdinalIgnoreCase) >= 0)
                    return tb;
                var deep = FindIcon(child);
                if (deep != null) return deep;
            }
            return null;
        }

        /// <summary>关闭：整体缩小 + 淡出，动画结束后再真正关闭。</summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // 关闭窗口 = 收进托盘：局域网配对服务继续在后台等手机连接。
            // 只有托盘菜单里的「退出」才会真正结束（App.Exiting = true）。
            if (!App.Exiting)
            {
                e.Cancel = true;
                Hide();
                TrayHost.NotifyHidden();
                return;
            }
            if (!_closingAnimated)
            {
                e.Cancel = true;
                _closingAnimated = true;
                try
                {
                    var dur = TimeSpan.FromMilliseconds(150);
                    var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

                    var sb = new Storyboard();
                    var fade = new DoubleAnimation(0.0, dur) { EasingFunction = ease };
                    Storyboard.SetTarget(fade, RootShell);
                    Storyboard.SetTargetProperty(fade, new PropertyPath(OpacityProperty));
                    sb.Children.Add(fade);

                    var scale = RootShell.RenderTransform as ScaleTransform;
                    if (scale == null) { scale = new ScaleTransform(1, 1); RootShell.RenderTransform = scale; }
                    foreach (var prop in new[] { ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty })
                    {
                        var a = new DoubleAnimation(0.96, dur) { EasingFunction = ease };
                        Storyboard.SetTarget(a, scale);
                        Storyboard.SetTargetProperty(a, new PropertyPath(prop));
                        sb.Children.Add(a);
                    }
                    sb.Completed += (s, a2) => Close();
                    sb.Begin();
                }
                catch { Close(); }
                return;
            }
            base.OnClosing(e);
        }

        // ---------------- 标题栏 ----------------
        /// <summary>供托盘菜单跳到指定页面。</summary>
        public void SelectPage(string title)
        {
            try
            {
                for (int i = 0; i < _nav.Count; i++)
                {
                    if (_nav[i].Title == title) { Nav.SelectedIndex = i; return; }
                }
            }
            catch { }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 只允许"鼠标"拖动窗口。
            // 智能白板是触屏设备，触摸/笔的按下会产生连带移动事件，
            // 若也走 DragMove，窗口会追着手指跑（表现为鼠标乱窜）。所以这里直接忽略触摸与笔。
            if (e.StylusDevice != null) return;
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (e.ClickCount == 2) { BtnMax_Click(sender, null); return; }
            try { DragMove(); } catch { }
        }

        private void BtnMin_Click(object sender, RoutedEventArgs e) { WindowState = WindowState.Minimized; }

        private void BtnMax_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) { Close(); }

        protected override void OnClosed(EventArgs e)
        {
            try { Views.PairingRuntime.Shutdown(); } catch { }   // 退出时才停局域网配对服务
            base.OnClosed(e);
        }

        // ---------------- 页面切换（WPF 合成线程上的平滑动画）----------------

        private void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = Nav.SelectedItem as NavEntry;
            if (item == null) return;

            UIElement view = BuildView(item.Title);
            bool animate = AnimOn;
            Host.Content = view;

            if (!animate) { view.Opacity = 1; Host.Content = view; return; }
            var slide = new TranslateTransform(0, 14);
            view.RenderTransform = slide;
            view.Opacity = 0;
            var dur = TimeSpan.FromMilliseconds(220);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            view.BeginAnimation(OpacityProperty, new DoubleAnimation(1, dur) { EasingFunction = ease });
            slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, dur) { EasingFunction = ease });
        }

        public static UIElement BuildView(string title)
        {
            switch (title)
            {
                case "学生积分": return new Views.StudentsView();
                case "积分排行": return new Views.RankView();
                case "加分记录": return new Views.HistoryView();
                case "数据统计": return new Views.StatsView();
                case "账号权限": return new Views.AccountsView();
                case "登录U盘": return new Views.UsbView();
                case "配对手机": return new Views.PairingView();
                case "系统设置": return new Views.SettingsView();
                default: return Placeholder(title);
            }
        }

        private static UIElement Placeholder(string title)
        {
            return new TextBlock
            {
                Text = title + "（WPF 版正在逐页迁移，学生积分页已完成）",
                FontFamily = (FontFamily)Application.Current.FindResource("UiFont"),
                FontSize = 14,
                Foreground = (Brush)Application.Current.FindResource("TextFaint"),
                Margin = new Thickness(4, 8, 0, 0)
            };
        }
    }
}
