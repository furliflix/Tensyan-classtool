using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Tensyan.UI
{
    /// <summary>
    /// 跨框架的 WinForms 兼容垫片：
    /// .NET 8 用托管属性，.NET Framework（32 位版）用等价的 Win32 消息/API。
    /// </summary>
    public static class WinCompat
    {
        private const int EM_SETCUEBANNER = 0x1501;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        /// <summary>输入框占位提示文字（.NET Framework 没有 TextBox.PlaceholderText，用 EM_SETCUEBANNER 实现）。</summary>
        public static void SetPlaceholder(TextBox box, string text)
        {
            if (box == null) return;
#if NETFRAMEWORK
            try
            {
                if (box.IsHandleCreated) SendMessage(box.Handle, EM_SETCUEBANNER, (IntPtr)1, text ?? "");
                else box.HandleCreated += (s, e) => { try { SendMessage(box.Handle, EM_SETCUEBANNER, (IntPtr)1, text ?? ""); } catch { } };
            }
            catch { }
#else
            try { box.PlaceholderText = text ?? ""; } catch { }
#endif
        }

        public static string GetPlaceholder(TextBox box)
        {
            if (box == null) return "";
#if NETFRAMEWORK
            return "";      // .NET Framework 上无法回读提示文字，界面逻辑不依赖它
#else
            try { return box.PlaceholderText ?? ""; } catch { return ""; }
#endif
        }

        /// <summary>进程级 DPI 感知（.NET Framework 由清单里的 dpiAware 负责）。</summary>
        public static void EnableDpiAwareness()
        {
#if NETFRAMEWORK
            try { SetProcessDPIAware(); } catch { }
#else
            try { Application.SetHighDpiMode(HighDpiMode.SystemAware); } catch { }
#endif
        }

#if NETFRAMEWORK
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();
#endif
    }
}
