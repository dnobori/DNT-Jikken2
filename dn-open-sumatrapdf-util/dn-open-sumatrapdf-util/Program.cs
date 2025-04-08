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


                string hmPath = @"C:\git\dndevtools\SumatraPDF\SumatraPDF.exe";

                if (File.Exists(hmPath) == false)
                {
                    throw new ApplicationException("No SumatraPDF installed.");
                }

                ProcessStartInfo ps = new ProcessStartInfo(hmPath, cmdLine);
                ps.UseShellExecute = false;

                Process.Start(ps);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "SumatraPDF Opener", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
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
