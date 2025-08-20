using System;
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
        CutNormalizePaste = 0,
        NormalizePaste = 1,
        NormalizeOnly = 2,
        YymmddPaste = 3,
        CutNormalizeOnly = 4,
        YymmddOnly = 5,
        YyyymmddOnly = 6,
        YyyymmddExOnly = 7,
        YymmddAndRandTag5Only = 8,
        RandTag8Only = 9,
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
            bool doNothing = false;
            string str;
            string strAppendTail = "";

            if (mode == Mode.YymmddPaste || mode == Mode.YymmddOnly)
            {
                str = DateTime.Now.ToString("yyMMdd");
                strAppendTail = " ";
            }
            else if (mode == Mode.YyyymmddOnly)
            {
                str = DateTime.Now.ToString("yyyyMMdd");
                strAppendTail = " ";
            }
            else if (mode == Mode.YyyymmddExOnly)
            {
                str = DateTime.Now.ToString("yyyy") + "/" + DateTime.Now.ToString("MM") + "/" + DateTime.Now.ToString("dd");
                strAppendTail = " ";
            }
            else if (mode == Mode.YymmddAndRandTag5Only)
            {
                str = Lib.GenerateRandTagWithYyymmdd(DateTimeOffset.Now, 6);
            }
            else if (mode == Mode.RandTag8Only)
            {
                str = Lib.GenerateRandTag(8);
            }
            else
            {
                if (mode == Mode.CutNormalizePaste || mode == Mode.CutNormalizeOnly)
                {
                    //Console.WriteLine("send key 1");
                    SendKeys.SendWait("^x");
                    SendKeys.Flush();

                    Thread.Sleep(100);
                }

                //Console.WriteLine("read");
                //Console.WriteLine($" str1 = '{str}'");

                str = Lib.ClipboardRead(out doNothing);
            }

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

                str = Lib.NormalizeComfortableUrl(str);

                str = Lib.NormalizeFileOrDirPath(str);
            }

            str += strAppendTail;

            //Console.WriteLine("write");
            Lib.ClipboardWrite(str);

            //Thread.Sleep(100);

            //Console.WriteLine("send key 2");
            if (mode == Mode.NormalizePaste || mode == Mode.CutNormalizePaste || mode == Mode.YymmddPaste)
            {
                SendKeys.SendWait("^v");
                SendKeys.Flush();
            }
        }
        catch { }
    }
}



