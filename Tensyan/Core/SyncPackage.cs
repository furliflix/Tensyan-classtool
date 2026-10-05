using System;
using System.Collections.Generic;

namespace Tensyan.Core
{
    /// <summary>
    /// 同步包的结构（与手机端 Java 版**字段名完全一致**，两端可互读）。
    /// 传输方式无关：局域网、蓝牙、文件都送这一个包。
    /// </summary>
    public class SyncPackageDto
    {
        public string Format { get; set; } = "tensyan-sync";
        public int Version { get; set; } = 1;
        public string ClassId { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string SenderDeviceId { get; set; } = "";
        public string SenderName { get; set; } = "";
        public string SentAt { get; set; } = "";
        public int StudentCount { get; set; }
        public int EventCount { get; set; }
        public List<SyncStudent> Roster { get; set; } = new List<SyncStudent>();
        public List<SyncEvent> Events { get; set; } = new List<SyncEvent>();
    }

    public class SyncStudent
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string No { get; set; } = "";
        public string Gender { get; set; } = "";
        public string Note { get; set; } = "";
        public int Score { get; set; }
    }

    public class SyncEvent
    {
        public string Id { get; set; } = "";
        public string TimeUtc { get; set; } = "";
        public string ClassId { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string StudentId { get; set; } = "";
        public string StudentName { get; set; } = "";
        public int Delta { get; set; }
        public string Reason { get; set; } = "";
        public string Note { get; set; } = "";
        public string Operator { get; set; } = "";
        public string OperatorRole { get; set; } = "Admin";
        public bool Undone { get; set; }
    }

    /// <summary>同步包的打包与合并（与手机端 Java 版同一套规则）。</summary>
    public static class SyncMerge
    {
        public static SyncPackageDto Build(AppData data, SchoolClass cls, string deviceId, string deviceName)
        {
            var pkg = new SyncPackageDto
            {
                ClassId = cls.Id,
                ClassName = cls.Name,
                SenderDeviceId = deviceId,
                SenderName = deviceName,
                SentAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };
            foreach (var s in cls.Students)
            {
                if (s.Archived) continue;
                pkg.Roster.Add(new SyncStudent { Id = s.Id, Name = s.Name, No = s.No, Gender = s.Gender, Note = s.Note, Score = s.Score });
            }
            foreach (var e in data.Events)
            {
                if (e.ClassId != cls.Id) continue;
                pkg.Events.Add(new SyncEvent
                {
                    Id = e.Id,
                    TimeUtc = e.TimeUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    ClassId = e.ClassId,
                    ClassName = e.ClassName,
                    StudentId = e.StudentId,
                    StudentName = e.StudentName,
                    Delta = e.Delta,
                    Reason = e.Reason,
                    Note = e.Note,
                    Operator = e.Operator,
                    OperatorRole = e.OperatorRole.ToString(),
                    Undone = e.Undone
                });
            }
            pkg.StudentCount = pkg.Roster.Count;
            pkg.EventCount = pkg.Events.Count;
            return pkg;
        }

        /// <summary>把收到的包并入本地数据；返回一句人话总结。调用方负责 Save()。</summary>
        public static string Merge(AppData data, SyncPackageDto pkg)
        {
            if (pkg == null) throw new Exception("同步包为空");
            if (!string.IsNullOrEmpty(pkg.Format) && pkg.Format != "tensyan-sync") throw new Exception("包格式不对：" + pkg.Format);

            string classId = pkg.ClassId ?? "";
            if (classId.Length == 0) throw new Exception("包内没有班级 ID");

            // 1) 班级：本地没有就整班带过来
            SchoolClass cls = data.FindClass(classId);
            bool newClass = false;
            if (cls == null)
            {
                cls = new SchoolClass { Name = string.IsNullOrEmpty(pkg.ClassName) ? "同步过来的班级" : pkg.ClassName };
                cls.Id = classId;
                data.Classes.Add(cls);
                newClass = true;
            }

            // 2) 名册：按 Id 补缺（不覆盖本地已改过的姓名）
            int addedStudents = 0;
            var have = new HashSet<string>();
            foreach (var s in cls.Students) have.Add(s.Id);
            if (pkg.Roster != null)
            {
                foreach (var rs in pkg.Roster)
                {
                    if (string.IsNullOrEmpty(rs.Id) || have.Contains(rs.Id)) continue;
                    cls.Students.Add(new Student { Id = rs.Id, Name = rs.Name, No = rs.No, Gender = rs.Gender, Note = rs.Note });
                    have.Add(rs.Id);
                    addedStudents++;
                }
            }

            // 3) 事件：按 Id 取并集（幂等，永不冲突）
            var known = new HashSet<string>();
            foreach (var e in data.Events) known.Add(e.Id);
            int addedEvents = 0, mergedUndone = 0;
            if (pkg.Events != null)
            {
                foreach (var re in pkg.Events)
                {
                    if (string.IsNullOrEmpty(re.Id)) continue;
                    if (known.Contains(re.Id))
                    {
                        if (re.Undone)
                        {
                            foreach (var e in data.Events)
                                if (e.Id == re.Id && !e.Undone) { e.Undone = true; mergedUndone++; break; }
                        }
                        continue;
                    }
                    DateTime t;
                    if (!DateTime.TryParse(re.TimeUtc, out t)) t = DateTime.UtcNow;
                    data.Events.Add(new ScoreEvent
                    {
                        Id = re.Id,
                        TimeUtc = t,
                        ClassId = classId,
                        ClassName = cls.Name,
                        StudentId = re.StudentId,
                        StudentName = re.StudentName,
                        Delta = re.Delta,
                        Reason = re.Reason,
                        Note = re.Note,
                        Operator = re.Operator,
                        OperatorRole = ParseRole(re.OperatorRole),
                        Undone = re.Undone
                    });
                    known.Add(re.Id);
                    addedEvents++;
                }
            }

            // 4) 分数按事件重算（两端不再各记一份）
            RecomputeScores(data, cls);
            data.Settings.LastClassId = cls.Id;

            string msg = (newClass ? "新增班级「" + cls.Name + "」；" : "")
                + "新增学生 " + addedStudents + " 名；新增记录 " + addedEvents + " 条"
                + (mergedUndone > 0 ? "；合并撤销 " + mergedUndone + " 条" : "");
            return msg;
        }

        public static void RecomputeScores(AppData data, SchoolClass cls)
        {
            foreach (var s in cls.Students) s.Score = 0;
            foreach (var e in data.Events)
            {
                if (e.ClassId != cls.Id || e.Undone) continue;
                foreach (var s in cls.Students)
                    if (s.Id == e.StudentId) { s.Score += e.Delta; break; }
            }
        }

        private static Role ParseRole(string s)
        {
            try { return (Role)Enum.Parse(typeof(Role), string.IsNullOrEmpty(s) ? "Admin" : s); }
            catch { return Role.Admin; }
        }
    }
}
