using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;

namespace Tensyan.UI
{
    /// <summary>
    /// 极简补间动画引擎：一个共享计时器（约 66fps）驱动所有动画，
    /// 同一 key 重复调用会替换旧动画，避免悬停/连点造成的动画叠加。
    /// </summary>
    public static class Anim
    {
        /// <summary>关闭后所有动画立即跳到终态（“系统设置 → 偏好设置”里可关）。</summary>
        public static bool Enabled = true;

        private class Item
        {
            public object Key;
            public double Duration;
            public double Elapsed;
            public Action<double> Step;
            public Action Done;
        }

        private static readonly List<Item> _items = new List<Item>();
        private static System.Windows.Forms.Timer _timer;
        private static Stopwatch _clock;
        private static double _last;

        public static void Run(double ms, Action<double> step, Action done = null)
        {
            Run(new object(), ms, step, done);
        }

        public static void Run(object key, double ms, Action<double> step, Action done = null)
        {
            if (step == null) return;
            if (key == null) key = new object();

            for (int i = _items.Count - 1; i >= 0; i--)
                if (ReferenceEquals(_items[i].Key, key)) _items.RemoveAt(i);

            if (!Enabled || ms <= 1)
            {
                try { step(1); } catch { }
                if (done != null) { try { done(); } catch { } }
                return;
            }

            _items.Add(new Item { Key = key, Duration = ms, Step = step, Done = done });
            EnsureTimer();
        }

        public static void Cancel(object key)
        {
            for (int i = _items.Count - 1; i >= 0; i--)
                if (ReferenceEquals(_items[i].Key, key)) _items.RemoveAt(i);
        }

        public static bool IsRunning(object key)
        {
            foreach (var it in _items) if (ReferenceEquals(it.Key, key)) return true;
            return false;
        }

        private static void EnsureTimer()
        {
            if (_timer == null)
            {
                _clock = Stopwatch.StartNew();
                _timer = new System.Windows.Forms.Timer { Interval = 15 };
                _timer.Tick += (s, e) => Tick();
            }
            if (!_timer.Enabled)
            {
                _last = 0;
                _clock.Reset(); _clock.Start();
                _timer.Start();
            }
        }

        private static void Tick()
        {
            double now = _clock.Elapsed.TotalMilliseconds;
            double dt = now - _last;
            _last = now;
            if (dt <= 0) dt = 15;
            if (dt > 120) dt = 120;      // 卡顿后不要一次跳太多

            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var it = _items[i];
                it.Elapsed += dt;
                double t = it.Duration <= 0 ? 1 : Math.Min(1, it.Elapsed / it.Duration);
                try { it.Step(t); } catch { }
                if (t >= 1)
                {
                    _items.RemoveAt(i);
                    try { if (it.Done != null) it.Done(); } catch { }
                }
            }
            if (_items.Count == 0 && _timer != null) _timer.Stop();
        }

        // ---------------- 缓动函数 ----------------

        public static double Clamp01(double t) { return t < 0 ? 0 : (t > 1 ? 1 : t); }

        public static double Linear(double t) { return Clamp01(t); }

        public static double EaseOutQuad(double t) { t = Clamp01(t); return 1 - (1 - t) * (1 - t); }

        public static double EaseOutCubic(double t) { t = Clamp01(t); double u = 1 - t; return 1 - u * u * u; }

        public static double EaseInCubic(double t) { t = Clamp01(t); return t * t * t; }

        public static double EaseInOutCubic(double t)
        {
            t = Clamp01(t);
            return t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
        }

        /// <summary>回弹（略微过冲后落回），用于“弹出”效果。</summary>
        public static double EaseOutBack(double t, double s = 1.70158)
        {
            t = Clamp01(t);
            double c3 = s + 1;
            return 1 + c3 * Math.Pow(t - 1, 3) + s * Math.Pow(t - 1, 2);
        }

        /// <summary>正弦鼓包：0 → 1 → 0，用于脉冲。</summary>
        public static double Bump(double t, double peakAt = 0.35)
        {
            t = Clamp01(t);
            if (t >= 1) return 0;
            return t < peakAt ? EaseOutQuad(t / peakAt) : 1 - EaseInOutCubic((t - peakAt) / (1 - peakAt));
        }

        public static double Lerp(double a, double b, double t) { return a + (b - a) * Clamp01(t); }

        public static int LerpInt(double a, double b, double t) { return (int)Math.Round(Lerp(a, b, t)); }

        public static float LerpF(double a, double b, double t) { return (float)Lerp(a, b, t); }
    }
}
