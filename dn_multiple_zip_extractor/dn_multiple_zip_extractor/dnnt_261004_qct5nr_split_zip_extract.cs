/*
DNNT 261004_QCT5NR 分割 ZIP 結合展開ユーティリティ

目的: 同一ディレクトリの ZIP / UNIX split 相当の ZIP 断片を認識し、安全に展開する。
対象: Windows Vista 以降 / .NET Framework 4.0 API / C# 4 / AnyCPU / WinExe。
原理: シーク可能な仮想連結ストリーム上で EOCD、ZIP64、中央・ローカルヘッダ、
      data descriptor の範囲と一致を検証する。連結中間ファイルは作らない。
      ZIP 解釈、CRC、ZipCrypto、厳密な Deflate 解釈は本ファイルで実装する。
      AES/PBKDF2/HMAC は .NET 標準の暗号プリミティブのみを使用する。
      保存は同じディレクトリの一時ファイルを検証後にハンドルで改名する。
      危険な ZIP パス・リンクは全体中断、個別の破損・保存失敗は警告して継続する。
注意: SFX/先頭・末尾ごみ、ZIP 自身のマルチディスク、Store/Deflate 以外は対象外。
      パス上限は通常の MAX_PATH。8.3 短縮名と紛らわしい名前は安全側に拒否する。
      ZIP の符号化情報がない名前は既定 CP932、復号不能時 CP437。
      環境変数 DNNT_ZIP_CODEPAGE により未指定名のコードページを変更できる。
      新規ディレクトリだけに日時を設定する。既存ディレクトリは設定しない。
      自作コードであること自体は無脆弱性を保証しない。README の制約・検証範囲参照。

本プログラムは生成 AI により生成されました。
AI バージョン・モデル: GPT-6 Astra Pro（内部ビルド識別子は取得不可）
思考レベル: このセッションの公開設定値は取得不可のため不明。
セッション開始日時(JST): 正確な値は取得不可。
本生成作業の最初の時計記録(JST): 2026-10-04 15:44:36 +09:00。
応答生成日時(JST): 2026-10-04 16:22:27 +0900 (JST)

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
            try
            {
                if (Environment.OSVersion.Platform != PlatformID.Win32NT || Environment.OSVersion.Version.Major < 6)
                    throw new PlatformNotSupportedException("Windows Vista 以降が必要です。");
                Native.EnsureConsole();
                consoleReady = true;
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
                string[] paths = GetInputs(args);
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
                using (SourceSet sourceSet = new SourceSet(sources))
                {
                    DetectedArchives detected = Detector.Detect(sourceSet.Parts);
                    Console.WriteLine("認識結果: {0} / 物理ファイル {1:N0} 個 / 論理 ZIP {2:N0} 個", detected.Mode, sources.Count, detected.Archives.Count);
                    string root = SelectDestination(Path.GetDirectoryName(sources[0]));
                    WarningBook warnings = new WarningBook();
                    using (SafeRoot safeRoot = new SafeRoot(root, warnings))
                    {
                        ExtractionPlan plan = ExtractionPlan.Build(detected.Archives, safeRoot, sourceSet, warnings);
                        plan.ConfirmDuplicates();
                        plan.BuildDirectoryTimes();
                        OverwritePolicy policy = new OverwritePolicy(plan, safeRoot, warnings);
                        policy.Preflight();
                        Extractor extractor = new Extractor(plan, safeRoot, passwords, policy, warnings);
                        try { extractor.Run(); }
                        finally { safeRoot.RestoreCreatedDirectoryTimes(plan.Directories); }
                        string summary = String.Format(NumberCulture,
                            "【{0:N0} 個の zip ファイル群 (モード: {1}) から、{2:N0} 個のファイル (合計 {3:N0} bytes) を展開完了】",
                            sources.Count, detected.Mode, extractor.SuccessCount, extractor.SuccessBytes);
                        Console.WriteLine(summary);
                        warnings.Print();
                        Console.WriteLine("意図的な省略: 重複 {0:N0} 個 / 上書きしない指定 {1:N0} 個", plan.DuplicateCount, extractor.SkippedCount);
                        Console.WriteLine(summary);
                        Console.WriteLine("展開先ディレクトリフルパス:\n" + Text.Safe(WindowsPaths.WithSlash(root)));
                        result = warnings.HasWarnings ? 299 : 0; // ERROR_PARTIAL_COPY
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
                    Console.WriteLine("何かキーを押すと終了します...");
                    try { Console.ReadKey(true); }
                    catch (InvalidOperationException) { Console.ReadLine(); }
                    catch (IOException) { }
                }
            }
            return result;
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
        private static string[] GetInputs(string[] args)
        {
            string[] selected = args;
            if (args.Length == 0)
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Title = "入力ファイル群を選択";
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
        /// <returns>ユーザーが指定した架空ファイルを含む、実際の展開先絶対ディレクトリ。</returns>
        private static string SelectDestination(string initial)
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "展開先ディレクトリを選択（_dummy.txt は作成しません）";
                dialog.Filter = "すべてのファイル (*.*)|*.*";
                dialog.FileName = "_dummy.txt";

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

    /// <summary>二つの候補モードの判定結果。</summary>
    internal sealed class DetectedArchives
    {
        internal string Mode;
        internal List<ZipArchiveData> Archives;
    }

    /// <summary>全ファイル個別検証と仮想連結検証を行い、混在・欠落・矛盾を報告する。</summary>
    internal static class Detector
    {
        internal static DetectedArchives Detect(IList<SourcePart> parts)
        {
            List<ZipArchiveData> separate = new List<ZipArchiveData>();
            List<string> problems = new List<string>();
            bool allSeparate = true;
            foreach (SourcePart part in parts)
            {
                Program.CheckCancel();
                ZipArchiveData archive;
                string problem;
                if (TryRead(new SourcePart[] { part }, out archive, out problem))
                {
                    separate.Add(archive);
                    problems.Add(Path.GetFileName(part.PathName) + ": 単独 ZIP として整合");
                }
                else
                {
                    allSeparate = false;
                    problems.Add(Path.GetFileName(part.PathName) + ": " + problem);
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
            if (allSeparate && joinedValid)
                throw new InvalidDataException("両モードで整合する曖昧な入力です。安全のため自動選択しません。");
            if (allSeparate) return new DetectedArchives { Mode = "個別複数 ZIP モード", Archives = separate };
            if (joinedValid) return new DetectedArchives { Mode = "巨大 ZIP 分割モード", Archives = new List<ZipArchiveData> { joined } };
            throw new InvalidDataException("入力の一貫性欠如。欠落断片、順序違い、別 ZIP との混在、破損または対象外形式が考えられます。\n"
                + String.Join("\n", problems.ToArray()) + "\n全断片の仮想連結: " + joinedProblem);
        }
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
            else if ((compressed != 0 && compressed != entry.CompressedSize) || (size != 0 && size != entry.Size)
                || (crc != 0 && crc != entry.Crc))
                throw new InvalidDataException("descriptor 使用時のローカル暫定値が中央情報と矛盾: " + entry.Label);
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
        internal long DuplicateCount;
        internal long FileCount
        {
            get { long n = 0; foreach (ZipEntry e in Entries) if (!e.IsDirectory && e.Keep) n++; return n; }
        }
        /// <summary>展開先に書き込まずに全 ZIP を検査し、順序を保持した計画を作成する。</summary>
        internal static ExtractionPlan Build(List<ZipArchiveData> archives, SafeRoot root, SourceSet inputs, WarningBook warnings)
        {
            ExtractionPlan plan = new ExtractionPlan();
            foreach (ZipArchiveData archive in archives)
                foreach (ZipEntry entry in archive.Entries) plan.Entries.Add(entry);
            foreach (ZipEntry entry in plan.Entries)
            {
                Program.CheckCancel();
                if (entry.SpecialObject != null) throw new SafetyException("危険な ZIP オブジェクト: " + entry.SpecialObject + " / " + entry.Label);
                // 採用名だけでなく、Unicode extra に隠された旧式名・ローカル名の遡りも拒否する。
                entry.Relative = WindowsPaths.Relative(entry.Name, entry.IsDirectory);
                WindowsPaths.Relative(entry.RawDecodedName, entry.IsDirectory);
                WindowsPaths.Relative(entry.LocalDecodedName, entry.IsDirectory);
                if (entry.IsDirectory)
                {
                    DirectoryPlan directory = plan.GetDirectory(entry.Relative);
                    if (directory != null && (directory.ExplicitTimes == null || !directory.ExplicitTimes.ModifiedUtc.HasValue))
                        directory.ExplicitTimes = entry.Times;
                    plan.AddParents(entry.Relative);
                }
                else
                {
                    List<ZipEntry> group;
                    if (!plan.groups.TryGetValue(entry.Relative, out group))
                    {
                        group = new List<ZipEntry>(); plan.groups.Add(entry.Relative, group); plan.groupOrder.Add(group);
                    }
                    group.Add(entry);
                    plan.AddParents(entry.Relative);
                }
                try
                {
                    FileStamp existing = root.Probe(entry.Relative);
                    if (existing != null && entry.IsDirectory != existing.IsDirectory)
                        entry.PlanError = "展開先のファイル / ディレクトリ種別が衝突しています: " + root.Destination(entry.Relative);
                    if (existing != null && !entry.IsDirectory && inputs.ContainsIdentity(existing.Identity))
                        entry.PlanError = "入力元 ZIP 自身への上書きは許可しません: " + root.Destination(entry.Relative);
                }
                catch (Exception ex)
                {
                    if (!Program.Recoverable(ex)) throw;
                    entry.PlanError = "展開先の事前検査失敗: " + ex.Message;
                }
            }
            foreach (KeyValuePair<string, List<ZipEntry>> pair in plan.groups)
                if (plan.Directories.ContainsKey(pair.Key))
                    throw new InvalidDataException("ZIP 内でファイルとディレクトリ（または親ディレクトリ）が衝突します: " + pair.Key
                        + " / " + pair.Value[0].Label);
            return plan;
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
        private readonly ExtractionPlan plan;
        private readonly SafeRoot root;
        private readonly PasswordManager passwords;
        private readonly OverwritePolicy overwrite;
        private readonly WarningBook warnings;
        internal long SuccessCount, SkippedCount;
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
                try
                {
                    if (entry.PlanError != null) throw new IOException(entry.PlanError);
                    EntryCodec.CheckSupported(entry);
                    if (entry.IsDirectory) ExtractDirectory(entry);
                    else ExtractFile(entry);
                }
                catch (Exception ex)
                {
                    if (!Program.Recoverable(ex)) throw;
                    warnings.Add(entry, ex.Message);
                    Console.WriteLine("  警告: " + Text.Safe(ex.Message));
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
        private void ExtractFile(ZipEntry entry)
        {
            using (PathLease check = root.DirectoryLease(WindowsPaths.Parent(entry.Relative), false, null, false))
            {
                if (!overwrite.Allow(entry, root.ProbeLeaf(entry.Relative, check))) { Skip(entry); return; }
            }
            byte[] password;
            if (!PasswordFor(entry, out password)) return;
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
                        if (!overwrite.Allow(entry, current)) { Skip(entry); return; }
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
