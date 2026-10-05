using System;
using System.Collections.Generic;
using System.IO;

namespace Tensyan.Core
{
    /// <summary>一台已配对的手机。</summary>
    public class PairedDevice
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Token { get; set; } = "";
        public string AccountUser { get; set; } = "";
        public string PairedUtc { get; set; } = "";
        public string LastSyncUtc { get; set; } = "";
        public string LastIp { get; set; } = "";
        public override string ToString() { return Name + "（" + AccountUser + "）"; }
    }

    public class PairingFile
    {
        public int Version { get; set; } = 1;
        public string DeviceId { get; set; } = "";
        public List<PairedDevice> Devices { get; set; } = new List<PairedDevice>();
    }

    /// <summary>
    /// 配对管理：PC 显示 6 位配对码，手机输入后换取长期令牌（token）。
    /// 之后手机每次连上拿着 token 就能**自动登录**，无需再输账号密码。
    /// </summary>
    public class PairingStore
    {
        public PairingFile Data { get; private set; }
        private string _code = "";
        private DateTime _codeExpire = DateTime.MinValue;
        private readonly object _lock = new object();

        public static string FilePath { get { return Path.Combine(Paths.DataDir, "pairing.json"); } }

        public PairingStore()
        {
            Data = new PairingFile();
            try
            {
                if (File.Exists(FilePath))
                {
                    var f = Json.Parse<PairingFile>(File.ReadAllText(FilePath));
                    if (f != null) Data = f;
                }
                if (string.IsNullOrEmpty(Data.DeviceId)) Data.DeviceId = Guid.NewGuid().ToString();
            }
            catch (Exception ex)
            {
                Paths.Log("配对文件读取失败，已重建：" + ex.Message);
                Data = new PairingFile { DeviceId = Guid.NewGuid().ToString() };
            }
            if (Data.Devices == null) Data.Devices = new List<PairedDevice>();
        }

        public void Save()
        {
            try
            {
                File.WriteAllText(FilePath, Json.Dump(Data));
            }
            catch (Exception ex) { Paths.Log("配对文件保存失败：" + ex.Message); }
        }

        /// <summary>当前配对码（6 位数字，默认 2 分钟有效）。</summary>
        public string Code
        {
            get
            {
                lock (_lock)
                {
                    if (DateTime.UtcNow > _codeExpire || _code.Length == 0) NewCode();
                    return _code;
                }
            }
        }

        public int CodeSecondsLeft
        {
            get
            {
                lock (_lock)
                {
                    if (DateTime.UtcNow > _codeExpire) return 0;
                    return (int)Math.Max(0, (_codeExpire - DateTime.UtcNow).TotalSeconds);
                }
            }
        }

        public string NewCode()
        {
            lock (_lock)
            {
                var rnd = new Random(Guid.NewGuid().GetHashCode());
                _code = rnd.Next(0, 1000000).ToString("000000");
                _codeExpire = DateTime.UtcNow.AddMinutes(2);
                return _code;
            }
        }

        /// <summary>手机提交配对码 → 发一个长期令牌。</summary>
        public PairedDevice Issue(string code, string deviceId, string deviceName, string accountUser)
        {
            lock (_lock)
            {
                if (DateTime.UtcNow > _codeExpire) return null;
                if (string.IsNullOrEmpty(code) || code != _code) return null;

                var dev = new PairedDevice
                {
                    Id = string.IsNullOrEmpty(deviceId) ? Guid.NewGuid().ToString() : deviceId,
                    Name = string.IsNullOrEmpty(deviceName) ? "Android 手机" : deviceName,
                    Token = Convert.ToBase64String(Auth.RandomBytes(32)),
                    AccountUser = accountUser ?? "",
                    PairedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                };

                // 同一台设备重复配对：替换旧记录
                for (int i = Data.Devices.Count - 1; i >= 0; i--)
                    if (Data.Devices[i].Id == dev.Id) Data.Devices.RemoveAt(i);
                Data.Devices.Add(dev);

                _codeExpire = DateTime.MinValue;   // 配对码一次性使用
                Save();
                return dev;
            }
        }

        public PairedDevice FindByToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            foreach (var d in Data.Devices)
                if (Auth.FixedEquals(d.Token, token)) return d;
            return null;
        }

        public void Touch(PairedDevice dev, string ip)
        {
            if (dev == null) return;
            dev.LastSyncUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            if (!string.IsNullOrEmpty(ip)) dev.LastIp = ip;
            Save();
        }

        public bool Unpair(string id)
        {
            for (int i = Data.Devices.Count - 1; i >= 0; i--)
                if (Data.Devices[i].Id == id) { Data.Devices.RemoveAt(i); Save(); return true; }
            return false;
        }
    }
}
