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
        YymmddAndRandTag5AndBracket = 10,
        BeginEndSectionWithNum = 11,
        BeginEndSectionWithTag = 12,
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
            else if (mode == Mode.YymmddAndRandTag5AndBracket)
            {
                str = "[" + Lib.GenerateRandTagWithYyymmdd(DateTimeOffset.Now, 6) + "]";
                //strAppendTail = " ";
            }
            else if (mode == Mode.RandTag8Only)
            {
                str = Lib.GenerateRandTag(8);
            }
            else if (mode == Mode.BeginEndSectionWithTag)
            {
                string tag1 = "" + GetAndIncrementSeqNo().ToString("D2") + "_" + Lib.GenerateRandTag(8) + DateTime.Now.ToString("yyMMdd");

                str = $"\r\n--- [TAG_{tag1}] ここから ---\r\n\r\n--- [/TAG_{tag1}] ここまで ---\r\n";
            }
            else if (mode == Mode.BeginEndSectionWithNum)
            {
                string tag1 = "" + GetAndIncrementSeqNo();

                str = $"\r\n--- [{tag1}] ここから ---\r\n\r\n--- [{tag1}] ここまで ---\r\n";
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

    /// <summary>
    /// HKCU\Software\dn-text-normalize\ の "seqno" (DWORD) を読み取り(無ければ0)、
    /// +1して書き戻し、+1後の値を返す。
    /// </summary>
    public static int GetAndIncrementSeqNo()
    {
        const string subKeyPath = @"Software\dn-text-normalize";
        string valueName = "seqno_" + DateTime.Now.ToString("yyyyMMdd");

        try
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(subKeyPath))
            {
                if (key == null)
                    return 1; // ここに来ることは通常ほぼ無いが、無理やり進めるなら1

                int current = 0;

                object obj = key.GetValue(valueName, 0);
                if (obj != null)
                {
                    // DWORDは通常 Int32 として返るが、念のため広めに扱う
                    if (obj is int)
                    {
                        current = (int)obj;
                    }
                    else if (obj is byte[])
                    {
                        byte[] b = (byte[])obj;
                        if (b.Length >= 4)
                            current = BitConverter.ToInt32(b, 0);
                    }
                    else
                    {
                        // 文字列などに化けていた場合は安全に0扱い
                        current = 0;
                    }
                }

                int next = unchecked(current + 1);

                // DWORDとして書く
                key.SetValue(valueName, next, RegistryValueKind.DWord);

                return next;
            }
        }
        catch
        {
            // 例外方針が不明なので、失敗時は「更新できなかったが1を返す」などにせず、
            // 呼び出し側で扱えるように投げ直す設計もあり。
            // 要件にないのでここでは「例外を握りつぶさず再スロー」推奨。
            return 1;
        }
    }
}



