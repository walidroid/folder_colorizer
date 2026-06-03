using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace FolderPainter
{
    public class MainForm : Form
    {
        // ── Fields ───────────────────────────────────────────────────────────
        private readonly string _folderPath;

        private Color  _selectedFill    = FolderIconEngine.Presets[0].Fill;
        private Color  _selectedShadow  = FolderIconEngine.Presets[0].Shadow;
        private string _selectedTexture = "None";
        private Panel  _previewPanel;

        private static readonly string[] TextureNames =
            { "None", "Dots", "Grid", "Diagonal", "Crosshatch", "Brick" };

        // ── Constructor ──────────────────────────────────────────────────────
        public MainForm(string folderPath)
        {
            _folderPath = folderPath;

            // Load current color/texture settings from state file if it exists
            var (colorName, fill, shadow, texture) = FolderIconEngine.GetCurrentState(_folderPath);
            if (fill != null && shadow != null)
            {
                _selectedFill = fill.Value;
                _selectedShadow = shadow.Value;
                _selectedTexture = texture ?? "None";
            }

            BuildUI();
            UpdateSelectedSwatches();
            UpdatePreview();
        }

        // ── UI Builder ───────────────────────────────────────────────────────
        private void BuildUI()
        {
            Text            = "Folder Painter v1.0.0  –  " + Path.GetFileName(_folderPath);
            Size            = new Size(540, 520);
            MinimumSize     = new Size(540, 520);
            MaximumSize     = new Size(540, 520);
            StartPosition   = FormStartPosition.CenterScreen;
            BackColor       = Color.FromArgb(30, 30, 30);
            ForeColor       = Color.White;
            Font            = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon            = SystemIcons.Application;

            // ── Title ────────────────────────────────────────────────────────
            Controls.Add(new Label
            {
                Text      = "🎨  Folder Painter",
                Font      = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = true,
                Location  = new Point(16, 12)
            });
            Controls.Add(new Label
            {
                Text         = _folderPath,
                Font         = new Font("Segoe UI", 8f),
                ForeColor    = Color.FromArgb(160, 160, 160),
                AutoSize     = false,
                Width        = 500,
                Height       = 16,
                Location     = new Point(16, 38),
                AutoEllipsis = true
            });
            Controls.Add(new Panel
            {
                BackColor = Color.FromArgb(60, 60, 60),
                Location  = new Point(16, 58),
                Size      = new Size(500, 1)
            });

            // ── Preview ──────────────────────────────────────────────────────
            Controls.Add(new Label
            {
                Text      = "PREVIEW",
                Font      = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = Color.FromArgb(130, 130, 130),
                AutoSize  = true,
                Location  = new Point(16, 68)
            });
            _previewPanel = new Panel
            {
                Location  = new Point(16, 88),
                Size      = new Size(506, 100),
                BackColor = Color.FromArgb(22, 22, 22)
            };
            _previewPanel.Paint += OnPreviewPaint;
            Controls.Add(_previewPanel);

            // ── Colors ───────────────────────────────────────────────────────
            Controls.Add(new Label
            {
                Text      = "COLORS",
                Font      = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = Color.FromArgb(130, 130, 130),
                AutoSize  = true,
                Location  = new Point(16, 202)
            });

            int cx = 16, cy = 222;
            foreach (var preset in FolderIconEngine.Presets)
            {
                var swatch = new ColorSwatch(preset.Display, preset.Fill, preset.Shadow);
                swatch.Location = new Point(cx, cy);
                swatch.Click   += (s, _) => OnColorSwatchClick((ColorSwatch)s);
                Controls.Add(swatch);
                cx += 34;
                if (cx > 16 + 14 * 34) { cx = 16; cy += 34; }
            }

            var customBtn = new FlatButton("+ Custom")
            {
                Location = new Point(16, cy + 38),
                Size     = new Size(90, 26)
            };
            customBtn.Click += OnCustomColorClick;
            Controls.Add(customBtn);

            // ── Textures ─────────────────────────────────────────────────────
            Controls.Add(new Label
            {
                Text      = "TEXTURE OVERLAY",
                Font      = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = Color.FromArgb(130, 130, 130),
                AutoSize  = true,
                Location  = new Point(16, 302)
            });

            int tx = 16;
            foreach (var tex in TextureNames)
            {
                var t   = tex;
                var btn = new ToggleButton(t) { Location = new Point(tx, 322) };
                btn.IsOn   = (t == _selectedTexture);
                btn.Click += (s, _) =>
                {
                    _selectedTexture = ((ToggleButton)s).Label;
                    foreach (Control c in Controls)
                        if (c is ToggleButton tb) tb.IsOn = (tb.Label == _selectedTexture);
                    UpdatePreview();
                };
                Controls.Add(btn);
                tx += 76;
            }

            // ── Action Buttons ────────────────────────────────────────────────
            Controls.Add(new Panel
            {
                BackColor = Color.FromArgb(60, 60, 60),
                Location  = new Point(16, 358),
                Size      = new Size(500, 1)
            });

            var applyBtn = new FlatButton("Apply")
            {
                Location    = new Point(330, 370),
                Size        = new Size(90, 32),
                AccentColor = Color.FromArgb(0, 120, 215)
            };
            applyBtn.Click += OnApplyClick;
            Controls.Add(applyBtn);

            var resetBtn = new FlatButton("Reset") { Location = new Point(432, 370), Size = new Size(76, 32) };
            resetBtn.Click += OnResetClick;
            Controls.Add(resetBtn);

            var cancelBtn = new FlatButton("Cancel") { Location = new Point(16, 370), Size = new Size(76, 32) };
            cancelBtn.Click += (_, __) => Close();
            Controls.Add(cancelBtn);
        }

        // ── Events ───────────────────────────────────────────────────────────

        private void OnColorSwatchClick(ColorSwatch swatch)
        {
            _selectedFill   = swatch.FillColor;
            _selectedShadow = swatch.ShadowColor;
            UpdateSelectedSwatches();
            UpdatePreview();
        }

        private void OnCustomColorClick(object sender, EventArgs e)
        {
            using var dlg = new ColorDialog { Color = _selectedFill, FullOpen = true };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                _selectedFill   = dlg.Color;
                _selectedShadow = ControlPaint.Dark(dlg.Color, 0.3f);
                UpdateSelectedSwatches();
                UpdatePreview();
            }
        }

        private void OnApplyClick(object sender, EventArgs e)
        {
            try
            {
                string presetName = "Custom";
                foreach (var p in FolderIconEngine.Presets)
                {
                    if (p.Fill.ToArgb() == _selectedFill.ToArgb() && p.Shadow.ToArgb() == _selectedShadow.ToArgb())
                    {
                        presetName = p.Name;
                        break;
                    }
                }
                FolderIconEngine.Apply(_folderPath, _selectedFill, _selectedShadow, _selectedTexture, presetName);
                MessageBox.Show(
                    "Folder icon updated!\n\nPress F5 or reopen Explorer to see the change.",
                    "Folder Painter", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error applying icon:\n" + ex.Message,
                    "Folder Painter", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateSelectedSwatches()
        {
            foreach (Control c in Controls)
            {
                if (c is ColorSwatch cs)
                {
                    cs.IsSelected = (cs.FillColor.ToArgb() == _selectedFill.ToArgb() &&
                                     cs.ShadowColor.ToArgb() == _selectedShadow.ToArgb());
                }
            }
        }

        private void OnResetClick(object sender, EventArgs e)
        {
            var confirm = MessageBox.Show(
                "Remove the custom icon and restore the folder to its default appearance?",
                "Folder Painter", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            try
            {
                FolderIconEngine.Remove(_folderPath);
                MessageBox.Show(
                    "Folder icon reset to default.\n\nPress F5 to see the change.",
                    "Folder Painter", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error resetting icon:\n" + ex.Message,
                    "Folder Painter", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Preview ───────────────────────────────────────────────────────────

        private void UpdatePreview() => _previewPanel?.Invalidate();

        private void OnPreviewPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Draw three folder previews at different sizes
            DrawFolderPreview(g, _selectedFill, _selectedShadow, _selectedTexture,  30, 15, 60);
            DrawFolderPreview(g, _selectedFill, _selectedShadow, _selectedTexture, 175,  8, 76);
            DrawFolderPreview(g, _selectedFill, _selectedShadow, _selectedTexture, 335,  2, 96);

            using var font  = new Font("Segoe UI", 8f);
            using var brush = new SolidBrush(Color.FromArgb(140, 140, 140));
            g.DrawString(Path.GetFileName(_folderPath), font, brush, new PointF(16, 80));
        }

        private static void DrawFolderPreview(Graphics g, Color fill, Color shadow, string texture,
                                              int x, int y, int size)
        {
            using var bmp = FolderIconEngine.RenderFolderIcon(fill, shadow, texture, size);
            g.DrawImage(bmp, x, y);
        }
    }
}
