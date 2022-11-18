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
                string cmdLine = initCommandLine(Environment.CommandLine);

                var key1 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Hidemaru");
                var key2 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Hidemaru");

                var key = (key1 != null ? key1 : key2); // wow64

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

                ProcessStartInfo ps = new ProcessStartInfo(hmPath, cmdLine);
                ps.UseShellExecute = false;

                Process.Start(ps);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Hidemaru Opener", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        static string initCommandLine(string src)
        {
            try
            {
                int i;
                // 実行可能ファイル本体の部分を除去する
                if (src.Length >= 1 && src[0] == '\"')
                {
                    i = src.IndexOf('\"', 1);
                }
                else
                {
                    i = src.IndexOf(' ');
                }

                if (i == -1)
                {
                    return "";
                }
                else
                {
                    return src.Substring(i + 1).TrimStart(' ');
                }
            }
            catch
            {
                return "";
            }
        }
    }
}
