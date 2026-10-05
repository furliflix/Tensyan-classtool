using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Tensyan.Wpf.Views
{
    /// <summary>
    /// 页面通用小工具：标题、副标题、卡片、按钮、表格。
    /// 7 个二级页面用纯 C# 构建界面（不走 XAML），风格与主界面一致。
    /// </summary>
    public static class UiKit
    {
        public static FontFamily UiFont { get { return (FontFamily)Application.Current.FindResource("UiFont"); } }
        public static FontFamily IconFont { get { return (FontFamily)Application.Current.FindResource("IconFont"); } }
        public static Brush Res(string key) { return (Brush)Application.Current.FindResource(key); }
        public static Style Btn(string key) { return (Style)Application.Current.FindResource(key); }

        public static TextBlock Title(string text)
        {
            return new TextBlock { Text = text, FontFamily = UiFont, FontSize = 24, FontWeight = FontWeights.SemiBold, Foreground = Res("TextMain") };
        }

        public static TextBlock Sub(string text)
        {
            return new TextBlock { Text = text, FontFamily = UiFont, FontSize = 13, Foreground = Res("TextSub"), Margin = new Thickness(0, 4, 0, 10), TextWrapping = TextWrapping.Wrap };
        }

        public static TextBlock Faint(string text)
        {
            return new TextBlock { Text = text, FontFamily = UiFont, FontSize = 12, Foreground = Res("TextFaint"), Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap };
        }

        public static TextBlock Icon(int code, double size, Brush brush)
        {
            return new TextBlock
            {
                Text = code > 0xFFFF ? char.ConvertFromUtf32(code) : ((char)code).ToString(),
                FontFamily = IconFont,
                FontSize = size,
                Foreground = brush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
        }

        public static Button Primary(string text, Action click, int iconCode = 0)
        {
            var b = new Button { Style = Btn("PrimaryButton"), Height = 34, MinWidth = 88, Margin = new Thickness(0, 0, 8, 0) };
            if (iconCode != 0) b.Content = IconRow(iconCode, text, Brushes.White);
            else b.Content = text;
            b.Click += (s, e) => { try { click(); } catch (Exception ex) { MessageBox.Show(ex.Message, "Tensyan"); } };
            return b;
        }

        public static Button Subtle(string text, Action click, int iconCode = 0)
        {
            var b = new Button { Style = Btn("SubtleButton"), Height = 34, MinWidth = 88, Margin = new Thickness(0, 0, 8, 0) };
            if (iconCode != 0) b.Content = IconRow(iconCode, text, Res("TextMain"));
            else b.Content = text;
            b.Click += (s, e) => { try { click(); } catch (Exception ex) { MessageBox.Show(ex.Message, "Tensyan"); } };
            return b;
        }

        private static StackPanel IconRow(int code, string text, Brush brush)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock
            {
                Text = code > 0xFFFF ? char.ConvertFromUtf32(code) : ((char)code).ToString(),
                FontFamily = IconFont,
                FontSize = 16,
                Foreground = brush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });
            sp.Children.Add(new TextBlock { Text = text, FontFamily = UiFont, FontSize = 14, Foreground = brush, VerticalAlignment = VerticalAlignment.Center });
            return sp;
        }

        public static Border Card(UIElement child, double pad = 16)
        {
            return new Border
            {
                Background = Res("Card"),
                BorderBrush = Res("Border"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(pad),
                Margin = new Thickness(0, 0, 12, 12),
                Child = child
            };
        }

        /// <summary>一排统计数字（大数字 + 说明）。</summary>
        public static Border Stat(string value, string caption, Brush color = null)
        {
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text = value,
                FontFamily = UiFont,
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = color ?? Res("Accent")
            });
            sp.Children.Add(new TextBlock { Text = caption, FontFamily = UiFont, FontSize = 12.5, Foreground = Res("TextSub"), Margin = new Thickness(0, 2, 0, 0) });
            return Card(sp, 14);
        }

        /// <summary>简易表格：表头 + 数据行（斑马纹）。</summary>
        public static ScrollViewer Table(string[] headers, List<string[]> rows, double[] widths = null, double rowHeight = 30)
        {
            var grid = new Grid();
            for (int i = 0; i < headers.Length; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = (widths != null && i < widths.Length) ? new GridLength(widths[i], GridUnitType.Star) : new GridLength(1, GridUnitType.Star)
                });
            }
            for (int r = 0; r <= rows.Count; r++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(r == 0 ? rowHeight + 6 : rowHeight) });

            for (int c = 0; c < headers.Length; c++)
            {
                var tb = new TextBlock
                {
                    Text = headers[c],
                    FontFamily = UiFont,
                    FontSize = 12.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Res("TextSub"),
                    Margin = new Thickness(10, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetRow(tb, 0); Grid.SetColumn(tb, c);
                grid.Children.Add(tb);
            }

            for (int r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                if (r % 2 == 1)
                {
                    var bg = new Border { Background = new SolidColorBrush(Color.FromArgb(0x0A, 0, 0, 0)), CornerRadius = new CornerRadius(4) };
                    Grid.SetRow(bg, r + 1); Grid.SetColumnSpan(bg, headers.Length);
                    grid.Children.Add(bg);
                }
                for (int c = 0; c < headers.Length && c < row.Length; c++)
                {
                    var tb = new TextBlock
                    {
                        Text = row[c],
                        FontFamily = UiFont,
                        FontSize = 13.5,
                        Foreground = Res("TextMain"),
                        Margin = new Thickness(10, 0, 6, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };
                    Grid.SetRow(tb, r + 1); Grid.SetColumn(tb, c);
                    grid.Children.Add(tb);
                }
            }

            return new ScrollViewer
            {
                Content = grid,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
        }
    }
}
