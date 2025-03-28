using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;

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
                Mode mode = Mode.Cmd;

                int index = 0;

                if (args[index].StartsWith("-") || args[index].StartsWith("/"))
                {
                    string modeStr = args[index].Substring(1).ToLowerInvariant();

                    if (modeStr == "w" || modeStr == "wt")
                    {
                        mode = Mode.Wt;
                    }

                    index++;
                }

                string fileOrDirectoryPath = args[index];

                string directoryPath;

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

                string exePath;
                string exeArgs;

                switch (mode)
                {
                    case Mode.Wt:
                        exePath = "wt.exe";
                        exeArgs = "/d \"" + directoryPath + "\"";
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
    }
}
