using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace FolderPainter
{
    /// <summary>
    /// Core engine: renders folder icons and applies/removes desktop.ini.
    /// Used by both MainForm (full UI) and headless CLI mode (--color / --reset).
    /// </summary>
    public static class FolderIconEngine
    {
        // ── P/Invoke ──────────────────────────────────────────────────────────
        [DllImport("Shell32.dll", CharSet = CharSet.Auto)]
        private static extern void SHChangeNotify(int wEventId, uint uFlags, string item1, string item2);

        private const int  SHCNE_UPDATEITEM   = 0x00002000;
        private const int  SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNF_PATH         = 0x0005;
        private const uint SHCNF_FLUSH        = 0x1000;

        // ── Color Presets ─────────────────────────────────────────────────────
        // Name   = single-word CLI key (no spaces) used in --color argument
        // Display = human-readable name shown in UI
        public static readonly (string Name, string Display, string Emoji, Color Fill, Color Shadow)[] Presets =
        {
            ("Default", "Default",  "🟡", Color.FromArgb(255, 196, 116), Color.FromArgb(200, 150,  70)),
            ("Red",     "Red",      "🔴", Color.FromArgb(220,  70,  70), Color.FromArgb(160,  30,  30)),
            ("Orange",  "Orange",   "🟠", Color.FromArgb(240, 150,  50), Color.FromArgb(180, 100,  20)),
            ("Yellow",  "Yellow",   "🟡", Color.FromArgb(245, 210,  60), Color.FromArgb(190, 155,  20)),
            ("Lime",    "Lime",     "🟢", Color.FromArgb(120, 200,  60), Color.FromArgb( 70, 145,  20)),
            ("Green",   "Green",    "🟢", Color.FromArgb( 60, 180,  80), Color.FromArgb( 20, 120,  40)),
            ("Teal",    "Teal",     "🩵", Color.FromArgb( 40, 180, 170), Color.FromArgb( 10, 120, 115)),
            ("Sky",     "Sky Blue", "🔵", Color.FromArgb( 60, 160, 230), Color.FromArgb( 20, 100, 170)),
            ("Blue",    "Blue",     "🔵", Color.FromArgb( 55, 100, 220), Color.FromArgb( 20,  50, 160)),
            ("Indigo",  "Indigo",   "🟣", Color.FromArgb(100,  80, 200), Color.FromArgb( 55,  30, 140)),
            ("Purple",  "Purple",   "🟣", Color.FromArgb(160,  60, 200), Color.FromArgb(100,  20, 140)),
            ("Pink",    "Pink",     "🩷", Color.FromArgb(230,  80, 160), Color.FromArgb(170,  30, 100)),
            ("Dark",    "Dark",     "⚫", Color.FromArgb( 70,  70,  70), Color.FromArgb( 30,  30,  30)),
            ("White",   "White",    "⚪", Color.FromArgb(235, 235, 235), Color.FromArgb(180, 180, 180)),
        };

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Apply a named color preset silently (used from CLI --color mode).</summary>
        public static void ApplyByName(string folderPath, string colorName)
        {
            foreach (var p in Presets)
                if (string.Equals(p.Name, colorName, StringComparison.OrdinalIgnoreCase))
                {
                    Apply(folderPath, p.Fill, p.Shadow, "None", p.Name);
                    return;
                }
            throw new ArgumentException($"Unknown color preset: '{colorName}'");
        }

        /// <summary>Apply a custom color and optional texture to a folder.</summary>
        public static void Apply(string folderPath, Color fill, Color shadow, string texture, string colorName = "Custom")
        {
            // 1. Prepare the hidden .FolderPainter directory
            string iconFolder = Path.Combine(folderPath, ".FolderPainter");
            Directory.CreateDirectory(iconFolder);

            // Strip attributes on existing files so we can overwrite them freely
            // (files written previously have Hidden+System which blocks File.Create)
            foreach (string existingFile in Directory.GetFiles(iconFolder))
                File.SetAttributes(existingFile, FileAttributes.Normal);

            // Mark the directory itself as Hidden+System
            new DirectoryInfo(iconFolder).Attributes =
                FileAttributes.Directory | FileAttributes.Hidden | FileAttributes.System;

            // 2. Render and save the ICO — must exist BEFORE desktop.ini is written
            string icoPath = Path.Combine(iconFolder, "folder.ico");
            using (var b256 = RenderFolderIcon(fill, shadow, texture, 256))
            using (var b128 = RenderFolderIcon(fill, shadow, texture, 128))
            using (var b64  = RenderFolderIcon(fill, shadow, texture,  64))
            using (var b48  = RenderFolderIcon(fill, shadow, texture,  48))
            using (var b32  = RenderFolderIcon(fill, shadow, texture,  32))
            using (var b16  = RenderFolderIcon(fill, shadow, texture,  16))
                SaveMultiResIco(icoPath, new[] { b256, b128, b64, b48, b32, b16 });

            // 3. Write state metadata so the UI can highlight the current color on next open
            string statePath    = Path.Combine(iconFolder, "state.txt");
            string stateContent = $"Color: {colorName}\r\nTexture: {texture}\r\nFill: #{fill.ToArgb():X8}\r\nShadow: #{shadow.ToArgb():X8}\r\n";
            File.WriteAllText(statePath, stateContent, Encoding.UTF8);

            // 4. Write desktop.ini as UTF-16 LE with BOM — REQUIRED by Windows Explorer
            //    Clear any previous attributes first so the write succeeds.
            //    [ViewState] FolderType=Generic prevents Explorer from showing a content-
            //    preview thumbnail (the "file stack" view) which would override the custom icon.
            string iniPath    = Path.Combine(folderPath, "desktop.ini");
            if (File.Exists(iniPath))
                File.SetAttributes(iniPath, FileAttributes.Normal);
            string iniContent =
                "[.ShellClassInfo]\r\n" +
                "IconResource=.FolderPainter\\folder.ico,0\r\n" +
                "[ViewState]\r\n" +
                "FolderType=Generic\r\n";
            File.WriteAllText(iniPath, iniContent, Encoding.Unicode);

            // 5. Now lock down the files with Hidden+System
            File.SetAttributes(statePath, FileAttributes.Hidden | FileAttributes.System);
            File.SetAttributes(iniPath,   FileAttributes.Hidden | FileAttributes.System);

            // 6. Set folder attributes
            //    ReadOnly = signals Explorer this folder has a custom desktop.ini appearance
            //    System   = further ensures Explorer processes the desktop.ini
            FileAttributes fa = File.GetAttributes(folderPath);
            File.SetAttributes(folderPath,
                (fa | FileAttributes.ReadOnly | FileAttributes.System) & ~FileAttributes.Normal);

            // 7. Notify Explorer to refresh the icon immediately
            RefreshFolder(folderPath);
        }

        /// <summary>Remove custom icon and restore the folder to default appearance.</summary>
        public static void Remove(string folderPath)
        {
            // Delete .FolderPainter subfolder
            string iconFolder = Path.Combine(folderPath, ".FolderPainter");
            if (Directory.Exists(iconFolder))
            {
                // Clear hidden/system attributes so deletion succeeds
                foreach (string f in Directory.GetFiles(iconFolder))
                    File.SetAttributes(f, FileAttributes.Normal);
                Directory.Delete(iconFolder, recursive: true);
            }

            // Delete desktop.ini
            string iniPath = Path.Combine(folderPath, "desktop.ini");
            if (File.Exists(iniPath))
            {
                File.SetAttributes(iniPath, FileAttributes.Normal);
                File.Delete(iniPath);
            }

            // Restore folder attributes to normal (remove ReadOnly and System)
            FileAttributes fa = File.GetAttributes(folderPath);
            File.SetAttributes(folderPath, fa & ~(FileAttributes.ReadOnly | FileAttributes.System));

            RefreshFolder(folderPath);
        }

        /// <summary>Retrieve the current color and texture state of a folder.</summary>
        public static (string ColorName, Color? Fill, Color? Shadow, string Texture) GetCurrentState(string folderPath)
        {
            string statePath = Path.Combine(folderPath, ".FolderPainter", "state.txt");
            if (!File.Exists(statePath))
            {
                return (null, null, null, null);
            }

            try
            {
                string[] lines = File.ReadAllLines(statePath);
                string colorName = null;
                string texture = "None";
                Color? fill = null;
                Color? shadow = null;

                foreach (string line in lines)
                {
                    int idx = line.IndexOf(':');
                    if (idx < 0) continue;
                    string key = line.Substring(0, idx).Trim().ToLowerInvariant();
                    string val = line.Substring(idx + 1).Trim();

                    if (key == "color") colorName = val;
                    else if (key == "texture") texture = val;
                    else if (key == "fill")
                    {
                        if (val.StartsWith("#")) val = val.Substring(1);
                        if (int.TryParse(val, System.Globalization.NumberStyles.HexNumber, null, out int argb))
                            fill = Color.FromArgb(argb);
                    }
                    else if (key == "shadow")
                    {
                        if (val.StartsWith("#")) val = val.Substring(1);
                        if (int.TryParse(val, System.Globalization.NumberStyles.HexNumber, null, out int argb))
                            shadow = Color.FromArgb(argb);
                    }
                }
                return (colorName, fill, shadow, texture);
            }
            catch
            {
                return (null, null, null, null);
            }
        }

        // ── Context-Menu Icon Helpers ─────────────────────────────────────────

        /// <summary>Returns the directory used to cache preset color-circle icons.</summary>
        public static string GetIconCacheDir()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FolderPainter", "icons");
            Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>
        /// Renders a small colored-circle bitmap suitable for context menu icons.
        /// </summary>
        public static Bitmap RenderColorCircle(Color fill, int size)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Color.Transparent);

            int m = 1; // margin
            using (var b = new SolidBrush(fill))
                g.FillEllipse(b, m, m, size - m * 2, size - m * 2);

            // subtle sheen on top half
            using var sheen = new LinearGradientBrush(
                new PointF(m, m), new PointF(m, (size - m * 2) / 2f + m),
                Color.FromArgb(90, Color.White), Color.Transparent);
            using var sheenPath = new GraphicsPath();
            sheenPath.AddEllipse(m, m, size - m * 2, (size - m * 2) / 2);
            g.FillPath(sheen, sheenPath);

            return bmp;
        }

        /// <summary>
        /// Generates a small colored-circle .ico for each color preset and saves them to
        /// %LocalAppData%\FolderPainter\icons\. Called once during context-menu registration.
        /// </summary>
        public static void SavePresetIcons()
        {
            string iconDir = GetIconCacheDir();
            foreach (var preset in Presets)
            {
                string icoPath = Path.Combine(iconDir, preset.Name + ".ico");
                using var b32 = RenderColorCircle(preset.Fill, 32);
                using var b16 = RenderColorCircle(preset.Fill, 16);
                SaveMultiResIco(icoPath, new[] { b32, b16 });
            }
        }

        // ── Icon Rendering ────────────────────────────────────────────────────

        public static Bitmap RenderFolderIcon(Color fill, Color shadow, string texture, int size)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Color.Transparent);

            float w = size, h = size;
            float tabW = w * 0.38f, tabH = h * 0.12f;
            float bodyY = h * 0.17f;
            float bodyH = h * 0.72f;

            // Drop shadow
            using (var dark = new SolidBrush(Color.FromArgb(80, shadow)))
            using (var sp = RoundRect(new RectangleF(w * 0.06f + 2, bodyY + 3, w * 0.88f, bodyH), size * 0.04f))
                g.FillPath(dark, sp);

            // Folder tab (top flap)
            using (var tb = new SolidBrush(ControlPaint.Light(fill, 0.15f)))
            using (var tp = RoundRect(new RectangleF(w * 0.05f, h * 0.10f, tabW, tabH + 4), size * 0.04f))
                g.FillPath(tb, tp);

            // Folder body (gradient)
            var bodyRect = new RectangleF(w * 0.05f, bodyY, w * 0.90f, bodyH);
            using (var grad = new LinearGradientBrush(
                new PointF(0, bodyY), new PointF(0, bodyY + bodyH),
                ControlPaint.Light(fill, 0.1f), shadow))
            using (var bp = RoundRect(bodyRect, size * 0.06f))
                g.FillPath(grad, bp);

            // Texture overlay
            DrawTexture(g, bodyRect, texture, size);

            // Sheen highlight on upper portion
            using (var sheen = new LinearGradientBrush(
                new PointF(0, bodyY), new PointF(0, bodyY + bodyH * 0.45f),
                Color.FromArgb(60, Color.White), Color.Transparent))
            using (var shp = RoundRect(
                new RectangleF(bodyRect.X, bodyRect.Y, bodyRect.Width, bodyRect.Height * 0.45f),
                size * 0.06f))
                g.FillPath(sheen, shp);

            return bmp;
        }

        // ── Private Helpers ───────────────────────────────────────────────────

        private static void RefreshFolder(string folderPath)
        {
            // Notify Explorer that the specific folder's icon has changed
            SHChangeNotify(SHCNE_UPDATEITEM, SHCNF_PATH | SHCNF_FLUSH, folderPath, null);
            // Flush the shell icon cache globally to prevent stale cached icons
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_FLUSH, null, null);
        }

        private static void DrawTexture(Graphics g, RectangleF rect, string texture, int size)
        {
            if (texture == "None") return;

            using var pen  = new Pen(Color.FromArgb(35, Color.Black), Math.Max(1, size / 64f));
            float step = size * 0.12f;
            using var clip = RoundRect(rect, size * 0.06f);
            g.SetClip(clip);

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
                    bool off = false;
                    float bh = step * 0.9f, bw = step * 1.8f;
                    for (float y = rect.Top; y < rect.Bottom; y += bh)
                    {
                        float ox = off ? bw / 2 : 0;
                        for (float x = rect.Left - ox; x < rect.Right; x += bw)
                            g.DrawRectangle(pen, x, y, bw, bh);
                        off = !off;
                    }
                    break;
            }
            g.ResetClip();
        }

        private static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = radius * 2;
            p.AddArc(r.X,         r.Y,          d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y,          d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d,   0, 90);
            p.AddArc(r.X,         r.Bottom - d, d, d,  90, 90);
            p.CloseFigure();
            return p;
        }

        private static void SaveMultiResIco(string path, Bitmap[] bitmaps)
        {
            using var fs = File.Create(path);
            using var bw = new BinaryWriter(fs);

            // ICONDIR header
            bw.Write((short)0);               // reserved
            bw.Write((short)1);               // type = icon
            bw.Write((short)bitmaps.Length);

            // Encode each bitmap as PNG
            var pngs = new List<byte[]>();
            foreach (var bmp in bitmaps)
            {
                using var ms = new MemoryStream();
                bmp.Save(ms, ImageFormat.Png);
                pngs.Add(ms.ToArray());
            }

            // Write ICONDIRENTRY records
            int offset = 6 + bitmaps.Length * 16;
            for (int i = 0; i < bitmaps.Length; i++)
            {
                int sz = bitmaps[i].Width;
                bw.Write((byte)(sz >= 256 ? 0 : sz)); // width  (0 means 256)
                bw.Write((byte)(sz >= 256 ? 0 : sz)); // height
                bw.Write((byte)0);                     // color count
                bw.Write((byte)0);                     // reserved
                bw.Write((short)1);                    // planes
                bw.Write((short)32);                   // bit count
                bw.Write(pngs[i].Length);
                bw.Write(offset);
                offset += pngs[i].Length;
            }

            // Write PNG data
            foreach (var png in pngs)
                bw.Write(png);
        }
    }
}
