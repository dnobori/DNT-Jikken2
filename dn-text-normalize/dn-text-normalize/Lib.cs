using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using Microsoft.Win32;
using System.Windows.Forms;
using System.Diagnostics;


public static class RandGen
{
    static RandomNumberGenerator gen = RandomNumberGenerator.Create();

    public static int GenRandSInt31()
    {
        byte[] bytes = new byte[4];
        gen.GetBytes(bytes);
        int value = BitConverter.ToInt32(bytes, 0);

        // 31bit の範囲に収める (最上位ビットをマスクする)
        return value & 0x7FFFFFFF;
    }
}

public static class Lib
{
    public static void ClipboardWrite(string str)
    {
        str = str._NonNull();

        Clipboard.SetDataObject(str, true);
    }

    public static string ClipboardRead(out bool doNothing)
    {
        doNothing = false;

        var data = Clipboard.GetDataObject();

        if (data != null && data.GetDataPresent(DataFormats.Text) || data.GetDataPresent(DataFormats.UnicodeText))
        {
            string str = (string)data.GetData(DataFormats.UnicodeText);

            return str._NonNull();
        }
        else if (Clipboard.ContainsFileDropList())
        {
            var fileList = Clipboard.GetFileDropList();

            StringBuilder sb = new StringBuilder();

            foreach (var file in fileList)
            {
                string file2 = NormalizeFileOrDirPath(file);

                sb.Append(file2);

                if (fileList.Count >= 2)
                {
                    sb.AppendLine();
                }
            }

            doNothing = true;

            return sb.ToString();
        }

        return "";
    }

