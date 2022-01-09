using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;

namespace dn_open_containing_folder_util
{
    internal class Program
    {
        static void Main(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    MessageBox.Show("Specify file path.");
                }
                else
                {
                    string filePath = args[0];

                    if (Directory.Exists(filePath))
                    {
                        string dirPath = filePath;
                        ProcessStartInfo ps = new ProcessStartInfo(dirPath);
                        ps.UseShellExecute = true;

                        Process.Start(ps);
                    }
                    else if (File.Exists(filePath))
                    {
                        string explorerPath = Path.Combine(Directory.GetParent(Environment.GetFolderPath(Environment.SpecialFolder.System)).FullName, "explorer.exe");

                        ProcessStartInfo ps = new ProcessStartInfo(explorerPath, $"/select,\"{filePath}\"");
                        ps.UseShellExecute = false;

                        Process.Start(ps);
                    }
                    else
                    {
                        string dirPath = Directory.GetParent(filePath).FullName;
                        ProcessStartInfo ps = new ProcessStartInfo(dirPath);
                        ps.UseShellExecute = true;

                        Process.Start(ps);
                    }

                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }
    }
}
