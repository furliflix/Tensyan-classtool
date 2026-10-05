using System;
using Tensyan.Core;

namespace Tensyan.UI
{
    /// <summary>
    /// 桌面端后台服务的持有者：配对信息 + 局域网服务。
    /// 放在 UI 层（Core 要保持跨端通用，不掺桌面专有逻辑）。
    /// </summary>
    public static class Lan
    {
        public static PairingStore Pairing;
        public static LanService Service;
        public static bool WantsExit;      // 托盘"退出"时置位，主窗体才真正关闭

        public static void EnsureStarted(App app)
        {
            try
            {
                if (Pairing == null) Pairing = new PairingStore();
                if (Service == null)
                {
                    Service = new LanService(app.Store, Pairing);
                    Service.Start();
                }
            }
            catch (Exception ex)
            {
                Paths.Log("局域网服务启动失败：" + ex);
            }
        }

        public static void Shutdown()
        {
            try { if (Service != null) Service.Stop(); } catch { }
            try { if (Pairing != null) Pairing.Save(); } catch { }
            Service = null;
        }
    }
}
