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
    [STAThread]
    static void Main(string[] args)
    {
        while (true)
        {
            Thread.Sleep(1000);

            Console.WriteLine("send key 1");
            SendKeys.SendWait("^C");

            Console.WriteLine("read");
            string str = Lib.ClipboardRead();
            Console.WriteLine($" str1 = '{str}'");

            str = Lib.NormalizeStrSoftEther(str);

            Console.WriteLine($" str2 = '{str}'");

            Console.WriteLine("write");
            Lib.ClipboardWrite(str);

            Console.WriteLine("send key 2");
            SendKeys.SendWait("^V");
        }
    }
    }



