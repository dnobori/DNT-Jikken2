using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using System.Windows.Forms;
using System.Diagnostics;


internal class Program
{
    public const string AppTitle = "秀丸プロセス終了ツール";

    public static void Main(string[] args)
    {
        KillMain();
    }

    static void KillMain()
    {
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

        var processList = Process.GetProcesses();

        List<Process> processListToKill = new List<Process>();

        int currentSessionId = Process.GetCurrentProcess().SessionId;

        foreach (var p in processList)
        {
            try
            {
                if (p.SessionId == currentSessionId)
                {
                    string exe = p.MainModule.FileName;

                    if (string.Equals(exe, hmPath, StringComparison.OrdinalIgnoreCase))
                    {
                        processListToKill.Add(p);
                    }
                }
            }
            catch { }
        }

        if (processListToKill.Count == 0)
        {
            MessageBox.Show($"終了すべき秀丸プロセスは 1 個もありませんでした。", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (MessageBox.Show($"{processListToKill.Count} 個の秀丸プロセスを終了します。\r\n\r\n未保存の変更点がある場合、データが消失する可能性があります。\r\nよろしいですか?", AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }

        StringWriter err = new StringWriter();

        int numKilled = 0;

        foreach (var p in processListToKill)
        {
            try
            {
                p.Kill();
                numKilled++;
            }
            catch (Exception ex)
            {
                err.WriteLine($"--- プロセス {p.Id} ---");
                err.WriteLine(ex.ToString());
                err.WriteLine();
            }
        }

        string errStr = err.ToString();
        if (errStr.Length >= 1)
        {
            MessageBox.Show(errStr, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        }

        if (numKilled >= 1)
        {
            MessageBox.Show($"{numKilled} 個の秀丸プロセスを終了しました。", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            MessageBox.Show($"終了した秀丸プロセスはありませんでした。", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

    }
}
