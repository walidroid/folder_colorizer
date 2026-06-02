using System;
using System.Windows.Forms;

namespace FolderPainter
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // ── Headless modes (called from right-click submenu) ──────────────
            // Format: FolderPainter.exe "folder path" --color <Name>
            //         FolderPainter.exe "folder path" --reset
            if (args.Length >= 2 && System.IO.Directory.Exists(args[0]))
            {
                string folderPath = args[0];
                string cmd        = args[1].ToLowerInvariant();

                if (cmd == "--reset")
                {
                    try
                    {
                        FolderIconEngine.Remove(folderPath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error removing icon:\n" + ex.Message,
                            "Folder Painter", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    return;   // exit without showing a window
                }

                if (cmd == "--color" && args.Length >= 3)
                {
                    try
                    {
                        FolderIconEngine.ApplyByName(folderPath, args[2]);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error applying color:\n" + ex.Message,
                            "Folder Painter", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    return;   // exit without showing a window
                }
            }

            // ── Full UI mode ──────────────────────────────────────────────────
            // Called from right-click "More Options..." or standalone
            if (args.Length >= 1 && System.IO.Directory.Exists(args[0]))
            {
                Application.Run(new MainForm(args[0]));
            }
            else
            {
                // No argument: show the setup/install form
                Application.Run(new SetupForm());
            }
        }
    }
}
