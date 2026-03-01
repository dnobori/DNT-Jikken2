// Program.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Dnnt260301_QZV5R7;

internal static class Program
{
    // 集計対象のルートディレクトリ（要求仕様）
    private const string TargetDir = @"c:\git";

    // 階層1の列挙用オプション：
    // Hidden/System も含めて列挙したいので AttributesToSkip は 0。
    private static readonly EnumerationOptions Level1EnumerationOptions = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = true,
        ReturnSpecialDirectories = false,
        AttributesToSkip = (FileAttributes)0
    };

    // 再帰カウント用オプション：
    // ReparsePoint(=symlink/junction等) を列挙から除外することで「辿らない/数えない」を同時に実現。
    private static readonly EnumerationOptions RecursiveEnumerationOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        ReturnSpecialDirectories = false,
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    /// <summary>
    /// エントリポイント。
    /// </summary>
    /// <param name="args">未使用。</param>
    /// <returns>終了コード（0:成功 / 1:失敗）。</returns>
    private static int Main(string[] args)
    {
        if (!Directory.Exists(TargetDir))
        {
            Console.Error.WriteLine($"TargetDir が存在しません: {TargetDir}");
            return 1;
        }

        // 階層1ディレクトリ一覧を取得
        string[] topLevelDirectories = GetTopLevelDirectories(TargetDir);

        // 結果格納（インデックス固定で書き込むことでロック不要）
        var results = new DirectoryCountResult[topLevelDirectories.Length];

        var parallelOptions = new ParallelOptions
        {
            // I/Oが主なので過剰スレッドにし過ぎない（基本はCPUコア数程度）
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount)
        };

        // 階層1ディレクトリ単位で並列に集計
        Parallel.For(0, topLevelDirectories.Length, parallelOptions, i =>
        {
            string dirPath = topLevelDirectories[i];
            string name = GetDirectoryName(dirPath);
            long count = CountEntriesRecursively(dirPath);
            results[i] = new DirectoryCountResult(name, count);
        });

        // 総数の多い順にソート（同数は名前で安定化）
        Array.Sort(results, DirectoryCountResultComparer.Instance);

        // Console.WriteLine連打は遅いのでまとめて出力
        var sb = new StringBuilder(results.Length * 32);
        foreach (var r in results)
        {
            sb.Append(r.DirectoryName).Append(' ').Append(r.TotalCount).AppendLine();
        }

        Console.Write(sb.ToString());
        return 0;
    }

    /// <summary>
    /// TargetDir 直下（階層1）のディレクトリを列挙して配列化する。
    /// </summary>
    /// <param name="targetDirectory">ルートディレクトリ。</param>
    /// <returns>階層1ディレクトリのフルパス配列。</returns>
    private static string[] GetTopLevelDirectories(string targetDirectory)
    {
        var list = new List<string>(capacity: 256);

        try
        {
            foreach (string dir in Directory.EnumerateDirectories(targetDirectory, "*", Level1EnumerationOptions))
            {
                list.Add(dir);
            }
        }
        catch (Exception ex) when (IsIgnorableEnumerationException(ex))
        {
            // ルート自体が読めない等：速度優先で握り潰し（空配列になる）
        }

        return list.ToArray();
    }

    /// <summary>
    /// 指定ディレクトリ配下の「ファイル + サブディレクトリ」総数を再帰的にカウントする。
    /// - ReparsePoint(symlink/junction) は辿らず・カウントもしない（EnumerationOptionsで除外）
    /// - Windows ショートカット(.lnk) はカウントしない
    /// </summary>
    /// <param name="rootDirectory">階層1ディレクトリのフルパス。</param>
    /// <returns>rootDirectory 配下の合計総数（ファイル + サブディレクトリ）。</returns>
    private static long CountEntriesRecursively(string rootDirectory)
    {
        // 階層1ディレクトリ自体が ReparsePoint の場合は「辿らない」ため 0。
        if (IsReparsePoint(rootDirectory))
        {
            return 0;
        }

        long total = 0;

        try
        {
            // OS側の再帰列挙（RecurseSubdirectories=true）を使い、追加の属性取得を極力避けて高速化。
            foreach (string entryPath in Directory.EnumerateFileSystemEntries(rootDirectory, "*", RecursiveEnumerationOptions))
            {
                // ReparsePoint は EnumerationOptions で列挙自体されない。
                // ショートカット(.lnk)は除外。
                if (IsWindowsShortcutFile(entryPath))
                {
                    continue;
                }

                total++;
            }
        }
        catch (Exception ex) when (IsIgnorableEnumerationException(ex))
        {
            // 途中でアクセス不可など：速度優先で握り潰し、数えられた分だけ返す
        }

        return total;
    }

    /// <summary>
    /// パスが ReparsePoint（symlink/junction等）かどうかを判定する。
    /// </summary>
    /// <param name="path">判定対象パス。</param>
    /// <returns>ReparsePoint の場合 true。</returns>
    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception ex) when (IsIgnorableEnumerationException(ex))
        {
            // 属性が取れない場合は安全側に倒して「辿らない」扱い
            return true;
        }
    }

    /// <summary>
    /// Windows ショートカット(.lnk)ファイルかどうかを判定する。
    /// ".lnk"で終わるディレクトリ名が極めて稀に存在し得るため、拡張子一致時のみ属性で最終確認する。
    /// </summary>
    /// <param name="path">フルパス。</param>
    /// <returns>ショートカットファイルなら true。</returns>
    private static bool IsWindowsShortcutFile(string path)
    {
        if (!EndsWithLnkExtension(path))
        {
            return false;
        }

        try
        {
            FileAttributes attributes = File.GetAttributes(path);
            // ディレクトリでなければ ".lnk" はショートカットファイルとして除外
            return (attributes & FileAttributes.Directory) == 0;
        }
        catch (Exception ex) when (IsIgnorableEnumerationException(ex))
        {
            // 判定できない場合も安全側に倒して除外
            return true;
        }
    }

    /// <summary>
    /// 末尾が ".lnk"（大文字小文字無視）かを、割り当て無しで高速判定する。
    /// </summary>
    /// <param name="path">パス文字列。</param>
    /// <returns>末尾が ".lnk" の場合 true。</returns>
    private static bool EndsWithLnkExtension(string path)
    {
        int len = path.Length;
        if (len < 4)
        {
            return false;
        }

        // ASCII英字の高速小文字化: 'A'..'Z' に 0x20 を OR すると 'a'..'z'
        return path[len - 4] == '.'
            && ((path[len - 3] | (char)0x20) == 'l')
            && ((path[len - 2] | (char)0x20) == 'n')
            && ((path[len - 1] | (char)0x20) == 'k');
    }

    /// <summary>
    /// 列挙処理で「よく起きる」例外を無視して継続してよいか判定する。
    /// </summary>
    /// <param name="exception">例外。</param>
    /// <returns>無視してよい場合 true。</returns>
    private static bool IsIgnorableEnumerationException(Exception exception)
        => exception is UnauthorizedAccessException
            or IOException
            or DirectoryNotFoundException
            or PathTooLongException;

    /// <summary>
    /// フルパスからディレクトリ名（末尾要素）を取得する。
    /// </summary>
    /// <param name="directoryPath">ディレクトリのフルパス。</param>
    /// <returns>ディレクトリ名。</returns>
    private static string GetDirectoryName(string directoryPath)
    {
        string trimmed = Path.TrimEndingDirectorySeparator(directoryPath);
        return Path.GetFileName(trimmed);
    }

    /// <summary>
    /// 階層1ディレクトリ1件ぶんの集計結果データ。
    /// </summary>
    internal readonly struct DirectoryCountResult
    {
        /// <summary>表示用のディレクトリ名（階層1）。</summary>
        public string DirectoryName { get; }

        /// <summary>配下の「ファイル + サブディレクトリ」総数。</summary>
        public long TotalCount { get; }

        /// <summary>
        /// 集計結果を生成する。
        /// </summary>
        /// <param name="directoryName">表示用ディレクトリ名。</param>
        /// <param name="totalCount">合計総数。</param>
        public DirectoryCountResult(string directoryName, long totalCount)
        {
            DirectoryName = directoryName;
            TotalCount = totalCount;
        }
    }

    /// <summary>
    /// 「総数の多い順」に並べるための比較器。
    /// </summary>
    private sealed class DirectoryCountResultComparer : IComparer<DirectoryCountResult>
    {
        /// <summary>共有インスタンス（割り当て削減）。</summary>
        public static readonly DirectoryCountResultComparer Instance = new();

        /// <summary>
        /// 総数（降順）→ディレクトリ名（昇順）の順で比較する。
        /// </summary>
        /// <param name="x">左。</param>
        /// <param name="y">右。</param>
        /// <returns>比較結果。</returns>
        public int Compare(DirectoryCountResult x, DirectoryCountResult y)
        {
            int countComparison = y.TotalCount.CompareTo(x.TotalCount);
            if (countComparison != 0)
            {
                return countComparison;
            }

            return string.CompareOrdinal(x.DirectoryName, y.DirectoryName);
        }
    }
}
