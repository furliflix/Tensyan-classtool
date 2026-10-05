using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Tensyan.Core
{
    /// <summary>U盘登录钥匙文件（TensyanKey.tky，HMAC-SHA256 签名，写入后置为隐藏）。</summary>
    public class UsbKeyFile
    {
        public int V { get; set; } = 1;
        public string App { get; set; } = "Tensyan";
        public string Install { get; set; } = "";
        public string User { get; set; } = "";
        public string Display { get; set; } = "";
        public string Role { get; set; } = "";
        public string Created { get; set; } = "";
        public string Expires { get; set; } = "";
        public string Nonce { get; set; } = "";
        public string PinHash { get; set; } = "";
        public string Sig { get; set; } = "";

        public string Payload()
        {
            // 数组形式：string.Join(string, params object[]) 是 .NET 4.0 才有的重载
            return string.Join("\n", new string[]
            {
                V.ToString(), App, Install, User, Display, Role, Created, Expires, Nonce, PinHash
            });
        }

        public bool HasPin { get { return !string.IsNullOrEmpty(PinHash); } }

        public DateTime? ExpireTime()
        {
            if (string.IsNullOrEmpty(Expires)) return null;
            DateTime t;
            if (DateTime.TryParse(Expires, null, System.Globalization.DateTimeStyles.RoundtripKind, out t)) return t.ToLocalTime();
            return null;
        }

        public DateTime CreateTime()
        {
            DateTime t;
            if (DateTime.TryParse(Created, null, System.Globalization.DateTimeStyles.RoundtripKind, out t)) return t.ToLocalTime();
            return DateTime.MinValue;
        }
    }

    public enum KeyCheck
    {
        Ok,
        NoFile,
        BadFormat,
        BadSignature,
        OtherInstall,
        Expired,
        NoAccount,
        Disabled,
        NeedPin,
        WrongPin
    }

    public static class KeyCheckText
    {
        public static string Msg(KeyCheck k)
        {
            switch (k)
            {
                case KeyCheck.Ok: return "校验通过";
                case KeyCheck.NoFile: return "U盘中未找到 TensyanKey.tky";
                case KeyCheck.BadFormat: return "密钥文件格式不正确";
                case KeyCheck.BadSignature: return "密钥签名无效（文件被篡改或不是本程序制作）";
                case KeyCheck.OtherInstall: return "该U盘由另一份 Tensyan 数据制作，与本机数据不匹配";
                case KeyCheck.Expired: return "该登录U盘已过期";
                case KeyCheck.NoAccount: return "U盘对应的账号在本机不存在";
                case KeyCheck.Disabled: return "U盘对应的账号已被停用";
                case KeyCheck.NeedPin: return "需要输入U盘口令";
                case KeyCheck.WrongPin: return "U盘口令不正确";
                default: return "未知错误";
            }
        }
    }

    public class UsbDrive
    {
        public string Root;         // 例：E:\
        public string Letter;       // 例：E:
        public string Label;
        public string DriveType;
        public long TotalBytes;
        public long FreeBytes;
        public bool HasKey;
        public string KeyPath;

        public string SizeText()
        {
            return FormatSize(TotalBytes) + " / 可用 " + FormatSize(FreeBytes);
        }

        public string Describe()
        {
            string l = Str.Blank(Label) ? "可移动磁盘" : Label;
            return Letter + "  " + l + "  (" + FormatSize(TotalBytes) + ")";
        }

        public static string FormatSize(long b)
        {
            if (b <= 0) return "0";
            string[] u = { "B", "KB", "MB", "GB", "TB" };
            double v = b; int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return v.ToString(i == 0 ? "0" : "0.#") + u[i];
        }
    }

    public static class UsbKey
    {
        public const string KeyFileName = "TensyanKey.tky";

        public static List<UsbDrive> Drives(bool removableOnly)
        {
            var list = new List<UsbDrive>();
            DriveInfo[] all;
            try { all = DriveInfo.GetDrives(); }
            catch { return list; }

            foreach (var d in all)
            {
                try
                {
                    if (!d.IsReady) continue;
                    if (d.DriveType != DriveType.Removable && d.DriveType != DriveType.Fixed) continue;
                    if (removableOnly && d.DriveType != DriveType.Removable) continue;
                    if (d.DriveType == DriveType.Fixed && d.Name.StartsWith(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:", StringComparison.OrdinalIgnoreCase) && removableOnly) continue;

                    var u = new UsbDrive
                    {
                        Root = d.RootDirectory.FullName,
                        Letter = d.Name.TrimEnd('\\'),
                        Label = d.VolumeLabel,
                        DriveType = d.DriveType == DriveType.Removable ? "可移动磁盘" : "本地磁盘",
                        TotalBytes = d.TotalSize,
                        FreeBytes = d.AvailableFreeSpace
                    };
                    string kp = Path.Combine(u.Root, KeyFileName);
                    if (File.Exists(kp)) { u.HasKey = true; u.KeyPath = kp; }
                    list.Add(u);
                }
                catch { }
            }
            return list;
        }

        /// <summary>登录用：扫描所有盘里带密钥文件的盘。</summary>
        public static UsbDrive FindKeyDrive(out UsbKeyFile key, out KeyCheck check, Store store)
        {
            key = null;
            check = KeyCheck.NoFile;
            foreach (var d in Drives(false))
            {
                if (!d.HasKey) continue;
                UsbKeyFile kf;
                KeyCheck c = ReadAndCheck(d.KeyPath, store, out kf);
                if (c == KeyCheck.BadSignature || c == KeyCheck.BadFormat || c == KeyCheck.OtherInstall) { check = c; continue; }
                key = kf; check = c;
                return d;
            }
            return null;
        }

        public static KeyCheck ReadAndCheck(string path, Store store, out UsbKeyFile key)
        {
            key = null;
            try
            {
                if (!File.Exists(path)) return KeyCheck.NoFile;
                string text = File.ReadAllText(path, Encoding.UTF8);
                var kf = Json.Parse<UsbKeyFile>(text);
                if (kf == null || kf.V != 1 || kf.App != "Tensyan" || string.IsNullOrEmpty(kf.Sig)) return KeyCheck.BadFormat;
                key = kf;

                if (kf.Install != store.Install.InstallId) return KeyCheck.OtherInstall;

                string expect = Auth.Sign(kf.Payload(), store.Install.Secret);
                if (!Auth.FixedEquals(expect, kf.Sig)) return KeyCheck.BadSignature;

                var exp = kf.ExpireTime();
                if (exp.HasValue && exp.Value < DateTime.Now) return KeyCheck.Expired;

                var acc = FindAccount(store, kf.User);
                if (acc == null) return KeyCheck.NoAccount;
                if (!acc.Enabled) return KeyCheck.Disabled;

                return kf.HasPin ? KeyCheck.NeedPin : KeyCheck.Ok;
            }
            catch (Exception ex)
            {
                Paths.Log("读取U盘密钥失败: " + ex.Message);
                return KeyCheck.BadFormat;
            }
        }

        public static Account FindAccount(Store store, string user)
        {
            foreach (var a in store.Accounts)
                if (string.Equals(a.User, user, StringComparison.OrdinalIgnoreCase)) return a;
            return null;
        }

        public static UsbKeyFile Build(Store store, Account acc, string pin, int validDays)
        {
            var kf = new UsbKeyFile
            {
                Install = store.Install.InstallId,
                User = acc.User,
                Display = Str.Blank(acc.Display) ? acc.User : acc.Display,
                Role = acc.Role.ToString(),
                Created = DateTime.Now.ToString("o"),
                Expires = validDays > 0 ? DateTime.Now.AddDays(validDays).ToString("o") : "",
                Nonce = Convert.ToBase64String(Auth.RandomBytes(9))
            };
            if (!string.IsNullOrEmpty(pin)) kf.PinHash = Auth.Hash(pin, kf.Nonce);
            kf.Sig = Auth.Sign(kf.Payload(), store.Install.Secret);
            return kf;
        }

        public static string Write(string driveRoot, UsbKeyFile kf)
        {
            try
            {
                string path = Path.Combine(driveRoot, KeyFileName);
                File.WriteAllText(path, Json.Dump(kf), new UTF8Encoding(false));
                try { File.SetAttributes(path, FileAttributes.Hidden | FileAttributes.System); } catch { }
                return path;
            }
            catch (Exception ex)
            {
                Paths.Log("写入U盘密钥失败: " + ex.Message);
                return null;
            }
        }

        public static bool DeleteKey(string driveRoot)
        {
            try
            {
                string path = Path.Combine(driveRoot, KeyFileName);
                if (File.Exists(path))
                {
                    try { File.SetAttributes(path, FileAttributes.Normal); } catch { }
                    File.Delete(path);
                }
                return true;
            }
            catch (Exception ex) { Paths.Log("删除U盘密钥失败: " + ex.Message); return false; }
        }

        public static bool CheckPin(UsbKeyFile kf, string pin)
        {
            if (kf == null) return false;
            if (!kf.HasPin) return true;
            return Auth.FixedEquals(Auth.Hash(pin ?? "", kf.Nonce), kf.PinHash);
        }
    }
}
