using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Text;
using Microsoft.Win32;
using System.Windows.Forms;
using System.Diagnostics;

internal class Program
{
    [Flags]
    public enum Mode
    {
        CutAndPaste = 0,
        Paste = 1,
        NormalizeOnly = 2,
    }

    [STAThread]
    static void Main(string[] args)
    {
        int i = 0;
        int.TryParse(args.ElementAtOrDefault(0), out i);

        Mode mode = (Mode)i;

        //MessageBox.Show(args.ElementAtOrDefault(0));

        try
        {
            if (mode == Mode.CutAndPaste)
            {
                //Console.WriteLine("send key 1");
                SendKeys.SendWait("^x");

                Thread.Sleep(100);
            }

            //Console.WriteLine("read");
            string str = Lib.ClipboardRead();
            //Console.WriteLine($" str1 = '{str}'");

            str = Lib.NormalizeStrSoftEther(str);

            //Console.WriteLine($" str2 = '{str}'");

            //Console.WriteLine("write");
            Lib.ClipboardWrite(str);

            //Thread.Sleep(100);

            //Console.WriteLine("send key 2");
            if (mode != Mode.NormalizeOnly)
            {
                SendKeys.SendWait("^v");
            }
        }
        catch { }
    }
}



