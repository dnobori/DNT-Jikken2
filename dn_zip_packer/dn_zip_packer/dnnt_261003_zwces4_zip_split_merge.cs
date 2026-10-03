/*
DNNT 261003_ZWCES4 31.5 MBytes ごとに分割する zip ファイル圧縮プログラム

目的:
  ファイル、フォルダ、および明示的に指定された ZIP の内容を、相対パスを保った
  独立した最大 19 個の暗号化 ZIP にまとめる。1 個の目安は 31,500,000 bytes。
  個数上限が絶対優先。必要なら目安容量を緩和し、できるだけ均等に配分する。
  これは .z01 等のマルチボリューム ZIP ではなく、各 ZIP を単独で展開できる形式。

環境:
  C# 4.0 / .NET Framework 4.0 / AnyCPU。実行時の外部 DLL・NuGet ライブラリ不要。
  WinExe としてビルドし、必要に応じて Win32 コンソールを確保する。/target:exe も可。
  Windows 7 以降のデスクトップ環境を想定。管理者権限は不要。
  Visual Studio 2026 の通常の IDE では .NET Framework 4.0 はサポート外。
  同梱プロジェクトと参照アセンブリを使うコマンドラインビルドについて README 参照。

動作原理:
  DeflateStream は圧縮・展開にだけ使用する。ZIP 構造、CRC、ZipCrypto、ZIP64、
  WinZip AES 読み取りはこのファイル内に実装。AES のブロック演算等は標準暗号 API。
  全入力を最大 8 並列で事前圧縮し、本圧縮と同じ DeflateStream の既定設定で測る。
  元 ZIP は必ず復号・展開してから新たに圧縮する。展開用一時ファイルは作らない。
  圧縮結果は 64 KiB の断片で任意にキャッシュし、作成途中も含めて共通予算で管理。
  予算は Windows の使用可能な物理/コミット/仮想空間の最小値の 80% を再評価する。
  予算不足では古いものから破棄し、必要時だけ再圧縮。配列確保失敗ならキャッシュ停止。
  ZIP 管理情報・暗号ヘッダ・ZIP64 境界を含む完成サイズで最大 19 個の計画を確定する。
  通常容量で first-fit/best-fit を比較し、超過見込みなら大きい順の均等配置・移動・交換。
  最小個数や完全な大域最適は保証しないが、19 個の上限と計画/実サイズ一致は検査する。
  キャッシュ時も入力の実体・サイズ・日時を照合し、キャッシュを失った再圧縮時は
  平文 SHA-256・CRC・圧縮量も照合する。成功するまで新規出力を所有ハンドルで保持。

安全性 / 解釈:
  [T261004_EL_ATCEV_01] 旧出力の削除は、指定された最初の名前、または
  「ベース名 + '.' + ASCII 数字ちょうど 2 桁 + '.zip'」に完全一致する直下の通常ファイルだけ。
  ワイルドカード削除、再帰削除、名前を再検索しての失敗時削除は行わない。
  新規出力は CREATE_NEW で作り、DELETE アクセス付きハンドルを成功まで保持する。
  書き込みストリームとは別に所有ハンドルを保持し、失敗時だけ削除保留にして閉じる。
  入出力の重なり、リンク解決後の実体同一性、不正 ZIP パスを検査する。
  入力のシンボリックリンク・ジャンクションは実体を走査し、リンク名の相対パスを
  保った通常ファイル/フォルダにする。同じ実体への別名は別々に格納する。
  祖先へ戻る循環だけは警告し、その位置を通常フォルダとして残して再下降しない。
  出力フォルダもリンクを解決して固定し、以後の列挙・作成・削除は実体パスで行う。
  ただし旧出力の削除候補そのものがリンクなら、安全のため削除も追跡も行わない。
  元の旧出力は確認後に削除するため、失敗しても旧出力の復元はしない。
  ZIP 入力 'a.zip' の内容は 'a/...' に入れる（末尾の .zip 拡張子だけを除去）。
  明示指定した ZIP だけが対象。名前が空または安全でない仮想ルートになる場合は拒否。
  フォルダの内部に存在する ZIP は通常のファイルであり、再帰的には展開しない。
  入力 ZIP: Store / Deflate、非暗号化 / ZipCrypto / WinZip AES AE-1・AE-2
  (128/192/256 bit)、単一ボリューム ZIP64 に対応。その他の方式は明示的に拒否する。
  出力は UTF-8 + ZipCrypto、パスワードは固定の 1 文字 m。機密保護には不適切。
  UTF-8 指示のない旧 ZIP 名は Unicode Path Extra Field を優先し、それもなければ
  LEGACY_ZIP_CODE_PAGE を使用する。既定値 932 (Windows 日本語 / CP932)。
  CP932 として不正なバイト列だけ CP437 で再解釈する。曖昧な名前の完全自動判別は
  できないため、非日本語の旧 ZIP では定数を作成元のコードページに変更する。
  UTF-8 フラグを偽っている不正名は旧コードページへ逃がさず、エラーにする。
  空の末端ディレクトリも保存する。ファイル名衝突およびファイル/フォルダ衝突は
  全 ZIP を合わせた名前空間で、大文字小文字を無視して検出する。
  長いパス・循環リンク・ZIP 内特殊ファイル等の制約、検証状況は README に記載。

参考仕様（仕様文書の転載ではなく、このソースは独自の実装）:
  https://pkware.cachefly.net/webdocs/casestudies/APPNOTE.TXT
  https://www.winzip.com/en/support/aes-encryption/
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;

/// <summary>単一ファイルで完結するアプリケーション。内部名は依頼の命名規則による。</summary>
internal static class dnnt_261003_zwces4_zip_split_merge
{
    private const long MAX_ONE_ZIP_FILE_SIZE = 31500000L;
    private static int MAX_NUM_ZIP_FILES = 18;
    private const string ZIP_PASSWORD = "m";
    // UTF-8/Unicode Path の明示指定がない既存 ZIP 用。日本語 Windows の CP932 を優先。
    private const int LEGACY_ZIP_CODE_PAGE = 932;
    private const int BufferSize = 65536;
    private const long Zip32Limit = 0xFFFFFFFFL;
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
    private static readonly CultureInfo NumberCulture = CultureInfo.InvariantCulture;
    private static volatile bool CancellationRequested;

    /// <summary>引数を入力パスとして受け取り、成功時 0、失敗時 Win32 形式のコードを返す。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
#if DNNT_SELF_TEST
        return SelfTests.Run();
#else
        int exitCode = 1;
        OutputTransaction transaction = null;
        try
        {
            Native.EnsureConsole();
            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
            {
                e.Cancel = true;
                CancellationRequested = true;
            };
            Console.WriteLine("ZIP ファイル分割結合ユーティリティ");
            Console.WriteLine();
            List<InputRoot> roots = new List<InputRoot>();
            if (args.Length != 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    try { roots.Add(InputRoot.Read(args[i])); }
                    catch (Exception ex)
                    {
                        throw new AppError(ErrorCode(ex),
                            "コマンドライン引数 " + (i + 1).ToString(NumberCulture) +
                            " 個目を読み取れません: " + args[i], ex);
                    }
                }
            }
            else
            {
                AddInputs(roots);
            }

            while (true)
            {
                CheckCancellation();
                Console.WriteLine();
                Console.WriteLine("以下の {0} 個のファイルまたはフォルダが指定されました。", roots.Count);
                for (int i = 0; i < roots.Count; i++)
                    Console.WriteLine("({0}/{1} 個目: {2}): {3}", i + 1, roots.Count,
                        roots[i].IsDirectory ? "フォルダ" : "ファイル", roots[i].Path);
                if (AskYesNo("上記の " + roots.Count.ToString(NumberCulture) +
                    " 個ののファイルまたはフォルダが指定されました。" + Environment.NewLine + "処理を継続しますか?(y/n)")) break;
                AddInputs(roots);
            }

            Console.WriteLine();
            Console.Write($"最大ファイル数 (無指定: {MAX_NUM_ZIP_FILES} 個): ");
            string tmp1 = Console.ReadLine();

            if (int.TryParse(tmp1, out var tmp2))
            {
                if (tmp2 >= 1)
                {
                    MAX_NUM_ZIP_FILES = tmp2;
                }
            }

            Console.WriteLine();

            string outputPath = SelectOutput();
            using (DirectoryGuard guard = new DirectoryGuard(System.IO.Path.GetDirectoryName(outputPath)))
            {
                // 保存ダイアログのフォルダがリンクでも、以後は固定した実体にだけ書く。
                OutputNames names = new OutputNames(System.IO.Path.Combine(guard.CanonicalPath,
                    System.IO.Path.GetFileName(outputPath)));
                if (!String.Equals(names.FirstPath, outputPath, StringComparison.OrdinalIgnoreCase))
                    Console.WriteLine("保存先の実体パス: {0}", names.FirstPath);
                Console.WriteLine("入力内容と名前の重複を検査しています...");
                Manifest manifest = Manifest.Build(roots, names);
                Console.WriteLine("対象: {0} 個のファイル、{1} 個の空フォルダ、合計 {2} bytes。",
                    manifest.FileCount, manifest.Items.Count - manifest.FileCount, FormatNumber(manifest.TotalBytes));
                Console.WriteLine("注意: 出力は固定パスワード m の ZipCrypto です。機密保護には使用しないでください。");
                using (CompressedCache cache = new CompressedCache(Native.AvailableMemoryBytes))
                {
                    int workers = Math.Max(1, Math.Min(Environment.ProcessorCount, 8));
                    Precompress(manifest, cache, workers);
                    PackingPlan plan = PackingPlan.Build(manifest.Items);
                    cache.Trim();
                    plan.Show(names);
                    // すべての復号・圧縮計測と配置検査が成功してから、初めて旧出力を削除する。
                    transaction = new OutputTransaction(names);
                    transaction.RemovePreviousOutputs(manifest);
                    PackResult result = Pack(manifest, transaction, plan, cache);
                    string resultText = FormatResult(result, names);
                    transaction.Commit();
                    TryWriteLine(resultText);
                    exitCode = 0;
                }
            }
        }
        catch (Exception ex)
        {
            exitCode = ErrorCode(ex);
            TryWriteLine("\nエラー: 処理を完了できませんでした。終了コード: " + exitCode.ToString(NumberCulture));
            TryWriteLine(ex.ToString());
            if (transaction != null) transaction.Rollback();
        }
        finally
        {
            if (transaction != null) transaction.Dispose();
            Native.WaitForExitKey();
        }
        return exitCode;
#endif
    }

    /// <summary>対話入力を既存リストに追記する。空行で終了し、無効なパスでは再入力する。</summary>
    private static void AddInputs(List<InputRoot> roots)
    {
        while (true)
        {
            CheckCancellation();
            Console.Write("入力ファイルあるいはフォルダ ({0} 個目): ", roots.Count + 1);
            string line = ReadLineRequired();
            if (String.IsNullOrWhiteSpace(line))
            {
                if (roots.Count != 0) return;
                continue;
            }
            try { roots.Add(InputRoot.Read(line)); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Console.WriteLine("エラー: そのファイルまたはフォルダは存在しないようです。");
                Console.WriteLine("詳細: {0}", ex.Message);
            }
        }
    }

    /// <summary>y/n のみを受理する確認。戻り値 true は y、false は n。</summary>
    private static bool AskYesNo(string prompt)
    {
        while (true)
        {
            CheckCancellation();
            Console.Write(prompt + " ");
            string answer = ReadLineRequired().Trim();
            if (String.Equals(answer, "y", StringComparison.OrdinalIgnoreCase)) return true;
            if (String.Equals(answer, "n", StringComparison.OrdinalIgnoreCase)) return false;
            Console.WriteLine("y または n を入力してください。");
        }
    }

    /// <summary>コンソール入力を読む。入力ストリーム終了はキャンセルとして扱う。</summary>
    private static string ReadLineRequired()
    {
        string line = Console.ReadLine();
        CheckCancellation();
        if (line == null) throw new AppError(1223, "コンソール入力が終了しました。");
        return line;
    }

    /// <summary>STA 上で保存コモンダイアログを開き、選択された絶対 .zip パスを返す。</summary>
    private static string SelectOutput()
    {
        using (SaveFileDialog dialog = new SaveFileDialog())
        {
            dialog.Title = "最初の ZIP ファイルの保存先";
            dialog.Filter = "ZIP ファイル (*.zip)|*.zip";
            dialog.DefaultExt = "zip";
            dialog.AddExtension = true;
            dialog.OverwritePrompt = true;
            dialog.CheckPathExists = true;
            dialog.ValidateNames = true;
            dialog.RestoreDirectory = true;
            dialog.FileName = Lib.GenTag() + "_MaterialFiles.zip";
            DialogResult result = dialog.ShowDialog(new ConsoleOwner());
            if (result != DialogResult.OK) throw new AppError(1223, "保存先の選択がキャンセルされました。");
            string path = PathRules.FullPath(dialog.FileName);
            if (!String.Equals(System.IO.Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase))
                throw new AppError(87, "保存ファイルの拡張子は .zip にしてください: " + path);
            return path;
        }
    }

    /// <summary>本圧縮と事前圧縮で共用する Deflate 設定。.NET 4.0 の既定レベルを両方に使う。</summary>
    private static DeflateStream CreateCompressor(Stream destination)
    {
        return new DeflateStream(destination, CompressionMode.Compress, true);
    }

    /// <summary>各入力を独立した読取ハンドルで一度圧縮し、サイズ・検証値・任意のキャッシュを確定する。</summary>
    /// <param name="manifest">重複検査済みの入力一覧。各要素をただ 1 個のワーカーが担当する。</param>
    /// <param name="cache">全ワーカーで共有する、内部で同期済みの圧縮データキャッシュ。</param>
    /// <param name="workers">同時処理数。既存 ZIP 内の別エントリも読取ハンドル・暗号状態を共有しない。</param>
    private static void Precompress(Manifest manifest, CompressedCache cache, int workers)
    {
        Console.WriteLine("事前圧縮: {0} スレッドで全データを検証・計測します。", workers);
        try
        {
            System.Threading.Tasks.ParallelOptions options = new System.Threading.Tasks.ParallelOptions();
            options.MaxDegreeOfParallelism = workers;
            System.Threading.Tasks.Parallel.For(0, manifest.Items.Count, options,
                delegate (int index, System.Threading.Tasks.ParallelLoopState loop)
                {
                    if (loop.ShouldExitCurrentIteration) return;
                    SourceItem item = manifest.Items[index];
                    try
                    {
                        CheckCancellation();
                        if (!item.IsDirectory)
                            Console.WriteLine("事前圧縮: 全体 {0} / {1} 個目のファイル: {2} bytes: {3}",
                                item.Ordinal, manifest.FileCount, FormatNumber(item.Length), item.ZipPath);
                        Measurement measurement = new Measurement();
                        measurement.Method = item.IsDirectory || item.Length == 0 ? (ushort)0 : (ushort)8;
                        using (CaptureStream capture = new CaptureStream(cache, item))
                        {
                            ContentResult content;
                            if (measurement.Method == 8)
                                using (DeflateStream compressor = CreateCompressor(capture))
                                    content = item.CopyPlaintextTo(compressor);
                            else content = item.CopyPlaintextTo(capture);
                            measurement.CompressedSize = checked(capture.Length + (item.IsDirectory ? 0L : 12L));
                            measurement.UncompressedSize = content.Length;
                            measurement.Crc = content.Crc;
                            measurement.Digest = content.Digest;
                            item.Measurement = measurement;
                            capture.Complete();
                        }
                    }
                    catch (Exception ex)
                    {
                        loop.Stop();
                        throw new AppError(ErrorCode(ex), "事前圧縮に失敗しました: " + item.Origin +
                            Environment.NewLine + "ZIP 内パス: " + item.ZipPath, ex);
                    }
                });
        }
        catch (AggregateException ex)
        {
            AggregateException errors = ex.Flatten();
            Exception first = errors.InnerExceptions[0];
            throw new AppError(ErrorCode(first), "事前圧縮を中止しました。旧出力はまだ削除していません。", errors);
        }
        cache.Trim();
        long total = 0;
        foreach (SourceItem item in manifest.Items)
        {
            if (item.Measurement == null) throw new AppError(13, "事前圧縮の計測結果がありません: " + item.Origin);
            if (!item.IsDirectory) total = checked(total + item.Measurement.CompressedSize - 12L);
        }
        Console.WriteLine("事前圧縮完了: 圧縮データ合計 {0} bytes (ZIP 管理情報・暗号ヘッダを除く)。", FormatNumber(total));
        Console.WriteLine("圧縮キャッシュ保持量: {0} bytes / 現在の上限 {1} bytes。", FormatNumber(cache.UsedBytes), FormatNumber(cache.LimitBytes));
    }

    /// <summary>事前確定した最大 19 個の計画を出力する。サイズや内容が変わった場合は全体を失敗させる。</summary>
    private static PackResult Pack(Manifest manifest, OutputTransaction transaction, PackingPlan plan, CompressedCache cache)
    {
        plan.Validate(manifest.Items);
        PackResult result = new PackResult();
        result.FileCount = manifest.FileCount;
        result.OriginalBytes = manifest.TotalBytes;
        result.CycleCutCount = manifest.CycleCutCount;
        foreach (PlannedPart part in plan.Parts)
        {
            CheckCancellation();
            OwnedOutput output = transaction.CreateNext();
            ZipOutput writer = new ZipOutput(output.Stream);
            foreach (SourceItem item in part.Items)
            {
                CheckCancellation();
                if (item.IsDirectory)
                    Console.WriteLine("{0}: 空フォルダ: {1}", output.Name, item.ZipPath);
                else
                    Console.WriteLine("{0}: 全体 {1} / {2} 個目のファイル: {3} bytes: {4}",
                        output.Name, item.Ordinal, manifest.FileCount, FormatNumber(item.Length), item.ZipPath);
                try
                {
                    EntryRecord record = writer.WriteCandidate(item, cache);
                    writer.Accept(record);
                    if (!item.IsDirectory)
                        result.CompressedPayloadBytes = checked(result.CompressedPayloadBytes + record.CompressedSize - 12L);
                }
                catch (Exception ex)
                {
                    throw new AppError(ErrorCode(ex), "出力中のエラー: " + item.Origin +
                        Environment.NewLine + "出力先: " + output.Name + Environment.NewLine + "ZIP 内パス: " + item.ZipPath, ex);
                }
            }
            output.ExpectedLength = writer.Finish();
            if (output.ExpectedLength != part.Layout.Size)
                throw new AppError(13, "ZIP の物理サイズが事前計画と一致しません: " + output.Name);
            result.AddPart(output.Name, output.ExpectedLength);
            Console.WriteLine("{0}: 完成サイズ {1} bytes{2}", output.Name, FormatNumber(output.ExpectedLength),
                output.ExpectedLength > MAX_ONE_ZIP_FILE_SIZE ? " (31,500,000 bytes 超過)" : "");
        }
        return result;
    }

    /// <summary>全 ZIP 合計、圧縮データ合計、最大 ZIP を区別した結果文字列を成功確定前に構成する。</summary>
    private static string FormatResult(PackResult result, OutputNames names)
    {
        double percent = result.OriginalBytes == 0 ? 0.0 :
            (1.0 - (double)result.CompressedPayloadBytes / (double)result.OriginalBytes) * 100.0;
        string text = String.Format(NumberCulture,
            "{0} ～ {1} (合計 {2} 個) に、全部で {3} 個のファイル群を圧縮しました。" +
            Environment.NewLine + Environment.NewLine +
            "圧縮前ファイル容量: {4} bytes" + Environment.NewLine +
            "圧縮後ファイル容量: {5} bytes ({6} % 削減)" + Environment.NewLine +
            "生成された {2} 個の zip ファイルの合計容量: {7} bytes" + Environment.NewLine +
            "このうち最大の .zip ファイルは、{8} ({9} 個目) であり {10} bytes です。",
            names.FirstPath, result.LastName, result.PartCount, result.FileCount,
            FormatNumber(result.OriginalBytes), FormatNumber(result.CompressedPayloadBytes),
            percent.ToString("F1", NumberCulture), FormatNumber(result.TotalZipBytes),
            result.LargestName, result.LargestIndex, FormatNumber(result.LargestBytes));
        text += Environment.NewLine + "圧縮後ファイル容量は圧縮データのみ。ZIP 総容量には暗号化・名前・各種ヘッダ等も含みます。";
        if (percent < 0) text += Environment.NewLine + "削減率が負の場合は、その割合だけ増加したことを表します。";
        if (result.CycleCutCount != 0)
            text += Environment.NewLine + "注意: 祖先へ戻る循環リンク " + result.CycleCutCount.ToString(NumberCulture) +
                " 箇所は通常フォルダとして記録し、無限になる子孫の再展開は行っていません。";
        return Environment.NewLine + text;
    }

    /// <summary>Win32 に近い終了コードに変換する。未知の管理例外は ERROR_GEN_FAILURE。</summary>
    private static int ErrorCode(Exception error)
    {
        AppError applicationError = error as AppError;
        if (applicationError != null) return applicationError.Code;
        Win32Exception nativeError = error as Win32Exception;
        if (nativeError != null && nativeError.NativeErrorCode != 0) return nativeError.NativeErrorCode;
        if (error is OperationCanceledException) return 995;
        if (error is UnauthorizedAccessException) return 5;
        if (error is FileNotFoundException) return 2;
        if (error is DirectoryNotFoundException) return 3;
        if (error is PathTooLongException) return 206;
        if (error is OverflowException) return 534;
        if (error is OutOfMemoryException) return 8;
        if (error is ArgumentException) return 87;
        if (error is InvalidDataException || error is CryptographicException || error is EndOfStreamException) return 13;
        int hresult = Marshal.GetHRForException(error);
        if ((hresult & unchecked((int)0xFFFF0000)) == unchecked((int)0x80070000))
            return hresult & 0xFFFF;
        return 31;
    }

    /// <summary>Ctrl+C による協調キャンセルを I/O ブロック間で通知する。</summary>
    private static void CheckCancellation()
    {
        if (CancellationRequested) throw new OperationCanceledException("Ctrl+C により中止しました。");
    }

    /// <summary>エラー報告中のコンソール例外で後片付けを妨げない。</summary>
    private static void TryWriteLine(string text)
    {
        try { Console.WriteLine(text); } catch (IOException) { } catch (ObjectDisposedException) { }
    }

    /// <summary>環境依存の桁区切りを避け、常に 3 桁ごとのカンマで返す。</summary>
    private static string FormatNumber(long value) { return value.ToString("N0", NumberCulture); }

    /// <summary>終了コードと詳細な原因を保持する業務例外。</summary>
    private sealed class AppError : Exception
    {
        internal readonly int Code;
        internal AppError(int code, string message) : base(message) { Code = code; }
        internal AppError(int code, string message, Exception inner) : base(message, inner) { Code = code; }
    }

    /// <summary>コモンダイアログの親コンソールウィンドウ。</summary>
    private sealed class ConsoleOwner : IWin32Window
    {
        public IntPtr Handle { get { return Native.GetConsoleWindow(); } }
    }

    /// <summary>ユーザーが指定した 1 件。ZIP もここでは物理ファイルである。</summary>
    private sealed class InputRoot
    {
        internal string Path;
        internal string CanonicalPath;
        internal string Identity;
        internal bool IsDirectory;
        /// <summary>引用符と絶対パスを検査し、種類・存在・読み取り可否をその場で確認する。</summary>
        internal static InputRoot Read(string text)
        {
            string path = PathRules.FullPath(text);
            FileAttributes attributes = File.GetAttributes(path);
            InputRoot root = new InputRoot();
            root.Path = path;
            root.IsDirectory = (attributes & FileAttributes.Directory) != 0;
            if (root.IsDirectory)
            {
                DirectorySnapshot snapshot = DirectorySnapshot.Capture(path);
                Directory.GetFileSystemEntries(path); // リンク先を含め、その場で列挙権限を検査。
                root.CanonicalPath = snapshot.CanonicalPath;
                root.Identity = snapshot.Identity;
            }
            else
            {
                FileSnapshot snapshot = FileSnapshot.Capture(path);
                root.CanonicalPath = snapshot.CanonicalPath;
                root.Identity = snapshot.Identity;
            }
            return root;
        }
    }

    /// <summary>安全な Windows パスと ZIP 内相対パスの検査を集約する。</summary>
    private static class PathRules
    {
        /// <summary>通常のドライブ絶対パスまたは UNC のみ受理。デバイス・ADS パスを拒否する。</summary>
        internal static string FullPath(string text)
        {
            if (text == null) throw new AppError(87, "パスが null です。");
            string path = text.Trim();
            if (path.Length >= 2 && path[0] == '"' && path[path.Length - 1] == '"')
                path = path.Substring(1, path.Length - 2);
            if (path.Length == 0 || path.IndexOf('"') >= 0 || path.IndexOf('\0') >= 0)
                throw new AppError(87, "パスが空、または引用符が不正です。");
            path = path.Replace('/', '\\');
            if (path.StartsWith(@"\\?\", StringComparison.Ordinal) || path.StartsWith(@"\\.\", StringComparison.Ordinal))
                throw new AppError(87, "デバイスパスと拡張長パスは受け付けません: " + path);
            bool drive = path.Length >= 3 && IsAsciiLetter(path[0]) && path[1] == ':' && path[2] == '\\';
            bool unc = path.StartsWith(@"\\", StringComparison.Ordinal);
            if (!drive && !unc) throw new AppError(87, "フルパスを指定してください: " + path);
            if (path.IndexOf(':', drive ? 2 : 0) >= 0)
                throw new AppError(87, "代替データストリームを含むパスは受け付けません: " + path);
            string full = System.IO.Path.GetFullPath(path);
            string root = System.IO.Path.GetPathRoot(full);
            if (String.IsNullOrEmpty(root)) throw new AppError(87, "ルートのないパスです: " + path);
            if (unc)
            {
                string[] pieces = root.Trim('\\').Split('\\');
                if (pieces.Length < 2 || pieces[0].Length == 0 || pieces[1].Length == 0)
                    throw new AppError(87, "UNC パスにはサーバー名と共有名が必要です: " + path);
            }
            while (full.Length > root.Length && full.EndsWith("\\", StringComparison.Ordinal))
                full = full.Substring(0, full.Length - 1);
            string suffix = full.Substring(root.Length);
            if (suffix.Length > 0)
                foreach (string component in suffix.Split('\\')) ValidateComponent(component, true);
            return full;
        }

        /// <summary>出力先の解決後の実体パスを保護する前に再解析の残留/差替えを検査。入力には使わない。</summary>
        internal static void RejectReparseAncestors(string fullPath)
        {
            string current = System.IO.Path.GetPathRoot(fullPath);
            CheckOrdinaryPath(current);
            string rest = fullPath.Substring(current.Length);
            if (rest.Length == 0) return;
            foreach (string component in rest.Split('\\'))
            {
                current = System.IO.Path.Combine(current, component);
                CheckOrdinaryPath(current);
            }
        }

        /// <summary>再解析ポイントを拒否する。属性取得自体の失敗も例外として伝播する。</summary>
        private static void CheckOrdinaryPath(string path)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new AppError(50, "出力先の解決後パスに再解析ポイントがあります。差し替えの可能性があるため中止します: " + path);
        }

        /// <summary>ZIP 内に使える安全な相対パスを返す。ディレクトリは末尾を / に統一する。</summary>
        internal static string ArchivePath(string name, bool directory)
        {
            if (String.IsNullOrEmpty(name)) throw new AppError(13, "ZIP 内の名前が空です。");
            string path = name.Replace('\\', '/');
            if (path.StartsWith("/", StringComparison.Ordinal)) throw new AppError(13, "ZIP 内に絶対パスがあります: " + name);
            if (directory && path.EndsWith("/", StringComparison.Ordinal)) path = path.Substring(0, path.Length - 1);
            foreach (string component in path.Split('/')) ValidateComponent(component);
            string result = path + (directory ? "/" : "");
            if (Utf8.GetByteCount(result) > UInt16.MaxValue)
                throw new AppError(206, "UTF-8 の ZIP 内パスが 65,535 bytes を超えます: " + name);
            return result;
        }

        /// <summary>Windows 展開時に別名・上位移動・デバイス・ADS となる成分を拒否する。</summary>
        private static void ValidateComponent(string value, bool allowEmpty = false)
        {
            if (allowEmpty == false && value.Length == 0)
            {
                throw new AppError(13, "安全でないパス成分です: [" + value + "]");
            }
            if (/*value.Length == 0 || */value == "." || value == ".." ||
                value.EndsWith(" ", StringComparison.Ordinal) || value.EndsWith(".", StringComparison.Ordinal))
                throw new AppError(13, "安全でないパス成分です: [" + value + "]");
            foreach (char c in value)
                if (c < 32 || c == 127 || "<>:\"/\\|?*".IndexOf(c) >= 0)
                    throw new AppError(13, "Windows で安全に展開できない名前です: " + value);
            string device = value.Split('.')[0].ToUpperInvariant();
            if (device == "CON" || device == "PRN" || device == "AUX" || device == "NUL" ||
                device == "CLOCK$" || device == "CONIN$" || device == "CONOUT$" ||
                (device.Length == 4 && (device.StartsWith("COM", StringComparison.Ordinal) ||
                device.StartsWith("LPT", StringComparison.Ordinal)) &&
                ("123456789¹²³".IndexOf(device[3]) >= 0)))
                throw new AppError(13, "Windows の予約デバイス名は使用できません: " + value);
        }

        /// <summary>物理パスが指定フォルダ自身またはその配下かを境界付きで比較する。</summary>
        internal static bool IsWithin(string path, string directory)
        {
            string parent = directory.TrimEnd('\\');
            return String.Equals(path.TrimEnd('\\'), parent, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(parent + "\\", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>選択されたフォルダの名前。ドライブのルートだけは C のような名前にする。</summary>
        internal static string RootName(string path)
        {
            string result = System.IO.Path.GetFileName(path.TrimEnd('\\'));
            if (result.Length == 2 && result[1] == ':') result = result.Substring(0, 1);
            return ArchivePath(result, false);
        }

        /// <summary>明示指定 ZIP の末尾の拡張子だけを除去し、安全な仮想ルート名を返す。</summary>
        /// <param name="path">拡張子が .zip（大文字小文字不問）と確認済みの入力フルパス。</param>
        /// <returns>末尾 / を付けない仮想ディレクトリ名。空名・不正な名前は例外。</returns>
        internal static string ZipRootName(string path)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (String.IsNullOrEmpty(name))
                throw new AppError(13, "入力 ZIP の拡張子 .zip を除去すると仮想ディレクトリ名が空になります: " + path);
            // a.zip.zip は a.zip のまま。a..zip → a. 等は既存の安全性検査で拒否する。
            return ArchivePath(name, false);
        }

        private static bool IsAsciiLetter(char c) { return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'); }
    }

    /// <summary>リンク解決後のディレクトリ情報。実体識別子は循環と入出力の重なりの検査用。</summary>
    private sealed class DirectorySnapshot
    {
        internal string CanonicalPath;
        internal string Identity;
        internal uint DosTime;

        /// <summary>path のリンク先を開き、実体パス・ボリューム/ファイル ID・更新日時を返す。</summary>
        internal static DirectorySnapshot Capture(string path)
        {
            using (SafeFileHandle handle = Native.OpenReadDirectory(path))
            {
                Native.FileInformation info = Native.Information(handle, path);
                DirectorySnapshot result = new DirectorySnapshot();
                result.CanonicalPath = Native.FinalPath(handle);
                // 一部のファイルシステムが未提供として返す 0 の ID は比較に使わない。
                result.Identity = info.FileIndexHigh == 0 && info.FileIndexLow == 0 ? null : Native.Identity(info);
                result.DosTime = ZipTime.Encode(DateTime.FromFileTimeUtc(info.LastWriteTime.ToInt64()).ToLocalTime());
                return result;
            }
        }
    }

    /// <summary>物理ファイルの識別子と列挙時の状態。試行間に元データが変わっていないか照合する。</summary>
    private sealed class FileSnapshot
    {
        internal string Path;
        internal string CanonicalPath;
        internal string Identity;
        internal long Length;
        internal long LastWriteFileTime;

        /// <summary>読み取りハンドルで存在・種類・読取権限を確認し、状態を採取する。</summary>
        internal static FileSnapshot Capture(string path)
        {
            using (FileStream stream = Native.OpenReadFile(path))
            {
                Native.FileInformation info = Native.Information(stream.SafeFileHandle, path);
                Native.RequireOrdinaryFile(info, path);
                FileSnapshot result = new FileSnapshot();
                result.Path = path;
                result.CanonicalPath = Native.FinalPath(stream.SafeFileHandle);
                result.Identity = Native.Identity(info);
                result.Length = Native.FileLength(info);
                result.LastWriteFileTime = info.LastWriteTime.ToInt64();
                return result;
            }
        }

        /// <summary>状態を照合した読み取りストリームを返す。呼出側が Dispose する。</summary>
        internal FileStream Open()
        {
            FileStream stream = Native.OpenReadFile(Path);
            try
            {
                Native.FileInformation info = Native.Information(stream.SafeFileHandle, Path);
                Native.RequireOrdinaryFile(info, Path);
                if (Native.Identity(info) != Identity || Native.FileLength(info) != Length ||
                    info.LastWriteTime.ToInt64() != LastWriteFileTime ||
                    !String.Equals(Native.FinalPath(stream.SafeFileHandle), CanonicalPath, StringComparison.OrdinalIgnoreCase))
                    throw new AppError(13, "列挙後に入力ファイルが変更または置換されました: " + Path);
                return stream;
            }
            catch { stream.Dispose(); throw; }
        }

        /// <summary>ファイル時刻を ZIP の DOS 時刻に変換する。</summary>
        internal uint DosStamp()
        {
            return ZipTime.Encode(DateTime.FromFileTimeUtc(LastWriteFileTime).ToLocalTime());
        }
    }

    /// <summary>再圧縮に必要な 1 ファイルまたは 1 空フォルダ。圧縮データの所有者は予算管理キャッシュ。</summary>
    private sealed class SourceItem
    {
        internal string ZipPath;
        internal byte[] NameBytes;
        internal string Origin;
        internal bool IsDirectory;
        internal long Length;
        internal int Ordinal;
        internal uint DosTime;
        internal FileSnapshot Physical;
        internal ZipSource Archive;
        internal ZipEntry ArchiveEntry;
        internal Measurement Measurement;
        internal CacheEntry CachedData;
#if DNNT_SELF_TEST
        internal byte[] SelfTestBytes;
#endif

        /// <summary>キャッシュ採用時にも実体・サイズ・日時を照合し、転送中は入力を読取ハンドルで固定する。</summary>
        internal Stream OpenValidationStream()
        {
            if (IsDirectory) return null;
#if DNNT_SELF_TEST
            if (SelfTestBytes != null) return null;
#endif
            return Archive == null ? Physical.Open() : Archive.OpenValidationStream();
        }

        /// <summary>内容を平文として target に流し、CRC と SHA-256 を返す。一時展開はしない。</summary>
        internal ContentResult CopyPlaintextTo(Stream target)
        {
            if (IsDirectory) return new ContentResult(0, 0, new byte[0]);
#if DNNT_SELF_TEST
            if (SelfTestBytes != null)
                using (MemoryStream memory = new MemoryStream(SelfTestBytes, false)) return Transfer(memory, target, Length);
#endif
            if (Archive != null) return Archive.CopyEntry(ArchiveEntry, target);
            using (FileStream source = Physical.Open())
                return Transfer(source, target, Length);
        }
    }

    /// <summary>内容を検証した結果。Digest は再試行間の内容変更を検出するための SHA-256。</summary>
    private sealed class ContentResult
    {
        internal readonly long Length;
        internal readonly uint Crc;
        internal readonly byte[] Digest;
        internal ContentResult(long length, uint crc, byte[] digest)
        { Length = length; Crc = crc; Digest = digest; }
    }

    /// <summary>事前圧縮で確定した圧縮量・内容検証値。圧縮データ自体は別の予算付きキャッシュで管理。</summary>
    private sealed class Measurement
    {
        internal long CompressedSize;
        internal long UncompressedSize;
        internal uint Crc;
        internal byte[] Digest;
        internal ushort Method;
    }

    /// <summary>圧縮済みデータの 64 KiB 以下の断片。Count 以降の未使用容量も予算に含める。</summary>
    private sealed class CacheBlock
    {
        internal readonly byte[] Bytes = new byte[BufferSize];
        internal int Count;
    }

    /// <summary>1 入力のキャッシュ。削除時は Live=false とし、進行中の捕捉も計測専用へ切り替える。</summary>
    private sealed class CacheEntry
    {
        internal LinkedList<CacheBlock> Blocks = new LinkedList<CacheBlock>();
        internal SourceItem Owner;
        internal LinkedListNode<CacheEntry> Node;
        internal bool Live;
        internal bool Complete;
    }

    /// <summary>FIFO キャッシュ。作成途中も同じ予算で計上し、古いエントリから丸ごと捨てる。</summary>
    private sealed class CompressedCache : IDisposable
    {
        // 配列実容量に加え、管理オブジェクト・参照・リンクの分を保守的に計上する。
        private const long EntryCharge = 512L;
        private const long BlockCharge = BufferSize + 128L;
        private readonly object gate = new object();
        private readonly LinkedList<CacheEntry> fifo = new LinkedList<CacheEntry>();
        private readonly Func<long> availableMemory;
        private long used;
        private long limit;
        private bool disabled;
        internal long UsedBytes { get { lock (gate) return used; } }
        internal long LimitBytes { get { lock (gate) return limit; } }

        /// <summary>available は使用可能メモリ bytes の取得関数。テストでは固定値を注入できる。</summary>
        internal CompressedCache(Func<long> available)
        {
            if (available == null) throw new ArgumentNullException("available");
            availableMemory = available;
            Trim();
        }

        /// <summary>オーバーフローさせずに floor(bytes * 0.8) を計算する。</summary>
        internal static long EightyPercent(long bytes)
        {
            return bytes <= 0 ? 0 : bytes / 5L * 4L + bytes % 5L * 4L / 5L;
        }

        /// <summary>現在の空きメモリを再取得し、予算超過なら古いデータから捨てる。gate 内でのみ呼ぶ。</summary>
        private void Refresh()
        {
            limit = disabled ? 0L : EightyPercent(availableMemory());
            while (used > limit && fifo.First != null) Release(fifo.First.Value);
        }

        /// <summary>capacity の追加を予約できるまで FIFO 削除する。確保後も空き量を再確認する。</summary>
        private bool MakeRoom(long capacity)
        {
            Refresh();
            while (capacity > limit - used && fifo.First != null) Release(fifo.First.Value);
            return capacity <= limit - used;
        }

        /// <summary>エントリの所有参照を外し、保持配列を解放可能にする。呼出側の古い参照も無効化する。</summary>
        private void Release(CacheEntry entry)
        {
            if (entry == null || !entry.Live) return;
            used -= EntryCharge;
            foreach (CacheBlock block in entry.Blocks)
            {
                Array.Clear(block.Bytes, 0, block.Bytes.Length);
                used -= BlockCharge;
            }
            entry.Blocks.Clear();
            fifo.Remove(entry.Node);
            entry.Node = null;
            entry.Live = false;
            if (entry.Owner != null && Object.ReferenceEquals(entry.Owner.CachedData, entry)) entry.Owner.CachedData = null;
            entry.Owner = null;
        }

        /// <summary>計測開始時に登録する。予算不足なら null を返し、圧縮はキャッシュなしで続行する。</summary>
        internal CacheEntry Begin(SourceItem item)
        {
            lock (gate)
            {
                if (item.IsDirectory || !MakeRoom(EntryCharge)) return null;
                try
                {
                    CacheEntry entry = new CacheEntry();
                    entry.Owner = item;
                    entry.Node = fifo.AddLast(entry);
                    entry.Live = true;
                    used += EntryCharge;
                    item.CachedData = entry;
                    Refresh();
                    return entry;
                }
                catch (OutOfMemoryException) { Disable(); return null; }
            }
        }

        /// <summary>圧縮断片を複製する。gate により並列登録・追記・追出しの競合を防止する。</summary>
        internal void Append(CacheEntry entry, byte[] buffer, int offset, int count)
        {
            if (entry == null) return;
            lock (gate)
            {
                Refresh();
                while (count > 0 && entry.Live)
                {
                    CacheBlock block = entry.Blocks.Last == null ? null : entry.Blocks.Last.Value;
                    if (block == null || block.Count == block.Bytes.Length)
                    {
                        // MakeRoom は自分自身を含む最古エントリを消すことがある。
                        if (!MakeRoom(BlockCharge) || !entry.Live) { Release(entry); return; }
                        try
                        {
                            block = new CacheBlock();
                            entry.Blocks.AddLast(block);
                            used += BlockCharge;
                        }
                        catch (OutOfMemoryException) { Disable(); return; }
                        Refresh();
                        if (!entry.Live) return;
                    }
                    int length = Math.Min(count, block.Bytes.Length - block.Count);
                    Buffer.BlockCopy(buffer, offset, block.Bytes, block.Count, length);
                    block.Count += length;
                    offset += length;
                    count -= length;
                }
            }
        }

        /// <summary>捕捉が正常終了したことを通知する。不完全なキャッシュは出力に使用しない。</summary>
        internal void Complete(CacheEntry entry)
        {
            lock (gate) { Refresh(); if (entry != null && entry.Live) entry.Complete = true; }
        }

        /// <summary>キャッシュから転送して消費する。途中で予算が下がった場合も false を返す。</summary>
        /// <remarks>false 時は一部が転送済みの場合がある。呼出側は暗号ヘッダから巻き戻して再圧縮する。</remarks>
        internal bool TryCopyTo(SourceItem item, Stream target)
        {
            lock (gate)
            {
                CacheEntry entry = item.CachedData;
                Refresh();
                if (entry == null || !entry.Live || !entry.Complete) return false;
                try
                {
                    while (entry.Blocks.First != null)
                    {
                        CheckCancellation();
                        Refresh();
                        if (!entry.Live) return false;
                        CacheBlock block = entry.Blocks.First.Value;
                        target.Write(block.Bytes, 0, block.Count);
                        Array.Clear(block.Bytes, 0, block.Bytes.Length);
                        entry.Blocks.RemoveFirst();
                        used -= BlockCharge;
                    }
                    return true;
                }
                finally { Release(entry); }
            }
        }

        /// <summary>失敗した捕捉を取り消す。すでに予算で追い出されたエントリでも安全。</summary>
        internal void Discard(CacheEntry entry) { lock (gate) Release(entry); }
        /// <summary>処理段階の境界でも上限を再評価する。</summary>
        internal void Trim() { lock (gate) Refresh(); }
        /// <summary>キャッシュの配列確保に失敗した場合だけ無効化する。入力検証・計測は続行する。</summary>
        private void Disable()
        {
            disabled = true;
            limit = 0;
            while (fifo.First != null) Release(fifo.First.Value);
        }
        /// <summary>全圧縮データを破棄する。OS へのメモリ返却時期自体は CLR の GC が決める。</summary>
        public void Dispose() { lock (gate) Disable(); }
    }

    /// <summary>圧縮結果のバイト数を必ず数え、予算がある間だけ断片キャッシュにも渡す書込先。</summary>
    private sealed class CaptureStream : Stream
    {
        private readonly CompressedCache cache;
        private readonly CacheEntry entry;
        private long length;
        private bool complete;
        internal CaptureStream(CompressedCache owner, SourceItem item) { cache = owner; entry = owner.Begin(item); }
        internal void Complete() { cache.Complete(entry); complete = true; }
        public override bool CanRead { get { return false; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return true; } }
        public override long Length { get { return length; } }
        public override long Position { get { return length; } set { throw new NotSupportedException(); } }
        public override void Flush() { }
        public override void Write(byte[] buffer, int offset, int count)
        {
            ValidateBuffer(buffer, offset, count);
            CheckCancellation();
            length = checked(length + count);
            cache.Append(entry, buffer, offset, count);
        }
        public override int Read(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        protected override void Dispose(bool disposing)
        {
            if (disposing && !complete) cache.Discard(entry);
            base.Dispose(disposing);
        }
    }

    /// <summary>圧縮済みサイズを ZIP の正確なレコード配置へ変換する純粋な計算状態。</summary>
    private struct ZipLayout
    {
        internal long LocalBytes;
        internal long CentralBytes;
        internal int Count;
        internal int Zip64Entries;
        internal long Size { get { return ZipOutput.TotalSize(LocalBytes, CentralBytes, Count, Zip64Entries != 0); } }

        /// <summary>item を offset に置いた場合のレコード。実出力と同じ EntryRecord の長さ計算を共用する。</summary>
        internal static EntryRecord Record(SourceItem item, long offset)
        {
            Measurement measurement = item.Measurement;
            if (measurement == null) throw new AppError(13, "配置計算前に圧縮量が確定していません: " + item.Origin);
            EntryRecord record = new EntryRecord();
            record.Source = item;
            record.Offset = offset;
            record.CompressedSize = measurement.CompressedSize;
            record.UncompressedSize = measurement.UncompressedSize;
            record.Method = measurement.Method;
            record.End = checked(offset + record.LocalLength);
            return record;
        }

        /// <summary>この値を変更せず、item 追加後の状態を返す。ZIP64 の位置・個数境界も含む。</summary>
        internal ZipLayout With(SourceItem item)
        {
            EntryRecord record = Record(item, LocalBytes);
            ZipLayout result = this;
            result.LocalBytes = record.End;
            result.CentralBytes = checked(CentralBytes + record.CentralLength);
            result.Count = checked(Count + 1);
            result.Zip64Entries = checked(Zip64Entries + (record.Uses64 ? 1 : 0));
            return result;
        }
    }

    /// <summary>1 個の ZIP の計画。Items の順序も実書込順序として保持する。</summary>
    private sealed class PlannedPart
    {
        internal readonly List<SourceItem> Items = new List<SourceItem>();
        internal ZipLayout Layout;
        /// <summary>item を末尾に追加し、その追加を反映したサイズ状態を確定する。</summary>
        internal void Add(SourceItem item) { Layout = Layout.With(item); Items.Add(item); }
        /// <summary>並びの変更後に ZIP64 のオフセット依存情報を含めて再計算する。</summary>
        internal void Rebuild()
        {
            Layout = new ZipLayout();
            foreach (SourceItem item in Items) Layout = Layout.With(item);
        }

        /// <summary>removeIndex を削除し added を末尾へ足す仮計画。-1 / null はその操作なし。</summary>
        internal ZipLayout Changed(int removeIndex, SourceItem added)
        {
            EntryRecord removed = removeIndex < 0 ? null : ZipLayout.Record(Items[removeIndex], 0);
            EntryRecord inserted = added == null ? null : ZipLayout.Record(added, 0);
            long local = checked(Layout.LocalBytes - (removed == null ? 0L : removed.LocalLength) +
                (inserted == null ? 0L : inserted.LocalLength));
            if (Layout.LocalBytes < Zip32Limit && local < Zip32Limit)
            {
                // すべてのオフセットが 32 bit に収まる場合だけ、定数時間の差分計算が可能。
                ZipLayout result = Layout;
                result.LocalBytes = local;
                result.CentralBytes = checked(Layout.CentralBytes - (removed == null ? 0L : removed.CentralLength) +
                    (inserted == null ? 0L : inserted.CentralLength));
                result.Count += (inserted == null ? 0 : 1) - (removed == null ? 0 : 1);
                result.Zip64Entries += (inserted != null && inserted.Uses64 ? 1 : 0) -
                    (removed != null && removed.Uses64 ? 1 : 0);
                return result;
            }
            ZipLayout full = new ZipLayout();
            for (int i = 0; i < Items.Count; i++) if (i != removeIndex) full = full.With(Items[i]);
            if (added != null) full = full.With(added);
            return full;
        }
    }

    /// <summary>最大 19 個の配置計画。正確な完成サイズを評価し、容量優先と均等化を切り替える。</summary>
    private sealed class PackingPlan
    {
        internal readonly List<PlannedPart> Parts = new List<PlannedPart>();
        internal bool Relaxed;
        private const int RefinementPasses = 12;
        private const int CandidateSamples = 32;
        private const long RefinementBudget = 2000000L;

        /// <summary>通常容量で複数の貪欲解を比較し、19 個に収まらなければ均等配分と局所改善を行う。</summary>
        internal static PackingPlan Build(List<SourceItem> items)
        {
            CheckCancellation();
            if (items.Count == 0)
            {
                PackingPlan empty = new PackingPlan();
                empty.Parts.Add(new PlannedPart());
                return empty;
            }
            List<SourceItem> descending = new List<SourceItem>(items);
            Dictionary<SourceItem, int> positions = new Dictionary<SourceItem, int>();
            for (int i = 0; i < items.Count; i++) positions.Add(items[i], i);
            descending.Sort(delegate (SourceItem first, SourceItem second)
            {
                long a = new ZipLayout().With(first).Size;
                long b = new ZipLayout().With(second).Size;
                int comparison = b.CompareTo(a);
                return comparison != 0 ? comparison : positions[first].CompareTo(positions[second]);
            });
            PackingPlan best = Capacity(items, MAX_ONE_ZIP_FILE_SIZE, false);
            best = PreferCompact(best, Capacity(descending, MAX_ONE_ZIP_FILE_SIZE, false));
            best = PreferCompact(best, Capacity(descending, MAX_ONE_ZIP_FILE_SIZE, true));
            if (best == null)
            {
                Console.WriteLine("通常容量での配置が 19 個を超える見込みのため、最大 19 個へ均等化します...");
                best = Balanced(descending);
                // 中間容量の best-fit も比較する。探索は有限であり、数学的な大域最適を保証しない。
                long low = MAX_ONE_ZIP_FILE_SIZE;
                long high = best.Maximum();
                for (int attempt = 0; attempt < 12 && high > low + 1L; attempt++)
                {
                    CheckCancellation();
                    long middle = low + (high - low) / 2L;
                    PackingPlan trial = Capacity(descending, middle, true);
                    if (trial == null) low = middle;
                    else
                    {
                        high = middle;
                        while (trial.Parts.Count < Math.Min(MAX_NUM_ZIP_FILES, items.Count)) trial.Parts.Add(new PlannedPart());
                        if (CompareBalance(trial, best) < 0) best = trial;
                    }
                }
                best.ImproveBalance();
                best.Parts.RemoveAll(delegate (PlannedPart part) { return part.Items.Count == 0; });
            }
            if (best.CapacityLegal()) best.Compact();
            best.Relaxed = !best.CapacityLegal();
            best.Validate(items);
            return best;
        }

        /// <summary>追加後に最初に収まる ZIP、または最も残りの少ない ZIP を選ぶ。19 個で打切る。</summary>
        private static PackingPlan Capacity(List<SourceItem> items, long capacity, bool bestFit)
        {
            PackingPlan result = new PackingPlan();
            foreach (SourceItem item in items)
            {
                CheckCancellation();
                int selected = -1;
                long fullest = -1;
                for (int i = 0; i < result.Parts.Count; i++)
                {
                    long size = result.Parts[i].Layout.With(item).Size;
                    if (size <= capacity && (!bestFit || size > fullest))
                    {
                        selected = i;
                        fullest = size;
                        if (!bestFit) break;
                    }
                }
                if (selected < 0)
                {
                    if (result.Parts.Count >= MAX_NUM_ZIP_FILES) return null;
                    selected = result.Parts.Count;
                    result.Parts.Add(new PlannedPart());
                }
                // 単独でも容量超過の入力は、この段階では単独 ZIP とする。
                result.Parts[selected].Add(item);
            }
            return result;
        }

        /// <summary>同じ容量制約なら ZIP 数が少ない計画を採る。同数なら先の計画を保持する。</summary>
        private static PackingPlan PreferCompact(PackingPlan first, PackingPlan second)
        {
            if (first == null) return second;
            if (second == null) return first;
            return second.Parts.Count < first.Parts.Count ? second : first;
        }

        /// <summary>大きいファイルから、追加後の完成サイズが最小になる ZIP へ置く (LPT 型)。</summary>
        private static PackingPlan Balanced(List<SourceItem> descending)
        {
            PackingPlan result = new PackingPlan();
            for (int i = 0; i < Math.Min(MAX_NUM_ZIP_FILES, descending.Count); i++) result.Parts.Add(new PlannedPart());
            foreach (SourceItem item in descending)
            {
                CheckCancellation();
                int selected = 0;
                long smallest = Int64.MaxValue;
                for (int i = 0; i < result.Parts.Count; i++)
                {
                    long size = result.Parts[i].Layout.With(item).Size;
                    if (size < smallest) { selected = i; smallest = size; }
                }
                result.Parts[selected].Add(item);
            }
            return result;
        }

        /// <summary>容量内の先行 ZIP へ後続候補を移す。移せる未処理ファイルが残らないところまで詰める。</summary>
        private void Compact()
        {
            for (int i = 0; i < Parts.Count; i++)
            {
                PlannedPart destination = Parts[i];
                if (destination.Layout.Size > MAX_ONE_ZIP_FILE_SIZE) continue;
                for (int j = i + 1; j < Parts.Count; j++)
                {
                    PlannedPart source = Parts[j];
                    List<SourceItem> remaining = new List<SourceItem>();
                    foreach (SourceItem item in source.Items)
                    {
                        CheckCancellation();
                        if (destination.Layout.With(item).Size <= MAX_ONE_ZIP_FILE_SIZE) destination.Add(item);
                        else remaining.Add(item);
                    }
                    if (remaining.Count != source.Items.Count)
                    {
                        source.Items.Clear();
                        source.Items.AddRange(remaining);
                        source.Rebuild();
                    }
                }
            }
            Parts.RemoveAll(delegate (PlannedPart part) { return part.Items.Count == 0; });
        }

        /// <summary>31.5 MB 超過がすべて単一エントリ由来なら、通常容量ルールを満たす。</summary>
        private bool CapacityLegal()
        {
            foreach (PlannedPart part in Parts)
                if (part.Layout.Size > MAX_ONE_ZIP_FILE_SIZE && part.Items.Count != 1) return false;
            return true;
        }

        /// <summary>均等化比較用の、完成サイズ降順の配列。メタデータ量の差も含む。</summary>
        private long[] SortedSizes()
        {
            long[] sizes = new long[Parts.Count];
            for (int i = 0; i < sizes.Length; i++) sizes[i] = Parts[i].Layout.Size;
            Array.Sort(sizes);
            Array.Reverse(sizes);
            return sizes;
        }
        /// <summary>完成サイズを大きい順に比較する。負数なら first の最大側のサイズ列が小さい。</summary>
        private static int CompareBalance(PackingPlan first, PackingPlan second)
        {
            long[] a = first.SortedSizes();
            long[] b = second.SortedSizes();
            for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
            {
                int comparison = a[i].CompareTo(b[i]);
                if (comparison != 0) return comparison;
            }
            return a.Length.CompareTo(b.Length);
        }
        /// <summary>計画全体で最大の完成 ZIP サイズを bytes で返す。</summary>
        internal long Maximum()
        {
            long maximum = 0;
            foreach (PlannedPart part in Parts) maximum = Math.Max(maximum, part.Layout.Size);
            return maximum;
        }

        /// <summary>先頭・末尾を含む最大 32 候補を等間隔で選び、非常に多数の入力でも探索を有限にする。</summary>
        private static List<int> Samples(int count)
        {
            List<int> indices = new List<int>();
            int samples = Math.Min(CandidateSamples, count);
            for (int i = 0; i < samples; i++)
                indices.Add(samples == 1 ? 0 : (int)((long)i * (count - 1) / (samples - 1)));
            return indices;
        }

        /// <summary>最大側から最小側への単独移動・二者交換で、対の最大サイズと偏りを改善する。</summary>
        private void ImproveBalance()
        {
            long budget = RefinementBudget;
            for (int pass = 0; pass < RefinementPasses && budget > 0; pass++)
            {
                int bestFirst = -1, bestSecond = -1, bestRemove = -1, bestExchange = -1;
                long bestGain = -1, bestGap = -1;
                for (int a = 0; a < Parts.Count && budget > 0; a++)
                    for (int b = 0; b < Parts.Count && budget > 0; b++)
                    {
                        CheckCancellation();
                        PlannedPart heavy = Parts[a], light = Parts[b];
                        long oldMaximum = heavy.Layout.Size;
                        long oldGap = oldMaximum - light.Layout.Size;
                        if (a == b || oldGap <= 0) continue;
                        List<int> firstIndices = Samples(heavy.Items.Count);
                        List<int> secondIndices = Samples(light.Items.Count);
                        secondIndices.Insert(0, -1); // -1 は交換でなく単独移動。
                        foreach (int remove in firstIndices)
                        {
                            if (budget <= 0) break;
                            foreach (int exchange in secondIndices)
                            {
                                if (budget <= 0) break;
                                if (exchange < 0 && heavy.Items.Count <= 1) continue;
                                SourceItem outgoing = heavy.Items[remove];
                                SourceItem incoming = exchange < 0 ? null : light.Items[exchange];
                                // ZIP64 オフセットがある計画では再計算の件数も探索予算へ計上する。
                                budget -= heavy.Layout.LocalBytes >= Zip32Limit || light.Layout.LocalBytes >= Zip32Limit ?
                                    (long)heavy.Items.Count + light.Items.Count + 2L : 1L;
                                ZipLayout newHeavy = heavy.Changed(remove, incoming);
                                ZipLayout newLight = light.Changed(exchange, outgoing);
                                long maximum = Math.Max(newHeavy.Size, newLight.Size);
                                long gap = Math.Abs(newHeavy.Size - newLight.Size);
                                long gain = oldMaximum - maximum;
                                long gapGain = oldGap - gap;
                                if (gain < 0 || (gain == 0 && gapGain <= 0)) continue;
                                if (gain > bestGain || (gain == bestGain && gapGain > bestGap))
                                {
                                    bestFirst = a; bestSecond = b; bestRemove = remove; bestExchange = exchange;
                                    bestGain = gain; bestGap = gapGain;
                                }
                            }
                        }
                    }
                if (bestFirst < 0) break;
                PlannedPart first = Parts[bestFirst], second = Parts[bestSecond];
                SourceItem moving = first.Items[bestRemove];
                SourceItem returning = bestExchange < 0 ? null : second.Items[bestExchange];
                first.Items.RemoveAt(bestRemove);
                if (returning != null) second.Items.RemoveAt(bestExchange);
                if (returning != null) first.Items.Add(returning);
                second.Items.Add(moving);
                first.Rebuild();
                second.Rebuild();
            }
        }

        /// <summary>生成上限・各入力のちょうど 1 回の出力・サイズ式を、削除前と出力開始時に再確認する。</summary>
        internal void Validate(List<SourceItem> expected)
        {
            if (Parts.Count < 1 || Parts.Count > MAX_NUM_ZIP_FILES)
                throw new AppError(13, "ZIP 生成数の内部エラー: 最大 " + MAX_NUM_ZIP_FILES + " 個です。");
            HashSet<SourceItem> found = new HashSet<SourceItem>();
            foreach (PlannedPart part in Parts)
            {
                CheckCancellation();
                if (part.Items.Count == 0 && !(Parts.Count == 1 && expected.Count == 0))
                    throw new AppError(13, "空の分割計画が残っています。");
                ZipLayout layout = new ZipLayout();
                foreach (SourceItem item in part.Items)
                {
                    if (!found.Add(item)) throw new AppError(13, "同じ入力が複数の ZIP に配置されています: " + item.ZipPath);
                    layout = layout.With(item);
                }
                if (layout.Size != part.Layout.Size) throw new AppError(13, "配置サイズの内部不一致。");
            }
            if (found.Count != expected.Count) throw new AppError(13, "配置したファイル数が入力数と一致しません。");
            foreach (SourceItem item in expected)
                if (!found.Contains(item)) throw new AppError(13, "配置されていない入力があります: " + item.ZipPath);
        }

        /// <summary>計画結果とサイズ超過理由を、既存ファイルを削除する前に表示する。</summary>
        internal void Show(OutputNames names)
        {
            Console.WriteLine("配置計画: {0} 個 (絶対上限 {1} 個)", Parts.Count, MAX_NUM_ZIP_FILES);
            for (int i = 0; i < Parts.Count; i++)
                Console.WriteLine("  {0}: {1} bytes / {2} エントリ{3}", System.IO.Path.GetFileName(names.PartPath(i + 1)),
                    FormatNumber(Parts[i].Layout.Size), Parts[i].Items.Count,
                    Parts[i].Layout.Size > MAX_ONE_ZIP_FILE_SIZE ? " (目安容量超過)" : "");
            if (Relaxed) Console.WriteLine("警告: 最大 19 個を優先し、容量の目安を緩和して均等化しました。ファイル内容は分割しません。");
            else if (Maximum() > MAX_ONE_ZIP_FILE_SIZE)
                Console.WriteLine("警告: 単独のファイルだけで目安容量を超える ZIP があります。");
        }
    }

    /// <summary>名前空間の 1 ノード。ディレクトリ同士は統合できるが、ファイルの衝突は許可しない。</summary>
    private sealed class NameNode
    {
        internal bool IsDirectory;
        internal bool HasChildren;
        internal string Path;
        internal string Origin;
        internal uint DosTime;
    }

    /// <summary>すべての入力を同一の相対名前空間に集めた一覧。</summary>
    private sealed class Manifest
    {
        internal readonly List<SourceItem> Items = new List<SourceItem>();
        internal readonly HashSet<string> InputIdentities = new HashSet<string>(StringComparer.Ordinal);
        internal readonly HashSet<string> InputPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, NameNode> nodes = new Dictionary<string, NameNode>(StringComparer.OrdinalIgnoreCase);
        internal int FileCount;
        internal long TotalBytes;
        internal int CycleCutCount;

        /// <summary>全入力を列挙・検査する。出力削除より前に重複・範囲の問題を発見する。</summary>
        internal static Manifest Build(List<InputRoot> roots, OutputNames output)
        {
            Manifest result = new Manifest();
            DirectorySnapshot outputDirectory = DirectorySnapshot.Capture(output.DirectoryPath);
            foreach (InputRoot root in roots)
            {
                CheckCancellation();
                if (root.IsDirectory)
                {
                    DirectorySnapshot directory = DirectorySnapshot.Capture(root.Path);
                    if (directory.Identity != root.Identity ||
                        !String.Equals(directory.CanonicalPath, root.CanonicalPath, StringComparison.OrdinalIgnoreCase))
                        throw new AppError(13, "入力フォルダまたはリンク先が変更されています: " + root.Path);
                    result.AddDirectoryTree(root.Path, PathRules.RootName(root.Path), outputDirectory);
                }
                else
                {
                    FileSnapshot snapshot = FileSnapshot.Capture(root.Path);
                    if (snapshot.Identity != root.Identity ||
                        !String.Equals(snapshot.CanonicalPath, root.CanonicalPath, StringComparison.OrdinalIgnoreCase))
                        throw new AppError(13, "入力ファイルまたはリンク先が変更されています: " + root.Path);
                    result.Protect(snapshot);
                    if (String.Equals(System.IO.Path.GetExtension(root.Path), ".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        ZipSource archive = ZipSource.Load(snapshot);
                        string rootName = PathRules.ZipRootName(root.Path);
                        result.AddDirectory(rootName, root.Path, snapshot.DosStamp());
                        foreach (ZipEntry entry in archive.Entries)
                        {
                            CheckCancellation();
                            string relative = PathRules.ArchivePath(entry.Name, entry.IsDirectory);
                            string combined = rootName + "/" + relative;
                            string origin = root.Path + " :: " + entry.Name;
                            if (entry.IsDirectory)
                            {
                                result.AddDirectory(combined, origin, entry.DosTime);
                                // 空でない親フォルダの暗号化エントリも、黙って飛ばさず検証する。
                                archive.CopyEntry(entry, Stream.Null);
                            }
                            else
                            {
                                SourceItem item = new SourceItem();
                                item.Archive = archive;
                                item.ArchiveEntry = entry;
                                item.Length = entry.UncompressedSize;
                                item.DosTime = entry.DosTime;
                                item.Origin = origin;
                                result.AddFile(combined, item);
                            }
                        }
                    }
                    else
                    {
                        result.AddPhysical(snapshot, PathRules.RootName(root.Path));
                    }
                }
            }
            List<string> paths = new List<string>(result.nodes.Keys);
            paths.Sort(StringComparer.Ordinal);
            foreach (string path in paths)
            {
                NameNode node = result.nodes[path];
                if (node.IsDirectory && !node.HasChildren)
                {
                    SourceItem item = new SourceItem();
                    item.IsDirectory = true;
                    item.ZipPath = PathRules.ArchivePath(node.Path, true);
                    item.NameBytes = Utf8.GetBytes(item.ZipPath);
                    item.Origin = node.Origin;
                    item.DosTime = node.DosTime;
                    item.Length = 0;
                    result.Items.Add(item);
                }
            }
            string candidate = System.IO.Path.Combine(outputDirectory.CanonicalPath, System.IO.Path.GetFileName(output.FirstPath));
            if (result.InputPaths.Contains(candidate))
                throw new AppError(87, "保存先が入力ファイルそのものです: " + candidate);
            return result;
        }

        /// <summary>リンク先も実体として走査。ZIP 内の相対名は実体名ではなく入力側の名前を保つ。</summary>
        /// <param name="path">物理ルート。シンボリックリンク/ジャンクションを許可する。</param>
        /// <param name="rootName">ZIP 内のルート名。</param>
        /// <param name="outputDirectory">固定済みの出力実体。リンク先から到達しても入力にはできない。</param>
        private void AddDirectoryTree(string path, string rootName, DirectorySnapshot outputDirectory)
        {
            Stack<DirectoryWork> stack = new Stack<DirectoryWork>();
            stack.Push(new DirectoryWork(path, rootName, null));
            while (stack.Count != 0)
            {
                CheckCancellation();
                DirectoryWork work = stack.Pop();
                work.Snapshot = DirectorySnapshot.Capture(work.PhysicalPath);
                RequireSeparateOutput(work.Snapshot, outputDirectory, work.PhysicalPath);
                AddDirectory(work.RelativePath, work.PhysicalPath, work.Snapshot.DosTime);

                // 全体の既訪問集合は使わない。別枝の同一実体は別の相対パスとして保存する。
                DirectoryWork ancestor = work.FindAncestor(work.Snapshot);
                if (ancestor != null)
                {
                    CycleCutCount = checked(CycleCutCount + 1);
                    Console.WriteLine("注意: 祖先へ戻る循環を検出しました。通常フォルダとして記録し、これ以上は再下降しません。" +
                        Environment.NewLine + "  ZIP 内パス: {0}/" + Environment.NewLine +
                        "  リンク側: {1}" + Environment.NewLine + "  実体: {2}" + Environment.NewLine + "  祖先: {3}",
                        work.RelativePath, work.PhysicalPath, work.Snapshot.CanonicalPath, ancestor.RelativePath);
                    continue;
                }
                string[] entries = Directory.GetFileSystemEntries(work.PhysicalPath);
                Array.Sort(entries, StringComparer.Ordinal);
                List<DirectoryWork> subdirectories = new List<DirectoryWork>();
                foreach (string entry in entries)
                {
                    CheckCancellation();
                    FileAttributes attributes = File.GetAttributes(entry);
                    string relative = work.RelativePath + "/" + System.IO.Path.GetFileName(entry);
                    if ((attributes & FileAttributes.Directory) != 0)
                        subdirectories.Add(new DirectoryWork(entry, relative, work));
                    else
                        AddPhysical(FileSnapshot.Capture(entry), relative);
                }
                for (int i = subdirectories.Count - 1; i >= 0; i--) stack.Push(subdirectories[i]);
            }
        }

        /// <summary>出力が入力実体自身/配下なら削除開始前に拒否。入出力引数は解決済み情報。</summary>
        internal static void RequireSeparateOutput(DirectorySnapshot input, DirectorySnapshot output, string inputPath)
        {
            if ((input.Identity != null && input.Identity == output.Identity) ||
                PathRules.IsWithin(output.CanonicalPath, input.CanonicalPath))
                throw new AppError(87, "出力先を入力フォルダ自身またはその配下にはできません。リンク先も対象です。" +
                    Environment.NewLine + "入力: " + inputPath + Environment.NewLine + "入力の実体: " + input.CanonicalPath +
                    Environment.NewLine + "出力フォルダ: " + output.CanonicalPath);
        }

        /// <summary>入力の物理的同一性を旧出力削除の保護対象に登録する。</summary>
        private void Protect(FileSnapshot snapshot)
        {
            InputIdentities.Add(snapshot.Identity);
            InputPaths.Add(snapshot.CanonicalPath);
        }

        /// <summary>通常ファイルを名前空間に登録する。フォルダ配下の .zip はここを通る。</summary>
        private void AddPhysical(FileSnapshot snapshot, string relative)
        {
            Protect(snapshot);
            SourceItem item = new SourceItem();
            item.Physical = snapshot;
            item.Length = snapshot.Length;
            item.DosTime = snapshot.DosStamp();
            item.Origin = snapshot.Path;
            AddFile(relative, item);
        }

        /// <summary>ファイルの重複、または既存ディレクトリとの衝突を例外として報告する。</summary>
        internal void AddFile(string relative, SourceItem item)
        {
            string path = PathRules.ArchivePath(relative, false);
            EnsureParents(path, item.Origin, item.DosTime);
            NameNode old;
            if (nodes.TryGetValue(path, out old)) ThrowCollision(path, old.Origin, item.Origin);
            NameNode node = new NameNode();
            node.Path = path;
            node.Origin = item.Origin;
            nodes.Add(path, node);
            item.ZipPath = path;
            item.NameBytes = Utf8.GetBytes(path);
            item.Ordinal = checked(++FileCount);
            TotalBytes = checked(TotalBytes + item.Length);
            Items.Add(item);
        }

        /// <summary>ディレクトリを登録。既存のディレクトリなら統合し、ファイルなら衝突とする。</summary>
        internal void AddDirectory(string relative, string origin, uint stamp)
        {
            string path = PathRules.ArchivePath(relative, true).TrimEnd('/');
            EnsureParents(path, origin, stamp);
            NameNode old;
            if (nodes.TryGetValue(path, out old))
            {
                if (!old.IsDirectory) ThrowCollision(path, old.Origin, origin);
                return;
            }
            NameNode node = new NameNode();
            node.IsDirectory = true;
            node.Path = path;
            node.Origin = origin;
            node.DosTime = stamp;
            nodes.Add(path, node);
        }

        /// <summary>明示的エントリがない親フォルダも登録し、ファイル a と a/b の衝突を防ぐ。</summary>
        private void EnsureParents(string path, string origin, uint stamp)
        {
            for (int index = path.IndexOf('/'); index >= 0; index = path.IndexOf('/', index + 1))
            {
                string parent = path.Substring(0, index);
                NameNode node;
                if (nodes.TryGetValue(parent, out node))
                {
                    if (!node.IsDirectory) ThrowCollision(parent, node.Origin, origin);
                }
                else
                {
                    node = new NameNode();
                    node.IsDirectory = true;
                    node.Path = parent;
                    node.Origin = origin;
                    node.DosTime = stamp;
                    nodes.Add(parent, node);
                }
                node.HasChildren = true;
            }
        }

        /// <summary>双方の元パスを付けて衝突を報告する。</summary>
        private static void ThrowCollision(string path, string oldOrigin, string newOrigin)
        {
            throw new AppError(183, "ZIP 内の相対パスが重複、またはファイルとフォルダが衝突しています (大文字小文字を無視)。\n" +
                "ZIP 内パス: " + path + "\n既存: " + oldOrigin + "\n追加: " + newOrigin);
        }
    }

    /// <summary>フォルダ走査スタック。親鎖だけで循環を判定し、別枝のエイリアスは省略しない。</summary>
    private sealed class DirectoryWork
    {
        internal readonly string PhysicalPath;
        internal readonly string RelativePath;
        internal readonly DirectoryWork Parent;
        internal DirectorySnapshot Snapshot;

        /// <summary>physical は読取パス、relative は出力相対名、parent は当該枝の親 (ルートは null)。</summary>
        internal DirectoryWork(string physical, string relative, DirectoryWork parent)
        { PhysicalPath = physical; RelativePath = relative; Parent = parent; }

        /// <summary>target と同じ実体の祖先を返す。ない場合は null。単なる別枝の再訪は循環ではない。</summary>
        internal DirectoryWork FindAncestor(DirectorySnapshot target)
        {
            for (DirectoryWork current = Parent; current != null; current = current.Parent)
                if ((target.Identity != null && current.Snapshot.Identity == target.Identity) ||
                    String.Equals(current.Snapshot.CanonicalPath, target.CanonicalPath, StringComparison.OrdinalIgnoreCase))
                    return current;
            return null;
        }
    }

    /// <summary>分割 ZIP の命名と、旧出力を削除できる名前かの厳密な判定。</summary>
    private sealed class OutputNames
    {
        internal readonly string FirstPath;
        internal readonly string DirectoryPath;
        internal readonly string BaseName;
        private readonly string firstName;
        internal OutputNames(string firstPath)
        {
            FirstPath = firstPath;
            DirectoryPath = System.IO.Path.GetDirectoryName(firstPath);
            firstName = System.IO.Path.GetFileName(firstPath);
            BaseName = System.IO.Path.GetFileNameWithoutExtension(firstName);
            if (String.IsNullOrEmpty(BaseName)) throw new AppError(87, "ZIP のベース名が空です。");
        }

        /// <summary>1 は指定名、2～19 はちょうど 2 桁の連番。それ以外は作成前に拒否する。</summary>
        internal string PartPath(int number)
        {
            if (number <= 0 || number > MAX_NUM_ZIP_FILES) throw new ArgumentOutOfRangeException("number",
                "ZIP の番号は 1～" + MAX_NUM_ZIP_FILES + " の範囲です。");
            if (number == 1) return FirstPath;
            return System.IO.Path.Combine(DirectoryPath, BaseName + "." + number.ToString("D2", NumberCulture) + ".zip");
        }

        /// <summary>ファイル名全体を検査する。数字は ASCII の 0～9 だけで、必ずちょうど 2 桁。</summary>
        internal bool IsOldName(string name)
        {
            return MatchesOldName(name, firstName, BaseName);
        }

        /// <summary>削除規則の純粋関数。ディレクトリの有無は呼出側で別に必ず検査する。</summary>
        internal static bool MatchesOldName(string name, string first, string stem)
        {
            if (name.IndexOf('/') >= 0 || name.IndexOf('\\') >= 0) return false;
            if (String.Equals(name, first, StringComparison.OrdinalIgnoreCase)) return true;
            string prefix = stem + ".";
            const string suffix = ".zip";
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return false;
            int digits = name.Length - prefix.Length - suffix.Length;
            if (digits != 2) return false;
            for (int i = prefix.Length; i < prefix.Length + digits; i++)
                if (name[i] < '0' || name[i] > '9') return false;
            return true;
        }
    }

    /// <summary>対象ディレクトリと祖先のハンドルを保持し、処理中のリネーム・再解析化を抑止する。</summary>
    private sealed class DirectoryGuard : IDisposable
    {
        private readonly List<SafeFileHandle> handles = new List<SafeFileHandle>();
        internal readonly string CanonicalPath;
        /// <summary>指定フォルダを実体に解決して固定する。後続 I/O は必ず CanonicalPath を使用する。</summary>
        internal DirectoryGuard(string directory)
        {
            try
            {
                CanonicalPath = PathRules.FullPath(Native.CanonicalDirectory(directory));
                PathRules.RejectReparseAncestors(CanonicalPath);
                string current = System.IO.Path.GetPathRoot(CanonicalPath);
                handles.Add(Native.OpenGuardDirectory(current));
                string rest = CanonicalPath.Substring(current.Length);
                if (rest.Length != 0)
                    foreach (string part in rest.Split('\\'))
                    {
                        current = System.IO.Path.Combine(current, part);
                        handles.Add(Native.OpenGuardDirectory(current));
                    }
                if (!String.Equals(Native.CanonicalDirectory(directory), CanonicalPath, StringComparison.OrdinalIgnoreCase) ||
                    !String.Equals(Native.CanonicalDirectory(CanonicalPath), CanonicalPath, StringComparison.OrdinalIgnoreCase))
                    throw new AppError(13, "出力フォルダの実体が保護処理中に変更されました: " + directory);
            }
            catch { Dispose(); throw; }
        }
        /// <summary>取得と逆順にディレクトリの保護ハンドルを解放する。</summary>
        public void Dispose()
        {
            for (int i = handles.Count - 1; i >= 0; i--) handles[i].Dispose();
            handles.Clear();
        }
    }

    /// <summary>今回作った 1 出力。そのハンドルを閉じるまで、別ファイルとの置換を許さない。</summary>
    private sealed class OwnedOutput
    {
        internal string Path;
        internal string Name;
        internal FileStream Stream;
        internal SafeFileHandle Handle;
        internal long ExpectedLength;
    }

    /// <summary>旧出力の削除と新出力全体の成功/失敗を管理する。名前だけで後片付けしない。</summary>
    private sealed class OutputTransaction : IDisposable
    {
        private readonly OutputNames names;
        private readonly List<OwnedOutput> outputs = new List<OwnedOutput>();
        private bool committed;
        private bool rolledBack;
        internal OutputTransaction(OutputNames outputNames) { names = outputNames; }

        /// <summary>直下だけ列挙し、全対象を検査・ハンドルで固定・確認してから削除する。</summary>
        internal void RemovePreviousOutputs(Manifest manifest)
        {
            List<SafeFileHandle> oldHandles = new List<SafeFileHandle>();
            List<string> oldPaths = new List<string>();
            try
            {
                string[] entries = Directory.GetFileSystemEntries(names.DirectoryPath);
                Array.Sort(entries, StringComparer.Ordinal);
                foreach (string path in entries)
                {
                    CheckCancellation();
                    string name = System.IO.Path.GetFileName(path);
                    if (!names.IsOldName(name)) continue;
                    FileAttributes attributes = File.GetAttributes(path);
                    if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                        throw new AppError(50, "削除候補の名前に一致するフォルダまたはリンクがあります。削除しません: " + path);
                    SafeFileHandle handle = Native.OpenDeleteFile(path);
                    oldHandles.Add(handle);
                    oldPaths.Add(path);
                    Native.FileInformation info = Native.Information(handle, path);
                    Native.RequireOrdinaryFile(info, path);
                    RequireOutputParent(handle, path);
                    if ((info.Attributes & (uint)FileAttributes.ReadOnly) != 0)
                        throw new AppError(5, "旧出力が読み取り専用です。属性を自動変更せず中止します: " + path);
                    if (manifest.InputIdentities.Contains(Native.Identity(info)) ||
                        manifest.InputPaths.Contains(Native.FinalPath(handle)))
                        throw new AppError(87, "旧出力の削除候補が入力ファイル、またはそのハードリンクです。削除しません: " + path);
                }
                if (oldPaths.Count != 0)
                {
                    Console.WriteLine("次の既存ファイルを削除して作り直します。失敗しても元の ZIP は復元されません。");
                    foreach (string path in oldPaths) Console.WriteLine("  " + path);
                    if (!AskYesNo("上記 " + oldPaths.Count.ToString(NumberCulture) + " 個の削除を許可しますか?(y/n)"))
                        throw new AppError(1223, "既存ファイルの削除がキャンセルされました。既存ファイルは削除していません。");
                }
                for (int i = 0; i < oldHandles.Count; i++)
                {
                    CheckCancellation();
                    // 開いた対象そのものを削除する。ここでパスを引き直してはいけない。
                    Native.SetDeletePending(oldHandles[i], true, oldPaths[i]);
                    oldHandles[i].Dispose();
                }
            }
            finally
            {
                foreach (SafeFileHandle handle in oldHandles) handle.Dispose();
            }
        }

        /// <summary>開いたハンドルが固定した出力フォルダ内にあるか確認し、祖先リンクの差替えを検出。</summary>
        private void RequireOutputParent(SafeFileHandle handle, string path)
        {
            string actual = Native.FinalPath(handle);
            string parent = System.IO.Path.GetDirectoryName(actual);
            if (parent == null || !String.Equals(parent.TrimEnd('\\'), names.DirectoryPath.TrimEnd('\\'),
                StringComparison.OrdinalIgnoreCase))
                throw new AppError(13, "出力先の実体が予定フォルダと異なります。処理を中止します。" +
                    Environment.NewLine + "予定: " + path + Environment.NewLine + "実体: " + actual);
        }

        /// <summary>新規ファイルだけを作る。列挙後に出現した同名ファイルは絶対に上書きしない。</summary>
        internal OwnedOutput CreateNext()
        {
            if (outputs.Count >= MAX_NUM_ZIP_FILES)
                throw new AppError(13, "ZIP の生成数が絶対上限 " + MAX_NUM_ZIP_FILES + " 個を超えようとしました。");
            string path = names.PartPath(checked(outputs.Count + 1));
            OwnedOutput item = new OwnedOutput();
            item.Path = path;
            item.Name = System.IO.Path.GetFileName(path);
            SafeFileHandle handle = null;
            SafeFileHandle borrowed = null;
            try
            {
                handle = Native.CreateOutputFile(path);
                item.Handle = handle;
                RequireOutputParent(handle, path);
                borrowed = new SafeFileHandle(handle.DangerousGetHandle(), false);
                // Stream の Dispose 後も削除用の所有ハンドルが残る。所有者は必ず item.Handle。
                item.Stream = new FileStream(borrowed, FileAccess.ReadWrite, 4096, false);
                outputs.Add(item);
                return item;
            }
            catch
            {
                try
                {
                    if (item.Stream != null) item.Stream.Dispose();
                    else if (borrowed != null) borrowed.Dispose();
                }
                finally
                {
                    if (handle != null)
                    {
                        try { Native.SetDeletePending(handle, true, path); }
                        finally { handle.Dispose(); }
                    }
                }
                throw;
            }
        }

        /// <summary>全体をディスクまでフラッシュして実サイズを検査する。完了するまでは全所有ハンドルを保持。</summary>
        internal void Commit()
        {
            foreach (OwnedOutput item in outputs)
            {
                CheckCancellation();
                item.Stream.Flush(true);
                if (item.Stream.Length != item.ExpectedLength)
                    throw new AppError(13, "ZIP の最終物理サイズが一致しません: " + item.Path);
            }
            foreach (OwnedOutput item in outputs)
            {
                CheckCancellation();
                // 非所有 SafeFileHandle を渡しているので、Stream を閉じても実体は固定されたまま。
                item.Stream.Dispose();
            }
            CheckCancellation();
            committed = true;
            foreach (OwnedOutput item in outputs) item.Handle.Dispose();
        }

        /// <summary>書き込みを終了した後、今回所有したハンドルにだけ削除保留を設定して閉じる。</summary>
        internal void Rollback()
        {
            if (rolledBack || committed) return;
            rolledBack = true;
            foreach (OwnedOutput item in outputs)
            {
                // 削除保留設定後には書込み等をせず、ハンドルを閉じるだけにする。
                try { item.Stream.Dispose(); }
                catch (Exception ex) { TryWriteLine("出力ストリーム終了時のエラー: " + item.Path + "\n" + ex.Message); }
                try { Native.SetDeletePending(item.Handle, true, item.Path); }
                catch (Exception ex)
                {
                    TryWriteLine("後片付けエラー: " + item.Path + "\n" + ex.Message +
                        "\nこの正確なパスにファイルが残っていないか確認してください。");
                }
                finally { item.Handle.Dispose(); }
            }
            if (outputs.Count > 0) TryWriteLine("今回の出力に対する後片付けを実行しました。上記に削除エラーがなければ中途半端な ZIP は残りません。");
        }

        /// <summary>未確定なら後片付けを行い、確定済みなら保持資源だけを解放する。</summary>
        public void Dispose()
        {
            if (!committed) Rollback();
            else foreach (OwnedOutput item in outputs) item.Handle.Dispose();
        }
    }

    /// <summary>結果集計。最大サイズが同じなら最初の ZIP を最大として報告する。</summary>
    private sealed class PackResult
    {
        internal int FileCount;
        internal int PartCount;
        internal int LargestIndex;
        internal int CycleCutCount;
        internal long OriginalBytes;
        internal long CompressedPayloadBytes;
        internal long TotalZipBytes;
        internal long LargestBytes = -1;
        internal string LargestName;
        internal string LastName;
        internal void AddPart(string name, long size)
        {
            PartCount = checked(PartCount + 1);
            TotalZipBytes = checked(TotalZipBytes + size);
            LastName = name;
            if (size > LargestBytes) { LargestBytes = size; LargestName = name; LargestIndex = PartCount; }
        }
    }

    /// <summary>Win32 のファイル識別・安全な削除・コンソール確保だけを担当する。</summary>
    private static class Native
    {
        /// <summary>GlobalMemoryStatusEx の ABI と同じ順序・幅のメモリ情報。</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatus
        {
            internal uint Length;
            internal uint MemoryLoad;
            internal ulong TotalPhysical;
            internal ulong AvailablePhysical;
            internal ulong TotalPageFile;
            internal ulong AvailablePageFile;
            internal ulong TotalVirtual;
            internal ulong AvailableVirtual;
            internal ulong AvailableExtendedVirtual;
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
        private static int memoryWarning;

        /// <summary>物理・コミット可能量・プロセスの空き仮想空間の最小値。失敗時は 0 でキャッシュ禁止。</summary>
        internal static long AvailableMemoryBytes()
        {
            MemoryStatus status = new MemoryStatus();
            status.Length = (uint)Marshal.SizeOf(typeof(MemoryStatus));
            if (!GlobalMemoryStatusEx(ref status))
            {
                int code = Marshal.GetLastWin32Error();
                if (Interlocked.Exchange(ref memoryWarning, 1) == 0)
                    TryWriteLine("警告: 使用可能メモリを取得できないためキャッシュを使用しません。Win32 エラー " + code);
                return 0;
            }
            ulong available = Math.Min(status.AvailablePhysical, Math.Min(status.AvailablePageFile, status.AvailableVirtual));
            return available > (ulong)Int64.MaxValue ? Int64.MaxValue : (long)available;
        }

        private const uint GenericRead = 0x80000000U;
        private const uint GenericWrite = 0x40000000U;
        private const uint DeleteAccess = 0x00010000U;
        private const uint ReadAttributes = 0x00000080U;
        private const uint ShareRead = 1U;
        private const uint ShareWrite = 2U;
        private const uint ShareDelete = 4U;
        private const uint CreateNew = 1U;
        private const uint OpenExisting = 3U;
        private const uint BackupSemantics = 0x02000000U;
        private const uint OpenReparsePoint = 0x00200000U;
        private const uint SequentialScan = 0x08000000U;

        /// <summary>ネイティブ FILETIME の符号なし 64 bit 値。</summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct FileTime
        {
            internal uint Low;
            internal uint High;
            internal long ToInt64() { return checked((long)(((ulong)High << 32) | Low)); }
        }

        /// <summary>BY_HANDLE_FILE_INFORMATION。ボリュームとファイル ID で同一実体を検出する。</summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct FileInformation
        {
            internal uint Attributes;
            internal FileTime CreationTime;
            internal FileTime LastAccessTime;
            internal FileTime LastWriteTime;
            internal uint VolumeSerialNumber;
            internal uint FileSizeHigh;
            internal uint FileSizeLow;
            internal uint NumberOfLinks;
            internal uint FileIndexHigh;
            internal uint FileIndexLow;
        }

        /// <summary>FILE_DISPOSITION_INFO の BOOLEAN は Win32 BOOL ではなく 1 byte。</summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct DispositionInformation { internal byte DeleteFile; }

        /// <summary>コンソール入力レコード。KEY_EVENT_RECORD の必要部分を明示オフセットで表す。</summary>
        [StructLayout(LayoutKind.Explicit, Size = 20)]
        private struct ConsoleInputRecord
        {
            [FieldOffset(0)] internal ushort EventType;
            [FieldOffset(4)] internal int KeyDown;
            [FieldOffset(10)] internal ushort VirtualKey;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(string name, uint access, uint share,
            IntPtr security, uint disposition, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle, StringBuilder path, uint length, uint flags);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int informationClass,
            ref DispositionInformation information, uint length);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllocConsole();
        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetConsoleWindow();
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ReadConsoleInputW(SafeFileHandle input, out ConsoleInputRecord record,
            uint count, out uint read);

        /// <summary>WinExe でもコンソールを持つ。既存コンソールがある /target:exe ではそのまま利用。</summary>
        internal static void EnsureConsole()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                throw new PlatformNotSupportedException("このアプリケーションは Windows 用です。");
            if (GetConsoleWindow() == IntPtr.Zero && !AllocConsole())
                throw LastError("コンソールの確保に失敗しました。");
            Console.OutputEncoding = new UTF8Encoding(false);
            Console.InputEncoding = new UTF8Encoding(false);
            StreamWriter output = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
            output.AutoFlush = true;
            Console.SetOut(output);
            Console.SetError(output);
            Console.SetIn(new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false), false, 4096));
            try { Console.Title = "ZIP ファイル分割結合ユーティリティ"; } catch (IOException) { }
        }

        /// <summary>成功・失敗にかかわらず実際のキー入力まで待つ。リダイレクト時は CONIN$ を開く。</summary>
        internal static void WaitForExitKey()
        {
            TryWriteLine("何かキーを押すと終了します...");
            try { Console.ReadKey(true); return; }
            catch (InvalidOperationException) { }
            catch (IOException) { }
            try
            {
                using (SafeFileHandle input = OpenHandle("CONIN$", GenericRead, ShareRead | ShareWrite, OpenExisting, 0))
                {
                    while (true)
                    {
                        ConsoleInputRecord record;
                        uint count;
                        if (!ReadConsoleInputW(input, out record, 1, out count)) throw LastError("終了キーを読み取れません。");
                        if (count == 1 && record.EventType == 1 && record.KeyDown != 0 && record.VirtualKey != 0) return;
                    }
                }
            }
            catch (Exception ex)
            {
                TryWriteLine("終了待機エラー: " + ex.Message);
            }
        }

        /// <summary>作成/オープンを共通化し、失敗時はパス付き Win32 例外を返す。</summary>
        private static SafeFileHandle OpenHandle(string path, uint access, uint sharing, uint disposition, uint flags)
        {
            SafeFileHandle handle = CreateFileW(path, access, sharing, IntPtr.Zero, disposition, flags, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw new Win32Exception(error, "ファイル/フォルダを開けません: " + path + "\n" + new Win32Exception(error).Message);
            }
            return handle;
        }

        /// <summary>リンク先の実体を読み、書換え・削除と共有しない同期ストリーム。リンク自体は開かない。</summary>
        internal static FileStream OpenReadFile(string path)
        {
            SafeFileHandle handle = OpenHandle(path, GenericRead, ShareRead, OpenExisting, SequentialScan);
            try { return new FileStream(handle, FileAccess.Read, BufferSize, false); }
            catch { handle.Dispose(); throw; }
        }

        /// <summary>旧出力の削除権限を事前に確保し、第三者による置換を禁止する。</summary>
        internal static SafeFileHandle OpenDeleteFile(string path)
        {
            return OpenHandle(path, DeleteAccess | ReadAttributes, ShareRead, OpenExisting, OpenReparsePoint);
        }

        /// <summary>新規出力だけを作る。読み書きと削除権限を同じハンドルに保持する。</summary>
        internal static SafeFileHandle CreateOutputFile(string path)
        {
            return OpenHandle(path, GenericRead | GenericWrite | DeleteAccess, ShareRead, CreateNew, 0x80U | SequentialScan);
        }

        /// <summary>ディレクトリの削除/リネームを共有しない保護ハンドル。</summary>
        internal static SafeFileHandle OpenGuardDirectory(string path)
        {
            SafeFileHandle handle = OpenHandle(path, ReadAttributes, ShareRead | ShareWrite, OpenExisting,
                BackupSemantics | OpenReparsePoint);
            try
            {
                FileInformation info = Information(handle, path);
                if ((info.Attributes & (uint)FileAttributes.Directory) == 0 ||
                    (info.Attributes & (uint)FileAttributes.ReparsePoint) != 0)
                    throw new AppError(50, "保護対象が通常のディレクトリではありません: " + path);
                return handle;
            }
            catch { handle.Dispose(); throw; }
        }

        /// <summary>リンク/ジャンクションを解決したディレクトリを開く。返すハンドルは呼出側が破棄する。</summary>
        internal static SafeFileHandle OpenReadDirectory(string path)
        {
            SafeFileHandle handle = OpenHandle(path, ReadAttributes, ShareRead | ShareWrite | ShareDelete,
                OpenExisting, BackupSemantics); // OPEN_REPARSE_POINT を付けず、リンク先のハンドルを得る。
            try
            {
                FileInformation info = Information(handle, path);
                if ((info.Attributes & (uint)FileAttributes.Directory) == 0 ||
                    (info.Attributes & (uint)FileAttributes.Device) != 0)
                    throw new AppError(50, "リンク先をディレクトリとして読み取れません: " + path);
                return handle;
            }
            catch { handle.Dispose(); throw; }
        }

        /// <summary>既存ディレクトリのリンク解決済み実体パスを返す。入力と出力先の双方で使う。</summary>
        internal static string CanonicalDirectory(string path)
        {
            using (SafeFileHandle handle = OpenReadDirectory(path)) return FinalPath(handle);
        }

        /// <summary>ハンドルからメタデータを読む。</summary>
        internal static FileInformation Information(SafeFileHandle handle, string path)
        {
            FileInformation info;
            if (!GetFileInformationByHandle(handle, out info)) throw LastError("ファイル情報を取得できません: " + path);
            return info;
        }

        /// <summary>ディレクトリや再解析ポイントを通常ファイルとして取り扱わない。</summary>
        internal static void RequireOrdinaryFile(FileInformation info, string path)
        {
            if ((info.Attributes & ((uint)FileAttributes.Directory | (uint)FileAttributes.ReparsePoint | (uint)FileAttributes.Device)) != 0)
                throw new AppError(50, "通常ファイルではありません: " + path);
        }

        /// <summary>ファイル ID とボリューム番号を連結した比較用の識別子。</summary>
        internal static string Identity(FileInformation info)
        {
            return info.VolumeSerialNumber.ToString("X8", NumberCulture) + ":" +
                info.FileIndexHigh.ToString("X8", NumberCulture) + info.FileIndexLow.ToString("X8", NumberCulture);
        }

        /// <summary>64 bit ファイル長。Stream で表現できない長さは例外にする。</summary>
        internal static long FileLength(FileInformation info)
        {
            return checked((long)(((ulong)info.FileSizeHigh << 32) | info.FileSizeLow));
        }

        /// <summary>パス解決済みの実体名を取得する。比較用なので \\?\ 接頭辞は除く。</summary>
        internal static string FinalPath(SafeFileHandle handle)
        {
            StringBuilder buffer = new StringBuilder(1024);
            uint length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, 0);
            if (length == 0) throw LastError("ファイルの実体パスを取得できません。");
            if (length >= buffer.Capacity)
            {
                buffer = new StringBuilder(checked((int)length + 1));
                length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, 0);
                if (length == 0 || length >= buffer.Capacity) throw LastError("実体パスを取得できません。");
            }
            string path = buffer.ToString();
            if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) return @"\\" + path.Substring(8);
            if (path.StartsWith(@"\\?\", StringComparison.Ordinal)) return path.Substring(4);
            return path;
        }

        /// <summary>保持中の同一実体に削除保留を設定/解除する。パス文字列はエラー表示専用。</summary>
        internal static void SetDeletePending(SafeFileHandle handle, bool delete, string path)
        {
            DispositionInformation information = new DispositionInformation();
            information.DeleteFile = delete ? (byte)1 : (byte)0;
            if (!SetFileInformationByHandle(handle, 4, ref information, 1))
                throw LastError((delete ? "削除保留を設定できません: " : "削除保留を解除できません: ") + path);
        }

        /// <summary>現在の Win32 エラーと文脈を含む例外を作る。</summary>
        private static Win32Exception LastError(string message)
        {
            int code = Marshal.GetLastWin32Error();
            return new Win32Exception(code, message + "\n" + new Win32Exception(code).Message);
        }
    }

    /// <summary>ZIP で使う DOS ローカル日時の変換。表現範囲外は端に丸める。</summary>
    private static class ZipTime
    {
        internal static uint Encode(DateTime localTime)
        {
            DateTime value = localTime;
            if (value.Year < 1980) value = new DateTime(1980, 1, 1, 0, 0, 0);
            if (value.Year > 2107) value = new DateTime(2107, 12, 31, 23, 59, 58);
            uint time = (uint)((value.Hour << 11) | (value.Minute << 5) | (value.Second / 2));
            uint date = (uint)(((value.Year - 1980) << 9) | (value.Month << 5) | value.Day);
            return time | (date << 16);
        }
    }

    /// <summary>ZIP への採用候補と中央ディレクトリ 1 件に必要な情報。</summary>
    private sealed class EntryRecord
    {
        internal SourceItem Source;
        internal long Offset;
        internal long CompressedSize;
        internal long UncompressedSize;
        internal uint Crc;
        internal ushort Method;
        internal ushort Flags;
        internal long End;
        internal bool Size64 { get { return CompressedSize >= Zip32Limit || UncompressedSize >= Zip32Limit; } }
        internal bool Uses64 { get { return Size64 || Offset >= Zip32Limit; } }
        internal int LocalExtraLength { get { return !Source.IsDirectory && Method == 8 ? 20 : 0; } }
        internal int DescriptorLength { get { return Source.IsDirectory ? 0 : (Size64 ? 24 : 16); } }
        internal int CentralExtraLength
        {
            get
            {
                int fields = (UncompressedSize >= Zip32Limit ? 1 : 0) +
                    (CompressedSize >= Zip32Limit ? 1 : 0) + (Offset >= Zip32Limit ? 1 : 0);
                return fields == 0 ? 0 : 4 + 8 * fields;
            }
        }
        internal long LocalLength { get { return checked(30L + Source.NameBytes.Length + LocalExtraLength + CompressedSize + DescriptorLength); } }
        internal long CentralLength { get { return 46L + Source.NameBytes.Length + CentralExtraLength; } }
    }

    /// <summary>出力 ZIP コンテナ。圧縮ライブラリを ZIP の生成や取り消しには使用しない。</summary>
    private sealed class ZipOutput
    {
        private readonly Stream stream;
        private readonly List<EntryRecord> entries = new List<EntryRecord>();
        private long dataEnd;
        private long centralBytes;
        private bool has64;
        private bool finished;
        internal int Count { get { return entries.Count; } }
        internal ZipOutput(Stream output)
        {
            if (!output.CanSeek || !output.CanWrite) throw new ArgumentException("シーク可能な出力が必要です。", "output");
            stream = output;
        }

        /// <summary>既知の圧縮サイズから、現在の ZIP に追加した場合の完成サイズを求める。</summary>
        internal long Predict(SourceItem source, Measurement measurement)
        {
            EntryRecord record = NewRecord(source);
            record.CompressedSize = measurement.CompressedSize;
            record.UncompressedSize = measurement.UncompressedSize;
            record.Method = measurement.Method;
            record.End = checked(dataEnd + record.LocalLength);
            return PredictRecord(record);
        }

        /// <summary>候補のローカル領域・中央ディレクトリ・ZIP64 を含む厳密なサイズ。</summary>
        internal long PredictRecord(EntryRecord record)
        {
            return TotalSize(record.End, checked(centralBytes + record.CentralLength), checked(entries.Count + 1), has64 || record.Uses64);
        }

        /// <summary>候補の初期レコードを作る。空ファイルは Store、それ以外は Deflate。</summary>
        private EntryRecord NewRecord(SourceItem item)
        {
            EntryRecord record = new EntryRecord();
            record.Source = item;
            record.Offset = dataEnd;
            record.UncompressedSize = item.Length;
            record.Method = item.IsDirectory || item.Length == 0 ? (ushort)0 : (ushort)8;
            record.Flags = item.IsDirectory ? (ushort)0x0800 : (ushort)0x0809;
            return record;
        }

        /// <summary>新たに圧縮したキャッシュを使用するか再圧縮してローカル領域を書く。元 ZIP の圧縮データは再利用しない。</summary>
        /// <param name="item">出力対象と既知の計測値。値があれば再圧縮結果を照合する。</param>
        /// <param name="cache">事前圧縮キャッシュ。null または失効時は通常の圧縮処理。</param>
        /// <returns>採用前のローカルレコード情報。呼出側が Accept で確定する。</returns>
        internal EntryRecord WriteCandidate(SourceItem item, CompressedCache cache = null)
        {
            if (finished) throw new InvalidOperationException("ZIP はすでに完了しています。");
            if (stream.Position != dataEnd) throw new AppError(13, "内部エラー: 出力位置が一致しません。");
            EntryRecord record = NewRecord(item);
            WriteLocal(record, false);
            long dataStart = stream.Position;
            ContentResult content;
            if (item.IsDirectory)
            {
                content = new ContentResult(0, 0, new byte[0]);
            }
            else
            {
                content = null;
                if (cache != null && item.Measurement != null)
                {
                    // キャッシュは今回新たに作った圧縮データだけ。入力 ZIP の圧縮領域は再利用しない。
                    using (Stream validation = item.OpenValidationStream())
                    using (CryptoWriteStream encrypted = new CryptoWriteStream(stream, (byte)(item.DosTime >> 8)))
                    {
                        if (cache.TryCopyTo(item, encrypted))
                            content = new ContentResult(item.Measurement.UncompressedSize, item.Measurement.Crc, item.Measurement.Digest);
                    }
                }
                if (content == null)
                {
                    // 転送中の予算縮小でも、暗号鍵状態を使い回さずヘッダから完全にやり直す。
                    stream.Position = dataStart;
                    stream.SetLength(dataStart);
                    using (CryptoWriteStream encrypted = new CryptoWriteStream(stream, (byte)(item.DosTime >> 8)))
                    {
                        if (record.Method == 8)
                        {
                            using (DeflateStream compressor = CreateCompressor(encrypted))
                                content = item.CopyPlaintextTo(compressor);
                        }
                        else content = item.CopyPlaintextTo(encrypted);
                    }
                }
            }
            record.CompressedSize = stream.Position - dataStart;
            record.UncompressedSize = content.Length;
            record.Crc = content.Crc;
            if (!item.IsDirectory)
            {
                Little.Write32(stream, 0x08074B50U);
                Little.Write32(stream, record.Crc);
                if (record.Size64)
                {
                    Little.Write64(stream, (ulong)record.CompressedSize);
                    Little.Write64(stream, (ulong)record.UncompressedSize);
                }
                else
                {
                    Little.Write32(stream, (uint)record.CompressedSize);
                    Little.Write32(stream, (uint)record.UncompressedSize);
                }
            }
            record.End = stream.Position;
            if (record.End != checked(record.Offset + record.LocalLength)) throw new AppError(13, "ローカルレコード長の内部不一致。");
            stream.Position = record.Offset;
            WriteLocal(record, true);
            stream.Position = record.End;

            Measurement previous = item.Measurement;
            if (previous != null)
            {
                if (previous.CompressedSize != record.CompressedSize || previous.UncompressedSize != content.Length ||
                    previous.Crc != content.Crc || previous.Method != record.Method || !FixedEquals(previous.Digest, content.Digest))
                    throw new AppError(13, "再試行時に内容または圧縮結果が変化しました: " + item.Origin);
            }
            else
            {
                Measurement measurement = new Measurement();
                measurement.CompressedSize = record.CompressedSize;
                measurement.UncompressedSize = content.Length;
                measurement.Crc = content.Crc;
                measurement.Digest = content.Digest;
                measurement.Method = record.Method;
                item.Measurement = measurement;
            }
            return record;
        }

        /// <summary>候補を採用し、中央ディレクトリの計上を確定する。</summary>
        internal void Accept(EntryRecord record)
        {
            if (record.Offset != dataEnd) throw new AppError(13, "採用レコードの開始位置が不正です。");
            entries.Add(record);
            centralBytes = checked(centralBytes + record.CentralLength);
            has64 |= record.Uses64;
            dataEnd = record.End;
        }

        /// <summary>ローカルヘッダ。将来 ZIP64 になる可能性に備え、Deflate には 20 byte を予約する。</summary>
        private void WriteLocal(EntryRecord record, bool complete)
        {
            bool size64 = complete && record.Size64;
            Little.Write32(stream, 0x04034B50U);
            Little.Write16(stream, (ushort)(record.Uses64 ? 45 : 20));
            Little.Write16(stream, record.Flags);
            Little.Write16(stream, record.Method);
            Little.Write32(stream, record.Source.DosTime);
            // bit 3 のファイルでは CRC と通常サイズは descriptor/central に置く。
            Little.Write32(stream, 0);
            Little.Write32(stream, size64 ? UInt32.MaxValue : 0U);
            Little.Write32(stream, size64 ? UInt32.MaxValue : 0U);
            Little.Write16(stream, (ushort)record.Source.NameBytes.Length);
            Little.Write16(stream, (ushort)record.LocalExtraLength);
            stream.Write(record.Source.NameBytes, 0, record.Source.NameBytes.Length);
            if (record.LocalExtraLength != 0)
            {
                // 0xFFFF は無解釈の予約領域。非 ZIP64 では読み手は未知 extra として読み飛ばせる。
                Little.Write16(stream, size64 ? (ushort)0x0001 : (ushort)0xFFFF);
                Little.Write16(stream, 16);
                Little.Write64(stream, size64 ? (ulong)record.UncompressedSize : 0UL);
                Little.Write64(stream, size64 ? (ulong)record.CompressedSize : 0UL);
            }
        }

        /// <summary>中央ディレクトリ 1 件。ZIP64 extra は必要な値だけを所定順に書く。</summary>
        private void WriteCentral(EntryRecord record)
        {
            Little.Write32(stream, 0x02014B50U);
            Little.Write16(stream, (ushort)(record.Uses64 ? 45 : 20)); // 作成 OS は MS-DOS。
            Little.Write16(stream, (ushort)(record.Uses64 ? 45 : 20));
            Little.Write16(stream, record.Flags);
            Little.Write16(stream, record.Method);
            Little.Write32(stream, record.Source.DosTime);
            Little.Write32(stream, record.Crc);
            Little.Write32(stream, record.CompressedSize >= Zip32Limit ? UInt32.MaxValue : (uint)record.CompressedSize);
            Little.Write32(stream, record.UncompressedSize >= Zip32Limit ? UInt32.MaxValue : (uint)record.UncompressedSize);
            Little.Write16(stream, (ushort)record.Source.NameBytes.Length);
            Little.Write16(stream, (ushort)record.CentralExtraLength);
            Little.Write16(stream, 0); // コメントなし。
            Little.Write16(stream, 0); // ディスク番号。
            Little.Write16(stream, 0); // 内部属性。
            Little.Write32(stream, record.Source.IsDirectory ? 0x10U : 0x20U);
            Little.Write32(stream, record.Offset >= Zip32Limit ? UInt32.MaxValue : (uint)record.Offset);
            stream.Write(record.Source.NameBytes, 0, record.Source.NameBytes.Length);
            if (record.CentralExtraLength != 0)
            {
                Little.Write16(stream, 0x0001);
                Little.Write16(stream, (ushort)(record.CentralExtraLength - 4));
                if (record.UncompressedSize >= Zip32Limit) Little.Write64(stream, (ulong)record.UncompressedSize);
                if (record.CompressedSize >= Zip32Limit) Little.Write64(stream, (ulong)record.CompressedSize);
                if (record.Offset >= Zip32Limit) Little.Write64(stream, (ulong)record.Offset);
            }
        }

        /// <summary>中央ディレクトリ・末尾構造を書き、正確な最終物理サイズを返す。ストリームは閉じない。</summary>
        internal long Finish()
        {
            if (finished) throw new InvalidOperationException("二重に完了処理を呼び出しました。");
            stream.Position = dataEnd;
            long expected = TotalSize(dataEnd, centralBytes, entries.Count, has64);
            bool zip64 = Needs64(dataEnd, centralBytes, entries.Count, has64);
            foreach (EntryRecord record in entries) WriteCentral(record);
            if (stream.Position != checked(dataEnd + centralBytes)) throw new AppError(13, "中央ディレクトリ長の内部不一致。");
            if (zip64)
            {
                long position64 = stream.Position;
                Little.Write32(stream, 0x06064B50U);
                Little.Write64(stream, 44);
                Little.Write16(stream, 45);
                Little.Write16(stream, 45);
                Little.Write32(stream, 0);
                Little.Write32(stream, 0);
                Little.Write64(stream, (ulong)entries.Count);
                Little.Write64(stream, (ulong)entries.Count);
                Little.Write64(stream, (ulong)centralBytes);
                Little.Write64(stream, (ulong)dataEnd);
                Little.Write32(stream, 0x07064B50U);
                Little.Write32(stream, 0);
                Little.Write64(stream, (ulong)position64);
                Little.Write32(stream, 1);
            }
            Little.Write32(stream, 0x06054B50U);
            Little.Write16(stream, 0);
            Little.Write16(stream, 0);
            Little.Write16(stream, zip64 ? UInt16.MaxValue : (ushort)entries.Count);
            Little.Write16(stream, zip64 ? UInt16.MaxValue : (ushort)entries.Count);
            Little.Write32(stream, zip64 ? UInt32.MaxValue : (uint)centralBytes);
            Little.Write32(stream, zip64 ? UInt32.MaxValue : (uint)dataEnd);
            Little.Write16(stream, 0);
            stream.Flush();
            if (stream.Position != expected || stream.Length != expected)
                throw new AppError(13, "ZIP の完成サイズが計算値に一致しません。");
            finished = true;
            entries.Clear();
            return expected;
        }

        /// <summary>ZIP64 末尾が必要か。エントリ単位で ZIP64 を使った場合も末尾を明示する。</summary>
        private static bool Needs64(long localBytes, long directoryBytes, int count, bool entry64)
        {
            return entry64 || count >= UInt16.MaxValue || localBytes >= Zip32Limit || directoryBytes >= Zip32Limit;
        }

        /// <summary>EOCD 22 byte、ZIP64 EOCD+locator 76 byte を含めた完成サイズ。</summary>
        internal static long TotalSize(long localBytes, long directoryBytes, int count, bool entry64)
        {
            return checked(localBytes + directoryBytes + 22L + (Needs64(localBytes, directoryBytes, count, entry64) ? 76L : 0L));
        }
    }

    /// <summary>ZIP の little endian 読み書きと、切断・範囲外の検査。</summary>
    private static class Little
    {
        internal static ushort U16(byte[] bytes, int offset)
        { return (ushort)(bytes[offset] | (bytes[offset + 1] << 8)); }
        internal static uint U32(byte[] bytes, int offset)
        {
            return (uint)bytes[offset] | ((uint)bytes[offset + 1] << 8) |
                ((uint)bytes[offset + 2] << 16) | ((uint)bytes[offset + 3] << 24);
        }
        internal static ulong U64(byte[] bytes, int offset)
        { return U32(bytes, offset) | ((ulong)U32(bytes, offset + 4) << 32); }
        internal static long Long64(byte[] bytes, int offset)
        {
            ulong value = U64(bytes, offset);
            if (value > Int64.MaxValue) throw new AppError(50, "ZIP の 64 bit 値が Stream の範囲を超えます。");
            return (long)value;
        }
        internal static void Write16(Stream stream, ushort value)
        { stream.WriteByte((byte)value); stream.WriteByte((byte)(value >> 8)); }
        internal static void Write32(Stream stream, uint value)
        {
            stream.WriteByte((byte)value); stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)(value >> 16)); stream.WriteByte((byte)(value >> 24));
        }
        internal static void Write64(Stream stream, ulong value)
        { Write32(stream, (uint)value); Write32(stream, (uint)(value >> 32)); }
        /// <summary>指定バイト数を完全に読む。短い Read を許容し、途中 EOF は例外にする。</summary>
        internal static byte[] ReadBytes(Stream stream, int length)
        {
            byte[] bytes = new byte[length];
            int position = 0;
            while (position < length)
            {
                CheckCancellation();
                int count = stream.Read(bytes, position, length - position);
                if (count == 0) throw new EndOfStreamException("ZIP データが途中で切れています。");
                position += count;
            }
            return bytes;
        }
        /// <summary>加算オーバーフローを避け、[offset, offset+length) が limit 内か検査する。</summary>
        internal static void RequireRange(long offset, long length, long limit, string context)
        {
            if (offset < 0 || length < 0 || offset > limit || length > limit - offset)
                throw new AppError(13, "ZIP 内の範囲が不正です: " + context);
        }
    }

    /// <summary>通常 CRC-32 (ZIP/IEEE)。暗号鍵更新では終了 XOR 前の UpdateRaw を使用する。</summary>
    private sealed class Crc32
    {
        private static readonly uint[] Table = MakeTable();
        private uint state = 0xFFFFFFFFU;
        internal uint Value { get { return state ^ 0xFFFFFFFFU; } }
        internal void Update(byte[] data, int offset, int count)
        {
            for (int i = offset; i < offset + count; i++) state = UpdateRaw(state, data[i]);
        }
        internal static uint UpdateRaw(uint value, byte next)
        { return (value >> 8) ^ Table[(int)((value ^ next) & 255U)]; }
        internal static uint Compute(byte[] data)
        {
            Crc32 crc = new Crc32();
            crc.Update(data, 0, data.Length);
            return crc.Value;
        }
        private static uint[] MakeTable()
        {
            uint[] table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint value = i;
                for (int bit = 0; bit < 8; bit++) value = (value & 1U) == 0 ? value >> 1 : (value >> 1) ^ 0xEDB88320U;
                table[(int)i] = value;
            }
            return table;
        }
    }

    /// <summary>古典的 ZIP 暗号の 3 個の鍵状態。暗号化と復号はそれぞれ平文で状態を更新する。</summary>
    private sealed class ClassicCipher
    {
        private uint first = 0x12345678U;
        private uint second = 0x23456789U;
        private uint third = 0x34567890U;
        internal ClassicCipher(string password)
        {
            byte[] bytes = Utf8.GetBytes(password);
            foreach (byte value in bytes) Update(value);
            Array.Clear(bytes, 0, bytes.Length);
        }
        private byte Mask()
        {
            uint value = (third & 65535U) | 2U;
            return (byte)(unchecked(value * (value ^ 1U)) >> 8);
        }
        private void Update(byte plain)
        {
            first = Crc32.UpdateRaw(first, plain);
            second = unchecked((second + (first & 255U)) * 134775813U + 1U);
            third = Crc32.UpdateRaw(third, (byte)(second >> 24));
        }
        internal byte Encrypt(byte plain)
        {
            byte cipher = (byte)(plain ^ Mask());
            Update(plain);
            return cipher;
        }
        internal byte Decrypt(byte cipher)
        {
            byte plain = (byte)(cipher ^ Mask());
            Update(plain);
            return plain;
        }
        internal void Clear() { first = second = third = 0; }
    }

    /// <summary>ZipCrypto の 12 byte ヘッダを書き、後続の圧縮データを暗号化して流す。</summary>
    private sealed class CryptoWriteStream : Stream
    {
        private readonly Stream destination;
        private readonly ClassicCipher cipher = new ClassicCipher(ZIP_PASSWORD);
        private readonly byte[] work = new byte[BufferSize];
        internal CryptoWriteStream(Stream output, byte checkByte)
        {
            destination = output;
            byte[] header = new byte[12];
            using (RandomNumberGenerator random = new RNGCryptoServiceProvider()) random.GetBytes(header);
            header[11] = checkByte;
            for (int i = 0; i < header.Length; i++) header[i] = cipher.Encrypt(header[i]);
            output.Write(header, 0, header.Length);
        }
        public override bool CanRead { get { return false; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return true; } }
        public override long Length { get { throw new NotSupportedException(); } }
        public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
        public override int Read(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Flush() { destination.Flush(); }
        /// <summary>入力バッファを書き換えず、一定サイズの作業領域だけで暗号化する。</summary>
        public override void Write(byte[] buffer, int offset, int count)
        {
            ValidateBuffer(buffer, offset, count);
            while (count > 0)
            {
                CheckCancellation();
                int length = Math.Min(count, work.Length);
                for (int i = 0; i < length; i++) work[i] = cipher.Encrypt(buffer[offset + i]);
                destination.Write(work, 0, length);
                offset += length;
                count -= length;
            }
        }
        protected override void Dispose(bool disposing)
        {
            cipher.Clear();
            Array.Clear(work, 0, work.Length);
            // 出力本体の所有権は OutputTransaction にあるため閉じない。
            base.Dispose(disposing);
        }
    }

    /// <summary>前進読み取り専用ストリームの共通インターフェース。</summary>
    private abstract class ForwardReadStream : Stream
    {
        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return false; } }
        public override long Length { get { throw new NotSupportedException(); } }
        public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
    }

    /// <summary>ZIP の 1 データ領域を越えて読み出さないストリーム。元ストリームは閉じない。</summary>
    private sealed class BoundedReadStream : ForwardReadStream
    {
        private readonly Stream source;
        internal long Remaining;
        internal BoundedReadStream(Stream input, long length)
        {
            if (length < 0) throw new ArgumentOutOfRangeException("length");
            source = input;
            Remaining = length;
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateBuffer(buffer, offset, count);
            if (count == 0 || Remaining == 0) return 0;
            CheckCancellation();
            int wanted = (int)Math.Min((long)count, Remaining);
            int read = source.Read(buffer, offset, wanted);
            if (read == 0) throw new EndOfStreamException("ZIP の圧縮データが途中で切れています。");
            Remaining -= read;
            return read;
        }
    }

    /// <summary>12 byte ヘッダの検証後に使う ZipCrypto 復号ストリーム。</summary>
    private sealed class ClassicReadStream : ForwardReadStream
    {
        private readonly Stream source;
        private readonly ClassicCipher cipher;
        internal ClassicReadStream(Stream input, byte expected)
        {
            source = input;
            cipher = new ClassicCipher(ZIP_PASSWORD);
            byte[] header = Little.ReadBytes(input, 12);
            for (int i = 0; i < header.Length; i++) header[i] = cipher.Decrypt(header[i]);
            if (header[11] != expected)
            {
                cipher.Clear();
                throw new AppError(13, "ZIP のパスワードが m ではないか、暗号ヘッダが破損しています。");
            }
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateBuffer(buffer, offset, count);
            int read = source.Read(buffer, offset, count);
            for (int i = 0; i < read; i++) buffer[offset + i] = cipher.Decrypt(buffer[offset + i]);
            return read;
        }
        protected override void Dispose(bool disposing) { cipher.Clear(); base.Dispose(disposing); }
    }

    /// <summary>WinZip AES AE-1/AE-2: PBKDF2-HMAC-SHA1、AES-CTR、HMAC-SHA1-80 の復号。</summary>
    private sealed class AesReadStream : ForwardReadStream
    {
        private readonly BoundedReadStream container;
        private readonly BoundedReadStream ciphertext;
        private Aes algorithm;
        private ICryptoTransform encryptor;
        private HMACSHA1 authentication;
        private readonly byte[] counter = new byte[16];
        private readonly byte[] keyStream = new byte[16];
        private int keyPosition = 16;
        private bool verified;
        internal long Remaining { get { return ciphertext.Remaining; } }

        /// <summary>strength は 1/2/3 (128/192/256 bit)。salt と検証値を読み、鍵を初期化する。</summary>
        internal AesReadStream(BoundedReadStream input, byte strength)
        {
            if (strength < 1 || strength > 3) throw new AppError(50, "未対応の AES 鍵長です。");
            container = input;
            int keyLength = 8 + 8 * strength;
            int saltLength = keyLength / 2;
            if (input.Remaining < saltLength + 2L + 10L) throw new AppError(13, "AES データが短すぎます。");
            byte[] salt = Little.ReadBytes(input, saltLength);
            byte[] storedVerification = Little.ReadBytes(input, 2);
            byte[] derived = null;
            byte[] encryptionKey = new byte[keyLength];
            byte[] authenticationKey = new byte[keyLength];
            try
            {
                using (Rfc2898DeriveBytes derivation = new Rfc2898DeriveBytes(ZIP_PASSWORD, salt, 1000))
                    derived = derivation.GetBytes(keyLength * 2 + 2);
                int difference = (derived[keyLength * 2] ^ storedVerification[0]) |
                    (derived[keyLength * 2 + 1] ^ storedVerification[1]);
                if (difference != 0) throw new AppError(13, "AES ZIP のパスワードが m ではないか、データが破損しています。");
                Buffer.BlockCopy(derived, 0, encryptionKey, 0, keyLength);
                Buffer.BlockCopy(derived, keyLength, authenticationKey, 0, keyLength);
                algorithm = Aes.Create();
                if (algorithm == null) throw new CryptographicException("AES プロバイダーがありません。");
                algorithm.Mode = CipherMode.ECB;
                algorithm.Padding = PaddingMode.None;
                algorithm.Key = encryptionKey;
                encryptor = algorithm.CreateEncryptor();
                authentication = new HMACSHA1(authenticationKey);
                ciphertext = new BoundedReadStream(container, container.Remaining - 10L);
            }
            catch
            {
                if (encryptor != null) encryptor.Dispose();
                if (algorithm != null) algorithm.Dispose();
                if (authentication != null) authentication.Dispose();
                throw;
            }
            finally
            {
                if (derived != null) Array.Clear(derived, 0, derived.Length);
                Array.Clear(encryptionKey, 0, encryptionKey.Length);
                Array.Clear(authenticationKey, 0, authenticationKey.Length);
            }
        }

        /// <summary>暗号文を先に HMAC に流し、little endian の 1 始まり CTR で復号する。</summary>
        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateBuffer(buffer, offset, count);
            if (verified && count != 0) return 0;
            int read = ciphertext.Read(buffer, offset, count);
            if (read == 0) return 0;
            authentication.TransformBlock(buffer, offset, read, buffer, offset);
            for (int i = offset; i < offset + read; i++)
            {
                if (keyPosition == 16)
                {
                    int digit = 0;
                    while (digit < counter.Length)
                    {
                        counter[digit] = unchecked((byte)(counter[digit] + 1));
                        if (counter[digit] != 0) break;
                        digit++;
                    }
                    if (digit == counter.Length) throw new CryptographicException("AES カウンターがあふれました。");
                    if (encryptor.TransformBlock(counter, 0, 16, keyStream, 0) != 16)
                        throw new CryptographicException("AES ブロック演算の結果が不正です。");
                    keyPosition = 0;
                }
                buffer[i] = (byte)(buffer[i] ^ keyStream[keyPosition++]);
            }
            return read;
        }

        /// <summary>全暗号文消費後、末尾 10 byte の HMAC を固定回数比較で検証する。</summary>
        internal void VerifyAuthentication()
        {
            if (verified) return;
            if (ciphertext.Remaining != 0) throw new AppError(13, "AES 圧縮データが最後まで消費されませんでした。");
            authentication.TransformFinalBlock(new byte[0], 0, 0);
            byte[] stored = Little.ReadBytes(container, 10);
            byte[] calculated = authentication.Hash;
            int difference = 0;
            for (int i = 0; i < 10; i++) difference |= stored[i] ^ calculated[i];
            if (difference != 0) throw new AppError(13, "AES ZIP の HMAC 認証に失敗しました。誤ったパスワードまたはデータ破損です。");
            if (container.Remaining != 0) throw new AppError(13, "AES データ末尾に余分なデータがあります。");
            verified = true;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (encryptor != null) encryptor.Dispose();
                if (algorithm != null) algorithm.Dispose();
                if (authentication != null) authentication.Dispose();
                Array.Clear(counter, 0, counter.Length);
                Array.Clear(keyStream, 0, keyStream.Length);
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>Stream の公開 Read/Write に渡されたバッファ範囲を検査する。</summary>
    private static void ValidateBuffer(byte[] buffer, int offset, int count)
    {
        if (buffer == null) throw new ArgumentNullException("buffer");
        if (offset < 0 || count < 0 || offset > buffer.Length || count > buffer.Length - offset)
            throw new ArgumentOutOfRangeException("count");
    }

    /// <summary>平文を一定ブロックにまとめて転送し、長さ・CRC-32・SHA-256 を得る。</summary>
    private static ContentResult Transfer(Stream input, Stream target, long expectedLength)
    {
        long total = 0;
        byte[] buffer = new byte[BufferSize];
        Crc32 crc = new Crc32();
        using (SHA256 hash = SHA256.Create())
        {
            if (hash == null) throw new CryptographicException("SHA-256 プロバイダーがありません。");
            bool ended = false;
            while (!ended)
            {
                CheckCancellation();
                int filled = 0;
                while (filled < buffer.Length)
                {
                    CheckCancellation();
                    int count = input.Read(buffer, filled, buffer.Length - filled);
                    if (count == 0) { ended = true; break; }
                    filled += count;
                    if ((long)filled > expectedLength - total)
                        throw new AppError(13, "展開後のサイズが宣言値を超えました。入力変更または ZIP 破損の可能性があります。");
                }
                if (filled != 0)
                {
                    crc.Update(buffer, 0, filled);
                    hash.TransformBlock(buffer, 0, filled, buffer, 0);
                    target.Write(buffer, 0, filled);
                    total = checked(total + filled);
                }
            }
            if (total != expectedLength) throw new AppError(13, "内容の実サイズと宣言値が一致しません。宣言 " +
                FormatNumber(expectedLength) + " bytes / 実際 " + FormatNumber(total) + " bytes。");
            hash.TransformFinalBlock(new byte[0], 0, 0);
            return new ContentResult(total, crc.Value, hash.Hash);
        }
    }

    /// <summary>認証値等を同じ長さなら固定回数で比較する。</summary>
    private static bool FixedEquals(byte[] first, byte[] second)
    {
        if (first == null || second == null || first.Length != second.Length) return false;
        int difference = 0;
        for (int i = 0; i < first.Length; i++) difference |= first[i] ^ second[i];
        return difference == 0;
    }

    /// <summary>入力 ZIP の中央ディレクトリ 1 件。位置やサイズはすべて 64 bit で保持する。</summary>
    private sealed class ZipEntry
    {
        internal string Name;
        internal byte[] RawName;
        internal bool IsDirectory;
        internal ushort Flags;
        internal ushort HeaderMethod;
        internal ushort Method;
        internal uint DosTime;
        internal uint Crc;
        internal long CompressedSize;
        internal long UncompressedSize;
        internal long LocalOffset;
        internal long DataOffset;
        internal long RecordEnd;
        internal ushort AesVersion;
        internal byte AesStrength;
        internal byte[] AesExtra;
        internal bool Encrypted { get { return (Flags & 1) != 0; } }
    }

    /// <summary>必要な ZIP extra field を保持する。未知フィールドも境界だけは必ず検査する。</summary>
    private sealed class ExtraFields
    {
        internal byte[] Zip64;
        internal byte[] Aes;
        internal byte[] UnicodeName;
        internal static ExtraFields Parse(byte[] data)
        {
            ExtraFields result = new ExtraFields();
            int position = 0;
            while (position < data.Length)
            {
                if (data.Length - position < 4) throw new AppError(13, "ZIP extra field のヘッダが切れています。");
                ushort id = Little.U16(data, position);
                int length = Little.U16(data, position + 2);
                position += 4;
                if (length > data.Length - position) throw new AppError(13, "ZIP extra field の長さが不正です。");
                if (id == 0x0001 || id == 0x9901 || id == 0x7075)
                {
                    byte[] value = new byte[length];
                    Buffer.BlockCopy(data, position, value, 0, length);
                    if (id == 0x0001)
                    {
                        if (result.Zip64 != null) throw new AppError(13, "ZIP64 extra field が重複しています。");
                        result.Zip64 = value;
                    }
                    else if (id == 0x9901)
                    {
                        if (result.Aes != null) throw new AppError(13, "AES extra field が重複しています。");
                        result.Aes = value;
                    }
                    else
                    {
                        if (result.UnicodeName != null) throw new AppError(13, "Unicode Path extra field が重複しています。");
                        result.UnicodeName = value;
                    }
                }
                position += length;
            }
            return result;
        }
    }

    /// <summary>入力 ZIP の索引作成と、1 エントリずつのストリーミング展開。ディスクへ展開しない。</summary>
    private sealed class ZipSource
    {
        internal readonly List<ZipEntry> Entries = new List<ZipEntry>();
        private readonly FileSnapshot snapshot;
        private long centralStart;
        private ZipSource(FileSnapshot file) { snapshot = file; }

        /// <summary>ZIP/ZIP64 の索引とローカルヘッダを検査する。壊れた ZIP は通常ファイル扱いしない。</summary>
        internal static ZipSource Load(FileSnapshot file)
        {
            try
            {
                ZipSource source = new ZipSource(file);
                using (FileStream stream = file.Open()) source.ReadIndex(stream);
                return source;
            }
            catch (Exception ex)
            {
                throw new AppError(ErrorCode(ex), "入力 ZIP を読み取れません: " + file.Path, ex);
            }
        }

        /// <summary>EOCD を末尾から探し、中央ディレクトリと ZIP64 の宣言値を相互検査する。</summary>
        private void ReadIndex(Stream stream)
        {
            if (stream.Length < 22) throw new AppError(13, "ZIP 終端レコードがありません。");
            int tailLength = (int)Math.Min(stream.Length, 65557L);
            long tailStart = stream.Length - tailLength;
            stream.Position = tailStart;
            byte[] tail = Little.ReadBytes(stream, tailLength);
            int endIndex = -1;
            for (int i = tail.Length - 22; i >= 0; i--)
            {
                if (Little.U32(tail, i) == 0x06054B50U && i + 22 + Little.U16(tail, i + 20) == tail.Length)
                { endIndex = i; break; }
            }
            if (endIndex < 0) throw new AppError(13, "有効な ZIP 終端レコードが見つかりません。末尾追記データ付き ZIP は受け付けません。");
            long endOffset = tailStart + endIndex;
            ushort disk = Little.U16(tail, endIndex + 4);
            ushort startDisk = Little.U16(tail, endIndex + 6);
            ushort countOnDisk = Little.U16(tail, endIndex + 8);
            ushort count16 = Little.U16(tail, endIndex + 10);
            long directorySize = Little.U32(tail, endIndex + 12);
            long directoryOffset = Little.U32(tail, endIndex + 16);
            long count = count16;
            if (disk != 0 || startDisk != 0 || countOnDisk != count16)
                throw new AppError(50, "マルチボリューム ZIP は入力できません。独立した ZIP を指定してください。");
            bool needs64 = count16 == UInt16.MaxValue || directorySize == Zip32Limit || directoryOffset == Zip32Limit;
            bool hasLocator = false;
            byte[] locator = null;
            long directoryBoundary = endOffset;
            if (endOffset >= 20)
            {
                stream.Position = endOffset - 20;
                locator = Little.ReadBytes(stream, 20);
                hasLocator = Little.U32(locator, 0) == 0x07064B50U;
            }
            if (needs64 && !hasLocator) throw new AppError(13, "必要な ZIP64 locator がありません。");
            if (hasLocator)
            {
                if (Little.U32(locator, 4) != 0 || Little.U32(locator, 16) != 1)
                    throw new AppError(50, "複数ディスクの ZIP64 は対応していません。");
                long offset64 = Little.Long64(locator, 8);
                Little.RequireRange(offset64, 56, endOffset - 20, "ZIP64 EOCD");
                stream.Position = offset64;
                byte[] end64 = Little.ReadBytes(stream, 56);
                if (Little.U32(end64, 0) != 0x06064B50U) throw new AppError(13, "ZIP64 EOCD の署名が不正です。");
                long length64 = Little.Long64(end64, 4);
                if (length64 < 44) throw new AppError(13, "ZIP64 EOCD が短すぎます。");
                Little.RequireRange(offset64 + 12, length64, endOffset - 20, "ZIP64 EOCD 長");
                if (offset64 + 12 + length64 != endOffset - 20)
                    throw new AppError(13, "ZIP64 EOCD と locator の配置が不正です。");
                if (Little.U32(end64, 16) != 0 || Little.U32(end64, 20) != 0 ||
                    Little.U64(end64, 24) != Little.U64(end64, 32))
                    throw new AppError(50, "マルチボリュームまたは不正な ZIP64 です。");
                count = Little.Long64(end64, 32);
                directorySize = Little.Long64(end64, 40);
                directoryOffset = Little.Long64(end64, 48);
                directoryBoundary = offset64;
                if (count16 != UInt16.MaxValue && count16 != count)
                    throw new AppError(13, "ZIP と ZIP64 のエントリ数が一致しません。");
                uint size32 = Little.U32(tail, endIndex + 12);
                uint offset32 = Little.U32(tail, endIndex + 16);
                if ((size32 != UInt32.MaxValue && size32 != directorySize) ||
                    (offset32 != UInt32.MaxValue && offset32 != directoryOffset))
                    throw new AppError(13, "ZIP と ZIP64 の中央ディレクトリ情報が一致しません。");
            }
            Little.RequireRange(directoryOffset, directorySize, directoryBoundary, "中央ディレクトリ");
            if (directoryOffset + directorySize != directoryBoundary)
                throw new AppError(50, "中央ディレクトリの配置が未対応または破損しています。自己解凍・特殊拡張 ZIP は通常の ZIP に変換してください。");
            if (count > Int32.MaxValue || count > directorySize / 46L)
                throw new AppError(13, "ZIP のエントリ数が不正または処理可能範囲を超えています。");
            centralStart = directoryOffset;
            long centralEnd = directoryOffset + directorySize;
            stream.Position = centralStart;
            for (long index = 0; index < count; index++)
            {
                CheckCancellation();
                Entries.Add(ReadCentralEntry(stream, centralEnd));
            }
            if (stream.Position != centralEnd)
            {
                // APPNOTE の任意の中央ディレクトリ署名だけは読み飛ばせる。署名の真正性は検証しない。
                Little.RequireRange(stream.Position, 6, centralEnd, "中央ディレクトリ署名");
                byte[] signature = Little.ReadBytes(stream, 6);
                if (Little.U32(signature, 0) != 0x05054B50U ||
                    stream.Position + Little.U16(signature, 4) != centralEnd)
                    throw new AppError(50, "未対応の中央ディレクトリ末尾構造です。");
                stream.Position = centralEnd;
            }
            List<ZipEntry> byOffset = new List<ZipEntry>(Entries);
            byOffset.Sort(delegate (ZipEntry first, ZipEntry second) { return first.LocalOffset.CompareTo(second.LocalOffset); });
            for (int i = 0; i < byOffset.Count; i++)
            {
                CheckCancellation();
                long limit = i + 1 == byOffset.Count ? centralStart : byOffset[i + 1].LocalOffset;
                if (byOffset[i].LocalOffset >= limit)
                    throw new AppError(13, "ローカルヘッダの位置が重複または逆転しています: " + byOffset[i].Name);
                ValidateLocal(stream, byOffset[i], limit);
            }
        }

        /// <summary>中央ディレクトリエントリを読み、圧縮方式・暗号方式・名前の文字コードを判定する。</summary>
        private static ZipEntry ReadCentralEntry(Stream stream, long limit)
        {
            Little.RequireRange(stream.Position, 46, limit, "中央ヘッダ");
            byte[] header = Little.ReadBytes(stream, 46);
            if (Little.U32(header, 0) != 0x02014B50U) throw new AppError(13, "中央ディレクトリの署名が不正です。");
            ZipEntry entry = new ZipEntry();
            entry.Flags = Little.U16(header, 8);
            entry.HeaderMethod = Little.U16(header, 10);
            entry.Method = entry.HeaderMethod;
            entry.DosTime = Little.U32(header, 12);
            entry.Crc = Little.U32(header, 16);
            entry.CompressedSize = Little.U32(header, 20);
            entry.UncompressedSize = Little.U32(header, 24);
            int nameLength = Little.U16(header, 28);
            int extraLength = Little.U16(header, 30);
            int commentLength = Little.U16(header, 32);
            uint disk = Little.U16(header, 34);
            uint attributes = Little.U32(header, 38);
            entry.LocalOffset = Little.U32(header, 42);
            if (nameLength == 0) throw new AppError(13, "名前のない ZIP エントリがあります。");
            Little.RequireRange(stream.Position, (long)nameLength + extraLength + commentLength, limit, "中央可変フィールド");
            entry.RawName = Little.ReadBytes(stream, nameLength);
            ExtraFields extra = ExtraFields.Parse(Little.ReadBytes(stream, extraLength));
            stream.Position += commentLength;
            int position64 = 0;
            if (entry.UncompressedSize == Zip32Limit) entry.UncompressedSize = ReadExtra64(extra.Zip64, ref position64);
            if (entry.CompressedSize == Zip32Limit) entry.CompressedSize = ReadExtra64(extra.Zip64, ref position64);
            if (entry.LocalOffset == Zip32Limit) entry.LocalOffset = ReadExtra64(extra.Zip64, ref position64);
            if (disk == UInt16.MaxValue)
            {
                if (extra.Zip64 == null || extra.Zip64.Length - position64 < 4) throw new AppError(13, "ZIP64 ディスク番号がありません。");
                disk = Little.U32(extra.Zip64, position64);
            }
            if (disk != 0) throw new AppError(50, "複数ディスクにまたがる ZIP エントリです。");
            if ((entry.Flags & ~0x080F) != 0)
                throw new AppError(50, "未対応の ZIP フラグです (中央ディレクトリ暗号化/Strong Encryption 等): 0x" +
                    entry.Flags.ToString("X4", NumberCulture));
            entry.Name = DecodeName(entry.RawName, extra.UnicodeName, (entry.Flags & 0x0800) != 0);
            int host = Little.U16(header, 4) >> 8;
            uint unixType = (attributes >> 16) & 0xF000U;
            if (((host == 3 || host == 19) && unixType == 0xA000U) ||
                (attributes & (uint)FileAttributes.ReparsePoint) != 0)
                throw new AppError(50, "ZIP 内のリンク/再解析ポイントは処理しません: " + entry.Name);
            if ((host == 3 || host == 19) && unixType != 0 && unixType != 0x8000U && unixType != 0x4000U)
                throw new AppError(50, "ZIP 内の特殊ファイルは処理しません: " + entry.Name);
            entry.IsDirectory = entry.Name.EndsWith("/", StringComparison.Ordinal) ||
                entry.Name.EndsWith("\\", StringComparison.Ordinal) || (attributes & 0x10U) != 0 ||
                ((host == 3 || host == 19) && unixType == 0x4000U);
            PathRules.ArchivePath(entry.Name, entry.IsDirectory);
            if (entry.IsDirectory && entry.UncompressedSize != 0)
                throw new AppError(13, "ZIP のディレクトリエントリに内容があります: " + entry.Name);
            if (entry.HeaderMethod == 99)
            {
                if (!entry.Encrypted || extra.Aes == null || extra.Aes.Length != 7 ||
                    extra.Aes[2] != (byte)'A' || extra.Aes[3] != (byte)'E')
                    throw new AppError(13, "WinZip AES extra field が不正です: " + entry.Name);
                entry.AesVersion = Little.U16(extra.Aes, 0);
                entry.AesStrength = extra.Aes[4];
                entry.Method = Little.U16(extra.Aes, 5);
                entry.AesExtra = extra.Aes;
                if ((entry.AesVersion != 1 && entry.AesVersion != 2) || entry.AesStrength < 1 || entry.AesStrength > 3)
                    throw new AppError(50, "未対応の AES バージョンまたは鍵長です: " + entry.Name);
                if (entry.AesVersion == 2 && entry.Crc != 0)
                    throw new AppError(13, "AE-2 の CRC フィールドが 0 ではありません: " + entry.Name);
                int overhead = (8 + 8 * entry.AesStrength) / 2 + 12;
                if (entry.CompressedSize < overhead) throw new AppError(13, "AES データが短すぎます: " + entry.Name);
            }
            else
            {
                if (extra.Aes != null) throw new AppError(13, "AES extra field と圧縮方式が矛盾しています: " + entry.Name);
                if (entry.Encrypted && entry.CompressedSize < 12)
                    throw new AppError(13, "ZipCrypto ヘッダがありません: " + entry.Name);
            }
            if (entry.Method != 0 && entry.Method != 8)
                throw new AppError(50, "ZIP 内の圧縮方式 " + entry.Method.ToString(NumberCulture) +
                    " は未対応です。Store (0) または Deflate (8) を使用してください: " + entry.Name);
            if (entry.Method == 0 && !entry.Encrypted && entry.CompressedSize != entry.UncompressedSize)
                throw new AppError(13, "Store エントリのサイズが一致しません: " + entry.Name);
            return entry;
        }

        /// <summary>ZIP64 extra から次の 64 bit 値を読む。値のない sentinel を許さない。</summary>
        private static long ReadExtra64(byte[] data, ref int position)
        {
            if (data == null || data.Length - position < 8) throw new AppError(13, "必要な ZIP64 extra 値がありません。");
            long value = Little.Long64(data, position);
            position += 8;
            return value;
        }

        /// <summary>UTF-8 指定、CRC が一致する Unicode Path、CP932 (既定) の優先順でファイル名を復号する。</summary>
        /// <param name="raw">区切り文字で分割する前の名前バイト列。CP932 の 2 byte 目の 0x5C も保つ。</param>
        /// <param name="unicode">0x7075 extra のデータ。ない場合は null。</param>
        /// <param name="utf8">general purpose bit 11。true の不正 UTF-8 は他の文字コードで救済しない。</param>
        /// <returns>復号後の名前。安全な相対パスかどうかの検査は呼出側で行う。</returns>
        internal static string DecodeName(byte[] raw, byte[] unicode, bool utf8)
        {
            try
            {
                if (utf8) return Utf8.GetString(raw);
                if (unicode != null && unicode.Length >= 5 && unicode[0] == 1 && Little.U32(unicode, 1) == Crc32.Compute(raw))
                    return Utf8.GetString(unicode, 5, unicode.Length - 5);
                Encoding legacy = Encoding.GetEncoding(LEGACY_ZIP_CODE_PAGE,
                    EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
                try { return legacy.GetString(raw); }
                catch (DecoderFallbackException)
                {
                    // 無指定の旧名で、選択した文字コードとして不正な場合だけ CP437 へフォールバック。
                    // UTF-8 指定/Unicode Path の不正は、この内側の catch では捕まえない。
                    Encoding fallback = Encoding.GetEncoding(437,
                        EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
                    return fallback.GetString(raw);
                }
            }
            catch (DecoderFallbackException ex)
            {
                throw new AppError(13, "ZIP のファイル名を明示された文字コードで解釈できません。" +
                    "UTF-8 フラグまたは Unicode Path extra field を確認してください。", ex);
            }
        }

        /// <summary>中央とローカルの整合性・データ範囲・descriptor を確認し、安全な読み取り位置を設定する。</summary>
        private static void ValidateLocal(Stream stream, ZipEntry entry, long limit)
        {
            Little.RequireRange(entry.LocalOffset, 30, limit, "ローカルヘッダ: " + entry.Name);
            stream.Position = entry.LocalOffset;
            byte[] local = Little.ReadBytes(stream, 30);
            if (Little.U32(local, 0) != 0x04034B50U) throw new AppError(13, "ローカルヘッダの署名が不正です: " + entry.Name);
            if (Little.U16(local, 6) != entry.Flags || Little.U16(local, 8) != entry.HeaderMethod ||
                Little.U32(local, 10) != entry.DosTime)
                throw new AppError(13, "中央とローカルのメタデータが一致しません: " + entry.Name);
            uint localCrc = Little.U32(local, 14);
            long localCompressed = Little.U32(local, 18);
            long localUncompressed = Little.U32(local, 22);
            int nameLength = Little.U16(local, 26);
            int extraLength = Little.U16(local, 28);
            Little.RequireRange(stream.Position, (long)nameLength + extraLength, limit, "ローカル可変フィールド: " + entry.Name);
            byte[] name = Little.ReadBytes(stream, nameLength);
            if (!FixedEquals(name, entry.RawName)) throw new AppError(13, "中央とローカルのファイル名が一致しません: " + entry.Name);
            ExtraFields extra = ExtraFields.Parse(Little.ReadBytes(stream, extraLength));
            bool local64 = localCompressed == Zip32Limit || localUncompressed == Zip32Limit;
            if (local64)
            {
                if (extra.Zip64 == null || extra.Zip64.Length < 16) throw new AppError(13, "ローカル ZIP64 サイズがありません: " + entry.Name);
                localUncompressed = Little.Long64(extra.Zip64, 0);
                localCompressed = Little.Long64(extra.Zip64, 8);
            }
            if (entry.HeaderMethod == 99)
            {
                if (!FixedEquals(extra.Aes, entry.AesExtra)) throw new AppError(13, "中央とローカルの AES 情報が一致しません: " + entry.Name);
            }
            else if (extra.Aes != null) throw new AppError(13, "ローカル AES 情報が矛盾しています: " + entry.Name);
            bool descriptor = (entry.Flags & 8) != 0;
            if (!descriptor)
            {
                if (localCrc != entry.Crc || localCompressed != entry.CompressedSize || localUncompressed != entry.UncompressedSize)
                    throw new AppError(13, "中央とローカルの CRC/サイズが一致しません: " + entry.Name);
            }
            else if ((localCrc != 0 && localCrc != entry.Crc) ||
                (localCompressed != 0 && localCompressed != entry.CompressedSize) ||
                (localUncompressed != 0 && localUncompressed != entry.UncompressedSize))
                throw new AppError(13, "ローカルヘッダの既知 CRC/サイズが矛盾しています: " + entry.Name);
            entry.DataOffset = stream.Position;
            Little.RequireRange(entry.DataOffset, entry.CompressedSize, limit, "圧縮データ: " + entry.Name);
            entry.RecordEnd = entry.DataOffset + entry.CompressedSize;
            if (descriptor)
                entry.RecordEnd = ValidateDescriptor(stream, entry, limit, local64 || entry.CompressedSize >= Zip32Limit || entry.UncompressedSize >= Zip32Limit);
            Little.RequireRange(entry.LocalOffset, entry.RecordEnd - entry.LocalOffset, limit, "エントリ全体: " + entry.Name);
        }

        /// <summary>署名あり/なしの data descriptor を検査する。CRC が署名値に等しい場合も試す。</summary>
        private static long ValidateDescriptor(Stream stream, ZipEntry entry, long limit, bool large)
        {
            long start = entry.DataOffset + entry.CompressedSize;
            int available = (int)Math.Min(24L, limit - start);
            if (available < 12) throw new AppError(13, "data descriptor がありません: " + entry.Name);
            stream.Position = start;
            byte[] data = Little.ReadBytes(stream, available);
            if (Little.U32(data, 0) == 0x08074B50U && MatchesDescriptor(data, 4, large, entry))
                return start + 4 + (large ? 20 : 12);
            if (MatchesDescriptor(data, 0, large, entry)) return start + (large ? 20 : 12);
            throw new AppError(13, "data descriptor の CRC またはサイズが一致しません: " + entry.Name);
        }

        /// <summary>descriptor の候補位置の CRC と両サイズを中央ディレクトリと比較する。</summary>
        private static bool MatchesDescriptor(byte[] data, int start, bool large, ZipEntry entry)
        {
            int length = large ? 20 : 12;
            if (data.Length - start < length || Little.U32(data, start) != entry.Crc) return false;
            if (large)
                return Little.U64(data, start + 4) == (ulong)entry.CompressedSize &&
                    Little.U64(data, start + 12) == (ulong)entry.UncompressedSize;
            return Little.U32(data, start + 4) == entry.CompressedSize && Little.U32(data, start + 8) == entry.UncompressedSize;
        }

        /// <summary>現在の ZIP ストリームから 1 項目を復号・展開する。元ストリームの所有権は移さない。</summary>
        internal static ContentResult DecodePayload(Stream file, ZipEntry entry, Stream target)
        {
            file.Position = entry.DataOffset;
            BoundedReadStream bounded = new BoundedReadStream(file, entry.CompressedSize);
            Stream packed = bounded;
            ClassicReadStream classic = null;
            AesReadStream aes = null;
            try
            {
                if (entry.HeaderMethod == 99)
                {
                    aes = new AesReadStream(bounded, entry.AesStrength);
                    packed = aes;
                }
                else if (entry.Encrypted)
                {
                    byte check = (entry.Flags & 8) != 0 ? (byte)(entry.DosTime >> 8) : (byte)(entry.Crc >> 24);
                    classic = new ClassicReadStream(bounded, check);
                    packed = classic;
                }
                ContentResult content;
                long packedLength = aes != null ? aes.Remaining : bounded.Remaining;
                if (entry.Method == 8 && packedLength != 0)
                {
                    using (DeflateStream decompressor = new DeflateStream(packed, CompressionMode.Decompress, true))
                        content = Transfer(decompressor, target, entry.UncompressedSize);
                }
                else
                {
                    // ゼロ長項目で圧縮データ自体を持たない ZIP も認める。
                    content = Transfer(packed, target, entry.UncompressedSize);
                }
                if (aes != null) aes.VerifyAuthentication();
                if (bounded.Remaining != 0) throw new AppError(13, "未消費の圧縮データが残りました: " + entry.Name);
                if (entry.AesVersion != 2 && content.Crc != entry.Crc)
                    throw new AppError(13, "展開後の CRC が一致しません。パスワードが m でないか、内容が破損しています: " + entry.Name);
                return content;
            }
            finally
            {
                if (classic != null) classic.Dispose();
                if (aes != null) aes.Dispose();
            }
        }

#if DNNT_SELF_TEST
        /// <summary>自己テスト専用。メモリ上の ZIP の索引を本番と同じ検査経路で読む。</summary>
        internal static List<ZipEntry> InspectMemory(Stream stream)
        {
            ZipSource source = new ZipSource(null);
            source.ReadIndex(stream);
            return source.Entries;
        }
#endif

        /// <summary>キャッシュ使用時の入力 ZIP の状態照合用ハンドル。復号・展開は事前処理で検証済み。</summary>
        internal Stream OpenValidationStream() { return snapshot.Open(); }

        /// <summary>索引済みエントリを復号・展開し、実サイズ、CRC または AES 認証を検証する。</summary>
        internal ContentResult CopyEntry(ZipEntry entry, Stream target)
        {
            try
            {
                using (FileStream file = snapshot.Open())
                {
                    return DecodePayload(file, entry, target);
                }
            }
            catch (Exception ex)
            {
                throw new AppError(ErrorCode(ex), "ZIP 内ファイルの読み取りに失敗しました: " + snapshot.Path + " :: " + entry.Name, ex);
            }
        }
    }


#if DNNT_SELF_TEST
    /// <summary>コンパイル記号 DNNT_SELF_TEST を指定した場合だけ入る、ファイルを作らない自己テスト。</summary>
    private static class SelfTests
    {
        private static int assertions;
        private static readonly string[] Fixtures = new string[]
        {
            "UEsDBDMAAQhjAABgRF16H501RwAAADAAAAAUAAsA5pqX5Y+3L+aXpeacrOiqni50eHQBmQcAAQBBRQEIAAECAwQFBgcIq/Q6h+p2nv8CL8o3+JNLrJgDk/tYZLTtA5pFyaFGN4KnxfFLGq8mDXP2GniZeeF562IaYyI5/sterU1Rme8MUEsBAjMAMwABCGMAAGBEXXofnTVHAAAAMAAAABQACwAAAAAAAAAgAAAAAAAAAOaal+WPty/ml6XmnKzoqp4udHh0AZkHAAEAQUUBCABQSwUGAAAAAAEAAQBNAAAAhAAAAAAA",
            "UEsDBDMAAQhjAABgRF16H501SwAAADAAAAAUAAsA5pqX5Y+3L+aXpeacrOiqni50eHQBmQcAAQBBRQIIAAECAwQFBgcICQoLDIP54Xzyio6T1haJdnN/MkXIkVIj+oHxmeUp6TXyFmXFykvpfYg0U2bmeDKuQwV8/9ipjyZchobG/L30F2umHFBLAQIzADMAAQhjAABgRF16H501SwAAADAAAAAUAAsAAAAAAAAAIAAAAAAAAADmmpflj7cv5pel5pys6KqeLnR4dAGZBwABAEFFAggAUEsFBgAAAAABAAEATQAAAIgAAAAAAA==",
            "UEsDBDMAAQhjAABgRF16H501TwAAADAAAAAUAAsA5pqX5Y+3L+aXpeacrOiqni50eHQBmQcAAQBBRQMIAAECAwQFBgcICQoLDA0ODxD3sR6pdk5sdnwmvNYHEo4NModTRraeGvrNVOoAUao8uK6apMhM8IjyM0ESdk695mvm4Gb0rf3+tvHZjdgkl3dQSwECMwAzAAEIYwAAYERdeh+dNU8AAAAwAAAAFAALAAAAAAAAACAAAAAAAAAA5pqX5Y+3L+aXpeacrOiqni50eHQBmQcAAQBBRQMIAFBLBQYAAAAAAQABAE0AAACMAAAAAAA=",
            "UEsDBDMAAQhjAABgRF0AAAAARwAAADAAAAAUAAsA5pqX5Y+3L+aXpeacrOiqni50eHQBmQcAAgBBRQEIAAECAwQFBgcIq/Q6h+p2nv8CL8o3+JNLrJgDk/tYZLTtA5pFyaFGN4KnxfFLGq8mDXP2GniZeeF562IaYyI5/sterU1Rme8MUEsBAjMAMwABCGMAAGBEXQAAAABHAAAAMAAAABQACwAAAAAAAAAgAAAAAAAAAOaal+WPty/ml6XmnKzoqp4udHh0AZkHAAIAQUUBCABQSwUGAAAAAAEAAQBNAAAAhAAAAAAA",
            "UEsDBDMAAQhjAABgRF0AAAAASwAAADAAAAAUAAsA5pqX5Y+3L+aXpeacrOiqni50eHQBmQcAAgBBRQIIAAECAwQFBgcICQoLDIP54Xzyio6T1haJdnN/MkXIkVIj+oHxmeUp6TXyFmXFykvpfYg0U2bmeDKuQwV8/9ipjyZchobG/L30F2umHFBLAQIzADMAAQhjAABgRF0AAAAASwAAADAAAAAUAAsAAAAAAAAAIAAAAAAAAADmmpflj7cv5pel5pys6KqeLnR4dAGZBwACAEFFAggAUEsFBgAAAAABAAEATQAAAIgAAAAAAA==",
            "UEsDBDMAAQhjAABgRF0AAAAATwAAADAAAAAUAAsA5pqX5Y+3L+aXpeacrOiqni50eHQBmQcAAgBBRQMIAAECAwQFBgcICQoLDA0ODxD3sR6pdk5sdnwmvNYHEo4NModTRraeGvrNVOoAUao8uK6apMhM8IjyM0ESdk695mvm4Gb0rf3+tvHZjdgkl3dQSwECMwAzAAEIYwAAYERdAAAAAE8AAAAwAAAAFAALAAAAAAAAACAAAAAAAAAA5pqX5Y+3L+aXpeacrOiqni50eHQBmQcAAgBBRQMIAFBLBQYAAAAAAQABAE0AAACMAAAAAAA=",
            "UEsDBBQAAQgIAABgRF16H501PwAAADAAAAAUAAAA5pqX5Y+3L+aXpeacrOiqni50eHSOU2BYXjv22UkU08Mm2Oerqm/pMcC4vUkXAUS1SwQJxyLNI7KV1TOpoHkWB4yvy6fw8FRx5fGRflD8EiAdyIpQSwECFAAUAAEICAAAYERdeh+dNT8AAAAwAAAAFAAAAAAAAAAAACAAAAAAAAAA5pqX5Y+3L+aXpeacrOiqni50eHRQSwUGAAAAAAEAAQBCAAAAcQAAAAAA",
            "UEsDBBQACQgIAABgRF0AAAAAAAAAAAAAAAAUAAAA5pqX5Y+3L+aXpeacrOiqni50eHSOU2BYXjv22UkU05Z4x5g307rNw7RZGFTEqSeSDJcem5XXNPQ4TOeL4KzW96xPzPD5qNFMON7FqhGIsk0XS3lQSwcIeh+dNT8AAAAwAAAAUEsBAhQAFAAJCAgAAGBEXXofnTU/AAAAMAAAABQAAAAAAAAAAAAgAAAAAAAAAOaal+WPty/ml6XmnKzoqp4udHh0UEsFBgAAAAABAAEAQgAAAIEAAAAAAA==",
            "UEsDBC0AAAgIAABgRF16H501//////////8UABQA5pqX5Y+3L+aXpeacrOiqni50eHQBABAAMAAAAAAAAAAzAAAAAAAAAAvPzIvKLFBwdA1WeNzc9rhp5+PmDiuFx42THzdNfty4+nHjwseN63m5DI2MTUzNzC0sAVBLAQItAC0AAAgIAABgRF16H501//////////8UABwAAAAAAAAAIAAAAP/////mmpflj7cv5pel5pys6KqeLnR4dAEAGAAwAAAAAAAAADMAAAAAAAAAAAAAAAAAAABQSwYGLAAAAAAAAAAtAC0AAAAAAAAAAAABAAAAAAAAAAEAAAAAAAAAXgAAAAAAAAB5AAAAAAAAAFBLBgcAAAAA1wAAAAAAAAABAAAAUEsFBgAAAAD///////////////8AAA=="
        };

        /// <summary>純粋なメモリ入出力で形式・暗号・巻戻し・境界を検査し、成功なら 0。</summary>
        internal static int Run()
        {
            try
            {
                Console.WriteLine("DNNT ZIP 自己テスト (ディスクへのテストファイル作成なし)");
                Check(Crc32.Compute(Encoding.ASCII.GetBytes("123456789")) == 0xCBF43926U, "CRC-32 既知値");
                TestClassicVector();
                TestNames();
                TestInputZipRootNames();
                TestLegacyNames();
                TestDirectoryTraversalRules();
                TestResultLayout();
                TestRoundTrip();
                TestRollback();
                TestExternalFixtures();
                TestSizeFormula();
                TestCountBoundary();
                TestPackingPlans();
                TestPlannedLayouts();
                TestPrecompressionCache();
                Console.WriteLine("PASS: " + assertions.ToString(NumberCulture) + " assertions");
                Console.WriteLine("保存ダイアログ・Win32 リンク解決・削除保留・実ファイル権限の試験は別途必要です。");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL: " + ex.ToString());
                return 1;
            }
        }

        /// <summary>テスト項目を検査し、失敗時は項目名を含む例外。</summary>
        private static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("自己テスト失敗: " + name);
            assertions++;
        }

        /// <summary>意図的に不正な入力が、想定する検証例外として拒否されるかを確認する。</summary>
        private static void ExpectFailure(Action action, string name)
        {
            bool failed = false;
            try { action(); }
            catch (AppError) { failed = true; }
            catch (InvalidDataException) { failed = true; }
            catch (EndOfStreamException) { failed = true; }
            catch (CryptographicException) { failed = true; }
            Check(failed, name);
        }

        /// <summary>Python zipfile と zlib で独立照合した、古典暗号の固定ベクトル。</summary>
        private static void TestClassicVector()
        {
            byte[] plain = new byte[32];
            for (int i = 0; i < plain.Length; i++) plain[i] = (byte)i;
            ClassicCipher encrypt = new ClassicCipher("m");
            byte[] encoded = new byte[plain.Length];
            for (int i = 0; i < plain.Length; i++) encoded[i] = encrypt.Encrypt(plain[i]);
            string hex = BitConverter.ToString(encoded).Replace("-", "");
            Check(hex == "8E5360585E3BF6D94914D3FD73F6B278A2981113952EBC351B016F15D4B4BF2A", "ZipCrypto 外部固定ベクトル");
            ClassicCipher decrypt = new ClassicCipher("m");
            for (int i = 0; i < encoded.Length; i++) encoded[i] = decrypt.Decrypt(encoded[i]);
            Check(FixedEquals(encoded, plain), "ZipCrypto 復号");
        }

        /// <summary>削除対象の完全一致、危険なパス、および大小文字を無視した全体衝突の検査。</summary>
        private static void TestNames()
        {
            string[] yes = { "a.zip", "A.ZIP", "a.02.zip", "A.00.ZIP", "a.01.zip", "a.19.zip", "a.20.zip", "a.99.zip" };
            string[] no = { "a.2.zip", "a.002.zip", "a.0000.zip", "a.9999.zip", "a.100.zip", "a.00000.zip", "a.10000.zip", "a.０２.zip", "a.02.zip.bak",
                "aX02.zip", "ab.02.zip", "a..zip", "a. 2.zip", "a.-2.zip", "a.02a.zip", "sub/a.02.zip", "sub\\a.02.zip" };
            foreach (string name in yes) Check(OutputNames.MatchesOldName(name, "a.zip", "a"), "削除対象: " + name);
            foreach (string name in no) Check(!OutputNames.MatchesOldName(name, "a.zip", "a"), "削除禁止: " + name);
            Check(OutputNames.MatchesOldName("a+b.[x].02.zip", "a+b.[x].zip", "a+b.[x]"), "正規表現メタ文字を含むベース名");
            string[] unsafeNames = { "../x", "/x", "C:/x", "a//x", "a/../x", "a\\..\\x", "CON", "a/NUL.txt", "a:x", "x.", "x " };
            foreach (string name in unsafeNames)
            {
                string local = name;
                ExpectFailure(delegate { PathRules.ArchivePath(local, false); }, "危険パス: " + name);
            }
            Check(PathRules.ArchivePath("日本語/かな.txt", false) == "日本語/かな.txt", "UTF-8 名前の保持");
            Manifest manifest = new Manifest();
            manifest.AddFile("A/Name.txt", MakeSource("unused", new byte[0]));
            ExpectFailure(delegate { manifest.AddFile("a/NAME.TXT", MakeSource("other", new byte[0])); }, "大小文字を無視した重複");
            ExpectFailure(delegate { manifest.AddFile("a", MakeSource("other", new byte[0])); }, "ファイルとフォルダの衝突");
            Manifest second = new Manifest();
            second.AddFile("x", MakeSource("unused", new byte[0]));
            ExpectFailure(delegate { second.AddFile("X/y", MakeSource("other", new byte[0])); }, "親が既存ファイル");
        }

        /// <summary>入力 ZIP の拡張子除去、通常入力の維持、除去後の衝突、暗号化 ZIP の名前を検査。</summary>
        private static void TestInputZipRootNames()
        {
            string[,] examples = {
                { @"C:\Data\old.zip", "old" },
                { @"C:\Data\OLD.ZIP", "OLD" },
                { @"C:\Data\old.ZiP", "old" },
                { @"C:\Data\archive.zip.zip", "archive.zip" },
                { @"C:\Data\report.v1.zip", "report.v1" },
                { @"C:\Data\日本語 資料.zip", "日本語 資料" },
                { @"\\server\share\資料.ZIP", "資料" },
                { @"C:\parent.zip\child.zip", "child" }
            };
            for (int i = 0; i < examples.GetLength(0); i++)
                Check(PathRules.ZipRootName(examples[i, 0]) == examples[i, 1], "ZIP 仮想ルート: " + examples[i, 0]);
            Check(PathRules.RootName(@"C:\Data\folder.zip") == "folder.zip", "物理フォルダ名は拡張子を除去しない");
            Check(PathRules.RootName(@"C:\Data\report.txt") == "report.txt", "通常ファイル名は変更しない");
            Check(PathRules.ArchivePath("Photos/backup.zip", false) == "Photos/backup.zip", "フォルダ配下の ZIP 名は変更しない");
            string[] invalid = { @"C:\Data\.zip", @"C:\Data\..zip", @"C:\Data\...zip", @"C:\Data\a..zip", @"C:\Data\a .zip" };
            foreach (string path in invalid)
            {
                string local = path;
                ExpectFailure(delegate { PathRules.ZipRootName(local); }, "除去後の空名・不正名を拒否: " + path);
            }

            string rootName = PathRules.ZipRootName(@"C:\Data\Old.ZIP");
            Manifest names = new Manifest();
            names.AddDirectory(rootName, @"C:\Data\Old.ZIP", 0);
            SourceItem fromZip = MakeSource(rootName + "/docs/readme.txt", Utf8.GetBytes("from ZIP"));
            fromZip.Origin = @"C:\Data\Old.ZIP :: docs/readme.txt";
            names.AddFile(fromZip.ZipPath, fromZip);
            Check(fromZip.ZipPath == "Old/docs/readme.txt", "新しいルート名をファイル相対パスへ反映");
            ExpectFailure(delegate {
                SourceItem physical = MakeSource("old/DOCS/README.TXT", new byte[0]);
                physical.Origin = @"C:\Data\old\DOCS\README.TXT";
                names.AddFile(physical.ZipPath, physical);
            }, "実フォルダと入力 ZIP の拡張子除去後の大小文字衝突");
            ExpectFailure(delegate {
                names.AddFile(PathRules.ZipRootName(@"D:\Other\OLD.zip") + "/docs/readme.txt",
                    MakeSource("second ZIP", new byte[0]));
            }, "異なる入力 ZIP の除去後の同名衝突");
            ExpectFailure(delegate { names.AddFile("old", MakeSource("physical old", new byte[0])); },
                "通常ファイル名と仮想ルート名の衝突");
            names.AddDirectory("old", @"C:\Data\old", 0);
            names.AddFile("old/other.txt", MakeSource("physical other", new byte[0]));
            Check(names.FileCount == 2, "同名ディレクトリは配下のファイルが重複しなければ統合");
            Manifest fileFirst = new Manifest();
            fileFirst.AddFile("old", MakeSource("physical old", new byte[0]));
            ExpectFailure(delegate { fileFirst.AddDirectory(rootName, @"C:\Data\Old.ZIP", 0); },
                "通常ファイルが先に指定されても仮想ルートとの衝突を検出");

            // 入力 ZIP をメモリ上で復号・展開し、新ルート付きの暗号化 ZIP へ再圧縮する。
            using (MemoryStream input = new MemoryStream(Convert.FromBase64String(Fixtures[0]), false))
            using (MemoryStream output = new MemoryStream())
            {
                List<ZipEntry> entries = ZipSource.InspectMemory(input);
                ZipOutput writer = new ZipOutput(output);
                string relative = PathRules.ZipRootName(@"C:\Data\日本語.zip") + "/" + entries[0].Name;
                byte[] original;
                using (MemoryStream plain = new MemoryStream())
                {
                    ZipSource.DecodePayload(input, entries[0], plain);
                    original = plain.ToArray();
                }
                writer.Accept(writer.WriteCandidate(MakeSource(relative, original)));
                SourceItem empty = MakeSource(PathRules.ZipRootName(@"C:\Data\empty.ZIP"), new byte[0]);
                empty.IsDirectory = true;
                empty.ZipPath = PathRules.ArchivePath(empty.ZipPath, true);
                empty.NameBytes = Utf8.GetBytes(empty.ZipPath);
                writer.Accept(writer.WriteCandidate(empty));
                long size = writer.Finish();
                List<ZipEntry> rewritten = ZipSource.InspectMemory(output);
                Check(rewritten.Count == 2 && output.Length == size, "新ルート付き ZIP の実サイズとエントリ数");
                Check(rewritten[0].Name == "日本語/暗号/日本語.txt", "再圧縮後の名前から外側の .zip だけを除去");
                Check((rewritten[0].Flags & 0x0801) == 0x0801, "変更後も UTF-8 + 暗号化を維持");
                Check(rewritten[1].IsDirectory && rewritten[1].Name == "empty/", "空 ZIP 相当の仮想ルートも拡張子を除去");
                using (MemoryStream plain = new MemoryStream())
                {
                    ZipSource.DecodePayload(output, rewritten[0], plain);
                    Check(FixedEquals(plain.ToArray(), original), "仮想ルート変更後も復号内容が一致");
                }
            }
        }

        /// <summary>UTF-8/Unicode Path/CP932/CP437 の優先順と、異なる文字コードが混在した ZIP を検査。</summary>
        private static void TestLegacyNames()
        {
            Encoding japanese = Encoding.GetEncoding(932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            string name = "日本語/表ソ/①㈱髙﨑.txt";
            byte[] raw = japanese.GetBytes(name);
            Check(ZipSource.DecodeName(raw, null, false) == name, "無指定の旧日本語名を CP932 で読む");
            Check(ZipSource.DecodeName(Utf8.GetBytes(name), null, true) == name, "UTF-8 フラグを優先");
            string windowsPath = "資料\\ソ表.txt";
            Check(PathRules.ArchivePath(ZipSource.DecodeName(japanese.GetBytes(windowsPath), null, false), false) ==
                "資料/ソ表.txt", "CP932 の 2 byte 目 0x5C をパス区切りと混同しない");
            Check(ZipSource.DecodeName(Encoding.ASCII.GetBytes("ascii/name.txt"), null, false) == "ascii/name.txt", "ASCII 名");
            byte[] fallback = { 0x63, 0x61, 0x66, 0x82, 0x2E, 0x74, 0x78, 0x74 };
            Check(ZipSource.DecodeName(fallback, null, false) == "café.txt", "CP932 として不正な旧名を CP437 で読む");

            byte[] unicode;
            using (MemoryStream data = new MemoryStream())
            {
                data.WriteByte(1);
                Little.Write32(data, Crc32.Compute(raw));
                byte[] text = Utf8.GetBytes("補助/別名.txt");
                data.Write(text, 0, text.Length);
                unicode = data.ToArray();
            }
            Check(ZipSource.DecodeName(raw, unicode, false) == "補助/別名.txt", "正しい Unicode Path が CP932 より優先");
            byte[] damagedCrc = (byte[])unicode.Clone();
            damagedCrc[1] ^= 1;
            Check(ZipSource.DecodeName(raw, damagedCrc, false) == name, "CRC 不一致の Unicode Path は採用しない");
            byte[] unknownVersion = (byte[])unicode.Clone();
            unknownVersion[0] = 2;
            Check(ZipSource.DecodeName(raw, unknownVersion, false) == name, "未知バージョンの Unicode Path は採用しない");
            Check(ZipSource.DecodeName(Utf8.GetBytes(name), unicode, true) == name, "bit 11 は旧 extra より優先");
            ExpectFailure(delegate { ZipSource.DecodeName(raw, null, true); }, "不正な UTF-8 指定を CP932 で救済しない");
            Manifest duplicate = new Manifest();
            duplicate.AddFile(ZipSource.DecodeName(raw, null, false), MakeSource("cp932", new byte[0]));
            ExpectFailure(delegate { duplicate.AddFile(ZipSource.DecodeName(Utf8.GetBytes(name), null, true),
                MakeSource("utf8", new byte[0])); }, "文字コードが異なっても同一の出力名は重複");
            byte[] invalidUnicode = new byte[6];
            Array.Copy(unicode, invalidUnicode, 5);
            invalidUnicode[5] = 0xFF;
            ExpectFailure(delegate { ZipSource.DecodeName(raw, invalidUnicode, false); }, "明示 Unicode Path の不正 UTF-8 を拒否");

            // Python の struct/zlib で独立に組み立てた ZIP。異なる文字コードの 5 エントリを含む。
            byte[] fixture = Convert.FromBase64String("UEsDBBQAAAAIAFdkRF02P8OVDAAAAAoAAAAYAAAAk/qWe4zqL5Vcg1wvh0CHiu7g7ZUudHh0cw6wNDZSSMpPqQQAUEsDBBQAAAgIAFdkRF1msW4WEQAAAA8AAAAaAAAA5re35ZyoL1VURi04X+aXpeacrOiqni50eHQLDXHTtVB43LjuaVvr03U7AVBLAwQUAAAACABXZERdCeIj7RMAAAARAAAAFAAaAGZhbGxiYWNrL3VuaWNvZGUudHh0dXAWAAG/UnnB6KOc5YqpL+WIpeWQjS50eHQLzctMzk9JVShILMlQSMpPqQQAUEsDBBQAAAAIAFdkRF1UuwteDAAAAAoAAAAIAAAAY2Fmgi50eHRzDjAxNldIyk+pBABQSwMEFAAAAAgAV2REXejETC8WAAAAFAAAAA0AAACOkZe/XINclVwudHh0cw6wNDZSSEpMzi7OSSzOUChILMkAAFBLAQIUABQAAAAIAFdkRF02P8OVDAAAAAoAAAAYAAAAAAAAAAAAIAAAAAAAAACT+pZ7jOovlVyDXC+HQIeK7uDtlS50eHRQSwECFAAUAAAICABXZERdZrFuFhEAAAAPAAAAGgAAAAAAAAAAACAAAABCAAAA5re35ZyoL1VURi04X+aXpeacrOiqni50eHRQSwECFAAUAAAACABXZERdCeIj7RMAAAARAAAAFAAaAAAAAAAAACAAAACLAAAAZmFsbGJhY2svdW5pY29kZS50eHR1cBYAAb9SecHoo5zliqkv5Yil5ZCNLnR4dFBLAQIUABQAAAAIAFdkRF1UuwteDAAAAAoAAAAIAAAAAAAAAAAAIAAAAOoAAABjYWaCLnR4dFBLAQIUABQAAAAIAFdkRF3oxEwvFgAAABQAAAANAAAAAAAAAAAAIAAAABwBAACOkZe/XINclVwudHh0UEsFBgAAAAAFAAUAWwEAAF0BAAAAAA==");
            string[] names = { "日本語/表ソ/①㈱髙﨑.txt", "混在/UTF-8_日本語.txt", "補助/別名.txt", "café.txt", "資料\\ソ表.txt" };
            string[] bodies = { "CP932 body", "UTF-8 の内容", "Unicode path body", "CP437 body", "CP932 backslash path" };
            using (MemoryStream input = new MemoryStream(fixture, false))
            using (MemoryStream output = new MemoryStream())
            {
                List<ZipEntry> entries = ZipSource.InspectMemory(input);
                Check(entries.Count == names.Length, "文字コード混在 ZIP のエントリ数");
                ZipOutput writer = new ZipOutput(output);
                for (int i = 0; i < entries.Count; i++)
                {
                    Check(entries[i].Name == names[i], "文字コード混在 ZIP の名前 " + i.ToString(NumberCulture));
                    using (MemoryStream plain = new MemoryStream())
                    {
                        ZipSource.DecodePayload(input, entries[i], plain);
                        Check(FixedEquals(plain.ToArray(), Utf8.GetBytes(bodies[i])), "文字コード混在 ZIP の内容");
                        writer.Accept(writer.WriteCandidate(MakeSource(entries[i].Name, plain.ToArray())));
                    }
                }
                writer.Finish();
                List<ZipEntry> rewritten = ZipSource.InspectMemory(output);
                Check(rewritten.Count == names.Length, "再出力でエントリを欠落させない");
                for (int i = 0; i < rewritten.Count; i++)
                {
                    Check(rewritten[i].Name == PathRules.ArchivePath(names[i], false), "再出力名の UTF-8 正規化");
                    Check((rewritten[i].Flags & 0x0801) == 0x0801, "再出力は UTF-8 + ZipCrypto");
                }
            }
        }

        /// <summary>ディスクを開かずにテスト用の解決済みディレクトリ情報を作る。</summary>
        private static DirectorySnapshot MakeDirectorySnapshot(string identity, string path)
        {
            DirectorySnapshot snapshot = new DirectorySnapshot();
            snapshot.Identity = identity;
            snapshot.CanonicalPath = path;
            return snapshot;
        }

        /// <summary>祖先への循環だけを抑止し、別枝の別名は残す。リンク解決後の出力重なりも検査。</summary>
        private static void TestDirectoryTraversalRules()
        {
            DirectoryWork root = new DirectoryWork(@"C:\input", "input", null);
            root.Snapshot = MakeDirectorySnapshot("volume:1", @"C:\input");
            DirectoryWork first = new DirectoryWork(@"C:\input\alias1", "input/alias1", root);
            first.Snapshot = MakeDirectorySnapshot("volume:2", @"D:\target");
            DirectoryWork second = new DirectoryWork(@"C:\input\alias2", "input/alias2", root);
            second.Snapshot = first.Snapshot;
            Check(first.FindAncestor(first.Snapshot) == null, "外部へのディレクトリリンクは辿る");
            Check(second.FindAncestor(second.Snapshot) == null, "別枝の同じ実体を省略しない");
            DirectoryWork back = new DirectoryWork(@"D:\target\back", "input/alias1/back", first);
            Check(back.FindAncestor(root.Snapshot) == root, "祖先への循環を ID で検出");
            Check(back.FindAncestor(MakeDirectorySnapshot("volume:1", @"Z:\alternate")) == root,
                "実体 ID が同じなら別パス表記の循環も検出");
            Check(back.FindAncestor(MakeDirectorySnapshot(null, @"c:\INPUT")) == root,
                "ID が不明でも解決後パスで循環を検出");
            DirectoryWork noId = new DirectoryWork(@"C:\unknown", "unknown", null);
            noId.Snapshot = MakeDirectorySnapshot(null, @"C:\unknown");
            DirectoryWork noIdChild = new DirectoryWork(@"C:\unknown\child", "unknown/child", noId);
            Check(noIdChild.FindAncestor(MakeDirectorySnapshot(null, @"C:\unknown\child")) == null,
                "未提供の ID 同士を誤って同一実体にしない");
            DirectorySnapshot separate = MakeDirectorySnapshot("volume:3", @"C:\input2");
            Manifest.RequireSeparateOutput(root.Snapshot, separate, root.PhysicalPath);
            Check(true, "入力名の部分一致だけで出力を拒否しない");
            ExpectFailure(delegate { Manifest.RequireSeparateOutput(root.Snapshot,
                MakeDirectorySnapshot("volume:4", @"C:\input\out"), root.PhysicalPath); }, "出力が入力配下なら拒否");
            ExpectFailure(delegate { Manifest.RequireSeparateOutput(first.Snapshot,
                MakeDirectorySnapshot("volume:2", @"Z:\alias"), first.PhysicalPath); }, "別表記でも出力実体が同じなら拒否");
        }

        /// <summary>成功表示の空行・項目ごとの改行・最大 ZIP の句読点・ゼロ容量を検査。</summary>
        private static void TestResultLayout()
        {
            OutputNames names = new OutputNames(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "a.zip"));
            PackResult result = new PackResult();
            result.FileCount = 2;
            result.OriginalBytes = 1000;
            result.CompressedPayloadBytes = 600;
            result.AddPart("a.zip", 120);
            result.AddPart("a.02.zip", 560);
            string text = FormatResult(result, names);
            string newline = Environment.NewLine;
            Check(text.Contains("圧縮しました。" + newline + newline + "圧縮前ファイル容量: 1,000 bytes" + newline), "概要の後に空行");
            Check(text.Contains("圧縮後ファイル容量: 600 bytes (40.0 % 削減)" + newline), "圧縮後容量の改行");
            Check(text.Contains("合計容量: 680 bytes" + newline + "このうち最大の .zip ファイルは、a.02.zip (2 個目) であり 560 bytes です。"),
                "合計容量と最大 ZIP の書式");
            result.CycleCutCount = 1;
            Check(FormatResult(result, names).Contains("循環リンク 1 箇所"), "循環の打切りを成功表示でも報告");
            result.OriginalBytes = 0;
            result.CompressedPayloadBytes = 0;
            Check(FormatResult(result, names).Contains("(0.0 % 削減)"), "0 byte 入力の削減率");
        }

        /// <summary>自己テスト専用のメモリ入力を構成する。</summary>
        private static SourceItem MakeSource(string name, byte[] bytes)
        {
            SourceItem source = new SourceItem();
            source.ZipPath = PathRules.ArchivePath(name, false);
            source.NameBytes = Utf8.GetBytes(source.ZipPath);
            source.Origin = "self-test :: " + name;
            source.SelfTestBytes = bytes;
            source.Length = bytes.LongLength;
            source.DosTime = ZipTime.Encode(new DateTime(2026, 10, 4, 12, 34, 56));
            return source;
        }

        /// <summary>UTF-8・圧縮しにくいデータ・空ファイル・空フォルダを出力し、本番の読取経路で検査。</summary>
        private static void TestRoundTrip()
        {
            byte[] randomBytes = new byte[131073];
            new Random(1729).NextBytes(randomBytes);
            SourceItem[] sources = {
                MakeSource("日本語/長文.txt", Utf8.GetBytes(new string('文', 50000))),
                MakeSource("random.bin", randomBytes),
                MakeSource("empty.bin", new byte[0]),
                MakeSource("empty-folder", new byte[0])
            };
            sources[3].IsDirectory = true;
            sources[3].ZipPath += "/";
            sources[3].NameBytes = Utf8.GetBytes(sources[3].ZipPath);
            using (MemoryStream memory = new MemoryStream())
            {
                ZipOutput writer = new ZipOutput(memory);
                long predicted = 0;
                foreach (SourceItem source in sources)
                {
                    EntryRecord record = writer.WriteCandidate(source);
                    predicted = writer.PredictRecord(record);
                    Check(writer.Predict(source, source.Measurement) == predicted, "既知サイズの予測一致");
                    writer.Accept(record);
                }
                Check(writer.Finish() == predicted && memory.Length == predicted, "完成 ZIP の実サイズ一致");
                List<ZipEntry> entries = ZipSource.InspectMemory(memory);
                Check(entries.Count == sources.Length, "往復エントリ数");
                for (int i = 0; i < entries.Count; i++)
                {
                    using (MemoryStream plain = new MemoryStream())
                    {
                        ZipSource.DecodePayload(memory, entries[i], plain);
                        Check(FixedEquals(plain.ToArray(), sources[i].SelfTestBytes), "往復の平文一致: " + entries[i].Name);
                        Check(entries[i].Name == sources[i].ZipPath, "往復の UTF-8 名前一致");
                        Check(entries[i].IsDirectory || entries[i].Flags == 0x0809, "UTF-8/暗号化/data descriptor フラグ");
                    }
                }
            }
        }

        /// <summary>大きい候補の巻戻し後に小さい候補を採用しても、ZIP を壊さず詰められることを検査。</summary>
        private static void TestRollback()
        {
            byte[] noise = new byte[100000];
            new Random(196).NextBytes(noise);
            SourceItem first = MakeSource("first.txt", Utf8.GetBytes("first"));
            SourceItem rejected = MakeSource("rejected.bin", noise);
            SourceItem last = MakeSource("last.txt", Utf8.GetBytes("last"));
            using (MemoryStream memory = new MemoryStream())
            {
                ZipOutput writer = new ZipOutput(memory);
                writer.Accept(writer.WriteCandidate(first));
                long start = memory.Position;
                EntryRecord trial = writer.WriteCandidate(rejected);
                Check(writer.PredictRecord(trial) > 5000, "試行候補の容量超過");
                memory.Position = start;
                memory.SetLength(start);
                Check(writer.Predict(rejected, rejected.Measurement) > 5000, "保留候補の既知サイズ判定");
                EntryRecord small = writer.WriteCandidate(last);
                Check(writer.PredictRecord(small) <= 5000, "後続の小さいファイルが入る");
                writer.Accept(small);
                writer.Finish();
                List<ZipEntry> entries = ZipSource.InspectMemory(memory);
                Check(entries.Count == 2 && entries[0].Name == "first.txt" && entries[1].Name == "last.txt", "巻戻し後の ZIP 内容");
            }
        }

        /// <summary>Python の標準 ZIP/zlib と cryptography で作った独立フィクスチャを読み取る。</summary>
        private static void TestExternalFixtures()
        {
            byte[] expected = Convert.FromBase64String("V2luWmlwIEFFUyDjg4bjgrnjg4g6IOOBk+OCk+OBq+OBoeOBrw0KMTIzNDU2Nzg5");
            for (int i = 0; i < Fixtures.Length; i++)
            {
                byte[] data = Convert.FromBase64String(Fixtures[i]);
                using (MemoryStream memory = new MemoryStream(data, false))
                {
                    List<ZipEntry> entries = ZipSource.InspectMemory(memory);
                    Check(entries.Count == 1, "外部フィクスチャの索引 " + i.ToString(NumberCulture));
                    using (MemoryStream plain = new MemoryStream())
                    {
                        ZipSource.DecodePayload(memory, entries[0], plain);
                        Check(FixedEquals(plain.ToArray(), expected), "外部フィクスチャの復号 " + i.ToString(NumberCulture));
                    }
                    if (i < 6)
                    {
                        byte[] damaged = (byte[])data.Clone();
                        int tagOffset = checked((int)(entries[0].DataOffset + entries[0].CompressedSize - 1));
                        damaged[tagOffset] ^= 1;
                        ZipEntry target = entries[0];
                        ExpectFailure(delegate {
                            using (MemoryStream broken = new MemoryStream(damaged, false)) ZipSource.DecodePayload(broken, target, Stream.Null);
                        }, "AES 認証タグ改変の拒否 " + i.ToString(NumberCulture));
                        byte[] wrongPassword = (byte[])data.Clone();
                        int saltLength = (8 + 8 * entries[0].AesStrength) / 2;
                        wrongPassword[checked((int)entries[0].DataOffset + saltLength)] ^= 1;
                        ExpectFailure(delegate {
                            using (MemoryStream broken = new MemoryStream(wrongPassword, false)) ZipSource.DecodePayload(broken, target, Stream.Null);
                        }, "AES パスワード検証値の拒否");
                    }
                }
            }
        }

        /// <summary>サイズ計算を境界値で検査。4 GiB 以上の元ファイルでも int へ切り詰めない。</summary>
        private static void TestSizeFormula()
        {
            Check(ZipOutput.TotalSize(100, 200, 65534, false) == 322, "通常 EOCD の長さ");
            Check(ZipOutput.TotalSize(100, 200, 65535, false) == 398, "65535 件で ZIP64 EOCD");
            Check(ZipOutput.TotalSize(100, 200, 1, true) == 398, "ZIP64 エントリの末尾");
            using (MemoryStream memory = new MemoryStream())
            {
                SourceItem large = MakeSource("large", new byte[0]);
                large.Length = 5L * 1024L * 1024L * 1024L;
                Measurement measurement = new Measurement();
                measurement.UncompressedSize = large.Length;
                measurement.CompressedSize = 100;
                measurement.Method = 8;
                ZipOutput writer = new ZipOutput(memory);
                long expected = (30 + 5 + 20 + 100 + 24) + (46 + 5 + 12) + 98;
                Check(writer.Predict(large, measurement) == expected, "4 GiB 超の単一エントリ計算");
            }
        }

        /// <summary>任意の単独 ZIP サイズを持つ計画専用入力を作る。大容量データ自体は確保しない。</summary>
        private static SourceItem SyntheticItem(int number, long singleSize)
        {
            SourceItem item = MakeSource("f" + number.ToString("D5", NumberCulture) + ".bin", new byte[1]);
            item.Measurement = new Measurement();
            item.Measurement.Method = 8;
            item.Measurement.UncompressedSize = 1;
            item.Measurement.CompressedSize = 12;
            item.Measurement.Digest = new byte[0];
            long baseline = new ZipLayout().With(item).Size;
            item.Measurement.CompressedSize = checked(12L + singleSize - baseline);
            if (item.Measurement.CompressedSize < 12) throw new ArgumentOutOfRangeException("singleSize");
            return item;
        }

        /// <summary>19 個境界、均等化、単独超過、正常時の隙間詰めと各入力の一意配置を検査する。</summary>
        private static void TestPackingPlans()
        {
            List<SourceItem> empty = new List<SourceItem>();
            PackingPlan emptyPlan = PackingPlan.Build(empty);
            emptyPlan.Validate(empty);
            Check(emptyPlan.Parts.Count == 1 && emptyPlan.Maximum() == 22, "空 ZIP の 1 個計画");
            List<SourceItem> exact = new List<SourceItem>();
            for (int i = 0; i < 19; i++) exact.Add(SyntheticItem(i, MAX_ONE_ZIP_FILE_SIZE));
            PackingPlan nineteen = PackingPlan.Build(exact);
            Check(nineteen.Parts.Count == 19 && nineteen.Maximum() == MAX_ONE_ZIP_FILE_SIZE, "19 個ちょうど・容量ちょうど");
            exact.Add(SyntheticItem(19, MAX_ONE_ZIP_FILE_SIZE));
            PackingPlan twenty = PackingPlan.Build(exact);
            Check(twenty.Parts.Count == 19 && twenty.Relaxed, "20 個目を作らず再配分");
            Check(twenty.Maximum() == MAX_ONE_ZIP_FILE_SIZE * 2L - 22L, "不可分な 20 個を 19 個に置く最小の最大サイズ");

            List<SourceItem> uniform = new List<SourceItem>();
            for (int i = 0; i < 380; i++) uniform.Add(SyntheticItem(i, 2000000L));
            PackingPlan equal = PackingPlan.Build(uniform);
            Check(equal.Parts.Count == 19 && equal.Relaxed, "通常容量で 19 個に収まらない 380 エントリ");
            long equalSize = 20L * (2000000L - 22L) + 22L;
            foreach (PlannedPart part in equal.Parts)
                Check(part.Layout.Size == equalSize && part.Items.Count == 20, "超過時の 19 個均等配置");

            List<SourceItem> oversized = new List<SourceItem>();
            oversized.Add(SyntheticItem(0, 50000000L));
            oversized.Add(SyntheticItem(1, 1000000L));
            PackingPlan singles = PackingPlan.Build(oversized);
            Check(singles.Parts.Count == 2 && !singles.Relaxed, "個数に余裕がある単独容量超過は隔離");
            List<SourceItem> small = new List<SourceItem>();
            long[] sizes = { 21000000L, 22000000L, 10000000L, 9000000L, 3000000L, 2500000L, 500000L };
            for (int i = 0; i < sizes.Length; i++) small.Add(SyntheticItem(i, sizes[i]));
            PackingPlan filled = PackingPlan.Build(small);
            Check(!filled.Relaxed, "通常容量の範囲で小さい後続候補を利用");
            for (int i = 0; i < filled.Parts.Count; i++)
            for (int j = i + 1; j < filled.Parts.Count; j++)
            foreach (SourceItem item in filled.Parts[j].Items)
                Check(filled.Parts[i].Layout.With(item).Size > MAX_ONE_ZIP_FILE_SIZE, "先行 ZIP に入る後続候補を残さない");

            Random random = new Random(261004);
            for (int trial = 0; trial < 12; trial++)
            {
                List<SourceItem> items = new List<SourceItem>();
                for (int i = 0; i < 20 + trial * 3; i++) items.Add(SyntheticItem(i, 1000L + random.Next(40000000)));
                PackingPlan plan = PackingPlan.Build(items);
                plan.Validate(items);
                Check(plan.Parts.Count <= MAX_NUM_ZIP_FILES, "乱数入力の絶対個数上限");
                foreach (PlannedPart part in plan.Parts)
                    Check(part.Items.Count != 0, "空の余分な ZIP を作らない");
            }
            OutputNames names = new OutputNames(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "a.zip"));
            Check(System.IO.Path.GetFileName(names.PartPath(19)) == "a.19.zip", "最大番号は 2 桁");
            bool rejected = false;
            try { names.PartPath(20); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "20 個目の名前を生成前に拒否");
            for (int i = 0; i <= 99; i++)
                Check(OutputNames.MatchesOldName("a." + i.ToString("D2", NumberCulture) + ".zip", "a.zip", "a"), "2 桁だけ削除対象");
            for (int i = 100; i <= 9999; i++)
                Check(!OutputNames.MatchesOldName("a." + i.ToString(NumberCulture) + ".zip", "a.zip", "a"), "3～4 桁は削除禁止");
        }

        /// <summary>高速な差分評価を全件再計算と照合する。4 GiB と個数境界も再現する。</summary>
        private static void TestPlannedLayouts()
        {
            Random random = new Random(1303);
            for (int test = 0; test < 80; test++)
            {
                PlannedPart part = new PlannedPart();
                for (int i = 0; i < 7; i++)
                {
                    SourceItem item = SyntheticItem(i, 1000L + random.Next(100000));
                    if (test % 3 == 0 && i == 2)
                    {
                        item.Measurement.CompressedSize = Zip32Limit + test;
                        item.Measurement.UncompressedSize = Zip32Limit * 2L;
                    }
                    part.Add(item);
                }
                SourceItem incoming = SyntheticItem(99, 100000L);
                if (test % 3 == 1) incoming.Measurement.CompressedSize = Zip32Limit;
                int index = test % part.Items.Count;
                ZipLayout predicted = part.Changed(index, incoming);
                ZipLayout reference = new ZipLayout();
                for (int i = 0; i < part.Items.Count; i++) if (i != index) reference = reference.With(part.Items[i]);
                reference = reference.With(incoming);
                Check(predicted.Size == reference.Size && predicted.LocalBytes == reference.LocalBytes &&
                    predicted.CentralBytes == reference.CentralBytes && predicted.Zip64Entries == reference.Zip64Entries,
                    "ZIP64 オフセットを含む差分配置計算");
            }
            SourceItem directory = MakeSource("empty", new byte[0]);
            directory.IsDirectory = true;
            directory.ZipPath += "/";
            directory.NameBytes = Utf8.GetBytes(directory.ZipPath);
            directory.Measurement = new Measurement();
            ZipLayout countLayout = new ZipLayout();
            for (int i = 0; i < 65535; i++) countLayout = countLayout.With(directory);
            Check(countLayout.Size == (30L + 46L + 2L * directory.NameBytes.Length) * 65535L + 98L,
                "計画の 65535 エントリ ZIP64 末尾");
        }

        /// <summary>疑似入力一覧。count 個をメモリ上で作り、事前圧縮の並列処理を実際に通す。</summary>
        private static Manifest MemoryManifest(int count, int bytes)
        {
            Manifest manifest = new Manifest();
            Random random = new Random(261003);
            for (int i = 0; i < count; i++)
            {
                byte[] content = new byte[bytes + i];
                random.NextBytes(content);
                SourceItem item = MakeSource("cache/" + i.ToString("D3", NumberCulture) + ".bin", content);
                item.Ordinal = i + 1;
                manifest.Items.Add(item);
                manifest.FileCount++;
                manifest.TotalBytes += content.Length;
            }
            return manifest;
        }

        /// <summary>計画どおりメモリ ZIP を書き、通常の読取コードで復号・展開して元データと照合する。</summary>
        private static void VerifyMemoryPlan(Manifest manifest, PackingPlan plan, CompressedCache cache)
        {
            foreach (PlannedPart part in plan.Parts)
            using (MemoryStream output = new MemoryStream())
            {
                ZipOutput writer = new ZipOutput(output);
                foreach (SourceItem item in part.Items) writer.Accept(writer.WriteCandidate(item, cache));
                long actual = writer.Finish();
                Check(actual == part.Layout.Size, "計画サイズと本番 ZIP 書込の一致");
                List<ZipEntry> entries = ZipSource.InspectMemory(output);
                Check(entries.Count == part.Items.Count, "キャッシュ経由 ZIP のエントリ数");
                for (int i = 0; i < entries.Count; i++)
                using (MemoryStream plain = new MemoryStream())
                {
                    ZipSource.DecodePayload(output, entries[i], plain);
                    Check(FixedEquals(plain.ToArray(), part.Items[i].SelfTestBytes), "キャッシュ/再圧縮後の復号内容一致");
                }
            }
        }

        /// <summary>全件キャッシュ、FIFO 追出し、キャッシュなし、出力途中の上限縮小と内容変更を検査。</summary>
        private static void TestPrecompressionCache()
        {
            Check(CompressedCache.EightyPercent(101) == 80, "80% の整数丸め");
            Check(CompressedCache.EightyPercent(Int64.MaxValue) > 0, "80% の計算で桁あふれしない");
            Manifest manifest = MemoryManifest(16, 32768);
            using (CompressedCache cache = new CompressedCache(delegate { return 200000000L; }))
            {
                Precompress(manifest, cache, 4);
                Check(cache.UsedBytes > 0 && cache.UsedBytes <= cache.LimitBytes, "作成途中を含む共有キャッシュ予算");
                PackingPlan plan = PackingPlan.Build(manifest.Items);
                VerifyMemoryPlan(manifest, plan, cache);
                Check(cache.UsedBytes == 0, "使い終えたキャッシュの解放");
            }
            using (CompressedCache cache = new CompressedCache(delegate { return 250000L; }))
            {
                Precompress(manifest, cache, 4);
                Check(cache.UsedBytes <= cache.LimitBytes, "並列追出し後も予算内");
                VerifyMemoryPlan(manifest, PackingPlan.Build(manifest.Items), cache);
            }
            using (CompressedCache cache = new CompressedCache(delegate { return 0L; }))
            {
                Precompress(manifest, cache, 4);
                Check(cache.UsedBytes == 0, "使用可能メモリ 0 でも計測を継続");
                VerifyMemoryPlan(manifest, PackingPlan.Build(manifest.Items), cache);
                SourceItem changed = manifest.Items[0];
                changed.SelfTestBytes[0] ^= 1;
                ExpectFailure(delegate {
                    using (MemoryStream memory = new MemoryStream()) new ZipOutput(memory).WriteCandidate(changed, cache);
                }, "追出し後の再圧縮で内容変更を拒否");
                changed.SelfTestBytes[0] ^= 1;
            }

            long available = 200000L;
            using (CompressedCache cache = new CompressedCache(delegate { return available; }))
            {
                SourceItem a = MakeSource("a", new byte[1]), b = MakeSource("b", new byte[1]), c = MakeSource("c", new byte[1]);
                CacheEntry first = cache.Begin(a);
                cache.Append(first, new byte[1], 0, 1); cache.Complete(first);
                CacheEntry second = cache.Begin(b);
                cache.Append(second, new byte[1], 0, 1); cache.Complete(second);
                CacheEntry third = cache.Begin(c);
                cache.Append(third, new byte[1], 0, 1); cache.Complete(third);
                Check(!first.Live && second.Live && third.Live, "最古のキャッシュを先に削除");
                available = 100L;
                cache.Trim();
                Check(cache.UsedBytes == 0, "空きメモリ低下時に既存キャッシュを削除");
            }

            Manifest large = MemoryManifest(1, BufferSize * 4);
            bool writing = false;
            int calls = 0;
            using (CompressedCache cache = new CompressedCache(delegate {
                return writing && ++calls >= 3 ? 0L : 200000000L;
            }))
            {
                Precompress(large, cache, 1);
                PackingPlan plan = PackingPlan.Build(large.Items);
                writing = true;
                VerifyMemoryPlan(large, plan, cache);
                Check(cache.UsedBytes == 0 && calls >= 3, "キャッシュ転送途中の追出しで暗号ヘッダから再実行");
            }
        }

        /// <summary>65535 エントリを本当にメモリ上に書き、ZIP64 の個数 sentinel 境界を往復検査。</summary>
        private static void TestCountBoundary()
        {
            using (MemoryStream memory = new MemoryStream())
            {
                ZipOutput writer = new ZipOutput(memory);
                for (int i = 0; i < 65535; i++)
                {
                    SourceItem directory = MakeSource("d" + i.ToString("D5", NumberCulture), new byte[0]);
                    directory.IsDirectory = true;
                    directory.ZipPath += "/";
                    directory.NameBytes = Utf8.GetBytes(directory.ZipPath);
                    writer.Accept(writer.WriteCandidate(directory));
                }
                long size = writer.Finish();
                Check(memory.Length == size, "65535 件 ZIP の実サイズ");
                List<ZipEntry> entries = ZipSource.InspectMemory(memory);
                Check(entries.Count == 65535, "65535 件 ZIP64 の読取個数");
            }
        }
    }
#endif
}
