/*
DNNT 261008_FLVS83 分割 ZIP 結合展開ユーティリティ R04

ソースコードファイル名: dnnt_261004_qct5nr_split_zip_extract_r04_261008_flvs83.cs
今回バージョン: R04 (入力ソースの指定バージョン: R03)
内部識別名・名前空間: dnnt_261004_qct5nr_split_zip_extract (継続)
前回標題: DNNT 261007_VGD8LA 分割 ZIP 結合展開ユーティリティ R03
R02 標題: DNNT 261007_TFDXE5 分割 ZIP 結合展開ユーティリティ 2
初版標題: DNNT 261004_QCT5NR 分割 ZIP 結合展開ユーティリティ

目的: ZIP / UNIX split 断片 / その混在群を認識し、通常展開またはベースラインへの差分展開を行う。
対象: .NET Framework 4.8 が動作する Windows / .NET Framework 4.8 API / AnyCPU。
      Visual Studio 2026 と 4.8 targeting pack でビルドする。既存プロジェクトの Exe 設定を継承。
      本体は C# 4 構文を保ち、既存 Lib.cs を含むプロジェクトでは C# 7.3 を指定する。
原理: シーク可能な仮想連結ストリーム上で EOCD、ZIP64、中央・ローカルヘッダ、
      data descriptor の範囲と一致を検証する。連結中間ファイルは作らない。
      ZIP 解釈、CRC、ZipCrypto は本ファイルで実装し、通常の Deflate 展開は標準 DeflateStream に任せる。
      標準 inflater が解釈を拒否した入力だけ、出力を巻き戻し従来の厳密 inflater で完全再検証する。
      AES/PBKDF2/HMAC は .NET 標準の暗号プリミティブのみを使用する。
      保存は同じディレクトリの一時ファイルを検証後に閉じ、MoveFileExW で確定する。
      危険な ZIP パス・リンク、および既存分類で回復不能な例外は全体中断する。
      回復可能と判定される保存等の失敗は警告して継続する。CRC 等の InvalidDataException は
      既存 Recoverable に含まれず全体中断となる点も維持する（README の R04 新規指摘参照）。
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

改修 R03 / DNNT 261007_VGD8LA / 2026/10/07 22:34:04 (JST):
      Q.zip.P の群別仮想連結を追加し、旧個別・全体連結の検査と併せて一意に認識する。
      /d・-d で差分とベースラインを独立して検査し、暗黙ディレクトリを含む相対パス構造から
      一致ファイル数＋一致ディレクトリ数が最大、次に深さ合計が最小の一意な基準を求める。
      照合は共通名の投影・同一部分木の共有・転置索引・上界枝刈りを使用し、全候補対を保持しない。
      ベースライン基準配下は差分基準へ、基準外は _base_misc_files/yyMMdd_HHmmss へ再配置する。
      差分で置換するベースラインの展開を省略し、差分の失敗時だけ既存の上書き許可内で復元を試す。
      正常終了時に e/E/o/O で Explorer を起動する。入力履歴は通常/差分共通とベースライン別に保存する。
      ZIP/暗号/CRC/Deflate と Win32 の保存アルゴリズムは R02 を維持する。

改修 R04 / DNNT 261008_FLVS83 / 2026/10/08 06:00:19 (JST):
      .NET Framework 4.8 を対象とし、標準 DeflateStream の native 展開を使用する。
      圧縮ペイロードの最終 1 byte を分離して終端不足と余分データを検出し、サイズ・CRC・AES 認証を維持。
      標準 inflater が拒否する従来互換入力だけ、未確定出力を巻き戻し旧 StrictDeflate で全検証し直す。
      MAX(論理プロセッサ数 - 1, 1) 以下の再利用 worker と上限付きメモリ pipe で複数ファイルを先読みする。
      パスワードの質問、上書き判断、親作成、保存・時刻・確定、警告・集計・復元は主スレッドで元順を保持。
      同じ SourcePart の物理 Position 設定と Read を一つの lock で保護し、入力の固定済みハンドルを継承。
      候補パスワードは不変 snapshot、例外は元型を保持、停止後に全 worker の終了を待ってから後片付けする。
      旧方式用の大きな出力配列は必要時だけ確保する。プロジェクトと App.config に対象環境と互換設定を反映。
      R03 の入力認識、差分照合・再配置、暗号形式、CRC、Win32 保存、例外の回復可否は維持する。

今回の生成情報 (R04):
      本プログラムは生成 AI により生成され、今回の改修も生成 AI が実施。
      AI バージョン・モデル: GPT-6 Astra Pro (内部ビルド識別子は取得不可)。
      思考レベル: このセッションで公開された設定値は取得不可のため推測しない。
      セッション開始日時(JST): 正確な値は取得不可。
      今回依頼の受信時刻(JST): 2026/10/08 05:32:10 (会話に供給された時刻情報)。
      応答生成日時(JST): 2026/10/08 06:00:19 (JST)
      実 C# のビルド・回帰結果、および Windows / 実並列動作の未検証範囲は README の R04 追記参照。

前回の生成情報 (R03、過去の検証状況を保持):
      本プログラムは生成 AI により生成され、今回の追加改修も生成 AI が実施。
      AI バージョン・モデル: GPT-6 Astra Pro (内部ビルド識別子は取得不可)。
      思考レベル: 公開された設定値を取得できないため記載しない。
      セッション開始日時(JST): 正確な値は取得不可。
      今回依頼の受信時刻(JST): 2026/10/07 21:56:34 (会話の時刻情報)。
      今回作業の最初の環境時計記録(JST): 2026/10/07 21:56:49。
      応答生成日時(JST): 2026/10/07 22:34:04 (JST)
      検証の実施結果と未検証範囲: README の今回 R03 追記を参照。

前回の生成情報 (R02、過去の検証状況を保持):
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
using System.Runtime.ExceptionServices;
using System.Threading;
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
        // 主スレッドの対話状態へ触れずに、一つの不要な先読み処理だけを停止する。
        [ThreadStatic]
        internal static Func<bool> WorkerCancelRequested;
        internal static readonly CultureInfo NumberCulture = CultureInfo.InvariantCulture;
        internal static Encoding LegacyEncoding;

        // ダイアログ履歴はユーザーごとに保持する。管理者権限を必要とする HKLM は使用しない。
        private const string DialogRegistrySubKey = @"Software\DNNT\dnnt_261004_qct5nr_split_zip_extract";
        private const string SourceDialogDirectoryValue = "SourceDialogDirectory";
        private const string BaselineDialogDirectoryValue = "BaselineSourceDialogDirectory";
        private const string DestinationDialogParentValue = "DestinationDialogParentDirectory";

        /// <summary>入力ファイル群と /d または -d を受け取り、Win32 に準じた終了コードを返す。</summary>
        [STAThread]
        private static int Main(string[] args)
        {
            int result = 31;
            bool consoleReady = false;
            string successfulDestination = null;
            try
            {
                if (Environment.OSVersion.Platform != PlatformID.Win32NT || Environment.OSVersion.Version.Major < 6)
                    throw new PlatformNotSupportedException(".NET Framework 4.8 が利用できる Windows 環境が必要です。");
                // ターゲット更新で Path の正規化・MAX_PATH の既定まで変えない。最初の Path 利用前に設定する。
                AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", true);
                AppContext.SetSwitch("Switch.System.IO.BlockLongPaths", true);
                // native 展開の選択は、最初の DeflateStream とその共有実装が初期化される前に確定する。
                AppContext.SetSwitch("Switch.System.IO.Compression.DoNotUseNativeZipLibraryForDecompression", false);
                Native.EnsureConsole();
                consoleReady = true;
                bool differential;
                string[] fileArguments = GetFileArguments(args, out differential);
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

                // 両入力群は、ハンドル、認識結果、重複判定をそれぞれ独立して所有する。
                // 差分側の認識・仮想パス検査が完了してから、必ず別ダイアログでベースラインを選ぶ。
                using (InputGroup primary = ReadInputGroup(fileArguments,
                    differential ? "(1) まず、差分 zip 群を指定してください" : "入力ファイル群を選択",
                    SourceDialogDirectoryValue, differential ? "差分 ZIP 群" : null))
                using (InputGroup baseline = differential ? ReadInputGroup(new string[0],
                    "(2) 次に、ベースライン zip 群を指定してください",
                    BaselineDialogDirectoryValue, "ベースライン ZIP 群") : null)
                {
                    WarningBook warnings = new WarningBook();
                    ExtractionPlan plan = primary.Plan;
                    DifferenceOverlay overlay = null;
                    if (differential)
                    {
                        primary.Plan.ConfirmDuplicates();
                        baseline.Plan.ConfirmDuplicates();
                        Console.WriteLine("基準ディレクトリを計算中...");
                        ReferenceMatch match = DirectoryMatcher.Find(baseline.Plan, primary.Plan);
                        overlay = DifferenceOverlay.Build(baseline.Plan, primary.Plan, match,
                            baseline.Sources[0], File.GetLastWriteTime(baseline.Sources[0]));
                        plan = overlay.Plan;
                        overlay.Print();
                    }
                    string root = SelectDestination(Path.GetDirectoryName(primary.Sources[0]), primary.Sources, differential);
                    using (SafeRoot safeRoot = new SafeRoot(root, warnings))
                    {
                        SourceSet[] inputs = differential
                            ? new SourceSet[] { primary.SourceSet, baseline.SourceSet }
                            : new SourceSet[] { primary.SourceSet };
                        plan.BindDestination(safeRoot, inputs);
                        if (!differential) plan.ConfirmDuplicates();
                        plan.BuildDirectoryTimes();
                        OverwritePolicy policy = new OverwritePolicy(plan, safeRoot, warnings);
                        policy.Preflight();
                        Extractor extractor = new Extractor(plan, safeRoot, passwords, policy, warnings);
                        try { extractor.Run(); }
                        finally { safeRoot.RestoreCreatedDirectoryTimes(plan.Directories); }
                        if (overlay != null)
                        {
                            Console.WriteLine("【差分 ZIP モードでの展開終了】");
                            overlay.Print();
                            Console.WriteLine("差分の失敗時にベースラインから復元できたファイル: {0:N0} 個", extractor.FallbackSuccessCount);
                        }
                        int sourceCount = primary.Sources.Count + (baseline == null ? 0 : baseline.Sources.Count);
                        string mode = baseline == null ? primary.Detected.Mode
                            : "差分 ZIP モード / 差分: " + primary.Detected.Mode + " / ベースライン: " + baseline.Detected.Mode;
                        string summary = String.Format(NumberCulture,
                            "【{0:N0} 個の zip ファイル群 (モード: {1}) から、{2:N0} 個のファイル (合計 {3:N0} bytes) を展開完了】",
                            sourceCount, mode, extractor.SuccessCount, extractor.SuccessBytes);
                        Console.WriteLine(summary);
                        warnings.Print();
                        Console.WriteLine("意図的な省略: 重複 {0:N0} 個 / 上書きしない指定 {1:N0} 個", plan.DuplicateCount, extractor.SkippedCount);
                        if (overlay != null)
                            Console.WriteLine("差分優先によるベースライン展開省略予定: {0:N0} 個 (合計 {1:N0} bytes)",
                                overlay.SupersededCount, overlay.SupersededBytes);
                        Console.WriteLine(summary);
                        Console.WriteLine("展開先ディレクトリフルパス:\n" + Text.Safe(WindowsPaths.WithSlash(root)));
                        result = warnings.HasWarnings ? 299 : 0; // ERROR_PARTIAL_COPY
                        if (result == 0) successfulDestination = root;
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
                    bool canOpen = result == 0 && successfulDestination != null;
                    if (canOpen) Console.WriteLine("e または o キーを押すとこのフォルダを開きます。");
                    Console.WriteLine("何かキーを押すと終了します...");
                    char key = '\0';
                    try { key = Console.ReadKey(true).KeyChar; }
                    catch (InvalidOperationException)
                    {
                        try
                        {
                            string line = Console.ReadLine();
                            if (line != null && line.Length == 1) key = line[0];
                        }
                        catch (IOException) { }
                    }
                    catch (IOException) { }
                    if (canOpen && (key == 'e' || key == 'E' || key == 'o' || key == 'O'))
                    {
                        try { OpenDestination(successfulDestination); }
                        catch (Exception ex)
                        {
                            if (!Recoverable(ex) && !(ex is InvalidOperationException)) throw;
                            Console.Error.WriteLine("展開は完了しましたが、フォルダを開けません: " + Text.Safe(ex.Message));
                            result = ExitCode(ex);
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>引数中のモードスイッチを除き、入力ファイル引数だけを元の順序で返す。</summary>
        /// <param name="args">コマンドライン引数。スイッチはどの位置でも指定可能。</param>
        /// <param name="differential">/d または -d（大文字も許容）があれば true。</param>
        /// <returns>後続の既存フルパス検査へ渡すファイル引数。</returns>
        internal static string[] GetFileArguments(string[] args, out bool differential)
        {
            differential = false;
            List<string> files = new List<string>();
            foreach (string value in args)
            {
                if (String.Equals(value, "/d", StringComparison.OrdinalIgnoreCase)
                    || String.Equals(value, "-d", StringComparison.OrdinalIgnoreCase)) differential = true;
                else files.Add(value);
            }
            return files.ToArray();
        }

        /// <summary>一方の入力群だけを選択・検証・認識し、所有権を持つ InputGroup を返す。</summary>
        /// <param name="args">ファイル引数。空ならダイアログを表示。</param>
        /// <param name="title">開くダイアログのタイトル。</param>
        /// <param name="historyValue">通常/差分側とベースライン側で分けるレジストリ値名。</param>
        /// <param name="label">コンソール用の入力群名。通常モードでは null。</param>
        /// <returns>呼出元が Dispose する入力群。</returns>
        private static InputGroup ReadInputGroup(string[] args, string title, string historyValue, string label)
        {
            string[] paths = GetInputs(args, title, historyValue);
            if (label != null) Console.WriteLine(label + ":");
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
            InputGroup group = new InputGroup(sources);
            try
            {
                Console.WriteLine("認識結果: {0} / 物理ファイル {1:N0} 個 / 論理 ZIP {2:N0} 個",
                    group.Detected.Mode, sources.Count, group.Detected.Archives.Count);
                return group;
            }
            catch { group.Dispose(); throw; }
        }

        /// <summary>完全な出力先パスを一つの引数として Windows の explorer.exe へ渡す。</summary>
        /// <param name="directory">展開が正常終了した出力先の絶対パス。</param>
        private static void OpenDestination(string directory)
        {
            string explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            // 通常パスには引用符が入らない。ルート末尾の逆斜線だけは終端引用符の前で二重化する。
            string argument = "\"" + directory + (directory.EndsWith("\\", StringComparison.Ordinal) ? "\\" : "") + "\"";
            System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo(explorer, argument);
            info.UseShellExecute = true;
            using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(info)) { }
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
        private static string[] GetInputs(string[] args, string title, string historyValue)
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
                    string rememberedSourceDirectory = ReadDialogDirectory(historyValue);
                    if (rememberedSourceDirectory != null) dialog.InitialDirectory = rememberedSourceDirectory;

                    if (dialog.ShowDialog() != DialogResult.OK) throw new OperationCanceledException("ファイル選択が取り消されました。");
                    selected = dialog.FileNames;

                    // OK 直後、並べ替え・除外・ZIP 認識の前の最初の選択ファイルを記録する。
                    // 通常/差分は共通値、ベースラインは独立値を使用する。
                    if (selected.Length != 0)
                        WriteDialogDirectory(historyValue, Path.GetDirectoryName(WindowsPaths.Full(selected[0])));

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

            // コマンドライン入力後にも履歴を更新する既存挙動は維持する。
            // ダイアログの場合は OK 直後に既に保存済みであり、正規化後の値で上書きしない。
            if (args.Length != 0 && !String.IsNullOrEmpty(directory))
                WriteDialogDirectory(historyValue, directory);

            return answer.ToArray();
        }

        /// <summary>
        /// 保存ダイアログの架空ファイル名の親ディレクトリを返す。ファイルは作成しない。
        /// 前回選択先の 1 つ上のディレクトリ a がレジストリにあれば、それを初期位置として使用する。
        /// </summary>
        /// <param name="initial">履歴がない場合に使用する初期ディレクトリ。</param>
        /// <param name="sources">展開対象の検証済み物理ファイルのフルパス一覧。1 件以上必要。順序は変更しない。</param>
        /// <param name="differential">差分モードなら true。保存ダイアログのタイトルに反映する。</param>
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
                dialog.Title = differential ? "差分 ZIP モードで展開する先を指定してください"
                    : "通常 ZIP モードで展開する先を指定してください";
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
            Func<bool> localCancellation = WorkerCancelRequested;
            if (localCancellation != null && localCancellation())
                throw new OperationCanceledException("先読み展開が取り消されました。");
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
        // R04: 複数の論理ストリームが共有する物理 FileStream の位置設定と読取りを一体として保護する。
        internal readonly object ReadLock = new object();
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

    /// <summary>通常、差分、ベースラインの一入力群。入力ハンドルとその認識・事前計画を独立所有する。</summary>
    internal sealed class InputGroup : IDisposable
    {
        internal readonly List<string> Sources;
        internal readonly SourceSet SourceSet;
        internal readonly DetectedArchives Detected;
        internal readonly ExtractionPlan Plan;

        /// <summary>順序確定済みの物理ファイルを保持し、ZIP 全構造と仮想パスを検査する。</summary>
        /// <param name="sources">ASCII 相当の Ordinal 順に並べた、空でないフルパス一覧。</param>
        internal InputGroup(List<string> sources)
        {
            Sources = sources;
            SourceSet = new SourceSet(sources);
            try
            {
                Detected = Detector.Detect(SourceSet.Parts);
                Plan = ExtractionPlan.Prepare(Detected.Archives);
            }
            catch { SourceSet.Dispose(); throw; }
        }

        /// <summary>この入力群が開いた入力ハンドルをすべて閉じる。</summary>
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
                SourcePart part = parts[low];
                int got;
                // 入力の実体を固定した既存ハンドルを使い続ける。復号・展開・CRC はこの lock の外。
                lock (part.ReadLock)
                {
                    FileStream source = part.File;
                    if (source.Position != local) source.Position = local;
                    got = source.Read(buffer, offset, amount);
                }
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
        internal readonly EntryTimes Times = new EntryTimes();
        internal bool Encrypted { get { return (Flags & 1) != 0; } }
        internal int Compression { get { return Aes == null ? Method : Aes.Method; } }
        internal string Label
        {
            get { return "'" + Name + "' (" + Archive.Label + " 内, local=0x" + LocalOffset.ToString("X", CultureInfo.InvariantCulture) + ")"; }
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

    /// <summary>個別・全体連結・群別連結の候補から確定した論理 ZIP の判定結果。</summary>
    internal sealed class DetectedArchives
    {
        internal string Mode;
        internal List<ZipArchiveData> Archives;
    }

    /// <summary>
    /// 全個別・全体連結・ファイル名による群別連結の三候補を、ZIP の全構造照合で判定する。
    /// 名前は候補を作るためだけに使い、拡張子だけで ZIP と断定しない。入力順は変更しない。
    /// </summary>
    internal static class Detector
    {
        /// <summary>同じ Q.zip を持つ物理入力と、その末尾 P。</summary>
        private sealed class NamedPart
        {
            internal SourcePart Part;
            internal string Suffix;
        }

        /// <summary>命名規則による推定群。Members は P の Ordinal 順へ並べ替える。</summary>
        private sealed class NamedGroup
        {
            internal string Stem;
            internal readonly List<NamedPart> Members = new List<NamedPart>();
            internal List<SourcePart> Ordered;
            internal ZipArchiveData Archive;
            internal string Problem;
        }

        /// <summary>すべての物理入力を過不足なく使う解釈を検査し、一意な論理 ZIP 群を返す。</summary>
        /// <param name="parts">既存のファイル名 Ordinal 順で保持された空でない物理入力一覧。</param>
        /// <returns>確定したモードと論理 ZIP の順序。未確定・曖昧なら詳細な例外。</returns>
        internal static DetectedArchives Detect(IList<SourcePart> parts)
        {
            if (parts == null) throw new ArgumentNullException("parts");
            if (parts.Count == 0) throw new InvalidDataException("認識する ZIP 入力がありません。");

            // 単独での検査結果はハイブリッド候補でも再利用する。ファイルハンドルを増やさず、
            // どの候補も JoinedStream 上で EOCD / ZIP64 / 中央・ローカルヘッダまで検査する。
            ZipArchiveData[] singles = new ZipArchiveData[parts.Count];
            string[] singleProblems = new string[parts.Count];
            List<ZipArchiveData> separate = new List<ZipArchiveData>();
            bool allSeparate = true;
            for (int index = 0; index < parts.Count; index++)
            {
                Program.CheckCancel();
                ZipArchiveData archive;
                string problem;
                if (TryRead(new SourcePart[] { parts[index] }, out archive, out problem))
                {
                    singles[index] = archive;
                    separate.Add(archive);
                }
                else
                {
                    allSeparate = false;
                    singleProblems[index] = problem;
                }
            }

            List<DetectedArchives> candidates = new List<DetectedArchives>();
            if (allSeparate)
                AddCandidate(candidates, new DetectedArchives
                {
                    Mode = parts.Count == 1 ? "個別複数 ZIP モード（単一 ZIP）" : "個別複数 ZIP モード",
                    Archives = separate
                });

            ZipArchiveData joined = null;
            string joinedProblem = null;
            bool joinedValid = false;
            if (parts.Count > 1)
            {
                // 従来の巨大 ZIP 分割モードは、同一 Q の存在を条件にしない。
                // .01.zip.a / .02.zip.b / .03.zip.c のように Q ごとに 1 個しかない旧入力も、
                // 元の全入力順で仮想連結して引き続き認識する。
                joinedValid = TryRead(parts, out joined, out joinedProblem);
                if (joinedValid)
                    AddCandidate(candidates, new DetectedArchives
                    {
                        Mode = "巨大 ZIP 分割モード",
                        Archives = new List<ZipArchiveData> { joined }
                    });
            }

            string hybridProblem;
            DetectedArchives hybrid = TryHybrid(parts, singles, singleProblems, joined, joinedProblem,
                joinedValid, out hybridProblem);
            if (hybrid != null) AddCandidate(candidates, hybrid);

            if (candidates.Count == 1) return candidates[0];
            if (candidates.Count > 1)
            {
                StringBuilder ambiguity = new StringBuilder();
                ambiguity.Append("複数の異なる ZIP 解釈が全構造検査に合格した曖昧な入力です。自動選択を中断します。");
                foreach (DetectedArchives candidate in candidates)
                {
                    ambiguity.Append("\n候補: ").Append(candidate.Mode);
                    for (int index = 0; index < candidate.Archives.Count; index++)
                        ambiguity.Append("\n  論理 ZIP ").Append(index + 1).Append(": ")
                            .Append(PartList(candidate.Archives[index].Parts));
                }
                throw new InvalidDataException(ambiguity.ToString());
            }

            StringBuilder failure = new StringBuilder();
            failure.Append("入力の一貫性欠如。欠落断片、順序違い、誤った群分け、別 ZIP との混在、破損または対象外形式が考えられます。");
            failure.Append("\n個別 ZIP 候補の検査:");
            for (int index = 0; index < parts.Count; index++)
                failure.Append("\n  ").Append(Path.GetFileName(parts[index].PathName)).Append(": ")
                    .Append(singles[index] == null ? singleProblems[index] : "単独 ZIP として整合");
            if (parts.Count > 1)
                failure.Append("\n全入力の仮想連結（元の ASCII ファイル名順）: ").Append(joinedProblem);
            failure.Append("\nハイブリッド候補の検査:\n").Append(hybridProblem);
            throw new InvalidDataException(failure.ToString());
        }

        /// <summary>
        /// Q.zip.P が 2 個以上ある群だけを結合する。群外の入力は単独 ZIP として照合する。
        /// Q と .zip の比較は Windows の通常のファイル名比較に合わせ OrdinalIgnoreCase、
        /// P のソートは仕様どおり ASCII の大文字・小文字を区別する Ordinal とする。
        /// </summary>
        /// <param name="parts">全物理入力。ここで元リストの順序は変更しない。</param>
        /// <param name="singles">各入力の単独 ZIP 検査結果。失敗位置は null。</param>
        /// <param name="singleProblems">各単独検査の失敗理由。</param>
        /// <param name="joined">全体連結の検査結果。</param>
        /// <param name="joinedProblem">全体連結が失敗した場合の理由。</param>
        /// <param name="joinedValid">全体連結が成功したか。</param>
        /// <param name="problem">群別候補が成立しない場合の詳細理由。</param>
        /// <returns>完全な群別解釈。成立しない場合は null。</returns>
        private static DetectedArchives TryHybrid(IList<SourcePart> parts, ZipArchiveData[] singles,
            string[] singleProblems, ZipArchiveData joined, string joinedProblem, bool joinedValid,
            out string problem)
        {
            Dictionary<string, NamedGroup> byStem = new Dictionary<string, NamedGroup>(StringComparer.OrdinalIgnoreCase);
            List<NamedGroup> groups = new List<NamedGroup>();
            NamedGroup[] memberships = new NamedGroup[parts.Count];
            for (int index = 0; index < parts.Count; index++)
            {
                Program.CheckCancel();
                string name = Path.GetFileName(parts[index].PathName);
                int separator = name.LastIndexOf(".zip.", StringComparison.OrdinalIgnoreCase);
                if (separator < 0) continue;
                string stem = name.Substring(0, separator + 4);
                string suffix = name.Substring(separator + 5);
                NamedGroup group;
                if (!byStem.TryGetValue(stem, out group))
                {
                    group = new NamedGroup { Stem = stem };
                    byStem.Add(stem, group);
                    groups.Add(group);
                }
                group.Members.Add(new NamedPart { Part = parts[index], Suffix = suffix });
                memberships[index] = group;
            }

            int splitCount = 0;
            bool valid = true;
            foreach (NamedGroup group in groups)
            {
                Program.CheckCancel();
                if (group.Members.Count < 2) continue;
                splitCount++;

                // 群が 1 個だけで全入力を含む場合も候補を作る。全体連結と同じ順序なら、
                // 既に実行済みの構造検査とメタデータを再利用して二度読みを避ける。
                group.Members.Sort(delegate (NamedPart a, NamedPart b)
                {
                    return StringComparer.Ordinal.Compare(a.Suffix, b.Suffix);
                });
                List<SourcePart> ordered = new List<SourcePart>();
                foreach (NamedPart member in group.Members) ordered.Add(member.Part);
                group.Ordered = ordered;
                group.Problem = NamingProblem(group);
                if (group.Problem == null)
                {
                    if (SameParts(ordered, parts))
                    {
                        if (joinedValid) group.Archive = joined;
                        else group.Problem = joinedProblem;
                    }
                    else
                    {
                        ZipArchiveData archive;
                        string readProblem;
                        if (TryRead(ordered, out archive, out readProblem)) group.Archive = archive;
                        else group.Problem = readProblem;
                    }
                }
                if (group.Archive == null) valid = false;
            }
            if (splitCount == 0)
            {
                problem = "  同じ Q.zip に属する入力が 2 個以上の群はありません。1 個だけの .zip.P は群分けしません。";
                return null;
            }

            List<ZipArchiveData> archives = new List<ZipArchiveData>();
            HashSet<NamedGroup> emitted = new HashSet<NamedGroup>();
            int standaloneCount = 0;
            for (int index = 0; index < parts.Count; index++)
            {
                Program.CheckCancel();
                NamedGroup group = memberships[index];
                if (group != null && group.Members.Count >= 2)
                {
                    // 論理 ZIP の出現位置は群中で最初に登場した物理入力の位置とする。
                    // 断片は離れて選ばれていても同じ群に集め、結合順だけを P で決める。
                    if (emitted.Add(group) && group.Archive != null) archives.Add(group.Archive);
                }
                else
                {
                    standaloneCount++;
                    if (singles[index] != null) archives.Add(singles[index]);
                    else valid = false;
                }
            }
            // 正常入力では膨大なファイル名一覧を連結した診断文字列を作らない。
            // 失敗時だけ、各群の順序・長さ・検査理由と、取り残された入力を詳述する。
            problem = null;
            if (!valid)
            {
                StringBuilder details = new StringBuilder();
                foreach (NamedGroup group in groups)
                {
                    if (group.Members.Count < 2) continue;
                    details.Append("  推定群 '").Append(group.Stem).Append("' / ")
                        .Append(group.Members.Count).Append(" 断片 / P の ASCII 大文字小文字区別順: ")
                        .Append(PartList(group.Ordered)).Append("\n    ");
                    if (group.Archive == null)
                        details.Append("不整合: ").Append(group.Problem)
                            .Append("。断片の欠落・誤選択・命名規則と実際の連結順を確認してください。");
                    else details.Append("仮想連結した 1 個の ZIP として全構造が整合。");
                    details.Append('\n');
                }
                for (int index = 0; index < parts.Count; index++)
                {
                    NamedGroup group = memberships[index];
                    if ((group == null || group.Members.Count < 2) && singles[index] == null)
                    {
                        details.Append("  群外の入力 '").Append(Path.GetFileName(parts[index].PathName))
                            .Append("': 単独 ZIP として不整合: ").Append(singleProblems[index]);
                        if (group != null)
                            details.Append("。同じ Q.zip の相手がない 1 個だけの断片候補です");
                        details.Append('\n');
                    }
                }
                problem = details.ToString();
                return null;
            }
            return new DetectedArchives
            {
                Mode = "ハイブリッドモード（分割 ZIP 群 " + splitCount.ToString(CultureInfo.InvariantCulture)
                    + " 個 / 単独 ZIP " + standaloneCount.ToString(CultureInfo.InvariantCulture) + " 個）",
                Archives = archives
            };
        }

        /// <summary>
        /// P の命名条件違反を、実際の名前・長さとともに報告する。連番の始点や文字種の進み方は
        /// 指定されていないため推測しない。欠落は結合後の ZIP 構造によって検出する。
        /// </summary>
        /// <param name="group">P 順の断片を持つ推定群。</param>
        /// <returns>命名条件に違反する詳細。違反がなければ null。</returns>
        private static string NamingProblem(NamedGroup group)
        {
            List<string> errors = new List<string>();
            HashSet<string> suffixes = new HashSet<string>(StringComparer.Ordinal);
            int expectedLength = group.Members[0].Suffix.Length;
            foreach (NamedPart member in group.Members)
            {
                string suffix = member.Suffix;
                bool ascii = suffix.Length != 0;
                for (int index = 0; index < suffix.Length; index++)
                {
                    char value = suffix[index];
                    if (!((value >= '0' && value <= '9') || (value >= 'A' && value <= 'Z')
                        || (value >= 'a' && value <= 'z'))) ascii = false;
                }
                bool duplicate = !suffixes.Add(suffix);
                if (ascii && suffix.Length == expectedLength && !duplicate) continue;
                string label = Path.GetFileName(member.Part.PathName) + " (P='" + suffix + "', "
                    + suffix.Length.ToString(CultureInfo.InvariantCulture) + " 文字)";
                if (!ascii) errors.Add(label + ": P は空でない半角英数字のみである必要があります");
                if (suffix.Length != expectedLength)
                    errors.Add(label + ": 同一 Q 群で P の文字数が不一致です（基準 "
                        + expectedLength.ToString(CultureInfo.InvariantCulture) + " 文字）");
                if (duplicate) errors.Add(label + ": 同一 Q 群で P が重複しています");
            }
            return errors.Count == 0 ? null : String.Join(" / ", errors.ToArray());
        }

        /// <summary>同一の物理断片を同じ順で参照するか。ファイル名だけの一致判定は行わない。</summary>
        /// <param name="left">比較する一方の断片順序。</param>
        /// <param name="right">比較する他方の断片順序。</param>
        /// <returns>同じ物理入力オブジェクトが同じ順なら true。</returns>
        private static bool SameParts(IList<SourcePart> left, IList<SourcePart> right)
        {
            if (left.Count != right.Count) return false;
            for (int index = 0; index < left.Count; index++)
                if (!Object.ReferenceEquals(left[index], right[index])) return false;
            return true;
        }

        /// <summary>
        /// 「全体連結」と「1 群だけの群別連結」が同じ構成なら 1 解釈であり、曖昧性ではない。
        /// 一方、論理 ZIP の境界や各 ZIP 内の断片順が違う完全候補は両方残して後で拒否する。
        /// </summary>
        /// <param name="candidates">完全に整合する、重複除去済みの既存候補。</param>
        /// <param name="added">追加する完全候補。同一解釈があれば追加しない。</param>
        private static void AddCandidate(List<DetectedArchives> candidates, DetectedArchives added)
        {
            foreach (DetectedArchives candidate in candidates)
            {
                if (candidate.Archives.Count != added.Archives.Count) continue;
                bool same = true;
                for (int index = 0; index < candidate.Archives.Count; index++)
                    if (!SameParts(candidate.Archives[index].Parts, added.Archives[index].Parts)) { same = false; break; }
                if (same) return;
            }
            candidates.Add(added);
        }

        /// <summary>問題のある群や曖昧な候補の断片順と長さを、診断用文字列にする。</summary>
        /// <param name="parts">候補の結合順に並んだ断片。</param>
        /// <returns>各物理名と bytes を含む順序付きの説明。</returns>
        private static string PartList(IList<SourcePart> parts)
        {
            StringBuilder result = new StringBuilder();
            for (int index = 0; index < parts.Count; index++)
            {
                if (index != 0) result.Append(" + ");
                result.Append('[').Append(Path.GetFileName(parts[index].PathName)).Append(", ")
                    .Append(parts[index].Length.ToString(CultureInfo.InvariantCulture)).Append(" bytes]");
            }
            return result.ToString();
        }

        /// <summary>既存の読取器で全構造を確認し、形式上の不成立を候補の失敗理由にする。</summary>
        /// <param name="parts">一つの ZIP として検証する物理断片順序。</param>
        /// <param name="archive">成功時の構造検証済み ZIP。失敗時は null。</param>
        /// <param name="problem">形式不一致の詳細。成功時は null。</param>
        /// <returns>ZIP の全構造検査が成功したとき true。</returns>
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
                    Stream.Dispose();
                    streamClosed = true;
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
        private readonly Dictionary<string, List<ZipEntry>> groups = new Dictionary<string, List<ZipEntry>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<List<ZipEntry>> groupOrder = new List<List<ZipEntry>>();
        // キーは採用する差分エントリ。値は、差分の失敗時だけ復元を試す省略済みベースライン。
        internal readonly Dictionary<ZipEntry, ZipEntry> Fallbacks = new Dictionary<ZipEntry, ZipEntry>();
        internal long DuplicateCount;
        internal long FileCount
        {
            get { long n = 0; foreach (ZipEntry e in Entries) if (!e.IsDirectory && e.Keep) n++; return n; }
        }

        /// <summary>物理出力先を参照せず、全入力パス・危険物・暗黙の親・入力内の衝突を検査する。</summary>
        /// <param name="archives">構造検証を終えた論理 ZIP。順序を保持する。</param>
        /// <returns>差分照合にも通常展開にも使用できる仮想展開計画。</returns>
        internal static ExtractionPlan Prepare(List<ZipArchiveData> archives)
        {
            ExtractionPlan plan = new ExtractionPlan();
            foreach (ZipArchiveData archive in archives)
            {
                foreach (ZipEntry entry in archive.Entries)
                {
                    Program.CheckCancel();
                    if (entry.SpecialObject != null) throw new SafetyException("危険な ZIP オブジェクト: " + entry.SpecialObject + " / " + entry.Label);
                    // 採用名だけでなく、Unicode extra に隠された旧式名・ローカル名の遡りも拒否する。
                    entry.Relative = WindowsPaths.Relative(entry.Name, entry.IsDirectory);
                    WindowsPaths.Relative(entry.RawDecodedName, entry.IsDirectory);
                    WindowsPaths.Relative(entry.LocalDecodedName, entry.IsDirectory);
                    plan.AddEntry(entry, false);
                }
            }
            plan.CheckCollisions(false);
            return plan;
        }

        /// <summary>従来の通常展開用 API。仮想検査に続き、出力先を作成せず検査する。</summary>
        internal static ExtractionPlan Build(List<ZipArchiveData> archives, SafeRoot root, SourceSet inputs, WarningBook warnings)
        {
            ExtractionPlan plan = Prepare(archives);
            plan.BindDestination(root, new SourceSet[] { inputs });
            return plan;
        }

        /// <summary>再配置が完了した全パスを事前検査し、どちらの入力 ZIP への上書きも防ぐ。</summary>
        /// <param name="root">ユーザーが指定した保存先。配下へのリンク越境は既存処理で拒否する。</param>
        /// <param name="inputs">通常なら一群、差分なら独立した二群の入力ハンドル所有者。</param>
        internal void BindDestination(SafeRoot root, IList<SourceSet> inputs)
        {
            HashSet<string> identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (SourceSet set in inputs)
                foreach (SourcePart part in set.Parts)
                    if (part.Identity != null) identities.Add(part.Identity);
            foreach (ZipEntry entry in Entries)
            {
                Program.CheckCancel();
                try
                {
                    FileStamp existing = root.Probe(entry.Relative);
                    if (existing != null && entry.IsDirectory != existing.IsDirectory)
                        entry.PlanError = "展開先のファイル / ディレクトリ種別が衝突しています: " + root.Destination(entry.Relative);
                    if (existing != null && !entry.IsDirectory && existing.Identity != null && identities.Contains(existing.Identity))
                        entry.PlanError = "入力元 ZIP 自身への上書きは許可しません: " + root.Destination(entry.Relative);
                }
                catch (Exception ex)
                {
                    if (!Program.Recoverable(ex)) throw;
                    entry.PlanError = "展開先の事前検査失敗: " + ex.Message;
                }
                ZipEntry fallback;
                if (Fallbacks.TryGetValue(entry, out fallback)) fallback.PlanError = entry.PlanError;
            }
        }

        /// <summary>正規化済み出力パスの一エントリを追加する。元 ZIP メタデータは保持する。</summary>
        /// <param name="entry">Relative だけを必要に応じて再配置した元エントリ。</param>
        /// <param name="replaceDirectoryTimes">差分の明示ディレクトリ日時を優先する場合 true。</param>
        internal void AddEntry(ZipEntry entry, bool replaceDirectoryTimes)
        {
            Entries.Add(entry);
            if (entry.IsDirectory)
            {
                DirectoryPlan directory = GetDirectory(entry.Relative);
                if (directory != null && (directory.ExplicitTimes == null || !directory.ExplicitTimes.ModifiedUtc.HasValue
                    || (replaceDirectoryTimes && entry.Times.ModifiedUtc.HasValue)))
                    directory.ExplicitTimes = entry.Times;
                AddParents(entry.Relative);
            }
            else
            {
                List<ZipEntry> group;
                if (!groups.TryGetValue(entry.Relative, out group))
                {
                    group = new List<ZipEntry>(); groups.Add(entry.Relative, group); groupOrder.Add(group);
                }
                group.Add(entry);
                AddParents(entry.Relative);
            }
        }

        /// <summary>仮想ファイル/親ディレクトリの衝突を拒否し、必要なら再配置で生じた同名ファイルも拒否する。</summary>
        /// <param name="requireUniqueFiles">入力ごとの重複確認を済ませた差分合成では true。</param>
        internal void CheckCollisions(bool requireUniqueFiles)
        {
            foreach (KeyValuePair<string, List<ZipEntry>> pair in groups)
            {
                Program.CheckCancel();
                if (Directories.ContainsKey(pair.Key))
                    throw new InvalidDataException("ZIP 内でファイルとディレクトリ（または親ディレクトリ）が衝突します: " + pair.Key
                        + " / " + pair.Value[0].Label);
                if (requireUniqueFiles && pair.Value.Count > 1)
                    throw new InvalidDataException("差分再配置後に想定外のファイル名衝突があります: " + pair.Key
                        + " / " + pair.Value[0].Label + " / " + pair.Value[1].Label);
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
        /// <summary>Y は以後すべての重複を許可、y は当該組だけ、n は全体中断。いずれも入力群内で先勝ち。</summary>
        internal void ConfirmDuplicates()
        {
            bool allowAll = false;
            foreach (List<ZipEntry> group in groupOrder)
            {
                Program.CheckCancel();
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
        /// <summary>重複除去・差分再配置後の採用エントリについて全子孫の最新日付を集計する。</summary>
        internal void BuildDirectoryTimes()
        {
            foreach (ZipEntry entry in Entries)
            {
                Program.CheckCancel();
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

    /// <summary>基準候補ディレクトリと、その自身を除く元ツリーの子孫統計。</summary>
    internal sealed class DirectoryNode
    {
        internal string Relative;
        internal int Depth;
        internal long FileCount, DirectoryCount;
        internal decimal Bytes;
        internal DirectoryNode Parent;
        internal List<MatchingTreeChild> Children;
        internal MatchingShape Shape;
    }

    /// <summary>仮想ディレクトリの直下要素。Directory が null ならファイル。</summary>
    internal struct MatchingTreeChild
    {
        internal int Token;
        internal DirectoryNode Directory;
    }

    /// <summary>二つの ZIP 群から厳密に求めた基準ディレクトリと一致件数。</summary>
    internal sealed class ReferenceMatch
    {
        internal DirectoryNode BaselineRoot, DeltaRoot;
        internal DirectoryNode BaselineTreeRoot, DeltaTreeRoot;
        internal long MatchedFiles, MatchedDirectories;
    }

    /// <summary>名前と種別を共通の整数に変換し、各側での出現を記録する。</summary>
    internal sealed class MatchingTokens
    {
        private readonly Dictionary<string, int> names = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly List<byte> presence = new List<byte>();

        /// <summary>Windows の既存パス比較規則に従って名前を整数化する。</summary>
        /// <param name="name">正規化済みの一つのパス要素。</param>
        /// <param name="directory">ディレクトリなら true。</param>
        /// <param name="side">ベースラインは 1、差分は 2。</param>
        /// <returns>名前と種別の両方が同じときだけ一致する整数。</returns>
        internal int Register(string name, bool directory, int side)
        {
            int id;
            if (!names.TryGetValue(name, out id))
            {
                id = names.Count;
                names.Add(name, id);
                presence.Add(0);
                presence.Add(0);
            }
            int token = checked(id * 2 + (directory ? 1 : 0));
            presence[token] = (byte)(presence[token] | side);
            return token;
        }

        /// <summary>当該名前・種別が両方のツリーに一度以上現れるか調べる。</summary>
        /// <param name="token">Register が返した整数。</param>
        /// <returns>一致に寄与する可能性があれば true。</returns>
        internal bool IsShared(int token) { return presence[token] == 3; }
    }

    /// <summary>採用エントリから、暗黙ディレクトリも補った元ツリーを構成する。</summary>
    internal sealed class MatchingTree
    {
        internal readonly DirectoryNode Root = new DirectoryNode { Relative = "", Depth = 0 };
        internal readonly List<DirectoryNode> Nodes = new List<DirectoryNode>();
        private Dictionary<string, DirectoryNode> directories = new Dictionary<string, DirectoryNode>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>空の仮想ルートを作る。</summary>
        private MatchingTree()
        {
            Nodes.Add(Root);
            directories.Add("", Root);
        }

        /// <summary>重複除去済みの計画を読み、元の件数と非圧縮 bytes を集計する。</summary>
        /// <param name="plan">Relative と Keep が確定した独立 ZIP 群の計画。</param>
        /// <param name="tokens">二つのツリーで共有する名前表。</param>
        /// <param name="side">ベースラインは 1、差分は 2。</param>
        /// <returns>候補ディレクトリ一覧と全体統計を持つツリー。</returns>
        internal static MatchingTree Build(ExtractionPlan plan, MatchingTokens tokens, int side)
        {
            MatchingTree tree = new MatchingTree();
            foreach (ZipEntry entry in plan.Entries)
            {
                Program.CheckCancel();
                if (!entry.Keep) continue;
                if (entry.Relative == null) throw new InvalidOperationException("基準ディレクトリ計算前の ZIP パス正規化が未完了です。");
                if (entry.IsDirectory)
                {
                    tree.EnsureDirectory(entry.Relative, tokens, side);
                    continue;
                }
                if (tree.directories.ContainsKey(entry.Relative))
                    throw new InvalidDataException("ZIP 内でファイルとディレクトリが衝突しています: " + Text.Safe(entry.Relative));
                if (!tree.files.Add(entry.Relative)) continue; // 同一正規化パスを構造上で二重計数しない。
                DirectoryNode parent = tree.EnsureDirectory(WindowsPaths.Parent(entry.Relative), tokens, side);
                int slash = entry.Relative.LastIndexOf('\\');
                string name = entry.Relative.Substring(slash + 1);
                AddChild(parent, new MatchingTreeChild { Token = tokens.Register(name, false, side), Directory = null });
                parent.FileCount++;
                parent.Bytes += entry.Size;
            }
            // 親を先に作った一覧を逆順にたどるため、深い ZIP でも再帰スタックを使わない。
            for (int i = tree.Nodes.Count - 1; i > 0; i--)
            {
                if ((i & 2047) == 0) Program.CheckCancel();
                DirectoryNode node = tree.Nodes[i];
                node.Parent.FileCount += node.FileCount;
                node.Parent.DirectoryCount += node.DirectoryCount;
                node.Parent.Bytes += node.Bytes;
            }
            tree.directories = null;
            tree.files = null;
            return tree;
        }

        /// <summary>不足している祖先を反復処理で補い、最初に現れた表記を保持する。</summary>
        /// <param name="relative">正規化済みの相対ディレクトリパス。</param>
        /// <param name="tokens">二つのツリーの共通名前表。</param>
        /// <param name="side">当該 ZIP 群を表す 1 または 2。</param>
        /// <returns>存在済み、または新規に補ったディレクトリ。</returns>
        private DirectoryNode EnsureDirectory(string relative, MatchingTokens tokens, int side)
        {
            DirectoryNode existing;
            if (directories.TryGetValue(relative, out existing)) return existing;
            List<int> missingEnds = new List<int>();
            int end = relative.Length;
            while (true)
            {
                string prefix = relative.Substring(0, end);
                if (directories.TryGetValue(prefix, out existing)) break;
                if (files.Contains(prefix))
                    throw new InvalidDataException("ZIP 内でファイルが親ディレクトリにも使われています: " + Text.Safe(prefix));
                missingEnds.Add(end);
                int slash = relative.LastIndexOf('\\', end - 1);
                end = slash < 0 ? 0 : slash;
            }
            for (int i = missingEnds.Count - 1; i >= 0; i--)
            {
                Program.CheckCancel();
                end = missingEnds[i];
                int slash = relative.LastIndexOf('\\', end - 1);
                string name = relative.Substring(slash + 1, end - slash - 1);
                DirectoryNode created = new DirectoryNode
                {
                    Relative = existing.Relative.Length == 0 ? name : existing.Relative + "\\" + name,
                    Depth = checked(existing.Depth + 1),
                    Parent = existing
                };
                directories.Add(created.Relative, created);
                Nodes.Add(created);
                AddChild(existing, new MatchingTreeChild { Token = tokens.Register(name, true, side), Directory = created });
                existing.DirectoryCount++;
                existing = created;
            }
            return existing;
        }

        /// <summary>直下要素用の小さな一覧を必要なディレクトリだけに割り当てる。</summary>
        /// <param name="parent">追加先のディレクトリ。</param>
        /// <param name="child">追加するファイルまたはディレクトリ。</param>
        private static void AddChild(DirectoryNode parent, MatchingTreeChild child)
        {
            if (parent.Children == null) parent.Children = new List<MatchingTreeChild>();
            parent.Children.Add(child);
        }
    }

    /// <summary>照合用部分木の一つの子。ShapeId=-1 はファイルを表す。</summary>
    internal struct MatchingShapeChild
    {
        internal int Token, ShapeId;
    }

    /// <summary>同一構造を共有する不変の部分木。件数は投影後の照合用件数。</summary>
    internal sealed class MatchingShape
    {
        internal int Id, SignatureHash;
        internal MatchingShapeChild[] Children;
        internal long Files, Directories;
        internal long Total { get { return Files + Directories; } }
    }

    /// <summary>ハッシュが一致しても子配列全体を比較し、衝突による誤一致を防ぐ。</summary>
    internal sealed class MatchingShapeComparer : IEqualityComparer<MatchingShape>
    {
        /// <summary>二つの正規化済み子配列を厳密に比較する。</summary>
        /// <param name="left">比較する一方の構造。</param>
        /// <param name="right">比較する他方の構造。</param>
        /// <returns>名前・種別・下位構造がすべて等しいとき true。</returns>
        public bool Equals(MatchingShape left, MatchingShape right)
        {
            if (Object.ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.Children.Length != right.Children.Length) return false;
            for (int i = 0; i < left.Children.Length; i++)
                if (left.Children[i].Token != right.Children[i].Token || left.Children[i].ShapeId != right.Children[i].ShapeId) return false;
            return true;
        }

        /// <summary>厳密比較に先立つ辞書の振り分け用ハッシュを返す。</summary>
        /// <param name="shape">不変の照合用部分木。</param>
        /// <returns>子配列から計算済みのハッシュ値。</returns>
        public int GetHashCode(MatchingShape shape) { return shape.SignatureHash; }
    }

    /// <summary>同じ照合用構造を持つ候補を最浅の出現位置で代表させる。</summary>
    internal sealed class MatchingGroup
    {
        internal MatchingShape Shape;
        internal DirectoryNode First, Second;

        /// <summary>より浅い代表へ更新し、同じ深さの別候補を一つ保持する。</summary>
        /// <param name="node">同じ構造に属する元ツリーのディレクトリ。</param>
        internal void Include(DirectoryNode node)
        {
            if (First == null || node.Depth < First.Depth) { First = node; Second = null; }
            else if (node.Depth == First.Depth && !Object.ReferenceEquals(node, First) && Second == null) Second = node;
        }
    }

    /// <summary>同じ直下要素を持つ構造群と、一括枝刈り用の安全な上界。</summary>
    internal sealed class MatchingPosting
    {
        internal readonly List<MatchingGroup> Groups = new List<MatchingGroup>();
        internal long MaximumFiles, MaximumDirectories;
        internal int MinimumDepth = Int32.MaxValue;

        /// <summary>一つの構造を加え、個別走査せず使える保守的な要約を更新する。</summary>
        /// <param name="group">当該名前・種別の直下要素を持つ候補群。</param>
        internal void Add(MatchingGroup group)
        {
            Groups.Add(group);
            MaximumFiles = Math.Max(MaximumFiles, group.Shape.Files);
            MaximumDirectories = Math.Max(MaximumDirectories, group.Shape.Directories);
            MinimumDepth = Math.Min(MinimumDepth, group.First.Depth);
        }
    }

    /// <summary>共有されない名前を安全に投影除外し、残った部分木を厳密に同一化する。</summary>
    internal sealed class MatchingShapes
    {
        internal readonly List<MatchingShape> Items = new List<MatchingShape>();
        private readonly Dictionary<MatchingShape, MatchingShape> interned = new Dictionary<MatchingShape, MatchingShape>(new MatchingShapeComparer());

        /// <summary>各候補を下から処理して、再利用可能な有向非巡回グラフを構築する。</summary>
        /// <param name="tree">元の統計を保持したツリー。</param>
        /// <param name="tokens">両側の出現が確定した名前表。</param>
        /// <returns>部分木 ID ごとの最浅代表候補。</returns>
        internal Dictionary<int, MatchingGroup> Build(MatchingTree tree, MatchingTokens tokens)
        {
            Dictionary<int, MatchingGroup> groups = new Dictionary<int, MatchingGroup>();
            for (int i = tree.Nodes.Count - 1; i >= 0; i--)
            {
                Program.CheckCancel();
                DirectoryNode node = tree.Nodes[i];
                List<MatchingShapeChild> children = new List<MatchingShapeChild>();
                if (node.Children != null)
                {
                    foreach (MatchingTreeChild child in node.Children)
                    {
                        if (!tokens.IsShared(child.Token)) continue;
                        children.Add(new MatchingShapeChild { Token = child.Token, ShapeId = child.Directory == null ? -1 : child.Directory.Shape.Id });
                    }
                }
                children.Sort(delegate (MatchingShapeChild a, MatchingShapeChild b) { return a.Token.CompareTo(b.Token); });
                MatchingShape proposal = new MatchingShape { Children = children.ToArray() };
                int hash = 17;
                foreach (MatchingShapeChild child in proposal.Children)
                {
                    unchecked { hash = (hash * 31 + child.Token) * 31 + child.ShapeId; }
                    if (child.ShapeId < 0) proposal.Files++;
                    else
                    {
                        MatchingShape lower = Items[child.ShapeId];
                        proposal.Files += lower.Files;
                        proposal.Directories += 1 + lower.Directories;
                    }
                }
                proposal.SignatureHash = hash;
                MatchingShape shape;
                if (!interned.TryGetValue(proposal, out shape))
                {
                    shape = proposal;
                    shape.Id = Items.Count;
                    Items.Add(shape);
                    interned.Add(shape, shape);
                }
                node.Shape = shape;
                // 元の件数・bytes は Node に残す。照合に不要となった直下配列は解放可能にする。
                node.Children = null;
                MatchingGroup group;
                if (!groups.TryGetValue(shape.Id, out group))
                {
                    group = new MatchingGroup { Shape = shape };
                    groups.Add(shape.Id, group);
                }
                group.Include(node);
            }
            return groups;
        }
    }

    /// <summary>ファイル一致とディレクトリ一致を分けた厳密な交差件数。</summary>
    internal struct MatchingCounts
    {
        internal long Files, Directories;
        internal long Total { get { return Files + Directories; } }
    }

    /// <summary>固定容量の交差計算キャッシュ。追い出し時は必要になれば再計算する。</summary>
    internal struct MatchingCacheItem
    {
        internal ulong Key;
        internal MatchingCounts Counts;
    }

    /// <summary>再帰呼び出しを置き換える、部分木の交差計算用フレーム。</summary>
    internal struct MatchingFrame
    {
        internal int Left, Right, LeftIndex, RightIndex;
        internal MatchingCounts Counts;
    }

    /// <summary>同名同種別の直下子だけを降下して、相対パスの完全一致件数を得る。</summary>
    internal sealed class MatchingIntersection
    {
        private readonly List<MatchingShape> shapes;
        // 候補対を全件保存しない。上限は計算精度には一切影響せず、再計算回数だけに影響する。
        private readonly MatchingCacheItem[] cache = new MatchingCacheItem[262144];
        private readonly List<MatchingFrame> stack = new List<MatchingFrame>();
        private long steps;

        /// <summary>不変部分木と固定容量キャッシュを用意する。</summary>
        /// <param name="items">両側に共通の部分木 ID 表。</param>
        internal MatchingIntersection(List<MatchingShape> items) { shapes = items; }

        /// <summary>二つの候補の相対パス集合の交差を反復処理で厳密計算する。</summary>
        /// <param name="left">一方の部分木 ID。</param>
        /// <param name="right">他方の部分木 ID。</param>
        /// <returns>相対パスと種別の両方が一致したファイル数とディレクトリ数。</returns>
        internal MatchingCounts Count(int left, int right)
        {
            Program.CheckCancel();
            MatchingCounts known;
            if (TryKnown(left, right, out known)) return known;
            stack.Clear();
            stack.Add(new MatchingFrame { Left = left, Right = right });
            while (stack.Count != 0)
            {
                int top = stack.Count - 1;
                MatchingFrame frame = stack[top];
                MatchingShapeChild[] a = shapes[frame.Left].Children, b = shapes[frame.Right].Children;
                bool descended = false;
                while (frame.LeftIndex < a.Length && frame.RightIndex < b.Length)
                {
                    if ((++steps & 2047) == 0) Program.CheckCancel();
                    MatchingShapeChild ca = a[frame.LeftIndex], cb = b[frame.RightIndex];
                    if (ca.Token < cb.Token) { frame.LeftIndex++; continue; }
                    if (ca.Token > cb.Token) { frame.RightIndex++; continue; }
                    frame.LeftIndex++;
                    frame.RightIndex++;
                    if (ca.ShapeId < 0) { frame.Counts.Files++; continue; }
                    frame.Counts.Directories++;
                    if (TryKnown(ca.ShapeId, cb.ShapeId, out known))
                    {
                        frame.Counts.Files += known.Files;
                        frame.Counts.Directories += known.Directories;
                    }
                    else
                    {
                        stack[top] = frame;
                        stack.Add(new MatchingFrame { Left = ca.ShapeId, Right = cb.ShapeId });
                        descended = true;
                        break;
                    }
                }
                if (descended) continue;
                Remember(frame.Left, frame.Right, frame.Counts);
                stack.RemoveAt(top);
                if (top == 0) return frame.Counts;
                MatchingFrame parent = stack[top - 1];
                parent.Counts.Files += frame.Counts.Files;
                parent.Counts.Directories += frame.Counts.Directories;
                stack[top - 1] = parent;
            }
            throw new InvalidOperationException("基準ディレクトリの交差計算が完了しませんでした。");
        }

        /// <summary>同一構造・空構造・キャッシュ済みの交差を取得する。</summary>
        /// <param name="left">一方の部分木 ID。</param>
        /// <param name="right">他方の部分木 ID。</param>
        /// <param name="counts">計算済みなら厳密な交差件数。</param>
        /// <returns>追加の降下が不要なら true。</returns>
        private bool TryKnown(int left, int right, out MatchingCounts counts)
        {
            counts = new MatchingCounts();
            if (left == right)
            {
                counts.Files = shapes[left].Files;
                counts.Directories = shapes[left].Directories;
                return true;
            }
            if (shapes[left].Total == 0 || shapes[right].Total == 0) return true;
            ulong key = PairKey(left, right);
            MatchingCacheItem item = cache[CacheIndex(key)];
            if (item.Key != key) return false;
            counts = item.Counts;
            return true;
        }

        /// <summary>計算結果を固定容量キャッシュの一枠に保持する。</summary>
        /// <param name="left">一方の部分木 ID。</param>
        /// <param name="right">他方の部分木 ID。</param>
        /// <param name="counts">厳密に計算済みの交差件数。</param>
        private void Remember(int left, int right, MatchingCounts counts)
        {
            ulong key = PairKey(left, right);
            cache[CacheIndex(key)] = new MatchingCacheItem { Key = key, Counts = counts };
        }

        /// <summary>交差の対称性を利用して、順序によらない整数キーを作る。</summary>
        /// <param name="left">一方の部分木 ID。</param>
        /// <param name="right">他方の部分木 ID。</param>
        /// <returns>未使用値の 0 と重ならない一意なキー。</returns>
        private static ulong PairKey(int left, int right)
        {
            if (left > right) { int swap = left; left = right; right = swap; }
            return (((ulong)(uint)left << 32) | (uint)right) + 1UL;
        }

        /// <summary>一意キーをキャッシュの枠番号へ振り分ける。</summary>
        /// <param name="key">PairKey が返した非ゼロ値。</param>
        /// <returns>固定容量配列の有効な添字。</returns>
        private static int CacheIndex(ulong key)
        {
            unchecked
            {
                key ^= key >> 33;
                key *= 0xff51afd7ed558ccdUL;
                key ^= key >> 33;
                return (int)(key & 262143UL);
            }
        }
    }

    /// <summary>最大一致、次いで最小深さ合計を選び、同点候補は詳細付きで拒否する。</summary>
    internal sealed class MatchingBest
    {
        internal DirectoryNode Baseline, Delta, OtherBaseline, OtherDelta;
        internal MatchingCounts Counts, OtherCounts;
        internal long DepthSum = Int64.MaxValue;

        /// <summary>上界と最浅の深さから、改善または同点検出の可能性を判定する。</summary>
        /// <param name="baseline">ベースライン側の同一構造候補群。</param>
        /// <param name="delta">差分側の同一構造候補群。</param>
        /// <returns>厳密な交差を調べる必要があれば true。</returns>
        internal bool CanCompete(MatchingGroup baseline, MatchingGroup delta)
        {
            long bound = Math.Min(baseline.Shape.Files, delta.Shape.Files) + Math.Min(baseline.Shape.Directories, delta.Shape.Directories);
            long depth = (long)baseline.First.Depth + delta.First.Depth;
            return CanCompete(bound, depth);
        }

        /// <summary>転置索引の一覧全体を、その保守的上界だけで省略可能か調べる。</summary>
        /// <param name="baseline">ベースライン側の候補群。</param>
        /// <param name="posting">同じ直下要素を持つ差分候補一覧の要約。</param>
        /// <returns>一覧内に改善または未検出の同点が残り得るなら true。</returns>
        internal bool CanCompete(MatchingGroup baseline, MatchingPosting posting)
        {
            long bound = Math.Min(baseline.Shape.Files, posting.MaximumFiles) + Math.Min(baseline.Shape.Directories, posting.MaximumDirectories);
            long depth = (long)baseline.First.Depth + posting.MinimumDepth;
            return CanCompete(bound, depth);
        }

        /// <summary>最良値の改善と、まだ必要な同点検出に絞った共通判定を行う。</summary>
        /// <param name="bound">候補集合の一致件数の上界。</param>
        /// <param name="depth">候補集合の深さ合計の下界。</param>
        /// <returns>調査を続ける必要があれば true。</returns>
        private bool CanCompete(long bound, long depth)
        {
            if (bound <= 0 || bound < Counts.Total) return false;
            if (bound > Counts.Total) return true;
            if (depth < DepthSum) return true;
            return depth == DepthSum && OtherBaseline == null;
        }

        /// <summary>厳密な交差件数を使い、最良候補と曖昧性の証拠を更新する。</summary>
        /// <param name="baseline">ベースライン側の候補群。</param>
        /// <param name="delta">差分側の候補群。</param>
        /// <param name="counts">この二群に共通の厳密な交差件数。</param>
        internal void Consider(MatchingGroup baseline, MatchingGroup delta, MatchingCounts counts)
        {
            if (counts.Total == 0) return;
            long depth = (long)baseline.First.Depth + delta.First.Depth;
            if (counts.Total < Counts.Total || (counts.Total == Counts.Total && depth > DepthSum)) return;
            if (counts.Total > Counts.Total || depth < DepthSum)
            {
                Baseline = baseline.First;
                Delta = delta.First;
                Counts = counts;
                DepthSum = depth;
                OtherBaseline = OtherDelta = null;
                if (baseline.Second != null) { OtherBaseline = baseline.Second; OtherDelta = delta.First; OtherCounts = counts; }
                else if (delta.Second != null) { OtherBaseline = baseline.First; OtherDelta = delta.Second; OtherCounts = counts; }
            }
            else if (OtherBaseline == null && (!Object.ReferenceEquals(Baseline, baseline.First) || !Object.ReferenceEquals(Delta, delta.First)))
            {
                OtherBaseline = baseline.First;
                OtherDelta = delta.First;
                OtherCounts = counts;
            }
        }
    }

    /// <summary>全ディレクトリ対を保持せず、正の一致を持ち得る構造対だけを厳密探索する。</summary>
    internal static class DirectoryMatcher
    {
        /// <summary>二つの独立した採用済み ZIP ツリーから最良の基準を求める。</summary>
        /// <param name="baseline">ベースライン ZIP 群の正規化・重複処理済み計画。</param>
        /// <param name="delta">差分 ZIP 群の正規化・重複処理済み計画。</param>
        /// <returns>唯一の最大一致・最浅候補と、投影前の元統計。</returns>
        internal static ReferenceMatch Find(ExtractionPlan baseline, ExtractionPlan delta)
        {
            MatchingTokens tokens = new MatchingTokens();
            MatchingTree baseTree = MatchingTree.Build(baseline, tokens, 1);
            MatchingTree deltaTree = MatchingTree.Build(delta, tokens, 2);
            MatchingShapes shapes = new MatchingShapes();
            Dictionary<int, MatchingGroup> baseGroups = shapes.Build(baseTree, tokens);
            Dictionary<int, MatchingGroup> deltaGroups = shapes.Build(deltaTree, tokens);
            MatchingIntersection intersection = new MatchingIntersection(shapes.Items);
            MatchingBest best = new MatchingBest();

            // 完全に同じ投影後構造は、下位を比較せず件数が確定する。強い初期下界になる。
            foreach (KeyValuePair<int, MatchingGroup> pair in baseGroups)
            {
                Program.CheckCancel();
                MatchingGroup other;
                if (deltaGroups.TryGetValue(pair.Key, out other) && best.CanCompete(pair.Value, other))
                    best.Consider(pair.Value, other, new MatchingCounts { Files = pair.Value.Shape.Files, Directories = pair.Value.Shape.Directories });
            }

            // 子の名前＋種別から、その子を持つ差分側の「異なる構造」だけを引ける転置索引。
            Dictionary<int, MatchingPosting> postings = new Dictionary<int, MatchingPosting>();
            foreach (MatchingGroup group in deltaGroups.Values)
            {
                Program.CheckCancel();
                foreach (MatchingShapeChild child in group.Shape.Children)
                {
                    MatchingPosting posting;
                    if (!postings.TryGetValue(child.Token, out posting)) { posting = new MatchingPosting(); postings.Add(child.Token, posting); }
                    posting.Add(group);
                }
            }
            List<MatchingGroup> ordered = new List<MatchingGroup>(baseGroups.Values);
            ordered.Sort(CompareGroups);
            foreach (MatchingPosting posting in postings.Values) posting.Groups.Sort(CompareGroups);

            // 一つのベースライン構造につき差分構造を一度だけ調べる。全候補対の集合は作らない。
            int[] visited = new int[shapes.Items.Count];
            int generation = 0;
            long enumerated = 0;
            foreach (MatchingGroup group in ordered)
            {
                Program.CheckCancel();
                if (group.Shape.Total < best.Counts.Total) continue;
                if (generation == Int32.MaxValue) { Array.Clear(visited, 0, visited.Length); generation = 0; }
                generation++;
                foreach (MatchingShapeChild child in group.Shape.Children)
                {
                    MatchingPosting posting;
                    if (!postings.TryGetValue(child.Token, out posting) || !best.CanCompete(group, posting)) continue;
                    foreach (MatchingGroup other in posting.Groups)
                    {
                        if ((++enumerated & 2047) == 0) Program.CheckCancel();
                        int id = other.Shape.Id;
                        if (visited[id] == generation) continue;
                        visited[id] = generation;
                        if (id == group.Shape.Id || !best.CanCompete(group, other)) continue;
                        best.Consider(group, other, intersection.Count(group.Shape.Id, id));
                    }
                }
            }

            if (best.Baseline == null)
                throw new InvalidDataException("基準ディレクトリを自動解決できません。相対パスと種別が一致するファイルまたはディレクトリがありません。\n"
                    + "ベースライン全体: " + Statistics(baseTree.Root) + "\n差分全体: " + Statistics(deltaTree.Root));
            if (best.OtherBaseline != null)
                throw new InvalidDataException("基準ディレクトリを自動解決できません。最大一致数 " + best.Counts.Total.ToString("N0", CultureInfo.InvariantCulture)
                    + "、最小深さ合計 " + best.DepthSum.ToString(CultureInfo.InvariantCulture) + " の候補が少なくとも 2 組あり、判然としません。\n"
                    + Candidate("候補 1", best.Baseline, best.Delta, best.Counts) + "\n"
                    + Candidate("候補 2", best.OtherBaseline, best.OtherDelta, best.OtherCounts));

            // 両条件の AND。減算で比較するため、2 倍する整数オーバーフローも起こさない。
            bool fewFiles = best.Baseline.FileCount < baseTree.Root.FileCount - best.Baseline.FileCount;
            bool fewDirectories = best.Baseline.DirectoryCount < baseTree.Root.DirectoryCount - best.Baseline.DirectoryCount;
            if (fewFiles && fewDirectories)
                throw new InvalidDataException("基準ディレクトリの自動解決結果を採用できません。ベースライン基準配下のファイル数・ディレクトリ数がともに全体の半数未満です。\n"
                    + Candidate("最大一致候補", best.Baseline, best.Delta, best.Counts) + "\n"
                    + "ベースライン基準配下: " + Statistics(best.Baseline) + "\nベースライン全体: " + Statistics(baseTree.Root));

            return new ReferenceMatch
            {
                BaselineRoot = best.Baseline, DeltaRoot = best.Delta,
                BaselineTreeRoot = baseTree.Root, DeltaTreeRoot = deltaTree.Root,
                MatchedFiles = best.Counts.Files, MatchedDirectories = best.Counts.Directories
            };
        }

        /// <summary>大きい構造、浅い候補の順に並べ、早期に強い最良値を見つけやすくする。</summary>
        /// <param name="left">比較する一方の候補群。</param>
        /// <param name="right">比較する他方の候補群。</param>
        /// <returns>並べ替えに用いる負値・ゼロ・正値。</returns>
        private static int CompareGroups(MatchingGroup left, MatchingGroup right)
        {
            int compared = right.Shape.Total.CompareTo(left.Shape.Total);
            if (compared != 0) return compared;
            compared = left.First.Depth.CompareTo(right.First.Depth);
            return compared != 0 ? compared : left.Shape.Id.CompareTo(right.Shape.Id);
        }

        /// <summary>仮想ルートを含むディレクトリ名を、例外説明用に整える。</summary>
        /// <param name="node">説明する候補または全体ルート。</param>
        /// <returns>画面表示用の安全な相対ディレクトリパス。</returns>
        private static string Display(DirectoryNode node) { return node.Relative.Length == 0 ? "/" : Text.Safe(node.Relative.Replace('\\', '/')) + "/"; }

        /// <summary>基準自身を除く元の子孫統計を表示用文字列にする。</summary>
        /// <param name="node">統計を表示するディレクトリ。</param>
        /// <returns>ファイル数、非圧縮 bytes、ディレクトリ数。</returns>
        private static string Statistics(DirectoryNode node)
        {
            return node.FileCount.ToString("N0", CultureInfo.InvariantCulture) + " 個のファイル（" + node.Bytes.ToString("N0", CultureInfo.InvariantCulture)
                + " bytes）、" + node.DirectoryCount.ToString("N0", CultureInfo.InvariantCulture) + " 個のディレクトリ";
        }

        /// <summary>同点・半数不足をユーザーが確認できるよう、一組の根拠を整える。</summary>
        /// <param name="label">候補の見出し。</param>
        /// <param name="baseline">ベースライン側の元ディレクトリ。</param>
        /// <param name="delta">差分側の元ディレクトリ。</param>
        /// <param name="counts">この組の厳密な一致件数。</param>
        /// <returns>両側のパス、深さ、一致内訳を含む説明。</returns>
        private static string Candidate(string label, DirectoryNode baseline, DirectoryNode delta, MatchingCounts counts)
        {
            return label + ": ベースライン '" + Display(baseline) + "'（深さ " + baseline.Depth.ToString(CultureInfo.InvariantCulture)
                + "）、差分 '" + Display(delta) + "'（深さ " + delta.Depth.ToString(CultureInfo.InvariantCulture)
                + "）、一致ファイル " + counts.Files.ToString("N0", CultureInfo.InvariantCulture) + "、一致ディレクトリ "
                + counts.Directories.ToString("N0", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>対応済みの二入力群を、差分側のルート配置を保った一つの出力計画へ変換する。</summary>
    internal sealed class DifferenceOverlay
    {
        internal ExtractionPlan Plan;
        internal ReferenceMatch Match;
        internal string MiscDirectory;
        internal bool HasMisc;
        internal long SupersededCount;
        internal decimal SupersededBytes;

        /// <summary>ベースラインを再配置し、差分で置換するファイルを省略した有効計画を返す。</summary>
        /// <param name="baseline">入力内の重複確認を終えたベースライン。</param>
        /// <param name="delta">入力内の重複確認を終えた差分。Relative は変更しない。</param>
        /// <param name="match">一意に決定済みの基準ディレクトリと統計。</param>
        /// <param name="firstBaselineFile">Ordinal 順の最初のベースライン物理 ZIP/断片名。</param>
        /// <param name="lastWriteLocal">日時文字列がない場合の同ファイルのローカル更新日時。</param>
        /// <returns>出力計画・省略統計・失敗時にだけ用いるベースラインの対応表。</returns>
        internal static DifferenceOverlay Build(ExtractionPlan baseline, ExtractionPlan delta, ReferenceMatch match,
            string firstBaselineFile, DateTime lastWriteLocal)
        {
            DifferenceOverlay overlay = new DifferenceOverlay();
            overlay.Plan = new ExtractionPlan();
            overlay.Match = match;
            overlay.MiscDirectory = "_base_misc_files\\" + ChooseTimestamp(firstBaselineFile, lastWriteLocal);
            overlay.Plan.DuplicateCount = checked(baseline.DuplicateCount + delta.DuplicateCount);
            string baselineRoot = match.BaselineRoot.Relative;
            string deltaRoot = match.DeltaRoot.Relative;
            foreach (ZipEntry entry in baseline.Entries)
            {
                Program.CheckCancel();
                if (entry.Keep && !IsAtOrBelow(entry.Relative, baselineRoot)) { overlay.HasMisc = true; break; }
            }

            Dictionary<string, ZipEntry> deltaFiles = new Dictionary<string, ZipEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (ZipEntry entry in delta.Entries)
            {
                Program.CheckCancel();
                if (!entry.Keep) continue;
                if (overlay.HasMisc && ReservedConflict(entry.Relative, entry.IsDirectory, overlay.MiscDirectory))
                    throw new InvalidDataException("差分 ZIP の内容とベースライン退避領域が衝突します: "
                        + entry.Relative + " / 退避領域: " + overlay.MiscDirectory + " / " + entry.Label);
                if (!entry.IsDirectory) deltaFiles.Add(entry.Relative, entry);
            }

            // 通常時はベースラインの残す内容を先に、差分の全内容を後に処理する。
            // 置換対象は同じ正規化済み出力パスのファイルだけ。内容CRCやサイズの同一性は要求しない。
            foreach (ZipEntry entry in baseline.Entries)
            {
                Program.CheckCancel();
                if (!entry.Keep) continue;
                string original = entry.Relative;
                bool inReference = IsAtOrBelow(original, baselineRoot);
                string suffix = inReference ? RemoveRoot(original, baselineRoot) : original;
                string mapped = Combine(inReference ? deltaRoot : overlay.MiscDirectory, suffix);
                mapped = WindowsPaths.Relative(mapped.Length == 0 && entry.IsDirectory ? "." : mapped, entry.IsDirectory);
                if (overlay.HasMisc && inReference && ReservedConflict(mapped, entry.IsDirectory, overlay.MiscDirectory))
                    throw new InvalidDataException("再配置したベースライン基準配下の内容と退避領域が衝突します: "
                        + mapped + " / 元: " + original + " / 退避領域: " + overlay.MiscDirectory);
                entry.Relative = mapped;
                ZipEntry replacement;
                if (!entry.IsDirectory && inReference && deltaFiles.TryGetValue(mapped, out replacement))
                {
                    overlay.Plan.Fallbacks.Add(replacement, entry);
                    overlay.SupersededCount++;
                    overlay.SupersededBytes += entry.Size;
                    continue;
                }
                overlay.Plan.AddEntry(entry, false);
            }
            // 同じ入力群内の明示ディレクトリ日時は先勝ちを保ち、群間では差分側を優先する。
            HashSet<string> deltaDirectoryTimes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ZipEntry entry in delta.Entries)
            {
                Program.CheckCancel();
                if (!entry.Keep) continue;
                bool replaceTimes = entry.IsDirectory && entry.Times.ModifiedUtc.HasValue && deltaDirectoryTimes.Add(entry.Relative);
                overlay.Plan.AddEntry(entry, replaceTimes);
            }
            overlay.Plan.CheckCollisions(true);
            return overlay;
        }

        /// <summary>正規化済みの path が root 自身またはその子孫かを、区切り境界付きで判定する。</summary>
        /// <param name="path">比較する仮想相対パス。</param>
        /// <param name="root">基準パス。空文字は ZIP 全体の仮想ルート。</param>
        /// <returns>Windows の大小文字無視比較で基準配下なら true。</returns>
        internal static bool IsAtOrBelow(string path, string root)
        {
            return root.Length == 0 || String.Equals(path, root, StringComparison.OrdinalIgnoreCase)
                || (path.Length > root.Length && path[root.Length] == '\\'
                    && path.StartsWith(root, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>基準配下であることを確認済みのパスから基準と区切りを除く。</summary>
        /// <param name="path">root 配下と判定済みの相対パス。</param>
        /// <param name="root">除く基準パス。空文字なら何も除かない。</param>
        /// <returns>基準からの相対パス。基準自身なら空文字。</returns>
        private static string RemoveRoot(string path, string root)
        {
            if (root.Length == 0) return path;
            return path.Length == root.Length ? "" : path.Substring(root.Length + 1);
        }

        /// <summary>空の仮想ルートにも対応する、検査済み相対パスの連結。</summary>
        /// <param name="root">連結先の仮想基準パス。</param>
        /// <param name="child">基準からの相対パス。</param>
        /// <returns>必要な場合だけ区切りを補ったパス。</returns>
        private static string Combine(string root, string child)
        {
            if (root.Length == 0) return child;
            return child.Length == 0 ? root : root + "\\" + child;
        }

        /// <summary>退避領域と重なる内容、または退避領域の親を塞ぐファイルを検出する。</summary>
        /// <param name="path">退避対象以外の出力相対パス。</param>
        /// <param name="directory">その内容がディレクトリなら true。</param>
        /// <param name="reserved">日時まで確定した退避用の相対ディレクトリ。</param>
        /// <returns>出力内容が退避領域を使用・妨害するとき true。</returns>
        private static bool ReservedConflict(string path, bool directory, string reserved)
        {
            return IsAtOrBelow(path, reserved) || (!directory && IsAtOrBelow(reserved, path));
        }

        /// <summary>ファイル名中の最後の有効な yyMMdd_HHmmss を選び、なければローカル更新日時を返す。</summary>
        /// <param name="path">最初のベースライン物理ファイル。ディレクトリ名部分は検索しない。</param>
        /// <param name="lastWriteLocal">ファイル名に日時がない場合の代替日時。</param>
        /// <returns>必ず年月日6桁・下線・時分秒6桁の13文字。</returns>
        internal static string ChooseTimestamp(string path, DateTime lastWriteLocal)
        {
            string name = Path.GetFileName(path);
            string last = null;
            for (int offset = 0; offset + 13 <= name.Length; offset++)
            {
                if (name[offset + 6] != '_') continue;
                bool digits = true;
                for (int i = 0; i < 13; i++)
                {
                    if (i == 6) continue;
                    char c = name[offset + i];
                    if (c < '0' || c > '9') { digits = false; break; }
                }
                if (!digits) continue;
                try
                {
                    // YY は 2000～2099 と解釈する。現在のカルチャーの二桁年窓に依存しない。
                    new DateTime(2000 + TwoDigits(name, offset), TwoDigits(name, offset + 2), TwoDigits(name, offset + 4),
                        TwoDigits(name, offset + 7), TwoDigits(name, offset + 9), TwoDigits(name, offset + 11));
                    last = name.Substring(offset, 13);
                }
                catch (ArgumentOutOfRangeException) { }
            }
            return last ?? lastWriteLocal.ToString("yyMMdd_HHmmss", CultureInfo.InvariantCulture);
        }

        /// <summary>ASCII 数字であることを検査済みの二文字を整数に変換する。</summary>
        /// <param name="value">ASCII 数字の検査済み文字列。</param>
        /// <param name="offset">二桁の先頭位置。</param>
        /// <returns>0～99 の整数。</returns>
        private static int TwoDigits(string value, int offset) { return (value[offset] - '0') * 10 + value[offset + 1] - '0'; }

        /// <summary>計算直後と展開後の両方で、同じ基準統計と省略予定を表示する。</summary>
        internal void Print()
        {
            Console.WriteLine("ベースライン ZIP 群の基準ディレクトリ: {0} ({1:N0} 個のファイル (合計 {2:N0} bytes)、{3:N0} 個のディレクトリが存在)",
                DisplayRoot(Match.BaselineRoot.Relative), Match.BaselineRoot.FileCount, Match.BaselineRoot.Bytes, Match.BaselineRoot.DirectoryCount);
            Console.WriteLine("差分 ZIP 群の基準ディレクトリ: {0} ({1:N0} 個のファイル (合計 {2:N0} bytes)、{3:N0} 個のディレクトリが存在)",
                DisplayRoot(Match.DeltaRoot.Relative), Match.DeltaRoot.FileCount, Match.DeltaRoot.Bytes, Match.DeltaRoot.DirectoryCount);
            Console.WriteLine("一致したファイル: {0:N0} 個、一致したディレクトリ: {1:N0} 個", Match.MatchedFiles, Match.MatchedDirectories);
            Console.WriteLine("差分 ZIP によりベースライン ZIP が上書きされることとなるファイル個数: {0:N0} 個 (合計 {1:N0} bytes)",
                SupersededCount, SupersededBytes);
            if (HasMisc) Console.WriteLine("ベースライン基準外の保存先: " + Text.Safe(MiscDirectory + "\\"));
        }

        /// <summary>仮想ルートを /、他の基準を末尾 / 付きで安全に表示する。</summary>
        /// <param name="relative">正規化した基準パス。仮想ルートは空文字。</param>
        /// <returns>制御文字を安全化した、末尾 / 付きの表示文字列。</returns>
        private static string DisplayRoot(string relative) { return relative.Length == 0 ? "/" : Text.Safe(relative.Replace('\\', '/') + "/"); }
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

    /// <summary>主スレッドが取得した候補列の不変スナップショット。ワーカーは候補順を変更しない。</summary>
    internal sealed class PasswordSnapshot
    {
        private readonly string[] candidates;
        internal readonly long Version;

        internal int Count { get { return candidates.Length; } }
        internal string this[int index] { get { return candidates[index]; } }

        /// <summary>候補配列を内部へ複製し、その候補順に対応する版番号とともに不変に保持する。</summary>
        internal PasswordSnapshot(IList<string> candidates, long version)
        {
            this.candidates = new string[candidates.Count];
            candidates.CopyTo(this.candidates, 0);
            Version = version;
        }
    }

    /// <summary>展開 producer から保存 consumer へ渡す一単位。Reset は圧縮互換再試行の巻戻し命令。</summary>
    internal struct EntryPipePacket
    {
        internal byte[] Buffer;
        internal int Count;
        internal bool Reset;
    }

    /// <summary>
    /// 一ジョブ専用の容量制限付き出力。最大 128 KiB × 8 個のバッファを再利用する。
    /// worker はこの Stream にだけ書き、保存先のディレクトリや一時ファイルには触れない。
    /// </summary>
    internal sealed class EntryPipe : Stream, IRestartableOutput
    {
        private const int MaximumBufferSize = 131072;
        private const int MaximumBuffers = 8;
        private readonly object gate = new object();
        private readonly Queue<EntryPipePacket> ready = new Queue<EntryPipePacket>(MaximumBuffers + 1);
        private readonly Queue<byte[]> unused = new Queue<byte[]>(MaximumBuffers);
        private readonly int bufferSize;
        private int allocatedBuffers;
        private bool completed;
        private bool stopped;
        private ExceptionDispatchInfo failure;

        /// <summary>宣言サイズに応じた小さなバッファから始め、大きいファイルでも容量を固定する。</summary>
        /// <param name="expectedSize">エントリの展開後宣言サイズ。</param>
        internal EntryPipe(long expectedSize)
        {
            bufferSize = (int)Math.Min(MaximumBufferSize, Math.Max(1L, expectedSize));
        }

        /// <summary>個別中止を記録し、満杯／空のキューで待っているスレッドを起こす。</summary>
        internal void Stop()
        {
            lock (gate)
            {
                stopped = true;
                Monitor.PulseAll(gate);
            }
        }

        /// <summary>producer の終端と元例外を保存する。先に届いたデータは consumer が元順で処理する。</summary>
        internal void Complete(ExceptionDispatchInfo error)
        {
            lock (gate)
            {
                failure = error;
                completed = true;
                Monitor.PulseAll(gate);
            }
        }

        /// <summary>停止済みなら個別キャンセルを通知する。gate を保持して呼ぶ。</summary>
        private void CheckStopped()
        {
            if (stopped) throw new OperationCanceledException("先読み展開が取り消されました。");
        }

        /// <summary>空きバッファを借りる。上限に達した場合も個別／全体キャンセルを定期確認する。</summary>
        private byte[] RentBuffer()
        {
            lock (gate)
            {
                while (true)
                {
                    Program.CheckCancel();
                    CheckStopped();
                    if (unused.Count != 0) return unused.Dequeue();
                    if (allocatedBuffers < MaximumBuffers)
                    {
                        byte[] result = new byte[bufferSize];
                        allocatedBuffers++;
                        return result;
                    }
                    Monitor.Wait(gate, 100);
                }
            }
        }

        /// <summary>consumer が使い終えたバッファを戻す。書出し例外時にも必ず呼ぶ。</summary>
        private void ReturnBuffer(byte[] buffer)
        {
            if (buffer == null) return;
            lock (gate)
            {
                unused.Enqueue(buffer);
                Monitor.PulseAll(gate);
            }
        }

        /// <summary>データまたは巻戻しを順序付きで公開する。制御 packet もキュー容量に含める。</summary>
        private void Publish(EntryPipePacket packet)
        {
            lock (gate)
            {
                while (ready.Count >= MaximumBuffers)
                {
                    Program.CheckCancel();
                    CheckStopped();
                    Monitor.Wait(gate, 100);
                }
                Program.CheckCancel();
                CheckStopped();
                ready.Enqueue(packet);
                Monitor.PulseAll(gate);
            }
        }

        /// <summary>codec が再利用する入力配列から専用バッファへ複写し、consumer へ受け渡す。</summary>
        public override void Write(byte[] buffer, int offset, int count)
        {
            Bytes.CheckBuffer(buffer, offset, count);
            while (count != 0)
            {
                byte[] owned = RentBuffer();
                bool published = false;
                try
                {
                    int amount = Math.Min(count, owned.Length);
                    Buffer.BlockCopy(buffer, offset, owned, 0, amount);
                    Publish(new EntryPipePacket { Buffer = owned, Count = amount });
                    published = true;
                    offset += amount;
                    count -= amount;
                }
                finally { if (!published) ReturnBuffer(owned); }
            }
        }

        /// <summary>native Deflate の受理差による再試行を、先行データの後ろに巻戻し命令として渡す。</summary>
        public void Restart()
        {
            Publish(new EntryPipePacket { Reset = true });
        }

        /// <summary>
        /// 元順で選ばれた一つの保存先へ転送し、producer の全検証完了まで待つ。
        /// Reset を受けた場合だけ、呼出し開始時点まで保存先を切り詰めて旧 inflater の再出力を受ける。
        /// </summary>
        /// <param name="destination">主スレッドが作成・所有する一時ファイル。</param>
        internal void CopyVerifiedTo(Stream destination)
        {
            long start = destination.CanSeek ? destination.Position : 0;
            while (true)
            {
                EntryPipePacket packet = new EntryPipePacket();
                bool havePacket;
                ExceptionDispatchInfo error;
                lock (gate)
                {
                    while (ready.Count == 0 && !completed)
                    {
                        Program.CheckCancel();
                        CheckStopped();
                        Monitor.Wait(gate, 100);
                    }
                    Program.CheckCancel();
                    CheckStopped();
                    havePacket = ready.Count != 0;
                    if (havePacket)
                    {
                        packet = ready.Dequeue();
                        Monitor.PulseAll(gate);
                    }
                    error = failure;
                }
                if (!havePacket)
                {
                    if (error != null) error.Throw();
                    return;
                }
                if (packet.Reset)
                {
                    if (!destination.CanSeek)
                        throw new NotSupportedException("互換展開の再試行には、巻戻し可能な一時保存先が必要です。");
                    destination.SetLength(start);
                    destination.Position = start;
                }
                else
                {
                    try { destination.Write(packet.Buffer, 0, packet.Count); }
                    finally { ReturnBuffer(packet.Buffer); }
                }
            }
        }

        /// <summary>producer と consumer の終了後だけ呼び、残った平文バッファを消去して解放する。</summary>
        internal void ReleaseBuffers()
        {
            lock (gate)
            {
                while (ready.Count != 0)
                {
                    byte[] buffer = ready.Dequeue().Buffer;
                    if (buffer != null) Array.Clear(buffer, 0, buffer.Length);
                }
                while (unused.Count != 0)
                {
                    byte[] buffer = unused.Dequeue();
                    Array.Clear(buffer, 0, buffer.Length);
                }
            }
        }

        public override bool CanRead { get { return false; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return true; } }
        public override long Length { get { throw new NotSupportedException(); } }
        public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
        public override void Flush() { Program.CheckCancel(); }
        public override int Read(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
    }

    /// <summary>
    /// 一エントリの投機的計算結果。候補検証と実展開は worker、対話と結果の採用は主スレッドが担当する。
    /// ExceptionDispatchInfo で元例外を保持し、順番が来るまで警告・失敗として外へ出さない。
    /// </summary>
    internal sealed class EntryWork : IDisposable
    {
        internal readonly ZipEntry Entry;
        internal readonly long PasswordVersion;
        private readonly PasswordSnapshot snapshot;
        private readonly EntryPipe pipe;
        private readonly object stateGate = new object();
        private byte[] suppliedPassword;
        private volatile bool canceled;
        private bool scheduled;
        private bool finished;
        private bool selectionFinished;
        private bool knownPassword;
        private bool disposed;
        private ExceptionDispatchInfo selectionFailure;

        /// <summary>候補の不変写し、または主スレッドが完全検証済みのパスワードの複製を保持する。</summary>
        /// <param name="entry">変更が完了した展開対象メタデータ。</param>
        /// <param name="candidates">同時に投入するジョブ間で共有可能な候補 snapshot。</param>
        /// <param name="verifiedPassword">対話後に既知となったバイト列。null なら snapshot の候補を調べる。</param>
        internal EntryWork(ZipEntry entry, PasswordSnapshot candidates, byte[] verifiedPassword)
        {
            Entry = entry;
            snapshot = candidates;
            PasswordVersion = candidates.Version;
            pipe = new EntryPipe(entry.Size);
            if (verifiedPassword != null) suppliedPassword = (byte[])verifiedPassword.Clone();
        }

        /// <summary>pool への登録完了を記録する。queue のロック内で、worker を起こす前に呼ぶ。</summary>
        internal void MarkScheduled() { scheduled = true; }

        /// <summary>Program.CheckCancel が worker ごとの停止要求を検査するための hook。</summary>
        private bool IsCanceled() { return canceled; }

        /// <summary>候補結果を公開する。実際の出力失敗とは別に、親作成より前に採用可否を返す。</summary>
        private void FinishSelection(bool found, ExceptionDispatchInfo error)
        {
            lock (stateGate)
            {
                if (selectionFinished) return;
                knownPassword = found;
                selectionFailure = error;
                selectionFinished = true;
                Monitor.PulseAll(stateGate);
            }
        }

        /// <summary>pool worker が実行する唯一の本体。UI、保存先、警告一覧、成功数には触れない。</summary>
        internal void Run()
        {
            byte[] password = null;
            ExceptionDispatchInfo error = null;
            Func<bool> previousCancellation = Program.WorkerCancelRequested;
            Program.WorkerCancelRequested = IsCanceled;
            try
            {
                Program.CheckCancel();
                EntryCodec.CheckSupported(Entry);
                bool found = true;
                if (Entry.Encrypted)
                {
                    if (suppliedPassword != null)
                    {
                        password = suppliedPassword;
                        suppliedPassword = null;
                    }
                    else found = PasswordManager.TryKnown(Entry, snapshot, out password);
                }
                FinishSelection(found, null);
                if (found) EntryCodec.WriteVerified(Entry, password, pipe);
            }
            catch (Exception ex)
            {
                error = ExceptionDispatchInfo.Capture(ex);
                FinishSelection(false, error);
            }
            finally
            {
                if (password != null) Array.Clear(password, 0, password.Length);
                if (suppliedPassword != null)
                {
                    Array.Clear(suppliedPassword, 0, suppliedPassword.Length);
                    suppliedPassword = null;
                }
                pipe.Complete(error);
                Program.WorkerCancelRequested = previousCancellation;
                lock (stateGate)
                {
                    finished = true;
                    Monitor.PulseAll(stateGate);
                }
            }
        }

        /// <summary>既知候補の全検証を待つ。false は全候補失敗であり、ignore の判断は主スレッドで行う。</summary>
        internal bool WaitForKnownPassword()
        {
            lock (stateGate)
            {
                while (!selectionFinished)
                {
                    Program.CheckCancel();
                    Monitor.Wait(stateGate, 100);
                }
                Program.CheckCancel();
                if (selectionFailure != null) selectionFailure.Throw();
                return knownPassword;
            }
        }

        /// <summary>順番が来た主スレッドだけが呼び、検証済み結果をその一時保存先へ転送する。</summary>
        internal void CopyVerifiedTo(Stream destination) { pipe.CopyVerifiedTo(destination); }

        /// <summary>個別停止を要求する。キュー待機と codec 内の CheckCancel の双方へ伝える。</summary>
        internal void Cancel()
        {
            canceled = true;
            pipe.Stop();
        }

        /// <summary>停止後の資源解放前に必ず worker の終了を確認する。全体キャンセル中でも途中で戻らない。</summary>
        internal void Join()
        {
            if (!scheduled) return;
            lock (stateGate)
            {
                while (!finished) Monitor.Wait(stateGate, 100);
            }
        }

        /// <summary>未採用・失敗・成功を問わず producer を終了させ、残留データと秘密バイト列を消去する。</summary>
        public void Dispose()
        {
            if (disposed) return;
            Cancel();
            Join();
            pipe.ReleaseBuffers();
            if (suppliedPassword != null)
            {
                Array.Clear(suppliedPassword, 0, suppliedPassword.Length);
                suppliedPassword = null;
            }
            disposed = true;
        }
    }

    /// <summary>
    /// 主スレッドが最大 N 件だけ先読みする再利用 worker pool。ファイル数に比例した Task/ハンドルを作らない。
    /// 各 worker は一件ずつ処理し、ディレクトリ・パスワード対話・復元の前には全件を停止／回収できる。
    /// </summary>
    internal sealed class ParallelExtraction : IDisposable
    {
        internal readonly int Limit;
        private readonly object queueGate = new object();
        private readonly Queue<EntryWork> queue;
        private readonly Dictionary<ZipEntry, EntryWork> pending;
        private readonly List<Thread> workers = new List<Thread>();
        private bool stopping;
        private bool disposed;

        /// <summary>実際の保持ジョブ数も worker 数もこの上限以下にする。worker は必要になってから開始する。</summary>
        internal ParallelExtraction(int limit)
        {
            if (limit < 1) throw new ArgumentOutOfRangeException("limit");
            Limit = limit;
            queue = new Queue<EntryWork>(limit);
            pending = new Dictionary<ZipEntry, EntryWork>(limit);
        }

        /// <summary>主スレッドが現在保持している未採用ジョブ数。完了済みの先行データも上限に含める。</summary>
        internal int Count { get { return pending.Count; } }

        /// <summary>主スレッドから、指定エントリの保持中ジョブを取得する。</summary>
        internal EntryWork Find(ZipEntry entry)
        {
            EntryWork work;
            return pending.TryGetValue(entry, out work) ? work : null;
        }

        /// <summary>candidate または検証済み password を用いるジョブを一件だけ投入する。</summary>
        /// <param name="entry">採用順が確定したファイル。</param>
        /// <param name="snapshot">候補順と版番号。</param>
        /// <param name="verifiedPassword">主スレッドで完全検証済みなら指定し、不要なら null。</param>
        /// <returns>主スレッドが元順で採用する結果窓口。</returns>
        internal EntryWork Schedule(ZipEntry entry, PasswordSnapshot snapshot, byte[] verifiedPassword)
        {
            if (disposed) throw new ObjectDisposedException("ParallelExtraction");
            if (pending.Count >= Limit) throw new InvalidOperationException("先読み展開の保持上限を超えました。");
            if (pending.ContainsKey(entry)) throw new InvalidOperationException("同じエントリを二重に投入できません。");
            EntryWork work = new EntryWork(entry, snapshot, verifiedPassword);
            try
            {
                // 開始失敗時も、まだ queue に載っていない work は待機せず安全に破棄できる。
                EnsureWorkerCount(Math.Min(Limit, pending.Count + 1));
                pending.Add(entry, work);
                lock (queueGate)
                {
                    queue.Enqueue(work);
                    work.MarkScheduled();
                    Monitor.PulseAll(queueGate);
                }
                return work;
            }
            catch
            {
                pending.Remove(entry);
                work.Dispose();
                throw;
            }
        }

        /// <summary>必要な上限まで常駐 worker を増やす。カルチャは主スレッドの候補文字コード判定と揃える。</summary>
        private void EnsureWorkerCount(int wanted)
        {
            while (workers.Count < wanted)
            {
                CultureInfo culture = CultureInfo.CurrentCulture;
                CultureInfo uiCulture = CultureInfo.CurrentUICulture;
                Thread worker = new Thread(delegate()
                {
                    // 設定対象自身のスレッドで行い、別スレッドの CultureInfo 変更制約にも依存しない。
                    Thread.CurrentThread.CurrentCulture = culture;
                    Thread.CurrentThread.CurrentUICulture = uiCulture;
                    WorkerLoop();
                });
                worker.IsBackground = true;
                worker.Name = "DNNT ZIP worker " + (workers.Count + 1).ToString(CultureInfo.InvariantCulture);
                workers.Add(worker);
                try { worker.Start(); }
                catch { workers.RemoveAt(workers.Count - 1); throw; }
            }
        }

        /// <summary>一件の計算終了後は同じスレッドを次のジョブへ再利用する。実行例外は EntryWork が保持する。</summary>
        private void WorkerLoop()
        {
            while (true)
            {
                EntryWork work;
                lock (queueGate)
                {
                    while (queue.Count == 0 && !stopping) Monitor.Wait(queueGate);
                    if (queue.Count == 0 && stopping) return;
                    work = queue.Dequeue();
                }
                work.Run();
            }
        }

        /// <summary>採用済み／省略済みの一件を停止・回収して次の先読み枠を空ける。</summary>
        internal void Release(ZipEntry entry)
        {
            EntryWork work;
            if (!pending.TryGetValue(entry, out work)) return;
            work.Dispose();
            pending.Remove(entry);
        }

        /// <summary>全件へ先に停止通知を送り、その後全終了を確認する。対話や逐次復元の前のバリアにも用いる。</summary>
        internal void CancelAllAndJoin()
        {
            foreach (EntryWork work in pending.Values) work.Cancel();
            foreach (EntryWork work in pending.Values) work.Dispose();
            pending.Clear();
        }

        /// <summary>入力ハンドルや出力日時の後処理に入る前に、全 worker の終了と残留データの破棄を保証する。</summary>
        public void Dispose()
        {
            if (disposed) return;
            CancelAllAndJoin();
            lock (queueGate)
            {
                stopping = true;
                Monitor.PulseAll(queueGate);
            }
            foreach (Thread worker in workers) worker.Join();
            disposed = true;
        }
    }


    /// <summary>候補パスワードの順序と ignore 状態を管理する。ディスクへパスワードを書かない。</summary>
    internal sealed class PasswordManager
    {
        private readonly List<string> passwords = new List<string>();
        private bool ignoreUnknown;
        private long version;
        private PasswordSnapshot cachedSnapshot;

        /// <summary>主スレッドだけが変更する候補順の版。worker は snapshot の版と比較する。</summary>
        internal long Version { get { return version; } }

        /// <summary>候補配列を複製する。返した snapshot の内容は、その後の候補追加や順序変更の影響を受けない。</summary>
        internal PasswordSnapshot Snapshot()
        {
            if (cachedSnapshot == null || cachedSnapshot.Version != version)
                cachedSnapshot = new PasswordSnapshot(passwords, version);
            return cachedSnapshot;
        }

        /// <summary>snapshot の既知候補だけを元順で完全検証する。対話・ignore 判断・候補更新は行わない。</summary>
        /// <param name="entry">検証対象の暗号エントリ。</param>
        /// <param name="snapshot">主スレッドが複製した不変候補列。</param>
        /// <param name="password">成功時の専用バイト列。呼出元 worker が必ず消去する。</param>
        /// <returns>完全検証に成功した候補がある場合だけ true。</returns>
        internal static bool TryKnown(ZipEntry entry, PasswordSnapshot snapshot, out byte[] password)
        {
            password = null;
            for (int i = 0; i < snapshot.Count; i++)
            {
                Program.CheckCancel();
                if (TryPassword(entry, snapshot[i], out password)) return true;
            }
            return false;
        }
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
                    if (line.Length != 0 && seen.Add(line))
                    {
                        passwords.Add(line);
                        unchecked { version++; }
                    }
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
                    unchecked { version++; }
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



    /// <summary>
    /// 元エントリ順の対話・保存・復元を維持し、独立した展開計算だけを上限付き worker へ先読み投入する。
    /// 出力パスの検査、親作成、一時ファイル、日時、確定、警告、集計は常にこの主スレッドが所有する。
    /// </summary>
    internal sealed class Extractor
    {
        private readonly ExtractionPlan plan;
        private readonly SafeRoot root;
        private readonly PasswordManager passwords;
        private readonly OverwritePolicy overwrite;
        private readonly WarningBook warnings;
        private ParallelExtraction parallel;
        internal long SuccessCount, SkippedCount, FallbackSuccessCount;
        internal decimal SuccessBytes;

        /// <summary>変更済み展開計画と、主スレッドが所有する保存・対話・警告サービスを受け取る。</summary>
        internal Extractor(ExtractionPlan value, SafeRoot target, PasswordManager candidates, OverwritePolicy policy, WarningBook book)
        {
            plan = value; root = target; passwords = candidates; overwrite = policy; warnings = book;
        }

        /// <summary>CPU 数 - 1（最低 1）、かつ対象ファイル数以下の並行度で処理する。1 件／1 worker 時は直接実行。</summary>
        internal void Run()
        {
            int wanted = Math.Max(Environment.ProcessorCount - 1, 1);
            int limit = (int)Math.Min((long)wanted, plan.FileCount);
            if (limit < 2)
            {
                RunEntries();
                return;
            }
            using (ParallelExtraction workers = new ParallelExtraction(limit))
            {
                parallel = workers;
                try { RunEntries(); }
                finally { parallel = null; }
            }
            // using の終了時点で全 worker を join 済み。呼出元のディレクトリ日時復元はその後に行われる。
        }

        /// <summary>R03 と同じ採用順・例外区分・差分復元条件で一件ずつ結果を確定する。</summary>
        private void RunEntries()
        {
            long number = 0, total = plan.FileCount;
            for (int index = 0; index < plan.Entries.Count; index++)
            {
                Program.CheckCancel();
                ZipEntry entry = plan.Entries[index];
                if (!entry.Keep) continue;
                if (!entry.IsDirectory)
                {
                    number++;
                    Console.WriteLine("({0:N0} 個目 / {1:N0} 個中: {2:N0} bytes) '{3}' を展開...", number, total, entry.Size, Text.Safe(entry.Name));
                }
                else if (parallel != null) parallel.CancelAllAndJoin();

                long successesBefore = SuccessCount, skipsBefore = SkippedCount;
                try
                {
                    if (entry.PlanError != null) throw new IOException(entry.PlanError);
                    EntryCodec.CheckSupported(entry);
                    if (entry.IsDirectory) ExtractDirectory(entry);
                    else if (parallel == null) ExtractFile(entry, entry);
                    else
                    {
                        FillAhead(index);
                        ExtractPreparedFile(entry);
                    }
                }
                catch (Exception ex)
                {
                    if (!Program.Recoverable(ex))
                    {
                        if (parallel != null) parallel.CancelAllAndJoin();
                        throw;
                    }
                    warnings.Add(entry, ex.Message);
                    Console.WriteLine("  警告: " + Text.Safe(ex.Message));
                }
                finally
                {
                    // 上書き拒否や保存先エラーでも producer を残さず、投機的な codec エラーを追加報告しない。
                    if (parallel != null) parallel.Release(entry);
                }

                // 成功時はベースラインのデータを一切読まない。n/N による意図的な省略でも復元しない。
                ZipEntry fallback;
                if (!entry.IsDirectory && SuccessCount == successesBefore && SkippedCount == skipsBefore
                    && plan.Fallbacks.TryGetValue(entry, out fallback))
                {
                    // 復元側のパスワード入力は次エントリより先。主スレッドの候補検証と worker の CPU 使用を重ねない。
                    if (parallel != null) parallel.CancelAllAndJoin();
                    RestoreBaseline(entry, fallback);
                }
            }
        }

        /// <summary>明示ディレクトリを越えず、採用ファイルを最大 N 件だけ先読みする。保存先には触れない。</summary>
        /// <param name="start">現在処理する元計画のインデックス。</param>
        private void FillAhead(int start)
        {
            PasswordSnapshot snapshot = passwords.Snapshot();
            for (int index = start; index < plan.Entries.Count && parallel.Count < parallel.Limit; index++)
            {
                Program.CheckCancel();
                ZipEntry entry = plan.Entries[index];
                if (!entry.Keep) continue;
                if (entry.IsDirectory) break;
                if (entry.PlanError != null || parallel.Find(entry) != null) continue;
                // 非対応方式の警告は当該エントリの順番で RunEntries が出す。先読み中には外へ出さない。
                try { EntryCodec.CheckSupported(entry); }
                catch (NotSupportedException) { continue; }
                parallel.Schedule(entry, snapshot, null);
            }
        }

        /// <summary>初回上書き確認の後で、候補版を照合し、当該ジョブだけの結果を元順で保存する。</summary>
        private void ExtractPreparedFile(ZipEntry entry)
        {
            if (!InitialOverwriteAllowed(entry, entry)) return;
            EntryWork work = parallel.Find(entry);
            if (work == null) work = parallel.Schedule(entry, passwords.Snapshot(), null);
            if (entry.Encrypted && work.PasswordVersion != passwords.Version)
            {
                // 先行対話で候補順が変わった場合、古い候補による結果・例外は採用せず現在順でやり直す。
                parallel.Release(entry);
                work = parallel.Schedule(entry, passwords.Snapshot(), null);
            }
            if (entry.Encrypted && !work.WaitForKnownPassword())
            {
                // 未知候補への質問と ignore 判断は従前どおり主スレッドだけで行う。
                // 全 future を停止してから GetVerified を実行し、合計 CPU 処理数の上限も守る。
                parallel.CancelAllAndJoin();
                byte[] password;
                if (!PasswordFor(entry, out password)) return;
                try { work = parallel.Schedule(entry, passwords.Snapshot(), password); }
                finally { if (password != null) Array.Clear(password, 0, password.Length); }
            }
            SaveFile(entry, entry, work, null);
        }

        /// <summary>省略したベースラインを、同じ差分ファイルに対する上書き許可の範囲内で逐次復元する。</summary>
        /// <param name="delta">採用に失敗した差分。事前確認・再確認の対象として用いる。</param>
        /// <param name="baseline">同じ出力先へ再配置済みのベースライン元エントリ。</param>
        private void RestoreBaseline(ZipEntry delta, ZipEntry baseline)
        {
            Program.CheckCancel();
            Console.WriteLine("  差分を保存できなかったため、ベースラインから復元を試みます: " + Text.Safe(baseline.Label));
            long before = SuccessCount;
            try
            {
                if (delta.PlanError != null) throw new IOException(delta.PlanError);
                if (baseline.PlanError != null) throw new IOException(baseline.PlanError);
                EntryCodec.CheckSupported(baseline);
                ExtractFile(baseline, delta);
                if (SuccessCount != before)
                {
                    FallbackSuccessCount++;
                    warnings.Add(delta, "差分の代わりにベースライン版を復元しました。差分適用は未完了です: " + baseline.Label);
                    Console.WriteLine("  ベースライン版を復元しました。差分版の保存は未完了です。");
                }
                else warnings.Add(delta, "ベースライン版への復元も完了しませんでした。");
            }
            catch (Exception ex)
            {
                if (!Program.Recoverable(ex)) throw;
                warnings.Add(baseline, "差分失敗後のベースライン復元失敗: " + ex.Message);
                Console.WriteLine("  ベースライン復元の警告: " + Text.Safe(ex.Message));
            }
        }

        /// <summary>主スレッドで候補を検証・質問する。ignore 後も既知候補を試す従前の判断を保持する。</summary>
        private bool PasswordFor(ZipEntry entry, out byte[] password)
        {
            password = null;
            if (!entry.Encrypted) return true;
            if (passwords.GetVerified(entry, out password)) return true;
            warnings.Add(entry, "ignore 指定: 既知の候補パスワードでは完全性検証まで成功しませんでした（不正パスワードと暗号データ破損は識別できない場合があります）。");
            return false;
        }

        /// <summary>明示ディレクトリエントリを、先行 worker の停止後に従前どおり逐次処理する。</summary>
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

        /// <summary>初回の上書き検査。先読み処理の成否を採用する前に、必ず元順で実行する。</summary>
        /// <returns>保存を続けてよい場合 true。拒否時は従前どおり省略数を増やして false。</returns>
        private bool InitialOverwriteAllowed(ZipEntry entry, ZipEntry policyEntry)
        {
            using (PathLease check = root.DirectoryLease(WindowsPaths.Parent(entry.Relative), false, null, false))
            {
                if (!overwrite.Allow(policyEntry, root.ProbeLeaf(entry.Relative, check)))
                {
                    Skip(entry);
                    return false;
                }
            }
            return true;
        }

        /// <summary>一 worker 時とベースライン復元用の直接実行。候補対話と書出しの順序を R03 から維持する。</summary>
        private void ExtractFile(ZipEntry entry, ZipEntry policyEntry)
        {
            if (!InitialOverwriteAllowed(entry, policyEntry)) return;
            byte[] password;
            if (!PasswordFor(entry, out password)) return;
            try { SaveFile(entry, policyEntry, null, password); }
            finally { if (password != null) Array.Clear(password, 0, password.Length); }
        }

        /// <summary>
        /// 当該エントリの順番で初めて親・一時保存先を作成し、完全検証後だけ最終名へ確定する。
        /// worker がある場合も出力パス・上書き選択・日時・警告・成功数を worker へ渡さない。
        /// </summary>
        /// <param name="entry">復号・展開する通常、差分、または復元用ベースライン。</param>
        /// <param name="policyEntry">通常は entry 自身。ベースライン復元では対応する差分。</param>
        /// <param name="work">並列計算結果。null なら呼出元スレッドで直接展開する。</param>
        /// <param name="password">直接実行時の検証済みパスワード。work 使用時は null。</param>
        private void SaveFile(ZipEntry entry, ZipEntry policyEntry, EntryWork work, byte[] password)
        {
            using (PathLease parents = root.DirectoryLease(WindowsPaths.Parent(entry.Relative), true, plan.Directories, false))
            using (StagedFile temporary = new StagedFile(root.Destination(WindowsPaths.Parent(entry.Relative)), entry, warnings))
            {
                if (work == null) EntryCodec.WriteVerified(entry, password, temporary.Stream);
                else work.CopyVerifiedTo(temporary.Stream);
                temporary.Stream.Flush();
                SetTimes(entry, temporary.Stream.SafeFileHandle);
                int races = 0;
                while (true)
                {
                    Program.CheckCancel();
                    FileStamp current = root.ProbeLeaf(entry.Relative, parents);
                    if (!overwrite.Allow(policyEntry, current)) { Skip(entry); return; }
                    try { temporary.Commit(root.Destination(entry.Relative), current != null); break; }
                    catch (Win32Exception ex)
                    {
                        // 新規保存の直前に対象が生じた場合、上書きへ勝手に切り替えず再確認する。
                        if ((ex.NativeErrorCode != 80 && ex.NativeErrorCode != 183) || ++races > 16) throw;
                    }
                }
                SuccessCount++;
                SuccessBytes += entry.Size;
                if (entry.TimestampWarning != null) warnings.Add(entry, entry.TimestampWarning);
            }
        }

        /// <summary>当該一時ファイルの日時を設定し、回復可能な日時エラーだけを元順の警告へ加える。</summary>
        private void SetTimes(ZipEntry entry, SafeFileHandle handle)
        {
            try { Native.Times(handle, entry.Times); }
            catch (Exception ex)
            {
                if (!Program.Recoverable(ex)) throw;
                warnings.Add(entry, "ファイル日時の設定失敗: " + ex.Message);
            }
        }

        /// <summary>意図的な上書き拒否を記録する。この増分がある場合はベースライン復元を行わない。</summary>
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
        /// <summary>標準展開を優先し、入力解釈差がある場合だけ出力を戻して従来方式で全検証し直す。</summary>
        /// <param name="entry">構造検査済みの元エントリ。呼出し中は変更しない。</param>
        /// <param name="password">この呼出しで所有・参照する復号用バイト列。無暗号なら null。</param>
        /// <param name="output">隔離した新規出力。巻戻し可能、Stream.Null、または IRestartableOutput を実装する。</param>
        internal static void WriteVerified(ZipEntry entry, byte[] password, Stream output)
        {
            if (output == null) throw new ArgumentNullException("output");
            long start = output.CanSeek ? output.Position : 0;
            try { WriteVerifiedCore(entry, password, output, false); }
            catch (NativeDeflateException ex)
            {
                Program.CheckCancel();
                // 通常成功時には二重解凍しない。HDIST=31/32 など従来受理できた入力の互換性を保つ。
                // 失敗した標準展開の平文を残したまま追記しない。再読込は同じ入力ハンドルから行う。
                if (Object.ReferenceEquals(output, Stream.Null)) { }
                else if (output.CanSeek)
                {
                    output.Position = start;
                    output.SetLength(start);
                }
                else
                {
                    IRestartableOutput restartable = output as IRestartableOutput;
                    if (restartable == null)
                        throw new InvalidDataException("従来 Deflate との互換再試行に必要な出力巻戻しができません。", ex);
                    restartable.Restart();
                }
                // 旧 decoder も終端・木・距離・サイズ・CRC/AES 認証を省略しない。
                // これにも失敗した入力を成功として採用することはない。
                WriteVerifiedCore(entry, password, output, true);
            }
        }

        /// <summary>一回分の復号・展開・完全性検証。再試行ごとに暗号と展開の状態をすべて作り直す。</summary>
        /// <param name="entry">検証・展開する ZIP エントリ。</param>
        /// <param name="password">復号パスワード。無暗号なら null。</param>
        /// <param name="output">検証完了まで外部へ確定しない出力。</param>
        /// <param name="useLegacyDeflate">標準 decoder が拒否した場合の互換再試行だけ true。</param>
        private static void WriteVerifiedCore(ZipEntry entry, byte[] password, Stream output, bool useLegacyDeflate)
        {
            CheckSupported(entry);
            using (JoinedStream joined = new JoinedStream(entry.Archive.Parts))
            using (SliceStream raw = new SliceStream(joined, entry.DataOffset, entry.CompressedSize))
            {
                if (!entry.Encrypted)
                {
                    Decode(raw, raw.Length, entry, output, useLegacyDeflate);
                }
                else if (entry.Aes != null)
                {
                    if (password == null) throw new InvalidDataException("暗号化エントリにパスワードが指定されていません。");
                    using (AesReadStream decoded = new AesReadStream(raw, entry.Aes, password))
                    {
                        Decode(decoded, decoded.Length, entry, output, useLegacyDeflate);
                        decoded.VerifyAuthentication();
                    }
                }
                else
                {
                    if (password == null) throw new InvalidDataException("暗号化エントリにパスワードが指定されていません。");
                    using (ZipCryptoStream decoded = new ZipCryptoStream(raw, password,
                        ((entry.Flags & 8) != 0) ? (byte)(entry.DosTime >> 8) : (byte)(entry.Crc >> 24)))
                        Decode(decoded, decoded.Length, entry, output, useLegacyDeflate);
                }
                if (raw.Position != raw.Length) throw new InvalidDataException("エントリの圧縮データが完全には消費されていません。");
            }
        }
        private static void Decode(Stream input, long compressedLength, ZipEntry entry, Stream output, bool useLegacyDeflate)
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
            else if (useLegacyDeflate) StrictDeflate.Inflate(input, compressedLength, window);
            else NativeDeflate.Inflate(input, compressedLength, window);
            window.Finish();
            if ((entry.Aes == null || entry.Aes.Version == 1) && window.Crc != entry.Crc)
                throw new InvalidDataException("CRC-32 が一致しません。破損または不正なパスワードです。");
        }
    }

    /// <summary>標準 decoder の互換再試行を、容量制限付き出力にも順序を保って通知する。</summary>
    internal interface IRestartableOutput
    {
        /// <summary>このエントリの未確定出力を先頭へ戻す。後続の平文に先行して適用する。</summary>
        void Restart();
    }

    /// <summary>標準 inflater の入力解釈差を、全検証を伴う従来方式への再試行へ伝える。</summary>
    /// <remarks>InvalidDataException は sealed。呼出元は本例外だけを捕捉し、入力と出力を最初からやり直す。</remarks>
    internal sealed class NativeDeflateException : IOException
    {
        internal NativeDeflateException(InvalidDataException reason)
            : base("標準 Deflate 展開で入力データを解釈できませんでした。", reason) { }
    }

    /// <summary>圧縮データの最終 byte を分離し、終端未成立での EOF と終端後の余分な byte を検出する。</summary>
    /// <remarks>
    /// .NET Framework 4.7.2 以降の native DeflateStream は、終端成立を確認してから次の入力を要求する。
    /// 最後の 1 byte を直前の Read に含めないため、終端後に余分な byte があれば必ず最低 1 byte が未読で残る。
    /// 宣言した圧縮長をすべて渡した後の追加 Read は、native 側が終端を認識できていないことを示す。
    /// Read(count=0) はこの検査の対象外。元 stream は呼出元の所有物なので Dispose で閉じない。
    /// この順序は対象 Framework の実装に基づく。別ランタイムへ移植するときは境界の回帰試験を再実施する。
    /// </remarks>
    internal sealed class NativeDeflateInput : Stream
    {
        private readonly Stream source;
        private readonly long length;
        private long position;
        private bool disposed;

        internal NativeDeflateInput(Stream input, long compressedLength)
        {
            if (input == null) throw new ArgumentNullException("input");
            if (!input.CanRead) throw new ArgumentException("入力ストリームを読み取れません。", "input");
            // 空内容の Deflate でも終端ブロックは必須。Store の空エントリにはこの wrapper を用いない。
            if (compressedLength <= 0) throw new InvalidDataException("Deflate の圧縮データが空、または圧縮サイズが不正です。");
            source = input;
            length = compressedLength;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Bytes.CheckBuffer(buffer, offset, count);
            if (disposed) throw new ObjectDisposedException("NativeDeflateInput");
            if (count == 0) return 0;
            Program.CheckCancel();
            long remaining = length - position;
            if (remaining == 0)
                throw new InvalidDataException("Deflate 終端ブロックを認識する前に圧縮データが終了しました。");

            // remaining > 1 の間は、最後の 1 byte を必ず留保する。通常の入力は大きな単位で読み出せる。
            long available = remaining == 1 ? 1 : remaining - 1;
            int request = (int)Math.Min((long)count, available);
            int got = source.Read(buffer, offset, request);
            if (got == 0) throw new InvalidDataException("Deflate の圧縮データが宣言サイズより前に切れています。");
            if (got < 0 || got > request) throw new IOException("入力ストリームが不正な読取サイズを返しました。");
            position += got;
            return got;
        }

        /// <summary>非 0 サイズの DeflateStream.Read が 0 を返してから呼び出す。</summary>
        internal void VerifyComplete()
        {
            if (disposed) throw new ObjectDisposedException("NativeDeflateInput");
            if (position != length)
                throw new InvalidDataException("Deflate 終端後に余分なデータがあるか、圧縮サイズが不一致です。");
        }

        public override bool CanRead { get { return !disposed && source.CanRead; } }
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
            disposed = true;
            base.Dispose(disposing);
        }
    }

    /// <summary>平文の生成を .NET Framework 標準 DeflateStream の native inflater に任せる。</summary>
    internal static class NativeDeflate
    {
        /// <summary>展開結果を block 単位でサイズ・CRC 検証用 window へ渡す。出力確定は呼出元が行う。</summary>
        internal static void Inflate(Stream input, long compressedLength, OutputWindow window)
        {
            byte[] buffer = new byte[131072];
            using (NativeDeflateInput bounded = new NativeDeflateInput(input, compressedLength))
            using (System.IO.Compression.DeflateStream inflater = new System.IO.Compression.DeflateStream(
                bounded, System.IO.Compression.CompressionMode.Decompress, true))
            {
                while (true)
                {
                    Program.CheckCancel();
                    int got;
                    try
                    {
                        // 必ず非 0 サイズで終端まで読む。宣言された平文サイズへ到達しただけでは終了しない。
                        got = inflater.Read(buffer, 0, buffer.Length);
                    }
                    catch (InvalidDataException ex)
                    {
                        // HDIST=31/32 等、native と従来 inflater で受理範囲が異なる入力は従来方式で完全再検証する。
                        // 出力、CRC、サイズ、認証のエラーはこの catch の外に置き、互換再試行の理由にしない。
                        throw new NativeDeflateException(ex);
                    }
                    if (got == 0) break;
                    window.Stored(buffer, got);
                }
                bounded.VerifyComplete();
            }
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
        // 通常の native / Store 経路では不要。従来 inflater の互換再試行で初めて確保する。
        private byte[] history, pending;
        private int cursor, pendingCount;
        private long count;
        private uint crc = 0xffffffffU;
        internal uint Crc { get { return crc ^ 0xffffffffU; } }
        internal OutputWindow(Stream output, long size) { destination = output; expected = size; }
        internal void Literal(byte value)
        {
            if (count >= expected) throw new InvalidDataException("展開サイズが ZIP ヘッダの宣言値を超えました。");
            if (history == null)
            {
                history = new byte[32768];
                pending = new byte[131072];
            }
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
