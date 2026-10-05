using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
#if !NETFRAMEWORK
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
#endif

namespace Tensyan.Core
{
#if NETFRAMEWORK
    // 32 位（.NET Framework）版使用 Tensyan x86\Compat\JsonCompat.cs 里的等价实现，
    // 输出格式与下面这份 System.Text.Json 配置完全一致，两个版本可共用同一份数据文件。
#else
    /// <summary>全局 JSON 序列化配置。</summary>
    public static class Json
    {
        public static readonly JsonSerializerOptions Opt = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Converters = { new JsonStringEnumConverter() },
            PropertyNameCaseInsensitive = true
        };

        public static string Dump(object o) { return JsonSerializer.Serialize(o, Opt); }

        public static T Parse<T>(string s)
        {
            return JsonSerializer.Deserialize<T>(s, Opt);
        }
    }
#endif

    /// <summary>程序目录 / 数据目录定位。默认绿色便携：exe 同级 data 目录。</summary>
    public static class Paths
    {
        public static string AppDir { get; private set; }
        public static string DataDir { get; private set; }
        public static bool PortableData { get; private set; }
        public static string BackupDir { get { return Path.Combine(DataDir, "backups"); } }
        public static string LogFile { get { return Path.Combine(DataDir, "tensyan.log"); } }
        public static string DataFile { get { return Path.Combine(DataDir, "classdata.json"); } }
        public static string AccountFile { get { return Path.Combine(DataDir, "accounts.json"); } }
        public static string InstallFile { get { return Path.Combine(DataDir, "install.json"); } }

        /// <summary>数据目录是被“降级/兜底”使用时的原因说明，界面需要提示用户。</summary>
        public static string WarningReason { get; private set; }

        private const string PointerName = "data-location.txt";

        /// <summary>用户自定义数据位置：优先程序目录旁的指针文件，其次用户目录。</summary>
        public static string PointerFile { get { return Path.Combine(AppDir, PointerName); } }
        public static string PointerFileFallback
        {
            get { return PathCompat.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tensyan", PointerName); }
        }

        public static void Init(string overrideDataDir = null)
        {
            AppDir = AppInfo.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            WarningReason = "";

            string chosen = overrideDataDir ?? Environment.GetEnvironmentVariable("TENSYAN_DATA");
            bool portable = false;

            // 0) 显式指定（命令行/环境变量）：不可写时不能硬用，否则数据全存不进去
            if (!Str.Blank(chosen) && !CanWrite(chosen))
            {
                WarningReason = "指定的数据目录不可写，已自动改用其它位置：" + chosen;
                chosen = null;
            }

            // 1) 用户在“系统设置 → 数据目录”里指定的位置
            if (Str.Blank(chosen))
            {
                string ptr = ReadPointer();
                if (!Str.Blank(ptr))
                {
                    if (CanWrite(ptr)) chosen = ptr;
                    else WarningReason = "自定义数据目录不可写，已忽略：" + ptr;
                }
            }

            // 2) 绿色便携：exe 同级 data
            if (Str.Blank(chosen))
            {
                string portableDir = Path.Combine(AppDir, "data");
                if (CanWrite(portableDir)) { chosen = portableDir; portable = true; }
            }

            // 3) 程序目录不可写（Program Files / 只读盘）→ 用户目录
            if (Str.Blank(chosen))
            {
                string roaming = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tensyan");
                if (CanWrite(roaming))
                {
                    chosen = roaming;
                    WarningReason = "程序所在目录不可写（可能是 Program Files 或只读盘），数据已改存到用户目录：" + roaming;
                }
            }

            // 4) 兜底：临时目录，保证程序仍然能用
            if (Str.Blank(chosen))
            {
                string temp = Path.Combine(Path.GetTempPath(), "Tensyan");
                try { Directory.CreateDirectory(temp); } catch { }
                chosen = temp;
                WarningReason = "程序目录和用户目录都不可写，数据只能暂存在临时目录（重启后可能被系统清理）：" + temp;
            }

            DataDir = chosen;
            PortableData = portable;
            try { Directory.CreateDirectory(DataDir); } catch { }
            try { Directory.CreateDirectory(BackupDir); } catch { }
            if (!string.IsNullOrEmpty(WarningReason)) Log("数据目录提示：" + WarningReason);
        }

        private static string ReadPointer()
        {
            foreach (var f in new[] { PointerFile, PointerFileFallback })
            {
                try
                {
                    if (File.Exists(f))
                    {
                        string t = File.ReadAllText(f).Trim();
                        if (t.Length > 0) return t;
                    }
                }
                catch { }
            }
            return null;
        }

        /// <summary>记住自定义数据目录（写指针文件，重启后仍生效）。</summary>
        public static bool WritePointer(string dir)
        {
            foreach (var f in new[] { PointerFile, PointerFileFallback })
            {
                try
                {
                    string parent = System.IO.Path.GetDirectoryName(f);
                    if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                    File.WriteAllText(f, dir);
                    Log("已记录数据目录：" + dir + " → " + f);
                    return true;
                }
                catch { }
            }
            Log("无法记录数据目录指针：" + dir);
            return false;
        }

        public static void ClearPointer()
        {
            foreach (var f in new[] { PointerFile, PointerFileFallback })
            {
                try { if (File.Exists(f)) File.Delete(f); } catch { }
            }
        }

        /// <summary>
        /// 可写性探测：只要**能创建并写入**就算可写。
        /// 注意不能把“删除失败”当成不可写 —— 受管环境常常禁止删除/改名，
        /// 但写入是允许的，早先的写法会误判成“哪里都写不了”。
        /// </summary>
        public static bool CanWrite(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                string probe = System.IO.Path.Combine(dir, ".wtest-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                File.WriteAllText(probe, "ok");
                try { File.Delete(probe); }
                catch { Log("提示：目录 " + dir + " 允许写入但不允许删除，按可写处理"); }
                return true;
            }
            catch { return false; }
        }

        public static void Log(string msg)
        {
            try
            {
                File.AppendAllText(LogFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }
    }

    /// <summary>本地数据仓库：班级/学生/积分记录/设置 + 账号 + 安装密钥。</summary>
    public class Store
    {
        public AppData Data { get; set; } = new AppData();
        public List<Account> Accounts { get; set; } = new List<Account>();
        public InstallInfo Install { get; set; } = new InstallInfo();

        /// <summary>启动时数据文件有问题（损坏/无法解析）的提示，界面应显示出来。</summary>
        public string LoadWarning { get; private set; } = "";

        private DateTime _lastBackup = DateTime.MinValue;
        private readonly object _lock = new object();

        public class InstallInfo
        {
            public string InstallId { get; set; } = "";
            public string Secret { get; set; } = "";
            public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        }

        public static Store Load()
        {
            var s = new Store();
            try
            {
                if (File.Exists(Paths.DataFile))
                    s.Data = Json.Parse<AppData>(File.ReadAllText(Paths.DataFile, Encoding.UTF8)) ?? new AppData();
            }
            catch (Exception ex)
            {
                Paths.Log("读取班级数据失败: " + ex.Message);
                s.Quarantine(Paths.DataFile, "班级数据");
                s.Data = new AppData();
            }

            try
            {
                if (File.Exists(Paths.AccountFile))
                    s.Accounts = Json.Parse<List<Account>>(File.ReadAllText(Paths.AccountFile, Encoding.UTF8)) ?? new List<Account>();
            }
            catch (Exception ex)
            {
                Paths.Log("读取账号数据失败: " + ex.Message);
                s.Quarantine(Paths.AccountFile, "账号数据");
                s.Accounts = new List<Account>();
            }

            try
            {
                if (File.Exists(Paths.InstallFile))
                    s.Install = Json.Parse<InstallInfo>(File.ReadAllText(Paths.InstallFile, Encoding.UTF8)) ?? new InstallInfo();
            }
            catch (Exception ex) { Paths.Log("读取安装信息失败: " + ex.Message); }

            if (string.IsNullOrEmpty(s.Install.InstallId))
            {
                s.Install.InstallId = Guid.NewGuid().ToString("N");
                s.Install.Secret = Convert.ToBase64String(Auth.RandomBytes(32));
                s.Install.CreatedUtc = DateTime.UtcNow;
                s.SaveInstall();
            }

            s.SeedIfEmpty();
            return s;
        }

        /// <summary>把无法解析的数据文件挪到 backups 里留底（改名而不是删除），并记下提示。</summary>
        private void Quarantine(string path, string what)
        {
            try
            {
                Directory.CreateDirectory(Paths.BackupDir);
                string target = Path.Combine(Paths.BackupDir,
                    Path.GetFileNameWithoutExtension(path) + ".broken-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
                File.Copy(path, target, true);
                LoadWarning = string.IsNullOrEmpty(LoadWarning)
                    ? what + "文件无法解析，已备份为 " + target + "，程序按空数据启动。"
                    : LoadWarning + " " + what + "文件也无法解析，已备份。";
                Paths.Log("数据文件损坏，已备份：" + target);
            }
            catch (Exception ex)
            {
                Paths.Log("备份损坏文件失败: " + ex.Message);
                LoadWarning = what + "文件无法解析，且备份失败，请手动检查 " + path;
            }
        }

        private void SeedIfEmpty()
        {
            bool dirty = false;

            if (Data.Settings == null) { Data.Settings = new Settings(); dirty = true; }
            var st = Data.Settings;

            if (st.AddTags == null || st.AddTags.Count == 0)
            {
                st.AddTags = new List<ReasonTag>
                {
                    new ReasonTag("积极回答", 1),
                    new ReasonTag("作业优秀", 2),
                    new ReasonTag("认真听讲", 1),
                    new ReasonTag("帮助同学", 2),
                    new ReasonTag("小组合作", 1),
                    new ReasonTag("进步明显", 3),
                    new ReasonTag("值日负责", 2),
                    new ReasonTag("课堂纪律好", 1)
                };
                dirty = true;
            }
            if (st.SubTags == null || st.SubTags.Count == 0)
            {
                st.SubTags = new List<ReasonTag>
                {
                    new ReasonTag("未交作业", 2),
                    new ReasonTag("上课讲话", 1),
                    new ReasonTag("迟到", 1),
                    new ReasonTag("打闹", 2),
                    new ReasonTag("不专心", 1),
                    new ReasonTag("影响他人", 2)
                };
                dirty = true;
            }
            if (st.QuickAdd == null || st.QuickAdd.Count == 0) { st.QuickAdd = new List<int> { 1, 2, 3, 5 }; dirty = true; }
            if (st.QuickSub == null || st.QuickSub.Count == 0) { st.QuickSub = new List<int> { 1, 2, 3, 5 }; dirty = true; }

            if (Data.Classes.Count == 0)
            {
                var c = new SchoolClass { Name = "我的班级", Grade = "", Teacher = "" };
                Data.Classes.Add(c);
                Data.Settings.LastClassId = c.Id;
                dirty = true;
            }

            Save();

            if (Data.Settings.FirstRunDone)
            {
                // 已有数据：无需额外动作，保存只用来补全字段
            }
        }

        public void Save()
        {
            lock (_lock)
            {
                try
                {
                    WriteAtomic(Paths.DataFile, Json.Dump(Data));
                    WriteAtomic(Paths.AccountFile, Json.Dump(Accounts));
                    Backup(false);
                    if (LastSaveError.Length > 0) Paths.Log("数据保存已恢复正常");
                    LastSaveError = "";
                }
                catch (Exception ex)
                {
                    LastSaveError = ex.Message;
                    Paths.Log("保存失败: " + ex.Message);
                }
            }
        }

        /// <summary>最近一次保存失败的原因（界面需要提示用户，而不是只写日志）。</summary>
        public string LastSaveError { get; private set; } = "";

        public void SaveInstall()
        {
            try { WriteAtomic(Paths.InstallFile, Json.Dump(Install)); }
            catch (Exception ex) { Paths.Log("保存安装信息失败: " + ex.Message); }
        }

        /// <summary>手动备份（管理员点“立即备份”时）。</summary>
        public string Backup(bool force)
        {
            try
            {
                if (!force && (DateTime.Now - _lastBackup).TotalMinutes < 5) return null;
                _lastBackup = DateTime.Now;
                Directory.CreateDirectory(Paths.BackupDir);
                string name = "classdata-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json";
                string target = Path.Combine(Paths.BackupDir, name);
                File.WriteAllText(target, Json.Dump(Data), Encoding.UTF8);

                int keep = Math.Max(3, Data.Settings.BackupKeep);
                var files = new List<FileInfo>(new DirectoryInfo(Paths.BackupDir).GetFiles("classdata-*.json"));
                files.Sort((a, b) => b.LastWriteTime.CompareTo(a.LastWriteTime));
                for (int i = keep; i < files.Count; i++)
                {
                    try { files[i].Delete(); } catch { }
                }
                return target;
            }
            catch (Exception ex) { Paths.Log("备份失败: " + ex.Message); return null; }
        }

        /// <summary>
        /// 原子写入：优先“临时文件 + 替换”，但受管环境可能禁止删除/改名，
        /// 因此逐级降级，最后直接覆盖写目标文件（只需创建/写入权限）。
        /// </summary>
        private static void WriteAtomic(string path, string content)
        {
            string tmp = path + ".tmp";
            bool wroteTmp = false;
            try
            {
                File.WriteAllText(tmp, content, new UTF8Encoding(false));
                wroteTmp = true;
            }
            catch (Exception ex) { Paths.Log("写临时文件失败（将直接写入目标文件）: " + ex.Message); }

            if (wroteTmp)
            {
                if (File.Exists(path))
                {
                    try { File.Replace(tmp, path, null); return; }
                    catch { }
                    try
                    {
                        // 覆盖写：不需要删除权限
                        File.Copy(tmp, path, true);
                        try { File.Delete(tmp); } catch { }
                        return;
                    }
                    catch { }
                }
                else
                {
                    try { File.Move(tmp, path); return; }
                    catch { }
                }
            }

            // 最终兜底：直接写目标文件
            File.WriteAllText(path, content, new UTF8Encoding(false));
            if (wroteTmp) { try { File.Delete(tmp); } catch { } }
        }

        /// <summary>添加一条积分变动，返回是否成功。</summary>
        public ScoreEvent AddEvent(SchoolClass cls, Student stu, int delta, string reason, string note, Session session)
        {
            var ev = new ScoreEvent
            {
                ClassId = cls.Id,
                ClassName = cls.Name,
                StudentId = stu.Id,
                StudentName = stu.Name,
                Delta = delta,
                Reason = reason ?? "",
                Note = note ?? "",
                Operator = session.Display,
                OperatorRole = session.Role
            };
            stu.Score += delta;
            Data.Events.Add(ev);

            int cap = 50000;
            if (Data.Events.Count > cap)
                Data.Events.RemoveRange(0, Data.Events.Count - cap);
            return ev;
        }

        public bool UndoLast(SchoolClass cls, out ScoreEvent undone)
        {
            undone = null;
            for (int i = Data.Events.Count - 1; i >= 0; i--)
            {
                var e = Data.Events[i];
                if (e.Undone) continue;
                if (cls != null && e.ClassId != cls.Id) continue;
                var stu = Data.FindStudent(e.StudentId);
                if (stu != null) stu.Score -= e.Delta;
                e.Undone = true;
                undone = e;
                return true;
            }
            return false;
        }

        public List<ScoreEvent> EventsOf(string classId)
        {
            var list = new List<ScoreEvent>();
            for (int i = Data.Events.Count - 1; i >= 0; i--)
            {
                var e = Data.Events[i];
                if (e.Undone) continue;
                if (!string.IsNullOrEmpty(classId) && e.ClassId != classId) continue;
                list.Add(e);
            }
            return list;
        }
    }
}
