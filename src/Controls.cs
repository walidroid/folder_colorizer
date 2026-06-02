using System;
using System.Drawing;
using System.Windows.Forms;

namespace FolderPainter
{
    // ── Color swatch button ──────────────────────────────────────────────
    public class ColorSwatch : Control
    {
        public Color FillColor   { get; }
        public Color ShadowColor { get; }
        private readonly string _name;

        public ColorSwatch(string name, Color fill, Color shadow)
        {
            _name       = name;
            FillColor   = fill;
            ShadowColor = shadow;
            Size        = new Size(28, 28);
            Cursor      = Cursors.Hand;

            var tip = new ToolTip();
            tip.SetToolTip(this, name);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using var brush = new SolidBrush(FillColor);
            using var pen   = new Pen(Color.FromArgb(80, Color.Black), 1.5f);
            g.FillEllipse(brush, 2, 2, 22, 22);
            g.DrawEllipse(pen, 2, 2, 22, 22);

            // Sheen
            using var sheen = new System.Drawing.Drawing2D.LinearGradientBrush(
                new PointF(2,2), new PointF(2,14),
                Color.FromArgb(60, Color.White), Color.Transparent);
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddEllipse(2, 2, 22, 10);
            g.FillPath(sheen, path);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            BackColor = Color.FromArgb(60, 60, 60);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            BackColor = Color.FromArgb(30, 30, 30);
        }
    }

    // ── Flat button ──────────────────────────────────────────────────────
    public class FlatButton : Control
    {
        public Color AccentColor { get; set; } = Color.FromArgb(70, 70, 70);
        private bool _hover;

        public FlatButton(string text)
        {
            Text   = text;
            Cursor = Cursors.Hand;
            Font   = new Font("Segoe UI", 9f);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var bg = _hover ? ControlPaint.Light(AccentColor, 0.15f) : AccentColor;
            using var brush = new SolidBrush(bg);
            using var path  = RoundRect(ClientRectangle, 4);
            g.FillPath(brush, path);

            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var tb = new SolidBrush(Color.White);
            g.DrawString(Text, Font, tb, ClientRectangle, sf);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true;  Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); }

        private static System.Drawing.Drawing2D.GraphicsPath RoundRect(Rectangle r, float radius)
        {
            var p = new System.Drawing.Drawing2D.GraphicsPath();
            float d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d - 1, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d - 1, r.Bottom - d - 1, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d - 1, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // ── Toggle button (for textures) ─────────────────────────────────────
    public class ToggleButton : Control
    {
        public string Label { get; }
        private bool _isOn;
        public bool IsOn
        {
            get => _isOn;
            set { _isOn = value; Invalidate(); }
        }

        public ToggleButton(string label)
        {
            Label  = label;
            Size   = new Size(72, 24);
            Cursor = Cursors.Hand;
            Font   = new Font("Segoe UI", 8f);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var bg = _isOn ? Color.FromArgb(0, 120, 215) : Color.FromArgb(55, 55, 55);
            using var brush = new SolidBrush(bg);
            using var path  = RoundRect(ClientRectangle, 3);
            g.FillPath(brush, path);

            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var tb = new SolidBrush(Color.White);
            g.DrawString(Label, Font, tb, ClientRectangle, sf);
        }

        protected override void OnClick(EventArgs e) { IsOn = true; base.OnClick(e); }

        private static System.Drawing.Drawing2D.GraphicsPath RoundRect(Rectangle r, float radius)
        {
            var p = new System.Drawing.Drawing2D.GraphicsPath();
            float d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d - 1, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d - 1, r.Bottom - d - 1, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d - 1, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
