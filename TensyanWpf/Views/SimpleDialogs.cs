using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Tensyan.Wpf.Views
{
    /// <summary>
    /// 小型对话框（Fluent 风格，与主界面同一套字体与配色）。
    /// WPF 没有 WinForms 的 InputBox，这里自己实现 输入 / 提示 / 确认 三种。
    /// </summary>
    public static class SimpleDialogs
    {
        private static FontFamily UiFont
        {
            get { return (FontFamily)Application.Current.FindResource("UiFont"); }
        }

        private static Brush Res(string key) { return (Brush)Application.Current.FindResource(key); }

        private static Window Shell(string title, string message, out StackPanel body)
        {
            var w = new Window
            {
                Title = title,
                SizeToContent = SizeToContent.Height,
                Width = 460,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Background = Brushes.White,
                FontFamily = UiFont,
                SnapsToDevicePixels = true,
                UseLayoutRounding = true
            };
            if (Application.Current.MainWindow != null) w.Owner = Application.Current.MainWindow;

            body = new StackPanel { Margin = new Thickness(22, 18, 22, 0) };
            if (!string.IsNullOrEmpty(message))
            {
                body.Children.Add(new TextBlock
                {
                    Text = message,
                    FontSize = 14,
                    Foreground = Res("TextMain"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 12)
                });
            }
            return w;
        }

        private static void Finish(Window w, StackPanel body, UIElement buttons)
        {
            var root = new StackPanel();
            root.Children.Add(body);
            root.Children.Add(buttons);
            w.Content = new Border { Padding = new Thickness(0, 0, 0, 18), Background = Brushes.White, Child = root };
        }

        /// <summary>多个输入框；取消返回 null。</summary>
        public static string[] Prompt(string title, string message, string[] labels, string[] defaults)
        {
            StackPanel body;
            var w = Shell(title, message, out body);
            var boxes = new List<TextBox>();
            for (int i = 0; i < labels.Length; i++)
            {
                body.Children.Add(new TextBlock
                {
                    Text = labels[i],
                    FontSize = 12.5,
                    Foreground = Res("TextSub"),
                    Margin = new Thickness(0, i == 0 ? 0 : 10, 0, 4)
                });
                var tb = new TextBox
                {
                    Text = (defaults != null && i < defaults.Length) ? defaults[i] : "",
                    FontSize = 14,
                    Height = 34,
                    Padding = new Thickness(8, 0, 8, 0),
                    VerticalContentAlignment = VerticalAlignment.Center,
                    FontFamily = UiFont
                };
                boxes.Add(tb);
                body.Children.Add(tb);
            }

            string[] result = null;
            var ok = new Button { Content = "确定", Width = 92, Height = 34, Style = (Style)Application.Current.FindResource("PrimaryButton"), Margin = new Thickness(8, 0, 0, 0), IsDefault = true };
            var cancel = new Button { Content = "取消", Width = 92, Height = 34, Style = (Style)Application.Current.FindResource("SubtleButton"), IsCancel = true };
            ok.Click += (s, e) =>
            {
                result = new string[boxes.Count];
                for (int i = 0; i < boxes.Count; i++) result[i] = boxes[i].Text.Trim();
                w.DialogResult = true;
            };

            var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(22, 18, 22, 0) };
            bar.Children.Add(cancel);
            bar.Children.Add(ok);
            Finish(w, body, bar);

            if (boxes.Count > 0) { boxes[0].Focus(); boxes[0].SelectAll(); }
            return w.ShowDialog() == true ? result : null;
        }

        /// <summary>信息提示。</summary>
        public static void Info(string title, string[] lines)
        {
            StackPanel body;
            var w = Shell(title, null, out body);
            foreach (var line in lines)
            {
                body.Children.Add(new TextBlock
                {
                    Text = line,
                    FontSize = 13.5,
                    Foreground = Res("TextMain"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 2)
                });
            }
            var ok = new Button { Content = "好", Width = 92, Height = 34, Style = (Style)Application.Current.FindResource("PrimaryButton"), IsDefault = true, IsCancel = true };
            ok.Click += (s, e) => w.DialogResult = true;
            var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(22, 18, 22, 0) };
            bar.Children.Add(ok);
            Finish(w, body, bar);
            w.ShowDialog();
        }

        /// <summary>确认（危险操作用红色按钮）。</summary>
        public static bool Confirm(string title, string message, bool danger = false)
        {
            StackPanel body;
            var w = Shell(title, message, out body);
            var ok = new Button
            {
                Content = "确定",
                Width = 92,
                Height = 34,
                IsDefault = true,
                Style = (Style)Application.Current.FindResource("PrimaryButton")
            };
            if (danger) ok.Background = Res("Red");
            ok.Click += (s, e) => w.DialogResult = true;
            var cancel = new Button { Content = "取消", Width = 92, Height = 34, Style = (Style)Application.Current.FindResource("SubtleButton"), IsCancel = true };

            var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(22, 18, 22, 0) };
            bar.Children.Add(cancel);
            bar.Children.Add(ok);
            Finish(w, body, bar);
            return w.ShowDialog() == true;
        }
    }
}
