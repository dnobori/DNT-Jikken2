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
    enum Mode
    {
        Normal = 0,
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            try
            {
                string fullCmdLine = initCommandLine(Environment.CommandLine);
                if (fullCmdLine != "" && fullCmdLine.StartsWith("/") == false && fullCmdLine.StartsWith("\\") == false)
                {
                    if (Directory.Exists(fullCmdLine) || File.Exists(fullCmdLine))
                    {
                        if (fullCmdLine.StartsWith("\"") && fullCmdLine.EndsWith("\"") && fullCmdLine.Length >= 3)
                        {
                            fullCmdLine = fullCmdLine.Substring(1, fullCmdLine.Length - 2);
                        }

                        if (fullCmdLine != "")
                        {
                            args = new string[] { fullCmdLine };
                        }
                    }
                }

                Mode mode = Mode.Normal;

                int index = 0;

                if (args.Length > index)
                {
                    if (args[index].StartsWith("-") || args[index].StartsWith("/"))
                    {
                        string modeStr = args[index].Substring(1).ToLowerInvariant();

                        index++;
                    }
                }

                string tmpPath = "";

                if (args.Length > index)
                {
                    tmpPath = args[index];
                }

                if (tmpPath.Length >= 2)
                {
                    if (tmpPath[tmpPath.Length - 1] == '\\')
                    {
                        tmpPath = tmpPath.Substring(0, tmpPath.Length - 1);
                    }
                }


                string directoryOrFilePath = "";

                bool isFilePath = false;

                if (tmpPath != "")
                {
                    if (File.Exists(tmpPath))
                    {
                        directoryOrFilePath = tmpPath;
                        isFilePath = true;
                    }
                    else if (Directory.Exists(tmpPath))
                    {
                        directoryOrFilePath = tmpPath;
                    }
                    else
                    {
                        int tryCount = 0;
                        string tmpDirName = tmpPath;

                        while (true)
                        {
                            if (Directory.Exists(tmpDirName))
                            {
                                directoryOrFilePath = tmpDirName;
                                break;
                            }
                            else
                            {
                                tryCount++;
                                tmpDirName = Path.GetDirectoryName(tmpDirName);

                                if (tryCount >= 10)
                                {
                                    throw new ApplicationException($"Path '{tmpPath}' not found.");
                                }
                            }
                        }
                    }
                }

                if (directoryOrFilePath.Length >= 2)
                {
                    if (directoryOrFilePath[directoryOrFilePath.Length - 1] == '\\')
                    {
                        directoryOrFilePath = directoryOrFilePath.Substring(0, directoryOrFilePath.Length - 1);
                    }
                }

                string cmdline = "";

                if (directoryOrFilePath != "")
                {
                    cmdline = "\"" + directoryOrFilePath + "\"";

                    if (isFilePath)
                    {
                        cmdline = "/select," + cmdline;
                    }
                }

                var key1 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\HmFilerClassic");
                var key2 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\HmFilerClassic");

                var key = (key1 != null ? key1 : key2); // wow64

                var value = key.GetValue("InstallLocation");

                if (value == null || string.IsNullOrEmpty((string)value))
                {
                    throw new ApplicationException("No hmfilter installed.");
                }

                string hfExePath = Path.Combine((string)value, "HmFilerClassic.exe");

                if (File.Exists(hfExePath) == false)
                {
                    throw new ApplicationException("No HmFilerClassic.exe installed.");
                }

                ProcessStartInfo ps = new ProcessStartInfo(hfExePath, cmdline);
                ps.UseShellExecute = false;

                Process.Start(ps);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "HmFilerClassic Opener", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
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