    public static string NormalizeFileOrDirPath(string str)
    {
        bool backSlashToSlash = false;

        bool isDir = false;

        if (str.StartsWith(@"C:\Dropbox\", StringComparison.OrdinalIgnoreCase))
        {
            isDir = IsDir(str);
            str = str.Substring(3);
            backSlashToSlash = true;
        }
        else if (str.StartsWith(@"C:\GoogleDrive\", StringComparison.OrdinalIgnoreCase))
        {
            isDir = IsDir(str);
            str = "GoogleDrive/" + str.Substring(15);
            backSlashToSlash = true;
        }
        else if (str.StartsWith(@"G:\共有ドライブ\", StringComparison.OrdinalIgnoreCase))
        {
            isDir = IsDir(str);
            str = "GoogleDrive/" + str.Substring(3);
            backSlashToSlash = true;
        }
        else if (str.StartsWith(@"M:\", StringComparison.OrdinalIgnoreCase))
        {
            isDir = IsDir(str);
            str = @"\\labfs.lab.coe.ad.jp\SHARE\" + str.Substring(3);
        }

        if (isDir)
        {
            char last = str.LastOrDefault();
            if (last == '\\' || last == '/') { }
            else
            {
                str = str + @"\";
            }
        }

        if (backSlashToSlash)
        {
            str = str.Replace(@"\", "/");
        }

        return str;
    }

    static bool IsDir(string path)
    {
        try
        {
            return Directory.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    public static string _NonNull(this string s) { if (s == null) return ""; else return s; }


    // 文字列をソフトイーサ表記規則に正規化する
    public static string NormalizeStrSoftEther(string str, bool trim = false)
    {
        bool b = false;
        str = str._NonNull();

        str = UnicodeControlCodesNormalizeUtil.Normalize(str);

        StringReader sr = new StringReader(str);
        StringWriter sw = new StringWriter();
        while (true)
        {
            string line = sr.ReadLine();
            if (line == null)
            {
                break;
            }
            if (b)
            {
                sw.WriteLine();
            }
            b = true;
            line = normalizeStrSoftEtherInternal(line);
            sw.Write(line);
        }

        int len = str.Length;

        try
        {
            if (str[len - 1] == '\n' || str[len - 1] == '\r')
            {
                sw.WriteLine();
            }
        }
        catch
        {
        }

        str = sw.ToString();

        str = UnicodeStdKangxiMapUtil.StrangeToNormal(str);

        if (trim)
        {
            str = str.Trim();
        }

        return str;
    }
    static string normalizeStrSoftEtherInternal(string str)
    {
        int i;
        if (str.Trim().Length == 0)
        {
            return "";
        }

        // 数個の半角スペースが続いた後に全角スペース 1 個が登場し、その後に半角スペース・全角スペースのいずれでも
        // ない文字が登場する場合は、それぞれのスペースの個数を記憶したのちに、それらのスペースを削除する
        int prefixHankakuSpecialMode = 0;
        int prefixHankakuSpaceNum = 0;
        string prefixSpaceBodyText = "";
        for (i = 0; i < str.Length; i++)
        {
            char c = str[i];
            switch (prefixHankakuSpecialMode)
            {
                case 0:
                    if (c == ' ')
                    {
                        prefixHankakuSpaceNum++;
                        prefixHankakuSpecialMode = 1;
                    }
                    else if (c == '　')
                    {
                        prefixHankakuSpecialMode = 2;
                    }
                    else
                    {
                        prefixHankakuSpecialMode = 99;
                    }
                    break;

                case 1:
                    if (c == ' ')
                    {
                        prefixHankakuSpaceNum++;
                    }
                    else if (c == '　')
                    {
                        prefixHankakuSpecialMode = 2;
                    }
                    else
                    {
                        prefixHankakuSpecialMode = 99;
                    }
                    break;

                case 2:
                    if (c == ' ' || c == '　')
                    {
                        prefixHankakuSpecialMode = 99;
                    }
                    else
                    {
                        prefixHankakuSpecialMode = 3;
                        prefixSpaceBodyText = str.Substring(i);
                    }
                    break;
            }
        }

        if (prefixHankakuSpecialMode == 3)
        {
            str = prefixSpaceBodyText;
        }

        StringBuilder sb1 = new StringBuilder();
        for (i = 0; i < str.Length; i++)
        {
            char c = str[i];

            if (c == ' ' || c == '　' || c == '\t')
            {
                sb1.Append(c);
            }
            else
            {
                break;
            }
        }
        string str2 = str.Substring(i).Trim();

        string str1 = sb1.ToString();

        str1 = ReplaceStr(str1, "　", "  ");
        str1 = ReplaceStr(str1, "\t", "    ");

        string ret = (str1 + normalizeStrSoftEtherInternal2(str2));

        if (prefixHankakuSpecialMode == 3)
        {
            StringBuilder sb2 = new StringBuilder();
            for (i = 0; i < prefixHankakuSpaceNum; i++)
            {
                sb2.Append(' ');
            }
            sb2.Append('　');
            sb2.Append(ret);

            ret = sb2.ToString();
        }

        return ret;
    }
    static string normalizeStrSoftEtherInternal2(string str)
    {
        NormalizeString(ref str, true, true, false, true);
        char[] chars = str.ToCharArray();
        StringBuilder sb = new StringBuilder();

        int i;
        for (i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            bool insert_space = false;
            bool insert_space2 = false;

            char c1 = (char)0;
            if (i >= 1)
            {
                c1 = chars[i - 1];
            }

            char c2 = (char)0;
            if (i < (chars.Length - 1))
            {
                c2 = chars[i + 1];
            }

            if (c == '\'' || c1 == '\'' || c2 == '\'' || c == '\"' || c1 == '\"' || c2 == '\"' || c == '>' || c1 == '>' || c2 == '>' || c == '<' || c1 == '<' || c2 == '<')
            {
            }
            else if (c == '(' || c == '[' || c == '{' || c == '<')
            {
                // 括弧開始
                if (c1 != '「' && c1 != '『' && c1 != '。' && c1 != '、' && c1 != '・')
                {
                    insert_space = true;
                }
            }
            else if (c == ')' || c == ']' || c == '}' || c == '>')
            {
                // 括弧終了
                if (c2 != '.' && c2 != ',' && c2 != '。' && c2 != '、')
                {
                    insert_space2 = true;
                }
            }
            else if (c == '～')
            {
                if (c1 != '～')
                {
                    insert_space = true;
                }

                if (c2 != '～')
                {
                    insert_space2 = true;
                }
            }
            else if (IsZenkaku(c) == false)
            {
                // 半角
                if (IsZenkaku(c1))
                {
                    // 前の文字が全角
                    if (c != '.' && c != ',' && c != ';' && c != ':' && c1 != '※' && c1 != '〒' && c1 != '℡' && c1 != '「' && c1 != '『' && c1 != '。' && c1 != '、' && c1 != '・')
                    {
                        insert_space = true;
                    }
                }
            }
            else
            {
                // 全角
                if (IsZenkaku(c1) == false)
                {
                    // 前の文字が半角
                    if (c != '。' && c != '、' && c != '」' && c != '』' && c != '・' && c1 != '(' && c1 != '[' && c1 != '{' && c1 != '<' && c1 != ';' && c1 != ':')
                    {
                        insert_space = true;
                    }
                }
            }

            if (insert_space)
            {
                sb.Append(' ');
            }

            sb.Append(c);

            if (insert_space2)
            {
                sb.Append(' ');
            }
        }

        str = sb.ToString();

        NormalizeString(ref str, true, true, false, true);

        return str;
    }


    // 指定した文字が全角かどうか調べる
    public static bool IsZenkaku(char c)
    {
        return !((c >= (char)0) && (c <= (char)256));
    }

    // 文字列の置換
    public static string ReplaceStr(string str, string oldKeyword, string newKeyword, bool caseSensitive = false)
    {
        int len_string, len_old, len_new;
        if (str == null || oldKeyword == null || newKeyword == null)
        {
            return null;
        }

        if (caseSensitive)
        {
            return str.Replace(oldKeyword, newKeyword);
        }

        int i, j, num;
        StringBuilder sb = new StringBuilder();

        len_string = str.Length;
        len_old = oldKeyword.Length;
        len_new = newKeyword.Length;

        i = j = num = 0;

        while (true)
        {
            i = SearchStr(str, oldKeyword, i, caseSensitive);
            if (i == -1)
            {
                sb.Append(str.Substring(j, len_string - j));
                break;
            }

            num++;

            sb.Append(str.Substring(j, i - j));
            sb.Append(newKeyword);

            i += len_old;
            j = i;
        }

        return sb.ToString();

    }

    public static int SearchStr(string str, string keyword, int start, bool caseSensitive = false)
    {
        if (str == null || keyword == null)
        {
            return -1;
        }

        try
        {
            return str.IndexOf(keyword, start, (caseSensitive ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase));
        }
        catch
        {
            return -1;
        }
    }

    public static string NormalizeString(string str)
    {
        if (str == null)
        {
            return "";
        }

        return str.Trim();
    }
    // 文字列を正規化する
    public static void NormalizeString(ref string str)
    {
        if (str == null)
        {
            str = "";
        }

        str = str.Trim();
    }

    // 文字列を正規化する

    public static void NormalizeStringStandard(ref string str)
    {
        NormalizeString(ref str, true, true, false, true);
    }
    public static void NormalizeString(ref string str, bool space, bool toHankaku, bool toZenkaku, bool toZenkakuKana)
    {
        NormalizeString(ref str);

        if (space)
        {
            str = NormalizeSpace(str);
        }

        if (toHankaku)
        {
            str = ZenkakuToHankaku(str);
        }

        if (toZenkaku)
        {
            str = HankakuToZenkaku(str);
        }

        if (toZenkakuKana)
        {
            str = KanaHankakuToZenkaku(str);
        }
    }
    public static string NormalizeString(string src, bool space, bool toHankaku, bool toZenkaku, bool toZenkakuKana)
    {
        NormalizeString(ref src, space, toHankaku, toZenkaku, toZenkakuKana);
        return src;
    }

    // 半角カナを全角カナに変換する
    public static string KanaHankakuToZenkaku(string str)
    {
        NormalizeString(ref str);

        str = str.Replace("ｶﾞ", "ガ");
        str = str.Replace("ｷﾞ", "ギ");
        str = str.Replace("ｸﾞ", "グ");
        str = str.Replace("ｹﾞ", "ゲ");
        str = str.Replace("ｺﾞ", "ゴ");
        str = str.Replace("ｻﾞ", "ザ");
        str = str.Replace("ｼﾞ", "ジ");
        str = str.Replace("ｽﾞ", "ズ");
        str = str.Replace("ｾﾞ", "ゼ");
        str = str.Replace("ｿﾞ", "ゾ");
        str = str.Replace("ﾀﾞ", "ダ");
        str = str.Replace("ﾁﾞ", "ヂ");
        str = str.Replace("ﾂﾞ", "ヅ");
        str = str.Replace("ﾃﾞ", "デ");
        str = str.Replace("ﾄﾞ", "ド");
        str = str.Replace("ﾊﾞ", "バ");
        str = str.Replace("ﾋﾞ", "ビ");
        str = str.Replace("ﾌﾞ", "ブ");
        str = str.Replace("ﾍﾞ", "ベ");
        str = str.Replace("ﾎﾞ", "ボ");
        str = str.Replace("ﾊﾟ", "パ");
        str = str.Replace("ﾋﾟ", "ピ");
        str = str.Replace("ﾌﾟ", "プ");
        str = str.Replace("ﾍﾟ", "ペ");
        str = str.Replace("ﾎﾟ", "ポ");

        char[] a = str.ToCharArray();
        int i;
        for (i = 0; i < a.Length; i++)
        {
            int j = ConstKanaHankaku.IndexOf(a[i]);

            if (j != -1)
            {
                a[i] = ConstKanaZenkaku[j];
            }
        }

        return new string(a);
    }
    // 半角を全角に変換する
    public static string HankakuToZenkaku(string str)
    {
        NormalizeString(ref str);

        str = KanaHankakuToZenkaku(str);

        char[] a = str.ToCharArray();
        int i;
        for (i = 0; i < a.Length; i++)
        {
            int j = ConstHankaku.IndexOf(a[i]);

            if (j != -1)
            {
                a[i] = ConstZenkaku[j];
            }
        }

        return new string(a);
    }

    public static bool _IsNullOrZeroLen(this string str) => string.IsNullOrEmpty(str);

    // スペースを正規化する
    public static string NormalizeSpace(string str)
    {
        NormalizeString(ref str);
        char[] sps =
        {
                ' ', '　', '\t',
            };

        string[] tokens = str.Split(sps, StringSplitOptions.RemoveEmptyEntries);

        return CombineStringArray(tokens, " ");
    }


    // 全角を半角に変換する
    public static string ZenkakuToHankaku(string str)
    {
        NormalizeString(ref str);

        str = ReplaceStr(str, "“", "\"");
        str = ReplaceStr(str, "”", "\"");
        str = ReplaceStr(str, "‘", "'");
        str = ReplaceStr(str, "’", "'");

        char[] a = str.ToCharArray();
        int i;
        for (i = 0; i < a.Length; i++)
        {
            int j = ConstZenkaku.IndexOf(a[i]);

            if (j != -1)
            {
                a[i] = ConstHankaku[j];
            }
        }

        return new string(a);
    }


    // 複数の文字列を結合する
    public static string CombineStringArray(string sepstr, params string[] strs)
    {
        if (strs == null) return "";

        List<string> tmp = new List<string>();

        foreach (string str in strs)
        {
            if (str._IsFilled())
            {
                tmp.Add(str);
            }
        }

        return CombineStringArray(tmp.ToArray(), sepstr);
    }


    public static string CombineStringArray(string sepstr, params object[] objs)
    {
        List<string> tmp = new List<string>();

        foreach (object obj in objs)
        {
            string str = (obj == null ? "" : obj.ToString());
            if (IsEmptyStr(str) == false)
            {
                tmp.Add(str);
            }
        }

        return CombineStringArray(tmp.ToArray(), sepstr);
    }

    public static string CombineStringArray(IEnumerable<string> strList, string sepstr = "", bool removeEmpty = false, int maxItems = int.MaxValue, string ommitStr = "...")
    {
        sepstr = sepstr._NonNull();

        StringBuilder b = new StringBuilder();

        int num = 0;

        foreach (string s in strList)
        {
            if (removeEmpty == false || s._IsFilled())
            {
                if (num >= maxItems)
                {
                    b.Append(ommitStr._NonNull());
                    break;
                }

                if (num >= 1)
                {
                    b.Append(sepstr);
                }

                if (s != null) b.Append(s);

                num++;
            }
        }

        return b.ToString();
    }

    public static bool _IsFilled(this string str) => IsFilledStr(str);


    public static bool IsFilledStr(string str)
    {
        return !IsEmptyStr(str);
    }

    public static bool IsEmptyStr(string s)
    {
        return string.IsNullOrWhiteSpace(s);
    }

    public const string ConstZenkaku = "｀｛｝０１２３４５６７８９／＊－＋！”＃＄％＆’（）＝￣｜￥［］＠；：＜＞？＿＾　ａｂｃｄｅｆｇｈｉｊｋｌｍｎｏｐｑｒｓｔｕｖｗｘｙｚＡＢＣＤＥＦＧＨＩＪＫＬＭＮＯＰＱＲＳＴＵＶＷＸＹＺ‘";
    public const string ConstHankaku = "`{}0123456789/*-+!\"#$%&'()=~|\\[]@;:<>?_^ abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ'";
    public const string ConstKanaZenkaku = "ー「」アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲンァゥェォャュョッィ゛゜";
    public const string ConstKanaHankaku = "ｰ｢｣ｱｲｳｴｵｶｷｸｹｺｻｼｽｾｿﾀﾁﾂﾃﾄﾅﾆﾇﾈﾉﾊﾋﾌﾍﾎﾏﾐﾑﾒﾓﾔﾕﾖﾗﾘﾙﾚﾛﾜｦﾝｧｩｪｫｬｭｮｯｨﾞﾟ";
    public const string ConstKanaZenkakuDakuon = "ガギグゲゴザジズゼゾダヂヅデドバビブベボパピプペポ";

    public static List<KeyValuePair<string, string>> GetLinesWithExactCrlfNewLines(string srcText)
    {
        if (srcText == null) srcText = "";

        List<KeyValuePair<string, string>> ret = new List<KeyValuePair<string, string>>();

        int len = srcText.Length;

        StringBuilder b = new StringBuilder();

        for (int i = 0; i < len; i++)
        {
            char c = srcText[i];

            if (c == '\r')
            {
                char c2 = (char)0;
                if (i < (len - 1))
                {
                    c2 = srcText[i + 1];
                }
                if (c2 == '\n')
                {
                    ret.Add(new KeyValuePair<string, string>(b.ToString(), "\r\n"));
                    i++;
                }
                else
                {
                    ret.Add(new KeyValuePair<string, string>(b.ToString(), "\r"));
                }
                b.Clear();
            }
            else if (c == '\n')
            {
                ret.Add(new KeyValuePair<string, string>(b.ToString(), "\n"));
                b.Clear();
            }
            else
            {
                b.Append(c);
            }
        }

        if (b.Length >= 1)
        {
            ret.Add(new KeyValuePair<string, string>(b.ToString(), ""));
        }

        return ret;
    }

    // QueryString をパースする
    public static QueryStringList ParseQueryString(string src, Encoding encoding = null, char splitChar = '&', bool trimKeyAndValue = false)
    {
        return new QueryStringList(src, encoding, splitChar, trimKeyAndValue);
    }

    // URL エンコード
    public static string EncodeUrl(string str, Encoding encoding = null, UrlEncodeParam param = null)
    {
        if (encoding == null) encoding = Encoding.UTF8;
        if (str == null) str = "";

        string ignoreCharList = param?.DoNotEncodeCharList._NonNull() ?? "";

        if (string.IsNullOrEmpty(ignoreCharList))
        {
            return Uri.EscapeDataString(str);
        }
        else
        {
            StringBuilder b = new StringBuilder();
            foreach (char c in str)
            {
                if (ignoreCharList.IndexOf(c) == -1)
                {
                    b.Append(Uri.EscapeDataString("" + c));
                }
                else
                {
                    b.Append(c);
                }
            }
            return b.ToString();
        }
    }


    // URL デコード
    public static string DecodeUrl(string str, Encoding encoding = null)
    {
        if (encoding == null) encoding = Encoding.UTF8;
        if (str == null) str = "";
        return Uri.UnescapeDataString(str);
    }


    public static bool TryParseUrl(string urlString, out Uri uri, out QueryStringList queryString, Encoding encoding = null)
    {
        if (encoding == null) encoding = Encoding.UTF8;
        if (string.IsNullOrEmpty(urlString)) throw new ApplicationException("url_string is empty.");
        if (urlString.StartsWith("/")) urlString = "http://null" + urlString;
        if (Uri.TryCreate(urlString, UriKind.Absolute, out uri))
        {
            queryString = ParseQueryString(uri.Query, encoding);
            return true;
        }
        else
        {
            uri = null;
            queryString = null;
            return false;
        }
    }

    public static string GenerateRandTagWithYyymmdd(DateTimeOffset now, int numCharsInTagTotal)
    {
        string dstr = now.LocalDateTime.ToString("yyMMdd"); 
        string timeCandidates = "ABCDEFGHJKLMNPQRSTUVWXYZ";

        int v1 = Math.Max(Math.Min((int)((double)now.LocalDateTime.Minute / 60.0 * (double)timeCandidates.Length), timeCandidates.Length), 0);
        int v2 = Math.Max(Math.Min((int)((double)now.LocalDateTime.Second / 60.0 * (double)timeCandidates.Length), timeCandidates.Length), 0);

        return dstr + "_" + timeCandidates[now.LocalDateTime.Hour] + timeCandidates[v1] + timeCandidates[v2] + GenerateRandTagCore("ABCDEFGHJKLMNPQRSTUVWXYZ23456789", numCharsInTagTotal - 3, "23456789");
    }

    public static string GenerateRandTag(int numCharsTotal)
    {
        return GenerateRandTagCore("ABCDEFGHJKLMNPQRSTUVWXYZ", 1) + GenerateRandTagCore("ABCDEFGHJKLMNPQRSTUVWXYZ23456789", numCharsTotal - 1, "23456789");
    }

    static string GenerateRandTagCore(string candidates, int numChars, string mustContainChars = "")
    {
        int numRetry = 0;

        L_RETRY:

        StringBuilder sb = new StringBuilder(numChars);

        for (int i = 0; i < numChars; i++)
        {
            int r = RandGen.GenRandSInt31() % candidates.Length;
            sb.Append(candidates[r]);
        }

        string ret = sb.ToString();

        if (mustContainChars._IsFilled())
        {
            bool ok = false;

            foreach (char c in ret)
            {
                if (mustContainChars.IndexOf(c) != -1)
                {
                    ok = true;
                    break;
                }
            }

            if (ok == false)
            {
                if (numRetry >= 1000)
                {
                    throw new Exception("numRetry >= 1000");
                }

                numRetry++;

                goto L_RETRY;
            }
        }

        return ret;
    }

    public static string NormalizeComfortableUrl(string srcUrl)
    {
        if (srcUrl == null) return "";

        try
        {
            string[] amazonStoreHostNameList = new string[]
                {
                    "www.amazon.co.jp",
                    "www.amazon.com",
                    "www.amazon.co.uk",
                };

            string[] defaultFileNamesList = new string[]
                {
                    "index.html",
                    "index.htm",
                    "index.cgi",
                    "default.html",
                    "default.htm",
                    "default.asp",
                    "default.aspx",
                    "index.php",
                    "index.pl",
                    "index.py",
                    "index.rb",
                    "index.xhtml",
                    "index.shtml",
                    "index.phtml",
                    "index.jsp",
                    "index.wml",
                    "index.asp",
                    "index.aspx",
                    "welcome.html",
                    "index.nginx-debian.html",
                };

            var originalLines = GetLinesWithExactCrlfNewLines(srcUrl);

            List<KeyValuePair<string, string>> lineDataDestination = new List<KeyValuePair<string, string>>();

            foreach (var lineDataOriginal in originalLines)
            {
                var lineData = lineDataOriginal;
                string line = lineData.Key;

                var trimmedLine = AdvancedTrim(line);

                string line2 = trimmedLine.Item1;

                bool isUrlLineOrEmptyLine = false;

                if (line2.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    line2.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseUrl(line2, out Uri uri, out QueryStringList qs))
                    {
                        string absolutePath = uri.AbsolutePath;

                        // ディレクトリ名が省略されている場合、これを追加
                        if (string.IsNullOrEmpty(uri.Query)) // Query string がある場合は、ここはいじらない
                        {
                            if (uri.AbsolutePath.EndsWith("/") == false)
                            {
                                string tmp1 = uri.Segments.LastOrDefault();
                                if (tmp1 == null) tmp1 = "";
                                if (tmp1.Length >= 3 && tmp1.Substring(1, tmp1.Length - 2).Contains("."))
                                {
                                    // /a/b/c/test.pdf のように、最後のパスに拡張子が含まれている場合: 何もしない
                                }
                                else
                                {
                                    // それ以外の場合: "/" を追加
                                    absolutePath = uri.AbsolutePath + "/";
                                }
                            }
                        }

                        // 末尾が index.html などの場合、これを削除
                        foreach (var defFileName in defaultFileNamesList)
                        {
                            if (absolutePath.EndsWith("/" + defFileName, StringComparison.OrdinalIgnoreCase))
                            {
                                absolutePath = absolutePath.Substring(0, absolutePath.Length - defFileName.Length);
                            }
                        }

                        if (string.IsNullOrEmpty(absolutePath))
                        {
                            absolutePath = "/";
                        }

                        string query = "";

                        if (string.IsNullOrEmpty(uri.Query) == false && uri.Query != "?")
                        {
                            query = uri.Query;
                        }

                        string fragment = "";

                        if (string.IsNullOrEmpty(uri.Fragment) == false && uri.Fragment != "#")
                        {
                            fragment = uri.Fragment;
                        }

                        foreach (var amazonHostNameCandidate in amazonStoreHostNameList)
                        {
                            if (amazonHostNameCandidate.Equals(uri.Host, StringComparison.OrdinalIgnoreCase))
                            {
                                // Amazon 商品 URL を簡略化
                                string[] tokens = uri.AbsolutePath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                                if (tokens.Length >= 3 && tokens[1].Equals("dp", StringComparison.OrdinalIgnoreCase) && tokens[2].Length == 10)
                                {
                                    absolutePath = "/dp/" + tokens[2] + "/";
                                    query = "";
                                    fragment = "";
                                }
                                else if (tokens.Length >= 2 && tokens[0].Equals("dp", StringComparison.OrdinalIgnoreCase) && tokens[1].Length == 10)
                                {
                                    absolutePath = "/dp/" + tokens[1] + "/";
                                    query = "";
                                    fragment = "";
                                }
                                else if (tokens.Length >= 3 && tokens[0].Equals("gp", StringComparison.OrdinalIgnoreCase) && tokens[1].Equals("product", StringComparison.OrdinalIgnoreCase) && tokens[2].Length == 10)
                                {
                                    absolutePath = "/dp/" + tokens[2] + "/";
                                    query = "";
                                    fragment = "";
                                }
                                break;
                            }
                        }

                        if (uri.Host.Equals("www.ebay.com", StringComparison.OrdinalIgnoreCase))
                        {
                            // ebay 商品 URL を簡略化
                            string[] tokens = uri.AbsolutePath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                            if (tokens.Length >= 2 && tokens[0].Equals("itm", StringComparison.OrdinalIgnoreCase) && tokens[1].Length >= 10)
                            {
                                absolutePath = "/itm/" + tokens[1] + "/";
                                query = "";
                                fragment = "";
                            }
                        }

                        if (uri.Host.Equals("www.askul.co.jp", StringComparison.OrdinalIgnoreCase))
                        {
                            // askul 商品 URL を簡略化
                            string[] tokens = uri.AbsolutePath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                            if (tokens.Length >= 2 && tokens[0].Equals("p", StringComparison.OrdinalIgnoreCase) && tokens[1].Length >= 4)
                            {
                                absolutePath = "/p/" + tokens[1] + "/";
                                query = "";
                                fragment = "";
                            }
                        }

                        if (query.IndexOf("utm_source=", StringComparison.OrdinalIgnoreCase) != -1)
                        {
                            bool changed = false;
                            var list = QueryStringList.Parse(query);

                            QueryStringList qs2 = new QueryStringList();

                            foreach (var kv in list)
                            {
                                if (kv.Key.Equals("utm_source", StringComparison.OrdinalIgnoreCase))
                                {
                                    changed = true;
                                }
                                else
                                {
                                    qs2.Add(kv);
                                }
                            }

                            if (changed)
                            {
                                query = qs2.ToString();

                                if (query.Trim().Length >= 1)
                                {
                                    query = "?" + query;
                                }
                            }
                        }

                        string dstText = uri.Scheme + "://" + uri.Host + (uri.IsDefaultPort ? "" : ":" + uri.Port) + absolutePath + query + fragment;
                        trimmedLine = new Tuple<string, string, string>(dstText, trimmedLine.Item2, trimmedLine.Item3);

                        isUrlLineOrEmptyLine = true;
                    }
                }

                if (string.IsNullOrEmpty(line2))
                {
                    isUrlLineOrEmptyLine = true;
                }

                if (isUrlLineOrEmptyLine == false)
                {
                    // URL でも空白行でもない行が出現したら、処理を一切しない
                    return srcUrl;
                }

                lineData = new KeyValuePair<string, string>(trimmedLine.Item2 + trimmedLine.Item1 + trimmedLine.Item3, lineData.Value);

                lineDataDestination.Add(lineData);
            }

            StringBuilder b = new StringBuilder();
            foreach (var lineData in lineDataDestination)
            {
                b.Append(lineData.Key);
                b.Append(lineData.Value);
            }

            return b.ToString();
        }
        catch
        {
            return srcUrl;
        }
    }

    public static Tuple<string, string, string> AdvancedTrim(
    string srcText,
    bool trimStart = true,
    bool trimEnd = true,
    char[] splitCharList = null)
    {
        if (srcText == null) srcText = "";
        if (splitCharList == null)
        {
            splitCharList = new char[] { ' ', '　', '\t', '\n', '\r', };
        }

        int len = srcText.Length;

        // トリムの開始位置と終了位置を決めるための変数を用意
        int startIndex = 0;
        int endIndex = len - 1;

        // 先頭のトリム
        if (trimStart)
        {
            while (startIndex < len)
            {
                if (!splitCharList.Contains(srcText[startIndex]))
                {
                    break;
                }
                startIndex++;
            }
        }

        // 末尾のトリム
        if (trimEnd)
        {
            while (endIndex >= startIndex)
            {
                if (!splitCharList.Contains(srcText[endIndex]))
                {
                    break;
                }
                endIndex--;
            }
        }

        // 先頭・末尾それぞれトリムされた文字列を取得
        // (trimStart = true のときのみ先頭が削られるので、その削られた部分を removedStart に入れる)
        string removedStart = (trimStart && startIndex > 0)
            ? srcText.Substring(0, startIndex)
            : "";

        // (trimEnd = true のときのみ末尾が削られるので、その削られた部分を removedEnd に入れる)
        string removedEnd = (trimEnd && endIndex < len - 1)
            ? srcText.Substring(endIndex + 1)
            : "";

        // トリム後の本体文字列
        string trimmedString;
        if (startIndex > endIndex)
        {
            // すべてがトリム対象になってしまった場合は空文字を返す
            trimmedString = "";
        }
        else
        {
            trimmedString = srcText.Substring(startIndex, endIndex - startIndex + 1);
        }

        return new Tuple<string, string, string>(trimmedString, removedStart, removedEnd);
    }


}



public class KeyValueList<TKey, TValue> : List<KeyValuePair<TKey, TValue>>
{
    public KeyValueList() { }

    public KeyValueList(IEnumerable<KeyValuePair<TKey, TValue>> srcData)
    {
        foreach (var kv in srcData)
        {
            this.Add(kv.Key, kv.Value);
        }
    }

    public void Add(TKey key, TValue value)
    {
        this.Add(new KeyValuePair<TKey, TValue>(key, value));
    }

    public KeyValueList<TKey, TValue> Clone()
    {
        KeyValueList<TKey, TValue> ret = new KeyValueList<TKey, TValue>();

        foreach (var kv in this)
        {
            ret.Add(kv.Key, kv.Value);
        }

        return ret;
    }

    public void RemoveWhen(Func<KeyValuePair<TKey, TValue>, bool> condition)
    {
        List<KeyValuePair<TKey, TValue>> toRemove = new List<KeyValuePair<TKey, TValue>>();

        foreach (var kv in this)
            if (condition(kv))
                toRemove.Add(kv);

        foreach (var kv in toRemove)
            this.Remove(kv);
    }

    public void RemoveWhenKey(TKey key, IEqualityComparer<TKey> comparer)
    {
        this.RemoveWhen(kv => comparer.Equals(kv.Key, key));
    }

    public void AddOrUpdateKeyValueSingle(TKey key, TValue value, IEqualityComparer<TKey> comparer)
    {
        this.RemoveWhenKey(key, comparer);

        this.Add(key, value);
    }

    public TValue GetSingleOrNew(TKey key, TValue newValue, IEqualityComparer<TKey> comparer = null)
    {
        var exists = this.Where(x => comparer?.Equals(x.Key, key) ?? object.Equals(x.Key, key));
        if (exists.Any()) return exists.Single().Value;

        this.Add(key, newValue);
        return newValue;
    }

    public TValue GetSingleOrNew(TKey key, Func<TValue> newValueProc, IEqualityComparer<TKey> comparer = null)
    {
        var exists = this.Where(x => comparer?.Equals(x.Key, key) ?? object.Equals(x.Key, key));
        if (exists.Any()) return exists.Single().Value;

        TValue newValue = newValueProc();
        this.Add(key, newValue);
        return newValue;
    }
}

public class UrlEncodeParam
{
    public string DoNotEncodeCharList { get; set; }

    public UrlEncodeParam(string dontEncodeCharList = "")
    {
        this.DoNotEncodeCharList = dontEncodeCharList;
    }
}

public class QueryStringList : KeyValueList<string, string>
{
    public char SplitChar = '&';

    public static QueryStringList Parse(string queryString, Encoding encoding = null, char splitChar = '&', bool trimKeyAndValue = false)
    {
        try
        {
            return new QueryStringList(queryString, encoding, splitChar, trimKeyAndValue);
        }
        catch
        {
            return new QueryStringList();
        }
    }

    public QueryStringList() { }

    public QueryStringList(IEnumerable<KeyValuePair<string, string>> srcData)
    {
        foreach (var kv in srcData)
        {
            this.Add(kv.Key, kv.Value);
        }
    }

    public QueryStringList(string queryString, Encoding encoding = null, char splitChar = '&', bool trimKeyAndValue = false)
    {
        this.SplitChar = splitChar;

        if (encoding == null) encoding = Encoding.UTF8;

        queryString = queryString._NonNull();

        // 先頭に ? があれば無視する
        if (queryString.StartsWith("?")) queryString = queryString.Substring(1);

        // ハッシュ文字 # があればそれ以降は無視する
        int i = queryString.IndexOf('#');
        if (i != -1) queryString = queryString.Substring(0, i);

        // & で分離する
        string[] tokens = queryString.Split(new char[] { this.SplitChar }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string token in tokens)
        {
            // key と value を取得する
            string key, value;

            i = token.IndexOf('=');

            if (i == -1)
            {
                key = token;
                value = "";
            }
            else
            {
                key = token.Substring(0, i);
                value = token.Substring(i + 1);
            }

            // key と value を URL デコードする
            key = Lib.DecodeUrl(key, encoding);
            value = Lib.DecodeUrl(value, encoding);

            if (trimKeyAndValue)
            {
                key = key.Trim();
                value = value.Trim();
            }

            this.Add(key, value);
        }
    }

    public override string ToString()
        => ToString(null);

    public string ToString(Encoding encoding, UrlEncodeParam urlEncodeParam = null)
    {
        if (encoding == null) encoding = Encoding.UTF8;

        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < this.Count; i++)
        {
            var kv = this[i];
            bool isLast = (i == (this.Count - 1));

            if (kv.Key._IsFilled() || kv.Value._IsFilled())
            {
                string key = kv.Key._NonNull();
                string value = kv.Value._NonNull();

                // key と value を URL エンコードする
                key = Lib.EncodeUrl(key, encoding, urlEncodeParam);
                value = Lib.EncodeUrl(value, encoding, urlEncodeParam);

                if (string.IsNullOrEmpty(value))
                {
                    sb.Append(key);
                }
                else
                {
                    sb.Append(key);
                    sb.Append('=');
                    sb.Append(value);
                }

                if (isLast == false)
                {
                    sb.Append(this.SplitChar);
                }
            }
        }

        return sb.ToString();
    }
}


// Unicode の康熙部首 (The Unicode Standard Kangxi Radicals) の変換ユーティティ
public static class UnicodeStdKangxiMapUtil
{
    // Strange: ⺃⺅⺉⺊⺋⺏⺐⺑⺒⺓⺔⺖⺘⺙⺛⺞⺟⺠⺡⺢⺣⺤⺥⺦⺨⺩⺪⺫⺬⺭⺯⺰⺱⺲⺸⺹⺺⺽⺾⺿⻀⻁⻂⻃⻄⻅⻈⻉⻋⻍⻎⻐⻑⻒⻓⻔⻖⻘⻙⻚⻛⻜⻝⻟⻠⻢⻣⻤⻥⻦⻧⻨⻩⻪⻫⻬⻭⻮⻯⻰⻱⻲⻳⼀⼁⼂⼃⼄⼅⼆⼇⼈⼉⼊⼋⼌⼍⼎⼏⼐⼑⼒⼓⼔⼕⼖⼗⼘⼙⼚⼛⼜⼝⼞⼟⼠⼡⼢⼣⼤⼥⼦⼧⼨⼩⼪⼫⼬⼭⼮⼯⼰⼱⼲⼳⼴⼵⼶⼷⼸⼹⼺⼻⼼⼽⼾⼿⽀⽁⽂⽃⽄⽅⽆⽇⽈⽉⽊⽋⽌⽍⽎⽏⽐⽑⽒⽓⽔⽕⽖⽗⽘⽙⽚⽛⽜⽝⽞⽟⽠⽡⽢⽣⽤⽥⽦⽧⽨⽩⽪⽫⽬⽭⽮⽯⽰⽱⽲⽳⽴⽵⽶⽷⽸⽹⽺⽻⽼⽽⽾⽿⾀⾁⾂⾃⾄⾅⾆⾇⾈⾉⾊⾋⾌⾍⾎⾏⾐⾑⾒⾓⾔⾕⾖⾗⾘⾙⾚⾛⾜⾝⾞⾟⾠⾡⾢⾣⾤⾥⾦⾧⾨⾩⾪⾫⾬⾭⾮⾯⾰⾱⾲⾳⾴⾵⾶⾷⾸⾹⾺⾻⾼⾽⾾⾿⿀⿁⿂⿃⿄⿅⿆⿇⿈⿉⿊⿋⿌⿍⿎⿏⿐⿑⿒⿓⿔⿕

    // Normal: 乚亻刂卜㔾尣尢尣巳幺彑忄扌攵旡歺母民氵氺灬爫爫丬犭王疋目示礻糹纟罓罒羋耂肀臼艹艹艹虎衤覀西见讠贝车辶辶钅長镸长门阝青韦页风飞食飠饣马骨鬼鱼鸟卤麦黄黾斉齐歯齿竜龙龜亀龟一丨丶丿乙亅二亠人儿入八冂冖冫几凵刀力勹匕匚匸十卜卩厂厶又口囗土士夂夊夕大女子宀寸小尢尸屮山巛工己巾干幺广廴廾弋弓彐彡彳心戈戶手支攴文斗斤方无日曰月木欠止歹殳毋比毛氏气水火爪父爻爿片牙牛犬玄玉瓜瓦甘生用田疋疒癶白皮皿目矛矢石示禸禾穴立竹米糸缶网羊羽老而耒耳聿肉臣自至臼舌舛舟艮色艸虍虫血行衣襾見角言谷豆豕豸貝赤走足身車辛辰辵邑酉釆里金長門阜隶隹雨靑非面革韋韭音頁風飛食首香馬骨高髟鬥鬯鬲鬼魚鳥鹵鹿麥麻黃黍黑黹黽鼎鼓鼠鼻齊齒龍龜龠

    // '⺃' (0x2e83) <--> '乚' (0x4e5a)
    // '⺅' (0x2e85) <--> '亻' (0x4ebb)
    // '⺉' (0x2e89) <--> '刂' (0x5202)
    // '⺊' (0x2e8a) <--> '卜' (0x535c)
    // '⺋' (0x2e8b) <--> '㔾' (0x353e)
    // '⺏' (0x2e8f) <--> '尣' (0x5c23)
    // '⺐' (0x2e90) <--> '尢' (0x5c22)
    // '⺑' (0x2e91) <--> '尣' (0x5c23)
    // '⺒' (0x2e92) <--> '巳' (0x5df3)
    // '⺓' (0x2e93) <--> '幺' (0x5e7a)
    // '⺔' (0x2e94) <--> '彑' (0x5f51)
    // '⺖' (0x2e96) <--> '忄' (0x5fc4)
    // '⺘' (0x2e98) <--> '扌' (0x624c)
    // '⺙' (0x2e99) <--> '攵' (0x6535)
    // '⺛' (0x2e9b) <--> '旡' (0x65e1)
    // '⺞' (0x2e9e) <--> '歺' (0x6b7a)
    // '⺟' (0x2e9f) <--> '母' (0x6bcd)
    // '⺠' (0x2ea0) <--> '民' (0x6c11)
    // '⺡' (0x2ea1) <--> '氵' (0x6c35)
    // '⺢' (0x2ea2) <--> '氺' (0x6c3a)
    // '⺣' (0x2ea3) <--> '灬' (0x706c)
    // '⺤' (0x2ea4) <--> '爫' (0x722b)
    // '⺥' (0x2ea5) <--> '爫' (0x722b)
    // '⺦' (0x2ea6) <--> '丬' (0x4e2c)
    // '⺨' (0x2ea8) <--> '犭' (0x72ad)
    // '⺩' (0x2ea9) <--> '王' (0x738b)
    // '⺪' (0x2eaa) <--> '疋' (0x758b)
    // '⺫' (0x2eab) <--> '目' (0x76ee)
    // '⺬' (0x2eac) <--> '示' (0x793a)
    // '⺭' (0x2ead) <--> '礻' (0x793b)
    // '⺯' (0x2eaf) <--> '糹' (0x7cf9)
    // '⺰' (0x2eb0) <--> '纟' (0x7e9f)
    // '⺱' (0x2eb1) <--> '罓' (0x7f53)
    // '⺲' (0x2eb2) <--> '罒' (0x7f52)
    // '⺸' (0x2eb8) <--> '羋' (0x7f8b)
    // '⺹' (0x2eb9) <--> '耂' (0x8002)
    // '⺺' (0x2eba) <--> '肀' (0x8080)
    // '⺽' (0x2ebd) <--> '臼' (0x81fc)
    // '⺾' (0x2ebe) <--> '艹' (0x8279)
    // '⺿' (0x2ebf) <--> '艹' (0x8279)
    // '⻀' (0x2ec0) <--> '艹' (0x8279)
    // '⻁' (0x2ec1) <--> '虎' (0x864e)
    // '⻂' (0x2ec2) <--> '衤' (0x8864)
    // '⻃' (0x2ec3) <--> '覀' (0x8980)
    // '⻄' (0x2ec4) <--> '西' (0x897f)
    // '⻅' (0x2ec5) <--> '见' (0x89c1)
    // '⻈' (0x2ec8) <--> '讠' (0x8ba0)
    // '⻉' (0x2ec9) <--> '贝' (0x8d1d)
    // '⻋' (0x2ecb) <--> '车' (0x8f66)
    // '⻍' (0x2ecd) <--> '辶' (0x8fb6)
    // '⻎' (0x2ece) <--> '辶' (0x8fb6)
    // '⻐' (0x2ed0) <--> '钅' (0x9485)
    // '⻑' (0x2ed1) <--> '長' (0x9577)
    // '⻒' (0x2ed2) <--> '镸' (0x9578)
    // '⻓' (0x2ed3) <--> '长' (0x957f)
    // '⻔' (0x2ed4) <--> '门' (0x95e8)
    // '⻖' (0x2ed6) <--> '阝' (0x961d)
    // '⻘' (0x2ed8) <--> '青' (0x9752)
    // '⻙' (0x2ed9) <--> '韦' (0x97e6)
    // '⻚' (0x2eda) <--> '页' (0x9875)
    // '⻛' (0x2edb) <--> '风' (0x98ce)
    // '⻜' (0x2edc) <--> '飞' (0x98de)
    // '⻝' (0x2edd) <--> '食' (0x98df)
    // '⻟' (0x2edf) <--> '飠' (0x98e0)
    // '⻠' (0x2ee0) <--> '饣' (0x9963)
    // '⻢' (0x2ee2) <--> '马' (0x9a6c)
    // '⻣' (0x2ee3) <--> '骨' (0x9aa8)
    // '⻤' (0x2ee4) <--> '鬼' (0x9b3c)
    // '⻥' (0x2ee5) <--> '鱼' (0x9c7c)
    // '⻦' (0x2ee6) <--> '鸟' (0x9e1f)
    // '⻧' (0x2ee7) <--> '卤' (0x5364)
    // '⻨' (0x2ee8) <--> '麦' (0x9ea6)
    // '⻩' (0x2ee9) <--> '黄' (0x9ec4)
    // '⻪' (0x2eea) <--> '黾' (0x9efe)
    // '⻫' (0x2eeb) <--> '斉' (0x6589)
    // '⻬' (0x2eec) <--> '齐' (0x9f50)
    // '⻭' (0x2eed) <--> '歯' (0x6b6f)
    // '⻮' (0x2eee) <--> '齿' (0x9f7f)
    // '⻯' (0x2eef) <--> '竜' (0x7adc)
    // '⻰' (0x2ef0) <--> '龙' (0x9f99)
    // '⻱' (0x2ef1) <--> '龜' (0x9f9c)
    // '⻲' (0x2ef2) <--> '亀' (0x4e80)
    // '⻳' (0x2ef3) <--> '龟' (0x9f9f)
    // '⼀' (0x2f00) <--> '一' (0x4e00)
    // '⼁' (0x2f01) <--> '丨' (0x4e28)
    // '⼂' (0x2f02) <--> '丶' (0x4e36)
    // '⼃' (0x2f03) <--> '丿' (0x4e3f)
    // '⼄' (0x2f04) <--> '乙' (0x4e59)
    // '⼅' (0x2f05) <--> '亅' (0x4e85)
    // '⼆' (0x2f06) <--> '二' (0x4e8c)
    // '⼇' (0x2f07) <--> '亠' (0x4ea0)
    // '⼈' (0x2f08) <--> '人' (0x4eba)
    // '⼉' (0x2f09) <--> '儿' (0x513f)
    // '⼊' (0x2f0a) <--> '入' (0x5165)
    // '⼋' (0x2f0b) <--> '八' (0x516b)
    // '⼌' (0x2f0c) <--> '冂' (0x5182)
    // '⼍' (0x2f0d) <--> '冖' (0x5196)
    // '⼎' (0x2f0e) <--> '冫' (0x51ab)
    // '⼏' (0x2f0f) <--> '几' (0x51e0)
    // '⼐' (0x2f10) <--> '凵' (0x51f5)
    // '⼑' (0x2f11) <--> '刀' (0x5200)
    // '⼒' (0x2f12) <--> '力' (0x529b)
    // '⼓' (0x2f13) <--> '勹' (0x52f9)
    // '⼔' (0x2f14) <--> '匕' (0x5315)
    // '⼕' (0x2f15) <--> '匚' (0x531a)
    // '⼖' (0x2f16) <--> '匸' (0x5338)
    // '⼗' (0x2f17) <--> '十' (0x5341)
    // '⼘' (0x2f18) <--> '卜' (0x535c)
    // '⼙' (0x2f19) <--> '卩' (0x5369)
    // '⼚' (0x2f1a) <--> '厂' (0x5382)
    // '⼛' (0x2f1b) <--> '厶' (0x53b6)
    // '⼜' (0x2f1c) <--> '又' (0x53c8)
    // '⼝' (0x2f1d) <--> '口' (0x53e3)
    // '⼞' (0x2f1e) <--> '囗' (0x56d7)
    // '⼟' (0x2f1f) <--> '土' (0x571f)
    // '⼠' (0x2f20) <--> '士' (0x58eb)
    // '⼡' (0x2f21) <--> '夂' (0x5902)
    // '⼢' (0x2f22) <--> '夊' (0x590a)
    // '⼣' (0x2f23) <--> '夕' (0x5915)
    // '⼤' (0x2f24) <--> '大' (0x5927)
    // '⼥' (0x2f25) <--> '女' (0x5973)
    // '⼦' (0x2f26) <--> '子' (0x5b50)
    // '⼧' (0x2f27) <--> '宀' (0x5b80)
    // '⼨' (0x2f28) <--> '寸' (0x5bf8)
    // '⼩' (0x2f29) <--> '小' (0x5c0f)
    // '⼪' (0x2f2a) <--> '尢' (0x5c22)
    // '⼫' (0x2f2b) <--> '尸' (0x5c38)
    // '⼬' (0x2f2c) <--> '屮' (0x5c6e)
    // '⼭' (0x2f2d) <--> '山' (0x5c71)
    // '⼮' (0x2f2e) <--> '巛' (0x5ddb)
    // '⼯' (0x2f2f) <--> '工' (0x5de5)
    // '⼰' (0x2f30) <--> '己' (0x5df1)
    // '⼱' (0x2f31) <--> '巾' (0x5dfe)
    // '⼲' (0x2f32) <--> '干' (0x5e72)
    // '⼳' (0x2f33) <--> '幺' (0x5e7a)
    // '⼴' (0x2f34) <--> '广' (0x5e7f)
    // '⼵' (0x2f35) <--> '廴' (0x5ef4)
    // '⼶' (0x2f36) <--> '廾' (0x5efe)
    // '⼷' (0x2f37) <--> '弋' (0x5f0b)
    // '⼸' (0x2f38) <--> '弓' (0x5f13)
    // '⼹' (0x2f39) <--> '彐' (0x5f50)
    // '⼺' (0x2f3a) <--> '彡' (0x5f61)
    // '⼻' (0x2f3b) <--> '彳' (0x5f73)
    // '⼼' (0x2f3c) <--> '心' (0x5fc3)
    // '⼽' (0x2f3d) <--> '戈' (0x6208)
    // '⼾' (0x2f3e) <--> '戶' (0x6236)
    // '⼿' (0x2f3f) <--> '手' (0x624b)
    // '⽀' (0x2f40) <--> '支' (0x652f)
    // '⽁' (0x2f41) <--> '攴' (0x6534)
    // '⽂' (0x2f42) <--> '文' (0x6587)
    // '⽃' (0x2f43) <--> '斗' (0x6597)
    // '⽄' (0x2f44) <--> '斤' (0x65a4)
    // '⽅' (0x2f45) <--> '方' (0x65b9)
    // '⽆' (0x2f46) <--> '无' (0x65e0)
    // '⽇' (0x2f47) <--> '日' (0x65e5)
    // '⽈' (0x2f48) <--> '曰' (0x66f0)
    // '⽉' (0x2f49) <--> '月' (0x6708)
    // '⽊' (0x2f4a) <--> '木' (0x6728)
    // '⽋' (0x2f4b) <--> '欠' (0x6b20)
    // '⽌' (0x2f4c) <--> '止' (0x6b62)
    // '⽍' (0x2f4d) <--> '歹' (0x6b79)
    // '⽎' (0x2f4e) <--> '殳' (0x6bb3)
    // '⽏' (0x2f4f) <--> '毋' (0x6bcb)
    // '⽐' (0x2f50) <--> '比' (0x6bd4)
    // '⽑' (0x2f51) <--> '毛' (0x6bdb)
    // '⽒' (0x2f52) <--> '氏' (0x6c0f)
    // '⽓' (0x2f53) <--> '气' (0x6c14)
    // '⽔' (0x2f54) <--> '水' (0x6c34)
    // '⽕' (0x2f55) <--> '火' (0x706b)
    // '⽖' (0x2f56) <--> '爪' (0x722a)
    // '⽗' (0x2f57) <--> '父' (0x7236)
    // '⽘' (0x2f58) <--> '爻' (0x723b)
    // '⽙' (0x2f59) <--> '爿' (0x723f)
    // '⽚' (0x2f5a) <--> '片' (0x7247)
    // '⽛' (0x2f5b) <--> '牙' (0x7259)
    // '⽜' (0x2f5c) <--> '牛' (0x725b)
    // '⽝' (0x2f5d) <--> '犬' (0x72ac)
    // '⽞' (0x2f5e) <--> '玄' (0x7384)
    // '⽟' (0x2f5f) <--> '玉' (0x7389)
    // '⽠' (0x2f60) <--> '瓜' (0x74dc)
    // '⽡' (0x2f61) <--> '瓦' (0x74e6)
    // '⽢' (0x2f62) <--> '甘' (0x7518)
    // '⽣' (0x2f63) <--> '生' (0x751f)
    // '⽤' (0x2f64) <--> '用' (0x7528)
    // '⽥' (0x2f65) <--> '田' (0x7530)
    // '⽦' (0x2f66) <--> '疋' (0x758b)
    // '⽧' (0x2f67) <--> '疒' (0x7592)
    // '⽨' (0x2f68) <--> '癶' (0x7676)
    // '⽩' (0x2f69) <--> '白' (0x767d)
    // '⽪' (0x2f6a) <--> '皮' (0x76ae)
    // '⽫' (0x2f6b) <--> '皿' (0x76bf)
    // '⽬' (0x2f6c) <--> '目' (0x76ee)
    // '⽭' (0x2f6d) <--> '矛' (0x77db)
    // '⽮' (0x2f6e) <--> '矢' (0x77e2)
    // '⽯' (0x2f6f) <--> '石' (0x77f3)
    // '⽰' (0x2f70) <--> '示' (0x793a)
    // '⽱' (0x2f71) <--> '禸' (0x79b8)
    // '⽲' (0x2f72) <--> '禾' (0x79be)
    // '⽳' (0x2f73) <--> '穴' (0x7a74)
    // '⽴' (0x2f74) <--> '立' (0x7acb)
    // '⽵' (0x2f75) <--> '竹' (0x7af9)
    // '⽶' (0x2f76) <--> '米' (0x7c73)
    // '⽷' (0x2f77) <--> '糸' (0x7cf8)
    // '⽸' (0x2f78) <--> '缶' (0x7f36)
    // '⽹' (0x2f79) <--> '网' (0x7f51)
    // '⽺' (0x2f7a) <--> '羊' (0x7f8a)
    // '⽻' (0x2f7b) <--> '羽' (0x7fbd)
    // '⽼' (0x2f7c) <--> '老' (0x8001)
    // '⽽' (0x2f7d) <--> '而' (0x800c)
    // '⽾' (0x2f7e) <--> '耒' (0x8012)
    // '⽿' (0x2f7f) <--> '耳' (0x8033)
    // '⾀' (0x2f80) <--> '聿' (0x807f)
    // '⾁' (0x2f81) <--> '肉' (0x8089)
    // '⾂' (0x2f82) <--> '臣' (0x81e3)
    // '⾃' (0x2f83) <--> '自' (0x81ea)
    // '⾄' (0x2f84) <--> '至' (0x81f3)
    // '⾅' (0x2f85) <--> '臼' (0x81fc)
    // '⾆' (0x2f86) <--> '舌' (0x820c)
    // '⾇' (0x2f87) <--> '舛' (0x821b)
    // '⾈' (0x2f88) <--> '舟' (0x821f)
    // '⾉' (0x2f89) <--> '艮' (0x826e)
    // '⾊' (0x2f8a) <--> '色' (0x8272)
    // '⾋' (0x2f8b) <--> '艸' (0x8278)
    // '⾌' (0x2f8c) <--> '虍' (0x864d)
    // '⾍' (0x2f8d) <--> '虫' (0x866b)
    // '⾎' (0x2f8e) <--> '血' (0x8840)
    // '⾏' (0x2f8f) <--> '行' (0x884c)
    // '⾐' (0x2f90) <--> '衣' (0x8863)
    // '⾑' (0x2f91) <--> '襾' (0x897e)
    // '⾒' (0x2f92) <--> '見' (0x898b)
    // '⾓' (0x2f93) <--> '角' (0x89d2)
    // '⾔' (0x2f94) <--> '言' (0x8a00)
    // '⾕' (0x2f95) <--> '谷' (0x8c37)
    // '⾖' (0x2f96) <--> '豆' (0x8c46)
    // '⾗' (0x2f97) <--> '豕' (0x8c55)
    // '⾘' (0x2f98) <--> '豸' (0x8c78)
    // '⾙' (0x2f99) <--> '貝' (0x8c9d)
    // '⾚' (0x2f9a) <--> '赤' (0x8d64)
    // '⾛' (0x2f9b) <--> '走' (0x8d70)
    // '⾜' (0x2f9c) <--> '足' (0x8db3)
    // '⾝' (0x2f9d) <--> '身' (0x8eab)
    // '⾞' (0x2f9e) <--> '車' (0x8eca)
    // '⾟' (0x2f9f) <--> '辛' (0x8f9b)
    // '⾠' (0x2fa0) <--> '辰' (0x8fb0)
    // '⾡' (0x2fa1) <--> '辵' (0x8fb5)
    // '⾢' (0x2fa2) <--> '邑' (0x9091)
    // '⾣' (0x2fa3) <--> '酉' (0x9149)
    // '⾤' (0x2fa4) <--> '釆' (0x91c6)
    // '⾥' (0x2fa5) <--> '里' (0x91cc)
    // '⾦' (0x2fa6) <--> '金' (0x91d1)
    // '⾧' (0x2fa7) <--> '長' (0x9577)
    // '⾨' (0x2fa8) <--> '門' (0x9580)
    // '⾩' (0x2fa9) <--> '阜' (0x961c)
    // '⾪' (0x2faa) <--> '隶' (0x96b6)
    // '⾫' (0x2fab) <--> '隹' (0x96b9)
    // '⾬' (0x2fac) <--> '雨' (0x96e8)
    // '⾭' (0x2fad) <--> '靑' (0x9751)
    // '⾮' (0x2fae) <--> '非' (0x975e)
    // '⾯' (0x2faf) <--> '面' (0x9762)
    // '⾰' (0x2fb0) <--> '革' (0x9769)
    // '⾱' (0x2fb1) <--> '韋' (0x97cb)
    // '⾲' (0x2fb2) <--> '韭' (0x97ed)
    // '⾳' (0x2fb3) <--> '音' (0x97f3)
    // '⾴' (0x2fb4) <--> '頁' (0x9801)
    // '⾵' (0x2fb5) <--> '風' (0x98a8)
    // '⾶' (0x2fb6) <--> '飛' (0x98db)
    // '⾷' (0x2fb7) <--> '食' (0x98df)
    // '⾸' (0x2fb8) <--> '首' (0x9996)
    // '⾹' (0x2fb9) <--> '香' (0x9999)
    // '⾺' (0x2fba) <--> '馬' (0x99ac)
    // '⾻' (0x2fbb) <--> '骨' (0x9aa8)
    // '⾼' (0x2fbc) <--> '高' (0x9ad8)
    // '⾽' (0x2fbd) <--> '髟' (0x9adf)
    // '⾾' (0x2fbe) <--> '鬥' (0x9b25)
    // '⾿' (0x2fbf) <--> '鬯' (0x9b2f)
    // '⿀' (0x2fc0) <--> '鬲' (0x9b32)
    // '⿁' (0x2fc1) <--> '鬼' (0x9b3c)
    // '⿂' (0x2fc2) <--> '魚' (0x9b5a)
    // '⿃' (0x2fc3) <--> '鳥' (0x9ce5)
    // '⿄' (0x2fc4) <--> '鹵' (0x9e75)
    // '⿅' (0x2fc5) <--> '鹿' (0x9e7f)
    // '⿆' (0x2fc6) <--> '麥' (0x9ea5)
    // '⿇' (0x2fc7) <--> '麻' (0x9ebb)
    // '⿈' (0x2fc8) <--> '黃' (0x9ec3)
    // '⿉' (0x2fc9) <--> '黍' (0x9ecd)
    // '⿊' (0x2fca) <--> '黑' (0x9ed1)
    // '⿋' (0x2fcb) <--> '黹' (0x9ef9)
    // '⿌' (0x2fcc) <--> '黽' (0x9efd)
    // '⿍' (0x2fcd) <--> '鼎' (0x9f0e)
    // '⿎' (0x2fce) <--> '鼓' (0x9f13)
    // '⿏' (0x2fcf) <--> '鼠' (0x9f20)
    // '⿐' (0x2fd0) <--> '鼻' (0x9f3b)
    // '⿑' (0x2fd1) <--> '齊' (0x9f4a)
    // '⿒' (0x2fd2) <--> '齒' (0x9f52)
    // '⿓' (0x2fd3) <--> '龍' (0x9f8d)
    // '⿔' (0x2fd4) <--> '龜' (0x9f9c)
    // '⿕' (0x2fd5) <--> '龠' (0x9fa0)

    public static readonly IEnumerable<char> StrangeCharList = new char[] {
        (char)0x2e83 /* '⺃' */,
        (char)0x2e85 /* '⺅' */,
        (char)0x2e89 /* '⺉' */,
        (char)0x2e8a /* '⺊' */,
        (char)0x2e8b /* '⺋' */,
        (char)0x2e8f /* '⺏' */,
        (char)0x2e90 /* '⺐' */,
        (char)0x2e91 /* '⺑' */,
        (char)0x2e92 /* '⺒' */,
        (char)0x2e93 /* '⺓' */,
        (char)0x2e94 /* '⺔' */,
        (char)0x2e96 /* '⺖' */,
        (char)0x2e98 /* '⺘' */,
        (char)0x2e99 /* '⺙' */,
        (char)0x2e9b /* '⺛' */,
        (char)0x2e9e /* '⺞' */,
        (char)0x2e9f /* '⺟' */,
        (char)0x2ea0 /* '⺠' */,
        (char)0x2ea1 /* '⺡' */,
        (char)0x2ea2 /* '⺢' */,
        (char)0x2ea3 /* '⺣' */,
        (char)0x2ea4 /* '⺤' */,
        (char)0x2ea5 /* '⺥' */,
        (char)0x2ea6 /* '⺦' */,
        (char)0x2ea8 /* '⺨' */,
        (char)0x2ea9 /* '⺩' */,
        (char)0x2eaa /* '⺪' */,
        (char)0x2eab /* '⺫' */,
        (char)0x2eac /* '⺬' */,
        (char)0x2ead /* '⺭' */,
        (char)0x2eaf /* '⺯' */,
        (char)0x2eb0 /* '⺰' */,
        (char)0x2eb1 /* '⺱' */,
        (char)0x2eb2 /* '⺲' */,
        (char)0x2eb8 /* '⺸' */,
        (char)0x2eb9 /* '⺹' */,
        (char)0x2eba /* '⺺' */,
        (char)0x2ebd /* '⺽' */,
        (char)0x2ebe /* '⺾' */,
        (char)0x2ebf /* '⺿' */,
        (char)0x2ec0 /* '⻀' */,
        (char)0x2ec1 /* '⻁' */,
        (char)0x2ec2 /* '⻂' */,
        (char)0x2ec3 /* '⻃' */,
        (char)0x2ec4 /* '⻄' */,
        (char)0x2ec5 /* '⻅' */,
        (char)0x2ec8 /* '⻈' */,
        (char)0x2ec9 /* '⻉' */,
        (char)0x2ecb /* '⻋' */,
        (char)0x2ecd /* '⻍' */,
        (char)0x2ece /* '⻎' */,
        (char)0x2ed0 /* '⻐' */,
        (char)0x2ed1 /* '⻑' */,
        (char)0x2ed2 /* '⻒' */,
        (char)0x2ed3 /* '⻓' */,
        (char)0x2ed4 /* '⻔' */,
        (char)0x2ed6 /* '⻖' */,
        (char)0x2ed8 /* '⻘' */,
        (char)0x2ed9 /* '⻙' */,
        (char)0x2eda /* '⻚' */,
        (char)0x2edb /* '⻛' */,
        (char)0x2edc /* '⻜' */,
        (char)0x2edd /* '⻝' */,
        (char)0x2edf /* '⻟' */,
        (char)0x2ee0 /* '⻠' */,
        (char)0x2ee2 /* '⻢' */,
        (char)0x2ee3 /* '⻣' */,
        (char)0x2ee4 /* '⻤' */,
        (char)0x2ee5 /* '⻥' */,
        (char)0x2ee6 /* '⻦' */,
        (char)0x2ee7 /* '⻧' */,
        (char)0x2ee8 /* '⻨' */,
        (char)0x2ee9 /* '⻩' */,
        (char)0x2eea /* '⻪' */,
        (char)0x2eeb /* '⻫' */,
        (char)0x2eec /* '⻬' */,
        (char)0x2eed /* '⻭' */,
        (char)0x2eee /* '⻮' */,
        (char)0x2eef /* '⻯' */,
        (char)0x2ef0 /* '⻰' */,
        (char)0x2ef1 /* '⻱' */,
        (char)0x2ef2 /* '⻲' */,
        (char)0x2ef3 /* '⻳' */,
        (char)0x2f00 /* '⼀' */,
        (char)0x2f01 /* '⼁' */,
        (char)0x2f02 /* '⼂' */,
        (char)0x2f03 /* '⼃' */,
        (char)0x2f04 /* '⼄' */,
        (char)0x2f05 /* '⼅' */,
        (char)0x2f06 /* '⼆' */,
        (char)0x2f07 /* '⼇' */,
        (char)0x2f08 /* '⼈' */,
        (char)0x2f09 /* '⼉' */,
        (char)0x2f0a /* '⼊' */,
        (char)0x2f0b /* '⼋' */,
        (char)0x2f0c /* '⼌' */,
        (char)0x2f0d /* '⼍' */,
        (char)0x2f0e /* '⼎' */,
        (char)0x2f0f /* '⼏' */,
        (char)0x2f10 /* '⼐' */,
        (char)0x2f11 /* '⼑' */,
        (char)0x2f12 /* '⼒' */,
        (char)0x2f13 /* '⼓' */,
        (char)0x2f14 /* '⼔' */,
        (char)0x2f15 /* '⼕' */,
        (char)0x2f16 /* '⼖' */,
        (char)0x2f17 /* '⼗' */,
        (char)0x2f18 /* '⼘' */,
        (char)0x2f19 /* '⼙' */,
        (char)0x2f1a /* '⼚' */,
        (char)0x2f1b /* '⼛' */,
        (char)0x2f1c /* '⼜' */,
        (char)0x2f1d /* '⼝' */,
        (char)0x2f1e /* '⼞' */,
        (char)0x2f1f /* '⼟' */,
        (char)0x2f20 /* '⼠' */,
        (char)0x2f21 /* '⼡' */,
        (char)0x2f22 /* '⼢' */,
        (char)0x2f23 /* '⼣' */,
        (char)0x2f24 /* '⼤' */,
        (char)0x2f25 /* '⼥' */,
        (char)0x2f26 /* '⼦' */,
        (char)0x2f27 /* '⼧' */,
        (char)0x2f28 /* '⼨' */,
        (char)0x2f29 /* '⼩' */,
        (char)0x2f2a /* '⼪' */,
        (char)0x2f2b /* '⼫' */,
        (char)0x2f2c /* '⼬' */,
        (char)0x2f2d /* '⼭' */,
        (char)0x2f2e /* '⼮' */,
        (char)0x2f2f /* '⼯' */,
        (char)0x2f30 /* '⼰' */,
        (char)0x2f31 /* '⼱' */,
        (char)0x2f32 /* '⼲' */,
        (char)0x2f33 /* '⼳' */,
        (char)0x2f34 /* '⼴' */,
        (char)0x2f35 /* '⼵' */,
        (char)0x2f36 /* '⼶' */,
        (char)0x2f37 /* '⼷' */,
        (char)0x2f38 /* '⼸' */,
        (char)0x2f39 /* '⼹' */,
        (char)0x2f3a /* '⼺' */,
        (char)0x2f3b /* '⼻' */,
        (char)0x2f3c /* '⼼' */,
        (char)0x2f3d /* '⼽' */,
        (char)0x2f3e /* '⼾' */,
        (char)0x2f3f /* '⼿' */,
        (char)0x2f40 /* '⽀' */,
        (char)0x2f41 /* '⽁' */,
        (char)0x2f42 /* '⽂' */,
        (char)0x2f43 /* '⽃' */,
        (char)0x2f44 /* '⽄' */,
        (char)0x2f45 /* '⽅' */,
        (char)0x2f46 /* '⽆' */,
        (char)0x2f47 /* '⽇' */,
        (char)0x2f48 /* '⽈' */,
        (char)0x2f49 /* '⽉' */,
        (char)0x2f4a /* '⽊' */,
        (char)0x2f4b /* '⽋' */,
        (char)0x2f4c /* '⽌' */,
        (char)0x2f4d /* '⽍' */,
        (char)0x2f4e /* '⽎' */,
        (char)0x2f4f /* '⽏' */,
        (char)0x2f50 /* '⽐' */,
        (char)0x2f51 /* '⽑' */,
        (char)0x2f52 /* '⽒' */,
        (char)0x2f53 /* '⽓' */,
        (char)0x2f54 /* '⽔' */,
        (char)0x2f55 /* '⽕' */,
        (char)0x2f56 /* '⽖' */,
        (char)0x2f57 /* '⽗' */,
        (char)0x2f58 /* '⽘' */,
        (char)0x2f59 /* '⽙' */,
        (char)0x2f5a /* '⽚' */,
        (char)0x2f5b /* '⽛' */,
        (char)0x2f5c /* '⽜' */,
        (char)0x2f5d /* '⽝' */,
        (char)0x2f5e /* '⽞' */,
        (char)0x2f5f /* '⽟' */,
        (char)0x2f60 /* '⽠' */,
        (char)0x2f61 /* '⽡' */,
        (char)0x2f62 /* '⽢' */,
        (char)0x2f63 /* '⽣' */,
        (char)0x2f64 /* '⽤' */,
        (char)0x2f65 /* '⽥' */,
        (char)0x2f66 /* '⽦' */,
        (char)0x2f67 /* '⽧' */,
        (char)0x2f68 /* '⽨' */,
        (char)0x2f69 /* '⽩' */,
        (char)0x2f6a /* '⽪' */,
        (char)0x2f6b /* '⽫' */,
        (char)0x2f6c /* '⽬' */,
        (char)0x2f6d /* '⽭' */,
        (char)0x2f6e /* '⽮' */,
        (char)0x2f6f /* '⽯' */,
        (char)0x2f70 /* '⽰' */,
        (char)0x2f71 /* '⽱' */,
        (char)0x2f72 /* '⽲' */,
        (char)0x2f73 /* '⽳' */,
        (char)0x2f74 /* '⽴' */,
        (char)0x2f75 /* '⽵' */,
        (char)0x2f76 /* '⽶' */,
        (char)0x2f77 /* '⽷' */,
        (char)0x2f78 /* '⽸' */,
        (char)0x2f79 /* '⽹' */,
        (char)0x2f7a /* '⽺' */,
        (char)0x2f7b /* '⽻' */,
        (char)0x2f7c /* '⽼' */,
        (char)0x2f7d /* '⽽' */,
        (char)0x2f7e /* '⽾' */,
        (char)0x2f7f /* '⽿' */,
        (char)0x2f80 /* '⾀' */,
        (char)0x2f81 /* '⾁' */,
        (char)0x2f82 /* '⾂' */,
        (char)0x2f83 /* '⾃' */,
        (char)0x2f84 /* '⾄' */,
        (char)0x2f85 /* '⾅' */,
        (char)0x2f86 /* '⾆' */,
        (char)0x2f87 /* '⾇' */,
        (char)0x2f88 /* '⾈' */,
        (char)0x2f89 /* '⾉' */,
        (char)0x2f8a /* '⾊' */,
        (char)0x2f8b /* '⾋' */,
        (char)0x2f8c /* '⾌' */,
        (char)0x2f8d /* '⾍' */,
        (char)0x2f8e /* '⾎' */,
        (char)0x2f8f /* '⾏' */,
        (char)0x2f90 /* '⾐' */,
        (char)0x2f91 /* '⾑' */,
        (char)0x2f92 /* '⾒' */,
        (char)0x2f93 /* '⾓' */,
        (char)0x2f94 /* '⾔' */,
        (char)0x2f95 /* '⾕' */,
        (char)0x2f96 /* '⾖' */,
        (char)0x2f97 /* '⾗' */,
        (char)0x2f98 /* '⾘' */,
        (char)0x2f99 /* '⾙' */,
        (char)0x2f9a /* '⾚' */,
        (char)0x2f9b /* '⾛' */,
        (char)0x2f9c /* '⾜' */,
        (char)0x2f9d /* '⾝' */,
        (char)0x2f9e /* '⾞' */,
        (char)0x2f9f /* '⾟' */,
        (char)0x2fa0 /* '⾠' */,
        (char)0x2fa1 /* '⾡' */,
        (char)0x2fa2 /* '⾢' */,
        (char)0x2fa3 /* '⾣' */,
        (char)0x2fa4 /* '⾤' */,
        (char)0x2fa5 /* '⾥' */,
        (char)0x2fa6 /* '⾦' */,
        (char)0x2fa7 /* '⾧' */,
        (char)0x2fa8 /* '⾨' */,
        (char)0x2fa9 /* '⾩' */,
        (char)0x2faa /* '⾪' */,
        (char)0x2fab /* '⾫' */,
        (char)0x2fac /* '⾬' */,
        (char)0x2fad /* '⾭' */,
        (char)0x2fae /* '⾮' */,
        (char)0x2faf /* '⾯' */,
        (char)0x2fb0 /* '⾰' */,
        (char)0x2fb1 /* '⾱' */,
        (char)0x2fb2 /* '⾲' */,
        (char)0x2fb3 /* '⾳' */,
        (char)0x2fb4 /* '⾴' */,
        (char)0x2fb5 /* '⾵' */,
        (char)0x2fb6 /* '⾶' */,
        (char)0x2fb7 /* '⾷' */,
        (char)0x2fb8 /* '⾸' */,
        (char)0x2fb9 /* '⾹' */,
        (char)0x2fba /* '⾺' */,
        (char)0x2fbb /* '⾻' */,
        (char)0x2fbc /* '⾼' */,
        (char)0x2fbd /* '⾽' */,
        (char)0x2fbe /* '⾾' */,
        (char)0x2fbf /* '⾿' */,
        (char)0x2fc0 /* '⿀' */,
        (char)0x2fc1 /* '⿁' */,
        (char)0x2fc2 /* '⿂' */,
        (char)0x2fc3 /* '⿃' */,
        (char)0x2fc4 /* '⿄' */,
        (char)0x2fc5 /* '⿅' */,
        (char)0x2fc6 /* '⿆' */,
        (char)0x2fc7 /* '⿇' */,
        (char)0x2fc8 /* '⿈' */,
        (char)0x2fc9 /* '⿉' */,
        (char)0x2fca /* '⿊' */,
        (char)0x2fcb /* '⿋' */,
        (char)0x2fcc /* '⿌' */,
        (char)0x2fcd /* '⿍' */,
        (char)0x2fce /* '⿎' */,
        (char)0x2fcf /* '⿏' */,
        (char)0x2fd0 /* '⿐' */,
        (char)0x2fd1 /* '⿑' */,
        (char)0x2fd2 /* '⿒' */,
        (char)0x2fd3 /* '⿓' */,
        (char)0x2fd4 /* '⿔' */,
        (char)0x2fd5 /* '⿕' */,
        };

    public static readonly IEnumerable<char> StrangeCharList2 = new char[] {
        (char)0xF06C /* 箇条書きの中黒点 (大) */,
        (char)0xF09F /* 箇条書きの中黒点 (小) */,
        (char)0x2022, // 中黒 (大)
        (char)0x00B7, // 中黒 (小)
        (char)0x0387, // 中黒 (小)
        (char)0x2219, // 中黒 (小)
        (char)0x22C5, // 中黒 (小)
        (char)0x30FB, // 中黒 (小)
        (char)0xFF65, // 中黒 (小)
        (char)0xF06E, // ■
        (char)0xF0B2, // □
        (char)0xF0FC, // ✓
        (char)0xF0D8, // ➢
        (char)0xf075, // ◆
        (char)0x00A5, // 円記号 (¥)
        };


    public static readonly IEnumerable<char> NormalCharList = new char[] {
        (char)0x4e5a /* '乚' */,
        (char)0x4ebb /* '亻' */,
        (char)0x5202 /* '刂' */,
        (char)0x535c /* '卜' */,
        (char)0x353e /* '㔾' */,
        (char)0x5c23 /* '尣' */,
        (char)0x5c22 /* '尢' */,
        (char)0x5c23 /* '尣' */,
        (char)0x5df3 /* '巳' */,
        (char)0x5e7a /* '幺' */,
        (char)0x5f51 /* '彑' */,
        (char)0x5fc4 /* '忄' */,
        (char)0x624c /* '扌' */,
        (char)0x6535 /* '攵' */,
        (char)0x65e1 /* '旡' */,
        (char)0x6b7a /* '歺' */,
        (char)0x6bcd /* '母' */,
        (char)0x6c11 /* '民' */,
        (char)0x6c35 /* '氵' */,
        (char)0x6c3a /* '氺' */,
        (char)0x706c /* '灬' */,
        (char)0x722b /* '爫' */,
        (char)0x722b /* '爫' */,
        (char)0x4e2c /* '丬' */,
        (char)0x72ad /* '犭' */,
        (char)0x738b /* '王' */,
        (char)0x758b /* '疋' */,
        (char)0x76ee /* '目' */,
        (char)0x793a /* '示' */,
        (char)0x793b /* '礻' */,
        (char)0x7cf9 /* '糹' */,
        (char)0x7e9f /* '纟' */,
        (char)0x7f53 /* '罓' */,
        (char)0x7f52 /* '罒' */,
        (char)0x7f8b /* '羋' */,
        (char)0x8002 /* '耂' */,
        (char)0x8080 /* '肀' */,
        (char)0x81fc /* '臼' */,
        (char)0x8279 /* '艹' */,
        (char)0x8279 /* '艹' */,
        (char)0x8279 /* '艹' */,
        (char)0x864e /* '虎' */,
        (char)0x8864 /* '衤' */,
        (char)0x8980 /* '覀' */,
        (char)0x897f /* '西' */,
        (char)0x89c1 /* '见' */,
        (char)0x8ba0 /* '讠' */,
        (char)0x8d1d /* '贝' */,
        (char)0x8f66 /* '车' */,
        (char)0x8fb6 /* '辶' */,
        (char)0x8fb6 /* '辶' */,
        (char)0x9485 /* '钅' */,
        (char)0x9577 /* '長' */,
        (char)0x9578 /* '镸' */,
        (char)0x957f /* '长' */,
        (char)0x95e8 /* '门' */,
        (char)0x961d /* '阝' */,
        (char)0x9752 /* '青' */,
        (char)0x97e6 /* '韦' */,
        (char)0x9875 /* '页' */,
        (char)0x98ce /* '风' */,
        (char)0x98de /* '飞' */,
        (char)0x98df /* '食' */,
        (char)0x98e0 /* '飠' */,
        (char)0x9963 /* '饣' */,
        (char)0x9a6c /* '马' */,
        (char)0x9aa8 /* '骨' */,
        (char)0x9b3c /* '鬼' */,
        (char)0x9c7c /* '鱼' */,
        (char)0x9e1f /* '鸟' */,
        (char)0x5364 /* '卤' */,
        (char)0x9ea6 /* '麦' */,
        (char)0x9ec4 /* '黄' */,
        (char)0x9efe /* '黾' */,
        (char)0x6589 /* '斉' */,
        (char)0x9f50 /* '齐' */,
        (char)0x6b6f /* '歯' */,
        (char)0x9f7f /* '齿' */,
        (char)0x7adc /* '竜' */,
        (char)0x9f99 /* '龙' */,
        (char)0x9f9c /* '龜' */,
        (char)0x4e80 /* '亀' */,
        (char)0x9f9f /* '龟' */,
        (char)0x4e00 /* '一' */,
        (char)0x4e28 /* '丨' */,
        (char)0x4e36 /* '丶' */,
        (char)0x4e3f /* '丿' */,
        (char)0x4e59 /* '乙' */,
        (char)0x4e85 /* '亅' */,
        (char)0x4e8c /* '二' */,
        (char)0x4ea0 /* '亠' */,
        (char)0x4eba /* '人' */,
        (char)0x513f /* '儿' */,
        (char)0x5165 /* '入' */,
        (char)0x516b /* '八' */,
        (char)0x5182 /* '冂' */,
        (char)0x5196 /* '冖' */,
        (char)0x51ab /* '冫' */,
        (char)0x51e0 /* '几' */,
        (char)0x51f5 /* '凵' */,
        (char)0x5200 /* '刀' */,
        (char)0x529b /* '力' */,
        (char)0x52f9 /* '勹' */,
        (char)0x5315 /* '匕' */,
        (char)0x531a /* '匚' */,
        (char)0x5338 /* '匸' */,
        (char)0x5341 /* '十' */,
        (char)0x535c /* '卜' */,
        (char)0x5369 /* '卩' */,
        (char)0x5382 /* '厂' */,
        (char)0x53b6 /* '厶' */,
        (char)0x53c8 /* '又' */,
        (char)0x53e3 /* '口' */,
        (char)0x56d7 /* '囗' */,
        (char)0x571f /* '土' */,
        (char)0x58eb /* '士' */,
        (char)0x5902 /* '夂' */,
        (char)0x590a /* '夊' */,
        (char)0x5915 /* '夕' */,
        (char)0x5927 /* '大' */,
        (char)0x5973 /* '女' */,
        (char)0x5b50 /* '子' */,
        (char)0x5b80 /* '宀' */,
        (char)0x5bf8 /* '寸' */,
        (char)0x5c0f /* '小' */,
        (char)0x5c22 /* '尢' */,
        (char)0x5c38 /* '尸' */,
        (char)0x5c6e /* '屮' */,
        (char)0x5c71 /* '山' */,
        (char)0x5ddb /* '巛' */,
        (char)0x5de5 /* '工' */,
        (char)0x5df1 /* '己' */,
        (char)0x5dfe /* '巾' */,
        (char)0x5e72 /* '干' */,
        (char)0x5e7a /* '幺' */,
        (char)0x5e7f /* '广' */,
        (char)0x5ef4 /* '廴' */,
        (char)0x5efe /* '廾' */,
        (char)0x5f0b /* '弋' */,
        (char)0x5f13 /* '弓' */,
        (char)0x5f50 /* '彐' */,
        (char)0x5f61 /* '彡' */,
        (char)0x5f73 /* '彳' */,
        (char)0x5fc3 /* '心' */,
        (char)0x6208 /* '戈' */,
        (char)0x6236 /* '戶' */,
        (char)0x624b /* '手' */,
        (char)0x652f /* '支' */,
        (char)0x6534 /* '攴' */,
        (char)0x6587 /* '文' */,
        (char)0x6597 /* '斗' */,
        (char)0x65a4 /* '斤' */,
        (char)0x65b9 /* '方' */,
        (char)0x65e0 /* '无' */,
        (char)0x65e5 /* '日' */,
        (char)0x66f0 /* '曰' */,
        (char)0x6708 /* '月' */,
        (char)0x6728 /* '木' */,
        (char)0x6b20 /* '欠' */,
        (char)0x6b62 /* '止' */,
        (char)0x6b79 /* '歹' */,
        (char)0x6bb3 /* '殳' */,
        (char)0x6bcb /* '毋' */,
        (char)0x6bd4 /* '比' */,
        (char)0x6bdb /* '毛' */,
        (char)0x6c0f /* '氏' */,
        (char)0x6c14 /* '气' */,
        (char)0x6c34 /* '水' */,
        (char)0x706b /* '火' */,
        (char)0x722a /* '爪' */,
        (char)0x7236 /* '父' */,
        (char)0x723b /* '爻' */,
        (char)0x723f /* '爿' */,
        (char)0x7247 /* '片' */,
        (char)0x7259 /* '牙' */,
        (char)0x725b /* '牛' */,
        (char)0x72ac /* '犬' */,
        (char)0x7384 /* '玄' */,
        (char)0x7389 /* '玉' */,
        (char)0x74dc /* '瓜' */,
        (char)0x74e6 /* '瓦' */,
        (char)0x7518 /* '甘' */,
        (char)0x751f /* '生' */,
        (char)0x7528 /* '用' */,
        (char)0x7530 /* '田' */,
        (char)0x758b /* '疋' */,
        (char)0x7592 /* '疒' */,
        (char)0x7676 /* '癶' */,
        (char)0x767d /* '白' */,
        (char)0x76ae /* '皮' */,
        (char)0x76bf /* '皿' */,
        (char)0x76ee /* '目' */,
        (char)0x77db /* '矛' */,
        (char)0x77e2 /* '矢' */,
        (char)0x77f3 /* '石' */,
        (char)0x793a /* '示' */,
        (char)0x79b8 /* '禸' */,
        (char)0x79be /* '禾' */,
        (char)0x7a74 /* '穴' */,
        (char)0x7acb /* '立' */,
        (char)0x7af9 /* '竹' */,
        (char)0x7c73 /* '米' */,
        (char)0x7cf8 /* '糸' */,
        (char)0x7f36 /* '缶' */,
        (char)0x7f51 /* '网' */,
        (char)0x7f8a /* '羊' */,
        (char)0x7fbd /* '羽' */,
        (char)0x8001 /* '老' */,
        (char)0x800c /* '而' */,
        (char)0x8012 /* '耒' */,
        (char)0x8033 /* '耳' */,
        (char)0x807f /* '聿' */,
        (char)0x8089 /* '肉' */,
        (char)0x81e3 /* '臣' */,
        (char)0x81ea /* '自' */,
        (char)0x81f3 /* '至' */,
        (char)0x81fc /* '臼' */,
        (char)0x820c /* '舌' */,
        (char)0x821b /* '舛' */,
        (char)0x821f /* '舟' */,
        (char)0x826e /* '艮' */,
        (char)0x8272 /* '色' */,
        (char)0x8278 /* '艸' */,
        (char)0x864d /* '虍' */,
        (char)0x866b /* '虫' */,
        (char)0x8840 /* '血' */,
        (char)0x884c /* '行' */,
        (char)0x8863 /* '衣' */,
        (char)0x897e /* '襾' */,
        (char)0x898b /* '見' */,
        (char)0x89d2 /* '角' */,
        (char)0x8a00 /* '言' */,
        (char)0x8c37 /* '谷' */,
        (char)0x8c46 /* '豆' */,
        (char)0x8c55 /* '豕' */,
        (char)0x8c78 /* '豸' */,
        (char)0x8c9d /* '貝' */,
        (char)0x8d64 /* '赤' */,
        (char)0x8d70 /* '走' */,
        (char)0x8db3 /* '足' */,
        (char)0x8eab /* '身' */,
        (char)0x8eca /* '車' */,
        (char)0x8f9b /* '辛' */,
        (char)0x8fb0 /* '辰' */,
        (char)0x8fb5 /* '辵' */,
        (char)0x9091 /* '邑' */,
        (char)0x9149 /* '酉' */,
        (char)0x91c6 /* '釆' */,
        (char)0x91cc /* '里' */,
        (char)0x91d1 /* '金' */,
        (char)0x9577 /* '長' */,
        (char)0x9580 /* '門' */,
        (char)0x961c /* '阜' */,
        (char)0x96b6 /* '隶' */,
        (char)0x96b9 /* '隹' */,
        (char)0x96e8 /* '雨' */,
        (char)0x9751 /* '靑' */,
        (char)0x975e /* '非' */,
        (char)0x9762 /* '面' */,
        (char)0x9769 /* '革' */,
        (char)0x97cb /* '韋' */,
        (char)0x97ed /* '韭' */,
        (char)0x97f3 /* '音' */,
        (char)0x9801 /* '頁' */,
        (char)0x98a8 /* '風' */,
        (char)0x98db /* '飛' */,
        (char)0x98df /* '食' */,
        (char)0x9996 /* '首' */,
        (char)0x9999 /* '香' */,
        (char)0x99ac /* '馬' */,
        (char)0x9aa8 /* '骨' */,
        (char)0x9ad8 /* '高' */,
        (char)0x9adf /* '髟' */,
        (char)0x9b25 /* '鬥' */,
        (char)0x9b2f /* '鬯' */,
        (char)0x9b32 /* '鬲' */,
        (char)0x9b3c /* '鬼' */,
        (char)0x9b5a /* '魚' */,
        (char)0x9ce5 /* '鳥' */,
        (char)0x9e75 /* '鹵' */,
        (char)0x9e7f /* '鹿' */,
        (char)0x9ea5 /* '麥' */,
        (char)0x9ebb /* '麻' */,
        (char)0x9ec3 /* '黃' */,
        (char)0x9ecd /* '黍' */,
        (char)0x9ed1 /* '黑' */,
        (char)0x9ef9 /* '黹' */,
        (char)0x9efd /* '黽' */,
        (char)0x9f0e /* '鼎' */,
        (char)0x9f13 /* '鼓' */,
        (char)0x9f20 /* '鼠' */,
        (char)0x9f3b /* '鼻' */,
        (char)0x9f4a /* '齊' */,
        (char)0x9f52 /* '齒' */,
        (char)0x9f8d /* '龍' */,
        (char)0x9f9c /* '龜' */,
        (char)0x9fa0 /* '龠' */,
        };

    public static readonly IEnumerable<char> NormalCharList2 = new char[] {
        '●' /* 箇条書きの中黒点 (大) */,
        '・' /* 箇条書きの中黒点 (小) */,
        '・', // 中黒 (小)
        '・', // 中黒 (小)
        '・', // 中黒 (小)
        '・', // 中黒 (小)
        '・', // 中黒 (小)
        '・', // 中黒 (小)
        '・', // 中黒 (小)
        '■', // ■
        '□', // □
        '✓', // ✓
        '➢', // ➢
        '◆', // ◆
        '\\', // バックスラッシュ (円記号は必ずバックスラッシュに)
    };

    public static readonly string StrangeCharArrayStr;
    public static readonly string NormalCharArrayStr;

    public static readonly string StrangeCharArrayStr2;
    public static readonly string NormalCharArrayStr2;

    static UnicodeStdKangxiMapUtil()
    {
        StrangeCharArrayStr = new string(StrangeCharList.ToArray());
        NormalCharArrayStr = new string(NormalCharList.ToArray());

        StrangeCharArrayStr2 = new string(StrangeCharList2.ToArray());
        NormalCharArrayStr2 = new string(NormalCharList2.ToArray());
    }

    public static char StrangeToNormal(char c)
    {
        int i = StrangeCharArrayStr.IndexOf(c);
        if (i != -1)
        {
            return NormalCharArrayStr[i];
        }
        i = StrangeCharArrayStr2.IndexOf(c);
        if (i != -1)
        {
            return NormalCharArrayStr2[i];
        }
        return c;
    }

    public static string StrangeToNormal(string str)
    {
        char[] a = str.ToCharArray();
        int i, len;
        len = a.Length;
        for (i = 0; i < len; i++)
        {
            a[i] = StrangeToNormal(a[i]);
        }
        return new string(a);
    }

    public static char NormalToStrange(char c)
    {
        int i = NormalCharArrayStr.IndexOf(c);
        if (i == -1) return StrangeCharArrayStr[i];
        return c;
    }

    public static string NormalToStrange(string str)
    {
        char[] a = str.ToCharArray();
        int i, len;
        len = a.Length;
        for (i = 0; i < len; i++)
        {
            a[i] = NormalToStrange(a[i]);
        }
        return new string(a);
    }
}

// Unicode の制御文字置換ユーティティ
public static class UnicodeControlCodesNormalizeUtil
{
    // 普通のスペース ' ' と見た目は同じだが、文字コードが異なる異字の配列
    public static readonly IEnumerable<char> Strange_Space_CharList = new char[]
    {
            (char)0x00A0 /* NO-BREAK SPACE (改行を許さない空白) */,
            (char)0x1680 /* OGHAM SPACE MARK (オガム文字用の固定幅空白) */,
            (char)0x180E /* MONGOLIAN VOWEL SEPARATOR (モンゴル語の母音区切り、幅ゼロ空白) */,
            (char)0x2000 /* EN QUAD (活字の 1/2 em 幅空白) */,
            (char)0x2001 /* EM QUAD (活字の 1 em 幅空白) */,
            (char)0x2002 /* EN SPACE (en 幅空白) */,
            (char)0x2003 /* EM SPACE (em 幅空白) */,
            (char)0x2004 /* THREE-PER-EM SPACE (全角の 1/3 幅空白) */,
            (char)0x2005 /* FOUR-PER-EM SPACE (全角の 1/4 幅空白) */,
            (char)0x2006 /* SIX-PER-EM SPACE (全角の 1/6 幅空白) */,
            (char)0x2007 /* FIGURE SPACE (等幅数字用空白) */,
            (char)0x2008 /* PUNCTUATION SPACE (句読点幅空白) */,
            (char)0x2009 /* THIN SPACE (細い空白) */,
            (char)0x200A /* HAIR SPACE (極細の空白) */,
            (char)0x202F /* NARROW NO-BREAK SPACE (狭い改行禁止空白) */,
            (char)0x205F /* MEDIUM MATHEMATICAL SPACE (数式用中幅空白) */,
            (char)0x3164 /* HANGUL FILLER (ハングル用空白記号) */
    };

    // 普通の改行 '\n' と見た目は同じだが、文字コードが異なる異字の配列
    public static readonly IEnumerable<char> Strange_NewLine_CharList = new char[]
    {
            (char)0x000B /* LINE TABULATION (VT) (垂直タブ — 縦方向改行) */,
            (char)0x000C /* FORM FEED (FF) (改ページ制御コード) */,
            (char)0x0085 /* NEXT LINE (NEL) (Unicode の次行制御) */,
            (char)0x2028 /* LINE SEPARATOR (行区切り用改行) */,
            (char)0x2029 /* PARAGRAPH SEPARATOR (段落区切り用改行) */
    };

    // 見た目は全く何も表示されないが、文字コードとして 1 文字を消費する制御コード
    public static readonly IEnumerable<char> Strange_HiddenControl_CharList = new char[]
    {
            (char)0x00AD /* SOFT HYPHEN (SHY) (改行時のみ表示されるソフトハイフン) */,
            (char)0x200B /* ZERO WIDTH SPACE (幅ゼロの空白) */,
            (char)0x200C /* ZERO WIDTH NON-JOINER (ZWNJ) (合字を阻止する幅ゼロ制御) */,
            (char)0x200D /* ZERO WIDTH JOINER (ZWJ) (合字を強制する幅ゼロ制御) */,
            (char)0x200E /* LEFT-TO-RIGHT MARK (LRM) (左→右方向指定の幅ゼロ) */,
            (char)0x200F /* RIGHT-TO-LEFT MARK (RLM) (右→左方向指定の幅ゼロ) */,
            (char)0x202A /* LEFT-TO-RIGHT EMBEDDING (LRE) (左→右埋め込み開始制御) */,
            (char)0x202B /* RIGHT-TO-LEFT EMBEDDING (RLE) (右→左埋め込み開始制御) */,
            (char)0x202C /* POP DIRECTIONAL FORMATTING (PDF) (埋め込み／上書き終了制御) */,
            (char)0x202D /* LEFT-TO-RIGHT OVERRIDE (LRO) (左→右上書き開始制御) */,
            (char)0x202E /* RIGHT-TO-LEFT OVERRIDE (RLO) (右→左上書き開始制御) */,
            (char)0x2060 /* WORD JOINER (単語分割禁止の幅ゼロ制御) */,
            (char)0x2061 /* FUNCTION APPLICATION (数学関数適用 — 不可視) */,
            (char)0x2062 /* INVISIBLE TIMES (不可視の掛け算記号) */,
            (char)0x2063 /* INVISIBLE SEPARATOR (不可視の区切り記号) */,
            (char)0x2064 /* INVISIBLE PLUS (不可視の足し算記号) */,
            (char)0x2066 /* LEFT-TO-RIGHT ISOLATE (LRI) (左→右アイソレート開始) */,
            (char)0x2067 /* RIGHT-TO-LEFT ISOLATE (RLI) (右→左アイソレート開始) */,
            (char)0x2068 /* FIRST STRONG ISOLATE (FSI) (最初に強い方向のアイソレート開始) */,
            (char)0x2069 /* POP DIRECTIONAL ISOLATE (PDI) (アイソレート終了制御) */,
            (char)0xFEFF /* ZERO WIDTH NO-BREAK SPACE (BOM) (BOM としても用いられる幅ゼロ改行禁止空白) */,
            (char)0xFFF9 /* INTERLINEAR ANNOTATION ANCHOR (行間注記用アンカー) */,
            (char)0xFFFA /* INTERLINEAR ANNOTATION SEPARATOR (行間注記用セパレータ) */,
            (char)0xFFFB /* INTERLINEAR ANNOTATION TERMINATOR (行間注記用終端) */
    };

    // 見た目はハイフンに見えるが、ASCII コード '-' (U+002D) とは異なる異字の配列
    public static readonly IEnumerable<char> Strange_Hyphen_CharList = new char[]
    {
        (char)0x2010 /* HYPHEN (改行可能なハイフン) */,
        (char)0x2011 /* NON‑BREAKING HYPHEN (改行を許さないハイフン) */,
        (char)0x058A /* ARMENIAN HYPHEN (アルメニア語用ハイフン) */,
        (char)0x1400 /* CANADIAN SYLLABICS HYPHEN (カナダ先住民音節文字用ハイフン) */,
        (char)0x1806 /* MONGOLIAN TODO SOFT HYPHEN (モンゴル語トド文字のソフトハイフン) */,
        (char)0x00AD /* SOFT HYPHEN (表示されない可能性のあるソフトハイフン) */,
        (char)0x2012 /* FIGURE DASH (数字列用ダッシュ) */,
        (char)0x2013 /* EN DASH (範囲を示すエンダッシュ) */,
        (char)0x2014 /* EM DASH (文章挿入用エムダッシュ) */,
        (char)0x2015 /* HORIZONTAL BAR (水平バー、日本語組版で使用) */,
        (char)0x2E3A /* TWO‑EM DASH (二倍エムダッシュ、省略線) */,
        (char)0x2E3B /* THREE‑EM DASH (三倍エムダッシュ、省略線) */,
        (char)0x2212 /* MINUS SIGN (数式用マイナス記号) */,
        (char)0x2796 /* HEAVY MINUS SIGN (太字マイナス、装飾用) */,
        (char)0xFF0D /* FULLWIDTH HYPHEN‑MINUS (全角ハイフンマイナス) */,
        (char)0xFE63 /* SMALL HYPHEN‑MINUS (小型ハイフンマイナス) */,
        (char)0xFE58 /* SMALL EM DASH (小型エムダッシュ) */,
        (char)0xFE31 /* PRESENTATION FORM FOR VERTICAL EM DASH (縦書き用エムダッシュ) */,
        (char)0xFE32 /* PRESENTATION FORM FOR VERTICAL EN DASH (縦書き用エンダッシュ) */,
        (char)0x2043 /* HYPHEN BULLET (箇条書き用ハイフン) */,
        (char)0x2053 /* SWUNG DASH (波状ダッシュ、文章の省略に使用) */,
        (char)0x30A0 /* KATAKANA‑HIRAGANA DOUBLE HYPHEN (カタカナ・ひらがなダブルハイフン) */
    };

    // 見た目はパイプ '|' に見えるが、ASCII コード '|' (U+007C) とは異なる異字の配列
    public static readonly IEnumerable<char> Strange_Pipe_CharList = new char[]
    {
        (char)0x01C0 /* LATIN LETTER DENTAL CLICK (縦線に似たクリック音文字) */,
        (char)0x05C0 /* HEBREW PUNCTUATION PASEQ (ヘブライ語の区切り記号) */,
        (char)0x2223 /* DIVIDES (数学の整除記号) */,
        (char)0x23D0 /* VERTICAL LINE EXTENSION (縦線延長記号) */,
        (char)0x2758 /* LIGHT VERTICAL BAR (細い縦線) */,
        (char)0x2759 /* MEDIUM VERTICAL BAR (中太縦線) */,
        (char)0x275A /* HEAVY VERTICAL BAR (太い縦線) */,
        (char)0xFF5C /* FULLWIDTH VERTICAL LINE (全角縦線) */,
        (char)0xFE31 /* PRESENTATION FORM FOR VERTICAL EM DASH (縦書き用エムダッシュ) */,
        (char)0xFE32 /* PRESENTATION FORM FOR VERTICAL EN DASH (縦書き用エンダッシュ) */
    };

    // 見た目はプラス '+' に見えるが、ASCII コード '+' (U+002B) とは異なる異字の配列
    public static readonly IEnumerable<char> Strange_Plus_CharList = new char[]
    {
        (char)0xFE62 /* SMALL PLUS SIGN (小型プラス) */,
        (char)0xFF0B /* FULLWIDTH PLUS SIGN (全角プラス) */,
        (char)0x2795 /* HEAVY PLUS SIGN (太線プラス) */,
        (char)0x2295 /* CIRCLED PLUS (丸囲みプラス) */,
        (char)0x229E /* SQUARED PLUS (四角囲みプラス) */
    };

    // 見た目はスラッシュ '/' に見えるが、ASCII コード '/' (U+002F) とは異なる異字の配列
    public static readonly IEnumerable<char> Strange_Slash_CharList = new char[]
    {
        (char)0x2044 /* FRACTION SLASH (分数用スラッシュ) */,
        (char)0x2215 /* DIVISION SLASH (除算用スラッシュ) */,
        (char)0x2571 /* BOX DRAWINGS LIGHT DIAGONAL UPPER RIGHT TO LOWER LEFT (罫線用細斜線) */,
        (char)0x29F8 /* BIG SOLIDUS (大型スラッシュ) */,
        (char)0xFE68 /* SMALL SOLIDUS (小型スラッシュ) */,
        (char)0xFF0F /* FULLWIDTH SOLIDUS (全角スラッシュ) */
    };

    // 見た目はアスタリスク '*' に見えるが、ASCII コード '*' (U+002A) とは異なる異字の配列
    public static readonly IEnumerable<char> Strange_Asterisk_CharList = new char[]
    {
        (char)0x204E /* LOW ASTERISK (低位置アスタリスク) */,
        (char)0x2217 /* ASTERISK OPERATOR (数学用アスタリスク演算子) */,
        (char)0x2731 /* HEAVY ASTERISK (太線アスタリスク) */,
        (char)0xFE61 /* SMALL ASTERISK (小型アスタリスク) */,
        (char)0xFF0A /* FULLWIDTH ASTERISK (全角アスタリスク) */
    };

    public static readonly string Strange_Space_CharsStr;
    public static readonly string Strange_NewLine_CharsStr;
    public static readonly string Strange_HiddenControl_CharsStr;
    public static readonly string Strange_Hyphon_CharsStr;
    public static readonly string Strange_Pipe_CharsStr;
    public static readonly string Strange_Plus_CharsStr;
    public static readonly string Strange_Slash_CharsStr;
    public static readonly string Strange_Asterisk_CharsStr;

    static UnicodeControlCodesNormalizeUtil()
    {
        Strange_Space_CharsStr = new string(Strange_Space_CharList.ToArray());
        Strange_NewLine_CharsStr = new string(Strange_NewLine_CharList.ToArray());
        Strange_HiddenControl_CharsStr = new string(Strange_HiddenControl_CharList.ToArray());
        Strange_Hyphon_CharsStr = new string(Strange_Hyphen_CharList.ToArray());
        Strange_Pipe_CharsStr = new string(Strange_Pipe_CharList.ToArray());
        Strange_Plus_CharsStr = new string(Strange_Plus_CharList.ToArray());
        Strange_Slash_CharsStr = new string(Strange_Slash_CharList.ToArray());
        Strange_Asterisk_CharsStr = new string(Strange_Slash_CharList.ToArray());
    }

    public static string Normalize(string str)
    {
        StringBuilder sb = new StringBuilder(str.Length);

        foreach (char src in str)
        {
            char dst;
            if (src == '\t' || src == ' ' || src == '　' || src == '\r' || src == '\n')
            {
                dst = src;
            }
            else if (src == '\b' || src == (char)0x007f)
            {
                dst = ' ';
            }
            else if (Strange_Space_CharsStr.Contains(src))
            {
                dst = ' ';
            }
            else if (Strange_NewLine_CharsStr.Contains(src))
            {
                dst = '\n';
            }
            else if (Strange_HiddenControl_CharList.Contains(src))
            {
                dst = (char)0;
            }
            else if (Strange_Hyphon_CharsStr.Contains(src))
            {
                dst = '-';
            }
            else if (Strange_Pipe_CharsStr.Contains(src))
            {
                dst = '|';
            }
            else if (Strange_Plus_CharsStr.Contains(src))
            {
                dst = '+';
            }
            else if (Strange_Slash_CharsStr.Contains(src))
            {
                dst = '/';
            }
            else if (Strange_Asterisk_CharsStr.Contains(src))
            {
                dst = '*';
            }
            else if (char.IsControl(src))
            {
                dst = (char)0;
            }
            else if (char.IsWhiteSpace(src))
            {
                dst = ' ';
            }
            else
            {
                dst = src;
            }

            if (dst != (char)0)
            {
                sb.Append(dst);
            }
        }

        return sb.ToString();
    }
}

