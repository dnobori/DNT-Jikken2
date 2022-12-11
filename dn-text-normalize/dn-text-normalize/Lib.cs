using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using System.Windows.Forms;
using System.Diagnostics;

public static class Lib
{
    public static void ClipboardWrite(string str)
    {
        str = str._NonNull();

        Clipboard.SetDataObject(str, true);
    }

    public static string ClipboardRead()
    {
        var data = Clipboard.GetDataObject();

        if (data != null)
        {
            if (data.GetDataPresent(DataFormats.Text) || data.GetDataPresent(DataFormats.UnicodeText))
            {
                string str = (string)data.GetData(DataFormats.UnicodeText);

                return str._NonNull();
            }
        }

        return "";
    }

    public static string _NonNull(this string s) { if (s == null) return ""; else return s; }


    // 文字列をソフトイーサ表記規則に正規化する
    public static string NormalizeStrSoftEther(string str, bool trim = false)
    {
        bool b = false;
        str = str._NonNull();

        str = ReplaceStr(str, "", "\n"); // Power Point

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

        if (trim)
        {
            str = str.Trim();
        }

        return str;
    }
    static string normalizeStrSoftEtherInternal(string str)
    {
        if (str.Trim().Length == 0)
        {
            return "";
        }

        int i;
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

        return str1 + normalizeStrSoftEtherInternal2(str2);
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

}


