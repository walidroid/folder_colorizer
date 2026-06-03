using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Diagnostics;

namespace FolderPainter
{
    /// <summary>
    /// Setup form: shown when app is launched without a folder argument.
    /// Registers or unregisters the cascading right-click context menu.
    /// </summary>
    public class SetupForm : Form
    {
        private Label _statusLabel;

        public SetupForm()
        {
            BuildUI();
            RefreshStatus();
        }

        private static bool IsAppInstalled()
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath)) return false;

                string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string pfX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

                return (!string.IsNullOrEmpty(pf) && exePath.StartsWith(pf, StringComparison.OrdinalIgnoreCase)) ||
                       (!string.IsNullOrEmpty(pfX86) && exePath.StartsWith(pfX86, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        private void BuildUI()
        {
            Text            = "Folder Painter v1.0.0 – Setup";
            bool installed  = IsAppInstalled();
            Size            = installed ? new Size(440, 195) : new Size(440, 310);
            MinimumSize     = Size;
            MaximumSize     = Size;
            StartPosition   = FormStartPosition.CenterScreen;
            BackColor       = Color.FromArgb(30, 30, 30);
            ForeColor       = Color.White;
            Font            = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon            = SystemIcons.Application;

            Controls.Add(new Label
            {
                Text      = "🎨  Folder Painter",
                Font      = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = true,
                Location  = new Point(20, 18)
            });
            Controls.Add(new Label
            {
                Text      = "Right-click any folder  →  🎨 Paint Folder  →  pick a color instantly",
                Font      = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(150, 150, 150),
                AutoSize  = true,
                Location  = new Point(20, 46)
            });
            Controls.Add(new Panel
            {
                BackColor = Color.FromArgb(60, 60, 60),
                Location  = new Point(20, 68),
                Size      = new Size(390, 1)
            });

            _statusLabel = new Label
            {
                Text      = "",
                Font      = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(200, 200, 200),
                AutoSize  = false,
                Size      = new Size(390, 60),
                Location  = new Point(20, 82)
            };
            Controls.Add(_statusLabel);

            if (installed)
            {
                _statusLabel.ForeColor = Color.FromArgb(140, 180, 250);
                _statusLabel.Text = "ℹ  This application is installed in Program Files.\n" +
                                    "Registry integration is managed by the system installer.";

                Controls.Add(new Label
                {
                    Text      = "To register or unregister the menu, please use the Windows Settings App or run the application from a portable directory.",
                    Font      = new Font("Segoe UI", 8.5f),
                    ForeColor = Color.FromArgb(150, 150, 150),
                    AutoSize  = false,
                    Size      = new Size(390, 40),
                    Location  = new Point(20, 130)
                });
            }
            else
            {
                // Register button
                var regBtn = new FlatButton("Register  (Add color submenu to right-click)")
                {
                    Location    = new Point(20, 160),
                    Size        = new Size(390, 34),
                    AccentColor = Color.FromArgb(0, 130, 100)
                };
                regBtn.Click += OnRegisterClick;
                Controls.Add(regBtn);

                // Unregister button
                var unregBtn = new FlatButton("Unregister  (Remove from right-click menu)")
                {
                    Location    = new Point(20, 204),
                    Size        = new Size(390, 34),
                    AccentColor = Color.FromArgb(120, 40, 40)
                };
                unregBtn.Click += OnUnregisterClick;
                Controls.Add(unregBtn);

                Controls.Add(new Label
                {
                    Text      = "⚠  Run as Administrator if registration fails.",
                    Font      = new Font("Segoe UI", 8f),
                    ForeColor = Color.FromArgb(180, 150, 60),
                    AutoSize  = true,
                    Location  = new Point(20, 254)
                });
            }
        }

        private void RefreshStatus()
        {
            if (IsAppInstalled()) return;

            try
            {
                using var key = Registry.ClassesRoot.OpenSubKey(@"Directory\shell\FolderPainterMenu");
                bool registered = (key != null);
                if (registered)
                {
                    _statusLabel.ForeColor = Color.FromArgb(80, 200, 120);
                    _statusLabel.Text = "✔  Context menu is REGISTERED.\n" +
                                        "Right-click any folder  →  🎨 Paint Folder  →  choose a color.";
                }
                else
                {
                    _statusLabel.ForeColor = Color.FromArgb(200, 100, 80);
                    _statusLabel.Text = "✘  Context menu is NOT registered.\n" +
                                        "Click \"Register\" below to add the color submenu.";
                }
            }
            catch
            {
                _statusLabel.Text = "Could not read registry status.";
            }
        }

        private void OnRegisterClick(object sender, EventArgs e)
        {
            try
            {
                RegisterContextMenu();
                MessageBox.Show(
                    "Registered successfully!\n\n" +
                    "Right-click any folder and choose:\n" +
                    "🎨 Paint Folder  ►  select a color",
                    "Folder Painter", MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshStatus();
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show(
                    "Access denied.\n\nPlease run FolderPainter.exe as Administrator to register.",
                    "Folder Painter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Folder Painter",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnUnregisterClick(object sender, EventArgs e)
        {
            try
            {
                UnregisterContextMenu();
                MessageBox.Show("Unregistered. The context menu entry has been removed.",
                    "Folder Painter", MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshStatus();
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show(
                    "Access denied.\n\nPlease run FolderPainter.exe as Administrator.",
                    "Folder Painter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Folder Painter",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Registry Helpers ─────────────────────────────────────────────────

        private static void RegisterContextMenu()
        {
            string exePath = Process.GetCurrentProcess().MainModule.FileName;
            string quoted  = $"\"{exePath}\"";

            // Generate colored-circle .ico files for each preset into %LocalAppData%\FolderPainter\icons\
            FolderIconEngine.SavePresetIcons();
            string iconDir = FolderIconEngine.GetIconCacheDir();

            // ── Parent flyout key ───────────────────────────────────────────────
            // SubCommands = "" tells Windows to build the flyout from nested shell subkeys
            using var parent = Registry.ClassesRoot.CreateSubKey(@"Directory\shell\FolderPainterMenu");
            parent.SetValue("MUIVerb", "Paint Folder");
            parent.SetValue("SubCommands", "");
            parent.SetValue("Icon", $"{exePath},0");

            using var shell = parent.CreateSubKey("shell");

            // ── 00: Restore default (remove icon) ───────────────────────────────
            CreateSubCommand(shell, "00_Reset", "Restore Default",
                $"{quoted} \"%1\" --reset");

            // ── Color presets ───────────────────────────────────────────────────────
            int i = 1;
            foreach (var preset in FolderIconEngine.Presets)
            {
                string presetIco = System.IO.Path.Combine(iconDir, preset.Name + ".ico");
                CreateSubCommand(shell,
                    $"{i:D2}_{preset.Name}",
                    preset.Display,
                    $"{quoted} \"%1\" --color {preset.Name}",
                    presetIco);
                i++;
            }

            // ── Last: open full UI ─────────────────────────────────────────────────
            CreateSubCommand(shell, "99_More", "More Options...",
                $"{quoted} \"%1\"", exePath);
        }

        private static void CreateSubCommand(RegistryKey shell, string keyName,
                                             string label, string command, string iconPath = null)
        {
            using var key = shell.CreateSubKey(keyName);
            key.SetValue("MUIVerb", label);
            if (!string.IsNullOrEmpty(iconPath) && System.IO.File.Exists(iconPath))
                key.SetValue("Icon", iconPath);
            using var cmd = key.CreateSubKey("command");
            cmd.SetValue("", command);
        }

        private static void UnregisterContextMenu()
        {
            // Remove new cascading key
            Registry.ClassesRoot.DeleteSubKeyTree(@"Directory\shell\FolderPainterMenu",
                throwOnMissingSubKey: false);
            // Also remove old single-entry key if it existed from a previous install
            Registry.ClassesRoot.DeleteSubKeyTree(@"Directory\shell\FolderPainter",
                throwOnMissingSubKey: false);
        }
    }
}
