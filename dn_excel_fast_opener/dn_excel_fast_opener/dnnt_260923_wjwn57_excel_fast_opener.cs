/*
DNNT 260923_WJWN57 Excel で xlsx ファイルを追加的に開くと時間がかかる

目的:
  dn_excel_fast_opener_r2 (出力 EXE 名は Q32 互換の dn_excel_fast_opener.exe)
  15 種の Excel 関連ファイルと、それらを直接指す .lnk を複数指定して開く。
  対応: .xlsx .xlsm .xls .csv .xlsb .xltx .xltm .xlt .xlam .xla .xml .ods
        .txt .dif .slk。関連付けは最初の 12 種のみ。.lnk 自体は関連付けない。
  .lnk は Windows Shell の IShellLinkW/IPersistFile で読取り、最大 16 段を追跡。
  リンクの引数・作業フォルダー・実行設定を実行しない。対象が EXE 等なら拒否。
  対象外・不正・不在が 1 件でもあれば全件を中止し、いずれも Excel へ送らない。
  Excel の通常の追加起動・既存インスタンスへの転送経路を避け、既存の
  Excel のファイルドロップ受信ウィンドウへ WM_DROPFILES を直接通知する。
  Excel 未起動なら、レジストリで取得した EXCEL.EXE に全ファイルを渡す。

仕様・動作原理:
  * .NET Framework 4.0 API / C# 4.0 / WinExe / AnyCPU。Office PIA 不要。
  * 参照は mscorlib.dll と System.dll のみ。Windows 標準 DLL を P/Invoke。
  * App Paths (HKCU/HKLM、64/32 bit) → Office InstallRoot → Excel.Application
    の LocalServer32 の順で EXCEL.EXE を検索する。拡張子関連付けは使わない。
  * XLMAIN とその所有プロセスを確認し、WS_EX_ACCEPTFILES が設定された
    XLMAIN、EXCEL7、XLDESK のいずれかに Unicode DROPFILES を一括送信する。
    受信登録がない相手には送らず、エラーにする。外部ウィンドウの設定は変更しない。
  * PostMessage の成功は「要求を渡せた」こと。ブックが開いたことの保証ではない。
    WM_DROPFILES は OLE のマウスドラッグそのものではない。対象 Excel ビルドで
    最初に実動確認すること。読み込み後のエラー・パスワード等は Excel が表示する。
  * 成功通知、常駐、Excel COM 自動化、キー入力、マウス移動、クリップボード変更、
    マクロ設定変更、保護ビュー解除、レジストリ書き込み、ファイル保存は行わない。
  * 引数なしは最前面を要求した MessageBox で Usage を表示する。
  * 検出できた例外は処理段階・引数・Win32 情報を含め MessageBox で表示する。
  * 通常経路に固定 Sleep はない。初回起動の競合時だけ最大 1,000 ms 確認する。
    処理全体は 4,000 ms で監視し、停止した OS 呼び出しがあってもエラー表示後、
    本プログラムだけを終了する。タイムアウト時の外部要求の結果は不明になり得る。
  * --new-instance は明示指定時だけ /x で別プロセス起動する。自動再送はしない。
  * --diagnose は Excel 登録・ウィンドウ・現在の関連付けを表示する。
  * --settings は既定のアプリ画面を開く。--refresh-associations は変更を通知する。
  * --check [files...] は .lnk 解決結果と検証結果だけを表示し、Excel を操作しない。
  * --self-test は引数・拡張子・リンク追跡・DROPFILES の自己検査。Excel は操作しない。
    リンク追跡の自己検査は代替読取関数を使い、実際の Shell COM 動作とは区別する。

注意:
  * Excel と同じ通常ユーザー・同じデスクトップで実行する。UIPI を解除しない。
  * Excel の編集モード、モーダル画面、起動途中、クラッシュ、ファイルの内容、
    同期/ネットワーク待ちによっては開けない。絶対的な速度保証はできない。
  * Windows 11 の UserChoice は直接書き換えない。関連付けは同梱 README 参照。
  * VS2026 の通常の net40 プロジェクト読み込みは公式非対応。
    同梱の net40 参照アセンブリ付きプロジェクトをコマンドラインでビルドする。
    参照アセンブリはビルド専用。配布時に追加 DLL や Office 相互運用 DLL は不要。
  * 送信前に全ファイルを検証するが、検証と実際のオープンの間の変更は防げない。
  * .lnk は通常 GetPath のみ。パスが得られない場合のみ、UI/探索/追跡/更新を
    禁止し 100 ms を指定した Resolve を試す。移動先の全ディスク探索はしない。
  * 拡張子で受付可否を検証するだけで、ファイル内容の安全性・Excel 互換性は保証しない。
    CSV/TXT の区切り・文字コード、テンプレート、アドイン等の扱いは Excel に従う。
  * .xlam/.xla はブックとは異なる。永続的アドイン登録やマクロ保護解除は行わない。
  * 原則 EXE 一つで使用可能。Windows 11 では OS の .NET Framework 4.8/4.8.1
    ランタイム上で実行される。古い 4.0 ランタイムを Windows 11 に入れない。

終了コード: 0=要求の引き渡し/補助操作成功、1=エラー、2=Usage、3=タイムアウト。
実機の Excel/Windows/VS2026 での動作検証は、このソース生成環境では未実施。
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using Microsoft.Win32;


namespace dnnt_260923_wjwn57_excel_fast_opener_r2
{
    /// <summary>非常駐ランチャーのエントリーポイントと実装。</summary>
    internal static class Program
    {
        private const string ApplicationName = "dn_excel_fast_opener_r2";
        private const int MaximumShortcutDepth = 16;
        private const int OperationTimeoutMilliseconds = 4000;
        private const int GateTimeoutMilliseconds = 1500;
        private const int StartupRetryMilliseconds = 1000;
        private const int ProbeTimeoutMilliseconds = 150;
        private const int MaximumMessageCharacters = 10000;
        private const string GateName = @"Local\dnnt_260923_wjwn57_excel_fast_opener_gate";

        /// <summary>Q32 で関連付ける 12 拡張子。列挙順も診断・Usage に使用する。</summary>
        private static readonly string[] AssociatedExtensions = new string[]
        {
            ".xlsx", ".xlsm", ".xls", ".csv", ".xlsb", ".xltx", ".xltm", ".xlt",
            ".xlam", ".xla", ".xml", ".ods"
        };

        /// <summary>直接指定・リンク先としては開くが、関連付けには登録しない 3 拡張子。</summary>
        private static readonly string[] AdditionalExtensions = new string[] { ".txt", ".dif", ".slk" };

        /// <summary>
        /// プログラムを実行する。args は OS により分割済みの引数。
        /// 戻り値はヘッダーに示した終了コード。
        /// </summary>
        [STAThread]
        private static int Main(string[] args)
        {
            try { return Run(args); }
            catch (Exception error)
            {
                ShowText("Error - startup", error.ToString(), true);
                return 1;
            }
        }

        /// <summary>args を解釈し、監視付きで処理する。戻り値は終了コード。</summary>
        private static int Run(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                ShowText("Usage", GetUsage(), false);
                return 2;
            }

            Invocation invocation;
            try
            {
                invocation = ParseArguments(args);
            }
            catch (Exception error)
            {
                ShowText("Error", error.ToString() + "\r\n\r\n" + GetUsage(), true);
                return 1;
            }
            if (invocation.Help)
            {
                ShowText("Usage", GetUsage(), false);
                return 0;
            }

            // OS 呼び出しそのものが停止した場合にも、本アプリを常駐させないための監視。
            // ワーカースレッドは Excel の読み込み完了を待たない。
            JobState state = new JobState(args);
            Thread worker = new Thread(delegate ()
            {
                try
                {
                    state.Result = Execute(invocation, state);
                }
                catch (Exception error)
                {
                    state.Error = error;
                }
            });
            worker.IsBackground = true;
            worker.Name = "Excel fast-open dispatch";
            worker.SetApartmentState(ApartmentState.STA);

            try
            {
                worker.Start();
                if (!worker.Join(OperationTimeoutMilliseconds))
                {
                    state.Cancel();
                    string detail = "処理が " + OperationTimeoutMilliseconds.ToString(CultureInfo.InvariantCulture)
                        + " ms 以内に完了しませんでした。\r\n"
                        + "Excel、ネットワーク、セキュリティ製品、OS 呼び出しの停止などが考えられます。\r\n"
                        + "既に Excel に要求が届いた可能性があります。開いたか確認してから再操作してください。\r\n"
                        + "自動再送はしません。OK 後は本プログラムだけを終了します。Excel は終了しません。\r\n\r\n"
                        + state.Describe();
                    ShowText("Error - timeout", detail, true);
                    // Background の P/Invoke が停止中でも、自分のプロセスのみ終了する。
                    Environment.Exit(3);
                    return 3;
                }
                if (state.Error != null)
                {
                    ShowText("Error", state.Error.Message + "\r\n\r\n--- 診断情報 ---\r\n"
                        + state.Describe() + "\r\n" + state.Error.ToString(), true);
                    return 1;
                }
                if (!String.IsNullOrEmpty(state.Result))
                    ShowText(invocation.SelfTest ? "Self-test" : (invocation.CheckOnly ? "Validation" : "Diagnostics"), state.Result, false);
                return 0;
            }
            catch (Exception error)
            {
                state.Cancel();
                ShowText("Error", state.Describe() + "\r\n\r\n" + error.ToString(), true);
                Environment.Exit(1);
                return 1;
            }
        }

        /// <summary>実行指定。Files は未正規化のパスで、補助操作との併用は許可しない。</summary>
        private sealed class Invocation
        {
            internal readonly List<string> Files = new List<string>();
            internal bool Help;
            internal bool Diagnose;
            internal bool Settings;
            internal bool RefreshAssociations;
            internal bool SelfTest;
            internal bool NewInstance;
            internal bool CheckOnly;
        }

        /// <summary>ワーカーと監視元が共有する診断情報。文字列の更新は参照単位で行う。</summary>
        private sealed class JobState
        {
            private int cancelled;
            private readonly string[] arguments;
            internal volatile string Stage = "開始";
            internal volatile string ExcelPath = "(未取得)";
            internal volatile string Target = "(未選択)";
            internal volatile string ResolvedFiles = "(検証前)";
            internal volatile string DispatchState = "外部要求はまだ送っていません。";
            internal volatile string Result;
            internal volatile Exception Error;

            /// <summary>元の引数のコピーを保存する。source は OS の引数配列。</summary>
            internal JobState(string[] source) { arguments = (string[])source.Clone(); }

            /// <summary>以降の外部要求を可能な限り抑止する。既に実行中の OS 呼び出しは取り消せない。</summary>
            internal void Cancel() { Interlocked.Exchange(ref cancelled, 1); }

            /// <summary>取消状態なら例外。外部副作用の直前にも呼び出す。</summary>
            internal void CheckCancellation()
            {
                if (Thread.VolatileRead(ref cancelled) != 0)
                    throw new OperationCanceledException("処理はタイムアウトにより取り消されました。");
            }

            /// <summary>現在の処理段階、引数、対象を診断用文字列として返す。</summary>
            internal string Describe()
            {
                StringBuilder text = new StringBuilder();
                text.AppendLine("処理段階: " + Stage);
                text.AppendLine("EXCEL.EXE: " + ExcelPath);
                text.AppendLine("対象: " + Target);
                text.AppendLine("ファイル検証: " + ResolvedFiles);
                text.AppendLine("要求状態: " + DispatchState);
                text.AppendLine("アプリ: " + Assembly.GetExecutingAssembly().Location);
                text.AppendLine("アプリのビット数: " + (IntPtr.Size * 8).ToString(CultureInfo.InvariantCulture));
                text.AppendLine("引数:");
                for (int index = 0; index < arguments.Length; index++)
                    text.AppendLine("  [" + index.ToString(CultureInfo.InvariantCulture) + "] " + arguments[index]);
                return text.ToString();
            }
        }

        /// <summary>引数を解釈して返す。-- より後の文字列はスイッチとして解釈しない。</summary>
        private static Invocation ParseArguments(string[] args)
        {
            Invocation invocation = new Invocation();
            bool literal = false;
            foreach (string argument in args)
            {
                if (String.IsNullOrEmpty(argument)) throw new ArgumentException("空の引数は指定できません。");
                if (!literal && argument == "--") { literal = true; continue; }
                if (!literal && (argument == "--help" || argument == "/?")) invocation.Help = true;
                else if (!literal && argument == "--diagnose") invocation.Diagnose = true;
                else if (!literal && argument == "--settings") invocation.Settings = true;
                else if (!literal && argument == "--refresh-associations") invocation.RefreshAssociations = true;
                else if (!literal && argument == "--self-test") invocation.SelfTest = true;
                else if (!literal && argument == "--check") invocation.CheckOnly = true;
                else if (!literal && (argument == "--new-instance" || argument == "/x")) invocation.NewInstance = true;
                else if (!literal && argument.StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException("不明なオプションです: " + argument);
                else invocation.Files.Add(argument);
            }
            int utilityCount = (invocation.Help ? 1 : 0) + (invocation.Diagnose ? 1 : 0)
                + (invocation.Settings ? 1 : 0) + (invocation.RefreshAssociations ? 1 : 0)
                + (invocation.SelfTest ? 1 : 0);
            if (utilityCount > 1 || (utilityCount > 0 && (invocation.Files.Count > 0 || invocation.NewInstance || invocation.CheckOnly)))
                throw new ArgumentException("補助オプションは単独で指定してください。");
            if (invocation.CheckOnly && invocation.NewInstance)
                throw new ArgumentException("--check と --new-instance / /x は併用できません。");
            if (utilityCount == 0 && invocation.Files.Count == 0)
                throw new ArgumentException("開くファイル、またはその .lnk を 1 個以上指定してください。");
            return invocation;
        }

        /// <summary>指定された操作を実行する。通常成功時は null、診断時は表示文字列を返す。</summary>
        private static string Execute(Invocation invocation, JobState state)
        {
            if (invocation.SelfTest)
            {
                state.Stage = "自己検査";
                return RunSelfTests();
            }
            if (invocation.RefreshAssociations || invocation.Settings)
            {
                state.Stage = "関連付け変更通知";
                state.CheckCancellation();
                Native.SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
                if (invocation.Settings)
                {
                    state.Stage = "既定のアプリ設定画面を起動";
                    state.CheckCancellation();
                    using (Process process = Process.Start(new ProcessStartInfo(
                        "ms-settings:defaultapps?registeredAppUser=dn_excel_fast_opener")
                    { UseShellExecute = true })) { }
                }
                return null;
            }
            if (invocation.Diagnose) return MakeDiagnostics(state);

            state.Stage = "引数とファイルの検証";
            List<string> paths = NormalizeAndValidateFiles(invocation.Files, state);
            if (invocation.CheckOnly)
                return "検証成功。Excel の検索・起動・ドロップ通知は行っていません。\r\n\r\n"
                    + state.ResolvedFiles
                    + "\r\n一括通知する重複除去後の件数: " + paths.Count.ToString(CultureInfo.InvariantCulture)
                    + "\r\n拡張子と存在だけの確認です。内容の妥当性・安全性は検査していません。";
            state.Stage = "レジストリから EXCEL.EXE を検索";
            string source;
            string excelPath = ResolveExcelExecutable(out source);
            state.ExcelPath = excelPath + " [" + source + "]";
            state.CheckCancellation();

            if (invocation.NewInstance)
            {
                LaunchExcel(excelPath, paths, true, state);
                return null;
            }

            // 多重起動を短時間だけ直列化する。サービス、ファイル、常駐プロセスは作らない。
            using (Mutex gate = new Mutex(false, GateName))
            {
                bool acquired = false;
                try
                {
                    state.Stage = "同時起動の調整";
                    try { acquired = gate.WaitOne(GateTimeoutMilliseconds); }
                    catch (AbandonedMutexException) { acquired = true; }
                    if (!acquired)
                        throw new TimeoutException("別の高速オープン処理が実行中です。自動再送はしません。");
                    state.CheckCancellation();
                    state.Stage = "既存 Excel ウィンドウの検索";
                    List<ExcelWindow> windows = FindExcelWindows();

                    // 他の同時呼び出しが Excel を作成直後の場合だけ、UI の出現を短く待つ。
                    // 既存ウィンドウがある通常経路には待ち時間を追加しない。
                    if (windows.Count == 0 && HasExcelProcessInCurrentSession())
                    {
                        state.Stage = "起動途中の Excel ウィンドウを確認";
                        Stopwatch timer = Stopwatch.StartNew();
                        do
                        {
                            state.CheckCancellation();
                            Thread.Sleep(20);
                            windows = FindExcelWindows();
                        } while (windows.Count == 0 && timer.ElapsedMilliseconds < StartupRetryMilliseconds);
                        if (windows.Count == 0)
                            throw new InvalidOperationException("Excel プロセスは存在しますが、対象デスクトップで XLMAIN を取得できません。"
                                + "\r\n起動途中、非表示の自動化用 Excel、別のデスクトップ、権限差を確認してください。"
                                + "\r\n意図的に別プロセスで開く場合だけ --new-instance を指定してください。");
                    }
                    if (windows.Count == 0)
                    {
                        LaunchExcel(excelPath, paths, false, state);
                    }
                    else
                    {
                        ExcelWindow selected = SelectExcelWindow(windows);
                        state.Target = selected.Describe();
                        ValidateAndProbeWindow(selected, state);
                        state.CheckCancellation();
                        // 復元・前面化はベストエフォート。入力制限を解除したりキーを送ったりしない。
                        if (Native.IsIconic(selected.Window)) Native.ShowWindowAsync(selected.Window, 9);
                        Native.SetForegroundWindow(selected.Window);
                        PostFileDrop(selected, paths, state);
                    }
                }
                finally
                {
                    if (acquired) gate.ReleaseMutex();
                }
            }
            return null;
        }

        /// <summary>
        /// 全入力を検証して、解決済みの絶対パスを指定順で返す。ひとつでも不正なら
        /// 例外にまとめ、正常なファイルも含めて一切 Excel へ送信しない。
        /// inputs は元の引数、state は診断・取消情報。同一文字列の重複だけを除去する。
        /// </summary>
        private static List<string> NormalizeAndValidateFiles(List<string> inputs, JobState state)
        {
            return ValidateFileBatch(inputs, state, ReadShortcutTarget, ValidateRegularFile);
        }

        /// <summary>
        /// 共通の全件検証。readShortcut は一段の .lnk 読取、validateFile は存在・種別確認。
        /// 自己検査では両者を置換するため、実ファイルや COM に依存せず制御を検査できる。
        /// 戻り値は正常時のみの全対象一覧。不正入力があればまとめた ArgumentException。
        /// </summary>
        private static List<string> ValidateFileBatch(List<string> inputs, JobState state,
            Func<string, string> readShortcut, Action<string> validateFile)
        {
            List<string> paths = new List<string>();
            Dictionary<string, bool> seen = new Dictionary<string, bool>(StringComparer.Ordinal);
            StringBuilder failures = new StringBuilder();
            StringBuilder report = new StringBuilder();
            int failureCount = 0;
            for (int index = 0; index < inputs.Count; index++)
            {
                state.CheckCancellation();
                string input = inputs[index];
                state.Stage = "ファイル検証 " + (index + 1).ToString(CultureInfo.InvariantCulture)
                    + "/" + inputs.Count.ToString(CultureInfo.InvariantCulture) + ": " + input;
                try
                {
                    string path = ResolveInputPath(input, state, readShortcut, validateFile);
                    report.AppendLine("[" + (index + 1).ToString(CultureInfo.InvariantCulture) + "] " + input);
                    report.AppendLine("  → " + path);
                    if (!seen.ContainsKey(path)) { seen.Add(path, true); paths.Add(path); }
                    else report.AppendLine("  (同一パスの重複のため通知は 1 回だけ)");
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception error)
                {
                    failureCount++;
                    failures.AppendLine("[" + (index + 1).ToString(CultureInfo.InvariantCulture) + "] " + input);
                    failures.AppendLine("  " + error.Message);
                    // COM / Win32 の原因を失わないように詳細も保持する。
                    failures.AppendLine("  詳細: " + error.ToString());
                    failures.AppendLine();
                }
            }
            state.ResolvedFiles = report.Length == 0 ? "(解決できたファイルなし)" : report.ToString();
            if (failureCount != 0)
                throw new ArgumentException("申し訳ありませんが、対応外のファイル、リンク先の問題、"
                    + "または読み取れないファイルが " + failureCount.ToString(CultureInfo.InvariantCulture)
                    + " 件含まれているため、今回のオープン操作をすべて中止しました。\r\n"
                    + "正常なファイルも含め、この呼び出しからは一件も Excel へ渡していません。\r\n"
                    + "該当する指定を取り除くか、リンク先などを修正して再実行してください。\r\n\r\n"
                    + "受け付ける拡張子: " + GetSupportedExtensionText() + "\r\n"
                    + ".lnk は上記ファイルを直接指すものだけが対象です。\r\n\r\n" + failures.ToString());
            if (paths.Count == 0) throw new ArgumentException("開く対象ファイルがありません。");
            return paths;
        }

        /// <summary>
        /// input を絶対パス化し .lnk の連鎖を最大 16 段までたどる。対象外・循環・不在は例外。
        /// readShortcut は絶対 .lnk パスから一段先のパスを返す。validateFile はファイルを検証。
        /// state は処理段階と取消状態。戻り値は対応拡張子を持つ通常ファイルの絶対パス。
        /// </summary>
        private static string ResolveInputPath(string input, JobState state,
            Func<string, string> readShortcut, Action<string> validateFile)
        {
            string path = NormalizeFilePath(input);
            Dictionary<string, bool> visited = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            int depth = 0;
            while (true)
            {
                state.CheckCancellation();
                string extension = Path.GetExtension(path);
                if (!String.Equals(extension, ".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsSupportedExtension(extension))
                        throw new ArgumentException("このファイル形式は対象外です: " + path
                            + "\r\n  拡張子: " + (extension.Length == 0 ? "(なし)" : extension)
                            + "\r\n  EXE やスクリプトの起動、ショートカットに登録された引数の実行は行いません。");
                    validateFile(path);
                    state.CheckCancellation();
                    return path;
                }
                if (visited.ContainsKey(path)) throw new ArgumentException("ショートカットが循環しています: " + path);
                if (depth >= MaximumShortcutDepth)
                    throw new ArgumentException("ショートカットの連鎖が上限の "
                        + MaximumShortcutDepth.ToString(CultureInfo.InvariantCulture) + " 段を超えています: " + path);
                visited.Add(path, true);
                depth++;
                validateFile(path);
                state.CheckCancellation();
                state.Stage = "ショートカット解決 " + depth.ToString(CultureInfo.InvariantCulture) + ": " + path;
                string target = readShortcut(path);
                state.CheckCancellation();
                if (String.IsNullOrWhiteSpace(target))
                    throw new ArgumentException("通常ファイルのリンク先を取得できません: " + path
                        + "\r\n  壊れたリンク、URL、仮想フォルダー、特殊なアプリへのリンクは対象外です。");
                // 読取関数の確定済みパスを使用する。通常パスのリテラル % は勝手に展開しない。
                if (!IsAbsoluteWindowsPath(target))
                    throw new ArgumentException("リンク先の絶対パスを確定できません: " + path
                        + "\r\n  取得結果: " + target
                        + "\r\n  ショートカットのリンク先を実在する絶対パスに修正してください。");
                path = NormalizeFilePath(target);
            }
        }

        /// <summary>
        /// input の通常 Windows パスを正規化して返す。URL、ワイルドカード、デバイス、
        /// 拡張長パス、ADS、NUL は拒否。相対入力は呼出し元のカレントディレクトリ基準。
        /// </summary>
        private static string NormalizeFilePath(string input)
        {
            if (String.IsNullOrWhiteSpace(input)) throw new ArgumentException("空のファイル名は指定できません。");
            if (input.IndexOf('\0') >= 0 || input.IndexOf('"') >= 0)
                throw new ArgumentException("パスに NUL や引用符を含めることはできません: " + input);
            if (input.StartsWith(@"\\.\", StringComparison.Ordinal)
                || input.StartsWith(@"\\?\", StringComparison.Ordinal)
                || input.IndexOf("://", StringComparison.Ordinal) >= 0
                || input.IndexOf('*') >= 0 || input.IndexOf('?') >= 0)
                throw new ArgumentException("URL、デバイスパス、拡張長パス、ワイルドカードは対象外です: " + input);
            string path = Path.GetFullPath(input);
            if (path.IndexOf(':', 2) >= 0)
                throw new ArgumentException("代替データストリームは指定できません: " + path);
            if (path.Length >= 260)
                throw new PathTooLongException(".NET Framework 4.0 の通常パス制限を超えています: " + path);
            return path;
        }

        /// <summary>path がドライブ絶対パスか通常 UNC パスかを返す。存在確認はしない。</summary>
        private static bool IsAbsoluteWindowsPath(string path)
        {
            if (String.IsNullOrEmpty(path)) return false;
            if (path.StartsWith(@"\\", StringComparison.Ordinal)) return true;
            return path.Length >= 3 && ((path[0] >= 'A' && path[0] <= 'Z') || (path[0] >= 'a' && path[0] <= 'z'))
                && path[1] == ':' && (path[2] == '\\' || path[2] == '/');
        }

        /// <summary>path の存在・属性を確認する。通常ファイルでなければ詳細付き例外。</summary>
        private static void ValidateRegularFile(string path)
        {
            uint attributes = Native.GetFileAttributesW(path);
            if (attributes == UInt32.MaxValue)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "ファイルを確認できません: " + path);
            if ((attributes & 0x10) != 0) throw new ArgumentException("フォルダーは対象外です: " + path);
        }

        /// <summary>
        /// shortcutPath の .lnk を Shell COM で読み、一段先の絶対パスを返す。
        /// .lnk 自体を起動せず、引数・作業フォルダー・管理者指定・MSI 起動等も実行しない。
        /// COM オブジェクトはこの STA ワーカーでのみ使い、読取終了時に解放する。
        /// </summary>
        private static string ReadShortcutTarget(string shortcutPath)
        {
            object instance = null;
            try
            {
                instance = new ShellLinkObject();
                System.Runtime.InteropServices.ComTypes.IPersistFile persisted =
                    (System.Runtime.InteropServices.ComTypes.IPersistFile)instance;
                persisted.Load(shortcutPath, 0); // STGM_READ。Save は呼び出さない。
                IShellLinkW link = (IShellLinkW)instance;
                string path = ReadLinkPath(link, 0);
                if (String.IsNullOrWhiteSpace(path)) path = Environment.ExpandEnvironmentVariables(ReadLinkPath(link, 4)); // SLGP_RAWPATH。
                if (!String.IsNullOrWhiteSpace(path)
                    && IsAbsoluteWindowsPath(path)) return path;

                // 通常リンクは上で返す。絶対パスが得られない場合だけ、相対情報等の解決を試す。
                // SLR_NO_UI | NOUPDATE | NOSEARCH | NOTRACK | NOLINKINFO、上位ワードは100ms。
                // 探索やダイアログによる数秒待ち、リンク書換え、Windows Installer 起動を避ける。
                int resolved = link.Resolve(IntPtr.Zero, (100u << 16) | 0x0001u | 0x0008u | 0x0010u | 0x0020u | 0x0040u);
                if (resolved < 0) Marshal.ThrowExceptionForHR(resolved);
                path = ReadLinkPath(link, 0);
                if (String.IsNullOrWhiteSpace(path)) path = Environment.ExpandEnvironmentVariables(ReadLinkPath(link, 4));
                if (String.IsNullOrWhiteSpace(path))
                    throw new ArgumentException("通常ファイルのリンク先がないショートカットです: " + shortcutPath);
                return path;
            }
            finally
            {
                // 同じ RCW を複数のインターフェイスとして使うため、解放は一度だけ。
                if (instance != null && Marshal.IsComObject(instance)) Marshal.FinalReleaseComObject(instance);
            }
        }

        /// <summary>link.GetPath を呼び出し、flags に応じたパスを返す。S_FALSE は空文字として扱う。</summary>
        private static string ReadLinkPath(IShellLinkW link, uint flags)
        {
            StringBuilder buffer = new StringBuilder(32768);
            int result = link.GetPath(buffer, buffer.Capacity, IntPtr.Zero, flags);
            if (result < 0) Marshal.ThrowExceptionForHR(result);
            return buffer.ToString();
        }

        /// <summary>extension が受け付ける 15 種の Excel 関連拡張子のひとつかを返す。</summary>
        private static bool IsSupportedExtension(string extension)
        {
            return ContainsExtension(AssociatedExtensions, extension) || ContainsExtension(AdditionalExtensions, extension);
        }

        /// <summary>extensions 内に extension があるかを大文字小文字を区別せず返す。</summary>
        private static bool ContainsExtension(string[] extensions, string extension)
        {
            foreach (string candidate in extensions)
                if (String.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Usage とエラーに表示する全対応拡張子の一覧を返す。</summary>
        private static string GetSupportedExtensionText()
        {
            return String.Join(" / ", AssociatedExtensions) + " / " + String.Join(" / ", AdditionalExtensions);
        }

        /// <summary>Windows 標準の ShellLink COM クラス。追加ライブラリや WSH の参照は不要。</summary>
        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLinkObject { }

        /// <summary>
        /// IShellLinkW の正規 vtable 順の宣言。HRESULT を保持する GetPath/Resolve 以外は
        /// CLR が失敗 HRESULT を例外へ変換する。未使用メソッドも順序維持のため省略しない。
        /// ネイティブの文字列は Unicode、HWND/PIDL は IntPtr、WORD は short とする。
        /// </summary>
        [ComImport]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellLinkW
        {
            /// <summary>target にリンク先を格納。capacity は文字数。戻り値は HRESULT。</summary>
            [PreserveSig]
            int GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder target, int capacity, IntPtr findData, uint flags);
            /// <summary>itemList に PIDL を取得する（未使用）。</summary>
            void GetIDList(out IntPtr itemList);
            /// <summary>itemList を設定する（未使用）。</summary>
            void SetIDList(IntPtr itemList);
            /// <summary>description に説明文を取得する（未使用）。</summary>
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder description, int capacity);
            /// <summary>description を設定する（未使用）。</summary>
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);
            /// <summary>directory に作業フォルダーを取得する（未使用）。</summary>
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int capacity);
            /// <summary>directory を作業フォルダーに設定する（未使用）。</summary>
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
            /// <summary>arguments に引数を取得する（未使用。取得・実行しない）。</summary>
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int capacity);
            /// <summary>arguments を設定する（未使用）。</summary>
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
            /// <summary>hotkey にショートカットキーを取得する（未使用）。</summary>
            void GetHotkey(out short hotkey);
            /// <summary>hotkey を設定する（未使用）。</summary>
            void SetHotkey(short hotkey);
            /// <summary>command に表示状態を取得する（未使用）。</summary>
            void GetShowCmd(out int command);
            /// <summary>command を表示状態に設定する（未使用）。</summary>
            void SetShowCmd(int command);
            /// <summary>location と index にアイコンを取得する（未使用）。</summary>
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder location, int capacity, out int index);
            /// <summary>location と index のアイコンを設定する（未使用）。</summary>
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string location, int index);
            /// <summary>path を相対パス情報に設定する（未使用）。</summary>
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
            /// <summary>owner と flags の条件でリンクを解決する。戻り値は HRESULT。</summary>
            [PreserveSig]
            int Resolve(IntPtr owner, uint flags);
            /// <summary>path をリンク先に設定する（未使用）。</summary>
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
        }

        /// <summary>
        /// 登録情報だけを基に EXCEL.EXE を検索する。戻り値は実在する絶対パス。
        /// source に採用したキーを返す。自身の拡張子関連付けはたどらない。
        /// </summary>
        private static string ResolveExcelExecutable(out string source)
        {
            RegistryView[] views = Environment.Is64BitOperatingSystem
                ? new RegistryView[] { RegistryView.Registry64, RegistryView.Registry32 }
                : new RegistryView[] { RegistryView.Registry32 };
            RegistryHive[] hives = new RegistryHive[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine };
            StringBuilder attempts = new StringBuilder();
            string candidate;
            string value;
            string location;
            const string appPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\excel.exe";
            foreach (RegistryHive hive in hives)
            {
                foreach (RegistryView view in views)
                {
                    location = hive.ToString() + " / " + view.ToString() + " / " + appPath;
                    value = ReadRegistryString(hive, view, appPath, null, attempts);
                    candidate = ValidateExecutableCandidate(value, false);
                    if (candidate != null) { source = location; return candidate; }
                    attempts.AppendLine(location + " = " + (value ?? "(なし)") + " [有効な EXCEL.EXE なし]");
                }
            }
            foreach (RegistryHive hive in hives)
            {
                foreach (RegistryView view in views)
                {
                    const string installRoot = @"SOFTWARE\Microsoft\Office\16.0\Excel\InstallRoot";
                    location = hive.ToString() + " / " + view.ToString() + " / " + installRoot;
                    value = ReadRegistryString(hive, view, installRoot, "Path", attempts);
                    if (!String.IsNullOrEmpty(value))
                    {
                        string root = Environment.ExpandEnvironmentVariables(value).Trim().Trim('"');
                        candidate = ValidateExecutableCandidate(root.TrimEnd('\\') + @"\EXCEL.EXE", false);
                        if (candidate != null) { source = location + " / Path"; return candidate; }
                    }
                }
            }
            // HKCR\Excel.Application は拡張子の既定アプリ変更の影響を受けない。
            foreach (RegistryView view in views)
            {
                value = ReadRegistryString(RegistryHive.ClassesRoot, view, @"Excel.Application\CLSID", null, attempts);
                Guid classId;
                if (String.IsNullOrEmpty(value) || !Guid.TryParse(value, out classId)) continue;
                location = @"CLSID\" + classId.ToString("B") + @"\LocalServer32";
                value = ReadRegistryString(RegistryHive.ClassesRoot, view, location, null, attempts);
                candidate = ValidateExecutableCandidate(value, true);
                if (candidate != null) { source = "ClassesRoot / " + view.ToString() + " / " + location; return candidate; }
            }
            source = null;
            throw new InvalidOperationException("レジストリから実在する EXCEL.EXE を取得できませんでした。"
                + "\r\nOffice の登録とインストール状態を確認してください。固定パスによる推測起動は行いません。\r\n\r\n" + attempts);
        }

        /// <summary>指定したレジストリ文字列を読む。読めない候補は記録し、他の候補を調べる。</summary>
        private static string ReadRegistryString(RegistryHive hive, RegistryView view, string path,
            string valueName, StringBuilder notes)
        {
            try
            {
                using (RegistryKey root = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey key = root.OpenSubKey(path, false))
                {
                    if (key == null) return null;
                    return key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                }
            }
            catch (System.Security.SecurityException error) { notes.AppendLine(path + ": " + error.Message); }
            catch (UnauthorizedAccessException error) { notes.AppendLine(path + ": " + error.Message); }
            catch (IOException error) { notes.AppendLine(path + ": " + error.Message); }
            return null;
        }

        /// <summary>登録値から EXCEL.EXE の絶対パスを取り出し検証する。不適切なら null。</summary>
        private static string ValidateExecutableCandidate(string raw, bool commandLine)
        {
            if (String.IsNullOrEmpty(raw)) return null;
            string path = Environment.ExpandEnvironmentVariables(raw).Trim();
            if (path.StartsWith("\"", StringComparison.Ordinal))
            {
                int end = path.IndexOf('"', 1);
                if (end < 0) return null;
                if (!commandLine && path.Substring(end + 1).Trim().Length > 0) return null;
                path = path.Substring(1, end - 1);
            }
            else if (commandLine)
            {
                // 空白を含む引用符なしの古い LocalServer32 値も、.exe の区切りで読む。
                int end = path.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                while (end >= 0)
                {
                    int after = end + 4;
                    if (after == path.Length || Char.IsWhiteSpace(path[after]))
                    { path = path.Substring(0, after); break; }
                    end = path.IndexOf(".exe", after, StringComparison.OrdinalIgnoreCase);
                }
                if (end < 0) return null;
            }
            try
            {
                if (!Path.IsPathRooted(path)) return null;
                // C:relative のようなドライブ相対形式は実行パスとして採用しない。
                if (!(path.StartsWith(@"\\", StringComparison.Ordinal)
                    || (path.Length >= 3 && path[1] == ':' && (path[2] == '\\' || path[2] == '/')))) return null;
                path = Path.GetFullPath(path);
                if (!String.Equals(Path.GetFileName(path), "EXCEL.EXE", StringComparison.OrdinalIgnoreCase)) return null;
                uint attributes = Native.GetFileAttributesW(path);
                if (attributes == UInt32.MaxValue || (attributes & 0x10) != 0) return null;
                return path;
            }
            catch (ArgumentException) { return null; }
            catch (NotSupportedException) { return null; }
            catch (PathTooLongException) { return null; }
            catch (System.Security.SecurityException) { return null; }
        }

        /// <summary>Excel の候補ウィンドウと、その所属・受信可能状態を保持する。</summary>
        private sealed class ExcelWindow
        {
            internal IntPtr Window;
            internal IntPtr DropWindow;
            internal uint ProcessId;
            internal string Image;
            internal string Caption;
            internal bool Visible;
            internal bool Enabled;
            internal bool Cloaked;
            internal string Problem;

            /// <summary>メッセージボックスに表示する識別情報を返す。</summary>
            internal string Describe()
            {
                return "HWND=" + FormatHandle(Window) + ", DROP=" + FormatHandle(DropWindow)
                    + ", PID=" + ProcessId.ToString(CultureInfo.InvariantCulture)
                    + ", Visible=" + Visible.ToString() + ", Enabled=" + Enabled.ToString()
                    + ", Cloaked=" + Cloaked.ToString() + "\r\n  " + Caption
                    + "\r\n  " + Image + (String.IsNullOrEmpty(Problem) ? "" : "\r\n  " + Problem);
            }
        }

        /// <summary>対象デスクトップの XLMAIN を列挙する。管理者権限差等の問題も記録する。</summary>
        private static List<ExcelWindow> FindExcelWindows()
        {
            List<ExcelWindow> windows = new List<ExcelWindow>();
            Exception callbackError = null;
            Native.EnumerateWindow callback = delegate (IntPtr window, IntPtr parameter)
            {
                try
                {
                    if (!String.Equals(GetWindowClass(window), "XLMAIN", StringComparison.OrdinalIgnoreCase)) return true;
                    ExcelWindow entry = new ExcelWindow();
                    entry.Window = window;
                    Native.GetWindowThreadProcessId(window, out entry.ProcessId);
                    entry.Visible = Native.IsWindowVisible(window);
                    entry.Enabled = Native.IsWindowEnabled(window);
                    entry.Caption = GetWindowCaption(window);
                    int cloaked;
                    entry.Cloaked = Native.DwmGetWindowAttribute(window, 14, out cloaked, 4) == 0 && cloaked != 0;
                    try
                    {
                        entry.Image = GetProcessImage(entry.ProcessId);
                        if (!String.Equals(Path.GetFileName(entry.Image), "EXCEL.EXE", StringComparison.OrdinalIgnoreCase))
                            return true; // クラス名だけを偽装した別アプリには送信しない。
                    }
                    catch (Win32Exception error)
                    {
                        entry.Image = "(取得できません)";
                        entry.Problem = "プロセスの確認に失敗: " + error.Message
                            + " (Win32=" + error.NativeErrorCode.ToString(CultureInfo.InvariantCulture) + ")";
                    }
                    if (entry.Visible && entry.Enabled && !entry.Cloaked && String.IsNullOrEmpty(entry.Problem))
                        entry.DropWindow = FindDropWindow(window, entry.ProcessId);
                    windows.Add(entry);
                    return true;
                }
                catch (Exception error)
                {
                    callbackError = error;
                    return false; // managed 例外をネイティブのコールバック境界に流さない。
                }
            };
            bool success = Native.EnumWindows(callback, IntPtr.Zero);
            GC.KeepAlive(callback);
            if (callbackError != null) throw new InvalidOperationException("Excel ウィンドウの列挙に失敗しました。", callbackError);
            if (!success) throw new Win32Exception(Marshal.GetLastWin32Error(), "EnumWindows が失敗しました。");
            return windows;
        }

        /// <summary>受信登録されたトップレベル/既知の Excel 子ウィンドウを返す。なければゼロ。</summary>
        private static IntPtr FindDropWindow(IntPtr top, uint processId)
        {
            if ((Native.GetWindowLongW(top, -20) & 0x10) != 0) return top;
            IntPtr found = IntPtr.Zero;
            Exception callbackError = null;
            Native.EnumerateWindow callback = delegate (IntPtr child, IntPtr parameter)
            {
                try
                {
                    string className = GetWindowClass(child);
                    if (className != "EXCEL7" && className != "XLDESK") return true;
                    uint owner;
                    Native.GetWindowThreadProcessId(child, out owner);
                    if (owner == processId && Native.IsWindowVisible(child) && Native.IsWindowEnabled(child)
                        && (Native.GetWindowLongW(child, -20) & 0x10) != 0)
                    { found = child; return false; }
                    return true;
                }
                catch (Exception error) { callbackError = error; return false; }
            };
            // EnumChildWindows の戻り値には成功/失敗判定としての意味が定義されていない。
            Native.EnumChildWindows(top, callback, IntPtr.Zero);
            GC.KeepAlive(callback);
            if (callbackError != null) throw new InvalidOperationException("Excel 子ウィンドウの列挙に失敗しました。", callbackError);
            return found;
        }

        /// <summary>利用可能な前面 Excel を優先し、なければ利用可能な列挙順先頭を返す。</summary>
        private static ExcelWindow SelectExcelWindow(List<ExcelWindow> windows)
        {
            IntPtr foreground = Native.GetAncestor(Native.GetForegroundWindow(), 2);
            ExcelWindow first = null;
            foreach (ExcelWindow window in windows)
            {
                if (!window.Visible || !window.Enabled || window.Cloaked || window.DropWindow == IntPtr.Zero
                    || !String.IsNullOrEmpty(window.Problem)) continue;
                if (window.Window == foreground) return window;
                if (first == null) first = window;
            }
            if (first != null) return first;
            StringBuilder details = new StringBuilder();
            foreach (ExcelWindow window in windows) details.AppendLine(window.Describe());
            throw new InvalidOperationException("Excel は検出しましたが、安全にファイルドロップできる受信先がありません。"
                + "\r\nダイアログ、別仮想デスクトップ、権限差、WS_EX_ACCEPTFILES 非対応を確認してください。"
                + "\r\nWM_DROPFILES は OLE ドラッグとは別方式です。受信登録がない相手には強制送信しません。"
                + "\r\n編集・ダイアログを終了して再試行するか、明示的に --new-instance を使用してください。\r\n\r\n" + details);
        }

        /// <summary>送信先の生存・所属・状態と、短いタイムアウトで UI の応答性を検証する。</summary>
        private static void ValidateAndProbeWindow(ExcelWindow selected, JobState state)
        {
            state.Stage = "Excel の受信状態・応答性を検証";
            uint processId;
            Native.GetWindowThreadProcessId(selected.DropWindow, out processId);
            if (!Native.IsWindow(selected.Window) || !Native.IsWindow(selected.DropWindow)
                || processId != selected.ProcessId || !Native.IsWindowEnabled(selected.Window)
                || !Native.IsWindowEnabled(selected.DropWindow)
                || (Native.GetWindowLongW(selected.DropWindow, -20) & 0x10) == 0)
                throw new InvalidOperationException("検出後に Excel のウィンドウ状態が変化しました。何も再送していません。");
            UIntPtr reply;
            Native.SetLastError(0);
            IntPtr result = Native.SendMessageTimeoutW(selected.DropWindow, 0, IntPtr.Zero, IntPtr.Zero,
                0x0001 | 0x0002, ProbeTimeoutMilliseconds, out reply);
            if (result == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                if (error == 0) error = 1460;
                throw new Win32Exception(error, "Excel が応答していないか、権限差でアクセスできません。"
                    + "編集、ダイアログ、計算の完了を確認してください。ファイルは送信していません。");
            }
        }

        /// <summary>
        /// 指定順の Unicode ファイルリストを、一つの WM_DROPFILES として通知する。
        /// ブック読み込み完了は待たない。OS が通知を受け付けても Excel が拒否する場合がある。
        /// </summary>
        private static void PostFileDrop(ExcelWindow selected, List<string> paths, JobState state)
        {
            state.Stage = "Unicode ファイルドロップデータを作成";
            Native.Rectangle rectangle;
            if (!Native.GetClientRect(selected.DropWindow, out rectangle))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "ドロップ先のクライアント領域を取得できません。");
            int x = Math.Max(0, Math.Min(30000, (rectangle.Right - rectangle.Left) / 2));
            int y = Math.Max(0, Math.Min(30000, (rectangle.Bottom - rectangle.Top) / 2));
            byte[] payload = BuildDropPayload(paths, x, y);
            IntPtr memory = Native.GlobalAlloc(0x0002 | 0x0040, new UIntPtr((uint)payload.Length));
            if (memory == IntPtr.Zero) throw new OutOfMemoryException("DROPFILES のメモリーを確保できません。");
            bool postingStarted = false;
            try
            {
                IntPtr pointer = Native.GlobalLock(memory);
                if (pointer == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "GlobalLock が失敗しました。");
                try { Marshal.Copy(payload, 0, pointer, payload.Length); }
                finally { Native.GlobalUnlock(memory); }
                state.CheckCancellation();
                uint processId;
                Native.GetWindowThreadProcessId(selected.DropWindow, out processId);
                if (processId != selected.ProcessId || !Native.IsWindow(selected.DropWindow))
                    throw new InvalidOperationException("ドロップ先が閉じられました。ファイルは送信していません。");
                state.Stage = "WM_DROPFILES を通知";
                state.DispatchState = "WM_DROPFILES 呼び出し開始。結果確定前の再送は禁止。";
                postingStarted = true;
                Native.SetLastError(0);
                if (!Native.PostMessageW(selected.DropWindow, 0x0233, memory, IntPtr.Zero))
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new Win32Exception(error == 0 ? 31 : error,
                        "WM_DROPFILES を通知できませんでした。Excel と本アプリを同じ通常権限で実行してください。"
                        + " UIPI の保護は解除しません。自動再送もしません。");
                }
                state.DispatchState = "WM_DROPFILES の通知成功。ブックのオープン完了は未監視。";
            }
            finally
            {
                // HDROP はポインターではなく HGLOBAL。別プロセスへは Windows がマーシャリングする。
                // PostMessage の内部で送信元ハンドルが処理され得るので、開始後は二重解放しない。
                // 受信側のデータは Excel/OS が管理する。残る送信元の資源は直後のプロセス終了で回収。
                // 呼び出す前に失敗した場合のみ、まだ自分が所有するメモリーを解放する。
                if (!postingStarted) Native.GlobalFree(memory);
            }
        }

        /// <summary>20 byte DROPFILES と UTF-16LE の二重 NUL 終端リストを組み立てて返す。</summary>
        private static byte[] BuildDropPayload(List<string> paths, int x, int y)
        {
            if (paths == null || paths.Count == 0) throw new ArgumentException("ドロップするファイルがありません。");
            StringBuilder names = new StringBuilder();
            foreach (string path in paths)
            {
                if (String.IsNullOrEmpty(path) || path.IndexOf('\0') >= 0) throw new ArgumentException("不正なファイル名です。");
                names.Append(path); names.Append('\0');
            }
            names.Append('\0');
            byte[] encoded = Encoding.Unicode.GetBytes(names.ToString());
            byte[] payload = new byte[checked(20 + encoded.Length)];
            Buffer.BlockCopy(BitConverter.GetBytes((uint)20), 0, payload, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(x), 0, payload, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(y), 0, payload, 8, 4);
            // offset 12: fNC=FALSE (client 座標)。offset 16: fWide=TRUE。
            Buffer.BlockCopy(BitConverter.GetBytes(1), 0, payload, 16, 4);
            Buffer.BlockCopy(encoded, 0, payload, 20, encoded.Length);
            return payload;
        }

        /// <summary>EXCEL.EXE を一回だけ直接起動する。newInstance=true のときだけ /x を付ける。</summary>
        private static void LaunchExcel(string excelPath, List<string> paths, bool newInstance, JobState state)
        {
            state.Stage = newInstance ? "EXCEL.EXE /x を起動" : "未起動の EXCEL.EXE を通常起動";
            StringBuilder arguments = new StringBuilder();
            if (newInstance) arguments.Append("/x ");
            for (int index = 0; index < paths.Count; index++)
            {
                if (index > 0) arguments.Append(' ');
                arguments.Append(QuoteArgument(paths[index]));
            }
            if (QuoteArgument(excelPath).Length + arguments.Length + 2 >= 32767)
                throw new ArgumentException("EXCEL.EXE のコマンドライン長の上限を超えます。指定ファイル数を減らしてください。");
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = excelPath;
            start.Arguments = arguments.ToString();
            start.WorkingDirectory = Path.GetDirectoryName(excelPath);
            start.UseShellExecute = false; // 自分に設定した .xlsx 関連付けへの再帰を防ぐ。
            start.CreateNoWindow = false;
            start.ErrorDialog = false;
            state.CheckCancellation();
            state.DispatchState = "EXCEL.EXE の起動要求開始。結果確定前の再送は禁止。";
            using (Process process = Process.Start(start))
            {
                if (process == null) throw new InvalidOperationException("EXCEL.EXE の起動結果を取得できません。");
                state.Target = "起動 PID=" + process.Id.ToString(CultureInfo.InvariantCulture);
            }
            state.DispatchState = "EXCEL.EXE の起動成功。ブックのオープン完了は未監視。";
        }

        /// <summary>Windows の argv 規則に従って一引数を引用する。シェルは介さない。</summary>
        private static string QuoteArgument(string argument)
        {
            if (argument == null) throw new ArgumentNullException("argument");
            StringBuilder text = new StringBuilder();
            text.Append('"');
            int backslashes = 0;
            foreach (char character in argument)
            {
                if (character == '\\') { backslashes++; continue; }
                if (character == '"') text.Append('\\', backslashes * 2 + 1);
                else text.Append('\\', backslashes);
                backslashes = 0;
                text.Append(character);
            }
            text.Append('\\', backslashes * 2);
            text.Append('"');
            return text.ToString();
        }

        /// <summary>現在のセッションに EXCEL プロセスがあるかを返す。常駐用監視ではない。</summary>
        private static bool HasExcelProcessInCurrentSession()
        {
            int session;
            using (Process current = Process.GetCurrentProcess()) session = current.SessionId;
            Process[] processes = Process.GetProcessesByName("EXCEL");
            bool found = false;
            try
            {
                foreach (Process process in processes)
                {
                    try { if (process.SessionId == session) found = true; }
                    catch (InvalidOperationException) { } // 列挙中に終了したプロセス。
                    catch (Win32Exception) { found = true; } // 権限差を「存在しない」と誤認しない。
                }
            }
            finally { foreach (Process process in processes) process.Dispose(); }
            return found;
        }

        /// <summary>プロセス ID に対応する実行イメージの完全パスを返す。32/64 bit を跨いで取得する。</summary>
        private static string GetProcessImage(uint processId)
        {
            IntPtr process = Native.OpenProcess(0x1000, false, processId);
            if (process == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "OpenProcess が失敗しました。");
            try
            {
                StringBuilder path = new StringBuilder(32768);
                int capacity = path.Capacity;
                if (!Native.QueryFullProcessImageNameW(process, 0, path, ref capacity))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "QueryFullProcessImageName が失敗しました。");
                return path.ToString();
            }
            finally { Native.CloseHandle(process); }
        }

        /// <summary>window のクラス名を返す。破棄済みなどの場合は空文字。</summary>
        private static string GetWindowClass(IntPtr window)
        {
            StringBuilder text = new StringBuilder(256);
            Native.GetClassNameW(window, text, text.Capacity);
            return text.ToString();
        }

        /// <summary>他プロセスのトップレベルウィンドウのタイトルを返す。</summary>
        private static string GetWindowCaption(IntPtr window)
        {
            StringBuilder text = new StringBuilder(1024);
            Native.GetWindowTextW(window, text, text.Capacity);
            return text.ToString();
        }

        /// <summary>ポインターサイズに依存せず HWND を診断用16進数として返す。</summary>
        private static string FormatHandle(IntPtr handle)
        {
            ulong value = IntPtr.Size == 8 ? unchecked((ulong)handle.ToInt64()) : unchecked((uint)handle.ToInt32());
            return "0x" + value.ToString("X", CultureInfo.InvariantCulture);
        }

        /// <summary>ファイルを開かず、登録と受信先・有効な関連付けの診断結果を返す。</summary>
        private static string MakeDiagnostics(JobState state)
        {
            state.Stage = "診断情報の収集";
            StringBuilder text = new StringBuilder();
            text.AppendLine(ApplicationName + " 2.0.0");
            text.AppendLine("自分: " + Assembly.GetExecutingAssembly().Location);
            text.AppendLine("ビット数: " + (IntPtr.Size * 8).ToString(CultureInfo.InvariantCulture));
            text.AppendLine("CLR: " + Environment.Version.ToString());
            text.AppendLine();
            try
            {
                string source;
                string path = ResolveExcelExecutable(out source);
                text.AppendLine("登録された Excel: " + path);
                text.AppendLine("取得元: " + source);
            }
            catch (Exception error) { text.AppendLine("Excel 登録エラー: " + error.Message); }
            text.AppendLine();
            text.AppendLine("受付拡張子: " + GetSupportedExtensionText());
            text.AppendLine(".lnk: 最大 16 段。リンクの引数は使用しません。");
            text.AppendLine("関連付け対象外: .txt / .dif / .slk / .lnk");
            text.AppendLine("有効な既定の実行ファイル (AssocQueryString、12 拡張子):");
            foreach (string extension in AssociatedExtensions)
            {
                text.AppendLine("  " + extension + ": " + QueryAssociatedExecutable(extension));
                string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\" + extension + @"\UserChoice";
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(keyPath, false))
                    text.AppendLine("    UserChoice.ProgId = " + (key == null ? "(なし)" : Convert.ToString(key.GetValue("ProgId"), CultureInfo.InvariantCulture)));
            }
            text.AppendLine();
            List<ExcelWindow> windows = FindExcelWindows();
            text.AppendLine("XLMAIN 数: " + windows.Count.ToString(CultureInfo.InvariantCulture));
            foreach (ExcelWindow window in windows)
            {
                text.AppendLine(window.Describe());
                text.AppendLine("  EXSTYLE=0x" + unchecked((uint)Native.GetWindowLongW(window.Window, -20)).ToString("X8", CultureInfo.InvariantCulture));
            }
            text.AppendLine();
            text.AppendLine("DROP=0x0 は、この状態では利用できる WM_DROPFILES 受信先がないことを示します。");
            text.AppendLine("診断はファイルのオープン成功や速度を検査するものではありません。");
            return text.ToString();
        }

        /// <summary>Shell が認識する拡張子の既定の実行ファイルを返す。診断用途に限る。</summary>
        private static string QueryAssociatedExecutable(string extension)
        {
            uint length = 32768;
            StringBuilder text = new StringBuilder((int)length);
            int result = Native.AssocQueryStringW(0, 2, extension, null, text, ref length);
            return result == 0 ? text.ToString() : "取得失敗 HRESULT=0x" + unchecked((uint)result).ToString("X8", CultureInfo.InvariantCulture);
        }

        /// <summary>純粋関数とネイティブ構造の自己検査を行い、成功内容を返す。</summary>
        private static string RunSelfTests()
        {
            int checks = 0;
            Assert(QuoteArgument("") == "\"\"", "空引数", ref checks);
            Assert(QuoteArgument("abc") == "\"abc\"", "通常引数", ref checks);
            Assert(QuoteArgument(@"C:\資料 A\B.xlsx") == "\"C:\\資料 A\\B.xlsx\"", "日本語・空白", ref checks);
            Assert(QuoteArgument("a\"b") == "\"a\\\"b\"", "引用符", ref checks);
            Assert(QuoteArgument("a\\") == "\"a\\\\\"", "末尾の逆斜線", ref checks);
            Assert(IsSupportedExtension(".XLSM"), "大文字拡張子", ref checks);
            Assert(!IsSupportedExtension(".exe"), "非対応拡張子", ref checks);
            Invocation parsed = ParseArguments(new string[] { "--new-instance", "--", "--name.xlsx", "資料.xls" });
            Assert(parsed.NewInstance && parsed.Files.Count == 2 && parsed.Files[0] == "--name.xlsx", "引数解釈", ref checks);
            bool rejected = false;
            try { ParseArguments(new string[] { "--diagnose", "a.xlsx" }); }
            catch (ArgumentException) { rejected = true; }
            Assert(rejected, "補助操作の誤用検出", ref checks);
            List<string> paths = new List<string>(new string[] { @"C:\資料 A.xlsx", @"C:\B.xlsm" });
            byte[] payload = BuildDropPayload(paths, 123, 456);
            Assert(BitConverter.ToUInt32(payload, 0) == 20, "DROPFILES.pFiles", ref checks);
            Assert(BitConverter.ToInt32(payload, 4) == 123 && BitConverter.ToInt32(payload, 8) == 456, "DROPFILES.pt", ref checks);
            Assert(BitConverter.ToInt32(payload, 12) == 0 && BitConverter.ToInt32(payload, 16) == 1, "DROPFILES flags", ref checks);
            Assert(Encoding.Unicode.GetString(payload, 20, payload.Length - 20) == paths[0] + "\0" + paths[1] + "\0\0", "UTF-16 と二重 NUL", ref checks);
            Assert(Marshal.SizeOf(typeof(Native.Rectangle)) == 16, "RECT レイアウト", ref checks);
            Assert(AssociatedExtensions.Length == 12 && AdditionalExtensions.Length == 3, "対応形式 12+3", ref checks);
            foreach (string extension in AssociatedExtensions)
                Assert(IsSupportedExtension(extension.ToUpperInvariant()), "受付 " + extension, ref checks);
            foreach (string extension in AdditionalExtensions)
                Assert(IsSupportedExtension(extension.ToUpperInvariant()) && !ContainsExtension(AssociatedExtensions, extension),
                    "受付のみ " + extension, ref checks);
            Assert(!IsSupportedExtension(".lnk") && !IsSupportedExtension(".xll") && !IsSupportedExtension(".pdf")
                && !IsSupportedExtension("") && !IsSupportedExtension(null), "非対象を排除", ref checks);
            Assert(ParseArguments(new string[] { "--check", "book.lnk" }).CheckOnly, "検証専用オプション", ref checks);
            rejected = false;
            try { ParseArguments(new string[] { "--check", "/x", "a.xlsx" }); }
            catch (ArgumentException) { rejected = true; }
            Assert(rejected, "検証と起動オプションの併用拒否", ref checks);
            Assert(IsAbsoluteWindowsPath(@"C:\Folder\A.xlsx") && IsAbsoluteWindowsPath(@"\\server\share\a.xlsx")
                && !IsAbsoluteWindowsPath(@"C:a.xlsx") && !IsAbsoluteWindowsPath(@"a.xlsx"), "絶対パス判定", ref checks);
            Dictionary<string, string> links = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            links.Add(@"C:\Test\A.lnk", @"C:\Test\B.lnk");
            links.Add(@"C:\Test\B.lnk", @"C:\Test\Book.xlsx");
            Func<string, string> read = delegate (string path) { return links[path]; };
            Action<string> exists = delegate (string path) { };
            JobState sample = new JobState(new string[0]);
            Assert(ResolveInputPath(@"C:\Test\A.lnk", sample, read, exists) == @"C:\Test\Book.xlsx", "リンク連鎖", ref checks);
            links[@"C:\Test\B.lnk"] = @"C:\Test\A.lnk";
            rejected = false;
            try { ResolveInputPath(@"C:\Test\A.lnk", sample, read, exists); }
            catch (ArgumentException) { rejected = true; }
            Assert(rejected, "リンク循環の拒否", ref checks);
            links[@"C:\Test\B.lnk"] = @"C:\Test\EXCEL.EXE";
            rejected = false;
            try { ResolveInputPath(@"C:\Test\A.lnk", sample, read, exists); }
            catch (ArgumentException) { rejected = true; }
            Assert(rejected, "実行ファイルへのリンクの拒否", ref checks);
            links[@"C:\Test\B.lnk"] = @"C:\Test\Book.xlsx";
            List<string> checkedPaths = ValidateFileBatch(new List<string>(new string[]
                { @"C:\Test\A.lnk", @"C:\Test\Book.xlsx", @"C:\Test\Data.csv" }), sample, read, exists);
            Assert(checkedPaths.Count == 2 && checkedPaths[0] == @"C:\Test\Book.xlsx"
                && checkedPaths[1] == @"C:\Test\Data.csv", "解決順と重複除去", ref checks);
            rejected = false;
            try { ValidateFileBatch(new List<string>(new string[] { @"C:\Test\Book.xlsx", @"C:\Test\bad.pdf", @"C:\Test\bad.exe" }), sample, read, exists); }
            catch (ArgumentException error) { rejected = error.Message.Contains("2 件") && error.Message.Contains("一件も"); }
            Assert(rejected, "複数不正入力の一括拒否", ref checks);
            rejected = false;
            try { ResolveInputPath(@"C:\Test\Book.xlsx", sample, read, delegate (string path) { throw new FileNotFoundException(path); }); }
            catch (FileNotFoundException) { rejected = true; }
            Assert(rejected, "存在確認失敗の伝達", ref checks);
            links.Clear();
            for (int index = 0; index < MaximumShortcutDepth; index++)
                links.Add(@"C:\Test\Link" + index.ToString(CultureInfo.InvariantCulture) + ".lnk",
                    index == MaximumShortcutDepth - 1 ? @"C:\Test\Book.xlsx"
                    : @"C:\Test\Link" + (index + 1).ToString(CultureInfo.InvariantCulture) + ".lnk");
            Assert(ResolveInputPath(@"C:\Test\Link0.lnk", sample, read, exists) == @"C:\Test\Book.xlsx", "16段は許可", ref checks);
            links[@"C:\Test\Link15.lnk"] = @"C:\Test\Link16.lnk";
            links.Add(@"C:\Test\Link16.lnk", @"C:\Test\Book.xlsx");
            rejected = false;
            try { ResolveInputPath(@"C:\Test\Link0.lnk", sample, read, exists); }
            catch (ArgumentException) { rejected = true; }
            Assert(rejected, "17段は拒否", ref checks);
            links.Clear();
            links.Add(@"C:\Test\Empty.lnk", "");
            rejected = false;
            try { ResolveInputPath(@"C:\Test\Empty.lnk", sample, read, exists); }
            catch (ArgumentException) { rejected = true; }
            Assert(rejected, "リンク先空欄を拒否", ref checks);
            rejected = false;
            try { NormalizeFilePath(@"C:\Test\Book.xlsx:payload"); }
            catch (ArgumentException) { rejected = true; }
            Assert(rejected, "ADS拒否", ref checks);
            rejected = false;
            try { NormalizeFilePath(@"\\?\C:\Test\Book.xlsx"); }
            catch (ArgumentException) { rejected = true; }
            Assert(rejected, "拡張長パス拒否", ref checks);
            rejected = false;
            try { NormalizeFilePath(@"https://example.invalid/Book.xlsx"); }
            catch (ArgumentException) { rejected = true; }
            Assert(rejected, "URL拒否", ref checks);
            return checks.ToString(CultureInfo.InvariantCulture) + " 個の自己検査が成功しました。\r\n"
                + "これは制御・引数・データ構造の検査です。実 .lnk の COM 解決、Excel の受信・関連付け・速度は別途実機で確認してください。";
        }

        /// <summary>condition を検査し成功数を加算する。不成立なら name を含む例外。</summary>
        private static void Assert(bool condition, string name, ref int count)
        {
            if (!condition) throw new InvalidOperationException("自己検査失敗: " + name);
            count++;
        }

        /// <summary>Usage の日本語テキストを返す。</summary>
        private static string GetUsage()
        {
            return "使い方:\r\n"
                + "  dn_excel_fast_opener.exe \"C:\\資料\\A.xlsx\" [\"C:\\資料\\B.xlsm\" ...]\r\n\r\n"
                + "既存 Excel には WM_DROPFILES、未起動なら EXCEL.EXE を直接起動します。\r\n"
                + "対応: " + GetSupportedExtensionText() + "\r\n"
                + "上記を直接指す .lnk にも対応（最大 16 段）。対象外があれば全件中止します。\r\n"
                + "関連付け: 上記のうち .txt / .dif / .slk と .lnk は登録しません。\r\n\r\n"
                + "--check [files...]  解決・存在確認だけを行う（Excel は操作しない）\r\n"
                + "--new-instance /x   明示的に別の Excel プロセスで開く\r\n"
                + "--diagnose          Excel 登録・ウィンドウ・関連付けを表示\r\n"
                + "--settings          Windows の既定のアプリ画面を開く\r\n"
                + "--refresh-associations  レジストリ適用後に関連付け変更を通知\r\n"
                + "--self-test         引数・15拡張子・リンク追跡・DROPFILES の自己検査\r\n"
                + "--help /?           この説明を表示\r\n"
                + "--                  これ以降をファイルパスとして解釈\r\n\r\n"
                + "--check 以外の補助操作は単独指定。実ファイルの読込は Excel が行います。\r\n"
                + "本アプリは成功時には何も表示せず終了し、常駐しません。\r\n"
                + "Excel と同じ通常権限で使用してください。\r\n"
                + "対象 Excel での WM_DROPFILES 対応と実際の速度は、関連付け前に確認してください。";
        }

        /// <summary>最前面・前景化を要求する Unicode MessageBox。長文は切り捨てず分割表示。</summary>
        private static void ShowText(string category, string text, bool error)
        {
            if (String.IsNullOrEmpty(text)) text = "(詳細なし)";
            int pages = (text.Length + MaximumMessageCharacters - 1) / MaximumMessageCharacters;
            for (int index = 0; index < pages; index++)
            {
                int start = index * MaximumMessageCharacters;
                string page = text.Substring(start, Math.Min(MaximumMessageCharacters, text.Length - start));
                string title = ApplicationName + " - " + category;
                if (pages > 1) title += " (" + (index + 1).ToString(CultureInfo.InvariantCulture) + "/" + pages.ToString(CultureInfo.InvariantCulture) + ")";
                // MB_TOPMOST | MB_SETFOREGROUND | MB_TASKMODAL | ICON。UAC 等のセキュア画面は越えない。
                Native.MessageBoxW(IntPtr.Zero, page, title, 0x00040000u | 0x00010000u | 0x00002000u | (error ? 0x10u : 0x40u));
            }
        }

        /// <summary>Windows 標準 API。ハンドルは IntPtr、SIZE_T は UIntPtr、BOOL は明示マーシャリング。</summary>
        private static class Native
        {
            /// <summary>ウィンドウ列挙コールバック。window は候補 HWND。false で列挙終了。</summary>
            [return: MarshalAs(UnmanagedType.Bool)]
            internal delegate bool EnumerateWindow(IntPtr window, IntPtr parameter);

            /// <summary>Win32 RECT。四つの32 bit座標で、AnyCPU でも16 byte。</summary>
            [StructLayout(LayoutKind.Sequential)]
            internal struct Rectangle
            {
                internal int Left;
                internal int Top;
                internal int Right;
                internal int Bottom;
            }

            /// <summary>Unicode メッセージボックス。戻り値は押されたボタンの ID。</summary>
            [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
            internal static extern int MessageBoxW(IntPtr owner, string text, string caption, uint type);

            /// <summary>対象デスクトップのトップレベルウィンドウを列挙する。</summary>
            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool EnumWindows(EnumerateWindow callback, IntPtr parameter);

            /// <summary>親の全子孫ウィンドウを列挙する。戻り値は判定に使わない。</summary>
            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool EnumChildWindows(IntPtr parent, EnumerateWindow callback, IntPtr parameter);

            /// <summary>HWND のクラス名を取得し、格納文字数を返す。</summary>
            [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
            internal static extern int GetClassNameW(IntPtr window, StringBuilder name, int maximum);

            /// <summary>トップレベル HWND のタイトルを取得し、格納文字数を返す。</summary>
            [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
            internal static extern int GetWindowTextW(IntPtr window, StringBuilder text, int maximum);

            /// <summary>前景の HWND を返す。</summary>
            [DllImport("user32.dll")]
            internal static extern IntPtr GetForegroundWindow();

            /// <summary>指定関係の祖先 HWND を返す。flags=2 は GA_ROOT。</summary>
            [DllImport("user32.dll")]
            internal static extern IntPtr GetAncestor(IntPtr window, uint flags);

            /// <summary>window が有効な HWND かを返す。</summary>
            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool IsWindow(IntPtr window);

            /// <summary>window に WS_VISIBLE があるかを返す。</summary>
            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool IsWindowVisible(IntPtr window);

            /// <summary>window が入力を受け付ける状態かを返す。</summary>
            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool IsWindowEnabled(IntPtr window);

            /// <summary>window が最小化されているかを返す。</summary>
            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool IsIconic(IntPtr window);

            /// <summary>ウィンドウ状態を非同期変更する。command=9 は復元。</summary>
            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool ShowWindowAsync(IntPtr window, int command);

            /// <summary>前面化を要求する。OS に拒否された場合は false。</summary>
            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool SetForegroundWindow(IntPtr window);

            /// <summary>所有スレッド ID を返し、processId に所有プロセス ID を設定する。</summary>
            [DllImport("user32.dll")]
            internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

            /// <summary>index=-20 の拡張スタイルを読む。スタイル値は64 bit環境でも32 bit。</summary>
            [DllImport("user32.dll", ExactSpelling = true)]
            internal static extern int GetWindowLongW(IntPtr window, int index);

            /// <summary>クライアント領域を取得する。失敗時は false。</summary>
            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool GetClientRect(IntPtr window, out Rectangle rectangle);

            /// <summary>WM_NULL の応答確認に使用。成功なら非ゼロ。reply は LRESULT。</summary>
            [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
            internal static extern IntPtr SendMessageTimeoutW(IntPtr window, uint message, IntPtr wordParameter,
                IntPtr longParameter, uint flags, uint timeout, out UIntPtr reply);

            /// <summary>WM_DROPFILES を通知する。wordParameter は HDROP で、単なる文字列ポインターではない。</summary>
            [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool PostMessageW(IntPtr window, uint message, IntPtr wordParameter, IntPtr longParameter);

            /// <summary>DWM 属性を取得する。attribute=14 は cloaked 状態。</summary>
            [DllImport("dwmapi.dll")]
            internal static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int valueSize);

            /// <summary>移動可能なグローバルメモリーを確保し HGLOBAL を返す。失敗時はゼロ。</summary>
            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

            /// <summary>HGLOBAL をロックし、データ領域のポインターを返す。</summary>
            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern IntPtr GlobalLock(IntPtr memory);

            /// <summary>HGLOBAL のロックを解除する。ゼロでもロック数がゼロになった正常終了の場合がある。</summary>
            [DllImport("kernel32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool GlobalUnlock(IntPtr memory);

            /// <summary>自分が所有する HGLOBAL を解放する。成功ならゼロ。</summary>
            [DllImport("kernel32.dll")]
            internal static extern IntPtr GlobalFree(IntPtr memory);

            /// <summary>ファイル属性を取得する。失敗時は UInt32.MaxValue。</summary>
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
            internal static extern uint GetFileAttributesW(string fileName);

            /// <summary>プロセスを要求アクセス権で開き、ハンドルを返す。</summary>
            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);

            /// <summary>限定照会権のプロセスから実行ファイルの完全パスを取得する。</summary>
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool QueryFullProcessImageNameW(IntPtr process, uint flags, StringBuilder fileName, ref int size);

            /// <summary>CloseHandle 対象のネイティブハンドルを閉じる。</summary>
            [DllImport("kernel32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CloseHandle(IntPtr handle);

            /// <summary>直後に呼ぶ API のため、スレッドの LastError を設定する。</summary>
            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern void SetLastError(uint error);

            /// <summary>関連付け変更を Shell に通知する。レジストリ自体は変更しない。</summary>
            [DllImport("shell32.dll")]
            internal static extern void SHChangeNotify(uint eventId, uint flags, IntPtr first, IntPtr second);

            /// <summary>拡張子の有効な関連付け文字列を取得する。戻り値は HRESULT。</summary>
            [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
            internal static extern int AssocQueryStringW(uint flags, uint query, string association,
                string extra, StringBuilder output, ref uint length);
        }
    }
}
