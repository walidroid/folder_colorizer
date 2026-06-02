using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FolderPainter
{
    public class MainForm : Form
    {
        // ── Win32 helpers ────────────────────────────────────────────────
        [DllImport("Shell32.dll", CharSet = CharSet.Auto)]
        private static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);
        private const int SHCNE_ASSOCCHANGED = 0x08000000;
        private const int SHCNF_FLUSH = 0x1000;

        // ── Fields ───────────────────────────────────────────────────────
        private readonly string _folderPath;

        // preset colors
        private static readonly (string Name, Color Fill, Color Shadow)[] Presets =
        {
            ("Default",     Color.FromArgb(255, 196, 116), Color.FromArgb(200, 150,  70)),
            ("Red",         Color.FromArgb(220,  70,  70), Color.FromArgb(160,  30,  30)),
            ("Orange",      Color.FromArgb(240, 150,  50), Color.FromArgb(180, 100,  20)),
            ("Yellow",      Color.FromArgb(245, 210,  60), Color.FromArgb(190, 155,  20)),
            ("Lime",        Color.FromArgb(120, 200,  60), Color.FromArgb( 70, 145,  20)),
            ("Green",       Color.FromArgb( 60, 180,  80), Color.FromArgb( 20, 120,  40)),
            ("Teal",        Color.FromArgb( 40, 180, 170), Color.FromArgb( 10, 120, 115)),
            ("Sky Blue",    Color.FromArgb( 60, 160, 230), Color.FromArgb( 20, 100, 170)),
            ("Blue",        Color.FromArgb( 55, 100, 220), Color.FromArgb( 20,  50, 160)),
            ("Indigo",      Color.FromArgb(100,  80, 200), Color.FromArgb( 55,  30, 140)),
            ("Purple",      Color.FromArgb(160,  60, 200), Color.FromArgb(100,  20, 140)),
            ("Pink",        Color.FromArgb(230,  80, 160), Color.FromArgb(170,  30, 100)),
            ("Dark",        Color.FromArgb( 70,  70,  70), Color.FromArgb( 30,  30,  30)),
            ("White",       Color.FromArgb(235, 235, 235), Color.FromArgb(180, 180, 180)),
        };

        // texture overlays
        private static readonly string[] TextureNames = { "None", "Dots", "Grid", "Diagonal", "Crosshatch", "Brick" };

        private Color _selectedFill    = Presets[0].Fill;
        private Color _selectedShadow  = Presets[0].Shadow;
        private string _selectedTexture = "None";
        private Panel _previewPanel;

        // ── Constructor ──────────────────────────────────────────────────
        public MainForm(string folderPath)
        {
            _folderPath = folderPath;
            BuildUI();
            UpdatePreview();
        }

        // ── UI Builder ───────────────────────────────────────────────────
        private void BuildUI()
        {
            Text          = "Folder Painter  –  " + Path.GetFileName(_folderPath);
            Size          = new Size(540, 520);
            MinimumSize   = new Size(540, 520);
            MaximumSize   = new Size(540, 520);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor     = Color.FromArgb(30, 30, 30);
            ForeColor     = Color.White;
            Font          = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = SystemIcons.Application;

            // ── Title bar label ─────────────────────────────────────────
            var title = new Label
            {
                Text      = "🎨  Folder Painter",
                Font      = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = true,
                Location  = new Point(16, 12)
            };
            Controls.Add(title);

            var pathLabel = new Label
            {
                Text      = _folderPath,
                Font      = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(160, 160, 160),
                AutoSize  = false,
                Width     = 500,
                Height    = 16,
                Location  = new Point(16, 38),
                AutoEllipsis = true
            };
            Controls.Add(pathLabel);

            // ── Separator ───────────────────────────────────────────────
            var sep1 = new Panel { BackColor = Color.FromArgb(60,60,60), Location = new Point(16,58), Size = new Size(500,1) };
            Controls.Add(sep1);

            // ── Preview ─────────────────────────────────────────────────
            var previewLabel = new Label
            {
                Text = "PREVIEW", Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = Color.FromArgb(130,130,130), AutoSize = true, Location = new Point(16, 68)
            };
            Controls.Add(previewLabel);

            _previewPanel = new Panel
            {
                Location  = new Point(16, 88),
                Size      = new Size(506, 100),
                BackColor = Color.FromArgb(22, 22, 22)
            };
            _previewPanel.Paint += OnPreviewPaint;
            Controls.Add(_previewPanel);

            // ── Colors ──────────────────────────────────────────────────
            var colorLabel = new Label
            {
                Text = "COLORS", Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = Color.FromArgb(130,130,130), AutoSize = true, Location = new Point(16, 202)
            };
            Controls.Add(colorLabel);

            int cx = 16, cy = 222;
            foreach (var (name, fill, shadow) in Presets)
            {
                var swatch = new ColorSwatch(name, fill, shadow);
                swatch.Location = new Point(cx, cy);
                swatch.Click   += (s, e) => OnColorSwatchClick((ColorSwatch)s);
                Controls.Add(swatch);
                cx += 34;
                if (cx > 16 + 14 * 34) { cx = 16; cy += 34; }
            }

            // Custom color button
            var customBtn = new FlatButton("+ Custom")
            {
                Location = new Point(16, cy + 38),
                Size     = new Size(90, 26)
            };
            customBtn.Click += OnCustomColorClick;
            Controls.Add(customBtn);

            // ── Textures ────────────────────────────────────────────────
            var texLabel = new Label
            {
                Text = "TEXTURE OVERLAY", Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = Color.FromArgb(130,130,130), AutoSize = true, Location = new Point(16, 302)
            };
            Controls.Add(texLabel);

            int tx = 16;
            foreach (var tex in TextureNames)
            {
                var t = tex; // capture
                var btn = new ToggleButton(t) { Location = new Point(tx, 322) };
                btn.IsOn = (t == _selectedTexture);
                btn.Click += (s, _) => {
                    _selectedTexture = ((ToggleButton)s).Label;
                    foreach (Control c in Controls)
                        if (c is ToggleButton tb) tb.IsOn = (tb.Label == _selectedTexture);
                    UpdatePreview();
                };
                Controls.Add(btn);
                tx += 76;
            }

            // ── Action Buttons ──────────────────────────────────────────
            var sep2 = new Panel { BackColor = Color.FromArgb(60,60,60), Location = new Point(16,358), Size = new Size(500,1) };
            Controls.Add(sep2);

            var applyBtn = new FlatButton("Apply") { Location = new Point(330, 370), Size = new Size(90, 32), AccentColor = Color.FromArgb(0,120,215) };
            applyBtn.Click += OnApplyClick;
            Controls.Add(applyBtn);

            var resetBtn = new FlatButton("Reset") { Location = new Point(432, 370), Size = new Size(76, 32) };
            resetBtn.Click += OnResetClick;
            Controls.Add(resetBtn);

            var cancelBtn = new FlatButton("Cancel") { Location = new Point(16, 370), Size = new Size(76, 32) };
            cancelBtn.Click += (_, __) => Close();
            Controls.Add(cancelBtn);
        }

        // ── Events ───────────────────────────────────────────────────────
        private void OnColorSwatchClick(ColorSwatch swatch)
        {
            _selectedFill   = swatch.FillColor;
            _selectedShadow = swatch.ShadowColor;
            UpdatePreview();
        }

        private void OnCustomColorClick(object sender, EventArgs e)
        {
            using var dlg = new ColorDialog { Color = _selectedFill, FullOpen = true };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                _selectedFill   = dlg.Color;
                _selectedShadow = ControlPaint.Dark(dlg.Color, 0.3f);
                UpdatePreview();
            }
        }

        private void OnApplyClick(object sender, EventArgs e)
        {
            try
            {
                ApplyIconToFolder(_folderPath, _selectedFill, _selectedShadow, _selectedTexture);
                MessageBox.Show("Folder icon updated!\n\nYou may need to press F5 or reopen Explorer to see the change.",
                    "Folder Painter", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error applying icon:\n" + ex.Message, "Folder Painter", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnResetClick(object sender, EventArgs e)
        {
            try
            {
                RemoveCustomIcon(_folderPath);
                MessageBox.Show("Folder icon reset to default.\n\nYou may need to press F5 to see the change.",
                    "Folder Painter", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error resetting icon:\n" + ex.Message, "Folder Painter", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Preview painting ─────────────────────────────────────────────
        private void UpdatePreview() => _previewPanel?.Invalidate();

        private void OnPreviewPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Draw 3 folder previews: small, medium, large
            DrawFolderPreview(g, _selectedFill, _selectedShadow, _selectedTexture,  30,  15, 60);
            DrawFolderPreview(g, _selectedFill, _selectedShadow, _selectedTexture, 170,   8, 76);
            DrawFolderPreview(g, _selectedFill, _selectedShadow, _selectedTexture, 330,   2, 96);

            // Label
            using var font = new Font("Segoe UI", 8f);
            using var brush = new SolidBrush(Color.FromArgb(140,140,140));
            g.DrawString(Path.GetFileName(_folderPath), font, brush, new PointF(16, 80));
        }

        // ── Core icon logic ──────────────────────────────────────────────
        private static void DrawFolderPreview(Graphics g, Color fill, Color shadow, string texture,
                                               int x, int y, int size)
        {
            using var bmp = RenderFolderIcon(fill, shadow, texture, size);
            g.DrawImage(bmp, x, y);
        }

        private static Bitmap RenderFolderIcon(Color fill, Color shadow, string texture, int size)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode    = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.Clear(Color.Transparent);

            float w = size, h = size;
            float tabW = w * 0.38f, tabH = h * 0.12f;
            float bodyY = h * 0.17f;
            float bodyH = h * 0.72f;

            // Shadow / depth
            using (var darkBrush = new SolidBrush(Color.FromArgb(80, shadow)))
            {
                var shadowRect = new RectangleF(w * 0.06f + 2, bodyY + 3, w * 0.88f, bodyH);
                g.FillPath(darkBrush, RoundRect(shadowRect, size * 0.04f));
            }

            // Tab (top flap)
            using var fillBrush = new SolidBrush(ControlPaint.Light(fill, 0.15f));
            var tabRect = new RectangleF(w * 0.05f, h * 0.10f, tabW, tabH + 4);
            g.FillPath(fillBrush, RoundRect(tabRect, size * 0.04f));

            // Body
            var bodyRect = new RectangleF(w * 0.05f, bodyY, w * 0.90f, bodyH);
            using (var grad = new System.Drawing.Drawing2D.LinearGradientBrush(
                new PointF(0, bodyY), new PointF(0, bodyY + bodyH),
                ControlPaint.Light(fill, 0.1f), shadow))
            {
                g.FillPath(grad, RoundRect(bodyRect, size * 0.06f));
            }

            // Texture overlay
            DrawTexture(g, bodyRect, texture, shadow, size);

            // Sheen
            using (var sheen = new System.Drawing.Drawing2D.LinearGradientBrush(
                new PointF(0, bodyY), new PointF(0, bodyY + bodyH * 0.45f),
                Color.FromArgb(60, Color.White), Color.Transparent))
            {
                g.FillPath(sheen, RoundRect(new RectangleF(bodyRect.X, bodyRect.Y, bodyRect.Width, bodyRect.Height * 0.45f), size * 0.06f));
            }

            return bmp;
        }

        private static void DrawTexture(Graphics g, RectangleF rect, string texture, Color baseColor, int size)
        {
            if (texture == "None") return;
            using var pen = new Pen(Color.FromArgb(35, Color.Black), Math.Max(1, size / 64f));
            float step = size * 0.12f;
            g.SetClip(RoundRect(rect, size * 0.06f));

            switch (texture)
            {
                case "Dots":
                    float r = Math.Max(1.5f, size * 0.03f);
                    for (float x = rect.Left + step; x < rect.Right; x += step)
                        for (float y = rect.Top + step; y < rect.Bottom; y += step)
                            g.FillEllipse(pen.Brush, x - r, y - r, r * 2, r * 2);
                    break;
                case "Grid":
                    for (float x = rect.Left + step; x < rect.Right; x += step)
                        g.DrawLine(pen, x, rect.Top, x, rect.Bottom);
                    for (float y = rect.Top + step; y < rect.Bottom; y += step)
                        g.DrawLine(pen, rect.Left, y, rect.Right, y);
                    break;
                case "Diagonal":
                    for (float d = rect.Left - rect.Height; d < rect.Right + rect.Height; d += step)
                        g.DrawLine(pen, d, rect.Top, d + rect.Height, rect.Bottom);
                    break;
                case "Crosshatch":
                    for (float d = rect.Left - rect.Height; d < rect.Right + rect.Height; d += step)
                    {
                        g.DrawLine(pen, d, rect.Top, d + rect.Height, rect.Bottom);
                        g.DrawLine(pen, d + rect.Height, rect.Top, d, rect.Bottom);
                    }
                    break;
                case "Brick":
                    bool offset = false;
                    float bh = step * 0.9f, bw = step * 1.8f;
                    for (float y = rect.Top; y < rect.Bottom; y += bh)
                    {
                        float ox = offset ? bw / 2 : 0;
                        for (float x = rect.Left - ox; x < rect.Right; x += bw)
                            g.DrawRectangle(pen, x, y, bw, bh);
                        offset = !offset;
                    }
                    break;
            }
            g.ResetClip();
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundRect(RectangleF r, float radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            float d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ── Folder icon application ──────────────────────────────────────
        private static void ApplyIconToFolder(string folderPath, Color fill, Color shadow, string texture)
        {
            string iconFolder = Path.Combine(folderPath, ".FolderPainter");
            Directory.CreateDirectory(iconFolder);
            File.SetAttributes(iconFolder, FileAttributes.Hidden | FileAttributes.System);

            string icoPath = Path.Combine(iconFolder, "folder.ico");
            using (var bmp256 = RenderFolderIcon(fill, shadow, texture, 256))
            using (var bmp128 = RenderFolderIcon(fill, shadow, texture, 128))
            using (var bmp64  = RenderFolderIcon(fill, shadow, texture,  64))
            using (var bmp48  = RenderFolderIcon(fill, shadow, texture,  48))
            using (var bmp32  = RenderFolderIcon(fill, shadow, texture,  32))
            using (var bmp16  = RenderFolderIcon(fill, shadow, texture,  16))
                SaveMultiResIco(icoPath, new[] { bmp256, bmp128, bmp64, bmp48, bmp32, bmp16 });

            // Write desktop.ini
            string iniPath = Path.Combine(folderPath, "desktop.ini");
            string iniContent = $"[.ShellClassInfo]\r\nIconResource=.FolderPainter\\folder.ico,0\r\nIconIndex=0\r\n";
            File.WriteAllText(iniPath, iniContent);

            // Mark folder as having custom icon
            var attr = File.GetAttributes(folderPath);
            File.SetAttributes(folderPath, attr | FileAttributes.System);
            File.SetAttributes(iniPath,   FileAttributes.Hidden | FileAttributes.System);

            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero);
        }

        private static void RemoveCustomIcon(string folderPath)
        {
            string iconFolder = Path.Combine(folderPath, ".FolderPainter");
            if (Directory.Exists(iconFolder))
                Directory.Delete(iconFolder, true);

            string iniPath = Path.Combine(folderPath, "desktop.ini");
            if (File.Exists(iniPath))
                File.Delete(iniPath);

            var attr = File.GetAttributes(folderPath);
            File.SetAttributes(folderPath, attr & ~FileAttributes.System);

            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero);
        }

        // ── ICO writer (multi-resolution) ────────────────────────────────
        private static void SaveMultiResIco(string path, Bitmap[] bitmaps)
        {
            using var fs = File.Create(path);
            using var bw = new BinaryWriter(fs);

            // ICONDIR header
            bw.Write((short)0);          // reserved
            bw.Write((short)1);          // type = icon
            bw.Write((short)bitmaps.Length);

            // Encode each bitmap as PNG into a MemoryStream
            var pngs = new List<byte[]>();
            foreach (var bmp in bitmaps)
            {
                using var ms = new MemoryStream();
                bmp.Save(ms, ImageFormat.Png);
                pngs.Add(ms.ToArray());
            }

            // Write ICONDIRENTRYs
            int dataOffset = 6 + bitmaps.Length * 16;
            for (int i = 0; i < bitmaps.Length; i++)
            {
                int sz = bitmaps[i].Width;
                bw.Write((byte)(sz >= 256 ? 0 : sz));  // width (0 = 256)
                bw.Write((byte)(sz >= 256 ? 0 : sz));  // height
                bw.Write((byte)0);                      // color count
                bw.Write((byte)0);                      // reserved
                bw.Write((short)1);                     // planes
                bw.Write((short)32);                    // bit count
                bw.Write(pngs[i].Length);
                bw.Write(dataOffset);
                dataOffset += pngs[i].Length;
            }

            // Write PNG data
            foreach (var png in pngs)
                bw.Write(png);
        }
    }
}
