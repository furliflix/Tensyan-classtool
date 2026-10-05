using System;
using System.Collections.Generic;

namespace Tensyan.Core
{
    /// <summary>登录模式：访客只读 / 成员仅加分 / 管理员全部权限。</summary>
    public enum Role
    {
        Guest = 0,
        Member = 1,
        Admin = 2
    }

    public static class RoleText
    {
        public static string Name(Role r)
        {
            switch (r)
            {
                case Role.Admin: return "管理员";
                case Role.Member: return "成员";
                default: return "访客";
            }
        }

        public static string Desc(Role r)
        {
            switch (r)
            {
                case Role.Admin: return "全部权限：加减分、管理班级与学生、账号与登录U盘、导出数据";
                case Role.Member: return "仅加分：可以给学生加分，不能扣分，不能修改班级与学生";
                default: return "只读：可以查看积分、排行、记录与统计，不能做任何修改";
            }
        }
    }

    public class Student
    {
        public string Id { get; set; } = Ids.New();
        public string Name { get; set; } = "";
        public int Score { get; set; }
        public string Gender { get; set; } = "";      // 男 / 女 / 空
        public string No { get; set; } = "";           // 学号 / 座号
        public string Note { get; set; } = "";
        public bool Archived { get; set; }
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }

    public class SchoolClass
    {
        public string Id { get; set; } = Ids.New();
        public string Name { get; set; } = "";
        public string Grade { get; set; } = "";
        public string Teacher { get; set; } = "";
        public List<Student> Students { get; set; } = new List<Student>();
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        public int TotalScore()
        {
            int s = 0;
            foreach (var st in Students) if (!st.Archived) s += st.Score;
            return s;
        }
    }

    public class ScoreEvent
    {
        public string Id { get; set; } = Ids.New();
        public DateTime TimeUtc { get; set; } = DateTime.UtcNow;
        public string ClassId { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string StudentId { get; set; } = "";
        public string StudentName { get; set; } = "";
        public int Delta { get; set; }
        public string Reason { get; set; } = "";
        public string Note { get; set; } = "";
        public string Operator { get; set; } = "";
        public Role OperatorRole { get; set; } = Role.Admin;
        public bool Undone { get; set; }
    }

    /// <summary>加减分理由标签。</summary>
    public class ReasonTag
    {
        public string Text { get; set; } = "";
        public int Points { get; set; } = 1;

        public ReasonTag() { }
        public ReasonTag(string text, int points) { Text = text; Points = points; }
    }

    public class Settings
    {
        public string SchoolName { get; set; } = "";
        public bool SoundEnabled { get; set; } = true;
        public bool AnimationEnabled { get; set; } = true;
        public int BackupKeep { get; set; } = 20;
        /// <summary>仅允许管理员扣分（成员模式下忽略）。</summary>
        public bool MemberCanSeeAllClasses { get; set; } = true;
        /// <summary>注册新账号是否需要管理员登录U盘认证（默认需要）。</summary>
        public bool RegisterNeedsAdminUsb { get; set; } = true;
        public string LastClassId { get; set; } = "";
        public bool FirstRunDone { get; set; }
        public List<ReasonTag> AddTags { get; set; } = new List<ReasonTag>();
        public List<ReasonTag> SubTags { get; set; } = new List<ReasonTag>();
        public List<int> QuickAdd = new List<int> { 1, 2, 3, 5 };
        public List<int> QuickSub = new List<int> { 1, 2, 3, 5 };
    }

    public class AppData
    {
        public int Version { get; set; } = 1;
        public List<SchoolClass> Classes { get; set; } = new List<SchoolClass>();
        public List<ScoreEvent> Events { get; set; } = new List<ScoreEvent>();
        public Settings Settings { get; set; } = new Settings();

        public SchoolClass FindClass(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var c in Classes) if (c.Id == id) return c;
            return null;
        }

        public Student FindStudent(string id)
        {
            foreach (var c in Classes)
                foreach (var s in c.Students)
                    if (s.Id == id) return s;
            return null;
        }
    }

    public class Account
    {
        public string Id { get; set; } = Ids.New();
        public string User { get; set; } = "";
        public string Display { get; set; } = "";
        public string Salt { get; set; } = "";
        public string PwdHash { get; set; } = "";
        public Role Role { get; set; } = Role.Member;
        public bool Enabled { get; set; } = true;
        public bool MustChangePwd { get; set; }
        /// <summary>该成员账号可见的班级 Id；为空表示全部班级。</summary>
        public List<string> ClassIds { get; set; } = new List<string>();
        public DateTime? LastLoginUtc { get; set; }
        public string Note { get; set; } = "";
    }

    public static class Ids
    {
        public static string New()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 10);
        }
    }

    /// <summary>当前会话身份。</summary>
    public class Session
    {
        public Role Role { get; set; } = Role.Guest;
        public string User { get; set; } = "guest";
        public string Display { get; set; } = "访客";
        public Account Account { get; set; }
        public string LoginMethod { get; set; } = "";   // 密码 / U盘 / 访客
        public string UsbDrive { get; set; } = "";

        public bool CanAddScore { get { return Role == Role.Admin || Role == Role.Member; } }
        public bool CanSubScore { get { return Role == Role.Admin; } }
        public bool CanManage { get { return Role == Role.Admin; } }
        public bool IsGuest { get { return Role == Role.Guest; } }
        public bool IsAdmin { get { return Role == Role.Admin; } }

        public static Session Guest()
        {
            return new Session { Role = Role.Guest, User = "guest", Display = "访客", LoginMethod = "访客" };
        }
    }
}
