/*
DNNT 261007_VGD8KZ 分割 ZIP 結合展開ユーティリティ R03

ソースコードファイル名: dnnt_261004_qct5nr_split_zip_extract_r03_261007_vgd8kz.cs
今回バージョン: R03 (入力ソースの指定バージョン: R02)
内部識別名・名前空間: dnnt_261004_qct5nr_split_zip_extract (継続)
前回標題: DNNT 261007_TFDXE5 分割 ZIP 結合展開ユーティリティ 2
初版標題: DNNT 261004_QCT5NR 分割 ZIP 結合展開ユーティリティ

目的: 同一ディレクトリの ZIP / UNIX split 相当の ZIP 断片 / 両者のハイブリッドを認識し、
      通常展開または /d・-d 指定によるベースライン＋差分の統合展開を行う。
対象: Windows Vista 以降 / .NET Framework 4.0 API / C# 4 / AnyCPU / WinExe。
原理: シーク可能な仮想連結ストリーム上で EOCD、ZIP64、中央・ローカルヘッダ、
      data descriptor の範囲と一致を検証する。連結中間ファイルは作らない。
      ZIP 解釈、CRC、ZipCrypto、厳密な Deflate 解釈は本ファイルで実装する。
      AES/PBKDF2/HMAC は .NET 標準の暗号プリミティブのみを使用する。
      保存は同じディレクトリの一時ファイルを検証後に閉じ、MoveFileExW で確定する。
      危険な ZIP パス・リンクは全体中断、個別の破損・保存失敗は警告して継続する。
注意: SFX/先頭・末尾ごみ、ZIP 自身のマルチディスク、Store/Deflate 以外は対象外。
      パス上限は通常の MAX_PATH。8.3 短縮名と紛らわしい名前は安全側に拒否する。
      ZIP の符号化情報がない名前は既定 CP932、復号不能時 CP437。
      環境変数 DNNT_ZIP_CODEPAGE により未指定名のコードページを変更できる。
      新規ディレクトリだけに日時を設定する。既存ディレクトリは設定しない。
      自作コードであること自体は無脆弱性を保証しない。README の制約・検証範囲参照。

入力 R01 に含まれる既存改修 [T261005_EL_SKCJM_04]: 展開先選択のダミー名は、入力物理ファイル名を
      大文字小文字を無視して比較した先頭名に、先頭 _ と末尾 .txt を付ける。
      比較のために入力リストを並べ替えず、ZIP の認識・結合・展開順は維持する。

改修 R02 / DNNT 261007_TFDXE5 / 2026/10/07 18:42:17 (JST):
      bit 3 (data descriptor 使用) 時、ローカルの暫定 CRC・両サイズと
      中央情報の一致を要求していた比較だけを除去する。Info-ZIP の非シーク
      暗号化出力で残る DOS 時刻由来の暫定 CRC に対応する互換性修正。
      中央サイズによるデータ境界、descriptor の長さ・署名・CRC・両サイズ、
      実データの復号・展開・CRC / AES 認証は従来どおり検証する。
      ローカル ZIP64 extra の構造・数値範囲検査も維持する。
      現行の保存処理と古い説明の相違、既存の競合制約は readme の R02 追記参照。

改修 R03 / DNNT 261007_VGD8KZ / 2026/10/07 21:28:46 (JST):
      個別ZIP・全連結ZIP・Q.zip.Pによる群別仮想連結の三候補を構造検証する。
      差分/ベースラインを独立して検査し、明示・暗黙ディレクトリを含む仮想木の
      同一相対パス・同一種別の一致数を最大化する。完全同値の部分木を圧縮し、
      相対パスの整数索引とprefix filterで、最良値に届き得る候補だけを厳密に照合する。
      同じ一致数では深さ和の最小を選び、なお複数解なら詳細例外で中断する。
      ベースライン基準配下を差分基準へ写像し、外側を _base_misc_files/yyMMdd_HHmmss へ保管。
      上書き対象のベースラインは遅延し、差分成功時は一切展開せず、個別失敗時だけ救済する。
      二群の同名ZIPでも警告を識別する。終了コード0のときe/E/o/Oで出力先をExplorerで開く。
      一時保存先の後片付けI/O例外が元の例外を隠さないよう、Disposeを局所補修した。
      最大cは近似しない。入力形状によって相対パス総量や候補数が二乗に増える最悪例はある。
      C#の実コンパイル/Windows実行は未検証。独立モデル照合と静的レビューの範囲はREADME参照。

今回の生成情報 (R03):
      本プログラムは生成 AI により生成され、今回の改修も生成 AI が実施。
      AI バージョン・モデル: GPT-6 Astra Pro (内部ビルド識別子は取得不可)。
      思考レベル: 公開された設定値を取得できないため不明。
      セッション開始日時(JST): 正確な値は取得不可。
      今回依頼の受信日時(JST): 2026/10/07 21:05:43 (会話で提供された時刻情報)。
      今回の最初の明示的な環境時計記録(JST): 2026/10/07 21:11:00。
      応答生成用成果物の確定日時(JST): 2026/10/07 21:28:46 (JST)。

前版の生成情報 (R02 の入力記録を保持):
      本プログラムは生成 AI により生成され、今回の修正も生成 AI が実施。
      AI バージョン・モデル: GPT-6 Astra Pro (内部ビルド識別子は取得不可)。
      思考レベル: 公開された設定値は取得不可のため記載しない。
      セッション開始日時(JST): 正確な値は取得不可。
      今回作業の最初の環境時計記録(JST): 2026/10/07 18:26:18。
      応答生成日時(JST): 2026/10/07 18:42:17 (JST)
      この環境に C# コンパイラ / Windows はなく、実 C# ビルド・Windows 動作は未検証。
      実サンプルの独立解凍器による全件検証と、対応ロジックの回帰検証を実施。

前回の生成情報 (入力ソースの記録を保持):
本プログラムは生成 AI により生成され、本改修も生成 AI により実施されました。
AI バージョン・モデル: GPT-6 Astra Pro（内部ビルド識別子は取得不可）
思考レベル: このセッションの公開設定値は取得不可のため不明。
セッション開始日時(JST): 正確な値は取得不可。
本改修作業の最初の時計記録(JST): 2026-10-05 17:17:40 +09:00。
応答生成日時(JST): 2026-10-05 17:23:31 +0900 (JST)

主な一次資料（仕様の転載ではなく独立実装）:
https://pkware.cachefly.net/webdocs/casestudies/APPNOTE.TXT
https://www.winzip.com/en/support/aes-encryption/
https://www.rfc-editor.org/rfc/rfc1951
https://learn.microsoft.com/en-us/windows/win32/fileio/naming-a-file
https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_rename_info
*/
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace dnnt_261004_qct5nr_split_zip_extract
{
    /// <summary>起動、対話、終了コードを管理する。通常の ZIP 入力はすべてフルパス。</summary>
    internal static class Program
    {
        internal static volatile bool CancelRequested;
        internal static readonly CultureInfo NumberCulture = CultureInfo.InvariantCulture;
        internal static Encoding LegacyEncoding;

        // ダイアログ履歴はユーザーごとに保持する。管理者権限を必要とする HKLM は使用しない。
        private const string DialogRegistrySubKey = @"Software\DNNT\dnnt_261004_qct5nr_split_zip_extract";
        private const string SourceDialogDirectoryValue = "SourceDialogDirectory";
        private const string DestinationDialogParentValue = "DestinationDialogParentDirectory";

        /// <summary>引数は入力ファイル群。戻り値は Win32 に準じた終了コード。</summary>
        [STAThread]
        private static int Main(string[] args)
        {
            int result = 31;
            bool consoleReady = false;
            string completedRoot = null;
            try
            {
                if (Environment.OSVersion.Platform != PlatformID.Win32NT || Environment.OSVersion.Version.Major < 6)
                    throw new PlatformNotSupportedException("Windows Vista 以降が必要です。");
                Native.EnsureConsole();
                consoleReady = true;
                bool differential;
                string[] inputArguments = ParseArguments(args, out differential);
                Console.WriteLine(differential ? "【差分 ZIP モード】" : "【通常 ZIP モード】");
                Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
                {
                    e.Cancel = true;
                    CancelRequested = true;
                };
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                LegacyEncoding = LoadEncoding();
                PasswordManager passwords = new PasswordManager();
                passwords.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "password_list.txt"));
                using (ArchiveInput primary = ReadInput(inputArguments, differential
                    ? "(1) まず、差分 zip 群を指定してください" : "入力ファイル群を選択",
                    differential ? "差分 ZIP 群" : null))
                {
                    if (differential)
                    {
                        // 二群を独立して検査する。群内の先勝ち重複と群間の差分上書きを混同しない。
                        ExtractionPlan delta = ExtractionPlan.BuildVirtual(primary.Detected.Archives);
                        delta.ConfirmDuplicates();
                        using (ArchiveInput baseline = ReadInput(new string[0],
                            "(2) 次に、ベースライン zip 群を指定してください", "ベースライン ZIP 群"))
                        {
                            ExtractionPlan original = ExtractionPlan.BuildVirtual(baseline.Detected.Archives);
                            original.ConfirmDuplicates();
                            Console.WriteLine("基準ディレクトリを計算中...");
                            DiffMatch match = DiffMatcher.Find(DiffTree.Build(original.Entries), DiffTree.Build(delta.Entries));
                            DifferentialPlan merged = DifferentialPlan.Build(original, delta, match,
                                DifferentialPlan.GetBaselineTimestamp(baseline.Sources[0]));
                            merged.PrintSummary();
                            string root = SelectDestination(Path.GetDirectoryName(primary.Sources[0]), primary.Sources, true);
                            result = RunExtraction(merged.Output, root,
                                new SourceSet[] { primary.SourceSet, baseline.SourceSet }, passwords,
                                checked(primary.Sources.Count + baseline.Sources.Count),
                                "差分 ZIP モード / ベースライン: " + baseline.Detected.Mode + " / 差分: " + primary.Detected.Mode, merged);
                            completedRoot = root;
                        }
                    }
                    else
                    {
                        string root = SelectDestination(Path.GetDirectoryName(primary.Sources[0]), primary.Sources, false);
                        ExtractionPlan plan = ExtractionPlan.BuildVirtual(primary.Detected.Archives);
                        plan.ConfirmDuplicates();
                        result = RunExtraction(plan, root, new SourceSet[] { primary.SourceSet }, passwords,
                            primary.Sources.Count, primary.Detected.Mode, null);
                        completedRoot = root;
                    }
                }
            }
            catch (Exception ex)
            {
                result = ExitCode(ex);
                if (consoleReady) Console.Error.WriteLine("\nエラー (終了コード {0}):\n{1}", result, Text.SafeMultiline(ex.ToString()));
                else MessageBox.Show(ex.Message, "DNNT ZIP 展開エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (consoleReady)
                {
                    bool openAvailable = result == 0 && completedRoot != null;
                    if (openAvailable) Console.WriteLine("e または o キーを押すとこのフォルダを開きます。");
                    Console.WriteLine("何かキーを押すと終了します...");
                    char choice = ReadExitKey();
                    if (openAvailable && (choice == 'e' || choice == 'E' || choice == 'o' || choice == 'O'))
                    {
                        try { OpenDestination(completedRoot); }
                        catch (Exception ex)
                        {
                            result = ExitCode(ex);
                            Console.Error.WriteLine("エクスプローラで展開先を開けません: " + Text.Safe(ex.Message));
                            Console.WriteLine("何かキーを押すと終了します...");
                            ReadExitKey();
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>終了待ちのキーを返す。入力リダイレクト時は一行、EOF/読取不可時は空文字相当。</summary>
        private static char ReadExitKey()
        {
            try
            {
                try { return Console.ReadKey(true).KeyChar; }
                catch (InvalidOperationException)
                {
                    string line = Console.ReadLine();
                    return String.IsNullOrEmpty(line) ? '\0' : line[0];
                }
            }
            catch (IOException) { return '\0'; }
        }

        /// <summary>/d または -d を取り除く。残りは従来どおり入力ファイルの絶対パス。</summary>
        internal static string[] ParseArguments(string[] args, out bool differential)
        {
            differential = false;
            List<string> paths = new List<string>();
            foreach (string arg in args)
            {
                if (String.Equals(arg, "/d", StringComparison.OrdinalIgnoreCase)
                    || String.Equals(arg, "-d", StringComparison.OrdinalIgnoreCase)) differential = true;
                else paths.Add(arg);
            }
            return paths.ToArray();
        }

        /// <summary>一群だけを選択・除外・整列・構造検査する。返値が入力ハンドルを所有する。</summary>
        private static ArchiveInput ReadInput(string[] args, string title, string label)
        {
            if (label != null) Console.WriteLine("【" + label + "】");
            string[] paths = GetInputs(args, title);
            Console.WriteLine("入力ファイル群 ({0:N0} 個のファイル) の認識中...", paths.Length);
            List<string> sources = new List<string>();
            List<string> excluded = new List<string>();
            foreach (string path in paths)
            {
                if (Path.GetFileName(path).IndexOf("zip", StringComparison.OrdinalIgnoreCase) >= 0) sources.Add(path);
                else excluded.Add(path);
            }
            if (excluded.Count != 0)
            {
                Console.WriteLine("無関係な除外ファイルリスト:");
                foreach (string path in excluded) Console.WriteLine("  " + Text.Safe(path));
            }
            if (sources.Count == 0) throw new InvalidDataException("ファイル名に zip を含む対象ファイルがありません。");
            sources.Sort(delegate (string a, string b) { return StringComparer.Ordinal.Compare(Path.GetFileName(a), Path.GetFileName(b)); });
            ArchiveInput input = new ArchiveInput(sources);
            try
            {
                foreach (ZipArchiveData archive in input.Detected.Archives)
                    foreach (ZipEntry entry in archive.Entries)
                    {
                        Program.CheckCancel();
                        entry.InputGroupLabel = label;
                    }
                Console.WriteLine("認識結果: {0} / 物理ファイル {1:N0} 個 / 論理 ZIP {2:N0} 個",
                    input.Detected.Mode, sources.Count, input.Detected.Archives.Count);
                return input;
            }
            catch { input.Dispose(); throw; }
        }

        /// <summary>出力計画を実在先と照合し、共通の上書き・復号・保存・集計処理を行う。</summary>
        private static int RunExtraction(ExtractionPlan plan, string root, IList<SourceSet> inputs,
            PasswordManager passwords, int physicalCount, string mode, DifferentialPlan differential)
        {
            WarningBook warnings = new WarningBook();
            using (SafeRoot safeRoot = new SafeRoot(root, warnings))
            {
                plan.CheckDestination(safeRoot, inputs);
                plan.BuildDirectoryTimes();
                OverwritePolicy policy = new OverwritePolicy(plan, safeRoot, warnings);
                policy.Preflight();
                Extractor extractor = new Extractor(plan, safeRoot, passwords, policy, warnings);
                try { extractor.Run(); }
                finally { safeRoot.RestoreCreatedDirectoryTimes(plan.Directories); }
                if (differential != null)
                {
                    Console.WriteLine("【差分 ZIP モードでの展開終了】");
                    differential.PrintSummary();
                    Console.WriteLine("差分成功によるベースライン読取・書込省略: {0:N0} 個 / 差分失敗時のベースライン救済保存: {1:N0} 個",
                        extractor.SupersededCount, extractor.FallbackSuccessCount);
                }
                string summary = String.Format(NumberCulture,
                    "【{0:N0} 個の zip ファイル群 (モード: {1}) から、{2:N0} 個のファイル (合計 {3:N0} bytes) を展開完了】",
                    physicalCount, mode, extractor.SuccessCount, extractor.SuccessBytes);
                Console.WriteLine(summary);
                warnings.Print();
                Console.WriteLine("意図的な省略: 重複 {0:N0} 個 / 上書きしない指定 {1:N0} 個", plan.DuplicateCount, extractor.SkippedCount);
                Console.WriteLine(summary);
                Console.WriteLine("展開先ディレクトリフルパス:\n" + Text.Safe(WindowsPaths.WithSlash(root)));
                return warnings.HasWarnings ? 299 : 0; // ERROR_PARTIAL_COPY
            }
        }

        /// <summary>検証済みの出力先を Windows の explorer.exe で開く。シェルコマンドは組み立てない。</summary>
        private static void OpenDestination(string root)
        {
            string executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            System.Diagnostics.ProcessStartInfo start = new System.Diagnostics.ProcessStartInfo();
            start.FileName = executable;
            start.Arguments = QuoteArgument(root);
            start.UseShellExecute = false;
            using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(start)) { }
        }

        /// <summary>Windows の引数引用規則で一つの引数を囲む。ドライブルート末尾の逆斜線も保持する。</summary>
        internal static string QuoteArgument(string value)
        {
            StringBuilder result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                result.Append('\\', c == '"' ? checked(slashes * 2 + 1) : slashes);
                result.Append(c);
                slashes = 0;
            }
            result.Append('\\', checked(slashes * 2));
            result.Append('"');
            return result.ToString();
        }

        /// <summary>既定の旧式ファイル名符号化を取得する。不正な環境設定は明示的に失敗させる。</summary>
        private static Encoding LoadEncoding()
        {
            int codePage = 932;
            string value = Environment.GetEnvironmentVariable("DNNT_ZIP_CODEPAGE");
            if (!String.IsNullOrEmpty(value) && !Int32.TryParse(value, NumberStyles.None, NumberCulture, out codePage))
                throw new ArgumentException("DNNT_ZIP_CODEPAGE は 932、437、65001 等の数値を指定してください。");
            return Encoding.GetEncoding(codePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        }

        /// <summary>
        /// ユーザーレジストリからダイアログの初期ディレクトリを取得する。
        /// 登録値がない、絶対パスとして不正、または現在存在しない場合は null を返す。
        /// </summary>
        /// <param name="valueName">本プログラム専用レジストリキー内の文字列値名。</param>
        /// <returns>利用可能な絶対ディレクトリ。利用不能なら null。</returns>
        private static string ReadDialogDirectory(string valueName)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(DialogRegistrySubKey, false))
                {
                    if (key == null) return null;
                    string value = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                    if (String.IsNullOrEmpty(value)) return null;
                    try
                    {
                        string full = WindowsPaths.TrimSlash(WindowsPaths.Full(value));
                        return Directory.Exists(full) ? full : null;
                    }
                    catch (ArgumentException) { return null; }
                    catch (NotSupportedException) { return null; }
                    catch (PathTooLongException) { return null; }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.Error.WriteLine("警告: ダイアログ履歴をレジストリから読み取れません: " + Text.Safe(ex.Message));
                return null;
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine("警告: ダイアログ履歴をレジストリから読み取れません: " + Text.Safe(ex.Message));
                return null;
            }
            catch (System.Security.SecurityException ex)
            {
                Console.Error.WriteLine("警告: ダイアログ履歴をレジストリから読み取れません: " + Text.Safe(ex.Message));
                return null;
            }
        }

        /// <summary>
        /// ダイアログで確定した絶対ディレクトリを HKEY_CURRENT_USER に REG_SZ として保存する。
        /// </summary>
        /// <param name="valueName">本プログラム専用レジストリキー内の文字列値名。</param>
        /// <param name="directory">保存する絶対ディレクトリ。</param>
        private static void WriteDialogDirectory(string valueName, string directory)
        {
            string full = WindowsPaths.TrimSlash(WindowsPaths.Full(directory));
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(DialogRegistrySubKey))
                {
                    if (key == null) throw new IOException("ダイアログ履歴用のユーザーレジストリキーを作成できません。");
                    key.SetValue(valueName, full, RegistryValueKind.String);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.Error.WriteLine("警告: ダイアログ履歴をレジストリへ保存できません: " + Text.Safe(ex.Message));
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine("警告: ダイアログ履歴をレジストリへ保存できません: " + Text.Safe(ex.Message));
            }
            catch (System.Security.SecurityException ex)
            {
                Console.Error.WriteLine("警告: ダイアログ履歴をレジストリへ保存できません: " + Text.Safe(ex.Message));
            }
        }

        /// <summary>入力ダイアログまたは引数を検証し、同一フォルダ内の重複除去済みパスを返す。</summary>
        private static string[] GetInputs(string[] args, string title)
        {
            string[] selected = args;
            if (args.Length == 0)
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Title = title;
                    dialog.Filter = "すべてのファイル (*.*)|*.*";
                    dialog.Multiselect = true;
                    dialog.CheckFileExists = true;
                    dialog.RestoreDirectory = true;

                    // 前回ダイアログで選択した 1 個目の入力ファイルを含むディレクトリ b を復元する。
                    string rememberedSourceDirectory = ReadDialogDirectory(SourceDialogDirectoryValue);
                    if (rememberedSourceDirectory != null) dialog.InitialDirectory = rememberedSourceDirectory;

                    if (dialog.ShowDialog() != DialogResult.OK) throw new OperationCanceledException("ファイル選択が取り消されました。");
                    selected = dialog.FileNames;

                }
            }
            List<string> answer = new List<string>();
            HashSet<string> unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> identities = new HashSet<string>(StringComparer.Ordinal);
            string directory = null;
            foreach (string path in selected)
            {
                CheckCancel();
                string full = WindowsPaths.Full(path);
                if (!File.Exists(full)) throw new FileNotFoundException("入力ファイルが実在しないか、読み取れません: " + full, full);
                full = Native.LongName(full);
                string parent = Path.GetDirectoryName(full);
                if (directory == null) directory = parent;
                else if (!String.Equals(directory, parent, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("入力ファイルはすべて同一ディレクトリ上に必要です: " + full + " / 基準: " + directory);
                if (!unique.Add(full)) continue;
                using (FileStream file = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    FileStamp stamp = Native.Stamp(file.SafeFileHandle);
                    if (stamp.Identity != null && !identities.Add(stamp.Identity)) continue;
                }
                answer.Add(full);
            }
            if (answer.Count == 0) throw new ArgumentException("入力ファイルがありません。");

            // 入力元ディレクトリ b は、ダイアログ選択・コマンドライン指定の別を問わず保存する。
            // ここまでで全入力の実在・絶対パス・同一ディレクトリ性を検証済みなので、
            // directory はユーザーが指定した 1 個目の有効入力ファイルを含む絶対ディレクトリでもある。
            if (!String.IsNullOrEmpty(directory))
                WriteDialogDirectory(SourceDialogDirectoryValue, directory);

            return answer.ToArray();
        }

        /// <summary>
        /// 保存ダイアログの架空ファイル名の親ディレクトリを返す。ファイルは作成しない。
        /// 前回選択先の 1 つ上のディレクトリ a がレジストリにあれば、それを初期位置として使用する。
        /// </summary>
        /// <param name="initial">履歴がない場合に使用する初期ディレクトリ。</param>
        /// <param name="sources">展開対象の検証済み物理ファイルのフルパス一覧。1 件以上必要。順序は変更しない。</param>
        /// <param name="differential">差分モードのとき true。保存先ダイアログのタイトルへ反映する。</param>
        /// <returns>ユーザーが指定した架空ファイルを含む、実際の展開先絶対ディレクトリ。</returns>
        private static string SelectDestination(string initial, IList<string> sources, bool differential)
        {
            if (sources == null) throw new ArgumentNullException("sources");
            if (sources.Count == 0) throw new ArgumentException("展開対象の物理ファイルがありません。", "sources");

            // ダミー名だけを大文字小文字無視のファイル名順で決める。
            // sources 自体をソートすると ZIP 断片の結合順や重複時の優先順が変わるため、
            // 先頭に相当する名前だけを走査で求める。同順位なら元の一覧で先のものを維持する。
            string firstFileName = Path.GetFileName(sources[0]);
            for (int index = 1; index < sources.Count; index++)
            {
                string fileName = Path.GetFileName(sources[index]);
                if (StringComparer.OrdinalIgnoreCase.Compare(fileName, firstFileName) < 0)
                    firstFileName = fileName;
            }

            // 元の拡張子と大文字小文字をそのまま残す。架空ファイル自体は作成しない。
            string dummyFileName = "_" + firstFileName + ".txt";

            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = differential ? "差分 ZIP モードで展開する先を指定してください" : "通常 ZIP モードで展開する先を指定してください";
                dialog.Filter = "すべてのファイル (*.*)|*.*";
                dialog.FileName = dummyFileName;

                string rememberedParent = ReadDialogDirectory(DestinationDialogParentValue);
                dialog.InitialDirectory = rememberedParent ?? initial;

                dialog.AddExtension = false;
                dialog.CheckFileExists = false;
                dialog.CheckPathExists = true;
                dialog.OverwritePrompt = false;
                dialog.CreatePrompt = false;
                dialog.RestoreDirectory = true;
                if (dialog.ShowDialog() != DialogResult.OK) throw new OperationCanceledException("展開先の指定が取り消されました。");

                string selectedFile = WindowsPaths.Full(dialog.FileName);
                string destination = WindowsPaths.TrimSlash(Path.GetDirectoryName(selectedFile));

                // ユーザー指定ファイルを含むディレクトリの 1 つ上を a として保存する。
                // ドライブ/UNC 共有のルートを直接選んだ場合は親が存在しないため、履歴値は更新しない。
                DirectoryInfo parent = Directory.GetParent(destination);
                if (parent != null) WriteDialogDirectory(DestinationDialogParentValue, parent.FullName);

                return destination;
            }
        }

        /// <summary>ユーザー中断を、個別ファイルの警告で握り潰さない例外として通知する。</summary>
        internal static void CheckCancel()
        {
            if (CancelRequested) throw new OperationCanceledException("ユーザーにより中断されました。");
        }

        /// <summary>想定する回復可能な個別処理エラーかどうかを判定する。</summary>
        internal static bool Recoverable(Exception ex)
        {
            return !(ex is SafetyException) && !(ex is OperationCanceledException) && !(ex is OutOfMemoryException)
                && (ex is IOException || ex is UnauthorizedAccessException || ex is Win32Exception || ex is ArgumentException
                    || ex is NotSupportedException || ex is CryptographicException || ex is FormatException);
        }

        /// <summary>例外を Windows の代表的な終了コードへ変換する。</summary>
        private static int ExitCode(Exception ex)
        {
            if (ex is OperationCanceledException) return 1223;
            if (ex is SafetyException || ex is UnauthorizedAccessException) return 5;
            if (ex is FileNotFoundException) return 2;
            if (ex is DirectoryNotFoundException) return 3;
            if (ex is OutOfMemoryException) return 8;
            if (ex is InvalidDataException || ex is FormatException) return 13;
            if (ex is ArgumentException) return 87;
            if (ex is PlatformNotSupportedException || ex is NotSupportedException) return 50;
            Win32Exception native = ex as Win32Exception;
            if (native != null && native.NativeErrorCode != 0) return native.NativeErrorCode;
            int hr = Marshal.GetHRForException(ex);
            if ((hr & unchecked((int)0xffff0000)) == unchecked((int)0x80070000)) return hr & 0xffff;
            return 31;
        }
    }

    /// <summary>安全性違反は全体中断する。IOException と区別し、個別の警告にしない。</summary>
    internal sealed class SafetyException : Exception
    {
        internal SafetyException(string message) : base(message) { }
    }

    /// <summary>パス表示・選択入力を扱う。ZIP 名の制御文字をコンソールへ直接出さない。</summary>
    internal static class Text
    {
        internal static string Safe(string value)
        {
            if (value == null) return "";
            StringBuilder result = new StringBuilder();
            foreach (char c in value)
            {
                if (c < 32 || c == 127 || (c >= '\u202a' && c <= '\u202e') || (c >= '\u2066' && c <= '\u2069'))
                    result.Append("\\u" + ((int)c).ToString("X4", CultureInfo.InvariantCulture));
                else result.Append(c);
            }
            return result.ToString();
        }
        internal static string SafeMultiline(string value)
        {
            return Safe(value).Replace("\\u000D", "\r").Replace("\\u000A", "\n");
        }
        /// <summary>allowed 中の 1 文字だけを受理する。EOF はキャンセル。</summary>
        internal static char Choice(string prompt, string allowed)
        {
            while (true)
            {
                Program.CheckCancel();
                Console.Write(prompt);
                string input = Console.ReadLine();
                if (input == null) throw new OperationCanceledException("標準入力が終了しました。");
                input = input.Trim();
                if (input.Length == 1 && allowed.IndexOf(input[0]) >= 0) return input[0];
                Console.WriteLine("指定された文字を 1 文字入力してください。");
            }
        }
        /// <summary>入力したパスワードを表示しない。空文字を有効な入力として保持する。</summary>
        internal static string Password()
        {
            uint mode;
            if (!Native.GetConsoleMode(Native.GetStdHandle(-10), out mode))
            {
                string line = Console.ReadLine();
                if (line == null) throw new OperationCanceledException("パスワード入力が終了しました。");
                return line;
            }
            StringBuilder text = new StringBuilder();
            while (true)
            {
                Program.CheckCancel();
                ConsoleKeyInfo key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return text.ToString(); }
                if (key.Key == ConsoleKey.Backspace) { if (text.Length != 0) text.Length--; }
                else if (key.KeyChar >= ' ') text.Append(key.KeyChar);
            }
        }
    }

    /// <summary>警告を元エントリ単位でまとめ、ファイル失敗数とディレクトリ警告数を分離する。</summary>
    internal sealed class WarningBook
    {
        private readonly Dictionary<string, List<string>> messages = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly List<string> order = new List<string>();
        private readonly HashSet<string> files = new HashSet<string>(StringComparer.Ordinal);
        internal bool HasWarnings { get { return order.Count != 0; } }
        internal void Add(ZipEntry entry, string message)
        {
            string key = entry.Label;
            if (!entry.IsDirectory) files.Add(key);
            Add(key, message);
        }
        internal void Add(string key, string message)
        {
            List<string> list;
            if (!messages.TryGetValue(key, out list)) { list = new List<string>(); messages.Add(key, list); order.Add(key); }
            if (!list.Contains(message)) list.Add(message);
        }
        internal void Print()
        {
            Console.WriteLine("警告リスト:");
            if (order.Count == 0)
            {
                Console.WriteLine("  なし");
                return;
            }
            foreach (string key in order)
            {
                Console.WriteLine("  " + Text.Safe(key));
                foreach (string message in messages[key]) Console.WriteLine("    " + Text.Safe(message));
            }
            Console.WriteLine("上記のとおり合計 {0:N0} 個の内容ファイルの展開または保存（日時設定を含む）に失敗しました。ご確認ください。", files.Count);
            if (order.Count > files.Count) Console.WriteLine("別途、ディレクトリ等の補助警告: {0:N0} 件", order.Count - files.Count);
        }
    }

    /// <summary>共有書込・削除を禁止して入力ファイルを保持する。リンク内の入力も許容する。</summary>
    internal sealed class SourcePart : IDisposable
    {
        internal readonly string PathName;
        internal readonly FileStream File;
        internal readonly long Length;
        internal readonly string Identity;
        internal SourcePart(string path)
        {
            PathName = path;
            File = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.RandomAccess);
            try { Length = File.Length; Identity = Native.Stamp(File.SafeFileHandle).Identity; }
            catch { File.Dispose(); throw; }
        }
        public void Dispose() { File.Dispose(); }
    }

    /// <summary>入力ハンドルの所有者。途中の生成失敗でも開いたハンドルを解放する。</summary>
    internal sealed class SourceSet : IDisposable
    {
        internal readonly List<SourcePart> Parts = new List<SourcePart>();
        internal SourceSet(List<string> paths)
        {
            try { foreach (string path in paths) Parts.Add(new SourcePart(path)); }
            catch { Dispose(); throw; }
        }
        internal bool ContainsIdentity(string identity)
        {
            if (identity == null) return false;
            foreach (SourcePart part in Parts) if (part.Identity == identity) return true;
            return false;
        }
        public void Dispose() { foreach (SourcePart part in Parts) part.Dispose(); }
    }

    /// <summary>一群の物理入力と検出結果をまとめて所有する。差分群とベースライン群は別インスタンス。</summary>
    internal sealed class ArchiveInput : IDisposable
    {
        internal readonly List<string> Sources;
        internal readonly SourceSet SourceSet;
        internal readonly DetectedArchives Detected;

        /// <param name="sources">検証・重複除去・Ordinal 整列済みの物理パス。</param>
        internal ArchiveInput(List<string> sources)
        {
            Sources = sources;
            SourceSet = new SourceSet(sources);
            try { Detected = Detector.Detect(SourceSet.Parts); }
            catch { SourceSet.Dispose(); throw; }
        }
        public void Dispose() { SourceSet.Dispose(); }
    }

    /// <summary>複数ファイルを 64 bit オフセットの単一読取ストリームとして見せる。元ハンドルは所有しない。</summary>
    internal sealed class JoinedStream : Stream
    {
        private readonly IList<SourcePart> parts;
        private readonly long[] starts;
        private readonly long length;
        private long position;
        internal JoinedStream(IList<SourcePart> value)
        {
            parts = value;
            starts = new long[value.Count + 1];
            for (int i = 0; i < value.Count; i++) starts[i + 1] = checked(starts[i] + value[i].Length);
            length = starts[value.Count];
        }
        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return true; } }
        public override bool CanWrite { get { return false; } }
        public override long Length { get { return length; } }
        public override long Position { get { return position; } set { Seek(value, SeekOrigin.Begin); } }
        /// <summary>境界をまたいで読み込む。返値は実際に読み込んだバイト数。</summary>
        public override int Read(byte[] buffer, int offset, int count)
        {
            Bytes.CheckBuffer(buffer, offset, count);
            int total = 0;
            while (count > 0 && position < length)
            {
                Program.CheckCancel();
                int low = 0, high = parts.Count;
                while (low + 1 < high)
                {
                    int middle = low + (high - low) / 2;
                    if (starts[middle] <= position) low = middle; else high = middle;
                }
                while (low < parts.Count && starts[low + 1] == position) low++;
                if (low >= parts.Count) break;
                long local = position - starts[low];
                int amount = (int)Math.Min((long)count, parts[low].Length - local);
                FileStream source = parts[low].File;
                if (source.Position != local) source.Position = local;
                int got = source.Read(buffer, offset, amount);
                if (got == 0) throw new EndOfStreamException("入力断片の途中で EOF: " + parts[low].PathName + " / offset=" + local);
                position += got; offset += got; count -= got; total += got;
            }
            return total;
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            long value;
            checked
            {
                if (origin == SeekOrigin.Begin) value = offset;
                else if (origin == SeekOrigin.Current) value = position + offset;
                else if (origin == SeekOrigin.End) value = length + offset;
                else throw new ArgumentException("SeekOrigin が不正です。");
            }
            if (value < 0 || value > length) throw new IOException("ストリーム範囲外へのシークです。");
            position = value;
            return value;
        }
        public override void Flush() { }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
    }

    /// <summary>基底ストリームの指定区間だけを読み出す。暗号・展開処理による範囲外読み出しを防ぐ。</summary>
    internal sealed class SliceStream : Stream
    {
        private readonly Stream source;
        private readonly long start;
        private readonly long length;
        private long position;
        internal SliceStream(Stream value, long first, long size)
        {
            if (first < 0 || size < 0 || first > value.Length || size > value.Length - first) throw new InvalidDataException("ZIP データ範囲が不正です。");
            source = value; start = first; length = size;
        }
        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return false; } }
        public override long Length { get { return length; } }
        public override long Position { get { return position; } set { throw new NotSupportedException(); } }
        public override int Read(byte[] buffer, int offset, int count)
        {
            Bytes.CheckBuffer(buffer, offset, count);
            count = (int)Math.Min((long)count, length - position);
            if (count == 0) return 0;
            source.Position = start + position;
            int got = source.Read(buffer, offset, count);
            if (got == 0) throw new EndOfStreamException("ZIP データ途中で EOF。");
            position += got;
            return got;
        }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
    }

    /// <summary>Little-endian 数値、完全読み出し、および CRC の共通関数。</summary>
    internal static class Bytes
    {
        internal static readonly byte[] Empty = new byte[0];
        internal static ushort U16(byte[] b, int p) { Need(b, p, 2); return (ushort)(b[p] | (b[p + 1] << 8)); }
        internal static uint U32(byte[] b, int p) { Need(b, p, 4); return (uint)(b[p] | (b[p + 1] << 8) | (b[p + 2] << 16) | (b[p + 3] << 24)); }
        internal static ulong U64(byte[] b, int p) { return (ulong)U32(b, p) | ((ulong)U32(b, p + 4) << 32); }
        internal static long I64(byte[] b, int p)
        {
            ulong value = U64(b, p);
            if (value > Int64.MaxValue) throw new InvalidDataException("64 bit 値が .NET のストリーム範囲を超えています。");
            return (long)value;
        }
        internal static void Need(byte[] b, int p, int count)
        {
            if (p < 0 || count < 0 || p > b.Length - count) throw new InvalidDataException("ZIP フィールドが途中で切れています。");
        }
        internal static void CheckBuffer(byte[] b, int p, int count)
        {
            if (b == null) throw new ArgumentNullException("buffer");
            if (p < 0 || count < 0 || p > b.Length - count) throw new ArgumentOutOfRangeException("count");
        }
        internal static byte[] Read(Stream source, int count)
        {
            byte[] result = new byte[count];
            ReadFully(source, result, 0, count);
            return result;
        }
        internal static void ReadFully(Stream source, byte[] b, int offset, int count)
        {
            while (count != 0)
            {
                int n = source.Read(b, offset, count);
                if (n == 0) throw new EndOfStreamException("ZIP ヘッダまたはデータが途中で切れています。");
                offset += n; count -= n;
            }
        }
        internal static byte[] At(Stream source, long offset, int count)
        {
            if (offset < 0 || count < 0 || offset > source.Length - count) throw new InvalidDataException("ZIP の参照範囲がファイル外です: offset=" + offset);
            source.Position = offset;
            return Read(source, count);
        }
        internal static bool Equal(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int difference = 0;
            for (int i = 0; i < a.Length; i++) difference |= a[i] ^ b[i];
            return difference == 0;
        }
        internal static byte[] Sub(byte[] source, int offset, int count)
        {
            Need(source, offset, count);
            byte[] result = new byte[count];
            Buffer.BlockCopy(source, offset, result, 0, count);
            return result;
        }
        internal static long Add(long a, long b)
        {
            if (a < 0 || b < 0 || b > Int64.MaxValue - a) throw new InvalidDataException("ZIP サイズ / オフセットのオーバーフローです。");
            return a + b;
        }
    }

    /// <summary>ZIP/ZipCrypto で使用する IEEE CRC-32。state は反転前の中間状態。</summary>
    internal static class Crc32
    {
        private static readonly uint[] Table = MakeTable();
        private static uint[] MakeTable()
        {
            uint[] table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xedb88320U ^ (c >> 1) : c >> 1;
                table[i] = c;
            }
            return table;
        }
        internal static uint Step(uint state, byte value) { return Table[(state ^ value) & 255] ^ (state >> 8); }
        internal static uint Update(uint state, byte[] buffer, int offset, int count)
        {
            int end = offset + count;
            while (offset < end) state = Step(state, buffer[offset++]);
            return state;
        }
        internal static uint Compute(byte[] value) { return Update(0xffffffffU, value, 0, value.Length) ^ 0xffffffffU; }
    }

    /// <summary>ZIP 内の三種の UTC 日時。rank により NTFS > Unix 拡張 > DOS の順に採用する。</summary>
    internal sealed class EntryTimes
    {
        internal DateTime? ModifiedUtc;
        internal DateTime? CreatedUtc;
        internal DateTime? AccessedUtc;
        private int modifiedRank = -1, createdRank = -1, accessedRank = -1;
        internal void SetModified(DateTime value, int rank) { if (rank >= modifiedRank) { ModifiedUtc = value; modifiedRank = rank; } }
        internal void SetCreated(DateTime value, int rank) { if (rank >= createdRank) { CreatedUtc = value; createdRank = rank; } }
        internal void SetAccessed(DateTime value, int rank) { if (rank >= accessedRank) { AccessedUtc = value; accessedRank = rank; } }
        /// <summary>個別日時がない場合だけ更新日時を補う。元のメタデータは変更しない。</summary>
        internal EntryTimes Resolved()
        {
            EntryTimes result = new EntryTimes();
            result.ModifiedUtc = ModifiedUtc;
            result.CreatedUtc = CreatedUtc ?? ModifiedUtc;
            result.AccessedUtc = AccessedUtc ?? ModifiedUtc;
            return result;
        }
        internal static EntryTimes All(DateTime value)
        {
            EntryTimes result = new EntryTimes();
            result.ModifiedUtc = value; result.CreatedUtc = value; result.AccessedUtc = value;
            return result;
        }
    }

    /// <summary>WinZip AES extra field の解釈結果。圧縮方式と暗号方式を混同しない。</summary>
    internal sealed class AesInfo
    {
        internal int Version, Strength, Method;
        internal int KeyBytes { get { return Strength == 1 ? 16 : Strength == 2 ? 24 : 32; } }
        internal int SaltBytes { get { return KeyBytes / 2; } }
        internal bool Same(AesInfo other)
        {
            return other != null && Version == other.Version && Strength == other.Strength && Method == other.Method;
        }
    }

    /// <summary>展開に必要な ZIP メタデータ。Keep=false は先勝ち重複による除外。</summary>
    internal sealed class ZipEntry
    {
        internal ZipArchiveData Archive;
        internal string Name, RawDecodedName, LocalDecodedName;
        internal byte[] RawName;
        internal ushort Flags, Method, DosTime, DosDate;
        internal uint Crc, Attributes;
        internal long CompressedSize, Size, LocalOffset, DataOffset;
        internal AesInfo Aes;
        internal bool IsDirectory, Keep = true;
        internal string SpecialObject, TimestampWarning, PlanError, Relative;
        // 異なる入力ディレクトリの同名ZIPでも、差分とベースラインの警告キーを区別する。
        internal string InputGroupLabel;
        internal readonly EntryTimes Times = new EntryTimes();
        internal bool Encrypted { get { return (Flags & 1) != 0; } }
        internal int Compression { get { return Aes == null ? Method : Aes.Method; } }
        internal string Label
        {
            get { return (InputGroupLabel == null ? "" : "【" + InputGroupLabel + "】 ")
                + "'" + Name + "' (" + Archive.Label + " 内, local=0x" + LocalOffset.ToString("X", CultureInfo.InvariantCulture) + ")"; }
        }

        /// <summary>読取元の名称・位置・日時を保ち、出力相対パスだけを独立したコピーへ割り当てる。</summary>
        /// <param name="relative">正規化済みの出力相対パス。メタデータは変更しない。</param>
        internal ZipEntry AtOutput(string relative)
        {
            ZipEntry copy = (ZipEntry)MemberwiseClone();
            copy.Relative = WindowsPaths.Relative(relative.Length == 0 ? "." : relative, IsDirectory);
            copy.PlanError = null;
            return copy;
        }
    }

    /// <summary>論理 ZIP の入力断片と検証済みエントリ。Entries はローカルヘッダ出現順。</summary>
    internal sealed class ZipArchiveData
    {
        internal readonly List<SourcePart> Parts;
        internal readonly List<ZipEntry> Entries = new List<ZipEntry>();
        internal readonly string Label;
        internal ZipArchiveData(IList<SourcePart> parts)
        {
            Parts = new List<SourcePart>(parts);
            Label = Path.GetFileName(parts[0].PathName);
            if (parts.Count > 1) Label += " ～ " + Path.GetFileName(parts[parts.Count - 1].PathName) + " [仮想連結]";
        }
    }

    /// <summary>個別・全連結・群別仮想連結の検証から確定した論理 ZIP 群。</summary>
    internal sealed class DetectedArchives
    {
        internal string Mode;
        internal List<ZipArchiveData> Archives;
    }

    /// <summary>三候補を構造検証し、名前だけによる誤認を避けて論理 ZIP 群を確定する。</summary>
    internal static class Detector
    {
        /// <summary>元の入力位置と、Q.zip.P の末尾 P。入力ハンドルは所有しない。</summary>
        private sealed class NamedPart
        {
            internal SourcePart Source;
            internal int SourceIndex;
            internal string Suffix;
        }

        /// <summary>Prefix が null なら単独候補、それ以外は完全一致する Q の候補群。</summary>
        private sealed class NameGroup
        {
            internal string Prefix;
            internal readonly List<NamedPart> Members = new List<NamedPart>();
        }

        /// <summary>入力は従来どおりファイル名の Ordinal 昇順。元の配列やハンドルは変更しない。</summary>
        internal static DetectedArchives Detect(IList<SourcePart> parts)
        {
            if (parts == null || parts.Count == 0) throw new InvalidDataException("認識する ZIP 入力がありません。");
            List<ZipArchiveData> separate = new List<ZipArchiveData>();
            ZipArchiveData[] individual = new ZipArchiveData[parts.Count];
            string[] individualProblems = new string[parts.Count];
            List<string> problems = new List<string>();
            bool allSeparate = true;
            for (int i = 0; i < parts.Count; i++)
            {
                Program.CheckCancel();
                ZipArchiveData archive;
                string problem;
                if (TryRead(new SourcePart[] { parts[i] }, out archive, out problem))
                {
                    individual[i] = archive;
                    separate.Add(archive);
                    problems.Add(Path.GetFileName(parts[i].PathName) + ": 単独 ZIP として整合");
                }
                else
                {
                    allSeparate = false;
                    individualProblems[i] = problem;
                    problems.Add(Path.GetFileName(parts[i].PathName) + ": " + problem);
                }
            }
            if (parts.Count == 1)
            {
                if (!allSeparate) throw new InvalidDataException("入力の一貫性欠如:\n" + String.Join("\n", problems.ToArray()));
                return new DetectedArchives { Mode = "個別複数 ZIP モード（単一 ZIP）", Archives = separate };
            }

            ZipArchiveData joined;
            string joinedProblem;
            bool joinedValid = TryRead(parts, out joined, out joinedProblem);
            List<ZipArchiveData> joinedList = new List<ZipArchiveData>();
            if (joinedValid) joinedList.Add(joined);
            List<ZipArchiveData> hybrid;
            string hybridProblem;
            bool hybridValid = TryReadHybrid(parts, individual, individualProblems, joined, joinedProblem,
                out hybrid, out hybridProblem);

            // 同じ断片・同じ順序の全連結と群別連結は一候補である。異なる構成だけを曖昧とする。
            List<string> candidates = new List<string>();
            if (allSeparate) candidates.Add("個別複数 ZIP モード");
            if (joinedValid) candidates.Add("巨大 ZIP 分割モード");
            if (hybridValid && !(allSeparate && SameLayout(separate, hybrid))
                && !(joinedValid && SameLayout(joinedList, hybrid))) candidates.Add("ハイブリッド ZIP モード");
            if (candidates.Count > 1)
                throw new InvalidDataException("複数の異なる ZIP 構成が構造検証に成功する曖昧な入力です。自動選択しません。\n"
                    + "成立候補: " + String.Join(" / ", candidates.ToArray())
                    + "\n" + String.Join("\n", problems.ToArray())
                    + "\n全断片の仮想連結: " + (joinedValid ? "整合" : joinedProblem)
                    + "\n群別仮想連結の検査:\n" + hybridProblem);

            // ファイル名は推定材料に限る。既存モードが実際に整合する場合、命名条件だけで拒否しない。
            if (allSeparate) return new DetectedArchives { Mode = "個別複数 ZIP モード", Archives = separate };
            if (joinedValid) return new DetectedArchives { Mode = "巨大 ZIP 分割モード", Archives = joinedList };
            if (hybridValid) return new DetectedArchives { Mode = "ハイブリッド ZIP モード", Archives = hybrid };
            throw new InvalidDataException("入力の一貫性欠如。欠落断片、順序違い、命名規則の不一致、別 ZIP との混在、破損または対象外形式が考えられます。\n"
                + String.Join("\n", problems.ToArray()) + "\n全断片の仮想連結: " + joinedProblem
                + "\n群別仮想連結の検査:\n" + hybridProblem);
        }

        /// <summary>
        /// Q を Ordinal 完全一致で分類し、同じ Q の複数断片だけを P の Ordinal 昇順で仮想連結する。
        /// .zip. の表記は大小文字を許すが、Q と P の大小文字は同一視しない。失敗理由は全候補群で集約する。
        /// </summary>
        private static bool TryReadHybrid(IList<SourcePart> parts, ZipArchiveData[] individual,
            string[] individualProblems, ZipArchiveData joined, string joinedProblem,
            out List<ZipArchiveData> archives, out string problem)
        {
            List<NameGroup> groups = new List<NameGroup>();
            Dictionary<string, NameGroup> namedGroups = new Dictionary<string, NameGroup>(StringComparer.Ordinal);
            for (int i = 0; i < parts.Count; i++)
            {
                Program.CheckCancel();
                string name = Path.GetFileName(parts[i].PathName);
                int marker = name.LastIndexOf(".zip.", StringComparison.OrdinalIgnoreCase);
                NameGroup group;
                if (marker < 0)
                {
                    group = new NameGroup();
                    groups.Add(group);
                }
                else
                {
                    string prefix = name.Substring(0, marker);
                    if (!namedGroups.TryGetValue(prefix, out group))
                    {
                        group = new NameGroup { Prefix = prefix };
                        namedGroups.Add(prefix, group);
                        groups.Add(group);
                    }
                }
                group.Members.Add(new NamedPart { Source = parts[i], SourceIndex = i,
                    Suffix = marker < 0 ? null : name.Substring(marker + 5) });
            }

            archives = new List<ZipArchiveData>();
            List<string> details = new List<string>();
            bool valid = true, hasSplitGroup = false;
            // 論理 ZIP 同士は入力順の最初の構成断片を基準に処理する。重複時の先勝ち規則を保つ。
            foreach (NameGroup group in groups)
            {
                Program.CheckCancel();
                if (group.Members.Count == 1)
                {
                    NamedPart member = group.Members[0];
                    ZipArchiveData archive = individual[member.SourceIndex];
                    string label = Path.GetFileName(member.Source.PathName);
                    if (group.Prefix != null) label += " [Q='" + group.Prefix + "' に対する断片候補は 1 個]";
                    if (archive != null)
                    {
                        archives.Add(archive);
                        details.Add(label + ": 単独の論理 ZIP として整合");
                    }
                    else
                    {
                        valid = false;
                        details.Add(label + ": 単独の論理 ZIP として不整合: " + individualProblems[member.SourceIndex]);
                    }
                    continue;
                }

                hasSplitGroup = true;
                group.Members.Sort(delegate (NamedPart a, NamedPart b) { return StringComparer.Ordinal.Compare(a.Suffix, b.Suffix); });
                List<SourcePart> groupParts = new List<SourcePart>();
                StringBuilder description = new StringBuilder();
                description.Append("Q='").Append(group.Prefix).Append("' の ").Append(group.Members.Count)
                    .Append(" 断片 (P の Ordinal 昇順):");
                foreach (NamedPart member in group.Members)
                {
                    groupParts.Add(member.Source);
                    description.Append("\n  P='").Append(member.Suffix).Append("' / ").Append(member.Source.PathName);
                }
                string namingProblem = CheckPartNames(group);
                if (namingProblem != null)
                {
                    valid = false;
                    details.Add(description.ToString() + "\n  命名条件が不整合のため、この群の順序を採用しません:\n" + namingProblem);
                    continue;
                }

                ZipArchiveData groupArchive;
                string groupProblem;
                bool groupValid;
                if (SameParts(parts, groupParts))
                {
                    // 全連結と同じ物理順序の一群なら、既に済ませた構造検査の結果を再利用する。
                    groupArchive = joined;
                    groupProblem = joinedProblem;
                    groupValid = joined != null;
                }
                else groupValid = TryRead(groupParts, out groupArchive, out groupProblem);
                if (groupValid)
                {
                    archives.Add(groupArchive);
                    details.Add(description.ToString() + "\n  1 個の論理 ZIP として整合");
                }
                else
                {
                    valid = false;
                    details.Add(description.ToString() + "\n  仮想連結 ZIP が不整合: " + groupProblem);
                }
            }
            if (!hasSplitGroup) details.Add("同一 Q の複数断片群がないため、ハイブリッド候補はありません。");
            problem = String.Join("\n", details.ToArray());
            return valid && hasSplitGroup;
        }

        /// <summary>群内の P が非空の ASCII 英数字、同幅、一意であることを調べる。連番の開始値・間隔は仮定しない。</summary>
        private static string CheckPartNames(NameGroup group)
        {
            List<string> errors = new List<string>();
            int width = group.Members[0].Suffix.Length;
            for (int i = 0; i < group.Members.Count; i++)
            {
                Program.CheckCancel();
                NamedPart member = group.Members[i];
                string suffix = member.Suffix;
                string label = "  " + Path.GetFileName(member.Source.PathName) + ": P='" + suffix + "'";
                if (suffix.Length == 0) errors.Add(label + " は空です。");
                if (suffix.Length != width) errors.Add(label + " の桁数 " + suffix.Length
                    + " は、先頭断片の桁数 " + width + " と異なります。");
                for (int j = 0; j < suffix.Length; j++)
                {
                    char c = suffix[j];
                    if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) continue;
                    errors.Add(label + " は ASCII 英数字以外を含みます (位置 " + (j + 1)
                        + ", U+" + ((int)c).ToString("X4", CultureInfo.InvariantCulture) + ")。");
                    break;
                }
                if (i != 0 && String.Equals(group.Members[i - 1].Suffix, suffix, StringComparison.Ordinal))
                    errors.Add(label + " は直前断片と同じ P であり、連結順序が一意に定まりません。");
            }
            return errors.Count == 0 ? null : String.Join("\n", errors.ToArray());
        }

        /// <summary>論理 ZIP の順序と、その中の物理断片の順序が同じ候補を重複判定する。</summary>
        private static bool SameLayout(IList<ZipArchiveData> a, IList<ZipArchiveData> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (!SameParts(a[i].Parts, b[i].Parts)) return false;
            return true;
        }

        /// <summary>仮想連結を構成するハンドルの同一性と順序を比較する。</summary>
        private static bool SameParts(IList<SourcePart> a, IList<SourcePart> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (!Object.ReferenceEquals(a[i], b[i])) return false;
            return true;
        }

        /// <summary>ZIP 構造に起因する失敗だけを候補の不成立へ変換し、I/O 障害・取消し等は伝播する。</summary>
        private static bool TryRead(IList<SourcePart> parts, out ZipArchiveData archive, out string problem)
        {
            archive = null;
            problem = null;
            try { archive = ZipReader.Read(parts); return true; }
            catch (Exception ex)
            {
                if (!(ex is InvalidDataException) && !(ex is EndOfStreamException) && !(ex is OverflowException)
                    && !(ex is DecoderFallbackException) && !(ex is FormatException)) throw;
                problem = ex.Message;
                return false;
            }
        }
    }

    /// <summary>中央ディレクトリとローカルヘッダの全照合を行う、外部 ZIP ライブラリ不要の読取器。</summary>
    internal static class ZipReader
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly Encoding Cp437 = Encoding.GetEncoding(437, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);

        /// <summary>候補 EOCD を後方探索し、全構造が一致した最初の候補を返す。</summary>
        internal static ZipArchiveData Read(IList<SourcePart> parts)
        {
            using (JoinedStream source = new JoinedStream(parts))
            {
                if (source.Length < 22) throw new InvalidDataException("EOCD を収容できない長さです。");
                int size = (int)Math.Min(source.Length, 65557L);
                long start = source.Length - size;
                byte[] tail = Bytes.At(source, start, size);
                string reason = "末尾に正しい EOCD がありません（末尾ごみも許可しません）。";
                for (int i = tail.Length - 22; i >= 0; i--)
                {
                    if (Bytes.U32(tail, i) != 0x06054b50U || i + 22 + Bytes.U16(tail, i + 20) != tail.Length) continue;
                    Program.CheckCancel();
                    try { return ReadAtEnd(source, parts, start + i); }
                    catch (Exception ex)
                    {
                        if (!(ex is InvalidDataException) && !(ex is EndOfStreamException) && !(ex is OverflowException)
                            && !(ex is DecoderFallbackException)) throw;
                        reason = "EOCD offset=0x" + (start + i).ToString("X", CultureInfo.InvariantCulture) + ": " + ex.Message;
                    }
                }
                throw new InvalidDataException(reason);
            }
        }

        /// <summary>指定 EOCD から ZIP64 を解決し、アーカイブの始端から終端までの整合性を確認する。</summary>
        private static ZipArchiveData ReadAtEnd(Stream source, IList<SourcePart> parts, long endOffset)
        {
            byte[] end = Bytes.At(source, endOffset, 22);
            if (Bytes.U16(end, 4) != 0 || Bytes.U16(end, 6) != 0) throw new InvalidDataException("ZIP 自身のマルチディスク形式は対象外です。");
            long count = Bytes.U16(end, 10), localCount = Bytes.U16(end, 8);
            long centralSize = Bytes.U32(end, 12), centralOffset = Bytes.U32(end, 16);
            long metadataEnd = endOffset;
            bool needs64 = centralSize == UInt32.MaxValue || centralOffset == UInt32.MaxValue;
            // 古い ZIP のちょうど 65535 件は、locator がなければ通常形式として全件照合する。
            byte[] locator = endOffset >= 20 ? Bytes.At(source, endOffset - 20, 20) : null;
            bool has64 = locator != null && Bytes.U32(locator, 0) == 0x07064b50U;
            if (has64)
            {
                if (Bytes.U32(locator, 4) != 0 || Bytes.U32(locator, 16) != 1) throw new InvalidDataException("ZIP64 が複数ディスクを参照しています。");
                long offset64 = Bytes.I64(locator, 8);
                byte[] fixed64 = Bytes.At(source, offset64, 56);
                if (Bytes.U32(fixed64, 0) != 0x06064b50U) throw new InvalidDataException("ZIP64 EOCD シグネチャが不正です。");
                long remainder = Bytes.I64(fixed64, 4);
                if (remainder < 44 || Bytes.Add(offset64, Bytes.Add(12, remainder)) != endOffset - 20)
                    throw new InvalidDataException("ZIP64 EOCD の長さまたは位置が不正です。");
                if (Bytes.U32(fixed64, 16) != 0 || Bytes.U32(fixed64, 20) != 0)
                    throw new InvalidDataException("ZIP64 のディスク番号が不正です。");
                long count64 = Bytes.I64(fixed64, 32), local64 = Bytes.I64(fixed64, 24);
                long size64 = Bytes.I64(fixed64, 40), central64 = Bytes.I64(fixed64, 48);
                if (count64 != local64) throw new InvalidDataException("ZIP64 エントリ数が一致しません。");
                if ((count != 65535 && count != count64) || (localCount != 65535 && localCount != local64)
                    || (centralSize != UInt32.MaxValue && centralSize != size64)
                    || (centralOffset != UInt32.MaxValue && centralOffset != central64))
                    throw new InvalidDataException("通常 EOCD と ZIP64 EOCD の値が一致しません。");
                count = count64; localCount = local64; centralSize = size64; centralOffset = central64;
                metadataEnd = offset64;
            }
            else if (needs64) throw new InvalidDataException("ZIP64 が必要ですが locator がありません。");
            if (count != localCount) throw new InvalidDataException("EOCD のエントリ数が一致しません。");
            if (Bytes.Add(centralOffset, centralSize) != metadataEnd) throw new InvalidDataException("中央ディレクトリの開始・長さ・終端が一致しません。");
            if (count > Int32.MaxValue || count > centralSize / 46) throw new InvalidDataException("エントリ数が格納可能なヘッダ数を超えています。");
            ZipArchiveData archive = new ZipArchiveData(parts);
            source.Position = centralOffset;
            // Archive Extra Data Record は、存在すれば中央領域サイズの一部として扱う。
            if (centralSize >= 8 && Bytes.U32(Bytes.At(source, centralOffset, 4), 0) == 0x08064b50U)
            {
                byte[] extraHeader = Bytes.At(source, centralOffset, 8);
                long next = Bytes.Add(centralOffset, Bytes.Add(8, Bytes.U32(extraHeader, 4)));
                if (next > metadataEnd) throw new InvalidDataException("Archive Extra Data Record が範囲外です。");
                source.Position = next;
            }
            else source.Position = centralOffset;
            for (long index = 0; index < count; index++)
            {
                Program.CheckCancel();
                long headerOffset = source.Position;
                if (headerOffset > metadataEnd - 46) throw new InvalidDataException("中央ヘッダが不足しています。");
                byte[] header = Bytes.Read(source, 46);
                if (Bytes.U32(header, 0) != 0x02014b50U) throw new InvalidDataException("中央ヘッダシグネチャ不一致: offset=" + headerOffset);
                int nameLength = Bytes.U16(header, 28), extraLength = Bytes.U16(header, 30), commentLength = Bytes.U16(header, 32);
                long next = Bytes.Add(source.Position, nameLength + extraLength + commentLength);
                if (next > metadataEnd || nameLength == 0) throw new InvalidDataException("中央ヘッダの可変長領域またはファイル名が不正です。");
                ZipEntry entry = new ZipEntry();
                entry.Archive = archive;
                entry.Flags = Bytes.U16(header, 8); entry.Method = Bytes.U16(header, 10);
                entry.DosTime = Bytes.U16(header, 12); entry.DosDate = Bytes.U16(header, 14);
                entry.Crc = Bytes.U32(header, 16); entry.CompressedSize = Bytes.U32(header, 20); entry.Size = Bytes.U32(header, 24);
                entry.Attributes = Bytes.U32(header, 38); entry.LocalOffset = Bytes.U32(header, 42);
                long disk = Bytes.U16(header, 34);
                entry.RawName = Bytes.Read(source, nameLength);
                Dictionary<ushort, byte[]> extra = Extras(Bytes.Read(source, extraLength));
                source.Position = next;
                ResolveCentral64(entry, extra, ref disk);
                if (disk != 0) throw new InvalidDataException("エントリが別ディスクを参照しています。");
                entry.RawDecodedName = DecodeRaw(entry.RawName, entry.Flags);
                entry.Name = DecodeName(entry.RawName, entry.Flags, extra);
                entry.Aes = ReadAes(extra, entry.Method, entry.Flags, entry.Crc);
                uint type = (entry.Attributes >> 16) & 0xf000U;
                entry.IsDirectory = entry.Name.EndsWith("/", StringComparison.Ordinal) || entry.Name.EndsWith("\\", StringComparison.Ordinal)
                    || (entry.Attributes & 16) != 0 || type == 0x4000;
                if (type == 0xa000) entry.SpecialObject = "external attributes の UNIX symlink";
                else if (type != 0 && type != 0x4000 && type != 0x8000) entry.SpecialObject = "UNIX の特殊ファイル種別 0x" + type.ToString("X");
                if ((entry.Attributes & 0x400) != 0) entry.SpecialObject = "external attributes の reparse point";
                if ((entry.Attributes & 8) != 0) entry.SpecialObject = "ボリュームラベル";
                ReadDosTime(entry);
                ApplyExtraMetadata(entry, extra, false);
                archive.Entries.Add(entry);
            }
            // 中央ディレクトリの optional digital signature は構造だけ検査する。
            if (source.Position < metadataEnd)
            {
                if (metadataEnd - source.Position < 6) throw new InvalidDataException("中央ディレクトリ末尾に不明なデータがあります。");
                byte[] signature = Bytes.Read(source, 6);
                if (Bytes.U32(signature, 0) != 0x05054b50U || source.Position + Bytes.U16(signature, 4) != metadataEnd)
                    throw new InvalidDataException("中央ディレクトリ署名レコードの構造が不正です。");
                source.Position = metadataEnd;
            }
            if (source.Position != metadataEnd) throw new InvalidDataException("中央ディレクトリの消費サイズが一致しません。");
            archive.Entries.Sort(delegate (ZipEntry a, ZipEntry b) { return a.LocalOffset.CompareTo(b.LocalOffset); });
            long cursor = 0;
            for (int i = 0; i < archive.Entries.Count; i++)
            {
                Program.CheckCancel();
                ZipEntry entry = archive.Entries[i];
                long next = i + 1 < archive.Entries.Count ? archive.Entries[i + 1].LocalOffset : centralOffset;
                if (entry.LocalOffset != cursor) throw new InvalidDataException("ローカルヘッダに空隙・重複・未参照データがあります。SFX も対象外です: " + entry.Label);
                ReadLocal(source, entry, next);
                cursor = next;
            }
            if (cursor != centralOffset) throw new InvalidDataException("ローカル領域が中央ディレクトリ開始位置まで連続していません。");
            return archive;
        }

        /// <summary>ZIP64 extra の値は、プレースホルダになっている項目だけを規定順に読む。</summary>
        private static void ResolveCentral64(ZipEntry entry, Dictionary<ushort, byte[]> extra, ref long disk)
        {
            bool needed = entry.Size == UInt32.MaxValue || entry.CompressedSize == UInt32.MaxValue
                || entry.LocalOffset == UInt32.MaxValue || disk == UInt16.MaxValue;
            if (!needed) return;
            byte[] field;
            if (!extra.TryGetValue(1, out field)) throw new InvalidDataException("中央ヘッダの ZIP64 extra が不足しています。");
            int p = 0;
            if (entry.Size == UInt32.MaxValue) { entry.Size = Bytes.I64(field, p); p += 8; }
            if (entry.CompressedSize == UInt32.MaxValue) { entry.CompressedSize = Bytes.I64(field, p); p += 8; }
            if (entry.LocalOffset == UInt32.MaxValue) { entry.LocalOffset = Bytes.I64(field, p); p += 8; }
            if (disk == UInt16.MaxValue) disk = Bytes.U32(field, p);
        }

        /// <summary>ローカルヘッダ・データサイズ・descriptor を中央情報と照合する。</summary>
        /// <param name="source">検証対象 ZIP のシーク可能な入力ストリーム。</param>
        /// <param name="entry">中央情報を持つエントリ。データ位置と補助メタデータも設定する。</param>
        /// <param name="limit">次のローカルヘッダまたは中央ディレクトリの開始位置（専有範囲の終端）。</param>
        private static void ReadLocal(Stream source, ZipEntry entry, long limit)
        {
            long first = entry.LocalOffset;
            if (first > limit - 30) throw new InvalidDataException("ローカルヘッダが重複または切断されています: " + entry.Label);
            byte[] h = Bytes.At(source, first, 30);
            if (Bytes.U32(h, 0) != 0x04034b50U) throw new InvalidDataException("ローカルヘッダシグネチャ不一致: " + entry.Label);
            if (Bytes.U16(h, 6) != entry.Flags || Bytes.U16(h, 8) != entry.Method
                || Bytes.U16(h, 10) != entry.DosTime || Bytes.U16(h, 12) != entry.DosDate)
                throw new InvalidDataException("中央とローカルの flags / method / DOS 日時が不一致: " + entry.Label);
            int nameLength = Bytes.U16(h, 26), extraLength = Bytes.U16(h, 28);
            entry.DataOffset = Bytes.Add(first, 30 + nameLength + extraLength);
            if (entry.DataOffset > limit) throw new InvalidDataException("ローカルヘッダの可変領域が範囲外: " + entry.Label);
            byte[] raw = Bytes.Read(source, nameLength);
            if (!Bytes.Equal(raw, entry.RawName)) throw new InvalidDataException("中央とローカルのファイル名バイト列が不一致: " + entry.Label);
            Dictionary<ushort, byte[]> extra = Extras(Bytes.Read(source, extraLength));
            entry.LocalDecodedName = DecodeName(raw, entry.Flags, extra);
            byte[] unicode;
            if (extra.TryGetValue(0x7075, out unicode) && ValidUnicode(unicode, raw)
                && !String.Equals(entry.LocalDecodedName, entry.Name, StringComparison.Ordinal))
                throw new InvalidDataException("中央とローカルの Unicode Path が不一致: " + entry.Label);
            AesInfo localAes = ReadAes(extra, entry.Method, entry.Flags, entry.Crc);
            if ((entry.Aes == null) != (localAes == null) || (entry.Aes != null && !entry.Aes.Same(localAes)))
                throw new InvalidDataException("中央とローカルの AES 情報が不一致: " + entry.Label);
            ApplyExtraMetadata(entry, extra, true);
            if (!entry.Times.ModifiedUtc.HasValue) entry.TimestampWarning = "有効な更新日時が ZIP にありません。取得できない日時は OS の値を維持します。";
            long compressed = Bytes.U32(h, 18), size = Bytes.U32(h, 22);
            if (compressed == UInt32.MaxValue || size == UInt32.MaxValue)
            {
                byte[] field;
                if (!extra.TryGetValue(1, out field)) throw new InvalidDataException("ローカル ZIP64 extra が不足しています: " + entry.Label);
                int p = 0;
                if (size == UInt32.MaxValue) { size = Bytes.I64(field, p); p += 8; }
                if (compressed == UInt32.MaxValue) compressed = Bytes.I64(field, p);
            }
            uint crc = Bytes.U32(h, 14);
            bool descriptor = (entry.Flags & 8) != 0;
            if (!descriptor)
            {
                if (compressed != entry.CompressedSize || size != entry.Size || crc != entry.Crc)
                    throw new InvalidDataException("中央とローカルのサイズまたは CRC が不一致: " + entry.Label);
            }
            // R02: bit 3 が立つ場合、ローカルの CRC・両サイズは確定値として照合しない。
            // 非シーク先へ出す暗号化 ZIP では、例えば DOS 時刻 << 16 が CRC 欄に残る。
            // これは任意値を認める ZIP 書込み規定ではなく、既存 ZIP との読取り互換性対応。
            // 上記の ZIP64 extra 検査は維持し、下記では中央サイズから求めた境界と
            // descriptor の署名・CRC・両サイズを照合する。実データの CRC / 認証も別途検証する。
            long dataEnd = Bytes.Add(entry.DataOffset, entry.CompressedSize);
            if (dataEnd > limit) throw new InvalidDataException("圧縮データが次のレコードに重複しています: " + entry.Label);
            if (!descriptor)
            {
                if (dataEnd != limit) throw new InvalidDataException("エントリ後方に未参照データがあります: " + entry.Label);
            }
            else
            {
                long length = limit - dataEnd;
                if (length != 12 && length != 16 && length != 20 && length != 24)
                    throw new InvalidDataException("data descriptor 長が不正です: " + entry.Label);
                byte[] dd = Bytes.At(source, dataEnd, (int)length);
                int p = (length == 16 || length == 24) ? 4 : 0;
                if (p == 4 && Bytes.U32(dd, 0) != 0x08074b50U) throw new InvalidDataException("descriptor シグネチャ不一致: " + entry.Label);
                uint ddCrc = Bytes.U32(dd, p); p += 4;
                bool wide = length == 20 || length == 24;
                long ddCompressed = wide ? Bytes.I64(dd, p) : Bytes.U32(dd, p); p += wide ? 8 : 4;
                long ddSize = wide ? Bytes.I64(dd, p) : Bytes.U32(dd, p);
                if (ddCrc != entry.Crc || ddCompressed != entry.CompressedSize || ddSize != entry.Size)
                    throw new InvalidDataException("data descriptor と中央情報が不一致: " + entry.Label);
            }
            if (entry.IsDirectory && entry.Size != 0) throw new InvalidDataException("非空データを持つディレクトリエントリです: " + entry.Label);
        }

        /// <summary>extra field の長さを全検査する。重複する既知メタデータは曖昧なので拒否する。</summary>
        private static Dictionary<ushort, byte[]> Extras(byte[] bytes)
        {
            Dictionary<ushort, byte[]> result = new Dictionary<ushort, byte[]>();
            int p = 0;
            while (p < bytes.Length)
            {
                ushort id = Bytes.U16(bytes, p), count = Bytes.U16(bytes, p + 2); p += 4;
                Bytes.Need(bytes, p, count);
                bool known = id == 1 || id == 0x9901 || id == 0x7075 || id == 0x000a || id == 0x5455
                    || id == 0x000d || id == 0x5855 || id == 0x756e;
                if (known)
                {
                    if (result.ContainsKey(id)) throw new InvalidDataException("既知 extra field が重複しています: 0x" + id.ToString("X4"));
                    result.Add(id, Bytes.Sub(bytes, p, count));
                }
                p += count;
            }
            return result;
        }
        private static string DecodeRaw(byte[] raw, ushort flags)
        {
            if ((flags & 0x800) != 0) return Utf8.GetString(raw);
            try { return (Program.LegacyEncoding ?? Encoding.GetEncoding(932)).GetString(raw); }
            catch (DecoderFallbackException) { return Cp437.GetString(raw); }
        }
        private static bool ValidUnicode(byte[] field, byte[] raw)
        {
            return field.Length >= 5 && field[0] == 1 && Bytes.U32(field, 1) == Crc32.Compute(raw);
        }
        private static string DecodeName(byte[] raw, ushort flags, Dictionary<ushort, byte[]> extra)
        {
            string ordinary = DecodeRaw(raw, flags);
            byte[] field;
            if (extra.TryGetValue(0x7075, out field) && ValidUnicode(field, raw))
            {
                string unicode = Utf8.GetString(field, 5, field.Length - 5);
                if ((flags & 0x800) != 0 && !String.Equals(unicode, ordinary, StringComparison.Ordinal))
                    throw new InvalidDataException("UTF-8 flag と Unicode Path が矛盾しています。");
                return unicode;
            }
            return ordinary;
        }
        private static AesInfo ReadAes(Dictionary<ushort, byte[]> extra, ushort method, ushort flags, uint crc)
        {
            byte[] field;
            if (!extra.TryGetValue(0x9901, out field))
            {
                if (method == 99) throw new InvalidDataException("method 99 に AES extra がありません。");
                return null;
            }
            if (method != 99 || (flags & 1) == 0) throw new InvalidDataException("AES extra と method / 暗号化 flag が矛盾しています。");
            Bytes.Need(field, 0, 7);
            AesInfo result = new AesInfo();
            result.Version = Bytes.U16(field, 0); result.Strength = field[4]; result.Method = Bytes.U16(field, 5);
            if ((result.Version != 1 && result.Version != 2) || field[2] != (byte)'A' || field[3] != (byte)'E'
                || result.Strength < 1 || result.Strength > 3)
                throw new InvalidDataException("未対応または不正な WinZip AES extra です。");
            if (result.Version == 2 && crc != 0) throw new InvalidDataException("AE-2 の CRC は 0 である必要があります。");
            return result;
        }
        private static void ReadDosTime(ZipEntry entry)
        {
            try
            {
                int d = entry.DosDate, t = entry.DosTime;
                DateTime local = new DateTime(1980 + (d >> 9), (d >> 5) & 15, d & 31,
                    t >> 11, (t >> 5) & 63, (t & 31) * 2, DateTimeKind.Local);
                entry.Times.SetModified(local.ToUniversalTime(), 0);
            }
            catch (ArgumentOutOfRangeException) { /* extra field に有効日時がある可能性がある。 */ }
        }
        private static DateTime UnixTime(byte[] field, int p)
        {
            int seconds = unchecked((int)Bytes.U32(field, p));
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds);
        }
        /// <summary>ローカル情報は同種の中央情報より優先し、異種では NTFS の精度を優先する。</summary>
        private static void ApplyExtraMetadata(ZipEntry entry, Dictionary<ushort, byte[]> extra, bool local)
        {
            byte[] field;
            int increment = local ? 1 : 0;
            if (extra.TryGetValue(0x000d, out field))
            {
                if (field.Length > 12) entry.SpecialObject = "UNIX extra 内のリンク先またはデバイス情報";
                if (field.Length >= 8)
                {
                    entry.Times.SetAccessed(UnixTime(field, 0), 10 + increment);
                    entry.Times.SetModified(UnixTime(field, 4), 10 + increment);
                }
            }
            if (extra.TryGetValue(0x756e, out field))
            {
                Bytes.Need(field, 0, 14);
                uint type = (uint)Bytes.U16(field, 4) & 0xf000U;
                if (type == 0xa000 || field.Length > 14) entry.SpecialObject = "ASi UNIX extra 内の symlink / リンク先";
                else if (type != 0 && type != 0x4000 && type != 0x8000) entry.SpecialObject = "ASi UNIX extra 内の特殊ファイル";
                if (Bytes.U32(field, 0) != Crc32.Compute(Bytes.Sub(field, 4, field.Length - 4)))
                    throw new InvalidDataException("ASi UNIX extra の CRC が不一致です。");
            }
            if (extra.TryGetValue(0x5855, out field) && field.Length >= 8)
            {
                entry.Times.SetAccessed(UnixTime(field, 0), 10 + increment);
                entry.Times.SetModified(UnixTime(field, 4), 10 + increment);
            }
            if (extra.TryGetValue(0x5455, out field))
            {
                Bytes.Need(field, 0, 1);
                int p = 1, flags = field[0];
                if ((flags & 1) != 0) { entry.Times.SetModified(UnixTime(field, p), 20 + increment); p += 4; }
                // 中央 UT は mtime だけを持つことがある。flags に atime/ctime があっても欠落を許容する。
                if ((flags & 2) != 0 && (local || field.Length >= p + 4)) { entry.Times.SetAccessed(UnixTime(field, p), 20 + increment); p += 4; }
                if ((flags & 4) != 0 && (local || field.Length >= p + 4)) entry.Times.SetCreated(UnixTime(field, p), 20 + increment);
            }
            if (extra.TryGetValue(0x000a, out field))
            {
                Bytes.Need(field, 0, 4);
                int p = 4;
                while (p < field.Length)
                {
                    ushort tag = Bytes.U16(field, p), size = Bytes.U16(field, p + 2); p += 4;
                    Bytes.Need(field, p, size);
                    if (tag == 1)
                    {
                        if (size < 24) throw new InvalidDataException("NTFS timestamp 属性が短すぎます。");
                        try
                        {
                            long m = Bytes.I64(field, p), a = Bytes.I64(field, p + 8), c = Bytes.I64(field, p + 16);
                            if (m != 0) entry.Times.SetModified(DateTime.FromFileTimeUtc(m), 30 + increment);
                            if (a != 0) entry.Times.SetAccessed(DateTime.FromFileTimeUtc(a), 30 + increment);
                            if (c != 0) entry.Times.SetCreated(DateTime.FromFileTimeUtc(c), 30 + increment);
                        }
                        catch (ArgumentOutOfRangeException) { entry.TimestampWarning = "NTFS extra に範囲外の日時があります。取得できた他の日時を使用します。"; }
                    }
                    p += size;
                }
            }
        }
    }

    /// <summary>Windows の文字列パスを正規化する。リアルパスや UNC への変換は行わない。</summary>
    internal static class WindowsPaths
    {
        internal static string WithSlash(string path) { return path.EndsWith("\\", StringComparison.Ordinal) ? path : path + "\\"; }
        internal static string TrimSlash(string path)
        {
            string root = Path.GetPathRoot(path);
            while (path.Length > root.Length && path.EndsWith("\\", StringComparison.Ordinal)) path = path.Substring(0, path.Length - 1);
            return path;
        }
        internal static string Full(string path)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentException("空のパスです。");
            path = path.Replace('/', '\\');
            if (path.StartsWith("\\\\?\\", StringComparison.Ordinal) || path.StartsWith("\\\\.\\", StringComparison.Ordinal))
                throw new ArgumentException("デバイス名前空間・拡張長パスは使用できません: " + path);
            bool drive = path.Length >= 3 && ((path[0] >= 'A' && path[0] <= 'Z') || (path[0] >= 'a' && path[0] <= 'z'))
                && path[1] == ':' && path[2] == '\\';
            bool unc = path.StartsWith("\\\\", StringComparison.Ordinal);
            if (!drive && !unc) throw new ArgumentException("相対パスは不可です。完全修飾パスが必要です: " + path);
            if (unc)
            {
                string[] elements = path.Substring(2).Split('\\');
                if (elements.Length < 2 || elements[0].Length == 0 || elements[1].Length == 0)
                    throw new ArgumentException("UNC パスには server と share が必要です: " + path);
            }
            return Path.GetFullPath(path);
        }
        /// <summary>ZIP 相対パスを正規化する。遡り、ADS、予約名、曖昧な 8.3 名等は全体中断。</summary>
        internal static string Relative(string name, bool directory)
        {
            if (name == null || name.Length == 0) throw new SafetyException("ZIP 内の空のパスは許可しません。");
            string value = name.Replace('/', '\\');
            if (value[0] == '\\' || value.IndexOf(':') >= 0) throw new SafetyException("ZIP に絶対パス・ドライブ指定・ADS があります: " + name);
            List<string> result = new List<string>();
            foreach (string original in value.Split('\\'))
            {
                if (original.Length == 0 || original == ".") continue;
                if (original.TrimEnd(' ') == "..") throw new SafetyException("ZIP にディレクトリ遡り '..' があります: " + name);
                foreach (char c in original)
                {
                    if (c < 32 || c == 127 || c == '"' || c == '<' || c == '>' || c == '|' || c == '*' || c == '?')
                        throw new SafetyException("ZIP パスに禁止文字があります: " + name);
                }
                string item = original.TrimEnd(' ', '.');
                if (item.Length == 0 || item == "..") throw new SafetyException("正規化で空または遡りになるパス要素です: " + name);
                string stem = item;
                int dot = stem.IndexOf('.');
                if (dot >= 0) stem = stem.Substring(0, dot);
                stem = stem.TrimEnd(' ').ToUpperInvariant();
                bool reserved = stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL"
                    || stem == "CONIN$" || stem == "CONOUT$" || stem == "CLOCK$";
                if (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)))
                    reserved |= (stem[3] >= '1' && stem[3] <= '9') || stem[3] == '\u00b9' || stem[3] == '\u00b2' || stem[3] == '\u00b3';
                if (reserved) throw new SafetyException("ZIP パスが Windows の予約デバイス名です: " + name);
                if (LooksLikeShortAlias(item)) throw new SafetyException("8.3 短縮名との別名衝突を事前に確定できない名前を拒否します: " + name);
                result.Add(item);
            }
            if (result.Count == 0 && !directory) throw new SafetyException("正規化後のファイルパスが空です: " + name);
            return String.Join("\\", result.ToArray());
        }
        private static bool LooksLikeShortAlias(string item)
        {
            int dot = item.IndexOf('.');
            string stem = dot < 0 ? item : item.Substring(0, dot);
            if (stem.Length > 8 || (dot >= 0 && (item.Length - dot - 1 > 3 || item.IndexOf('.', dot + 1) >= 0))) return false;
            int tilde = stem.LastIndexOf('~');
            if (tilde < 1 || tilde == stem.Length - 1) return false;
            for (int i = tilde + 1; i < stem.Length; i++) if (stem[i] < '0' || stem[i] > '9') return false;
            return true;
        }
        internal static string Parent(string relative)
        {
            int p = relative.LastIndexOf('\\');
            return p < 0 ? "" : relative.Substring(0, p);
        }
        internal static int Depth(string relative)
        {
            if (relative.Length == 0) return 0;
            int count = 1;
            foreach (char c in relative) if (c == '\\') count++;
            return count;
        }
        internal static void CheckLength(string full, bool creatingDirectory)
        {
            if (full.Length >= 260 || (creatingDirectory && full.Length >= 248))
                throw new PathTooLongException(".NET Framework 4.0 の通常パス長制限を超えています: " + full);
        }
    }

    /// <summary>開いた対象の識別情報。パスの別名と、事前確認後の対象の入れ替わりの判定に使う。</summary>
    internal sealed class FileStamp
    {
        internal uint Attributes, Links;
        internal long Length, Modified;
        internal string Identity;
        internal bool IsDirectory { get { return (Attributes & 16) != 0; } }
        internal bool IsReparse { get { return (Attributes & 0x400) != 0; } }
        internal bool Same(FileStamp other)
        {
            return other != null && Identity != null && Identity == other.Identity && Length == other.Length && Modified == other.Modified;
        }
    }

    /// <summary>必要最小限の Win32 相互運用。ドライブを UNC に変換する API は使用しない。</summary>
    internal static class Native
    {
        internal const uint ListDirectory = 0x1, ReadAttributes = 0x80, WriteAttributes = 0x100;
        internal const uint GenericRead = 0x80000000, GenericWrite = 0x40000000, DeleteAccess = 0x10000;
        internal const uint OpenExisting = 3, CreateNew = 1;
        internal const uint BackupSemantics = 0x02000000, OpenReparsePoint = 0x00200000;
        internal const uint ShareRead = 1, ShareWrite = 2, ShareDelete = 4;

        [StructLayout(LayoutKind.Sequential)]
        internal struct FileTime
        {
            internal uint Low, High;
            internal long Value { get { return unchecked((long)(((ulong)High << 32) | Low)); } }
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct ByHandleInformation
        {
            internal uint Attributes;
            internal FileTime Creation, Access, Write;
            internal uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeFileHandle CreateFileW(string fileName, uint access, uint share, IntPtr security,
            uint disposition, uint attributes, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out ByHandleInformation info);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFileAttributesW(string name);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateDirectoryW(string name, IntPtr security);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetLongPathNameW(string shortPath, StringBuilder longPath, uint count);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetFileTime(SafeFileHandle handle, ref long creation, ref long access, ref long write);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int informationClass, IntPtr data, uint length);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool MoveFileExW(string existingFileName, string newFileName, uint flags);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllocConsole();
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr GetStdHandle(int number);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetConsoleMode(IntPtr handle, out uint mode);

        internal static Win32Exception Error(string operation, string path)
        {
            int code = Marshal.GetLastWin32Error();
            return new Win32Exception(code, operation + ": " + path + " / " + new Win32Exception(code).Message);
        }
        internal static void EnsureConsole()
        {
            if (GetConsoleWindow() == IntPtr.Zero && !AllocConsole()) throw Error("コンソールを作成できません", "");
            Console.OutputEncoding = new UTF8Encoding(false);
            StreamWriter writer = new StreamWriter(Console.OpenStandardOutput(), Console.OutputEncoding);
            writer.AutoFlush = true;
            Console.SetOut(writer);
            StreamWriter error = new StreamWriter(Console.OpenStandardError(), Console.OutputEncoding);
            error.AutoFlush = true;
            Console.SetError(error);
            Console.SetIn(new StreamReader(Console.OpenStandardInput(), Console.InputEncoding, false));
        }
        /// <summary>8.3 名の展開のみ。シンボリックリンクやドライブのリアルパス解決は行わない。</summary>
        internal static string LongName(string full)
        {
            StringBuilder result = new StringBuilder(260);
            uint got = GetLongPathNameW(full, result, (uint)result.Capacity);
            if (got == 0) return full; // 親の一覧権限がない場合でも、直接読取可能なら処理を継続する。
            if (got >= result.Capacity) throw new PathTooLongException("入力ファイルの長い名前が通常パス制限を超えています。");
            return result.ToString();
        }
        internal static FileStamp Stamp(SafeFileHandle handle)
        {
            ByHandleInformation info;
            if (!GetFileInformationByHandle(handle, out info)) throw Error("ファイル情報の取得失敗", "開いたハンドル");
            FileStamp result = new FileStamp();
            result.Attributes = info.Attributes; result.Links = info.Links;
            result.Length = checked((long)(((ulong)info.SizeHigh << 32) | info.SizeLow));
            result.Modified = info.Write.Value;
            result.Identity = (info.IndexHigh == 0 && info.IndexLow == 0) ? null
                : info.Volume.ToString("X8") + ":" + info.IndexHigh.ToString("X8") + info.IndexLow.ToString("X8");
            return result;
        }
        /// <summary>存在しない場合だけ false。アクセス拒否等を「ない」と誤認しない。</summary>
        internal static bool Attributes(string path, out uint attributes)
        {
            WindowsPaths.CheckLength(path, false);
            attributes = GetFileAttributesW(path);
            if (attributes != UInt32.MaxValue) return true;
            int error = Marshal.GetLastWin32Error();
            if (error == 2 || error == 3) return false;
            throw Error("属性の確認失敗", path);
        }
        /// <summary>新規作成した場合だけ true。既存オブジェクトは呼出元が再検査する。</summary>
        internal static bool MakeDirectory(string path)
        {
            WindowsPaths.CheckLength(path, true);
            if (CreateDirectoryW(path, IntPtr.Zero)) return true;
            if (Marshal.GetLastWin32Error() == 183) return false;
            throw Error("ディレクトリ作成失敗", path);
        }
        internal static SafeFileHandle OpenDirectory(string path, bool allowReparse, bool writeTimes, bool follow)
        {
            WindowsPaths.CheckLength(path, false);
            // 属性専用アクセスは共有制御の対象外となり得るため、ディレクトリ読取アクセスも要求する。
            // 重要: 展開先ディレクトリを ShareRead のみで長時間保持すると、同一ディレクトリ内の
            // MoveFileExW による一時ファイル確定処理と自己競合し ERROR_SHARING_VIOLATION (32) になる。
            // そのため共有は読取・書込・削除を許可する。境界安全性は共有拒否ではなく、各操作時の
            // reparse-point 検査と、作成済みディレクトリのファイル ID 照合によって確認する。
            SafeFileHandle handle = CreateFileW(path, ListDirectory | ReadAttributes | (writeTimes ? WriteAttributes : 0),
                ShareRead | ShareWrite | ShareDelete,
                IntPtr.Zero, OpenExisting, BackupSemantics | (follow ? 0 : OpenReparsePoint), IntPtr.Zero);
            if (handle.IsInvalid) { Win32Exception error = Error("ディレクトリを安全に固定できません", path); handle.Dispose(); throw error; }
            try
            {
                FileStamp info = Stamp(handle);
                if (info.IsReparse && !allowReparse) throw new SafetyException("展開先境界内に reparse point / junction / symlink があります: " + path);
                if (!info.IsDirectory) throw new IOException("ディレクトリの場所にファイルがあります: " + path);
                return handle;
            }
            catch { handle.Dispose(); throw; }
        }
        /// <summary>FILETIME=0 は当該時刻を変更しない。null の項目には 0 を渡す。</summary>
        internal static void Times(SafeFileHandle handle, EntryTimes value)
        {
            EntryTimes resolved = value.Resolved();
            long creation = resolved.CreatedUtc.HasValue ? resolved.CreatedUtc.Value.ToFileTimeUtc() : 0;
            long access = resolved.AccessedUtc.HasValue ? resolved.AccessedUtc.Value.ToFileTimeUtc() : 0;
            long write = resolved.ModifiedUtc.HasValue ? resolved.ModifiedUtc.Value.ToFileTimeUtc() : 0;
            if (!SetFileTime(handle, ref creation, ref access, ref write)) throw Error("日時の設定失敗", "開いたハンドル");
        }
        /// <summary>検証済み一時ファイルを同一ディレクトリ内の最終名へ確定する。元ファイルを先に削除しない。</summary>
        /// <param name="source">検証済み一時ファイルのフルパス。</param>
        /// <param name="destination">最終保存先のフルパス。</param>
        /// <param name="replace">既存ファイルを置換してよい場合は true。</param>
        internal static void MoveVerifiedFile(string source, string destination, bool replace)
        {
            WindowsPaths.CheckLength(source, false);
            WindowsPaths.CheckLength(destination, false);
            const uint MoveFileReplaceExisting = 0x00000001;
            uint flags = replace ? MoveFileReplaceExisting : 0U;
            if (!MoveFileExW(source, destination, flags))
                throw Error("検証済みファイルの確定失敗", destination);
        }
        /// <summary>パス名ではなく、保持している一時ファイルそのものを削除予定にする。</summary>
        internal static void DeleteOnClose(SafeFileHandle handle)
        {
            IntPtr memory = Marshal.AllocHGlobal(4);
            try
            {
                Marshal.WriteInt32(memory, 1);
                if (!SetFileInformationByHandle(handle, 4, memory, 4)) throw Error("一時ファイルの削除予約失敗", "開いたハンドル");
            }
            finally { Marshal.FreeHGlobal(memory); }
        }
    }

    /// <summary>作成したディレクトリの実体を記憶し、後処理で別物の日時を変更しない。</summary>
    internal sealed class CreatedDirectory
    {
        internal string Relative, Identity;
    }

    /// <summary>1 操作中、親ディレクトリの実体をハンドルで追跡する。</summary>
    internal sealed class PathLease : IDisposable
    {
        internal readonly List<SafeFileHandle> Handles = new List<SafeFileHandle>();
        internal SafeFileHandle Leaf;
        internal bool Missing;
        public void Dispose()
        {
            for (int i = Handles.Count - 1; i >= 0; i--) Handles[i].Dispose();
            Handles.Clear();
        }
    }

    /// <summary>指定されたルートのリンクは許容するが、その下のリンク境界は一切たどらない。</summary>
    internal sealed class SafeRoot : IDisposable
    {
        internal readonly string Root;
        private readonly List<SafeFileHandle> anchors = new List<SafeFileHandle>();
        private readonly Dictionary<string, CreatedDirectory> created = new Dictionary<string, CreatedDirectory>(StringComparer.OrdinalIgnoreCase);
        private readonly WarningBook warnings;
        private SafeFileHandle rootHandle;
        internal SafeRoot(string path, WarningBook book)
        {
            Root = WindowsPaths.TrimSlash(WindowsPaths.Full(path));
            warnings = book;
            try
            {
                // 選択済みルートまでの名前を固定するだけで、実体パスは問い合わせない。
                string driveRoot = Path.GetPathRoot(Root);
                string current = driveRoot;
                SafeFileHandle first = Native.OpenDirectory(current, true, false, false);
                anchors.Add(first);
                string remaining = Root.Substring(driveRoot.Length);
                foreach (string part in remaining.Split('\\'))
                {
                    if (part.Length == 0) continue;
                    current = WindowsPaths.WithSlash(current) + part;
                    anchors.Add(Native.OpenDirectory(current, true, false, false));
                }
                // 選択された root が junction/symlink でも実体ディレクトリとして許容する。
                rootHandle = Native.OpenDirectory(Root, true, false, true);
                anchors.Add(rootHandle);
            }
            catch { Dispose(); throw; }
        }
        internal string Destination(string relative)
        {
            string result = relative.Length == 0 ? Root : WindowsPaths.WithSlash(Root) + relative;
            WindowsPaths.CheckLength(result, false);
            return result;
        }
        /// <summary>relative ディレクトリを安全に開く。create=false なら事前検査だけで作成しない。</summary>
        internal PathLease DirectoryLease(string relative, bool create, Dictionary<string, DirectoryPlan> plans, bool writeLeafTimes)
        {
            PathLease lease = new PathLease();
            lease.Leaf = rootHandle;
            try
            {
                string current = "";
                string[] parts = relative.Length == 0 ? new string[0] : relative.Split('\\');
                for (int i = 0; i < parts.Length; i++)
                {
                    current = current.Length == 0 ? parts[i] : current + "\\" + parts[i];
                    string full = Destination(current);
                    uint attributes;
                    bool exists = Native.Attributes(full, out attributes);
                    bool made = false;
                    if (!exists)
                    {
                        if (!create) { lease.Missing = true; return lease; }
                        made = Native.MakeDirectory(full);
                    }
                    else if ((attributes & 0x400) != 0)
                        throw new SafetyException("展開先ルートの下でリンク境界をまたぐため中断します: " + full);
                    CreatedDirectory recorded;
                    bool owned = created.TryGetValue(current, out recorded);
                    bool write = made || (writeLeafTimes && i == parts.Length - 1 && owned);
                    SafeFileHandle handle = Native.OpenDirectory(full, false, write, false);
                    lease.Handles.Add(handle);
                    lease.Leaf = handle;
                    FileStamp stamp = Native.Stamp(handle);
                    if (owned && (recorded.Identity == null || recorded.Identity != stamp.Identity))
                        throw new SafetyException("本処理が作成したディレクトリの実体を確認できないか、入れ替わっています: " + full);
                    if (made)
                    {
                        CreatedDirectory record = new CreatedDirectory { Relative = current, Identity = stamp.Identity };
                        created[current] = record;
                        DirectoryPlan plan;
                        if (plans != null && plans.TryGetValue(current, out plan) && plan.Effective != null)
                        {
                            try { Native.Times(handle, plan.Effective); }
                            catch (Exception ex)
                            {
                                if (!Program.Recoverable(ex)) throw;
                                warnings.Add("ディレクトリ: " + full, "作成時の日時設定: " + ex.Message);
                            }
                        }
                    }
                }
                return lease;
            }
            catch { lease.Dispose(); throw; }
        }
        /// <summary>親を固定した状態で最後のパス要素を no-follow 検査する。</summary>
        internal FileStamp ProbeLeaf(string relative, PathLease parents)
        {
            if (parents.Missing) return null;
            string full = Destination(relative);
            uint attributes;
            if (!Native.Attributes(full, out attributes)) return null;
            if ((attributes & 0x400) != 0) throw new SafetyException("展開対象が symlink / junction / reparse point です: " + full);
            SafeFileHandle handle = Native.CreateFileW(full, Native.ReadAttributes,
                Native.ShareRead | Native.ShareWrite | Native.ShareDelete, IntPtr.Zero, Native.OpenExisting,
                Native.OpenReparsePoint | Native.BackupSemantics, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                int code = Marshal.GetLastWin32Error();
                Win32Exception error = Native.Error("展開対象の確認失敗", full);
                handle.Dispose();
                if (code == 2 || code == 3) return null;
                throw error;
            }
            using (handle)
            {
                FileStamp stamp = Native.Stamp(handle);
                if (stamp.IsReparse) throw new SafetyException("展開対象のリンクへの入れ替わりを検出しました: " + full);
                // ハードリンク経由の既存ファイル改変も安全側に禁止する。
                if (!stamp.IsDirectory && stamp.Links > 1) throw new SafetyException("複数ハードリンクを持つ展開先ファイルは安全のため上書きしません: " + full);
                return stamp;
            }
        }
        internal FileStamp Probe(string relative)
        {
            if (relative.Length == 0) return Native.Stamp(rootHandle);
            using (PathLease parents = DirectoryLease(WindowsPaths.Parent(relative), false, null, false)) return ProbeLeaf(relative, parents);
        }
        /// <summary>新規に作成したディレクトリだけを、深い階層から再設定する。</summary>
        internal void RestoreCreatedDirectoryTimes(Dictionary<string, DirectoryPlan> plans)
        {
            List<CreatedDirectory> list = new List<CreatedDirectory>(created.Values);
            list.Sort(delegate (CreatedDirectory a, CreatedDirectory b) { return WindowsPaths.Depth(b.Relative).CompareTo(WindowsPaths.Depth(a.Relative)); });
            foreach (CreatedDirectory item in list)
            {
                try
                {
                    using (PathLease lease = DirectoryLease(item.Relative, false, null, true))
                    {
                        if (lease.Missing) throw new IOException("作成したディレクトリが見つかりません。");
                        DirectoryPlan plan;
                        if (plans.TryGetValue(item.Relative, out plan) && plan.Effective != null) Native.Times(lease.Leaf, plan.Effective);
                    }
                }
                catch (Exception ex)
                {
                    if (!Program.Recoverable(ex)) throw;
                    warnings.Add("ディレクトリ: " + WindowsPaths.WithSlash(Root) + item.Relative, "終了時の日時復元: " + ex.Message);
                }
            }
        }
        public void Dispose()
        {
            for (int i = anchors.Count - 1; i >= 0; i--) anchors[i].Dispose();
            anchors.Clear();
        }
    }

    /// <summary>検証済みデータだけを最終名へ確定する一時保存先。失敗時はハンドルで削除する。</summary>
    internal sealed class StagedFile : IDisposable
    {
        internal readonly string TemporaryPath;
        internal readonly FileStream Stream;
        private bool committed;
        private bool streamClosed;
        private readonly WarningBook warnings;
        private readonly ZipEntry entry;
        internal StagedFile(string directory, ZipEntry item, WarningBook book)
        {
            entry = item; warnings = book;
            string prefix = WindowsPaths.WithSlash(directory);
            int available = 259 - prefix.Length;
            if (available < 1) throw new PathTooLongException("一時保存先を作成できるパス長がありません。");
            SafeFileHandle handle = null;
            for (int attempt = 0; attempt < 32; attempt++)
            {
                string name = (available >= 20 ? "._dnnt_" : "") + Guid.NewGuid().ToString("N");
                if (name.Length > available) name = name.Substring(0, available);
                if (String.Equals(name, Path.GetFileName(entry.Relative), StringComparison.OrdinalIgnoreCase)) continue;
                TemporaryPath = prefix + name;
                handle = Native.CreateFileW(TemporaryPath,
                    Native.GenericRead | Native.GenericWrite | Native.DeleteAccess, 0, IntPtr.Zero, Native.CreateNew, 0x80, IntPtr.Zero);
                if (!handle.IsInvalid) break;
                int code = Marshal.GetLastWin32Error();
                Win32Exception error = Native.Error("一時保存先を作成できません", TemporaryPath);
                handle.Dispose(); handle = null;
                if (code != 80 && code != 183) throw error;
            }
            if (handle == null) throw new IOException("衝突しない一時ファイル名を確保できませんでした。");
            try { Stream = new FileStream(handle, FileAccess.ReadWrite, 131072, false); }
            catch
            {
                // FileStream の構築自体が失敗した場合も、作成済み一時ファイルを残さない。
                try { Native.DeleteOnClose(handle); }
                catch (Exception cleanup)
                {
                    if (Program.Recoverable(cleanup))
                        warnings.Add(entry, "一時ファイル初期化失敗後の削除失敗: " + TemporaryPath + " / " + cleanup.Message);
                }
                finally { handle.Dispose(); }
                throw;
            }
        }
        /// <summary>検証・日時設定済みデータを最終パスへ確定する。</summary>
        /// <param name="path">最終保存先のフルパス。</param>
        /// <param name="replace">既存ファイルを置換してよい場合は true。</param>
        internal void Commit(string path, bool replace)
        {
            // FileRenameInfo を開いた FileStream に対して実行すると、環境によって
            // ERROR_SHARING_VIOLATION (32) となる場合がある。検証済み内容を Flush し、
            // ハンドルを閉じてから同一ディレクトリ内で MoveFileExW により確定する。
            CloseStream();
            Native.MoveVerifiedFile(TemporaryPath, path, replace);
            committed = true;
        }

        /// <summary>書込みストリームを一度だけ Flush・Close する。</summary>
        private void CloseStream()
        {
            if (streamClosed) return;
            Stream.Flush();
            Stream.Dispose();
            streamClosed = true;
        }

        public void Dispose()
        {
            if (!streamClosed)
            {
                try
                {
                    if (!committed)
                    {
                        try { Native.DeleteOnClose(Stream.SafeFileHandle); }
                        catch (Exception ex)
                        {
                            if (!Program.Recoverable(ex)) throw;
                            warnings.Add(entry, "一時ファイルの削除に失敗しました: " + TemporaryPath + " / " + ex.Message);
                        }
                    }
                }
                finally
                {
                    // 復号・検証中の本来の例外（安全性違反・取消し等を含む）を、
                    // 後片付け中の通常I/O例外で置き換えない。Commit前のFlush失敗は従来どおり伝播する。
                    try { Stream.Dispose(); }
                    catch (Exception ex)
                    {
                        if (!Program.Recoverable(ex)) throw;
                        warnings.Add(entry, "一時保存先を閉じる際に失敗しました: " + TemporaryPath + " / " + ex.Message);
                    }
                    finally { streamClosed = true; }
                }
                return;
            }

            // Commit 途中で失敗した場合は既にハンドルを閉じているため、パスで一時ファイルを掃除する。
            if (!committed)
            {
                try { File.Delete(TemporaryPath); }
                catch (Exception ex)
                {
                    if (!Program.Recoverable(ex)) throw;
                    warnings.Add(entry, "一時ファイルの削除に失敗しました: " + TemporaryPath + " / " + ex.Message);
                }
            }
        }
    }

    /// <summary>ディレクトリ日時の計画。明示ディレクトリエントリがなければ子孫ファイルの最新更新日。</summary>
    internal sealed class DirectoryPlan
    {
        internal string Relative;
        internal EntryTimes ExplicitTimes;
        internal DateTime? LatestFile;
        internal EntryTimes Effective
        {
            get
            {
                if (ExplicitTimes != null && ExplicitTimes.ModifiedUtc.HasValue) return ExplicitTimes.Resolved();
                return LatestFile.HasValue ? EntryTimes.All(LatestFile.Value) : null;
            }
        }
    }

    /// <summary>全エントリの安全性・重複・ファイル/ディレクトリ衝突を、書出し前に調べる。</summary>
    internal sealed class ExtractionPlan
    {
        internal readonly List<ZipEntry> Entries = new List<ZipEntry>();
        internal readonly Dictionary<string, DirectoryPlan> Directories = new Dictionary<string, DirectoryPlan>(StringComparer.OrdinalIgnoreCase);
        // 差分保存が失敗した場合だけ読むベースライン。キーは出力計画中の差分エントリ。
        internal readonly Dictionary<ZipEntry, ZipEntry> Fallbacks = new Dictionary<ZipEntry, ZipEntry>();
        private readonly Dictionary<string, List<ZipEntry>> groups = new Dictionary<string, List<ZipEntry>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<List<ZipEntry>> groupOrder = new List<List<ZipEntry>>();
        internal long DuplicateCount;
        internal long FileCount
        {
            get { long n = 0; foreach (ZipEntry e in Entries) if (!e.IsDirectory && e.Keep) n++; return n; }
        }
        /// <summary>物理出力先に依存せず、全 ZIP の安全性・仮想構造を検査する。入力順を保持する。</summary>
        internal static ExtractionPlan BuildVirtual(List<ZipArchiveData> archives)
        {
            ExtractionPlan plan = new ExtractionPlan();
            foreach (ZipArchiveData archive in archives)
                foreach (ZipEntry entry in archive.Entries)
                {
                    Program.CheckCancel();
                    if (entry.SpecialObject != null) throw new SafetyException("危険な ZIP オブジェクト: " + entry.SpecialObject + " / " + entry.Label);
                    // 採用名だけでなく、Unicode extra に隠された旧式名・ローカル名の遡りも拒否する。
                    entry.Relative = WindowsPaths.Relative(entry.Name, entry.IsDirectory);
                    WindowsPaths.Relative(entry.RawDecodedName, entry.IsDirectory);
                    WindowsPaths.Relative(entry.LocalDecodedName, entry.IsDirectory);
                    plan.AddPlannedEntry(entry);
                }
            plan.CheckVirtualConflicts();
            return plan;
        }

        /// <summary>検査済みの入力または写像済みコピーを登録する。群間上書きは呼出元が事前解決する。</summary>
        internal void AddPlannedEntry(ZipEntry entry)
        {
            Entries.Add(entry);
            if (entry.IsDirectory)
            {
                DirectoryPlan directory = GetDirectory(entry.Relative);
                if (directory != null && (directory.ExplicitTimes == null || !directory.ExplicitTimes.ModifiedUtc.HasValue))
                    directory.ExplicitTimes = entry.Times;
            }
            else
            {
                List<ZipEntry> group;
                if (!groups.TryGetValue(entry.Relative, out group))
                {
                    group = new List<ZipEntry>(); groups.Add(entry.Relative, group); groupOrder.Add(group);
                }
                group.Add(entry);
            }
            AddParents(entry.Relative);
        }

        /// <summary>明示・暗黙ディレクトリと同名ファイルの衝突を検査する。ディスクには触れない。</summary>
        internal void CheckVirtualConflicts()
        {
            foreach (KeyValuePair<string, List<ZipEntry>> pair in groups)
                if (Directories.ContainsKey(pair.Key))
                    throw new InvalidDataException("ZIP 内でファイルとディレクトリ（または親ディレクトリ）が衝突します: " + pair.Key
                        + " / " + pair.Value[0].Label);
        }

        /// <summary>最終的な写像先を物理検査する。差分・ベースライン双方の入力 ZIP を保護する。</summary>
        internal void CheckDestination(SafeRoot root, IList<SourceSet> inputs)
        {
            foreach (ZipEntry entry in Entries)
            {
                Program.CheckCancel();
                CheckDestinationEntry(entry, root, inputs);
                ZipEntry fallback;
                if (Fallbacks.TryGetValue(entry, out fallback)) CheckDestinationEntry(fallback, root, inputs);
            }
        }

        /// <summary>一つの出力エントリの種別・入力上書き・通常パス長を事前検査する。</summary>
        private static void CheckDestinationEntry(ZipEntry entry, SafeRoot root, IList<SourceSet> inputs)
        {
            try
            {
                FileStamp existing = root.Probe(entry.Relative);
                if (existing != null && entry.IsDirectory != existing.IsDirectory)
                    entry.PlanError = "展開先のファイル / ディレクトリ種別が衝突しています: " + root.Destination(entry.Relative);
                if (existing != null && !entry.IsDirectory)
                {
                    foreach (SourceSet input in inputs)
                    {
                        if (input.ContainsIdentity(existing.Identity))
                        {
                            entry.PlanError = "入力元 ZIP 自身への上書きは許可しません: " + root.Destination(entry.Relative);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (!Program.Recoverable(ex)) throw;
                entry.PlanError = "展開先の事前検査失敗: " + ex.Message;
            }
        }
        private DirectoryPlan GetDirectory(string relative)
        {
            if (relative.Length == 0) return null; // ユーザーが選択したルートの日時は変更しない。
            DirectoryPlan result;
            if (!Directories.TryGetValue(relative, out result))
            {
                result = new DirectoryPlan { Relative = relative }; Directories.Add(relative, result);
            }
            return result;
        }
        private void AddParents(string relative)
        {
            string current = WindowsPaths.Parent(relative);
            while (current.Length != 0) { GetDirectory(current); current = WindowsPaths.Parent(current); }
        }
        /// <summary>Y は以後すべての重複を許可、y は当該組だけ、n は全体中断。いずれも先勝ち。</summary>
        internal void ConfirmDuplicates()
        {
            bool allowAll = false;
            foreach (List<ZipEntry> group in groupOrder)
            {
                if (group.Count < 2) continue;
                if (!allowAll)
                {
                    List<string> labels = new List<string>();
                    foreach (ZipEntry entry in group) labels.Add(Text.Safe(entry.Label));
                    char choice = Text.Choice(String.Join(" と ", labels.ToArray()) + " の " + group.Count
                        + " つのファイルは重複しています。処理を継続しますか? (Y/y/n): ", "Yyn");
                    if (choice == 'n') throw new OperationCanceledException("ZIP 内の重複により中断しました。");
                    if (choice == 'Y') allowAll = true;
                }
                for (int i = 1; i < group.Count; i++) { group[i].Keep = false; DuplicateCount++; }
            }
        }
        /// <summary>重複除去後の採用エントリについて全子孫の最新日付を集計する。</summary>
        internal void BuildDirectoryTimes()
        {
            foreach (ZipEntry entry in Entries)
            {
                if (entry.IsDirectory || !entry.Keep || entry.PlanError != null || !entry.Times.ModifiedUtc.HasValue) continue;
                string relative = WindowsPaths.Parent(entry.Relative);
                while (relative.Length != 0)
                {
                    DirectoryPlan directory = Directories[relative];
                    DateTime value = entry.Times.ModifiedUtc.Value;
                    if (!directory.LatestFile.HasValue || directory.LatestFile.Value < value) directory.LatestFile = value;
                    relative = WindowsPaths.Parent(relative);
                }
            }
        }
    }

    /// <summary>重複解決後の ZIP エントリから作る仮想木。物理ファイルを作成せず、元のエントリも変更しない。</summary>
    internal sealed class DiffTree
    {
        internal readonly DiffNode Root;
        internal readonly List<DiffNode> Nodes = new List<DiffNode>();

        private DiffTree()
        {
            Root = new DiffNode("", null, true);
            Nodes.Add(Root);
        }

        /// <summary>正規化済み Relative を持つ有効エントリを取り込み、明示・暗黙ディレクトリを一意に数える。</summary>
        internal static DiffTree Build(IEnumerable<ZipEntry> entries)
        {
            DiffTree result = new DiffTree();
            foreach (ZipEntry entry in entries)
            {
                Program.CheckCancel();
                if (!entry.Keep || entry.PlanError != null) continue;
                if (entry.Relative == null) throw new InvalidOperationException("差分計算の前に ZIP 内の相対パスを正規化する必要があります。");
                if (entry.Relative.Length == 0)
                {
                    if (!entry.IsDirectory) throw new InvalidDataException("差分計算対象に空のファイルパスがあります。");
                    continue; // 仮想ルート自身は ZIP 内のディレクトリ個数に含めない。
                }
                string[] elements = entry.Relative.Split('\\');
                DiffNode parent = result.Root;
                for (int index = 0; index < elements.Length; index++)
                {
                    bool directory = index != elements.Length - 1 || entry.IsDirectory;
                    if (elements[index].Length == 0 || elements[index] == "." || elements[index] == "..")
                        throw new InvalidDataException("差分計算対象に未正規化のパスがあります: " + Text.Safe(entry.Relative));
                    DiffNode node;
                    if (!parent.Children.TryGetValue(elements[index], out node))
                    {
                        node = new DiffNode(elements[index], parent, directory);
                        parent.Children.Add(node.Name, node);
                        result.Nodes.Add(node);
                    }
                    else if (node.IsDirectory != directory)
                        throw new InvalidDataException("ZIP の仮想木でファイルとディレクトリが衝突しています: " + Text.Safe(entry.Relative));
                    else if (!directory)
                        throw new InvalidDataException("差分計算の前に解決されていないファイル重複があります: " + Text.Safe(entry.Relative));
                    if (!directory)
                    {
                        if (entry.Size < 0) throw new InvalidDataException("差分計算対象のファイルサイズが負です: " + Text.Safe(entry.Relative));
                        node.Files = 1;
                        node.Bytes = entry.Size;
                    }
                    parent = node;
                }
            }
            // 親は子より先に登録されるので、逆順一走査で全配下の集計が確定する。
            for (int index = result.Nodes.Count - 1; index > 0; index--)
            {
                DiffNode node = result.Nodes[index];
                DiffNode parent = node.Parent;
                parent.Files = checked(parent.Files + node.Files);
                parent.Directories = checked(parent.Directories + node.Directories + (node.IsDirectory ? 1L : 0L));
                parent.Bytes = checked(parent.Bytes + node.Bytes);
                if ((index & 1023) == 0) Program.CheckCancel();
            }
            return result;
        }
    }

    /// <summary>仮想木の一要素。Files/Directories/Bytes は配下の集計であり、ディレクトリ自身を含めない。</summary>
    internal sealed class DiffNode
    {
        internal readonly string Name;
        internal readonly DiffNode Parent;
        internal readonly bool IsDirectory;
        internal readonly int Depth;
        internal readonly Dictionary<string, DiffNode> Children;
        internal long Files, Directories;
        internal decimal Bytes;
        internal int Token;
        internal DiffShape Shape;

        internal DiffNode(string name, DiffNode parent, bool directory)
        {
            Name = name; Parent = parent; IsDirectory = directory;
            Depth = parent == null ? 0 : checked(parent.Depth + 1);
            if (directory) Children = new Dictionary<string, DiffNode>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>表示・最終マッピング時だけ相対パスを復元し、暗黙の全親パスを重複保持する費用を避ける。</summary>
        internal string Relative
        {
            get
            {
                if (Parent == null) return "";
                string[] elements = new string[Depth];
                DiffNode node = this;
                for (int index = elements.Length - 1; index >= 0; index--) { elements[index] = node.Name; node = node.Parent; }
                return String.Join("\\", elements);
            }
        }
    }

    /// <summary>確定した双方の基準パス、元の木の配下集計、および同一相対パスの一致個数。</summary>
    internal sealed class DiffMatch
    {
        internal string BaselineRelative, DifferentialRelative;
        internal long BaselineFiles, BaselineDirectories, DifferentialFiles, DifferentialDirectories;
        internal decimal BaselineBytes, DifferentialBytes;
        internal long MatchedFiles, MatchedDirectories;
        internal long ComparedPairs;
    }

    /// <summary>比較用部分木の完全同値クラス。名前・種別・子の構造を照合し、ハッシュ値だけで同一視しない。</summary>
    internal sealed class DiffShape
    {
        internal int Id;
        internal DiffEdge[] Edges;
        internal long Files, Directories;
        internal DiffRepresentatives Baseline, Differential;
        internal int[] Features;
        internal long Count { get { return Files + Directories; } }
    }

    /// <summary>比較用部分木の一辺。Token の最下位ビットはディレクトリ種別、それ以外は名前の識別子。</summary>
    internal struct DiffEdge
    {
        internal int Token;
        internal DiffShape Child;
        internal bool IsDirectory { get { return (Token & 1) != 0; } }
    }

    /// <summary>同じ一致集合を持つ基準候補のうち最小深さの実在ディレクトリ。曖昧さを落とさず少数例を保持する。</summary>
    internal sealed class DiffRepresentatives
    {
        internal DiffNode First;
        internal long Count;
        internal readonly List<DiffNode> Examples = new List<DiffNode>();

        internal void Add(DiffNode node)
        {
            if (First == null || node.Depth < First.Depth)
            {
                First = node; Count = 0; Examples.Clear();
            }
            if (node.Depth != First.Depth) return;
            Count++;
            if (Examples.Count < 3) Examples.Add(node);
        }

        /// <summary>互いに重ならない構造クラスを統合し、最小深さにある候補総数と代表例を引き継ぐ。</summary>
        internal void Merge(DiffRepresentatives other)
        {
            if (other == null) return;
            if (First == null || other.First.Depth < First.Depth)
            {
                First = other.First; Count = 0; Examples.Clear();
            }
            if (other.First.Depth != First.Depth) return;
            Count = checked(Count + other.Count);
            foreach (DiffNode node in other.Examples) if (Examples.Count < 3) Examples.Add(node);
        }
    }

    /// <summary>全候補対を無条件に列挙せず、完全同値圧縮と逆引き prefix filter で最大の一致数を厳密に求める。</summary>
    internal sealed class DiffMatcher
    {
        private readonly DiffTree baseline, differential;
        private readonly Dictionary<string, int> names = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, int> tokenSides = new Dictionary<int, int>();
        private readonly Dictionary<ulong, List<DiffShape>> shapeBuckets = new Dictionary<ulong, List<DiffShape>>();
        private readonly List<DiffShape> shapes = new List<DiffShape>();
        private readonly Dictionary<long, int> pathIds = new Dictionary<long, int>();
        private readonly List<bool> pathDirectories = new List<bool>();
        private readonly List<DiffSet> sets = new List<DiffSet>();
        private bool[] directoryByRank;
        private long best, bestDepth = Int64.MaxValue, ties, compared;
        private DiffRepresentatives chosenBaseline, chosenDifferential;
        private long chosenFiles, chosenDirectories;
        private readonly List<string> tieExamples = new List<string>();
        private int ticks;

        private sealed class DiffSet
        {
            internal int[] Features;
            internal DiffRepresentatives Baseline, Differential;
        }

        private sealed class Anchor
        {
            internal long BaselineCount, DifferentialCount;
            internal readonly List<DiffNode> Baseline = new List<DiffNode>();
            internal readonly List<DiffNode> Differential = new List<DiffNode>();
        }

        private struct ShapePair { internal DiffShape Left, Right; }
        private struct FeatureWalk { internal DiffShape Shape; internal int Prefix; }

        private DiffMatcher(DiffTree baseTree, DiffTree diffTree)
        {
            baseline = baseTree; differential = diffTree;
            pathDirectories.Add(false); // 相対パス ID=0 は名前を持たない仮想ルート。
        }

        /// <summary>最大 c、次に最小の深さ和で基準を選び、同順位の複数解・一致なし・半数未満を詳細例外にする。</summary>
        internal static DiffMatch Find(DiffTree baseline, DiffTree differential)
        {
            if (baseline == null || differential == null) throw new ArgumentNullException("差分計算対象の仮想木");
            return new DiffMatcher(baseline, differential).Run();
        }

        private DiffMatch Run()
        {
            RegisterTokens(baseline, 1);
            RegisterTokens(differential, 2);
            BuildShapes(baseline, true);
            BuildShapes(differential, false);

            // 安全な下限を先に得る。ここで調べる候補数は固定上限で、後続の厳密探索を省略しない。
            foreach (DiffShape shape in shapes)
                if (shape.Baseline != null && shape.Differential != null && shape.Count > best) best = shape.Count;
            Seed(baseline.Root.Shape, differential.Root.Shape);
            SeedAnchors();
            BuildSets();
            SearchSets();
            if (chosenBaseline == null)
                throw new InvalidDataException("差分 ZIP の基準ディレクトリを確定できません。同一の正規化相対パス・種別を持つファイル／ディレクトリの一致がありません。\n"
                    + "ベースライン全体: ファイル " + baseline.Root.Files + " 個、ディレクトリ " + baseline.Root.Directories + " 個。\n"
                    + "差分全体: ファイル " + differential.Root.Files + " 個、ディレクトリ " + differential.Root.Directories + " 個。");
            if (ties != 1)
                throw new InvalidDataException("差分 ZIP の基準ディレクトリが曖昧です。最大一致数 c=" + best
                    + "、最小深さ和=" + bestDepth + " となる基準の組が " + ties + " 組あります。\n"
                    + String.Join("\n", tieExamples.ToArray())
                    + "\n相対パスの対応を一意に決められないため、自動で一つを選んで展開することはしません。");

            DiffNode baseNode = chosenBaseline.First, diffNode = chosenDifferential.First;
            // 種別ごとに半数以上を要求する。総数 0 の種別には制限を掛けず、基準自身・仮想ルートは数えない。
            bool tooFewFiles = baseline.Root.Files > 0 && baseNode.Files < (baseline.Root.Files + 1) / 2;
            bool tooFewDirectories = baseline.Root.Directories > 0 && baseNode.Directories < (baseline.Root.Directories + 1) / 2;
            if (tooFewFiles || tooFewDirectories)
                throw new InvalidDataException("差分 ZIP の基準ディレクトリの自動解決に失敗している可能性があります。\n"
                    + "ベースライン基準: " + Display(baseNode) + "、差分基準: " + Display(diffNode) + "。\n"
                    + "基準配下／ベースライン全体: ファイル " + baseNode.Files + " / " + baseline.Root.Files
                    + "、ディレクトリ " + baseNode.Directories + " / " + baseline.Root.Directories + "。\n"
                    + "ファイル数・ディレクトリ数の各々に半数以上を要求します。半数未満: "
                    + (tooFewFiles ? "ファイル " : "") + (tooFewDirectories ? "ディレクトリ" : "")
                    + "。基準自身と仮想ルートは配下ディレクトリ数に含めません。\n"
                    + "最大一致: ファイル " + chosenFiles + " 個、ディレクトリ " + chosenDirectories + " 個。");
            DiffMatch result = new DiffMatch();
            result.BaselineRelative = baseNode.Relative; result.DifferentialRelative = diffNode.Relative;
            result.BaselineFiles = baseNode.Files; result.BaselineDirectories = baseNode.Directories; result.BaselineBytes = baseNode.Bytes;
            result.DifferentialFiles = diffNode.Files; result.DifferentialDirectories = diffNode.Directories; result.DifferentialBytes = diffNode.Bytes;
            result.MatchedFiles = chosenFiles; result.MatchedDirectories = chosenDirectories; result.ComparedPairs = compared;
            return result;
        }

        /// <summary>同じ Windows 名を同一整数にし、ファイルとディレクトリは別トークンにする。</summary>
        private void RegisterTokens(DiffTree tree, int side)
        {
            foreach (DiffNode node in tree.Nodes)
            {
                Poll();
                if (node.Parent == null) continue;
                int name;
                if (!names.TryGetValue(node.Name, out name)) { name = checked(names.Count + 1); names.Add(node.Name, name); }
                node.Token = checked(name * 2 + (node.IsDirectory ? 1 : 0));
                int flags;
                tokenSides.TryGetValue(node.Token, out flags);
                tokenSides[node.Token] = flags | side;
            }
        }

        /// <summary>候補根はすべて残す。一致相手の存在しない辺だけを除去し、下から完全同値の部分木を共有する。</summary>
        private void BuildShapes(DiffTree tree, bool isBaseline)
        {
            for (int index = tree.Nodes.Count - 1; index >= 0; index--)
            {
                Poll();
                DiffNode node = tree.Nodes[index];
                if (!node.IsDirectory) continue;
                List<DiffEdge> edges = new List<DiffEdge>();
                foreach (DiffNode child in node.Children.Values)
                {
                    if (tokenSides[child.Token] != 3) continue;
                    DiffEdge edge = new DiffEdge(); edge.Token = child.Token;
                    if (child.IsDirectory) edge.Child = child.Shape;
                    edges.Add(edge);
                }
                edges.Sort(delegate(DiffEdge first, DiffEdge second) { return first.Token.CompareTo(second.Token); });
                ulong hash = 14695981039346656037UL;
                foreach (DiffEdge edge in edges)
                {
                    hash = Mix(hash, edge.Token);
                    hash = Mix(hash, edge.Child == null ? -1 : edge.Child.Id);
                }
                List<DiffShape> bucket;
                DiffShape shape = null;
                if (shapeBuckets.TryGetValue(hash, out bucket))
                    foreach (DiffShape candidate in bucket) if (EqualEdges(candidate.Edges, edges)) { shape = candidate; break; }
                if (shape == null)
                {
                    shape = new DiffShape(); shape.Id = shapes.Count; shape.Edges = edges.ToArray();
                    foreach (DiffEdge edge in shape.Edges)
                    {
                        if (edge.IsDirectory) { shape.Files += edge.Child.Files; shape.Directories += edge.Child.Directories + 1; }
                        else shape.Files++;
                    }
                    if (bucket == null) { bucket = new List<DiffShape>(); shapeBuckets.Add(hash, bucket); }
                    bucket.Add(shape); shapes.Add(shape);
                }
                node.Shape = shape;
                DiffRepresentatives representatives = isBaseline ? shape.Baseline : shape.Differential;
                if (representatives == null)
                {
                    representatives = new DiffRepresentatives();
                    if (isBaseline) shape.Baseline = representatives; else shape.Differential = representatives;
                }
                representatives.Add(node);
            }
        }

        private static bool EqualEdges(DiffEdge[] first, List<DiffEdge> second)
        {
            if (first.Length != second.Count) return false;
            for (int index = 0; index < first.Length; index++)
                if (first[index].Token != second[index].Token || first[index].Child != second[index].Child) return false;
            return true;
        }

        private static ulong Mix(ulong hash, int value)
        {
            return unchecked((hash ^ (uint)value) * 1099511628211UL);
        }

        /// <summary>少数の希少な同名ファイルから、相対パスが一致する最も浅い祖先対を初期下限として調べる。</summary>
        private void SeedAnchors()
        {
            Dictionary<int, Anchor> anchors = new Dictionary<int, Anchor>();
            AddAnchors(baseline, true, anchors); AddAnchors(differential, false, anchors);
            List<Anchor> usable = new List<Anchor>();
            foreach (Anchor anchor in anchors.Values)
                if (anchor.BaselineCount > 0 && anchor.DifferentialCount > 0 && anchor.BaselineCount <= 8 && anchor.DifferentialCount <= 8)
                    usable.Add(anchor);
            usable.Sort(delegate(Anchor first, Anchor second)
            {
                return (first.BaselineCount * first.DifferentialCount).CompareTo(second.BaselineCount * second.DifferentialCount);
            });
            int remaining = 64;
            HashSet<long> seen = new HashSet<long>();
            foreach (Anchor anchor in usable)
                foreach (DiffNode first in anchor.Baseline)
                    foreach (DiffNode second in anchor.Differential)
                    {
                        if (remaining-- == 0) return;
                        DiffNode left = first.Parent, right = second.Parent;
                        while (left.Parent != null && right.Parent != null && left.Token == right.Token)
                        {
                            left = left.Parent; right = right.Parent; Poll();
                        }
                        long pair = ((long)left.Shape.Id << 32) | (uint)right.Shape.Id;
                        if (seen.Add(pair)) Seed(left.Shape, right.Shape);
                    }
        }

        private void AddAnchors(DiffTree tree, bool isBaseline, Dictionary<int, Anchor> anchors)
        {
            foreach (DiffNode node in tree.Nodes)
            {
                Poll();
                if (node.IsDirectory || tokenSides[node.Token] != 3) continue;
                Anchor anchor;
                if (!anchors.TryGetValue(node.Token, out anchor)) { anchor = new Anchor(); anchors.Add(node.Token, anchor); }
                if (isBaseline) { anchor.BaselineCount++; if (anchor.Baseline.Count < 8) anchor.Baseline.Add(node); }
                else { anchor.DifferentialCount++; if (anchor.Differential.Count < 8) anchor.Differential.Add(node); }
            }
        }

        /// <summary>初期候補の一致数を木の同名辺に沿って厳密に計算する。再帰呼出しと全候補対の列挙は行わない。</summary>
        private void Seed(DiffShape left, DiffShape right)
        {
            if (Math.Min(left.Count, right.Count) <= best) return;
            Stack<ShapePair> work = new Stack<ShapePair>();
            ShapePair initial = new ShapePair(); initial.Left = left; initial.Right = right; work.Push(initial);
            long count = 0;
            while (work.Count != 0)
            {
                Poll();
                ShapePair pair = work.Pop();
                if (pair.Left == pair.Right) { count += pair.Left.Count; continue; }
                int first = 0, second = 0;
                while (first < pair.Left.Edges.Length && second < pair.Right.Edges.Length)
                {
                    Poll();
                    DiffEdge a = pair.Left.Edges[first], b = pair.Right.Edges[second];
                    if (a.Token < b.Token) { first++; continue; }
                    if (a.Token > b.Token) { second++; continue; }
                    count++;
                    if (a.IsDirectory && a.Child.Count != 0 && b.Child.Count != 0)
                    {
                        ShapePair child = new ShapePair(); child.Left = a.Child; child.Right = b.Child; work.Push(child);
                    }
                    first++; second++;
                }
            }
            if (count > best) best = count;
        }

        /// <summary>比較対象の各相対パスを共有整数 ID にする。候補数ではなく、実際に保持する相対パス総数に比例する前処理。</summary>
        private void BuildFeatures(DiffShape shape)
        {
            if (shape.Features != null) return;
            int[] features = new int[checked((int)shape.Count)];
            int offset = 0;
            Stack<FeatureWalk> work = new Stack<FeatureWalk>();
            FeatureWalk initial = new FeatureWalk(); initial.Shape = shape; initial.Prefix = 0; work.Push(initial);
            while (work.Count != 0)
            {
                FeatureWalk item = work.Pop();
                foreach (DiffEdge edge in item.Shape.Edges)
                {
                    Poll();
                    long key = ((long)item.Prefix << 32) | (uint)edge.Token;
                    int id;
                    if (!pathIds.TryGetValue(key, out id))
                    {
                        id = pathDirectories.Count; pathIds.Add(key, id); pathDirectories.Add(edge.IsDirectory);
                    }
                    features[offset++] = id;
                    if (edge.IsDirectory && edge.Child.Count != 0)
                    {
                        FeatureWalk child = new FeatureWalk(); child.Shape = edge.Child; child.Prefix = id; work.Push(child);
                    }
                }
            }
            if (offset != features.Length) throw new InvalidOperationException("比較用仮想木の相対パス集計に不整合があります。");
            Array.Sort(features);
            shape.Features = features;
        }

        /// <summary>片側にしか存在しない相対パスを除き、完全に同じ一致集合をもう一段共有する。</summary>
        private void BuildSets()
        {
            long required = Math.Max(1L, best);
            foreach (DiffShape shape in shapes) if (shape.Count >= required) BuildFeatures(shape);
            int[] sides = new int[pathDirectories.Count];
            foreach (DiffShape shape in shapes)
            {
                if (shape.Features == null) continue;
                int side = (shape.Baseline == null ? 0 : 1) | (shape.Differential == null ? 0 : 2);
                foreach (int feature in shape.Features) { sides[feature] |= side; Poll(); }
            }
            Dictionary<ulong, List<DiffSet>> buckets = new Dictionary<ulong, List<DiffSet>>();
            foreach (DiffShape shape in shapes)
            {
                Poll();
                if (shape.Features == null) continue;
                int count = 0;
                foreach (int feature in shape.Features) if (sides[feature] == 3) count++;
                if (count < required) { shape.Features = null; continue; }
                int[] features = new int[count];
                int offset = 0;
                ulong hash = 14695981039346656037UL;
                foreach (int feature in shape.Features)
                    if (sides[feature] == 3) { features[offset++] = feature; hash = Mix(hash, feature); }
                shape.Features = null;
                List<DiffSet> bucket;
                DiffSet set = null;
                if (buckets.TryGetValue(hash, out bucket))
                    foreach (DiffSet candidate in bucket) if (EqualFeatures(candidate.Features, features)) { set = candidate; break; }
                if (set == null)
                {
                    set = new DiffSet(); set.Features = features; sets.Add(set);
                    if (bucket == null) { bucket = new List<DiffSet>(); buckets.Add(hash, bucket); }
                    bucket.Add(set);
                }
                if (shape.Baseline != null)
                {
                    if (set.Baseline == null) set.Baseline = new DiffRepresentatives();
                    set.Baseline.Merge(shape.Baseline);
                }
                if (shape.Differential != null)
                {
                    if (set.Differential == null) set.Differential = new DiffRepresentatives();
                    set.Differential.Merge(shape.Differential);
                }
            }
            foreach (DiffSet set in sets)
                if (set.Baseline != null && set.Differential != null && set.Features.Length > best) best = set.Features.Length;
            // これ以降は整数集合だけで計算できるので索引の登録要素を消去する。
            // Dictionaryの確保済み容量そのものはClearでは解放されない。
            shapeBuckets.Clear(); pathIds.Clear();
        }

        private static bool EqualFeatures(int[] first, int[] second)
        {
            if (first.Length != second.Length) return false;
            for (int index = 0; index < first.Length; index++) if (first[index] != second[index]) return false;
            return true;
        }

        /// <summary>希少順の先頭 m-k+1 要素から逆引きする。一致数 k 以上の組は必ずこの範囲の一要素を共有する。</summary>
        private void SearchSets()
        {
            int[] baseFrequency = new int[pathDirectories.Count], diffFrequency = new int[pathDirectories.Count];
            List<DiffSet> baseSets = new List<DiffSet>(), diffSets = new List<DiffSet>();
            long required = Math.Max(1L, best);
            foreach (DiffSet set in sets)
            {
                if (set.Features.Length < required) continue;
                if (set.Baseline != null) { baseSets.Add(set); foreach (int feature in set.Features) baseFrequency[feature]++; }
                if (set.Differential != null) { diffSets.Add(set); foreach (int feature in set.Features) diffFrequency[feature]++; }
            }
            List<int> order = new List<int>();
            for (int feature = 1; feature < pathDirectories.Count; feature++)
                if (baseFrequency[feature] != 0 || diffFrequency[feature] != 0) order.Add(feature);
            order.Sort(delegate(int first, int second)
            {
                int comparedFrequency = diffFrequency[first].CompareTo(diffFrequency[second]);
                if (comparedFrequency != 0) return comparedFrequency;
                comparedFrequency = baseFrequency[first].CompareTo(baseFrequency[second]);
                return comparedFrequency != 0 ? comparedFrequency : first.CompareTo(second);
            });
            int[] rank = new int[pathDirectories.Count];
            directoryByRank = new bool[order.Count];
            for (int index = 0; index < order.Count; index++) { rank[order[index]] = index; directoryByRank[index] = pathDirectories[order[index]]; }
            foreach (DiffSet set in sets)
            {
                if (set.Features.Length < required) continue;
                for (int index = 0; index < set.Features.Length; index++) set.Features[index] = rank[set.Features[index]];
                Array.Sort(set.Features); Poll();
            }
            baseSets.Sort(delegate(DiffSet first, DiffSet second)
            {
                int orderBySize = second.Features.Length.CompareTo(first.Features.Length);
                return orderBySize != 0 ? orderBySize : first.Baseline.First.Depth.CompareTo(second.Baseline.First.Depth);
            });
            diffSets.Sort(delegate(DiffSet first, DiffSet second)
            {
                int orderBySize = second.Features.Length.CompareTo(first.Features.Length);
                return orderBySize != 0 ? orderBySize : first.Differential.First.Depth.CompareTo(second.Differential.First.Depth);
            });
            List<int>[] postings = new List<int>[order.Count];
            for (int index = 0; index < diffSets.Count; index++)
                foreach (int feature in diffSets[index].Features)
                {
                    if (postings[feature] == null) postings[feature] = new List<int>();
                    postings[feature].Add(index); Poll();
                }
            int[] seen = new int[diffSets.Count];
            for (int row = 0; row < baseSets.Count; row++)
            {
                Poll();
                DiffSet first = baseSets[row];
                long target = Math.Max(1L, best);
                if (first.Features.Length < target) break;
                // 行内で best が増えても、この開始時の prefix は必要な候補の上位集合である。
                int prefixLength = checked(first.Features.Length - (int)target + 1);
                for (int prefix = 0; prefix < prefixLength; prefix++)
                {
                    List<int> posting = postings[first.Features[prefix]];
                    if (posting == null) continue;
                    foreach (int column in posting)
                    {
                        Poll();
                        DiffSet second = diffSets[column];
                        if (second.Features.Length < Math.Max(1L, best)) break;
                        if (seen[column] == row + 1) continue;
                        seen[column] = row + 1;
                        long upper = Math.Min(first.Features.Length, second.Features.Length);
                        long depth = (long)first.Baseline.First.Depth + second.Differential.First.Depth;
                        if (upper < best || (upper == best && depth > bestDepth)) continue;
                        long files, directories;
                        compared++;
                        if (Intersect(first.Features, second.Features, out files, out directories))
                            Consider(first.Baseline, second.Differential, files, directories);
                    }
                }
            }
        }

        /// <summary>整数集合の共通要素を厳密に数える。大きさの差が大きい組は小集合側から二分探索する。</summary>
        private bool Intersect(int[] first, int[] second, out long files, out long directories)
        {
            files = directories = 0;
            long required = Math.Max(1L, best);
            int[] smaller = first.Length <= second.Length ? first : second;
            int[] larger = first.Length <= second.Length ? second : first;
            if ((long)smaller.Length * 8 < larger.Length)
            {
                for (int index = 0; index < smaller.Length; index++)
                {
                    Poll();
                    if (files + directories + smaller.Length - index < required) return false;
                    int feature = smaller[index];
                    if (Array.BinarySearch(larger, feature) >= 0)
                    {
                        if (directoryByRank[feature]) directories++; else files++;
                    }
                }
            }
            else
            {
                int left = 0, right = 0;
                while (left < first.Length && right < second.Length)
                {
                    Poll();
                    if (files + directories + Math.Min(first.Length - left, second.Length - right) < required) return false;
                    int a = first[left], b = second[right];
                    if (a < b) { left++; continue; }
                    if (a > b) { right++; continue; }
                    if (directoryByRank[a]) directories++; else files++;
                    left++; right++;
                }
            }
            return files + directories >= required;
        }

        /// <summary>候補根名やファイル内容は採点せず、共通パス数、深さ和の順に比較する。最後の同点を保持する。</summary>
        private void Consider(DiffRepresentatives first, DiffRepresentatives second, long files, long directories)
        {
            long score = files + directories, depth = (long)first.First.Depth + second.First.Depth;
            if (score < best || (score == best && depth > bestDepth)) return;
            if (chosenBaseline == null || score > best || depth < bestDepth)
            {
                best = score; bestDepth = depth; ties = 0; tieExamples.Clear();
                chosenBaseline = first; chosenDifferential = second; chosenFiles = files; chosenDirectories = directories;
            }
            ties = checked(ties + checked(first.Count * second.Count));
            foreach (DiffNode a in first.Examples)
                foreach (DiffNode b in second.Examples)
                    if (tieExamples.Count < 8)
                        tieExamples.Add("  ベースライン " + Display(a) + " ↔ 差分 " + Display(b)
                            + "（ファイル " + files + "、ディレクトリ " + directories + "）");
        }

        private void Poll() { if ((++ticks & 1023) == 0) Program.CheckCancel(); }
        private static string Display(DiffNode node) { string path = node.Relative; return path.Length == 0 ? "/" : "'" + Text.Safe(path.Replace('\\', '/')) + "/'"; }
    }

    /// <summary>独立した二群を差分側のルートへ写像し、差分優先の最終出力計画を作る。</summary>
    internal sealed class DifferentialPlan
    {
        internal readonly ExtractionPlan Output = new ExtractionPlan();
        internal DiffMatch Match;
        internal string MiscRelative;
        internal long OverwrittenCount;
        internal decimal OverwrittenBytes;

        /// <summary>
        /// ベースライン基準配下は差分基準へ、それ以外は日時付き保管先へ写す。
        /// 引数の仮想計画を変更せず、元 ZIP メタデータを共有する出力用コピーを作る。
        /// </summary>
        internal static DifferentialPlan Build(ExtractionPlan baseline, ExtractionPlan differential, DiffMatch match, string timestamp)
        {
            DifferentialPlan result = new DifferentialPlan();
            result.Match = match;
            result.MiscRelative = "_base_misc_files\\" + timestamp;
            result.Output.DuplicateCount = checked(baseline.DuplicateCount + differential.DuplicateCount);
            Dictionary<string, ZipEntry> deltaFiles = new Dictionary<string, ZipEntry>(StringComparer.OrdinalIgnoreCase);
            List<ZipEntry> deltaEntries = new List<ZipEntry>();
            foreach (ZipEntry entry in differential.Entries)
            {
                Program.CheckCancel();
                if (!entry.Keep) continue;
                ZipEntry copy = entry.AtOutput(entry.Relative);
                copy.InputGroupLabel = "差分 ZIP 群";
                deltaEntries.Add(copy);
                if (!copy.IsDirectory) deltaFiles.Add(copy.Relative, copy);
            }

            bool hasMisc = false;
            List<ZipEntry> mappedInside = new List<ZipEntry>();
            Dictionary<string, ZipEntry> mappedFiles = new Dictionary<string, ZipEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (ZipEntry entry in baseline.Entries)
            {
                Program.CheckCancel();
                if (!entry.Keep) continue;
                bool inside = IsWithin(entry.Relative, match.BaselineRelative);
                string destination;
                if (inside)
                {
                    string suffix = Below(entry.Relative, match.BaselineRelative);
                    destination = Combine(match.DifferentialRelative, suffix);
                }
                else
                {
                    hasMisc = true;
                    destination = Combine(result.MiscRelative, entry.Relative);
                }
                ZipEntry copy = entry.AtOutput(destination);
                copy.InputGroupLabel = "ベースライン ZIP 群";
                if (inside) mappedInside.Add(copy);
                if (!copy.IsDirectory)
                {
                    ZipEntry previous;
                    if (mappedFiles.TryGetValue(copy.Relative, out previous))
                        throw new InvalidDataException("ベースラインの写像先が重複します: " + copy.Relative
                            + "\n  " + previous.Label + "\n  " + copy.Label);
                    mappedFiles.Add(copy.Relative, copy);
                    ZipEntry delta;
                    if (inside && deltaFiles.TryGetValue(copy.Relative, out delta))
                    {
                        // 差分成功時にはベースラインの展開・CRC読取そのものを省く。
                        // 差分が個別失敗/ignoreになった場合だけ Extractor がベースラインを救済する。
                        result.Output.Fallbacks.Add(delta, copy);
                        result.OverwrittenCount++;
                        result.OverwrittenBytes += copy.Size;
                        continue;
                    }
                }
                result.Output.AddPlannedEntry(copy);
            }

            if (hasMisc)
            {
                // 保存用の当該日時の領域だけを予約する。別の日時の既存構造は排除しない。
                foreach (ZipEntry entry in deltaEntries) CheckReserved(entry, result.MiscRelative);
                foreach (ZipEntry entry in mappedInside) CheckReserved(entry, result.MiscRelative);
            }
            foreach (ZipEntry entry in deltaEntries) result.Output.AddPlannedEntry(entry);
            result.Output.CheckVirtualConflicts();

            // 明示ディレクトリ日時も差分側を優先する。群内では既存どおり最初の有効値。
            foreach (KeyValuePair<string, DirectoryPlan> pair in differential.Directories)
            {
                DirectoryPlan destination;
                if (pair.Value.ExplicitTimes != null && pair.Value.ExplicitTimes.ModifiedUtc.HasValue
                    && result.Output.Directories.TryGetValue(pair.Key, out destination))
                    destination.ExplicitTimes = pair.Value.ExplicitTimes;
            }
            return result;
        }

        /// <summary>保管領域への混入、および保管領域の祖先をファイルにする衝突を拒否する。</summary>
        private static void CheckReserved(ZipEntry entry, string reserved)
        {
            if (IsWithin(entry.Relative, reserved) || (!entry.IsDirectory && IsWithin(reserved, entry.Relative)))
                throw new InvalidDataException("ベースライン外側ファイルの保管先が元の内容物と衝突します: " + reserved
                    + "\n  写像先: " + entry.Relative + "\n  元エントリ: " + entry.Label);
        }

        /// <summary>baseRelative 自身または区切り境界を含む配下か。空文字は ZIP の仮想ルート。</summary>
        internal static bool IsWithin(string relative, string baseRelative)
        {
            return baseRelative.Length == 0 || String.Equals(relative, baseRelative, StringComparison.OrdinalIgnoreCase)
                || relative.StartsWith(baseRelative + "\\", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>既に配下と確認済みのパスを基準からの相対値にする。</summary>
        private static string Below(string relative, string baseRelative)
        {
            if (baseRelative.Length == 0) return relative;
            return relative.Length == baseRelative.Length ? "" : relative.Substring(baseRelative.Length + 1);
        }

        /// <summary>正規化済み相対パスを、空のルートも許容して結合する。</summary>
        private static string Combine(string parent, string child)
        {
            if (parent.Length == 0) return child;
            return child.Length == 0 ? parent : parent + "\\" + child;
        }

        /// <summary>
        /// 最初の物理入力名にある最後の有効な yyMMdd_HHmmss を使用する。
        /// 2桁年の暦検証は2000～2099年と定義し、仕様どおり名前の全位置を走査する。
        /// 見つからない場合は同じ物理入力のローカル更新日時を使用する。
        /// </summary>
        internal static string GetBaselineTimestamp(string firstSource)
        {
            string found = TimestampInName(Path.GetFileName(firstSource));
            return found ?? File.GetLastWriteTime(firstSource).ToString("yyMMdd_HHmmss", CultureInfo.InvariantCulture);
        }

        /// <summary>名前だけから日時を抽出する。妥当な候補がなければ null。読み書きはしない。</summary>
        internal static string TimestampInName(string name)
        {
            string answer = null;
            for (int offset = 0; offset <= name.Length - 13; offset++)
            {
                if (name[offset + 6] != '_') continue;
                bool valid = true;
                for (int index = 0; index < 13; index++)
                    if (index != 6 && !IsDigit(name[offset + index])) { valid = false; break; }
                if (!valid) continue;
                try
                {
                    // OSのCalendar.TwoDigitYearMax設定による世紀の変動を避ける。
                    new DateTime(2000 + TwoDigits(name, offset), TwoDigits(name, offset + 2), TwoDigits(name, offset + 4),
                        TwoDigits(name, offset + 7), TwoDigits(name, offset + 9), TwoDigits(name, offset + 11));
                    answer = name.Substring(offset, 13);
                }
                catch (ArgumentOutOfRangeException) { }
            }
            return answer;
        }
        private static bool IsDigit(char value) { return value >= '0' && value <= '9'; }
        private static int TwoDigits(string value, int offset) { return (value[offset] - '0') * 10 + value[offset + 1] - '0'; }

        /// <summary>基準・一致件数・ベースライン省略予定量を、計算時と展開終了時に同じ内容で表示する。</summary>
        internal void PrintSummary()
        {
            Console.WriteLine("ベースライン ZIP 群の基準ディレクトリ: {0} ({1:N0} 個のファイル (合計 {2:N0} bytes)、{3:N0} 個のディレクトリが存在)",
                DisplayDirectory(Match.BaselineRelative), Match.BaselineFiles, Match.BaselineBytes, Match.BaselineDirectories);
            Console.WriteLine("差分 ZIP 群の基準ディレクトリ: {0} ({1:N0} 個のファイル (合計 {2:N0} bytes)、{3:N0} 個のディレクトリが存在)",
                DisplayDirectory(Match.DifferentialRelative), Match.DifferentialFiles, Match.DifferentialBytes, Match.DifferentialDirectories);
            Console.WriteLine("一致したファイル: {0:N0} 個、一致したディレクトリ: {1:N0} 個", Match.MatchedFiles, Match.MatchedDirectories);
            Console.WriteLine("差分 ZIP によりベースライン ZIP が上書きされることとなるファイル個数: {0:N0} 個 (合計 {1:N0} bytes)",
                OverwrittenCount, OverwrittenBytes);
        }
        private static string DisplayDirectory(string relative) { return relative.Length == 0 ? "/" : Text.Safe(relative.Replace('\\', '/') + "/"); }
    }

    /// <summary>事前確認と展開時確認を分離し、確認した実体と異なる場合は再確認する。</summary>
    internal sealed class OverwritePolicy
    {
        private readonly ExtractionPlan plan;
        private readonly SafeRoot root;
        private readonly Dictionary<ZipEntry, FileStamp> anticipated = new Dictionary<ZipEntry, FileStamp>();
        private readonly Dictionary<ZipEntry, Decision> decisions = new Dictionary<ZipEntry, Decision>();
        private bool anticipatedOverwrite;
        private int laterMode; // 0=個別確認、1=予期しない上書きを許可、2=予期しない上書きを無視
        private sealed class Decision { internal FileStamp Stamp; internal bool Allow; }
        internal OverwritePolicy(ExtractionPlan value, SafeRoot destination, WarningBook warnings) { plan = value; root = destination; }
        internal void Preflight()
        {
            List<string> names = new List<string>();
            foreach (ZipEntry entry in plan.Entries)
            {
                if (entry.IsDirectory || !entry.Keep || entry.PlanError != null) continue;
                Program.CheckCancel();
                try
                {
                    FileStamp existing = root.Probe(entry.Relative);
                    if (existing == null) continue;
                    if (existing.IsDirectory) { entry.PlanError = "ファイルの展開先にディレクトリがあります。"; continue; }
                    anticipated.Add(entry, existing);
                    names.Add(root.Destination(entry.Relative));
                }
                catch (Exception ex)
                {
                    if (!Program.Recoverable(ex)) throw;
                    entry.PlanError = "上書きの事前検査失敗: " + ex.Message;
                }
            }
            if (names.Count == 0) return;
            while (true)
            {
                char answer = Text.Choice("【事前チェック】 '" + Text.Safe(names[0]) + "' という物理ファイル等を代表として、合計 "
                    + names.Count + " 個のファイルの上書きが発生する予定です。どうしますか (y: すべて上書きをする, n: 上書きせず無視する, a: 対象となる可能性があるファイル一覧を表示する, q: 何もせず終了する) ?: ", "ynaq");
                if (answer == 'q') throw new OperationCanceledException("上書き事前確認で終了が選択されました。");
                if (answer == 'a') { foreach (string path in names) Console.WriteLine("  " + Text.Safe(path)); continue; }
                anticipatedOverwrite = answer == 'y';
                return;
            }
        }
        /// <summary>current=null は新規保存。確認済み対象と違う場合は指定どおり再確認する。</summary>
        internal bool Allow(ZipEntry entry, FileStamp current)
        {
            if (current == null) return true;
            if (current.IsDirectory) throw new IOException("展開先ファイルの場所にディレクトリがあります: " + root.Destination(entry.Relative));
            Decision previous;
            if (decisions.TryGetValue(entry, out previous) && previous.Stamp.Same(current)) return previous.Allow;
            FileStamp planned;
            bool allow;
            if (anticipated.TryGetValue(entry, out planned) && planned.Same(current)) allow = anticipatedOverwrite;
            else if (laterMode != 0) allow = laterMode == 1;
            else
            {
                char answer = Text.Choice("【展開時検出】 '" + Text.Safe(root.Destination(entry.Relative))
                    + "' という物理ファイルに対して、上書きが発生します。どうしますか (Y: これを含めてすべて上書きをする, N: これを含めて上書きせず無視する, y: このファイルは上書きし、別の上書き可能性がある場合は再度確認する, n: このファイルは上書きせず、別の上書き可能性がある場合は再度確認する, q: 中断して終了する) ?: ", "YNynq");
                if (answer == 'q') throw new OperationCanceledException("展開時の上書き確認で中断しました。");
                if (answer == 'Y') laterMode = 1;
                if (answer == 'N') laterMode = 2;
                allow = answer == 'Y' || answer == 'y';
            }
            decisions[entry] = new Decision { Stamp = current, Allow = allow };
            return allow;
        }
    }

    /// <summary>候補パスワードの順序と ignore 状態を管理する。ディスクへパスワードを書かない。</summary>
    internal sealed class PasswordManager
    {
        private readonly List<string> passwords = new List<string>();
        private bool ignoreUnknown;
        /// <summary>UTF-8（先頭 BOM 任意）の読取専用ファイルから、空行以外を原文のまま読み込む。</summary>
        internal void Load(string path)
        {
            uint attributes;
            if (!Native.Attributes(path, out attributes)) return;
            if ((attributes & 0x10) != 0) throw new IOException("password_list.txt がファイルではなくディレクトリです: " + path);
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            using (FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (StreamReader reader = new StreamReader(file, new UTF8Encoding(false, true), false))
            {
                bool first = true;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (first && line.Length != 0 && line[0] == '\ufeff') line = line.Substring(1);
                    first = false;
                    if (line.Length != 0 && seen.Add(line)) passwords.Add(line);
                }
            }
        }
        /// <summary>全候補を完全検証する。戻り値 false は ignore による省略。成功バイト列は呼出元が消去する。</summary>
        internal bool GetVerified(ZipEntry entry, out byte[] password)
        {
            password = null;
            foreach (string candidate in passwords)
            {
                Program.CheckCancel();
                if (TryPassword(entry, candidate, out password)) return true;
            }
            if (ignoreUnknown) return false;
            while (true)
            {
                Console.Write("【解読パスワードが必要】 '" + Text.Safe(entry.Name) + "' (" + Text.Safe(entry.Archive.Label)
                    + " 内) の解読パスワード ('ignore' で暗号化された全ファイルを無視): ");
                string candidate = Text.Password();
                if (candidate == "ignore") { ignoreUnknown = true; return false; }
                if (TryPassword(entry, candidate, out password))
                {
                    passwords.Remove(candidate);
                    passwords.Insert(0, candidate);
                    return true;
                }
                Console.WriteLine("エラー: そのパスワードでは展開できません。");
            }
        }
        private static bool TryPassword(ZipEntry entry, string text, out byte[] selected)
        {
            selected = null;
            List<byte[]> variants = Encodings(text, entry);
            try
            {
                foreach (byte[] bytes in variants)
                {
                    Program.CheckCancel();
                    try
                    {
                        // 書出しエラーをパスワード不正と誤認しないため、候補判定は出力なしで行う。
                        EntryCodec.WriteVerified(entry, bytes, Stream.Null);
                        selected = (byte[])bytes.Clone();
                        return true;
                    }
                    catch (InvalidDataException) { }
                    catch (EndOfStreamException) { }
                }
                return false;
            }
            finally { foreach (byte[] bytes in variants) Array.Clear(bytes, 0, bytes.Length); }
        }
        /// <summary>文字列を既存 ZIP 実装で一般的な UTF-8/CP932/OEM/ANSI のバイト表現に変換して試す。</summary>
        private static List<byte[]> Encodings(string password, ZipEntry entry)
        {
            List<Encoding> encodings = new List<Encoding>();
            Encoding legacy = Program.LegacyEncoding ?? Encoding.GetEncoding(932);
            if (entry.Aes == null && (entry.Flags & 0x800) == 0) encodings.Add(legacy);
            encodings.Add(new UTF8Encoding(false, true));
            encodings.Add(legacy);
            encodings.Add(Encoding.GetEncoding(437));
            encodings.Add(Encoding.Default);
            encodings.Add(Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage));
            List<byte[]> results = new List<byte[]>();
            foreach (Encoding encoding in encodings)
            {
                try
                {
                    Encoding strict = Encoding.GetEncoding(encoding.CodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
                    byte[] value = strict.GetBytes(password);
                    bool duplicate = false;
                    foreach (byte[] existing in results) if (Bytes.Equal(existing, value)) { duplicate = true; break; }
                    if (duplicate) Array.Clear(value, 0, value.Length); else results.Add(value);
                }
                catch (EncoderFallbackException) { }
            }
            return results;
        }
    }

    /// <summary>展開順、確定保存、個別警告、および成功サイズを管理する。</summary>
    internal sealed class Extractor
    {
        private enum FileOutcome { Unavailable, Saved, OverwriteSkipped }
        private readonly ExtractionPlan plan;
        private readonly SafeRoot root;
        private readonly PasswordManager passwords;
        private readonly OverwritePolicy overwrite;
        private readonly WarningBook warnings;
        internal long SuccessCount, SkippedCount;
        internal long SupersededCount, FallbackSuccessCount;
        internal decimal SuccessBytes;
        internal Extractor(ExtractionPlan value, SafeRoot target, PasswordManager candidates, OverwritePolicy policy, WarningBook book)
        {
            plan = value; root = target; passwords = candidates; overwrite = policy; warnings = book;
        }
        internal void Run()
        {
            long number = 0, total = plan.FileCount;
            foreach (ZipEntry entry in plan.Entries)
            {
                Program.CheckCancel();
                if (!entry.Keep) continue;
                if (!entry.IsDirectory)
                {
                    number++;
                    Console.WriteLine("({0:N0} 個目 / {1:N0} 個中: {2:N0} bytes) '{3}' を展開...", number, total, entry.Size, Text.Safe(entry.Name));
                }
                FileOutcome outcome = FileOutcome.Unavailable;
                try
                {
                    if (entry.PlanError != null) throw new IOException(entry.PlanError);
                    EntryCodec.CheckSupported(entry);
                    if (entry.IsDirectory) ExtractDirectory(entry);
                    else outcome = ExtractFile(entry, entry);
                }
                catch (Exception ex)
                {
                    if (!Program.Recoverable(ex)) throw;
                    warnings.Add(entry, ex.Message);
                    Console.WriteLine("  警告: " + Text.Safe(ex.Message));
                }
                ZipEntry fallback;
                if (!entry.IsDirectory && plan.Fallbacks.TryGetValue(entry, out fallback))
                {
                    if (outcome == FileOutcome.Saved) SupersededCount++;
                    else if (outcome == FileOutcome.Unavailable)
                    {
                        // 単なる名前一致だけで旧版を捨てない。差分が失敗した時だけ旧版を読み、
                        // CRC等を通常どおり検証して救済する。明示的な物理上書きnには介入しない。
                        Program.CheckCancel();
                        Console.WriteLine("  差分を保存できなかったためベースラインを救済展開: '{0}' ({1:N0} bytes)",
                            Text.Safe(fallback.Name), fallback.Size);
                        try
                        {
                            if (fallback.PlanError != null) throw new IOException(fallback.PlanError);
                            EntryCodec.CheckSupported(fallback);
                            if (ExtractFile(fallback, entry) == FileOutcome.Saved) FallbackSuccessCount++;
                        }
                        catch (Exception ex)
                        {
                            if (!Program.Recoverable(ex)) throw;
                            warnings.Add(fallback, "差分失敗後のベースライン救済も失敗: " + ex.Message);
                            Console.WriteLine("  警告: " + Text.Safe(ex.Message));
                        }
                    }
                }
            }
        }
        private bool PasswordFor(ZipEntry entry, out byte[] password)
        {
            password = null;
            if (!entry.Encrypted) return true;
            if (passwords.GetVerified(entry, out password)) return true;
            warnings.Add(entry, "ignore 指定: 既知の候補パスワードでは完全性検証まで成功しませんでした（不正パスワードと暗号データ破損は識別できない場合があります）。");
            return false;
        }
        private void ExtractDirectory(ZipEntry entry)
        {
            byte[] password;
            if (!PasswordFor(entry, out password)) return;
            try
            {
                EntryCodec.WriteVerified(entry, password, Stream.Null);
                using (PathLease lease = root.DirectoryLease(entry.Relative, true, plan.Directories, false)) { }
                if (entry.TimestampWarning != null) warnings.Add(entry, entry.TimestampWarning);
            }
            finally { if (password != null) Array.Clear(password, 0, password.Length); }
        }
        /// <summary>policyEntry は同一出力先の上書き判断キー。差分と遅延ベースラインで共有する。</summary>
        private FileOutcome ExtractFile(ZipEntry entry, ZipEntry policyEntry)
        {
            using (PathLease check = root.DirectoryLease(WindowsPaths.Parent(entry.Relative), false, null, false))
            {
                if (!overwrite.Allow(policyEntry, root.ProbeLeaf(entry.Relative, check))) { Skip(entry); return FileOutcome.OverwriteSkipped; }
            }
            byte[] password;
            if (!PasswordFor(entry, out password)) return FileOutcome.Unavailable;
            try
            {
                using (PathLease parents = root.DirectoryLease(WindowsPaths.Parent(entry.Relative), true, plan.Directories, false))
                using (StagedFile temporary = new StagedFile(root.Destination(WindowsPaths.Parent(entry.Relative)), entry, warnings))
                {
                    EntryCodec.WriteVerified(entry, password, temporary.Stream);
                    temporary.Stream.Flush();
                    SetTimes(entry, temporary.Stream.SafeFileHandle);
                    int races = 0;
                    while (true)
                    {
                        Program.CheckCancel();
                        FileStamp current = root.ProbeLeaf(entry.Relative, parents);
                        if (!overwrite.Allow(policyEntry, current)) { Skip(entry); return FileOutcome.OverwriteSkipped; }
                        try { temporary.Commit(root.Destination(entry.Relative), current != null); break; }
                        catch (Win32Exception ex)
                        {
                            // 新規保存の直前に対象が生じた場合、上書きへ勝手に切り替えず再確認する。
                            if ((ex.NativeErrorCode != 80 && ex.NativeErrorCode != 183) || ++races > 16) throw;
                        }
                    }
                    SuccessCount++;
                    SuccessBytes += entry.Size;
                    // 日時は Commit 前に一時ファイルへ設定済みで、同一ボリューム内の名前変更後も保持される。
                    if (entry.TimestampWarning != null) warnings.Add(entry, entry.TimestampWarning);
                }
            }
            finally { if (password != null) Array.Clear(password, 0, password.Length); }
            return FileOutcome.Saved;
        }
        private void SetTimes(ZipEntry entry, SafeFileHandle handle)
        {
            try { Native.Times(handle, entry.Times); }
            catch (Exception ex)
            {
                if (!Program.Recoverable(ex)) throw;
                warnings.Add(entry, "ファイル日時の設定失敗: " + ex.Message);
            }
        }
        private void Skip(ZipEntry entry)
        {
            SkippedCount++;
            Console.WriteLine("  上書きしない指定により省略: " + Text.Safe(root.Destination(entry.Relative)));
        }
    }

    /// <summary>Store/Deflate と暗号を組み合わせ、サイズ・CRC・認証タグまで検証する。</summary>
    internal static class EntryCodec
    {
        internal static void CheckSupported(ZipEntry entry)
        {
            if ((entry.Flags & (0x10 | 0x20 | 0x40 | 0x2000 | 0x4000)) != 0)
                throw new NotSupportedException("拡張 Deflate / patched data / PKWARE strong encryption / masked header 等の未対応 flag があります。");
            if (entry.Compression != 0 && entry.Compression != 8)
                throw new NotSupportedException("未対応の compression method: " + entry.Compression + "（対応: Store=0, Deflate=8）。");
        }
        /// <summary>output へストリーミング展開する。検証失敗は InvalidDataException。出力は呼出元が隔離する。</summary>
        internal static void WriteVerified(ZipEntry entry, byte[] password, Stream output)
        {
            CheckSupported(entry);
            using (JoinedStream joined = new JoinedStream(entry.Archive.Parts))
            using (SliceStream raw = new SliceStream(joined, entry.DataOffset, entry.CompressedSize))
            {
                if (!entry.Encrypted)
                {
                    Decode(raw, raw.Length, entry, output);
                }
                else if (entry.Aes != null)
                {
                    if (password == null) throw new InvalidDataException("暗号化エントリにパスワードが指定されていません。");
                    using (AesReadStream decoded = new AesReadStream(raw, entry.Aes, password))
                    {
                        Decode(decoded, decoded.Length, entry, output);
                        decoded.VerifyAuthentication();
                    }
                }
                else
                {
                    if (password == null) throw new InvalidDataException("暗号化エントリにパスワードが指定されていません。");
                    using (ZipCryptoStream decoded = new ZipCryptoStream(raw, password,
                        ((entry.Flags & 8) != 0) ? (byte)(entry.DosTime >> 8) : (byte)(entry.Crc >> 24)))
                        Decode(decoded, decoded.Length, entry, output);
                }
                if (raw.Position != raw.Length) throw new InvalidDataException("エントリの圧縮データが完全には消費されていません。");
            }
        }
        private static void Decode(Stream input, long compressedLength, ZipEntry entry, Stream output)
        {
            OutputWindow window = new OutputWindow(output, entry.Size);
            if (entry.Compression == 0)
            {
                if (compressedLength != entry.Size) throw new InvalidDataException("Store エントリの圧縮後・展開後サイズが一致しません。");
                byte[] buffer = new byte[131072];
                int n;
                while ((n = input.Read(buffer, 0, buffer.Length)) != 0)
                {
                    Program.CheckCancel();
                    window.Stored(buffer, n);
                }
            }
            else StrictDeflate.Inflate(input, compressedLength, window);
            window.Finish();
            if ((entry.Aes == null || entry.Aes.Version == 1) && window.Crc != entry.Crc)
                throw new InvalidDataException("CRC-32 が一致しません。破損または不正なパスワードです。");
        }
    }

    /// <summary>伝統的 PKZIP 暗号を逐次復号する。ヘッダ 1 byte の一致だけでは成功と扱わない。</summary>
    internal sealed class ZipCryptoStream : Stream
    {
        private readonly Stream source;
        private readonly long length;
        private long position;
        private uint key0 = 0x12345678U, key1 = 0x23456789U, key2 = 0x34567890U;
        internal ZipCryptoStream(Stream input, byte[] password, byte check)
        {
            source = input;
            if (input.Length < 12) throw new InvalidDataException("ZipCrypto ヘッダが不足しています。");
            length = input.Length - 12;
            foreach (byte value in password) Update(value);
            byte[] header = Bytes.Read(input, 12);
            for (int i = 0; i < 12; i++) header[i] = DecodeByte(header[i]);
            bool matches = header[11] == check;
            Array.Clear(header, 0, header.Length);
            if (!matches) throw new InvalidDataException("ZipCrypto のパスワード検査値が一致しません。");
        }
        private void Update(byte value)
        {
            unchecked
            {
                key0 = Crc32.Step(key0, value);
                key1 = (key1 + (byte)key0) * 134775813U + 1U;
                key2 = Crc32.Step(key2, (byte)(key1 >> 24));
            }
        }
        private byte DecodeByte(byte value)
        {
            uint temporary = (key2 & 0xffffU) | 2U;
            byte plain = (byte)(value ^ (byte)(unchecked(temporary * (temporary ^ 1U)) >> 8));
            Update(plain);
            return plain;
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            Bytes.CheckBuffer(buffer, offset, count);
            count = (int)Math.Min((long)count, length - position);
            if (count == 0) return 0;
            int got = source.Read(buffer, offset, count);
            if (got == 0) throw new EndOfStreamException("ZipCrypto データが途中で切れています。");
            for (int i = offset; i < offset + got; i++) buffer[i] = DecodeByte(buffer[i]);
            position += got;
            return got;
        }
        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return false; } }
        public override long Length { get { return length; } }
        public override long Position { get { return position; } set { throw new NotSupportedException(); } }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        protected override void Dispose(bool disposing)
        {
            key0 = key1 = key2 = 0;
            base.Dispose(disposing);
        }
    }

    /// <summary>WinZip AES AE-1/AE-2: PBKDF2-HMAC-SHA1(1000), AES-CTR, HMAC-SHA1-80。</summary>
    internal sealed class AesReadStream : Stream
    {
        private readonly Stream source;
        private readonly long length;
        private long position;
        private SymmetricAlgorithm algorithm;
        private ICryptoTransform encryptor;
        private HMACSHA1 authentication;
        private readonly byte[] counter = new byte[16], keyStream = new byte[16];
        private int keyPosition = 16;
        private bool authenticated;
        internal AesReadStream(Stream input, AesInfo info, byte[] password)
        {
            source = input;
            long overhead = info.SaltBytes + 2 + 10;
            if (input.Length < overhead) throw new InvalidDataException("WinZip AES のヘッダ / 認証情報が不足しています。");
            length = input.Length - overhead;
            byte[] salt = Bytes.Read(input, info.SaltBytes);
            byte[] verifier = Bytes.Read(input, 2);
            byte[] derived = null, key = null, authenticationKey = null;
            try
            {
                using (Rfc2898DeriveBytes derivation = new Rfc2898DeriveBytes(password, salt, 1000))
                    derived = derivation.GetBytes(info.KeyBytes * 2 + 2);
                int diff = (verifier[0] ^ derived[derived.Length - 2]) | (verifier[1] ^ derived[derived.Length - 1]);
                if (diff != 0) throw new InvalidDataException("AES のパスワード検査値が一致しません。");
                key = Bytes.Sub(derived, 0, info.KeyBytes);
                authenticationKey = Bytes.Sub(derived, info.KeyBytes, info.KeyBytes);
                algorithm = new AesCryptoServiceProvider();
                algorithm.BlockSize = 128;
                algorithm.KeySize = info.KeyBytes * 8;
                algorithm.Mode = CipherMode.ECB;
                algorithm.Padding = PaddingMode.None;
                encryptor = algorithm.CreateEncryptor(key, new byte[16]);
                authentication = new HMACSHA1(authenticationKey);
            }
            catch { Dispose(); throw; }
            finally
            {
                Array.Clear(salt, 0, salt.Length); Array.Clear(verifier, 0, verifier.Length);
                if (derived != null) Array.Clear(derived, 0, derived.Length);
                if (key != null) Array.Clear(key, 0, key.Length);
                if (authenticationKey != null) Array.Clear(authenticationKey, 0, authenticationKey.Length);
            }
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            Bytes.CheckBuffer(buffer, offset, count);
            count = (int)Math.Min((long)count, length - position);
            if (count == 0) return 0;
            int got = source.Read(buffer, offset, count);
            if (got == 0) throw new EndOfStreamException("AES 暗号文が途中で切れています。");
            // MAC の対象は復号前の暗号文。出力バッファは同じ配列でも変更されない。
            authentication.TransformBlock(buffer, offset, got, buffer, offset);
            for (int i = offset; i < offset + got; i++)
            {
                if (keyPosition == 16)
                {
                    // WinZip CTR のカウンターは 1 から始まる little-endian の 128 bit 整数。
                    int p = 0;
                    while (p < counter.Length) { counter[p] = unchecked((byte)(counter[p] + 1)); if (counter[p] != 0) break; p++; }
                    if (p == counter.Length) throw new InvalidDataException("AES カウンターが一巡しました。");
                    encryptor.TransformBlock(counter, 0, 16, keyStream, 0);
                    keyPosition = 0;
                }
                buffer[i] ^= keyStream[keyPosition++];
            }
            position += got;
            return got;
        }
        /// <summary>全暗号文を読んだ後に 80 bit の認証タグを定時間比較する。</summary>
        internal void VerifyAuthentication()
        {
            if (authenticated) return;
            if (position != length) throw new InvalidDataException("AES 暗号文を最後まで消費していません。");
            authentication.TransformFinalBlock(Bytes.Empty, 0, 0);
            byte[] expected = Bytes.Read(source, 10);
            byte[] actual = authentication.Hash;
            int difference = 0;
            for (int i = 0; i < 10; i++) difference |= expected[i] ^ actual[i];
            Array.Clear(expected, 0, expected.Length); Array.Clear(actual, 0, actual.Length);
            if (difference != 0) throw new InvalidDataException("AES HMAC-SHA1-80 認証コードが一致しません。破損または不正なパスワードです。");
            authenticated = true;
        }
        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return false; } }
        public override long Length { get { return length; } }
        public override long Position { get { return position; } set { throw new NotSupportedException(); } }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (encryptor != null) { encryptor.Dispose(); encryptor = null; }
                if (algorithm != null) { algorithm.Dispose(); algorithm = null; }
                if (authentication != null) { authentication.Dispose(); authentication = null; }
                Array.Clear(counter, 0, counter.Length); Array.Clear(keyStream, 0, keyStream.Length);
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>32 KiB 履歴と固定サイズの書出しバッファ。ヘッダで宣言したサイズを超えて展開しない。</summary>
    internal sealed class OutputWindow
    {
        private readonly Stream destination;
        private readonly long expected;
        private readonly byte[] history = new byte[32768], pending = new byte[131072];
        private int cursor, pendingCount;
        private long count;
        private uint crc = 0xffffffffU;
        internal uint Crc { get { return crc ^ 0xffffffffU; } }
        internal OutputWindow(Stream output, long size) { destination = output; expected = size; }
        internal void Literal(byte value)
        {
            if (count >= expected) throw new InvalidDataException("展開サイズが ZIP ヘッダの宣言値を超えました。");
            history[cursor] = value;
            cursor = (cursor + 1) & 32767;
            pending[pendingCount++] = value;
            count++;
            if (pendingCount == pending.Length) FlushPending();
        }
        internal void Repeat(int distance, int length)
        {
            if (distance < 1 || distance > 32768 || distance > count) throw new InvalidDataException("Deflate の後方参照が履歴範囲外です。");
            if (length > expected - count) throw new InvalidDataException("Deflate の繰返しが宣言サイズを超えます。");
            for (int i = 0; i < length; i++) Literal(history[(cursor - distance) & 32767]);
        }
        internal void Stored(byte[] buffer, int length)
        {
            if (length > expected - count) throw new InvalidDataException("Store データが宣言サイズを超えます。");
            crc = Crc32.Update(crc, buffer, 0, length);
            destination.Write(buffer, 0, length);
            count += length;
        }
        private void FlushPending()
        {
            Program.CheckCancel();
            if (pendingCount == 0) return;
            crc = Crc32.Update(crc, pending, 0, pendingCount);
            destination.Write(pending, 0, pendingCount);
            pendingCount = 0;
        }
        internal void Finish()
        {
            if (count != expected) throw new InvalidDataException("展開サイズが ZIP ヘッダと一致しません: expected=" + expected + ", actual=" + count);
            FlushPending();
        }
    }

    /// <summary>Deflate の LSB-first ビット読取。終端ブロック後に未消費バイトが残る場合も検出する。</summary>
    internal sealed class DeflateBits
    {
        private readonly Stream source;
        private readonly long expected;
        private readonly byte[] buffer = new byte[65536];
        private int offset, available;
        private long injectedBytes;
        private ulong bits;
        internal int Count { get; private set; }
        internal DeflateBits(Stream input, long length) { source = input; expected = length; }
        internal void Fill(int required)
        {
            while (Count < required)
            {
                if (offset == available)
                {
                    Program.CheckCancel();
                    available = source.Read(buffer, 0, buffer.Length);
                    offset = 0;
                    if (available == 0) return;
                }
                bits |= (ulong)buffer[offset++] << Count;
                injectedBytes++;
                Count += 8;
            }
        }
        internal int Peek(int count) { return (int)(bits & ((1UL << count) - 1)); }
        internal void Drop(int count)
        {
            if (count > Count) throw new InvalidDataException("Deflate のビット列が途中で切れています。");
            bits >>= count; Count -= count;
        }
        internal int Read(int count)
        {
            if (count == 0) return 0;
            Fill(count);
            if (Count < count) throw new InvalidDataException("Deflate のビット列が途中で切れています。");
            int result = Peek(count);
            Drop(count);
            return result;
        }
        internal void AlignByte() { Drop(Count & 7); }
        internal void Finish()
        {
            if (injectedBytes != expected || Count >= 8)
                throw new InvalidDataException("Deflate 終端後に余分なデータがあるか、圧縮サイズが不一致です。");
            // 最終バイト内の未使用パディングビットは RFC 1951 に従い値を制限しない。
        }
    }

    /// <summary>最大 15 bit の canonical Huffman 表。過剰割当・不正な不完全木を拒否する。</summary>
    internal sealed class Huffman
    {
        private readonly int[] table;
        private readonly int width;
        internal Huffman(int[] lengths, bool allowSingle, bool allowEmpty)
        {
            int[] counts = new int[16];
            int symbols = 0, maximum = 0;
            foreach (int length in lengths)
            {
                if (length < 0 || length > 15) throw new InvalidDataException("Huffman コード長が範囲外です。");
                if (length != 0) { counts[length]++; symbols++; maximum = Math.Max(maximum, length); }
            }
            if (symbols == 0)
            {
                if (!allowEmpty) throw new InvalidDataException("空の Huffman 木です。");
                width = 1; table = new int[2]; return;
            }
            int left = 1;
            for (int length = 1; length <= maximum; length++)
            {
                left = (left << 1) - counts[length];
                if (left < 0) throw new InvalidDataException("Huffman コードが過剰割当です。");
            }
            if (left != 0 && !(allowSingle && symbols == 1 && maximum == 1))
                throw new InvalidDataException("不正な不完全 Huffman 木です。");
            width = maximum;
            table = new int[1 << width];
            int[] next = new int[16];
            int code = 0;
            for (int length = 1; length <= maximum; length++) { code = (code + counts[length - 1]) << 1; next[length] = code; }
            for (int symbol = 0; symbol < lengths.Length; symbol++)
            {
                int length = lengths[symbol];
                if (length == 0) continue;
                int forward = next[length]++, reverse = 0;
                for (int i = 0; i < length; i++) { reverse = (reverse << 1) | (forward & 1); forward >>= 1; }
                int packed = (length << 16) | (symbol + 1);
                for (int i = reverse; i < table.Length; i += 1 << length) table[i] = packed;
            }
        }
        internal int Decode(DeflateBits bits)
        {
            bits.Fill(width);
            int value = table[bits.Peek(width)];
            int length = value >> 16;
            if (value == 0 || length > bits.Count) throw new InvalidDataException("Huffman コードが不正または切断されています。");
            bits.Drop(length);
            return (value & 0xffff) - 1;
        }
    }

    /// <summary>RFC 1951 の Store / fixed / dynamic ブロックを解釈する厳密なストリーミング inflater。</summary>
    internal static class StrictDeflate
    {
        private static readonly int[] LengthBase = { 3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 19, 23, 27, 31, 35, 43, 51, 59, 67, 83, 99, 115, 131, 163, 195, 227, 258 };
        private static readonly int[] LengthExtra = { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0 };
        private static readonly int[] DistanceBase = { 1, 2, 3, 4, 5, 7, 9, 13, 17, 25, 33, 49, 65, 97, 129, 193, 257, 385, 513, 769, 1025, 1537, 2049, 3073, 4097, 6145, 8193, 12289, 16385, 24577 };
        private static readonly int[] DistanceExtra = { 0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13 };
        private static readonly Huffman FixedLiterals = MakeFixedLiterals();
        private static readonly Huffman FixedDistances = MakeFixedDistances();
        private static Huffman MakeFixedLiterals()
        {
            int[] lengths = new int[288];
            for (int i = 0; i < lengths.Length; i++) lengths[i] = i < 144 ? 8 : i < 256 ? 9 : i < 280 ? 7 : 8;
            return new Huffman(lengths, false, false);
        }
        private static Huffman MakeFixedDistances()
        {
            int[] lengths = new int[32];
            for (int i = 0; i < lengths.Length; i++) lengths[i] = 5;
            return new Huffman(lengths, false, false);
        }
        /// <summary>入力 length バイトを解釈し、出力は window に送る。終端・参照・木をすべて検査する。</summary>
        internal static void Inflate(Stream input, long length, OutputWindow window)
        {
            DeflateBits bits = new DeflateBits(input, length);
            bool final;
            do
            {
                Program.CheckCancel();
                final = bits.Read(1) != 0;
                int type = bits.Read(2);
                if (type == 0)
                {
                    bits.AlignByte();
                    int count = bits.Read(16), complement = bits.Read(16);
                    if ((count ^ 0xffff) != complement) throw new InvalidDataException("Deflate 非圧縮ブロックの LEN/NLEN が不一致です。");
                    for (int i = 0; i < count; i++) window.Literal((byte)bits.Read(8));
                }
                else if (type == 1) DecodeBlock(bits, window, FixedLiterals, FixedDistances);
                else if (type == 2)
                {
                    Huffman literals, distances;
                    DynamicTrees(bits, out literals, out distances);
                    DecodeBlock(bits, window, literals, distances);
                }
                else throw new InvalidDataException("Deflate の予約済みブロック種別です。");
            } while (!final);
            bits.Finish();
        }
        private static void DecodeBlock(DeflateBits bits, OutputWindow window, Huffman literals, Huffman distances)
        {
            while (true)
            {
                int symbol = literals.Decode(bits);
                if (symbol < 256) { window.Literal((byte)symbol); continue; }
                if (symbol == 256) return;
                if (symbol < 257 || symbol > 285) throw new InvalidDataException("Deflate の予約済み長さコードです。");
                int index = symbol - 257;
                int length = LengthBase[index] + bits.Read(LengthExtra[index]);
                int distanceSymbol = distances.Decode(bits);
                if (distanceSymbol < 0 || distanceSymbol >= 30) throw new InvalidDataException("Deflate の予約済み距離コードです。");
                int distance = DistanceBase[distanceSymbol] + bits.Read(DistanceExtra[distanceSymbol]);
                window.Repeat(distance, length);
            }
        }
        private static void DynamicTrees(DeflateBits bits, out Huffman literals, out Huffman distances)
        {
            int literalCount = bits.Read(5) + 257, distanceCount = bits.Read(5) + 1, codeCount = bits.Read(4) + 4;
            if (literalCount > 286) throw new InvalidDataException("Deflate HLIT が範囲外です。");
            // 規定の permutation: 16,17,18,0,8,7,9,6,10,5,11,4,12,3,13,2,14,1,15。
            int[] order = { 16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15 };
            int[] codeLengths = new int[19];
            for (int i = 0; i < codeCount; i++) codeLengths[order[i]] = bits.Read(3);
            Huffman codeTree = new Huffman(codeLengths, false, false);
            int[] combined = new int[literalCount + distanceCount];
            int p = 0;
            while (p < combined.Length)
            {
                int symbol = codeTree.Decode(bits);
                if (symbol <= 15) { combined[p++] = symbol; continue; }
                int repeat, value;
                if (symbol == 16)
                {
                    if (p == 0) throw new InvalidDataException("Deflate コード長の先頭で前値の繰返しが指定されています。");
                    repeat = bits.Read(2) + 3; value = combined[p - 1];
                }
                else if (symbol == 17) { repeat = bits.Read(3) + 3; value = 0; }
                else if (symbol == 18) { repeat = bits.Read(7) + 11; value = 0; }
                else throw new InvalidDataException("Deflate コード長シンボルが範囲外です。");
                if (repeat > combined.Length - p) throw new InvalidDataException("Deflate コード長の繰返しが配列を超えています。");
                while (repeat-- > 0) combined[p++] = value;
            }
            int[] literalLengths = new int[literalCount], distanceLengths = new int[distanceCount];
            Array.Copy(combined, 0, literalLengths, 0, literalCount);
            Array.Copy(combined, literalCount, distanceLengths, 0, distanceCount);
            if (literalLengths[256] == 0) throw new InvalidDataException("Deflate の EOB コードがありません。");
            literals = new Huffman(literalLengths, true, false);
            distances = new Huffman(distanceLengths, true, true);
        }
    }
}
