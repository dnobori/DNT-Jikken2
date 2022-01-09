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
                    string dirPath;

                    if (Directory.Exists(filePath))
                    {
                        dirPath = filePath;
                    }
                    else
                    {
                        dirPath = Path.GetDirectoryName(filePath);
                    }

                    ProcessStartInfo ps = new ProcessStartInfo(dirPath);
                    ps.UseShellExecute = true;

                    Process.Start(ps);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }
    }
}
