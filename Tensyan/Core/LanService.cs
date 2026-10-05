using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Tensyan.Core
{
    /// <summary>
    /// PC 端的局域网服务（长期驻留后台）：
    ///   · UDP 47654：回答手机的"找电脑"广播（免去手输 IP）
    ///   · TCP 47653：配对、自动登录、收发同步包（与手机端 Java 版同一套协议）
    ///
    /// 协议是一行一个 JSON（JSON 文本本身不含换行），简单到不会出错。
    /// </summary>
    public class LanService
    {
        public const int TcpPort = 47653;
        public const int UdpPort = 47654;

        private readonly Store _store;
        private readonly PairingStore _pairing;
        private readonly object _dataLock = new object();

        private TcpListener _tcp;
        private UdpClient _udp;
        private volatile bool _running;

        /// <summary>日志（可能来自后台线程，UI 侧自行 Invoke）。</summary>
        public event Action<string> Log;
        /// <summary>数据被手机合并过，UI 需要重新读一遍。</summary>
        public event Action DataMerged;

        public bool Running { get { return _running; } }
        public string LastError { get; private set; }

        public LanService(Store store, PairingStore pairing)
        {
            _store = store;
            _pairing = pairing;
            LastError = "";
        }

        public string DeviceName
        {
            get
            {
                try { return Environment.MachineName; }
                catch { return "电脑"; }
            }
        }

        // ---------------- 启动 / 停止 ----------------

        public void Start()
        {
            if (_running) return;
            _running = true;
            LastError = "";

            var t1 = new Thread(TcpLoop) { IsBackground = true, Name = "tensyan-lan-tcp" };
            t1.Start();
            var t2 = new Thread(UdpLoop) { IsBackground = true, Name = "tensyan-lan-udp" };
            t2.Start();
            Fire("局域网服务已启动（端口 " + TcpPort + "），等待手机配对/同步");
        }

        public void Stop()
        {
            _running = false;
            try { if (_tcp != null) _tcp.Stop(); } catch { }
            try { if (_udp != null) _udp.Close(); } catch { }
            _tcp = null;
            _udp = null;
            Fire("局域网服务已停止");
        }

        private void Fire(string msg)
        {
            try { var h = Log; if (h != null) h(msg); } catch { }
        }

        // ---------------- UDP：回答手机的发现广播 ----------------

        private void UdpLoop()
        {
            try
            {
                _udp = new UdpClient(UdpPort);
                _udp.EnableBroadcast = true;
                var ep = new IPEndPoint(IPAddress.Any, 0);
                while (_running)
                {
                    byte[] data = _udp.Receive(ref ep);
                    string ask = Encoding.UTF8.GetString(data);
                    if (ask.IndexOf("tensyan", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    string reply = "{\"app\":\"Tensyan\",\"role\":\"pc\",\"device\":\"" + Escape(DeviceName)
                        + "\",\"port\":" + TcpPort + ",\"pairing\":true,\"paired\":" + _pairing.Data.Devices.Count + "}";
                    byte[] outBytes = Encoding.UTF8.GetBytes(reply);
                    try { _udp.Send(outBytes, outBytes.Length, ep); } catch { }
                }
            }
            catch (Exception ex)
            {
                if (_running)
                {
                    LastError = ex.Message;
                    Fire("发现服务不可用（" + ex.Message + "）；若手机找不到电脑，请检查防火墙是否放行 UDP " + UdpPort);
                }
            }
        }

        // ---------------- TCP：配对 / 登录 / 同步 ----------------

        private void TcpLoop()
        {
            try
            {
                _tcp = new TcpListener(IPAddress.Any, TcpPort);
                _tcp.Start();
                while (_running)
                {
                    TcpClient client = _tcp.AcceptTcpClient();
                    var th = new Thread(() => HandleClient(client)) { IsBackground = true, Name = "tensyan-lan-conn" };
                    th.Start();
                }
            }
            catch (Exception ex)
            {
                if (_running)
                {
                    LastError = ex.Message;
                    Fire("同步服务不可用（" + ex.Message + "）；请检查防火墙是否放行 TCP " + TcpPort);
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            string ip = "";
            try
            {
                ip = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
            }
            catch { }
            try
            {
                client.ReceiveTimeout = 60000;
                using (var stream = client.GetStream())
                using (var reader = new System.IO.StreamReader(stream, Encoding.UTF8))
                using (var writer = new System.IO.StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true })
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string reply = HandleLine(line, ip);
                        writer.WriteLine(reply);
                    }
                }
            }
            catch (Exception ex)
            {
                Paths.Log("局域网连接结束：" + ex.Message);
            }
            finally
            {
                try { client.Close(); } catch { }
            }
        }

        /// <summary>处理一行请求，返回一行应答。public 便于自检与将来复用。</summary>
        public string HandleLine(string line, string ip)
        {
            try
            {
                var req = Json.Parse<Dictionary<string, object>>(line);
                string cmd = GetString(req, "cmd");

                if (cmd == "who")
                    return "{\"ok\":true,\"app\":\"Tensyan\",\"role\":\"pc\",\"device\":\"" + Escape(DeviceName) + "\",\"port\":" + TcpPort + "}";

                if (cmd == "pair")
                {
                    string code = GetString(req, "code");
                    string deviceId = GetString(req, "deviceId");
                    string deviceName = GetString(req, "deviceName");
                    string want = GetString(req, "account");

                    string account = PickAccount(want);
                    if (account == null) return "{\"ok\":false,\"err\":\"电脑上还没有管理员账号，请先在电脑端创建\"}";

                    PairedDevice dev = _pairing.Issue(code, deviceId, deviceName, account);
                    if (dev == null) return "{\"ok\":false,\"err\":\"配对码不对或已过期，请在电脑上看最新的一串数字\"}";

                    Fire("手机「" + dev.Name + "」配对成功，已绑定账号 " + account);
                    return "{\"ok\":true,\"token\":\"" + Escape(dev.Token) + "\",\"user\":\"" + Escape(account)
                        + "\",\"display\":\"" + Escape(DisplayOf(account)) + "\",\"role\":\"" + RoleOf(account) + "\"}";
                }

                // 以下都要令牌
                var dev2 = _pairing.FindByToken(GetString(req, "token"));
                if (dev2 == null) return "{\"ok\":false,\"err\":\"未配对或令牌失效，请重新配对\"}";

                if (cmd == "hello")
                {
                    _pairing.Touch(dev2, ip);
                    Fire("手机「" + dev2.Name + "」自动登录（账号 " + dev2.AccountUser + "）");
                    return "{\"ok\":true,\"user\":\"" + Escape(dev2.AccountUser)
                        + "\",\"display\":\"" + Escape(DisplayOf(dev2.AccountUser)) + "\",\"role\":\"" + RoleOf(dev2.AccountUser) + "\"}";
                }

                if (cmd == "peek")
                {
                    lock (_dataLock)
                    {
                        var c = CurrentClass();
                        return "{\"ok\":true,\"device\":\"" + Escape(DeviceName) + "\",\"classes\":" + _store.Data.Classes.Count
                            + ",\"events\":" + _store.Data.Events.Count
                            + ",\"ClassName\":\"" + Escape(c == null ? "" : c.Name)
                            + "\",\"Students\":" + (c == null ? 0 : c.Students.Count) + "}";
                    }
                }

                if (cmd == "pull")
                {
                    lock (_dataLock)
                    {
                        var c = CurrentClass();
                        if (c == null) return "{\"ok\":false,\"err\":\"电脑上还没有班级\"}";
                        var pkg = SyncMerge.Build(_store.Data, c, _pairing.Data.DeviceId, DeviceName);
                        _pairing.Touch(dev2, ip);
                        Fire("已把电脑上的班级「" + c.Name + "」发给手机（" + pkg.StudentCount + " 名学生 / " + pkg.EventCount + " 条记录）");
                        return "{\"ok\":true,\"package\":" + Json.Dump(pkg) + "}";
                    }
                }

                if (cmd == "push")
                {
                    lock (_dataLock)
                    {
                        var pkgObj = GetObject(req, "package");
                        if (pkgObj == null) return "{\"ok\":false,\"err\":\"没有收到同步包\"}";
                        var pkg = Json.Parse<SyncPackageDto>(Json.Dump(pkgObj));
                        string summary = SyncMerge.Merge(_store.Data, pkg);
                        _store.Save();
                        _pairing.Touch(dev2, ip);
                        Fire("已合并手机「" + dev2.Name + "」的数据：" + summary);
                        var h = DataMerged; if (h != null) h();
                        return "{\"ok\":true,\"summary\":\"" + Escape(summary) + "\"}";
                    }
                }

                return "{\"ok\":false,\"err\":\"未知指令：\" + cmd}";
            }
            catch (Exception ex)
            {
                Paths.Log("局域网协议出错：" + ex);
                return "{\"ok\":false,\"err\":\"" + Escape(ex.Message) + "\"}";
            }
        }

        // ---------------- 小工具 ----------------

        private SchoolClass CurrentClass()
        {
            var c = _store.Data.FindClass(_store.Data.Settings.LastClassId);
            if (c == null && _store.Data.Classes.Count > 0) c = _store.Data.Classes[0];
            return c;
        }

        private string PickAccount(string want)
        {
            if (_store.Accounts == null || _store.Accounts.Count == 0) return null;
            if (!string.IsNullOrEmpty(want))
                foreach (var a in _store.Accounts)
                    if (a.User == want) return a.User;
            string adminUser = null;
            foreach (var a in _store.Accounts)
                if (a.Role == Role.Admin) { adminUser = a.User; break; }
            return adminUser ?? _store.Accounts[0].User;
        }

        private string DisplayOf(string user)
        {
            foreach (var a in _store.Accounts)
                if (a.User == user) return string.IsNullOrEmpty(a.Display) ? a.User : a.Display;
            return user;
        }

        private string RoleOf(string user)
        {
            foreach (var a in _store.Accounts)
                if (a.User == user) return a.Role.ToString();
            return "Member";
        }

        private static string GetString(Dictionary<string, object> d, string key)
        {
            if (d == null || !d.ContainsKey(key) || d[key] == null) return "";
            return d[key].ToString();
        }

        private static object GetObject(Dictionary<string, object> d, string key)
        {
            if (d == null || !d.ContainsKey(key)) return null;
            return d[key];
        }

        public static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
        }

        /// <summary>本机在局域网里的 IPv4（用于界面提示）。</summary>
        public static string LocalIp()
        {
            try
            {
                foreach (var ip in Dns.GetHostAddresses(Dns.GetHostName()))
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                        return ip.ToString();
            }
            catch { }
            return "";
        }
    }
}
