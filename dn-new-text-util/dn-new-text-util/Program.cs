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
using Microsoft.VisualBasic;
using System.Text.RegularExpressions;

internal class Program
{
    public const string AppTitle = "テキストファイル作成ツール";

    public static void Main(string[] args)
    {
        if (args.Length < 3)
        {
            MessageBox.Show("引数を指定してください。\r\n\r\n第一引数: 標準ディレクトリ名\r\n第二引数: 特別ディリレクトリ名\r\n第三引数: AI プロンプトディリレクトリ名", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        string baseDirNormal = args[0];
        string baseDirSpecial = args[1];
        string baseDirPrompt = args[2];

        try
        {
            Application.SetCompatibleTextRenderingDefault(true);
            //FastFont.LoadFontFile("YuGothR.ttc");
            FastFont2.SetFormsDefaultFontForBootSpeedUp(FastFont2.CreateFont("Meiryo UI"));
        }
        catch { }

        byte[] emptyFileData = new byte[] { 0xef, 0xbb, 0xbf, 0x0d, 0x0a, 0x0d, 0x0a, 0x0d, 0x0a, };

        try
        {
            string inputName = Interaction.InputBox(
                    "新しい文書の名前を入力してください。\r\n\"_untitled_\" のままでも構いません。\r\n\r\n(1) 1 文字目を '/' にすると、サブフォルダも新規作成します。\r\n(2-1) 1 文字目を '?' にすると、特別ディリクトリに作成します。\r\n(2-2) 1 文字目を '!' にすると、特別ディレクトリかつサブフォルダに作成します。\r\n(3) 1 文字目を '+' にすると、AI プロンプトディレクトリかつサブフォルダに作成します。",
                    AppTitle,
                    "_untitled_");

            inputName = inputName.Trim();

            if (inputName == null || inputName == "")
            {
                return;
            }

            if (inputName == "_untitled_")
            {
                inputName = "";
            }

            if (inputName.Length >= 64)
            {
                inputName = inputName.Substring(0, 64);
            }

            var now = DateTime.Now;

            string yymmdd = now.ToString("yyMMdd");

            string targetDir = baseDirNormal;

            if (inputName.StartsWith("/") || inputName.StartsWith("!") || inputName.StartsWith("+"))
            {
                bool promptMode = false;

                // フォルダ作成モード
                string name2 = inputName.Substring(1).Trim();
                name2 = SanitizeFileName(name2);
                if (name2.Length == 0)
                {
                    name2 = now.ToString("HHmmss") + " memo";
                }

                if (inputName.StartsWith("!"))
                {
                    targetDir = baseDirSpecial;
                }
                else if (inputName.StartsWith("+"))
                {
                    targetDir = baseDirPrompt;
                    promptMode = true;
                    yymmdd = now.ToString("yyMMdd") + "_" + now.ToString("HHmmss");
                }

                string dirToCreate = "";
                string fileBaseNameToCreate = "";

                for (int i = 0; i <= 10000; i++)
                {
                    string candidate;

                    if (i == 0)
                    {
                        fileBaseNameToCreate = yymmdd + " " + name2;

                        candidate = Path.Combine(targetDir, fileBaseNameToCreate);
                    }
                    else
                    {
                        fileBaseNameToCreate = yymmdd + " " + name2 + $" ({(i + 1).ToString()})";

                        candidate = Path.Combine(targetDir, fileBaseNameToCreate);
                    }

                    if (Directory.Exists(candidate) || File.Exists(candidate))
                    {
                        continue;
                    }
                    else
                    {
                        dirToCreate = candidate;
                        break;
                    }
                }

                if (dirToCreate == "")
                {
                    throw new ApplicationException("Unknown error");
                }

                if (promptMode)
                {
                    fileBaseNameToCreate += " - InputPrompt";
                }

                string filePath = Path.Combine(dirToCreate, fileBaseNameToCreate + ".txt");

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                }
                catch { }

                using (var file = File.Open(filePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
                {
                    file.Write(emptyFileData, 0, emptyFileData.Length);
                    file.Close();
                }

                ProcessStartInfo ps = new ProcessStartInfo(filePath);
                ps.UseShellExecute = true;

                Process.Start(ps);
            }
            else
            {
                // ファイル作成モード
                if (inputName.StartsWith("?"))
                {
                    targetDir = baseDirSpecial;
                    inputName = inputName.Substring(1);
                }
                string name2 = inputName.Trim();
                name2 = SanitizeFileName(name2);
                if (name2.Length == 0)
                {
                    name2 = now.ToString("HHmmss") + " memo";
                }

                string filePathToCreate = "";
                string fileBaseNameToCreate = "";

                for (int i = 0; i <= 10000; i++)
                {
                    string candidate;

                    if (i == 0)
                    {
                        fileBaseNameToCreate = yymmdd + " " + name2 + ".txt";
                        candidate = Path.Combine(targetDir, fileBaseNameToCreate);
                    }
                    else
                    {
                        fileBaseNameToCreate = yymmdd + " " + name2 + $" ({(i + 1).ToString()}).txt";
                        candidate = Path.Combine(targetDir, fileBaseNameToCreate);
                    }

                    if (Directory.Exists(candidate) || File.Exists(candidate))
                    {
                        continue;
                    }
                    else
                    {
                        filePathToCreate = candidate;
                        break;
                    }
                }

                if (filePathToCreate == "")
                {
                    throw new ApplicationException("Unknown error");
                }

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(filePathToCreate));
                }
                catch { }

                using (var file = File.Open(filePathToCreate, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
                {
                    file.Write(emptyFileData, 0, emptyFileData.Length);
                    file.Close();
                }

                ProcessStartInfo ps = new ProcessStartInfo(filePathToCreate);
                ps.UseShellExecute = true;

                Process.Start(ps);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// ファイル名に使用できない文字をすべて '_' に置換する
    /// </summary>
    /// <param name="input">元の文字列</param>
    /// <returns>置換後の文字列</returns>
    public static string SanitizeFileName(string input)
    {
        // OS が禁止している全文字を取得
        char[] invalid = Path.GetInvalidFileNameChars();

        // Regex クラスで使用できるようにエスケープ
        string pattern = $"[{Regex.Escape(new string(invalid))}]";

        // すべて '_' に置換
        return Regex.Replace(input, pattern, "_");
    }
}



public static class FastFont2
{
    static readonly string WinFontsDirPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");
    static readonly string UserFontsDirPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Fonts");

    static readonly PrivateFontCollection Col = new PrivateFontCollection();
    static readonly HashSet<string> FilenameHashSet = new HashSet<string>();

    static readonly Dictionary<string, FontFamily> NameToFontFamilyDict = new Dictionary<string, FontFamily>();
    static readonly Dictionary<string, Font> CachedFont = new Dictionary<string, Font>();

    static bool ResetFlag = true;

    static List<int> LangIDList = new List<int>();

    static FastFont2()
    {
        LangIDList.Add(0);
        LangIDList.Add(System.Globalization.CultureInfo.GetCultureInfo("en-us").LCID);
        LangIDList.Add(System.Globalization.CultureInfo.GetCultureInfo("ja-jp").LCID);

        LoadFontFile("msgothic.ttc");
        LoadFontFile("tahoma.ttf");
        LoadFontFile("meiryo.ttc");
    }

    public static void LoadFontFile(string fontFileName)
    {
        lock (FilenameHashSet)
        {
            if (FilenameHashSet.Contains(fontFileName) == false)
            {
                string fontPath1 = Path.Combine(WinFontsDirPath, fontFileName);
                string fontPath2 = Path.Combine(UserFontsDirPath, fontFileName);

                bool ok = false;

                try
                {
                    if (File.Exists(fontPath1))
                    {
                        Col.AddFontFile(fontPath1);
                        ok = true;
                    }
                    else if (File.Exists(fontPath2))
                    {
                        Col.AddFontFile(fontPath2);
                        ok = true;
                    }
                }
                catch
                {
                }

                if (ok)
                {
                    FilenameHashSet.Add(fontFileName);

                    lock (NameToFontFamilyDict)
                    {
                        ResetFlag = true;
                    }
                }
            }
        }
    }
    static void RebuildNameToFontFamilyDictIfNecessary()
    {
        lock (NameToFontFamilyDict)
        {
            if (ResetFlag)
            {
                NameToFontFamilyDict.Clear();

                foreach (var family in Col.Families)
                {
                    foreach (var lang in LangIDList)
                    {
                        string name = family.GetName(lang);

                        NameToFontFamilyDict[name] = family;
                    }
                }

                ResetFlag = false;
            }
        }
    }

    public static Font CreateFont(string fontName, float fontSize = 9.0f, FontStyle style = FontStyle.Regular, GraphicsUnit unit = GraphicsUnit.Point)
    {
        RebuildNameToFontFamilyDictIfNecessary();

        if (NameToFontFamilyDict.TryGetValue(fontName, out var family) == false)
        {
            if (NameToFontFamilyDict.TryGetValue("PlemolJP HS", out var family2) == false)
            {
                throw new ApplicationException($"Font name '{fontName}' not found.");
            }

            family = family2;
        }

        return new Font(family, fontSize, style, unit);
    }

    public static Font GetCachedFont(string fontName, float fontSize = 9.0f, FontStyle style = FontStyle.Regular, GraphicsUnit unit = GraphicsUnit.Point)
    {
        string key = $"{fontName}:{fontSize}:{style}:{unit}";

        lock (CachedFont)
        {
            if (CachedFont.TryGetValue(key, out var ret))
            {
                return ret;
            }
        }

        var ret2 = CreateFont(fontName, fontSize, style, unit);

        lock (CachedFont)
        {
            CachedFont[key] = ret2;
        }

        return ret2;
    }

    public static void SetFormsDefaultFontForBootSpeedUp(Font font)
    {
        {
            Type targetType = typeof(System.Windows.Forms.Control);

            FieldInfo fieldInfo = targetType.GetField(
                "defaultFont",
                BindingFlags.NonPublic | BindingFlags.Static
            );

            if (fieldInfo != null)
            {
                fieldInfo.SetValue(null, font);
            }
        }
        {
            Type targetType = typeof(System.Windows.Forms.ToolStripManager);

            FieldInfo fieldInfo = targetType.GetField(
                "defaultFont",
                BindingFlags.NonPublic | BindingFlags.Static
            );

            if (fieldInfo != null)
            {
                fieldInfo.SetValue(null, font);
            }
        }
    }
}


