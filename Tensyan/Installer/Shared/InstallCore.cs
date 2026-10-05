using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace Tensyan.Setup
{
    /// <summary>安装/卸载共用逻辑（安装程序与卸载程序都编译这个文件）。</summary>
    public static class InstallCore
    {
        public const string AppName = "Tensyan 班级积分工具";
        public const string Version = "1.1.0";
        public const string Publisher = "Clarusvita Studio";
        public const string ExeName = "Tensyan.exe";
        public const string UninstallerName = "TensyanUninstall.exe";
        public const string DataFolder = "data";
        public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Tensyan";
        public const string UninstallKeyMachine = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Tensyan";

        /// <summary>
        /// 默认安装位置：以管理员运行时装到 Program Files（机器级），
        /// 普通权限运行时装到当前用户的 Programs 目录（用户级，无需 UAC）。
        /// </summary>
        public static string DefaultDir
        {
            get
            {
                if (IsElevated())
                    return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tensyan");
                return Path.Combine(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
                    "Tensyan");
            }
        }

        public static string LogPath
        {
            get { return Path.Combine(Path.GetTempPath(), "tensyan-setup.log"); }
        }

        public static void Log(string msg)
        {
            try
            {
                File.AppendAllText(LogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }

        /// <summary>快捷方式根目录（正常用桌面/开始菜单；自检时可用环境变量指定）。</summary>
        public static string LnkRoot
        {
            get
            {
                string env = Environment.GetEnvironmentVariable("TENSYAN_LNK_DIR");
                return Blank(env) ? null : env;
            }
        }

        public static string StartMenuDir
        {
            get
            {
                if (LnkRoot != null) return Combine3(LnkRoot, "StartMenu", "Tensyan");
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Tensyan");
            }
        }

        public static string DesktopLnk
        {
            get
            {
                if (LnkRoot != null) return Combine3(LnkRoot, "Desktop", AppName + ".lnk");
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk");
            }
        }

        public static string StartMenuLnk
        {
            get { return Path.Combine(StartMenuDir, AppName + ".lnk"); }
        }

        public static string StartMenuUninstallLnk
        {
            get { return Path.Combine(StartMenuDir, "卸载 " + AppName + ".lnk"); }
        }

        // ---------------- 环境检查 ----------------

        public static bool IsAppRunning()
        {
            try
            {
                var me = Process.GetCurrentProcess().ProcessName;
                foreach (var p in Process.GetProcessesByName("Tensyan"))
                    if (p.Id != Process.GetCurrentProcess().Id && p.ProcessName != me) return true;
            }
            catch { }
            return false;
        }

        public static void KillApp()
        {
            try
            {
                foreach (var p in Process.GetProcessesByName("Tensyan"))
                {
                    try { p.Kill(); p.WaitForExit(4000); } catch { }
                }
            }
            catch { }
        }

        /// <summary>
        /// 目标目录是否可写：只要**能创建并写入**就算可写。
        /// 不能要求“删除成功”——受管环境常禁止删除/改名，但允许写入。
        /// </summary>
        public static bool CanWriteTo(string dir)
        {
            string reason;
            return CanWriteTo(dir, out reason);
        }

        /// <summary>可写性检查（带失败原因，交给界面显示）。</summary>
        public static bool CanWriteTo(string dir, out string reason)
        {
            reason = "";
            try
            {
                Directory.CreateDirectory(dir);
                return ProbeWritable(dir, out reason);
            }
            catch (UnauthorizedAccessException ex) { reason = "没有写入权限：" + ex.Message; return false; }
            catch (DirectoryNotFoundException ex) { reason = "路径不存在：" + ex.Message; return false; }
            catch (IOException ex) { reason = "磁盘不可写或已满：" + ex.Message; return false; }
            catch (Exception ex) { reason = ex.Message; return false; }
        }

        /// <summary>
        /// 只做检查、**不创建任何目录**（用于界面里边输入边校验）。
        /// 目标目录还不存在时，向上找到最近的已存在目录来探测，并说明安装时会创建它。
        /// </summary>
        public static bool ProbeTarget(string dir, out string reason, out bool willCreate)
        {
            reason = "";
            willCreate = false;
            try
            {
                if (Blank(dir)) { reason = "请输入安装位置"; return false; }

                string root = Path.GetPathRoot(dir);
                if (!string.IsNullOrEmpty(root) && !Directory.Exists(root))
                {
                    reason = "盘符不存在：" + root + "（这台电脑没有这个盘）";
                    return false;
                }

                string probeDir = dir;
                while (!Directory.Exists(probeDir))
                {
                    willCreate = true;
                    string parent = Path.GetDirectoryName(probeDir.TrimEnd('\\'));
                    if (string.IsNullOrEmpty(parent) || string.Equals(parent, probeDir, StringComparison.OrdinalIgnoreCase)) break;
                    probeDir = parent;
                }
                if (!Directory.Exists(probeDir)) { reason = "路径不存在：" + dir; return false; }
                return ProbeWritable(probeDir, out reason);
            }
            catch (UnauthorizedAccessException ex) { reason = "没有写入权限：" + ex.Message; return false; }
            catch (Exception ex) { reason = ex.Message; return false; }
        }

        private static bool ProbeWritable(string dir, out string reason)
        {
            reason = "";
            try
            {
                string probe = Path.Combine(dir, ".wtest-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                File.WriteAllText(probe, "ok");
                try { File.Delete(probe); } catch { Log("提示：目录 " + dir + " 允许写入但不允许删除，按可写处理"); }
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                reason = "没有写入权限（可能被系统策略、杀毒软件限制，或本程序是被某个受限环境/沙箱启动的）：" + ex.Message
                    + " 建议：直接在 Windows 资源管理器里双击本安装程序再试，或点“使用默认”装到用户目录。";
                return false;
            }
            catch (DirectoryNotFoundException ex) { reason = "路径不存在：" + ex.Message; return false; }
            catch (IOException ex) { reason = "磁盘不可写或已满：" + ex.Message; return false; }
            catch (Exception ex) { reason = ex.Message; return false; }
        }

        // ---------------- 安装 ----------------

        /// <summary>把内嵌的 payload.zip 解压到目标目录；返回解压出的文件数。
        /// XP 版（net35）没有 ZipArchive，改由该版本的 Program 写内嵌文件，不走这里。</summary>
        public static int ExtractPayload(byte[] zipBytes, string dir, Action<int, string> progress)
        {
#if NET35
            Log("当前版本不支持 ZIP 负载");
            return 0;
#else
            Directory.CreateDirectory(dir);
            int done = 0;
            using (var ms = new MemoryStream(zipBytes))
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Read))
            {
                int total = zip.Entries.Count;
                foreach (var entry in zip.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;    // 目录项
                    string target = Path.Combine(dir, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
                    string parent = Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                    using (var src = entry.Open())
                    using (var dst = File.Create(target))
                        src.CopyTo(dst);
                    done++;
                    if (progress != null) progress(total == 0 ? 100 : (int)(done * 100.0 / total), "正在复制 " + entry.Name);
                }
            }
            return done;
#endif
        }

        /// <summary>写入卸载信息（控制面板 / 设置 → 应用）。失败不算致命（受限账户可能被拒）。</summary>
        public static bool WriteUninstallEntry(string dir, long sizeKb)
        {
            // 管理员安装 → 机器级 HKLM（所有用户可见）；否则 → 当前用户 HKCU
            bool machine = IsElevated();
            try
            {
                using (var baseKey = machine ? Registry.LocalMachine : Registry.CurrentUser)
                using (var key = baseKey.CreateSubKey(machine ? UninstallKeyMachine : UninstallKey))
                {
                    if (key == null) { Log("无法创建卸载注册表项"); return false; }
                    string exe = Path.Combine(dir, ExeName);
                    string unins = Path.Combine(dir, UninstallerName);
                    key.SetValue("DisplayName", AppName);
                    key.SetValue("DisplayVersion", Version);
                    key.SetValue("Publisher", Publisher);
                    key.SetValue("DisplayIcon", exe + ",0");
                    key.SetValue("InstallLocation", dir);
                    key.SetValue("UninstallString", "\"" + unins + "\"");
                    key.SetValue("QuietUninstallString", "\"" + unins + "\" --silent");
                    key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                    key.SetValue("EstimatedSize", (int)Math.Max(1, sizeKb), RegistryValueKind.DWord);
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    key.SetValue("URLInfoAbout", "https://clarusvita.studio");
                }
                Log("已写入卸载信息：" + (machine ? "HKLM" : "HKCU") + "\\" + (machine ? UninstallKeyMachine : UninstallKey));
                return true;
            }
            catch (Exception ex)
            {
                Log("写入卸载信息失败（安装继续）: " + ex.Message);
                // 管理员写入 HKLM 失败时退回 HKCU，至少保证能卸载
                if (machine) return WriteUninstallEntryFallbackUser(dir, sizeKb);
                return false;
            }
        }

        private static bool WriteUninstallEntryFallbackUser(string dir, long sizeKb)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(UninstallKey))
                {
                    if (key == null) return false;
                    string exe = Path.Combine(dir, ExeName);
                    string unins = Path.Combine(dir, UninstallerName);
                    key.SetValue("DisplayName", AppName);
                    key.SetValue("DisplayVersion", Version);
                    key.SetValue("Publisher", Publisher);
                    key.SetValue("DisplayIcon", exe + ",0");
                    key.SetValue("InstallLocation", dir);
                    key.SetValue("UninstallString", "\"" + unins + "\"");
                    key.SetValue("QuietUninstallString", "\"" + unins + "\" --silent");
                    key.SetValue("EstimatedSize", (int)Math.Max(1, sizeKb), RegistryValueKind.DWord);
                }
                Log("已改用 HKCU 写入卸载信息");
                return true;
            }
            catch (Exception ex) { Log("HKCU 写入也失败: " + ex.Message); return false; }
        }

        public static void RemoveUninstallEntry()
        {
            try { DeleteKeySafe(Registry.CurrentUser, UninstallKey); Log("已删除 HKCU 卸载信息"); }
            catch (Exception ex) { Log("删除 HKCU 卸载信息失败: " + ex.Message); }
            try { DeleteKeySafe(Registry.LocalMachine, UninstallKeyMachine); Log("已删除 HKLM 卸载信息"); }
            catch { }
        }

        /// <summary>卸载是否需要管理员（装在 Program Files，或注册在 HKLM）。</summary>
        public static bool NeedsElevation(string dir)
        {
            if (IsElevated()) return false;
            try
            {
                string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
#if NET35
                string pf86 = null;      // XP 没有 Program Files (x86) 这个枚举值
#else
                string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
#endif
                if (!string.IsNullOrEmpty(dir))
                {
                    if (!string.IsNullOrEmpty(pf) && dir.StartsWith(pf, StringComparison.OrdinalIgnoreCase)) return true;
                    if (!string.IsNullOrEmpty(pf86) && dir.StartsWith(pf86, StringComparison.OrdinalIgnoreCase)) return true;
                }
                using (var key = Registry.LocalMachine.OpenSubKey(UninstallKeyMachine))
                    if (key != null) return true;
            }
            catch { }
            return false;
        }

        /// <summary>从注册表读取安装位置（安装目录被移动时用得上）。</summary>
        public static string ReadInstallDir()
        {
            foreach (var machine in new[] { true, false })
            {
                try
                {
                    using (var baseKey = machine ? Registry.LocalMachine : Registry.CurrentUser)
                    using (var key = baseKey.OpenSubKey(machine ? UninstallKeyMachine : UninstallKey))
                    {
                        if (key != null)
                        {
                            var v = key.GetValue("InstallLocation") as string;
                            if (!string.IsNullOrEmpty(v)) return v;
                        }
                    }
                }
                catch { }
            }
            return null;
        }

        /// <summary>
        /// 管理员安装后，把安装目录的“修改”权限授予当前登录用户，
        /// 这样程序以后（非管理员身份）也能把 data 写在自己目录里。
        /// </summary>
        public static void GrantUserAccess(string dir)
        {
            if (!IsElevated()) return;
            try
            {
                string user = Environment.UserDomainName + "\\" + Environment.UserName;
                var psi = new ProcessStartInfo("icacls.exe", "\"" + dir + "\" /grant \"" + user + ":(OI)(CI)M\" /T /C /Q")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = Process.Start(psi))
                {
                    p.StandardOutput.ReadToEnd();
                    p.StandardError.ReadToEnd();
                    p.WaitForExit(30000);
                    Log("已授予 " + user + " 对安装目录的修改权限（icacls 退出码 " + p.ExitCode + "）");
                }
            }
            catch (Exception ex) { Log("授予目录权限失败: " + ex.Message); }
        }

        // ---------------- 快捷方式 ----------------

        public static bool CreateShortcut(string lnkPath, string target, string args, string workDir, string iconPath, string description)
        {
            try
            {
                string parent = Path.GetDirectoryName(lnkPath);
                if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);

                Type t = Type.GetTypeFromProgID("WScript.Shell");
                if (t == null) { Log("无法创建快捷方式（WScript.Shell 不可用）：" + lnkPath); return false; }
                object shell = Activator.CreateInstance(t);
                object sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
                Type st = sc.GetType();
                st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { target });
                st.InvokeMember("Arguments", BindingFlags.SetProperty, null, sc, new object[] { args ?? "" });
                st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { workDir });
                st.InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { iconPath });
                st.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { description ?? "" });
                st.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
                Marshal.ReleaseComObject(sc);
                Marshal.ReleaseComObject(shell);
                Log("已创建快捷方式：" + lnkPath);
                return true;
            }
            catch (Exception ex)
            {
                Log("创建快捷方式失败 " + lnkPath + " : " + ex.Message);
                return false;
            }
        }

        public static void CreateShortcuts(string dir, bool desktop, bool startMenu)
        {
            string exe = Path.Combine(dir, ExeName);
            string unins = Path.Combine(dir, UninstallerName);
            if (desktop) CreateShortcut(DesktopLnk, exe, "", dir, exe, AppName + " · 班级积分工具");
            if (startMenu)
            {
                CreateShortcut(StartMenuLnk, exe, "", dir, exe, AppName + " · 班级积分工具");
                if (File.Exists(unins)) CreateShortcut(StartMenuUninstallLnk, unins, "", dir, unins, "卸载 " + AppName);
            }
        }

        public static void RemoveShortcuts()
        {
            foreach (var f in new[] { DesktopLnk, StartMenuLnk, StartMenuUninstallLnk })
            {
                try { if (File.Exists(f)) { File.Delete(f); Log("已删除快捷方式：" + f); } } catch (Exception ex) { Log("删除快捷方式失败: " + ex.Message); }
            }
            try
            {
                if (Directory.Exists(StartMenuDir) && Directory.GetFileSystemEntries(StartMenuDir).Length == 0)
                {
                    Directory.Delete(StartMenuDir);
                    Log("已删除开始菜单文件夹");
                }
            }
            catch { }
        }

        // ---------------- 卸载 ----------------

        /// <summary>卸载：删文件（可选保留 data）、删快捷方式、删注册表项。返回 0 表示成功。</summary>
        public static int Uninstall(string dir, bool removeData, Action<string> progress)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                Log("卸载失败：目录不存在 " + dir);
                return 2;
            }
            try
            {
                RemoveShortcuts();
                RemoveUninstallEntry();

                string self = Path.Combine(dir, UninstallerName);
                string dataDir = Path.Combine(dir, DataFolder);
                foreach (var file in Directory.GetFiles(dir))
                {
                    if (string.Equals(file, self, StringComparison.OrdinalIgnoreCase)) continue;
                    try { File.Delete(file); if (progress != null) progress("已删除 " + Path.GetFileName(file)); }
                    catch (Exception ex) { Log("删除失败 " + file + " : " + ex.Message); }
                }
                foreach (var sub in Directory.GetDirectories(dir))
                {
                    if (!removeData && string.Equals(sub, dataDir, StringComparison.OrdinalIgnoreCase))
                    {
                        Log("保留数据目录：" + sub);
                        continue;
                    }
                    try { Directory.Delete(sub, true); if (progress != null) progress("已删除 " + Path.GetFileName(sub)); }
                    catch (Exception ex) { Log("删除目录失败 " + sub + " : " + ex.Message); }
                }

                // data 被保留时，目录里只剩 data：删掉快捷方式后保持原样
                if (!removeData && Directory.Exists(dataDir))
                {
                    Log("数据已保留在：" + dataDir);
                    return 0;
                }

                // 目录已空则删掉自己（exe 自身删不掉，交给 cmd 延时处理）
                bool empty;
                try { empty = Directory.GetFileSystemEntries(dir).Length <= 1; }
                catch { empty = false; }
                if (empty) ScheduleSelfDelete(self, dir);
                else Log("目录内还有其它文件，未删除目录：" + dir);
                return 0;
            }
            catch (Exception ex)
            {
                Log("卸载异常: " + ex);
                return 3;
            }
        }

        /// <summary>延时删除卸载器自身与空目录（进程退出后再执行）。</summary>
        public static void ScheduleSelfDelete(string exePath, string dir)
        {
            try
            {
                string cmd = string.Format("/c ping -n 2 127.0.0.1 > nul & del /f /q \"{0}\" & rmdir \"{1}\"", exePath, dir);
                Process.Start(new ProcessStartInfo("cmd.exe", cmd)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                Log("已安排删除：" + exePath + " 与目录 " + dir);
            }
            catch (Exception ex) { Log("安排自删除失败: " + ex.Message); }
        }

        /// <summary>当前进程是否以管理员身份运行。</summary>
        public static bool IsElevated()
        {
            try
            {
                var id = System.Security.Principal.WindowsIdentity.GetCurrent();
                var p = new System.Security.Principal.WindowsPrincipal(id);
                return p.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        /// <summary>
        /// 目标机是否已安装 .NET Framework 4.8+（32 位版程序运行所需）。
        /// Release ≥ 528040 即 4.8 或更高；新系统上两个注册表视图都查一遍。
        /// </summary>
        public static bool HasNetFx48(out string detail)
        {
            detail = "未检测到 .NET Framework 4.x";
#if NET35
            // .NET 3.5 上拿不到 RegistryView；32 位进程访问 HKLM\SOFTWARE 会被自动重定向，够用
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))
                {
                    if (key != null)
                    {
                        int release = ToInt(key.GetValue("Release"));
                        string v = Convert.ToString(key.GetValue("Version"));
                        if (release >= 528040) { detail = "已安装 .NET Framework " + v + "（Release " + release + "）"; return true; }
                        if (release > 0) { detail = "只检测到 .NET Framework " + v + "（Release " + release + "），低于 4.8"; return false; }
                    }
                }
            }
            catch { }
            return false;
#else
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                    using (var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))
                    {
                        if (key == null) continue;
                        object rel = key.GetValue("Release");
                        object ver = key.GetValue("Version");
                        int release = rel == null ? 0 : Convert.ToInt32(rel);
                        string v = ver == null ? "?" : Convert.ToString(ver);
                        if (release >= 528040)
                        {
                            detail = "已安装 .NET Framework " + v + "（Release " + release + "）";
                            return true;
                        }
                        if (release > 0)
                        {
                            detail = "只检测到 .NET Framework " + v + "（Release " + release + "），低于 4.8";
                            return false;
                        }
                    }
                }
                catch { }
            }
            return false;
