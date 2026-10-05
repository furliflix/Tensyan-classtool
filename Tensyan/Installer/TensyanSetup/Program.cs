using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
namespace Tensyan.Setup
{
    internal static class Program
    {
        internal static bool Silent;
        internal static string TargetDir;
        internal static bool NoShortcuts;
        internal static bool NoLaunch;
        internal static bool UninstallMode;
        internal static string ShotPath;
        internal static bool Elevated;
        internal static bool DiagMode;

        [STAThread]
        private static void Main(string[] args)
        {
            Ui.SetProcessDPIAware();
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
                else if (a == "--elevated") Elevated = true;
                else if (a == "--diag") DiagMode = true;
                else if (a == "--shot" && i + 1 < args.Length) ShotPath = args[++i];
                else if ((a == "--dir" || a == "/dir" || a == "/d") && i + 1 < args.Length) TargetDir = args[++i];
            }

            InstallCore.Log("=== TensyanSetup 启动 参数=" + string.Join(" ", args) + " 静默=" + Silent);

            if (DiagMode)
            {
                // 命令行诊断：把报告写到文件并输出到控制台（供自检/远程排查）
                string report = InstallCore.Diagnose(string.IsNullOrWhiteSpace(TargetDir) ? InstallCore.DefaultDir : TargetDir);
                Console.WriteLine(report);
                string outFile = Environment.GetEnvironmentVariable("TENSYAN_DIAG_OUT");
                if (!string.IsNullOrWhiteSpace(outFile))
                {
                    try { File.WriteAllText(outFile, report, System.Text.Encoding.UTF8); } catch { }
                }
                return;
            }

            if (ShotPath != null)
            {
                // 开发自检：把安装界面画到图片里
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

            if (Silent)
            {
                int code = SilentRun();
                Environment.Exit(code);
            }

            var form = new SetupForm();
            Application.Run(form);
        }

        /// <summary>静默模式（自动部署/自检用）：不弹任何界面。</summary>
        private static int SilentRun()
        {
            string dir = string.IsNullOrWhiteSpace(TargetDir) ? InstallCore.DefaultDir : TargetDir;
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

                if (!HasPayload) { InstallCore.Log("安装失败：安装包内没有应用负载"); return 5; }

                int n = ExtractPayload(dir, null);
                InstallCore.Log("已复制 " + n + " 个文件到 " + dir);

                WriteUninstaller(dir);

                if (!NoShortcuts) InstallCore.CreateShortcuts(dir, true, true);
                bool registered = InstallCore.WriteUninstallEntry(dir, InstallCore.DirSizeKb(dir));
                InstallCore.Log("已登记到应用和功能：" + registered);
                InstallCore.GrantUserAccess(dir);   // 管理员安装时，让当前用户以后也能在安装目录写数据

                if (!NoLaunch)
                {
                    try { Process.Start(new ProcessStartInfo(Path.Combine(dir, InstallCore.ExeName)) { WorkingDirectory = dir }); }
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
                            s.CopyTo(ms);
                            return ms.ToArray();
                        }
                    }
                InstallCore.Log("资源不存在：" + logicalName);
            }
            catch (Exception ex) { InstallCore.Log("读取资源失败 " + logicalName + " : " + ex.Message); }
            return null;
        }

        // ---------------- 负载读写（net48 版用内嵌 ZIP；XP 版由自己的 Program 实现同名成员）----------------

        /// <summary>安装包里是否有可用的应用负载。</summary>
        internal static bool HasPayload { get { return ReadResource("payload.zip") != null; } }

        /// <summary>把应用负载写入目标目录，返回写入的文件数。</summary>
        internal static int ExtractPayload(string dir, Action<int, string> progress)
        {
            byte[] payload = ReadResource("payload.zip");
            if (payload == null) return 0;
            return InstallCore.ExtractPayload(payload, dir, progress);
        }

        /// <summary>写出卸载器。</summary>
        internal static void WriteUninstaller(string dir)
        {
            byte[] un = ReadResource("uninstaller.exe");
            if (un != null) File.WriteAllBytes(Path.Combine(dir, InstallCore.UninstallerName), un);
        }

        internal static Image ReadImage(string logicalName)
        {
            var bytes = ReadResource(logicalName);
            if (bytes == null) return null;
            try
            {
                // 注意：必须复制一份，Image.FromStream 依赖流，流释放后图片会失效
                using (var ms = new MemoryStream(bytes))
                using (var img = Image.FromStream(ms))
                    return new Bitmap(img);
            }
            catch (Exception ex)
            {
                InstallCore.Log("解码图片失败 " + logicalName + " : " + ex.Message);
                return null;
            }
        }
    }
}
