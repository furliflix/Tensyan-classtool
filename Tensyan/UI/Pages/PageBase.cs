using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Tensyan.Core;

namespace Tensyan.UI.Pages
{
    /// <summary>页面基类（Fluent）：统一内边距、标题区与工具栏布局，尺寸按 DPI 缩放。</summary>
    public class PageBase : UserControl
    {
        public App App;
        protected Panel Head;
        protected Panel Tools;
        protected Panel Body;
        protected string TitleText = "";
        protected string SubText = "";

        public PageBase(App app)
        {
            App = app;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            BackColor = Theme.Bg;
            Font = Theme.Body();
            AutoScaleMode = AutoScaleMode.None;

            Head = new Panel { BackColor = Theme.Bg, Height = Theme.P(64) };
            Head.Paint += HeadPaint;
            Controls.Add(Head);

            Tools = new Panel { BackColor = Theme.Bg, Height = Theme.P(52), Visible = false };
            Controls.Add(Tools);

            Body = new Panel { BackColor = Theme.Bg };
            Controls.Add(Body);
        }

        protected void EnableTools(int height = 52)
        {
            Tools.Visible = true;
            Tools.Height = Theme.P(height);
        }

        protected void HeadPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var f = Theme.Title())
            using (var b = new SolidBrush(Theme.TextMain))
                g.DrawString(TitleText, f, b, 0, Theme.P(6));
            using (var f = Theme.Caption())
            using (var b = new SolidBrush(Theme.TextSub))
                g.DrawString(SubText, f, b, Theme.P(1), Theme.P(36));
        }

        protected RoundButton MkBtn(string text, IconKind? icon, int width, Color? fill = null, bool soft = true, Color? outline = null)
        {
            var b = new RoundButton
            {
                Text = text,
                Size = Theme.PS(width, 34),
                Font = Theme.Body(),
                Soft = soft,
                Fill = soft ? Theme.Card : (fill ?? Theme.Primary),
                OutlineColor = outline ?? (soft ? Theme.BorderStrong : (fill ?? Theme.Primary)),
                TextColor = Color.White
            };
            if (icon.HasValue) b.Icon = icon;
            return b;
        }

        public virtual void Reload() { }

        private double _enter;   // 0 = 已就位，1 = 初始偏移

        /// <summary>页面进入动画：内容从右侧轻微滑入（Fluent 的入场动效）。</summary>
        public void PlayEnter()
        {
            if (!Anim.Enabled) { _enter = 0; Layout3(); return; }
            _enter = 1;
            Layout3();
            Anim.Run(this, 260, t =>
            {
                if (IsDisposed) return;
                _enter = 1 - Anim.EaseOutCubic(t);
                Layout3();
            }, () =>
            {
                if (IsDisposed) return;
                _enter = 0;
                Layout3();
            });
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Layout3();
        }

        protected virtual void Layout3()
        {
            int dx = (int)(_enter * Theme.P(22));
            int pad = Theme.P(24);
            int w = Math.Max(0, ClientSize.Width - pad * 2);
            int y = Theme.P(14);
            Head.SetBounds(pad + dx, y, w, Head.Height);
            y += Head.Height;
            if (Tools.Visible)
            {
                Tools.SetBounds(pad + dx, y + Theme.P(4), w, Tools.Height);
                y += Tools.Height + Theme.P(8);
            }
            Body.SetBounds(pad + dx, y, w, Math.Max(0, ClientSize.Height - y - Theme.P(14)));
        }

        protected void SetSub(string text)
        {
            SubText = text;
            Head.Invalidate();
        }
    }
}
