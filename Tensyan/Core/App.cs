using System;
using System.Collections.Generic;

namespace Tensyan.Core
{
    /// <summary>应用控制器：持有数据仓库与当前会话，供各页面调用。</summary>
    public class App
    {
        public static App I;

        public Store Store;
        public Session Session = Session.Guest();
        public SchoolClass Current;

        public event EventHandler DataChanged;
        public event EventHandler ClassSwitched;

        public App(Store store)
        {
            Store = store;
            I = this;
            string last = store.Data.Settings.LastClassId;
            Current = store.Data.FindClass(last);
            if (Current == null && store.Data.Classes.Count > 0) Current = store.Data.Classes[0];
        }

        public AppData Data { get { return Store.Data; } }
        public Settings Settings { get { return Store.Data.Settings; } }

        public void Raise()
        {
            Store.Save();
            var h = DataChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        public void SwitchClass(SchoolClass c)
        {
            Current = c;
            if (c != null) { Settings.LastClassId = c.Id; Store.Save(); }
            var h = ClassSwitched;
            if (h != null) h(this, EventArgs.Empty);
        }

        /// <summary>当前身份能看到的班级。</summary>
        public List<SchoolClass> VisibleClasses()
        {
            var all = Data.Classes;
            if (Session.Role == Role.Member && Session.Account != null && Session.Account.ClassIds != null && Session.Account.ClassIds.Count > 0)
            {
                var list = new List<SchoolClass>();
                foreach (var c in all)
                    if (Session.Account.ClassIds.Contains(c.Id)) list.Add(c);
                if (list.Count > 0) return list;
            }
            return all;
        }

        public bool CanSeeClass(SchoolClass c)
        {
            if (c == null) return false;
            if (Session.Role != Role.Member) return true;
            var acc = Session.Account;
            if (acc == null || acc.ClassIds == null || acc.ClassIds.Count == 0) return true;
            return acc.ClassIds.Contains(c.Id);
        }

        public ScoreEvent Score(Student st, int delta, string reason, string note)
        {
            if (st == null || Current == null) return null;
            if (delta > 0 && !Session.CanAddScore) return null;
            if (delta < 0 && !Session.CanSubScore) return null;
            var ev = Store.AddEvent(Current, st, delta, reason, note, Session);
            Raise();
            return ev;
        }

        public bool Undo(out string msg)
        {
            msg = "";
            if (!Session.IsAdmin) { msg = "只有管理员可以撤销操作"; return false; }
            ScoreEvent ev;
            if (!Store.UndoLast(Current, out ev)) { msg = "没有可撤销的记录"; return false; }
            msg = "已撤销：" + ev.StudentName + " " + (ev.Delta > 0 ? "+" : "") + ev.Delta;
            Raise();
            return true;
        }

        public Student FindStudent(string id) { return Data.FindStudent(id); }

        public string ClassNameOf(string classId)
        {
            var c = Data.FindClass(classId);
            return c == null ? "" : c.Name;
        }

        public int Rank(Student st)
        {
            if (Current == null) return 0;
            int rank = 1;
            foreach (var s in Current.Students)
            {
                if (s.Archived || s.Id == st.Id) continue;
                if (s.Score > st.Score) rank++;
            }
            return rank;
        }

        // ---------------- 示例数据（首次试用 / 截图自检） ----------------

        public static readonly string[] DemoNames =
        {
            "张一鸣","王思远","李佳琪","刘子豪","陈雨萱","杨博文","赵梦洁","黄俊杰","周欣怡","吴天宇",
            "徐若涵","孙浩然","马诗涵","朱明轩","胡雅静","郭子涵","林嘉怡","何思睿","高晨阳","罗兰心",
            "郑亦凡","唐雨桐","冯子墨","邓可欣","曹宇航","彭嘉悦","许安然","韩泽宇"
        };

        public void SeedDemo(int count = 28, int classCount = 2)
        {
            var rnd = new Random(20260318);
            string[] classNames = { "三(2)班", "五(1)班" };
            string[] teachers = { "李老师", "王老师" };

            Data.Classes.Clear();
            Data.Events.Clear();

            for (int ci = 0; ci < classCount; ci++)
            {
                var cls = new SchoolClass
                {
                    Name = classNames[ci % classNames.Length],
                    Grade = ci == 0 ? "三年级" : "五年级",
                    Teacher = teachers[ci % teachers.Length]
                };
                int n = ci == 0 ? count : Math.Max(8, count - 8);
                for (int i = 0; i < n; i++)
                {
                    string name = DemoNames[i % DemoNames.Length];
                    var st = new Student
                    {
                        Name = name,
                        Gender = i % 2 == 0 ? "男" : "女",
                        No = (i + 1).ToString("00"),
                        Score = 2 + rnd.Next(0, 26)
                    };
                    cls.Students.Add(st);

                    int evCount = rnd.Next(2, 7);
                    for (int k = 0; k < evCount; k++)
                    {
                        int delta = rnd.Next(0, 100) < 78 ? 1 + rnd.Next(0, 2) : -(1 + rnd.Next(0, 1));
                        var tags = delta > 0 ? Data.Settings.AddTags : Data.Settings.SubTags;
                        string reason = tags.Count > 0 ? tags[rnd.Next(tags.Count)].Text : (delta > 0 ? "表现优秀" : "需要改进");
                        Data.Events.Add(new ScoreEvent
                        {
                            TimeUtc = DateTime.UtcNow.AddDays(-rnd.Next(0, 13)).AddMinutes(-rnd.Next(0, 600)),
                            ClassId = cls.Id,
                            ClassName = cls.Name,
                            StudentId = st.Id,
                            StudentName = st.Name,
                            Delta = delta,
                            Reason = reason,
                            Operator = ci == 0 ? "李老师" : "王老师",
                            OperatorRole = Role.Admin
                        });
                    }
                }
                Data.Classes.Add(cls);
            }

            Data.Events.Sort((a, b) => a.TimeUtc.CompareTo(b.TimeUtc));
            Settings.LastClassId = Data.Classes[0].Id;
            Current = Data.Classes[0];
            Store.Save();
        }
    }
}