#endif
        }

        private static int ToInt(object o)
        {
            try { return o == null ? 0 : Convert.ToInt32(o); } catch { return 0; }
        }

        /// <summary>XP 特别版要检查 .NET Framework 3.5 是否已安装。</summary>
        public static bool HasNetFx35(out string detail)
        {
            detail = "未检测到 .NET Framework 3.5";
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v3.5"))
                {
                    if (key != null)
                    {
                        int install = ToInt(key.GetValue("Install"));
                        int sp = ToInt(key.GetValue("SP"));
                        string v = Convert.ToString(key.GetValue("Version"));
                        if (install == 1)
                        {
                            detail = "已安装 .NET Framework " + (v == "" ? "3.5" : v) + (sp > 0 ? " SP" + sp : "");
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        public static string TempDir { get { return Path.GetTempPath(); } }
        public static string LocalProgramsDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"); }
        }

        private static bool ProbeLine(StringBuilder sb, string label, string dir)
        {
            string reason;
            bool ok = CanWriteTo(dir, out reason);
            sb.AppendLine(string.Format("  {0,-14} {1}   {2}", label, ok ? "可写 ✓" : "拒绝 ✗", dir));
            if (!ok) sb.AppendLine("                 " + reason);
            return ok;
        }

        /// <summary>
        /// 安装诊断：分别探测临时目录 / 用户目录 / 目标目录，并给出结论。
        /// 正常 Windows 上临时目录一定可写；连它都写不了，说明本程序运行在受限环境（沙箱）里。
        /// </summary>
        public static string Diagnose(string target)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Tensyan 安装诊断报告");
            sb.AppendLine("时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("安装程序版本：v" + Version + "（" + Publisher + "）");
            sb.AppendLine("安装程序位置：" + (System.Reflection.Assembly.GetEntryAssembly() != null
                ? System.Reflection.Assembly.GetEntryAssembly().Location : "(未知)"));
            sb.AppendLine("以管理员身份运行：" + (IsElevated() ? "是" : "否"));
            sb.AppendLine("系统：" + Environment.OSVersion + " / " + (Is64Bit() ? "64 位" : "32 位")
                + " / 用户：" + Environment.UserName);
            sb.AppendLine();
            sb.AppendLine("可写性探测：");

            bool tempOk = ProbeLine(sb, "临时目录", TempDir);
            bool localOk = ProbeLine(sb, "用户程序目录", LocalProgramsDir);
            bool targetOk = ProbeLine(sb, "目标目录", Blank(target) ? DefaultDir : target);

            // 目标所在磁盘剩余空间
            try
            {
                string root = Path.GetPathRoot(Blank(target) ? DefaultDir : target);
                if (!string.IsNullOrEmpty(root))
                {
                    var di = new DriveInfo(root);
                    if (di.IsReady)
                        sb.AppendLine(string.Format("  目标磁盘 {0} 类型={1} 剩余={2} MB", root, di.DriveType, di.AvailableFreeSpace / 1024 / 1024));
                    else
                        sb.AppendLine("  目标磁盘 " + root + " 未就绪（可能是光驱或未插入的移动盘）");
                }
            }
            catch (Exception ex) { sb.AppendLine("  目标磁盘信息读取失败：" + ex.Message); }

            sb.AppendLine();
            if (!localOk)
            {
                // 正常 Windows 账号对自己的 %LOCALAPPDATA% 一定有写权限；连它都写不了 → 受限环境
                sb.AppendLine("结论：**本程序运行在受限环境里**（连当前用户的 AppData 目录都写不进去）。");
                sb.AppendLine("      这通常是被某个工具/沙箱/容器代为启动，可写范围被限制在它自己的目录里；");
                sb.AppendLine("      也可能是整机被组策略/还原卡/杀软限制。");
                sb.AppendLine("      处理办法：在 Windows 资源管理器里找到本安装程序，直接双击运行（不要通过其它程序代跑），");
                sb.AppendLine("                或先把安装程序复制到桌面再双击；");
                sb.AppendLine("                便携版同理：把 Tensyan 文件夹放到可写位置后直接双击 Tensyan.exe。");
            }
            else if (!targetOk)
            {
                sb.AppendLine("结论：程序运行环境正常，只是**目标目录写不进去**。");
                sb.AppendLine("      处理办法：点“使用默认”换成用户目录，或点“以管理员身份重试”，");
                sb.AppendLine("                或换一个你自己有权限的文件夹（例如 D 盘下自己新建的目录）。");
            }
            else
            {
                sb.AppendLine("结论：环境正常、目标目录可写，可以直接安装。");
            }

            // 报告落盘：%TEMP% → 程序目录 → 桌面，取第一个能写的
            string report = sb.ToString();
            string saved = null;
            foreach (var p in new[]
            {
                Path.Combine(TempDir, "tensyan-setup-diag.txt"),
                SafeCombine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly() != null
                    ? System.Reflection.Assembly.GetEntryAssembly().Location : ""), "TensyanSetup-诊断.txt"),
                SafeCombine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "TensyanSetup-诊断.txt")
            })
            {
                if (string.IsNullOrEmpty(p)) continue;
                try { File.WriteAllText(p, report, Encoding.UTF8); saved = p; break; } catch { }
            }
            sb.AppendLine();
            sb.AppendLine(saved != null ? "（本报告已保存到：" + saved + "）" : "（本报告无法保存到磁盘，可用“复制全部”发给开发者）");
            report = sb.ToString();
            Log("诊断报告:\r\n" + report);
            return report;
        }

        private static string SafeCombine(string dir, string name)
        {
            try
            {
                if (string.IsNullOrEmpty(dir)) return null;
                return Path.Combine(dir, name);
            }
            catch { return null; }
        }

        /// <summary>以管理员身份重新启动自己（UAC 提示）。</summary>
        public static bool RestartElevated(string args)
        {
            try
            {
                string exe = System.Reflection.Assembly.GetEntryAssembly().Location;
                var psi = new ProcessStartInfo(exe, args) { UseShellExecute = true, Verb = "runas" };
                Process.Start(psi);
                Log("已请求以管理员身份重新启动：" + exe + " " + args);
                return true;
            }
            catch (Exception ex)
            {
                Log("提权重启失败: " + ex.Message);
                return false;
            }
        }

        public static long DirSizeKb(string dir)
        {
            long total = 0;
            try
            {
                foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try { total += new FileInfo(f).Length; } catch { }
                }
            }
            catch { }
            return total / 1024;
        }

        // ---------------- net35 兼容辅助（.NET 3.5 缺少这些 API）----------------

        private static string Combine3(string a, string b, string c)
        {
            return Path.Combine(Path.Combine(a, b), c);
        }

        /// <summary>供共享源码（如卸载器界面）调用的空字符串判断。</summary>
        public static bool IsBlank(string s) { return Blank(s); }

        private static bool Blank(string s)
        {
            if (s == null) return true;
            for (int i = 0; i < s.Length; i++) if (!char.IsWhiteSpace(s[i])) return false;
            return true;
        }

        private static bool Is64Bit()
        {
#if NET35
            return IntPtr.Size == 8;
#else
            return Environment.Is64BitOperatingSystem;
#endif
        }

        private static void DeleteKeySafe(RegistryKey root, string subKey)
        {
            try
            {
#if NET35
                root.DeleteSubKeyTree(subKey);
#else
                root.DeleteSubKeyTree(subKey, false);
#endif
            }
            catch { }
        }
    }
}
