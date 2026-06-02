using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FolderPainter
{
    /// <summary>
    /// Shown when the app is launched without a folder argument.
    /// Lets the user register or unregister the right-click context menu entry.
    /// </summary>
    public class SetupForm : Form
    {
        private Label  _statusLabel;
        private bool   _registered;

        public SetupForm()
        {
            BuildUI();
            RefreshStatus();
        }

        private void BuildUI()
        {
            Text          = "Folder Painter – Setup";
            Size          = new Size(420, 300);
            MinimumSize   = Size;
            MaximumSize   = Size;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor     = Color.FromArgb(30, 30, 30);
            ForeColor     = Color.White;
            Font          = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = SystemIcons.Application;

            // Title
            var title = new Label
            {
                Text     = "🎨  Folder Painter",
                Font     = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = true,
                Location  = new Point(20, 18)
            };
            Controls.Add(title);

            var sub = new Label
            {
                Text      = "Add \"Paint Folder\" to your right-click menu",
                Font      = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(150, 150, 150),
                AutoSize  = true,
                Location  = new Point(20, 46)
            };
            Controls.Add(sub);

            var sep = new Panel { BackColor = Color.FromArgb(60,60,60), Location = new Point(20,68), Size = new Size(370,1) };
            Controls.Add(sep);

            _statusLabel = new Label
            {
                Text      = "",
                Font      = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(200, 200, 200),
                AutoSize  = false,
                Size      = new Size(370, 60),
                Location  = new Point(20, 82)
            };
            Controls.Add(_statusLabel);

            // Register button
            var regBtn = new FlatButton("Register (Add to right-click menu)")
            {
                Location   = new Point(20, 155),
                Size       = new Size(370, 34),
                AccentColor = Color.FromArgb(0, 130, 100)
            };
            regBtn.Click += OnRegisterClick;
            Controls.Add(regBtn);

            // Unregister button
            var unregBtn = new FlatButton("Unregister (Remove from right-click menu)")
            {
                Location   = new Point(20, 200),
                Size       = new Size(370, 34),
                AccentColor = Color.FromArgb(120, 40, 40)
            };
            unregBtn.Click += OnUnregisterClick;
            Controls.Add(unregBtn);

            var note = new Label
            {
                Text      = "⚠  Run as Administrator if registration fails.",
                Font      = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(180, 150, 60),
                AutoSize  = true,
                Location  = new Point(20, 246)
            };
            Controls.Add(note);
        }

        private void RefreshStatus()
        {
            try
            {
                using var key = Registry.ClassesRoot.OpenSubKey(@"Directory\shell\FolderPainter");
                _registered = (key != null);
                if (_registered)
                {
                    _statusLabel.ForeColor = Color.FromArgb(80, 200, 120);
                    _statusLabel.Text = "✔  Context menu is REGISTERED.\n" +
                                        "Right-click any folder → \"🎨 Paint Folder\" to open.";
                }
                else
                {
                    _statusLabel.ForeColor = Color.FromArgb(200, 100, 80);
                    _statusLabel.Text = "✘  Context menu is NOT registered.\n" +
                                        "Click \"Register\" below to add it.";
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
                MessageBox.Show("Registered successfully!\n\nRight-click any folder and choose \"🎨 Paint Folder\".",
                    "Folder Painter", MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshStatus();
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("Access denied.\n\nPlease run FolderPainter.exe as Administrator to register the context menu.",
                    "Folder Painter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Folder Painter", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                MessageBox.Show("Access denied.\n\nPlease run FolderPainter.exe as Administrator.",
                    "Folder Painter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Folder Painter", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Registry helpers ─────────────────────────────────────────────
        private static void RegisterContextMenu()
        {
            // Path to this EXE
            string exePath = Process.GetCurrentProcess().MainModule.FileName;

            // HKCR\Directory\shell\FolderPainter
            using var shellKey = Registry.ClassesRoot.CreateSubKey(@"Directory\shell\FolderPainter");
            shellKey.SetValue("", "🎨 Paint Folder");
            shellKey.SetValue("Icon", $"\"{exePath}\",0");

            // HKCR\Directory\shell\FolderPainter\command
            using var cmdKey = shellKey.CreateSubKey("command");
            cmdKey.SetValue("", $"\"{exePath}\" \"%1\"");
        }

        private static void UnregisterContextMenu()
        {
            Registry.ClassesRoot.DeleteSubKeyTree(@"Directory\shell\FolderPainter", throwOnMissingSubKey: false);
        }
    }

    // tiny shim so SetupForm.cs compiles without System.Diagnostics explicitly
    using Process = System.Diagnostics.Process;
}
