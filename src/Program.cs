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

            // Called from right-click context menu with folder path
            if (args.Length > 0 && System.IO.Directory.Exists(args[0]))
            {
                string folderPath = args[0];
                Application.Run(new MainForm(folderPath));
            }
            // No argument: show installer/uninstaller UI
            else
            {
                Application.Run(new SetupForm());
            }
        }
    }
}
