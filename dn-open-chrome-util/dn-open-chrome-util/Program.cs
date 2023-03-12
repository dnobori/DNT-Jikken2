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

                var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe");

                var value = key.GetValue("");

                if (value == null || string.IsNullOrEmpty((string)value))
                {
                    throw new ApplicationException("No chrome installed.");
                }

                string exePath = (string)value;

                if (File.Exists(exePath) == false)
                {
                    throw new ApplicationException("No chrome installed.");
                }

                var info = new FileInfo(exePath);

                if (info.Length <= 128_000)
                {
                    throw new ApplicationException($"{exePath} file size is too large: {info.Length}");
                }

                ProcessStartInfo ps = new ProcessStartInfo(exePath, cmdLine);
                ps.UseShellExecute = false;

                Process.Start(ps);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "chrome Opener", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
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
