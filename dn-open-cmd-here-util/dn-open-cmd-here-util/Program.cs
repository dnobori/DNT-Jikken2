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
    enum Mode1
    {
        Cmd = 0,
        Wt,
    }

    enum Mode2
    {
        Normal = 0,
        Git,
        GitBash,
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            try
            {
                bool parseArgs = true;

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
                            parseArgs = false;
                        }
                    }
                }

                Mode1 mode = Mode1.Cmd;
                Mode2 mode2 = Mode2.Normal;

                int argsIndex = 0;

                if (parseArgs)
                {
                    while (args.Length > argsIndex)
                    {
                        if (args[argsIndex].StartsWith("-") || args[argsIndex].StartsWith("/"))
                        {
                            string modeStr = args[argsIndex].Substring(1).ToLowerInvariant();

                            if (modeStr == "w" || modeStr == "wt")
                            {
                                mode = Mode1.Wt;
                            }
                            else if (modeStr == "git")
                            {
                                mode2 = Mode2.Git;
                            }
                            else if (modeStr == "gitbash")
                            {
                                mode2 = Mode2.GitBash;
                            }

                            argsIndex++;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                string myExePath = Assembly.GetEntryAssembly().Location;
                string myExeDirPath = Path.GetDirectoryName(myExePath);
                string myExeSimpleFileNameLower = Path.GetFileNameWithoutExtension(myExePath).ToLowerInvariant();
                if (myExeSimpleFileNameLower == "cw" || myExeSimpleFileNameLower == "wth" || myExeSimpleFileNameLower == "wh")
                {
                    mode = Mode1.Wt;
                }

                //MessageBox.Show(args[index]);

                string fileOrDirectoryPath = "";

                if (args.Length > argsIndex)
                {
                    fileOrDirectoryPath = args[argsIndex];
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

                string myHomeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);

                bool specialCurrentDirectory = true;
                string cdPath = Environment.CurrentDirectory;
                if (string.Equals(cdPath, myHomeDir, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(cdPath, systemDir, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(cdPath, myExeDirPath, StringComparison.OrdinalIgnoreCase))
                {
                    specialCurrentDirectory = false;
                }
                if (string.IsNullOrEmpty(cdPath) || Directory.Exists(cdPath) == false)
                {
                    specialCurrentDirectory = false;
                }

                if (specialCurrentDirectory == false)
                {
                    if (directoryPath == "")
                    {
                        string tmp = @"c:\tmp";
                        if (Directory.Exists(tmp)) directoryPath = tmp;
                    }

                    if (directoryPath == "")
                    {
                        directoryPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    }
                }
                else
                {
                    if (directoryPath == "")
                    {
                        directoryPath = cdPath;
                    }
                }

                switch (mode)
                {
                    case Mode1.Wt:
                        exePath = "wt.exe";
                        if (directoryPath.Contains(" "))
                        {
                            exeArgs = "/d \"" + directoryPath + "\"";
                        }
                        else
                        {
                            exeArgs = "/d " + directoryPath;
                        }

                        if (mode2 == Mode2.Git)
                        {
                            exeArgs += " cmd /c \"C:\\Program Files\\Git\\git-cmd.exe\"";
                        }
                        else if (mode2 == Mode2.GitBash)
                        {
                            exeArgs += " cmd /c \"C:\\Program Files\\Git\\bin\\bash.exe\"";
                        }

                        break;

                    default:
                        exePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
                        exeArgs = "";

                        if (mode2 == Mode2.Git)
                        {
                            exeArgs = "/c \"C:\\Program Files\\Git\\git-cmd.exe\"";
                        }
                        else if (mode2 == Mode2.GitBash)
                        {
                            exeArgs = "/c \"C:\\Program Files\\Git\\bin\\bash.exe\"";
                        }
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
