using System;
using System.Security.Cryptography;
using System.Text;

namespace Tensyan.Core
{
    /// <summary>密码与签名：PBKDF2-SHA256，恒定时间比较。</summary>
    public static class Auth
    {
        public const int Iterations = 120000;
        private const int SaltLen = 16;
        private const int KeyLen = 32;

        public static byte[] RandomBytes(int n)
        {
            var b = new byte[n];
#if NET35
            // .NET 3.5 的 RandomNumberGenerator 不实现 IDisposable
            RandomNumberGenerator.Create().GetBytes(b);
#else
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(b);
#endif
            return b;
        }

        public static string Hash(string password, string saltBase64, int iterations = Iterations)
        {
            byte[] salt = Convert.FromBase64String(saltBase64);
            // 三个版本共用自己实现的 PBKDF2-HMAC-SHA256（.NET 3.5 的 Rfc2898DeriveBytes 只支持 SHA1）
            return Convert.ToBase64String(Pbkdf2.DeriveKey(password ?? "", salt, iterations, KeyLen));
        }

        public static void SetPassword(Account acc, string password)
        {
            acc.Salt = Convert.ToBase64String(RandomBytes(SaltLen));
            acc.PwdHash = Hash(password, acc.Salt);
        }

        public static bool Verify(Account acc, string password)
        {
            if (acc == null || string.IsNullOrEmpty(acc.Salt) || string.IsNullOrEmpty(acc.PwdHash)) return false;
            string h = Hash(password, acc.Salt);
            return FixedEquals(h, acc.PwdHash);
        }

        public static bool FixedEquals(string a, string b)
        {
            if (a == null || b == null) return false;
            byte[] x = Encoding.UTF8.GetBytes(a);
            byte[] y = Encoding.UTF8.GetBytes(b);
            if (x.Length != y.Length) return false;
            int diff = 0;
            for (int i = 0; i < x.Length; i++) diff |= x[i] ^ y[i];
            return diff == 0;
        }

        /// <summary>U盘密钥文件签名。</summary>
        public static string Sign(string payload, string secretBase64)
        {
            byte[] key = Convert.FromBase64String(secretBase64);
            using (var h = new HMACSHA256(key))
                return Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        }

        /// <summary>用字节密钥签名（U盘密钥里携带的共享密钥）。</summary>
        public static string SignBytes(string payload, byte[] key)
        {
            using (var h = new HMACSHA256(key))
                return Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        }

        public static string RandomToken(int bytes = 8)
        {
            return Convert.ToBase64String(RandomBytes(bytes)).Replace("+", "A").Replace("/", "B").Replace("=", "");
        }
    }
}
