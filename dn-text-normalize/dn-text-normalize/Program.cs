﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Text;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Win32;
using System.Windows.Forms;
using System.Diagnostics;

#pragma warning disable CS0162 // 到達できないコードが検出されました

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
        if (false) // Test mode
        {
            StringWriter testin = new StringWriter();
            testin.WriteLine("   こんにちは");
            testin.WriteLine("   　こんにちは2");
            testin.WriteLine("   　abcこんにちは2");
            testin.WriteLine("　こんにちは2");
            testin.WriteLine("　　abcこんにちは2");

            string test1 = Lib.NormalizeStrSoftEther(testin.ToString());

            Console.WriteLine(test1);

            return;
        }

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
                SendKeys.Flush();

                Thread.Sleep(100);
            }

            //Console.WriteLine("read");
            //Console.WriteLine($" str1 = '{str}'");

            bool doNothing = false;

            string str = Lib.ClipboardRead(out doNothing);

            if (str.StartsWith("Dropbox", StringComparison.OrdinalIgnoreCase) ||
                str.StartsWith("GoogleDrive", StringComparison.OrdinalIgnoreCase))
            {
                doNothing = true;
            }

            if (doNothing == false)
            {
                string strTrimed = str.Trim();

                bool isPath = false;
                bool isIpAddress = false;

                string ipTmp = Lib.ZenkakuToHankaku(strTrimed);

                ipTmp = ipTmp.Replace("．", ".");

                IPAddress ip;
                if ((ipTmp.IndexOf(".") != -1 || ipTmp.IndexOf(":") != -1) && IPAddress.TryParse(ipTmp, out ip))
                {
                    isIpAddress = true;
                    ipTmp = ip.ToString();
                }

                if (strTrimed.StartsWith("\"") && strTrimed.EndsWith("\"") && strTrimed.Length >= 3)
                {
                    strTrimed = strTrimed.Substring(1, strTrimed.Length - 2);
                }

                if (strTrimed.Length >= 3 && ((strTrimed[0] >= 'a' && strTrimed[0] <= 'z') || (strTrimed[0] >= 'A' && strTrimed[0] <= 'Z')) && strTrimed[1] == ':' && (strTrimed[2] == '\\' || strTrimed[2] == '/'))
                {
                    isPath = true;
                }
                else if (strTrimed.StartsWith(@"\\") && strTrimed.Length >= 3)
                {
                    isPath = true;
                }

                if (isPath)
                {
                    str = str.Trim();

                    if (str.StartsWith("\"") && str.EndsWith("\"") && str.Length >= 3)
                    {
                        str = str.Substring(1, str.Length - 2);
                    }
                }
                else if (isIpAddress)
                {
                    str = ipTmp;
                }
                else
                {
                    str = Lib.NormalizeStrSoftEther(str);
                }

                str = Lib.NormalizeFileOrDirPath(str);
            }

            //Console.WriteLine("write");
            Lib.ClipboardWrite(str);

            //Thread.Sleep(100);

            //Console.WriteLine("send key 2");
            if (mode != Mode.NormalizeOnly)
            {
                SendKeys.SendWait("^v");
                SendKeys.Flush();
            }
        }
        catch { }
    }
}



