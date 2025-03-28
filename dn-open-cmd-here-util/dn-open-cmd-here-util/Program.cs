using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Reflection;

namespace dn_open_containing_folder_util
{
    enum Mode
    {
        Cmd = 0,
        Wt,
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

                Mode mode = Mode.Cmd;

                int index = 0;

                if (args.Length > index)
                {
                    if (args[index].StartsWith("-") || args[index].StartsWith("/"))
                    {
                        string modeStr = args[index].Substring(1).ToLowerInvariant();

                        if (modeStr == "w" || modeStr == "wt")
                        {
                            mode = Mode.Wt;
                        }

                        index++;
                    }
                }

                string myExePath = Assembly.GetEntryAssembly().Location;
                string myExeFileName = Path.GetFileNameWithoutExtension(myExePath).ToLowerInvariant();
                if (myExeFileName == "cw" || myExeFileName == "wth" || myExeFileName == "wh")
                {
                    mode = Mode.Wt;
                }

                //MessageBox.Show(args[index]);

                string fileOrDirectoryPath = "";

                if (args.Length > index)
                {
                    fileOrDirectoryPath = args[index];
                }

                string directoryPath = "";

                if (fileOrDirectoryPath != "")
                {
                    if (File.Exists(fileOrDirectoryPath))
                    {
                        directoryPath = Path.GetDirectoryName(fileOrDirectoryPath);
                    }
                    else if (Directory.Exists(fileOrDirectoryPath))
                    {
                        directoryPath = fileOrDirectoryPath;
                    }
                    else
                    {
                        int tryCount = 0;
                        string tmpDirName = fileOrDirectoryPath;

                        while (true)
                        {
                            if (Directory.Exists(tmpDirName))
                            {
                                directoryPath = tmpDirName;
                                break;
                            }
                            else
                            {
                                tryCount++;
                                tmpDirName = Path.GetDirectoryName(tmpDirName);

                                if (tryCount >= 10)
                                {
                                    throw new ApplicationException($"Path '{fileOrDirectoryPath}' not found.");
                                }
                            }
                        }
                    }
                }

                string exePath;
                string exeArgs;

                if (directoryPath.Length >= 4)
                {
                    if (directoryPath[directoryPath.Length - 1] == '\\')
                    {
                        directoryPath = directoryPath.Substring(0, directoryPath.Length - 1);
                    }
                }

                if (directoryPath == "")
                {
                    string tmp = @"c:\tmp";
                    if (Directory.Exists(tmp)) directoryPath = tmp;
                }

                if (directoryPath == "")
                {
                    directoryPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                }

                switch (mode)
                {
                    case Mode.Wt:
                        exePath = "wt.exe";
                        if (directoryPath.Contains(" "))
                        {
                            exeArgs = "/d \"" + directoryPath + "\"";
                        }
                        else
                        {
                            exeArgs = "/d " + directoryPath;
                        }
                        break;

                    default:
                        exePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
                        exeArgs = "";
                        break;
                }

                ProcessStartInfo ps = new ProcessStartInfo(exePath);
                ps.WorkingDirectory = directoryPath;
                ps.UseShellExecute = false;
                ps.Arguments = exeArgs;

                Process.Start(ps);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
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
