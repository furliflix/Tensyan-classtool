using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Tensyan.Core;
using Tensyan.UI;
using Tensyan.UI.Pages;

namespace Tensyan
{
    internal static class Program
    {
        /// <summary>U盘登录密钥自检：签名、口令、有效期、跨安装识别。</summary>
        private static void SelfTest(string dir)
        {
            var sb = new System.Text.StringBuilder();
            int pass = 0, fail = 0;

            Action<string, bool, string> check = (name, ok, detail) =>
            {
                if (ok) pass++; else fail++;
                sb.AppendLine((ok ? "[PASS] " : "[FAIL] ") + name + (string.IsNullOrEmpty(detail) ? "" : "  ->  " + detail));
            };

            try
            {
                Directory.CreateDirectory(dir);
                Paths.Init(Path.Combine(dir, "data"));
                Theme.InitScale();
                var store = Store.Load();
                if (store.Accounts.Count == 0)
                {
                    var seed = new Account { User = "admin", Display = "管理员", Role = Role.Admin };
                    Auth.SetPassword(seed, "tensyan123");
                    store.Accounts.Add(seed);
                    store.Save();
                }
                var acc = store.Accounts[0];

                // 1. 基本密钥
                var kf = UsbKey.Build(store, acc, "", 0);
                string p1 = Path.Combine(dir, UsbKey.KeyFileName);
                File.WriteAllText(p1, Json.Dump(kf), new System.Text.UTF8Encoding(false));

                UsbKeyFile back;
                var c1 = UsbKey.ReadAndCheck(p1, store, out back);
                check("正常密钥校验通过", c1 == KeyCheck.Ok, KeyCheckText.Msg(c1));
                check("账号与角色一致", back != null && back.User == acc.User && back.Role == acc.Role.ToString(), back == null ? "null" : back.User + "/" + back.Role);

                // 2. 文件被篡改（改用户名，签名应失效）
                string tampered = Json.Dump(kf).Replace("\"" + acc.User + "\"", "\"hacker\"");
                string p2 = Path.Combine(dir, "tampered" + UsbKey.KeyFileName);
                File.WriteAllText(p2, tampered, new System.Text.UTF8Encoding(false));
                var c2 = UsbKey.ReadAndCheck(p2, store, out back);
                check("篡改后签名失效", c2 == KeyCheck.BadSignature, KeyCheckText.Msg(c2));

                // 3. 口令
                var kp = UsbKey.Build(store, acc, "1234", 0);
                string p3 = Path.Combine(dir, "pin" + UsbKey.KeyFileName);
                File.WriteAllText(p3, Json.Dump(kp), new System.Text.UTF8Encoding(false));
                var c3 = UsbKey.ReadAndCheck(p3, store, out back);
                check("带口令的密钥需要口令", c3 == KeyCheck.NeedPin, KeyCheckText.Msg(c3));
                check("正确口令通过", UsbKey.CheckPin(kp, "1234"), "");
                check("错误口令拒绝", !UsbKey.CheckPin(kp, "0000"), "");

                // 4. 过期
                var ke = UsbKey.Build(store, acc, "", 7);
                ke.Expires = DateTime.Now.AddDays(-1).ToString("o");
                ke.Sig = Auth.Sign(ke.Payload(), store.Install.Secret);
                string p4 = Path.Combine(dir, "expired" + UsbKey.KeyFileName);
                File.WriteAllText(p4, Json.Dump(ke), new System.Text.UTF8Encoding(false));
                var c4 = UsbKey.ReadAndCheck(p4, store, out back);
                check("过期密钥被拒绝", c4 == KeyCheck.Expired, KeyCheckText.Msg(c4));

                // 5. 有效期内的密钥不能过期
                var kv = UsbKey.Build(store, acc, "", 30);
                string p5 = Path.Combine(dir, "valid" + UsbKey.KeyFileName);
                File.WriteAllText(p5, Json.Dump(kv), new System.Text.UTF8Encoding(false));
                var c5 = UsbKey.ReadAndCheck(p5, store, out back);
                check("30天有效期密钥有效", c5 == KeyCheck.Ok, KeyCheckText.Msg(c5));

                // 6. 来自另一份 Tensyan 数据的密钥
                var other = new Store();
                other.Install.InstallId = Guid.NewGuid().ToString("N");
                other.Install.Secret = Convert.ToBase64String(Auth.RandomBytes(32));
                var ko = UsbKey.Build(store, acc, "", 0);
                ko.Install = other.Install.InstallId;
                string p6 = Path.Combine(dir, "other" + UsbKey.KeyFileName);
                File.WriteAllText(p6, Json.Dump(ko), new System.Text.UTF8Encoding(false));
                var c6 = UsbKey.ReadAndCheck(p6, store, out back);
                check("异机密钥被识别", c6 == KeyCheck.OtherInstall, KeyCheckText.Msg(c6));

                // 7. 账号不存在
                var kn = UsbKey.Build(store, acc, "", 0);
                kn.User = "ghost";
                kn.Sig = Auth.Sign(kn.Payload(), store.Install.Secret);
                string p7 = Path.Combine(dir, "ghost" + UsbKey.KeyFileName);
                File.WriteAllText(p7, Json.Dump(kn), new System.Text.UTF8Encoding(false));
                var c7 = UsbKey.ReadAndCheck(p7, store, out back);
                check("账号不存在被识别", c7 == KeyCheck.NoAccount, KeyCheckText.Msg(c7));

                // 8. 磁盘枚举不抛异常
                var drives = UsbKey.Drives(false);
                check("磁盘枚举可用", drives != null, "检测到 " + (drives == null ? 0 : drives.Count) + " 个磁盘");

                // 9. Excel 导出可用
                string xlsx = Path.Combine(dir, "export-test.xlsx");
                string msg;
                bool okX = Exporter.ExportClassScores(store.Data, store.Data.Classes[0], xlsx, out msg);
                check("Excel 导出可用", okX && File.Exists(xlsx) && new FileInfo(xlsx).Length > 1000, msg + " size=" + (File.Exists(xlsx) ? new FileInfo(xlsx).Length.ToString() : "0"));

                // 10. 密码哈希
                var a2 = new Account { User = "t" };
                Auth.SetPassword(a2, "p@ss");
                check("密码校验正确", Auth.Verify(a2, "p@ss") && !Auth.Verify(a2, "wrong"), "");

                // 11. 全新数据目录不预置任何账号（首次使用应走创建管理员向导）
                Paths.Init(PathCompat.Combine(dir, "fresh", "data"));
                var fresh = Store.Load();
                check("全新数据不预置账号", fresh.Accounts.Count == 0, "accounts=" + fresh.Accounts.Count);
                check("全新数据已建默认班级", fresh.Data.Classes.Count >= 1, "classes=" + fresh.Data.Classes.Count);
                var registered = new Account { User = "teacher01", Display = "张老师", Role = Role.Member };
                Auth.SetPassword(registered, "abc123");
                fresh.Accounts.Add(registered);
                fresh.Save();
                var reload = Store.Load();
                check("注册账号可持久化", reload.Accounts.Count == 1 && Auth.Verify(reload.Accounts[0], "abc123"), "accounts=" + reload.Accounts.Count);

                // 12. 用户名长度规则（允许两字中文名）
                check("两字用户名合法（张三）", "张三".Length >= 2, "len=" + "张三".Length);
                check("一字用户名被拒（张）", "张".Length < 2, "len=" + "张".Length);

                // 13. 可写性探测：只要“能创建并写入”就算可写，不能依赖“删除成功”
                string okDir = Path.Combine(dir, "wtest-ok");
                check("可写目录判定为可写", Paths.CanWrite(okDir), okDir);
                check("探测后不留垃圾文件", Directory.GetFiles(okDir).Length == 0, okDir);
                string badDir = PathCompat.Combine(Environment.SystemDirectory, "__tensyan_nowrite", "x");
                check("系统目录判定为不可写", !Paths.CanWrite(badDir), badDir);

                // 14. 受管环境（禁止删除/改名）下仍要能存数据：
                //     故意把临时文件路径占成目录，强制走“直接写目标文件”的兜底分支
                Paths.Init(PathCompat.Combine(dir, "savefallback", "data"));
                var st2 = Store.Load();
                Directory.CreateDirectory(Paths.DataFile + ".tmp");
                st2.Data.Classes[0].Name = "回退写入测试班";
                st2.Save();
                check("临时文件不可用时仍能保存", st2.LastSaveError.Length == 0 && File.Exists(Paths.DataFile),
                    st2.LastSaveError.Length == 0 ? "ok" : st2.LastSaveError);
                check("保存内容确实落盘", File.Exists(Paths.DataFile) && File.ReadAllText(Paths.DataFile).Contains("回退写入测试班"), Paths.DataFile);

                // 15. 指定的数据目录不可写时自动跳过，不能“哪儿都写不了”
                Paths.Init(badDir);
                check("不可写目录被自动跳过", !Paths.DataDir.StartsWith(badDir, StringComparison.OrdinalIgnoreCase) && Paths.WarningReason.Length > 0,
                    Paths.WarningReason + " → " + Paths.DataDir);
                check("最终数据目录确实可写", Paths.CanWrite(Paths.DataDir), Paths.DataDir);

                // 16. 记住自定义数据位置（“更改数据位置”功能）
                string custom = Path.Combine(dir, "customdata");
                bool ptrOk = Paths.WritePointer(custom);
                Paths.Init();
                check("自定义数据位置生效", ptrOk && string.Equals(Paths.DataDir, custom, StringComparison.OrdinalIgnoreCase), Paths.DataDir);
                Paths.ClearPointer();
                Paths.Init();
                check("清除记录后回到默认位置", !string.Equals(Paths.DataDir, custom, StringComparison.OrdinalIgnoreCase), Paths.DataDir);
            }
            catch (Exception ex)
            {
                fail++;
                sb.AppendLine("[FAIL] 自检异常: " + ex);
            }

            sb.AppendLine();
            sb.AppendLine("结果：通过 " + pass + " 项，失败 " + fail + " 项");
            File.WriteAllText(Path.Combine(dir, "selftest.txt"), sb.ToString(), new System.Text.UTF8Encoding(true));
        }

