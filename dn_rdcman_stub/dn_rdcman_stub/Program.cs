using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using System.Windows.Forms;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Reflection;
using System.Text.RegularExpressions;

internal class Program
{
    public const string AppTitle = "RDCMan Stub ツール";

    public static void Main(string[] args)
    {
        try
        {
            bool editMode = false;

            if (args.Length < 1)
            {
                MessageBox.Show("引数を指定してください。\r\n\r\n第一引数: rdg ファイル名\r\n第二引数 (オプション): [/edit]", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (args.Length >= 2)
            {
                if (args[1].Equals("/edit", StringComparison.InvariantCultureIgnoreCase))
                {
                    editMode = true;
                }
            }

            DateTime now = DateTime.Now;
            DateTime oldNow = now.AddMonths(-2);

            string myExePath = GetAppRealProcessExeFileNameInternal();
            string myExeDir = Path.GetDirectoryName(myExePath);
            string rdcManRealExePath = Path.Combine(myExeDir, "RDCManReal", "RDCMan.exe");

            if (File.Exists(rdcManRealExePath) == false)
            {
                throw new Exception($"File \"{rdcManRealExePath}\" not found.");
            }

            string rdgPath = args[0];

            if (rdgPath.IndexOf(".rdgcopy", StringComparison.InvariantCultureIgnoreCase) != -1)
            {
                throw new Exception(".rdgcopy ディレクトリ内のファイルは、指定できません。");
            }

            if (editMode == false)
            {
                // 指定された rdg ファイルと同一のディレクトリにコピーディレクトリを作成
                string rdgDir = Path.GetDirectoryName(rdgPath);

                string copyDir = Path.Combine(rdgDir, ".rdgcopy");

                if (Directory.Exists(copyDir) == false)
                {
                    Directory.CreateDirectory(copyDir);
                }

                // ファイル名生成
                string copyFileName = ".copy." + Path.GetFileNameWithoutExtension(rdgPath) + "." + DateTimeToYymmddStr(now, yearTwoDigits: true) + "_" + DateTimeToHhmmssStr(now, millisecs: true) + Path.GetExtension(rdgPath);
                string copyFilePath = Path.Combine(copyDir, copyFileName);

                // コピー
                File.Copy(rdgPath, copyFilePath, true);
                File.SetLastWriteTime(copyFilePath, now);

                // 実行
                Run(rdcManRealExePath, "\"" + copyFilePath + "\"");

                // GC
                DirectoryInfo di = new DirectoryInfo(copyDir);

                string ext = Path.GetExtension(rdgPath);

                if (string.IsNullOrEmpty(ext) == false)
                {
                    foreach (var file in di.GetFileSystemInfos())
                    {
                        if (file.Attributes.HasFlag(FileAttributes.Directory) == false)
                        {
                            if (file.Name.StartsWith(".copy.", StringComparison.InvariantCultureIgnoreCase) && file.Name.EndsWith(ext))
                            {
                                if (file.LastWriteTime <= oldNow)
                                {
                                    try
                                    {
                                        File.Delete(file.FullName);
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                // 実行
                Run(rdcManRealExePath, "\"" + rdgPath + "\"");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    static string GetAppRealProcessExeFileNameInternal()
    {
        try
        {
            Process myProcess = Process.GetCurrentProcess();

            Process myProcess2 = Process.GetProcessById(myProcess.Id);

            return myProcess2.MainModule.FileName;
        }
        catch
        {
            throw new SystemException("GetAppRealProcessExeFileNameInternal: Failed to obtain the path.");
        }
    }

    // プログラムを起動する
    public static Process Run(string exeName, string args, bool shellExecute = false)
    {
        Process p = new Process();
        p.StartInfo.FileName = exeName;
        p.StartInfo.Arguments = args;

        if (shellExecute)
        {
            p.StartInfo.UseShellExecute = true;
        }

        p.Start();

        return p;
    }

    public static string DateTimeToHhmmssStr(DateTime dt, string zeroValue = "", bool millisecs = false)
    {
        string ret = dt.ToString("HHmmss");

        if (millisecs)
        {
            long ticks = dt.Ticks % 10000000;
            if (ticks >= 9990000)
            {
                ticks = 9990000;
            }

            string msecStr = ((decimal)ticks / (decimal)10000000).ToString(".000");

            ret += msecStr;
        }

        return ret;
    }
    public static string DateTimeToYymmddStr(DateTime dt, string zeroValue = "", bool yearTwoDigits = false)
    {
        string ret = dt.ToString("yyyyMMdd");

        if (yearTwoDigits)
        {
            ret = ret.Substring(2);
        }

        return ret;
    }

}

