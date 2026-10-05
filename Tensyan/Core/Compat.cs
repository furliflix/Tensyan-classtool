using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Tensyan.Core
{
    /// <summary>
    /// 跨框架兼容垫片：同一份源码要同时喂给 .NET 8（x64 版）、.NET Framework 4.8（32 位版）
    /// 和 .NET Framework 3.5（XP 特别版）。这里把各版本缺失的 API 收口到一处，
    /// 业务代码只调用这些辅助方法。
    /// </summary>
    public static class Str
    {
        /// <summary>
        /// string.IsNullOrWhiteSpace 是 .NET 4.0 才有的；XP 版（3.5）用等价实现。
        /// </summary>
        public static bool Blank(string s)
        {
            if (s == null) return true;
            for (int i = 0; i < s.Length; i++)
                if (!char.IsWhiteSpace(s[i])) return false;
            return true;
        }
    }

    /// <summary>并行/后台执行：.NET 3.5 没有 Task，用线程池等价实现。</summary>
    public static class TaskCompat
    {
        /// <summary>把一段工作丢到后台线程执行（不阻塞界面）。</summary>
        public static void Run(Action work)
        {
            if (work == null) return;
#if NET35
            ThreadPool.QueueUserWorkItem(delegate { try { work(); } catch { } });
#else
            System.Threading.Tasks.Task.Run(() =>
            {
                try { work(); } catch { }
            });
#endif
        }
    }

    /// <summary>程序目录：.NET 3.5 没有 AppContext。</summary>
    public static class AppInfo
    {
        public static string BaseDirectory
        {
            get
            {
#if NET35
                return AppDomain.CurrentDomain.BaseDirectory;
#else
                return AppContext.BaseDirectory;
#endif
            }
        }
    }

    /// <summary>路径拼接：Path.Combine 的三参数重载是 .NET 4.0 才有的。</summary>
    public static class PathCompat
    {
        public static string Combine(string a, string b, string c)
        {
            return System.IO.Path.Combine(System.IO.Path.Combine(a, b), c);
        }
    }

    /// <summary>PBKDF2-HMAC-SHA256：.NET 3.5 的 Rfc2898DeriveBytes 只支持 SHA1
    /// （带 HashAlgorithmName 的重载是 4.7.2 才有的），所以自己实现一份，
    /// 三个版本共用，保证同一个密码算出的哈希完全一致。
    /// </summary>
    public static class Pbkdf2
    {
        public static byte[] DeriveKey(string password, byte[] salt, int iterations, int keyLength)
        {
            if (password == null) password = "";
            if (salt == null) salt = new byte[0];
            if (iterations < 1) iterations = 1;

            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(password)))
            {
                int hashLen = hmac.HashSize / 8;                 // 32
                int blocks = (keyLength + hashLen - 1) / hashLen;
                var output = new byte[blocks * hashLen];
                var saltBlock = new byte[salt.Length + 4];

                for (int i = 0; i < blocks; i++)
                {
                    Buffer.BlockCopy(salt, 0, saltBlock, 0, salt.Length);
                    saltBlock[salt.Length + 0] = (byte)((i + 1) >> 24);
                    saltBlock[salt.Length + 1] = (byte)((i + 1) >> 16);
                    saltBlock[salt.Length + 2] = (byte)((i + 1) >> 8);
                    saltBlock[salt.Length + 3] = (byte)(i + 1);

                    byte[] u = hmac.ComputeHash(saltBlock);
                    var t = new byte[u.Length];
                    Buffer.BlockCopy(u, 0, t, 0, u.Length);

                    for (int it = 1; it < iterations; it++)
                    {
                        u = hmac.ComputeHash(u);
                        for (int k = 0; k < t.Length; k++) t[k] ^= u[k];
                    }
                    Buffer.BlockCopy(t, 0, output, i * hashLen, hashLen);
                }

                if (output.Length == keyLength) return output;
                var trimmed = new byte[keyLength];
                Buffer.BlockCopy(output, 0, trimmed, 0, keyLength);
                return trimmed;
            }
        }
    }
}