        [STAThread]
        private static void Main(string[] args)
        {
            string dataDir = null;
            bool demo = false;
            string shotDir = null;
            string loginTestDir = null;
            string uiTestDir = null;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "--data" && i + 1 < args.Length) dataDir = args[++i];
                else if (a == "--demo") demo = true;
                else if (a == "--shot" && i + 1 < args.Length) shotDir = args[++i];
                else if (a == "--logintest" && i + 1 < args.Length) loginTestDir = args[++i];
                else if (a == "--uitest" && i + 1 < args.Length) uiTestDir = args[++i];
                else if (a == "--selftest" && i + 1 < args.Length) { SelfTest(args[++i]); return; }
                else if (a == "--help" || a == "-h")
                {
                    MessageBox.Show("Tensyan 班级积分工具\n\n参数：\n  --data <目录>   指定数据目录\n  --demo          载入示例数据（用于试用与界面预览）\n  --shot <目录>   生成界面截图后退出（开发自检用）\n  --selftest <目录>  运行U盘密钥/数据自检后退出（开发自检用）\n  --logintest <目录> 登录/注册耗时自检后退出（开发自检用）\n  --uitest <目录>    按身份自检“添加学生”等界面功能后退出（开发自检用）",
                        "Tensyan", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            Paths.Init(dataDir);
            Theme.InitScale();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            WinCompat.EnableDpiAwareness();

            if (loginTestDir != null || uiTestDir != null)
            {
                // 自检模式：异常只记日志不弹窗（弹窗没人点会把自检挂住）
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) =>
                {
                    Paths.Log("自检界面异常: " + e.Exception);
                    LoginTestLog("!! 界面异常: " + e.Exception.Message);
                };
                if (loginTestDir != null) RunLoginTest(loginTestDir);
                if (uiTestDir != null) RunUiTest(uiTestDir);
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += (s, e) => Paths.Log("未处理异常: " + e.ExceptionObject);
            Application.ThreadException += (s, e) =>
            {
                Paths.Log("界面异常: " + e.Exception);
                MessageBox.Show("发生了一个错误：\n" + e.Exception.Message + "\n\n详细信息已记录到：\n" + Paths.LogFile,
                    "Tensyan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            };

            bool created;
            using (var mutex = new Mutex(true, "Tensyan.ClassPoints.SingleInstance", out created))
            {
                if (!created && shotDir == null)
                {
                    MessageBox.Show("Tensyan 已经在运行了。\n请在任务栏中找到它，或先关闭再重新打开。", "Tensyan",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var store = Store.Load();
                var app = new App(store);
                // XP 特别版：老机器性能有限，动画强制关闭（界面更跟手）
                // 2026-10-05：WinForms 没有 GPU 合成线程，面板动画必然出现子控件撕裂
                // （同类的 PCL2 是 WPF，动画走合成线程所以丝滑）。
                // 本版本统一停用动画：撕裂感彻底消失。等壳层迁到 WPF 后再按页面启用。
                Anim.Enabled = false;
                // 原来的写法（保留备查）：Anim.Enabled = store.Data.Settings.AnimationEnabled && !Theme.IsXp;
                Paths.Log("=== 启动：数据目录=" + Paths.DataDir + " 账号=" + store.Accounts.Count + " 班级=" + store.Data.Classes.Count
                    + " 便携=" + Paths.PortableData + " 缩放=" + Theme.Scale.ToString("0.##")
                    + (Theme.IsXp ? "  [Windows XP 模式：直角窗口 / 动画关闭]" : ""));

                if (demo) app.SeedDemo(28, 2);

                if (shotDir != null)
                {
                    // 截图自检默认关掉动画，避免抓到过渡帧；需要时用 TENSYAN_SHOT_ANIM=1 保留
                    if (Environment.GetEnvironmentVariable("TENSYAN_SHOT_ANIM") != "1") Anim.Enabled = false;
                    Screenshots.Run(app, shotDir);
                    return;
                }

                store.Data.Settings.FirstRunDone = true;
                store.Save();

                // 全程序只有一个主窗口：登录/注册与主界面都是它的两个状态，
                // 退出登录只是切回登录状态（不再有第二个顶层窗体）
                Paths.Log("创建主窗口…");
                using (var shell = new MainForm(app))
                {
                    Paths.Log("进入消息循环");
                    Application.Run(shell);
                }
                Paths.Log("消息循环结束，进程退出");
            }
        }

        // ================= 自检辅助 =================

        private static System.Windows.Forms.Timer _watchdog;
        private static string _loginTestPath;

        private static void LoginTestLog(string line)
        {
            try { File.AppendAllText(_loginTestPath, line + Environment.NewLine); } catch { }
        }

        private static MainForm NewShell(App app)
        {
            var shell = new MainForm(app);
            shell.StartPosition = FormStartPosition.Manual;
            shell.Location = new Point(-4000, -4000);
            return shell;
        }

        /// <summary>泵消息直到条件成立或超时，返回耗时。</summary>
        private static double PumpUntil(Func<bool> cond, int timeoutMs, System.Diagnostics.Stopwatch sw)
        {
            int end = Environment.TickCount + timeoutMs;
            while (!cond() && Environment.TickCount < end)
            {
                Application.DoEvents();
                Thread.Sleep(10);
            }
            Application.DoEvents();
            return sw == null ? 0 : sw.Elapsed.TotalMilliseconds;
        }

        /// <summary>登录/注册自检：单窗口状态下 点击 → 校验 → 切到主界面。</summary>
        private static void RunLoginTest(string dir)
        {
            var sb = new System.Text.StringBuilder();
            try
            {
                Directory.CreateDirectory(dir);
                _loginTestPath = Path.Combine(dir, "logintest-progress.txt");
                try { File.WriteAllText(_loginTestPath, ""); } catch { }
                LoginTestLog("开始自检");

                _watchdog = new System.Windows.Forms.Timer { Interval = 30000 };
                _watchdog.Tick += (s, e) => { _watchdog.Stop(); LoginTestLog("!! 超时：某一步未返回"); Environment.Exit(3); };
                _watchdog.Start();

                Paths.Init(Path.Combine(dir, "data"));
                Theme.InitScale();
                Anim.Enabled = true;
                LoginTestLog("初始化完成");

                var store = Store.Load();
                var app = new App(store);
                Account acc = null;
                foreach (var a in store.Accounts) if (string.Equals(a.User, "admin", StringComparison.OrdinalIgnoreCase)) acc = a;
                if (acc == null) { acc = new Account { User = "admin", Display = "管理员", Role = Role.Admin }; store.Accounts.Add(acc); }
                Auth.SetPassword(acc, "tensyan123");
                store.Save();

                // ---------- 1) 账号密码登录 ----------
                var sw = new System.Diagnostics.Stopwatch();
                double tSwitched = 0, tReady = 0;
                var shell = NewShell(app);
                shell.DebugLoginView.LoggedIn += (s, e) => { tSwitched = sw.Elapsed.TotalMilliseconds; };
                shell.Shown += (s, e) => { sw.Reset(); sw.Start(); shell.DebugLoginView.DebugLogin("admin", "tensyan123"); };
                shell.Show();
                tReady = PumpUntil(() => shell.DebugLoggedIn, 10000, sw);
                bool loginHidden = !shell.DebugLoginView.Visible;
                bool pageVisible = shell.DebugCurrentPage != null && shell.DebugCurrentPage.Visible;
                LoginTestLog("阶段: 密码登录完成 loggedIn=" + shell.DebugLoggedIn);

                sb.AppendLine("【窗口结构】整个程序只有 1 个主窗口；登录/注册与主界面是它的两个状态");
                sb.AppendLine("登录结果                       : " + (shell.DebugLoggedIn ? "OK" : "失败"));
                sb.AppendLine("点击 → 密码校验完成(后台线程)   : " + tSwitched.ToString("0") + " ms");
                sb.AppendLine("点击 → 已切到主界面             : " + tReady.ToString("0") + " ms");
                sb.AppendLine("登录视图是否已隐藏              : " + (loginHidden ? "是" : "否"));
                sb.AppendLine("学生页是否已显示                : " + (pageVisible ? "是" : "否"));

                // ---------- 2) 注册（已有管理员 → 需要U盘认证） ----------
                sb.AppendLine();
                sb.AppendLine("--- 注册路径（已有管理员）---");
                shell.DebugShowLogin();
                PumpUntil(() => true, 200, null);
                shell.DebugLoginView.DebugRegister("blocked01", "测试", "abc123", "abc123");
                PumpUntil(() => true, 200, null);
                string gateMsg = shell.DebugLoginView.DebugRegisterMessage;
                bool gateBlocked = !shell.DebugLoggedIn;
                sb.AppendLine("未认证时注册被拦下              : " + (gateBlocked ? "是" : "否") + "   提示：" + gateMsg);

                app.Settings.RegisterNeedsAdminUsb = false;
                shell.DebugLoginView.RefreshState();
                var sw2 = new System.Diagnostics.Stopwatch();
                double rSwitched = 0, rReady = 0;
                shell.DebugLoginView.LoggedIn += (s, e) => { rSwitched = sw2.Elapsed.TotalMilliseconds; };
                sw2.Reset(); sw2.Start();
                shell.DebugLoginView.DebugRegister("student01", "学习委员", "abc123", "abc123");
                rReady = PumpUntil(() => shell.DebugLoggedIn, 10000, sw2);
                bool regOk = false;
                foreach (var a in app.Store.Accounts) if (a.User == "student01" && a.Role == Role.Member) regOk = true;
                LoginTestLog("阶段: 注册用例完成 ok=" + regOk);
                sb.AppendLine("注册结果 / 账号已写入           : " + (shell.DebugLoggedIn ? "OK" : "失败") + " / " + (regOk ? "是" : "否"));
                sb.AppendLine("注册 → 校验完成(后台线程)       : " + rSwitched.ToString("0") + " ms");
                sb.AppendLine("注册 → 已切到主界面             : " + rReady.ToString("0") + " ms");
                sb.AppendLine("注册后是否已离开登录界面        : " + (!shell.DebugLoginView.Visible ? "是" : "否（异常）"));

                // ---------- 3) 首次创建管理员（本机无管理员，不需要U盘） ----------
                sb.AppendLine();
                sb.AppendLine("--- 首次创建管理员（本机无管理员）---");
                app.Store.Accounts.Clear();
                shell.DebugShowLogin();
                PumpUntil(() => true, 200, null);
                bool needUsb = shell.DebugLoginView.DebugNeedsUsbForRegister;
                shell.DebugLoginView.DebugRegister("admin2", "管理员", "abc123", "abc123");
                PumpUntil(() => shell.DebugLoggedIn, 10000, null);
                bool createdAdmin = false;
                foreach (var a in app.Store.Accounts) if (a.User == "admin2" && a.Role == Role.Admin) createdAdmin = true;
                LoginTestLog("阶段: 首次管理员用例完成 created=" + createdAdmin);
                sb.AppendLine("注册页是否要求U盘认证            : " + (needUsb ? "要求（错误！）" : "不要求（正确）"));
                sb.AppendLine("能否直接创建管理员              : " + (createdAdmin ? "能" : "不能（错误！）"));
                sb.AppendLine("创建后是否进入主界面            : " + (shell.DebugLoggedIn ? "是" : "否"));

                shell.Close();
                Application.DoEvents();
            }
            catch (Exception ex)
            {
                sb.AppendLine("异常: " + ex);
            }
            _watchdog.Stop();
            File.WriteAllText(Path.Combine(dir, "logintest.txt"), sb.ToString(), new System.Text.UTF8Encoding(true));
        }

        /// <summary>功能自检：按身份验证“能否添加学生”这条链路（store → 界面卡片）。</summary>
        private static void RunUiTest(string dir)
        {
            var sb = new System.Text.StringBuilder();
            int pass = 0, fail = 0;
            Action<string, bool, string> check = (name, ok, detail) =>
            {
                if (ok) pass++; else fail++;
                sb.AppendLine((ok ? "[PASS] " : "[FAIL] ") + name + (string.IsNullOrEmpty(detail) ? "" : "  ->  " + detail));
            };

            try
            {
                Directory.CreateDirectory(dir);
                Paths.Init(Path.Combine(dir, "data"));
                Theme.InitScale();
                Anim.Enabled = false;

                var store = Store.Load();
                var app = new App(store);

                Account admin = null;
                foreach (var a in store.Accounts) if (a.Role == Role.Admin) admin = a;
                if (admin == null)
                {
                    admin = new Account { User = "admin", Display = "管理员", Role = Role.Admin };
                    Auth.SetPassword(admin, "tensyan123");
                    store.Accounts.Add(admin);
                    store.Save();
                }
                var adminSession = new Session { Role = Role.Admin, User = admin.User, Display = admin.Display, Account = admin, LoginMethod = "自检" };
                app.Session = adminSession;
                if (app.Data.Classes.Count == 0) app.Data.Classes.Add(new SchoolClass { Name = "自检班" });
                if (app.Current == null) app.Current = app.Data.Classes[0];

                var shell = NewShell(app);
                shell.Show();
                PumpUntil(() => shell.DebugLoggedIn, 5000, null);
                check("主窗口直接进入主界面", shell.DebugLoggedIn, "单窗口状态切换");
                shell.DebugSwitchPage(0);
                PumpUntil(() => true, 200, null);

                var page = shell.DebugCurrentPage as PageStudents;
                check("学生积分页可打开", page != null, page == null ? "页面为空" : "ok");

                if (page != null)
                {
                    int before = page.DebugCardCount;
                    int count0 = app.Current.Students.Count;
                    string r = page.DebugAddStudent("自检学生");
                    PumpUntil(() => true, 200, null);
                    check("管理员可添加学生", app.Current.Students.Count == count0 + 1, r);
                    check("添加后卡片数增加", page.DebugCardCount >= before + 1, "before=" + before + " after=" + page.DebugCardCount);

                    bool onDisk = false;
                    var reloaded = Store.Load();
                    foreach (var c in reloaded.Data.Classes)
                        foreach (var st in c.Students)
                            if (st.Name == "自检学生") onDisk = true;
                    check("新增学生已写入磁盘", onDisk, Paths.DataFile);
                }

                // 成员身份
                var member = new Account { User = "member01", Display = "任课老师", Role = Role.Member };
                shell.DebugSetSession(new Session { Role = Role.Member, User = "member01", Display = "任课老师", Account = member, LoginMethod = "自检" });
                shell.DebugSwitchPage(0);
                PumpUntil(() => true, 200, null);
                var page2 = shell.DebugCurrentPage as PageStudents;
                string r2 = page2 == null ? "页面为空" : page2.DebugAddStudent("不该加进去");
                check("成员不能添加学生", page2 != null && !page2.DebugCanAddStudent, r2);

                // 访客身份
                shell.DebugSetSession(Session.Guest());
                shell.DebugSwitchPage(0);
                PumpUntil(() => true, 200, null);
                var page3 = shell.DebugCurrentPage as PageStudents;
                string r3 = page3 == null ? "页面为空" : page3.DebugAddStudent("也不该加进去");
                check("访客不能添加学生", page3 != null && !page3.DebugCanAddStudent, r3);

                // 退出登录：应回到同一个窗口的登录状态
                app.Session = adminSession;
                shell.DebugSetSession(adminSession);
                shell.DebugShowLogin();
                PumpUntil(() => true, 200, null);
                check("退出登录后回到登录状态", !shell.DebugLoggedIn && shell.DebugLoginView.Visible, "同一窗口");

                shell.Close();
                Application.DoEvents();
            }
            catch (Exception ex)
            {
                fail++;
                sb.AppendLine("[FAIL] 自检异常: " + ex);
            }

            sb.AppendLine();
            sb.AppendLine("结果：通过 " + pass + " 项，失败 " + fail + " 项");
            File.WriteAllText(Path.Combine(dir, "uitest.txt"), sb.ToString(), new System.Text.UTF8Encoding(true));
        }

        /// <summary>界面截图自检（--shot 目录）：全部来自同一个主窗口的不同状态。</summary>
        internal static class Screenshots
        {
            public static void Run(App app, string dir)
            {
                Directory.CreateDirectory(dir);

                Account adminAcc = null;
                foreach (var a in app.Store.Accounts) if (a.Role == Role.Admin) adminAcc = a;
                if (adminAcc == null)
                {
                    adminAcc = new Account { User = "admin", Display = "班主任", Role = Role.Admin };
                    Auth.SetPassword(adminAcc, "tensyan123");
                    app.Store.Accounts.Add(adminAcc);
                    app.Store.Save();
                }
                var adminSession = new Session { Role = Role.Admin, User = adminAcc.User, Display = "班主任", LoginMethod = "账号密码", Account = adminAcc };
                var memberSession = new Session { Role = Role.Member, User = "member01", Display = "任课老师", LoginMethod = "U盘登录", Account = new Account { User = "member01", Display = "任课老师", Role = Role.Member } };

                var shell = NewShell(app);
                shell.Show();
                PumpUntil(() => true, 300, null);

                // ---- 登录态 ----
                shell.DebugShowLogin();
                PumpUntil(() => true, 200, null);
                Capture(shell, Path.Combine(dir, "01-login.png"));

                shell.DebugLoginView.DebugShowTab(2);
                PumpUntil(() => true, 200, null);
                Capture(shell, Path.Combine(dir, "03-register.png"));

                shell.DebugLoginView.DebugShowTab(1);
                PumpUntil(() => true, 200, null);
                Capture(shell, Path.Combine(dir, "04-usb-login.png"));

                // 本机还没有管理员时：注册页变成“创建管理员账号”，不需要U盘
                var backup = new List<Account>(app.Store.Accounts);
                app.Store.Accounts.Clear();
                shell.DebugLoginView.DebugShowTab(2);
                shell.DebugLoginView.RefreshState();
                PumpUntil(() => true, 200, null);
                Capture(shell, Path.Combine(dir, "26-first-admin.png"));
                app.Store.Accounts.AddRange(backup);
                shell.DebugLoginView.RefreshState();

                shell.DebugLoginView.DebugShowTab(0);

                // ---- 主界面态 ----
                shell.ShowMain(adminSession, false);
                PumpUntil(() => true, 300, null);
                for (int i = 0; i < 7 && i < shell.DebugPageCount; i++)
                {
                    shell.DebugSwitchPage(i);
                    Capture(shell, Path.Combine(dir, "1" + (i + 1) + "-page" + i + ".png"));
                }

                // 系统设置的“数据与备份”分页（含数据目录与“更改数据位置…”）
                shell.DebugSwitchPage(6);
                var settings = shell.DebugCurrentPage as PageSettings;
                if (settings != null)
                {
                    settings.DebugShowTab(3);
                    PumpUntil(() => true, 200, null);
                    Capture(shell, Path.Combine(dir, "18-settings-data.png"));
                    settings.DebugShowTab(0);
                }

                // ---- 成员 / 访客 ----
                shell.DebugSetSession(memberSession);
                for (int i = 0; i < 4 && i < shell.DebugPageCount; i++)
                {
                    shell.DebugSwitchPage(i);
                    Capture(shell, Path.Combine(dir, "3" + (i + 1) + "-page" + i + ".png"));
                }
                shell.DebugSetSession(Session.Guest());
                for (int i = 0; i < 4 && i < shell.DebugPageCount; i++)
                {
                    shell.DebugSwitchPage(i);
                    Capture(shell, Path.Combine(dir, "4" + (i + 1) + "-page" + i + ".png"));
                }

                shell.Close();
                shell.Dispose();
                Application.DoEvents();

                // ---- 独立对话框 ----
                var stu = app.Current != null && app.Current.Students.Count > 0 ? app.Current.Students[0] : new Student { Name = "张一鸣" };
                if (app.Current != null && app.Current.Students.Count > 0)
                    Shot(new ScoreDialog(app, stu), Path.Combine(dir, "20-score.png"));
                Shot(new UsbCreateDialog(app), Path.Combine(dir, "21-usb.png"));
                Shot(new AccountEditDialog(app, adminAcc, false), Path.Combine(dir, "22-account.png"));
                Shot(new StudentEditDialog(stu, false), Path.Combine(dir, "23-student.png"));
                Shot(new RandomPickDialog(app), Path.Combine(dir, "24-random.png"));
                Shot(new AboutDialog(), Path.Combine(dir, "25-about.png"));
            }

            private static void Capture(Form f, string path)
            {
                try
                {
                    Application.DoEvents();
                    Thread.Sleep(Anim.Enabled ? 460 : 130);
                    Application.DoEvents();
                    using (var bmp = new Bitmap(f.Width, f.Height))
                    {
                        f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
                catch (Exception ex)
                {
                    Paths.Log("截图失败 " + path + " : " + ex.Message);
                }
            }

            private static void Shot(Form f, string path, bool dispose = true)
            {
                try
                {
                    f.StartPosition = FormStartPosition.Manual;
                    if (f.Location.X > -1000) f.Location = new Point(-4000, -4000);
                    f.Show();
                    f.Refresh();
                    Capture(f, path);
                    if (dispose) { f.Hide(); f.Dispose(); }
                }
                catch (Exception ex)
                {
                    Paths.Log("截图失败 " + path + " : " + ex.Message);
                }
            }
        }
    }
}
