using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace Tensyan.Setup
{
    /// <summary>
    /// Tensyan XP 特别版 安装程序（net35 / x86）。
    /// 与 x64/32 位版共用同一套安装界面（SetupForm）与核心逻辑（InstallCore），
    /// 差别只有两点：
    ///   1) 负载不是 ZIP（net35 没有 ZipArchive），而是把程序文件直接内嵌成资源写出去；
    ///   2) 运行库检查的是 .NET Framework 3.5（XP 上常见），卸载信息写 HKLM/HKCU 的 32 位视图。
    /// </summary>
    internal static class Program
    {
        internal static bool Silent;
        internal static string TargetDir;
        internal static bool NoShortcuts;
        internal static bool NoLaunch;
        internal static bool UninstallMode;
        internal static string ShotPath;
        internal static bool DiagMode;
        internal static bool Elevated;      // XP 上通常“是管理员就是管理员”，没有 UAC

        [STAThread]
        private static void Main(string[] args)
        {
            try { Ui.SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            AppDomain.CurrentDomain.UnhandledException += (s, e) => InstallCore.Log("未处理异常: " + e.ExceptionObject);
            Application.ThreadException += (s, e) =>
            {
                InstallCore.Log("界面异常: " + e.Exception);
                MessageBox.Show("安装程序出错：" + e.Exception.Message + "\r\n\r\n日志：" + InstallCore.LogPath,
                    InstallCore.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            };

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i].ToLowerInvariant();
                if (a == "--silent" || a == "/s" || a == "/silent") Silent = true;
                else if (a == "--uninstall" || a == "/uninstall") UninstallMode = true;
                else if (a == "--no-shortcuts") NoShortcuts = true;
                else if (a == "--no-launch") NoLaunch = true;
                else if (a == "--diag") DiagMode = true;
                else if (a == "--shot" && i + 1 < args.Length) ShotPath = args[++i];
                else if ((a == "--dir" || a == "/dir" || a == "/d") && i + 1 < args.Length) TargetDir = args[++i];
            }

            InstallCore.Log("=== TensyanSetupXP 启动 参数=" + string.Join(" ", args) + " 静默=" + Silent);
            try { Elevated = InstallCore.IsElevated(); } catch { }

            if (DiagMode)
            {
                string report = InstallCore.Diagnose(string.IsNullOrEmpty(TargetDir) ? InstallCore.DefaultDir : TargetDir);
                try { Console.WriteLine(report); } catch { }
                return;
            }

            if (ShotPath != null)
            {
                var f = new SetupForm { StartPosition = FormStartPosition.Manual, Location = new Point(-4000, -4000) };
                f.Show();
                Application.DoEvents();
                System.Threading.Thread.Sleep(300);
                Application.DoEvents();
                using (var bmp = new Bitmap(f.Width, f.Height))
                {
                    f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                    bmp.Save(ShotPath, System.Drawing.Imaging.ImageFormat.Png);
                }
                f.Close();
                return;
            }

            if (Silent) { Environment.Exit(SilentRun()); return; }

            Application.Run(new SetupForm());
        }

        private static int SilentRun()
        {
            string dir = string.IsNullOrEmpty(TargetDir) ? InstallCore.DefaultDir : TargetDir;
            try
            {
                if (UninstallMode)
                {
                    InstallCore.Log("静默卸载：" + dir);
                    if (InstallCore.IsAppRunning()) InstallCore.KillApp();
                    return InstallCore.Uninstall(dir, false, m => InstallCore.Log(m));
                }

                if (!InstallCore.CanWriteTo(dir)) { InstallCore.Log("目标目录不可写：" + dir); return 4; }
                if (InstallCore.IsAppRunning()) { InstallCore.Log("检测到 Tensyan 正在运行，先结束它"); InstallCore.KillApp(); }

                int n = ExtractPayload(dir, null);
                InstallCore.Log("已复制 " + n + " 个文件到 " + dir);
                WriteUninstaller(dir);
                if (!NoShortcuts) InstallCore.CreateShortcuts(dir, true, true);
                InstallCore.WriteUninstallEntry(dir, InstallCore.DirSizeKb(dir));
                InstallCore.GrantUserAccess(dir);

                if (!NoLaunch)
                {
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.Combine(dir, InstallCore.ExeName)) { WorkingDirectory = dir }); }
                    catch (Exception ex) { InstallCore.Log("启动失败: " + ex.Message); }
                }
                InstallCore.Log("静默安装完成：" + dir);
                return 0;
            }
            catch (Exception ex)
            {
                InstallCore.Log("静默安装异常: " + ex);
                return 9;
            }
        }

        internal static byte[] ReadResource(string logicalName)
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                foreach (var name in asm.GetManifestResourceNames())
                    if (string.Equals(name, logicalName, StringComparison.OrdinalIgnoreCase))
                    {
                        using (var s = asm.GetManifestResourceStream(name))
                        using (var ms = new MemoryStream())
                        {
                            // .NET 3.5 没有 Stream.CopyTo，手工搬
                            var buf = new byte[81920];
                            int read;
                            while ((read = s.Read(buf, 0, buf.Length)) > 0) ms.Write(buf, 0, read);
                            return ms.ToArray();
                        }
                    }
                InstallCore.Log("资源不存在：" + logicalName);
            }
            catch (Exception ex) { InstallCore.Log("读取资源失败 " + logicalName + " : " + ex.Message); }
            return null;
        }

        internal static Image ReadImage(string logicalName)
        {
            var bytes = ReadResource(logicalName);
            if (bytes == null) return null;
            try
            {
                using (var ms = new MemoryStream(bytes))
                using (var img = Image.FromStream(ms))
                    return new Bitmap(img);
            }
            catch { return null; }
        }

        // ---------------- 负载：直接写内嵌文件（XP 上不需要解压 ZIP）----------------

        private static readonly string[,] PayloadMap = new string[,]
        {
            { "payload.app.exe",    "Tensyan.exe" },
            { "payload.app.config", "Tensyan.exe.config" },
            { "payload.readme",     "README.md" },
            { "payload.manual",     "使用说明.txt" },
            { "payload.install",    "安装说明.txt" }
        };

        internal static bool HasPayload { get { return ReadResource("payload.app.exe") != null; } }

        internal static int ExtractPayload(string dir, Action<int, string> progress)
        {
            Directory.CreateDirectory(dir);
            int total = PayloadMap.GetLength(0);
            int done = 0;
            for (int i = 0; i < total; i++)
            {
                byte[] bytes = ReadResource(PayloadMap[i, 0]);
                if (bytes == null) continue;
                string target = Path.Combine(dir, PayloadMap[i, 1]);
                File.WriteAllBytes(target, bytes);
                done++;
                if (progress != null) progress((int)(done * 100.0 / total), "正在复制 " + PayloadMap[i, 1]);
            }
            return done;
        }

        internal static void WriteUninstaller(string dir)
        {
            byte[] un = ReadResource("uninstaller.exe");
            if (un != null) File.WriteAllBytes(Path.Combine(dir, InstallCore.UninstallerName), un);
        }
    }
}
