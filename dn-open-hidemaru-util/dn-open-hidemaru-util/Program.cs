using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;
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
                string filePath = args.Length == 0 ? "" : args[0];

                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Hidemaru"))
                {
                    var value = key.GetValue("InstallLocation");

                    if (value == null || string.IsNullOrEmpty((string)value))
                    {
                        throw new ApplicationException("No hidemaru installed.");
                    }

                    string hmPath = Path.Combine((string)value, "hidemaru.exe");

                    if (File.Exists(hmPath) == false)
                    {
                        throw new ApplicationException("No hidemaru installed.");
                    }

                    ProcessStartInfo ps = new ProcessStartInfo(hmPath, string.IsNullOrEmpty(filePath) ? "" : $"\"{filePath}\"");
                    ps.UseShellExecute = false;

                    Process.Start(ps);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Hidemaru Opener", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }
    }
}
