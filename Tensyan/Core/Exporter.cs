using System;
using System.Collections.Generic;
using System.IO;

namespace Tensyan.Core
{
    /// <summary>Excel 导出（零依赖 XLSX）。</summary>
    public static class Exporter
    {
        public static string DefaultFileName(string prefix)
        {
            return prefix + "-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".xlsx";
        }

        /// <summary>导出某个班级的积分榜。</summary>
        public static bool ExportClassScores(AppData data, SchoolClass cls, string path, out string msg)
        {
            msg = "";
            if (cls == null) { msg = "没有可导出的班级"; return false; }

            var list = new List<Student>();
            foreach (var s in cls.Students) if (!s.Archived) list.Add(s);
            list.Sort((a, b) => b.Score.CompareTo(a.Score));

            var rows = new List<string[]>();
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                rows.Add(new[]
                {
                    (i + 1).ToString(),
                    s.Name,
                    s.No,
                    s.Gender,
                    s.Score.ToString(),
                    s.Note
                });
            }

            string title = (data.Settings.SchoolName + " " + cls.Name).Trim() + " 积分排行  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            bool ok = Xlsx.Write(path, cls.Name,
                new[] { "排名", "姓名", "学号", "性别", "积分", "备注" },
                rows, new[] { 8, 14, 10, 8, 10, 24 }, title);

            msg = ok ? ("已导出 " + list.Count + " 名学生的积分到 " + path) : "导出失败，请检查文件是否被占用";
            return ok;
        }

        /// <summary>导出加分记录。</summary>
        public static bool ExportEvents(AppData data, SchoolClass cls, List<ScoreEvent> events, string path, out string msg)
        {
            msg = "";
            var rows = new List<string[]>();
            foreach (var e in events)
            {
                rows.Add(new[]
                {
                    e.TimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                    e.ClassName,
                    e.StudentName,
                    (e.Delta > 0 ? "+" : "") + e.Delta,
                    e.Reason,
                    e.Operator,
                    RoleText.Name(e.OperatorRole),
                    e.Note
                });
            }

            string title = (cls == null ? "全部班级" : cls.Name) + " 积分记录  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            bool ok = Xlsx.Write(path, "积分记录",
                new[] { "时间", "班级", "学生", "变动", "理由", "操作人", "身份", "备注" },
                rows, new[] { 20, 14, 12, 8, 16, 12, 10, 24 }, title);

            msg = ok ? ("已导出 " + rows.Count + " 条记录到 " + path) : "导出失败，请检查文件是否被占用";
            return ok;
        }

        /// <summary>导出全部班级总表。</summary>
        public static bool ExportAll(AppData data, string path, out string msg)
        {
            msg = "";
            var rows = new List<string[]>();
            foreach (var c in data.Classes)
            {
                foreach (var s in c.Students)
                {
                    if (s.Archived) continue;
                    rows.Add(new[] { c.Name, c.Grade, c.Teacher, s.Name, s.No, s.Gender, s.Score.ToString() });
                }
            }
            bool ok = Xlsx.Write(path, "全部班级",
                new[] { "班级", "年级", "教师", "姓名", "学号", "性别", "积分" },
                rows, new[] { 16, 10, 12, 14, 10, 8, 10 },
                data.Settings.SchoolName + " 全部班级积分总表  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            msg = ok ? ("已导出 " + rows.Count + " 条学生数据到 " + path) : "导出失败，请检查文件是否被占用";
            return ok;
        }

        /// <summary>导出学生名单模板（用于批量导入前准备）。</summary>
        public static bool ExportRoster(SchoolClass cls, string path, out string msg)
        {
            msg = "";
            var rows = new List<string[]>();
            if (cls != null)
                foreach (var s in cls.Students)
                    rows.Add(new[] { s.Name, s.Gender, s.No, s.Score.ToString(), s.Note });
            bool ok = Xlsx.Write(path, "学生名单",
                new[] { "姓名", "性别", "学号", "积分", "备注" }, rows, new[] { 14, 8, 10, 10, 24 },
                (cls == null ? "" : cls.Name + " ") + "学生名单  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            msg = ok ? ("已导出 " + rows.Count + " 名学生到 " + path) : "导出失败";
            return ok;
        }
    }
}
