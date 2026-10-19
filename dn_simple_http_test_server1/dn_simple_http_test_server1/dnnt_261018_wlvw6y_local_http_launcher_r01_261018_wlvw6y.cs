/*
DNNT 261018_WLVW6Y ローカルホスト用軽量 HTTP サーバー + Chrome ランチャー dn_simple_http_test_server1

ファイル名: dnnt_261018_wlvw6y_local_http_launcher_r01_261018_wlvw6y.cs
プログラム名: dn_simple_http_test_server1
内部名: dnnt_261018_wlvw6y_local_http_launcher
版: R01（初版）。.NET Framework 4.8 / C# 7.3 / AnyCPU / コンソール Exe。

目的・仕様:
  絶対ファイル/ディレクトリパスを1個指定し、その親/自身を静的公開する。
  全アドレスのTCP待受け・UDP使用中ポートをAPI列挙し、7101～8100から両loopbackに
  Bind/Listenできた最小ポートを保持する。IPv4/IPv6とも有効である必要がある。
  HTTP/0.9、1.0、1.1、GET/HEAD/OPTIONS、Keep-Alive、pipelining、chunked要求本文、
  100-continue、単一/複数/suffix/open-ended Range、304/412、index/一覧を扱う。
  ファイルは64KiBの非同期I/Oで送信。Nagle無効、NoLinger、送受信300秒の無通信監視。
  Windowsではレジストリ等からChromeを探し、--new-windowで起動。qで即時終了、e/oで再度開く。
  LinuxではWindows固有部分だけ実行時に分岐し、同じソースでHTTP機能を検証できる。

動作原理:
  Program/ChromeLauncher → HttpServer → HttpProtocol → WebPath → ファイル/DirectoryListing。
  HttpConnectionが先読み・部分送信・通信時刻を管理し、HttpResponseが共通ヘッダを生成。
  MimeTypesは添付の1890定義行を順に登録し1296拡張子へ集約、重複は後勝ち・最長拡張子優先。

注意・仕様解釈:
  HTTP/0.9は規格どおり本文のみ。指定8ヘッダはHTTP/1.xのエラー/redirect/304/OPTIONSにも付与。
  ルートからの字句的逸脱は拒否するが、シンボリックリンク/ジャンクション先は仕様どおり許可。
  ETagはサイズ/mtimeに基づく弱いvalidator。If-Range付きGETは強比較できず全体200を返す。
  16KiB/行、64KiB/ヘッダ、100フィールド、64MiB/要求本文、16個/Range指定の上限がある。
  WindowsのNoLingerだけで即再bindを保証することはできず、排他bind不能なら次候補へ進む。
  Windows/UNC/実レジストリ/Chromeの実機試験はLinuxでのHTTP試験とは区別する。
  詳細な解釈・検証実績・原仕様は同梱historyおよびtest-results参照。

生成AI: OpenAI ChatGPT、GPT-6 Astra Pro（生成AIによる作成）。
AIバージョン: GPT-6系列。詳細なモデルsnapshot識別子はこの環境から取得できない。
思考レベル: 環境から取得できる明示値がないため未記載（推測しない）。
セッション開始日時: 2026/10/18 20:57:19 (JST)（依頼メッセージ受信日時）。
応答用ソース生成日時: 2026/10/18 21:20:45 (JST)

R01 / DNNT 261018_WLVW6Y / 2026/10/18 21:20:45 (JST)
  全機能を新規実装。入力ソースコード・過去historyは存在しない。
*/

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

[assembly: AssemblyTitle("dn_simple_http_test_server1")]
[assembly: AssemblyDescription("ローカルホスト用軽量 HTTP サーバー + Chrome ランチャー / DNNT 261018_WLVW6Y R01")]
[assembly: AssemblyProduct("dn_simple_http_test_server1")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: ComVisible(false)]

namespace DnSimpleHttpTestServer1
{

    /// <summary>
    /// コマンドラインの解釈、HTTP サーバーの開始、Chrome の起動と対話操作を担当する。
    /// </summary>
    internal static class Program
    {
        /// <summary>コンソールに表示するプログラム名。</summary>
        private const string ProgramName = "dn_simple_http_test_server1";

        /// <summary>
        /// コンソールアプリケーションの入口。ファイル指定時はその親を公開し、ディレクトリ指定時は自身を公開する。
        /// </summary>
        /// <param name="args">公開対象の絶対ファイルパスまたは絶対ディレクトリパスを 1 個だけ渡す。</param>
        /// <returns>使い方の誤りは 2、起動失敗は 1。通常は対話を継続し、q でプロセスを直接終了する。</returns>
        private static int Main(string[] args)
        {
            // 日本語を Windows コンソールおよびリダイレクト先の両方で出力できるようにする。
            try { Console.OutputEncoding = new UTF8Encoding(false); }
            catch (IOException) { }
            catch (System.Security.SecurityException) { }

            if (args == null || args.Length != 1 || !PlatformSupport.IsAbsolutePath(args[0]))
            {
                PrintUsage();
                return 2;
            }

            try
            {
                // 引数の空白を Trim しない。特に Linux では末尾空白を含むファイル名も有効である。
                string suppliedPath = Path.GetFullPath(args[0]);
                string rootPath;
                string targetPath;
                if (Directory.Exists(suppliedPath))
                {
                    rootPath = suppliedPath;
                    targetPath = "/";
                }
                else if (File.Exists(suppliedPath))
                {
                    rootPath = Path.GetDirectoryName(suppliedPath);
                    if (string.IsNullOrEmpty(rootPath))
                        throw new ArgumentException("指定ファイルの親ディレクトリを取得できません。", "args");

                    // ファイル名全体を URL の 1 セグメントとして符号化する。空白、日本語、#、%、? も正しく開く。
                    targetPath = "/" + Uri.EscapeDataString(Path.GetFileName(suppliedPath));
                }
                else
                {
                    throw new FileNotFoundException("指定したファイルまたはディレクトリが存在しないか、アクセスできません。", suppliedPath);
                }

                using (HttpServer server = new HttpServer(rootPath))
                {
                    // Start が IPv4 と IPv6 の両方の Listen を完了してから URL を表示・起動する。
                    server.Start();
                    string url = "http://127.0.0.1:" + server.Port.ToString(CultureInfo.InvariantCulture) + targetPath;
                    ConsoleUi.WriteLine(ProgramName + ": TCP ポート番号 " + server.Port.ToString(CultureInfo.InvariantCulture) + " で Listen 中");
                    ConsoleUi.WriteLine(url);
                    ChromeLauncher.Open(url);
                    RunConsoleLoop(url);
                }
                return 0;
            }
            catch (Exception exception)
            {
                ConsoleUi.WriteLine("起動に失敗しました: " + exception.ToString());
                return 1;
            }
        }

        /// <summary>実際の生成実行ファイル名を用いて、必要な引数と公開範囲を表示する。</summary>
        private static void PrintUsage()
        {
            ConsoleUi.WriteLine(ProgramName);
            ConsoleUi.WriteLine("使い方: dnnt_261018_wlvw6y_local_http_launcher.exe \"ファイルまたはディレクトリのフルパス\"");
            ConsoleUi.WriteLine("例: dnnt_261018_wlvw6y_local_http_launcher.exe \"C:\\tmp\\a.html\"");
            ConsoleUi.WriteLine("例: dnnt_261018_wlvw6y_local_http_launcher.exe \"\\\\server\\share\\web\"");
            if (!PlatformSupport.IsWindows)
                ConsoleUi.WriteLine("Linux での例: mono dnnt_261018_wlvw6y_local_http_launcher.exe /tmp/web/index.html");
            ConsoleUi.WriteLine("引数は 1 個の絶対パスです。ファイル指定時は親ディレクトリ、ディレクトリ指定時はそのディレクトリを公開します。");
        }

        /// <summary>
        /// q / e / o の 1 キー操作を待つ。標準入力が閉じてもメインスレッドを保持し、HTTP サーバーを継続する。
        /// </summary>
        /// <param name="url">初回と再起動時に Chrome へ渡す完全な HTTP URL。</param>
        private static void RunConsoleLoop(string url)
        {
            bool useCharacterInput = Console.IsInputRedirected;
            ConsoleUi.EnablePrompt();
            for (;;)
            {
                int input;
                try
                {
                    // 通常のコンソールは Enter 不要。パイプ入力では ReadKey が使えないため Read を使う。
                    input = useCharacterInput ? Console.In.Read() : Console.ReadKey(true).KeyChar;
                }
                catch (InvalidOperationException)
                {
                    if (!useCharacterInput)
                    {
                        useCharacterInput = true;
                        continue;
                    }
                    input = -1;
                }
                catch (IOException)
                {
                    input = -1;
                }

                if (input < 0)
                {
                    ConsoleUi.DisablePrompt();
                    ConsoleUi.WriteLine("標準入力が終了しました。HTTP サーバーは継続します。終了するにはプロセスを停止してください。");
                    // EOF でビジーループやプロセス終了にならないよう、CPU を消費せず待機する。
                    Thread.Sleep(Timeout.Infinite);
                    continue;
                }

                char command = char.ToLowerInvariant((char)input);
                if (command == 'q')
                {
                    // 仕様どおり finally / Dispose の実行を待たず、即座にプロセスを終了する。
                    Environment.Exit(0);
                    return;
                }
                if (command == 'e' || command == 'o')
                    ChromeLauncher.Open(url);
                // 改行や未定義キーは無視する。表示中のプロンプトを不要に増やさない。
            }
        }
    }

    /// <summary>OS に依存するパス判定と、全ローカルアドレスのポート占有情報取得をまとめる。</summary>
    internal static class PlatformSupport
    {
        /// <summary>実行時 OS が Windows NT 系なら true。それ以外の Linux 等では false。</summary>
        internal static bool IsWindows
        {
            get { return Environment.OSVersion.Platform == PlatformID.Win32NT; }
        }

        /// <summary>
        /// Windows ではドライブ文字付き絶対パスまたは完全な UNC、Linux では / から始まるパスかを判定する。
        /// </summary>
        /// <param name="path">コマンドライン等から受け取った、空白を除去していないパス。</param>
        /// <returns>カレントディレクトリやカレントドライブの補完を必要としない構文なら true。</returns>
        internal static bool IsAbsolutePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            if (!IsWindows)
                return path[0] == '/';

            // Path.IsPathRooted だけでは C:relative や \relative も通るため、完全修飾であることを別途確認する。
            string normalized = path.Replace('/', '\\');
            if (IsDriveAbsolutePath(normalized, 0))
                return true;
            if (normalized.StartsWith(@"\\?\", StringComparison.Ordinal))
            {
                if (IsDriveAbsolutePath(normalized, 4))
                    return true;
                if (normalized.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
                    return HasUncServerAndShare(normalized, 8);
                return false;
            }
            if (normalized.StartsWith(@"\\.\", StringComparison.Ordinal))
                return false;
            return normalized.StartsWith(@"\\", StringComparison.Ordinal) && HasUncServerAndShare(normalized, 2);
        }

        /// <summary>指定位置が ASCII ドライブ文字、コロン、区切り文字からなるかを判定する。</summary>
        /// <param name="path">区切りをバックスラッシュに統一したパス。</param>
        /// <param name="offset">通常は 0、拡張長パス接頭辞の後なら 4。</param>
        /// <returns>指定位置から始まる部分がドライブ絶対パスなら true。</returns>
        private static bool IsDriveAbsolutePath(string path, int offset)
        {
            if (path.Length < offset + 3)
                return false;
            char drive = path[offset];
            return ((drive >= 'A' && drive <= 'Z') || (drive >= 'a' && drive <= 'z'))
                && path[offset + 1] == ':' && path[offset + 2] == '\\';
        }

        /// <summary>UNC のサーバー名と共有名が双方とも存在することを確認する。</summary>
        /// <param name="path">区切りをバックスラッシュに統一したパス。</param>
        /// <param name="offset">サーバー名の先頭位置。</param>
        /// <returns>空でないサーバー名と共有名がある場合は true。</returns>
        private static bool HasUncServerAndShare(string path, int offset)
        {
            int separator = path.IndexOf('\\', offset);
            return separator > offset && separator + 1 < path.Length && path[separator + 1] != '\\';
        }

        /// <summary>
        /// TCP の Listen および UDP の受信エンドポイントが使っているポート番号を、アドレスによらず列挙する。
        /// </summary>
        /// <returns>IPv4 / IPv6、loopback / wildcard / その他 NIC のアドレスをすべて含めたポート番号の集合。</returns>
        /// <exception cref="NetworkInformationException">OS のネットワークテーブル取得が失敗した場合。</exception>
        internal static HashSet<int> GetOccupiedPorts()
        {
            // .NET Framework / Windows では標準ライブラリが IP Helper (GetTcpTable / GetUdpTable 等) を呼び出す。
            // netstat 子プロセスや管理者権限を必要としない。Linux では同じ API の Linux 実装へ実行時に切り替わる。
            IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
            HashSet<int> ports = new HashSet<int>();
            foreach (IPEndPoint endpoint in properties.GetActiveTcpListeners())
                ports.Add(endpoint.Port);
            foreach (IPEndPoint endpoint in properties.GetActiveUdpListeners())
                ports.Add(endpoint.Port);
            // この値はスナップショットである。競合を最終判定するのは呼び出し側による実際の両系統 Bind / Listen。
            return ports;
        }
    }

    /// <summary>Windows に登録された Chrome を探し、通常モードの新規ウインドウとして URL を開く。</summary>
    internal static class ChromeLauncher
    {
        /// <summary>初回表示または e / o 操作で Chrome を起動する。失敗してもサーバーは終了させない。</summary>
        /// <param name="url">Chrome に渡す絶対 HTTP URL。ファイル名部分は呼び出し元で URL エンコード済み。</param>
        internal static void Open(string url)
        {
            if (!PlatformSupport.IsWindows)
            {
                ConsoleUi.WriteLine("Windows 以外では Chrome の自動起動を省略します。上記 URL をブラウザで開けます。");
                return;
            }

            try
            {
                string executable = FindChromePath();
                if (executable == null)
                {
                    ConsoleUi.WriteLine("Google Chrome が見つかりません。HTTP サーバーは継続します。URL を手動で開いてください。");
                    return;
                }

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = "--new-window " + QuoteWindowsArgument(url),
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(executable)
                };
                // Process の Dispose は起動した Chrome の終了ではなく、こちらのプロセスハンドル解放だけを行う。
                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                        throw new InvalidOperationException("Chrome プロセスの開始結果を取得できませんでした。");
                }
                ConsoleUi.WriteLine("Google Chrome を新規ウインドウ指定で起動しました: " + url);
            }
            catch (Exception exception)
            {
                ConsoleUi.WriteLine("Google Chrome の起動に失敗しました: " + exception.Message + " HTTP サーバーは継続します。");
            }
        }

        /// <summary>ユーザー登録を優先し、32 / 64 ビットのレジストリと既定インストール先を調べる。</summary>
        /// <returns>実在する chrome.exe の絶対パス。見つからなければ null。</returns>
        private static string FindChromePath()
        {
            RegistryHive[] hives = { RegistryHive.CurrentUser, RegistryHive.LocalMachine };
            RegistryView[] views = Environment.Is64BitOperatingSystem
                ? new[] { RegistryView.Registry64, RegistryView.Registry32 }
                : new[] { RegistryView.Registry32 };

            // App Paths の既定値は実行ファイルそのものの絶対パスとして扱い、空白で分割しない。
            foreach (RegistryHive hive in hives)
            {
                foreach (RegistryView view in views)
                {
                    string candidate = ValidateExecutable(ReadRegistryString(hive, view,
                        @"Software\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe"));
                    if (candidate != null)
                        return candidate;
                }
            }

            string[] commandKeys =
            {
                @"Software\Classes\ChromeHTML\shell\open\command",
                @"Software\Clients\StartMenuInternet\Google Chrome\shell\open\command",
                @"Software\Classes\Applications\chrome.exe\shell\open\command"
            };
            foreach (RegistryHive hive in hives)
            {
                foreach (RegistryView view in views)
                {
                    foreach (string commandKey in commandKeys)
                    {
                        string candidate = ExecutableFromCommand(ReadRegistryString(hive, view, commandKey));
                        if (candidate != null)
                            return candidate;
                    }
                }
            }

            string[] baseDirectories =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetEnvironmentVariable("ProgramW6432")
            };
            foreach (string baseDirectory in baseDirectories)
            {
                if (string.IsNullOrEmpty(baseDirectory))
                    continue;
                string candidate = ValidateExecutable(Path.Combine(baseDirectory, @"Google\Chrome\Application\chrome.exe"));
                if (candidate != null)
                    return candidate;
            }
            return null;
        }

        /// <summary>指定したレジストリビューにあるキーの既定文字列値を、読み取り専用で取得する。</summary>
        /// <param name="hive">CurrentUser または LocalMachine。</param>
        /// <param name="view">Registry32 または Registry64。</param>
        /// <param name="subKeyPath">ハイブを除いたサブキーの完全名。</param>
        /// <returns>既定文字列。キーなし・アクセス拒否などの場合は null。</returns>
        private static string ReadRegistryString(RegistryHive hive, RegistryView view, string subKeyPath)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey key = baseKey.OpenSubKey(subKeyPath, false))
                    return key == null ? null : key.GetValue(null, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
            }
            catch (System.Security.SecurityException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (IOException) { return null; }
        }

        /// <summary>コマンド登録から chrome.exe の部分のみを取り出し、登録済み追加引数は引き継がない。</summary>
        /// <param name="command">レジストリの shell\open\command の既定値。</param>
        /// <returns>実在する Chrome の絶対パス。正しく取り出せなければ null。</returns>
        private static string ExecutableFromCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return null;
            command = Environment.ExpandEnvironmentVariables(command).Trim();
            if (command[0] == '"')
            {
                int closingQuote = command.IndexOf('"', 1);
                return closingQuote > 1 ? ValidateExecutable(command.Substring(1, closingQuote - 1)) : null;
            }

            // 引用符のない登録でも "Program Files" の空白では区切らない。
            int start = 0;
            for (;;)
            {
                int extension = command.IndexOf(".exe", start, StringComparison.OrdinalIgnoreCase);
                if (extension < 0)
                    return null;
                int end = extension + 4;
                if (end == command.Length || char.IsWhiteSpace(command[end]))
                {
                    string candidate = ValidateExecutable(command.Substring(0, end));
                    if (candidate != null)
                        return candidate;
                }
                start = end;
            }
        }

        /// <summary>候補文字列から余分な外側引用符を除去し、絶対パスの chrome.exe が実在するかを確認する。</summary>
        /// <param name="value">App Paths 値、コマンドから抽出したパス、または既定配置候補。</param>
        /// <returns>検証済みパス。候補なし・不正・ファイルなしなら null。</returns>
        private static string ValidateExecutable(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            string path = Environment.ExpandEnvironmentVariables(value).Trim();
            if (path.Length >= 2 && path[0] == '"' && path[path.Length - 1] == '"')
                path = path.Substring(1, path.Length - 2);
            try
            {
                return PlatformSupport.IsAbsolutePath(path)
                    && string.Equals(Path.GetFileName(path), "chrome.exe", StringComparison.OrdinalIgnoreCase)
                    && File.Exists(path) ? path : null;
            }
            catch (ArgumentException) { return null; }
            catch (NotSupportedException) { return null; }
            catch (PathTooLongException) { return null; }
        }

        /// <summary>Windows の argv 規則に従い、1 引数の引用符と直前のバックスラッシュをエスケープする。</summary>
        /// <param name="value">Chrome に 1 個の引数として渡す URL。</param>
        /// <returns>外側の二重引用符を含む、ProcessStartInfo.Arguments 用文字列。</returns>
        private static string QuoteWindowsArgument(string value)
        {
            StringBuilder result = new StringBuilder(value.Length + 2);
            result.Append('"');
            int backslashes = 0;
            foreach (char character in value)
            {
                if (character == '\\')
                {
                    backslashes++;
                    continue;
                }
                if (character == '"')
                    result.Append('\\', backslashes * 2 + 1);
                else
                    result.Append('\\', backslashes);
                result.Append(character);
                backslashes = 0;
            }
            result.Append('\\', backslashes * 2);
            result.Append('"');
            return result.ToString();
        }
    }

    /// <summary>HTTP 処理スレッドと対話スレッドのコンソール出力を同期し、エラーの後にプロンプトを戻す。</summary>
    internal static class ConsoleUi
    {
        /// <summary>1 行出力とプロンプトを他のスレッドから分断されないための排他オブジェクト。</summary>
        private static readonly object OutputLock = new object();
        /// <summary>仕様で指定された、常時表示する操作案内。</summary>
        private const string Prompt = "q: 終了、e または o: Web ブラウザでページを再度開く?>";
        /// <summary>対話中でプロンプトを再表示すべきか。OutputLock 内でのみ読み書きする。</summary>
        private static bool promptEnabled;
        /// <summary>現在の出力末尾に改行なしのプロンプトがあるか。OutputLock 内でのみ読み書きする。</summary>
        private static bool promptVisible;
        /// <summary>切断済みの標準出力に繰り返し書き込まないための状態。OutputLock 内でのみ読み書きする。</summary>
        private static bool outputClosed;

        /// <summary>1 件のメッセージを出力し、操作中なら末尾にプロンプトを再表示する。</summary>
        /// <param name="message">表示する文字列。複数スレッドから同時に呼び出してよい。</param>
        internal static void WriteLine(string message)
        {
            lock (OutputLock)
            {
                if (outputClosed)
                    return;
                try
                {
                    if (promptVisible)
                        Console.WriteLine();
                    promptVisible = false;
                    Console.WriteLine(message);
                    if (promptEnabled)
                    {
                        Console.Write(Prompt);
                        promptVisible = true;
                    }
                }
                catch (IOException) { outputClosed = true; }
                catch (ObjectDisposedException) { outputClosed = true; }
            }
        }

        /// <summary>対話プロンプトを有効にして初回表示する。</summary>
        internal static void EnablePrompt()
        {
            lock (OutputLock)
            {
                promptEnabled = true;
                if (promptVisible || outputClosed)
                    return;
                try
                {
                    Console.Write(Prompt);
                    promptVisible = true;
                }
                catch (IOException) { outputClosed = true; }
                catch (ObjectDisposedException) { outputClosed = true; }
            }
        }

        /// <summary>標準入力の EOF 後などに、利用できなくなった操作プロンプトの再表示を止める。</summary>
        internal static void DisablePrompt()
        {
            lock (OutputLock)
                promptEnabled = false;
        }

        /// <summary>HTTP レイヤーのエラーを、時刻・ステータス・メソッド・対象・理由とともに記録する。</summary>
        /// <param name="status">HTTP ステータスコード。例: 404。</param>
        /// <param name="method">受け付けたメソッド。未解析なら null でもよい。</param>
        /// <param name="target">受信した request-target。制御文字は表示時にエスケープする。</param>
        /// <param name="detail">サーバーが判断した具体的なエラー理由。</param>
        internal static void LogHttpError(int status, string method, string target, string detail)
        {
            WriteLine("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "] HTTP "
                + status.ToString(CultureInfo.InvariantCulture) + " " + EscapeLogValue(method) + " "
                + EscapeLogValue(target) + ": " + EscapeLogValue(detail));
        }

        /// <summary>ログへ混入した改行や端末制御文字を可視化し、極端に長い値の出力を抑える。</summary>
        /// <param name="value">リクエストまたはエラー理由の文字列。</param>
        /// <returns>最大 2048 入力文字を扱った 1 行の安全な表示文字列。値なしは -。</returns>
        private static string EscapeLogValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "-";
            int count = Math.Min(value.Length, 2048);
            StringBuilder result = new StringBuilder(count);
            for (int index = 0; index < count; index++)
            {
                char character = value[index];
                if (char.IsControl(character) || character == '\u2028' || character == '\u2029')
                {
                    result.Append(@"\u");
                    result.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
                }
                else
                {
                    result.Append(character);
                }
            }
            if (count < value.Length)
                result.Append("...");
            return result.ToString();
        }
    }

    /// <summary>IPv4/IPv6 の両ループバックだけで動く非同期の静的 HTTP サーバー。</summary>
    internal sealed class HttpServer : IDisposable
    {
        internal const int IdleTimeoutSeconds = 300;
        internal const int TransferBufferSize = 65536;
        private readonly string rootPath;
        private readonly string rootPrefix;
        private readonly ConcurrentDictionary<long, HttpConnection> connections = new ConcurrentDictionary<long, HttpConnection>();
        private Socket ipv4Listener;
        private Socket ipv6Listener;
        private Timer idleTimer;
        private long nextConnectionId;
        private int stopping;
        private bool started;

        /// <summary>公開ルートを保持する。リンク先の実パスへ解決しないため、ジャンクション等を辿れる。</summary>
        /// <param name="rootPath">存在する公開ディレクトリの絶対パス。</param>
        internal HttpServer(string rootPath)
        {
            string full = Path.GetFullPath(rootPath);
            string volumeRoot = Path.GetPathRoot(full);
            this.rootPath = full.Length > volumeRoot.Length ? full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : full;
            rootPrefix = this.rootPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? this.rootPath : this.rootPath + Path.DirectorySeparatorChar;
        }

        /// <summary>両アドレスで確保した TCP ポート番号。Start 成功後に参照する。</summary>
        internal int Port { get; private set; }

        /// <summary>全アドレスの TCP listen/UDP bind を除外し、7101～8100 の最小の利用可能ポートで開始する。</summary>
        internal void Start()
        {
            if (started) throw new InvalidOperationException("HTTP サーバーはすでに開始されています。");
            if (!Socket.OSSupportsIPv4 || !Socket.OSSupportsIPv6)
                throw new NotSupportedException("IPv4 と IPv6 の両方が使用可能である必要があります。");
            HashSet<int> occupied = PlatformSupport.GetOccupiedPorts();
            Exception lastError = null;
            for (int candidate = 7101; candidate < 8101; candidate++)
            {
                if (occupied.Contains(candidate)) continue;
                Socket first = null;
                Socket second = null;
                try
                {
                    first = CreateListener(IPAddress.Loopback, candidate);
                    second = CreateListener(IPAddress.IPv6Loopback, candidate);
                    ipv4Listener = first;
                    ipv6Listener = second;
                    Port = candidate;
                    started = true;
                    break;
                }
                catch (SocketException exception)
                {
                    lastError = exception;
                    if (first != null) first.Dispose();
                    if (second != null) second.Dispose();
                }
            }
            if (!started) throw new IOException("7101～8100 の 1000 候補で IPv4/IPv6 の両方を確保できませんでした。", lastError);
            // SendTimeout/ReceiveTimeout は同期 I/O 用なので、非同期 I/O もこの監視で期限を保証する。
            idleTimer = new Timer(CheckIdleConnections, null, 1000, 1000);
            _ = AcceptLoopAsync(ipv4Listener);
            _ = AcceptLoopAsync(ipv6Listener);
        }

        /// <summary>1 アドレスの listen socket を作る。失敗した socket は必ず閉じる。</summary>
        /// <param name="address">127.0.0.1 または ::1。</param><param name="port">候補ポート。</param>
        /// <returns>Bind/Listen 済みの socket。</returns>
        private static Socket CreateListener(IPAddress address, int port)
        {
            Socket listener = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                listener.NoDelay = true;
                listener.LingerState = new LingerOption(false, 0);
                if (PlatformSupport.IsWindows)
                {
                    // Windows の SO_REUSEADDR は別プロセスの二重 bind も許すため、確保は排他的に行う。
                    // Windows が再 bind を拒む残存状態は候補選択で次のポートへ進む。
                    listener.ExclusiveAddressUse = true;
                }
                else listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                if (address.AddressFamily == AddressFamily.InterNetworkV6) listener.DualMode = false;
                listener.Bind(new IPEndPoint(address, port));
                listener.Listen(512);
                return listener;
            }
            catch { listener.Dispose(); throw; }
        }

        /// <summary>待受けを継続し、各接続を非同期に処理する。待機中の接続に専用スレッドを割り当てない。</summary>
        /// <param name="listener">片方のループバック待受け socket。</param>
        private async Task AcceptLoopAsync(Socket listener)
        {
            try
            {
                while (Volatile.Read(ref stopping) == 0)
                {
                    Socket socket;
                    try { socket = await listener.AcceptAsync().ConfigureAwait(false); }
                    catch (SocketException exception)
                    {
                        // Windows AcceptEx は受理直前の相手切断でも reset/aborted を返す。待受け全体は継続する。
                        if (IsTransientAcceptError(exception)) continue;
                        throw;
                    }
                    if (Volatile.Read(ref stopping) != 0) { socket.Dispose(); break; }
                    HttpConnection connection;
                    try { connection = new HttpConnection(socket); }
                    catch (SocketException exception)
                    {
                        socket.Dispose();
                        if (IsTransientAcceptError(exception)) continue;
                        throw;
                    }
                    catch { socket.Dispose(); throw; }
                    long id = Interlocked.Increment(ref nextConnectionId);
                    connections[id] = connection;
                    // 同期完了が連続する大量 pipeline に accept ループを占有させない。
                    // 開始だけを thread pool に渡し、通信待ちは引き続き非同期 I/O で行う。
                    _ = Task.Run(() => ServeConnectionAsync(id, connection));
                }
            }
            catch (ObjectDisposedException) { }
            catch (Exception exception)
            {
                if (Volatile.Read(ref stopping) == 0)
                {
                    ConsoleUi.WriteLine("HTTP 待受けエラー: " + exception.Message);
                    Dispose();
                    Environment.Exit(1);
                }
            }
        }

        /// <summary>受理直前/直後のクライアント切断による一時エラーを判定する。</summary>
        /// <param name="exception">受理処理のエラー。</param><returns>次の接続の受理を継続できれば true。</returns>
        private static bool IsTransientAcceptError(SocketException exception)
        {
            return exception.SocketErrorCode == SocketError.ConnectionReset || exception.SocketErrorCode == SocketError.ConnectionAborted ||
                exception.SocketErrorCode == SocketError.NetworkReset;
        }

        /// <summary>同じ接続内の要求は順に処理し、pipelining の応答順を保つ。</summary>
        /// <param name="id">接続辞書の識別子。</param><param name="connection">処理対象接続。</param>
        private async Task ServeConnectionAsync(long id, HttpConnection connection)
        {
            HttpRequest request = null;
            try
            {
                while (Volatile.Read(ref stopping) == 0)
                {
                    request = new HttpRequest();
                    if (!await HttpProtocol.ReadRequestAsync(connection, request).ConfigureAwait(false)) break;
                    if (request.Method != "GET" && request.Method != "HEAD" && request.Method != "OPTIONS")
                    {
                        request.KeepAlive = false;
                        await HttpResponse.SendErrorAsync(connection, request, 405, "GET / HEAD / OPTIONS のみを扱います。",
                            new Dictionary<string, string> { { "Allow", "GET, HEAD, OPTIONS" } }).ConfigureAwait(false);
                        break;
                    }
                    await HttpProtocol.DiscardBodyAsync(connection, request).ConfigureAwait(false);
                    await ServeResourceAsync(connection, request).ConfigureAwait(false);
                    if (!request.KeepAlive || request.IsHttp09) break;
                }
            }
            catch (HttpProtocolException exception)
            {
                if (request == null) request = new HttpRequest();
                request.KeepAlive = false;
                if (!request.ResponseStarted && !connection.IsClosed)
                {
                    try { await HttpResponse.SendErrorAsync(connection, request, exception.StatusCode, exception.Message, null).ConfigureAwait(false); }
                    catch (Exception) { }
                }
            }
            catch (Exception exception)
            {
                // 通信断・300 秒の無通信切断は通常動作。ファイル送信途中の切断に二つ目の応答を混ぜない。
                if (!(exception is SocketException) && !(exception is ObjectDisposedException) && !connection.IsClosed)
                {
                    if (request == null) request = new HttpRequest();
                    if (request.ResponseStarted)
                        ConsoleUi.LogHttpError(500, request.Method, request.Target, "応答途中に処理を中断: " + exception.Message);
                    else
                    {
                        request.KeepAlive = false;
                        try { await HttpResponse.SendErrorAsync(connection, request, 500, "HTTP 処理中にエラーが発生しました。", null).ConfigureAwait(false); }
                        catch (Exception) { }
                    }
                }
            }
            finally
            {
                HttpConnection ignored;
                connections.TryRemove(id, out ignored);
                connection.Dispose();
            }
        }

        /// <summary>1 秒ごとに monotonic clock で直近の実送受信を調べ、300 秒以上無通信の接続を閉じる。</summary>
        /// <param name="state">Timer の状態（未使用）。</param>
        private void CheckIdleConnections(object state)
        {
            long now = Stopwatch.GetTimestamp();
            foreach (HttpConnection connection in connections.Values)
                if (now - connection.LastActivity >= (long)IdleTimeoutSeconds * Stopwatch.Frequency) connection.Dispose();
        }

        /// <summary>通常の終了・開始失敗時に待受けと接続を閉じる。q キーでは直接プロセスを終了する。</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref stopping, 1) != 0) return;
            if (idleTimer != null) idleTimer.Dispose();
            if (ipv4Listener != null) ipv4Listener.Dispose();
            if (ipv6Listener != null) ipv6Listener.Dispose();
            foreach (HttpConnection connection in connections.Values) connection.Dispose();
        }

        /// <summary>公開パスを決め、OPTIONS、ディレクトリ、index、通常ファイルへ振り分ける。</summary>
        /// <param name="connection">接続。</param><param name="request">検証済み要求。</param>
        private async Task ServeResourceAsync(HttpConnection connection, HttpRequest request)
        {
            if (request.Method == "OPTIONS" && request.Target == "*")
            {
                await SendOptionsAsync(connection, request).ConfigureAwait(false);
                return;
            }
            MappedPath mapped = WebPath.Map(rootPath, rootPrefix, request.Target);
            if (request.Method == "OPTIONS")
            {
                await SendOptionsAsync(connection, request).ConfigureAwait(false);
                return;
            }
            try
            {
                if (Directory.Exists(mapped.PhysicalPath))
                {
                    if (!mapped.HasTrailingSlash && !request.IsHttp09)
                    {
                        string location = mapped.UrlPath.TrimEnd('/') + "/" + mapped.Query;
                        byte[] body = HttpResponse.Utf8.GetBytes("<!doctype html><meta charset=\"utf-8\"><title>Moved Permanently</title><a href=\"" +
                            WebUtility.HtmlEncode(location) + "\">ディレクトリを開く</a>\n");
                        await HttpResponse.SendBytesAsync(connection, request, 301, "text/html; charset=utf-8", body,
                            new Dictionary<string, string> { { "Location", location } }).ConfigureAwait(false);
                        return;
                    }
                    string[] indexNames = { "index.html", "index.htm", "default.html", "default.htm" };
                    foreach (string indexName in indexNames)
                    {
                        string indexPath = Path.Combine(mapped.PhysicalPath, indexName);
                        if (File.Exists(indexPath))
                        {
                            await ServeFileAsync(connection, request, indexPath).ConfigureAwait(false);
                            return;
                        }
                    }
                    byte[] listing = DirectoryListing.Create(mapped.PhysicalPath, mapped.UrlPath.TrimEnd('/') + "/");
                    string listingTag;
                    using (System.Security.Cryptography.SHA256 hash = System.Security.Cryptography.SHA256.Create())
                        listingTag = "W/\"listing-" + BitConverter.ToString(hash.ComputeHash(listing)).Replace("-", "").ToLowerInvariant() + "\"";
                    Dictionary<string, string> listingHeaders = new Dictionary<string, string> { { "ETag", listingTag } };
                    // 一覧は子ファイルのサイズ・時刻にも依存するため、ディレクトリ mtime を Last-Modified に流用しない。
                    int listingStatus = request.IsHttp09 ? 200 : HttpValidators.Evaluate(request, listingTag, null);
                    if (listingStatus == 304)
                        await HttpResponse.SendHeadersAsync(connection, request, 304, null, null, listingHeaders).ConfigureAwait(false);
                    else if (listingStatus == 412)
                        await HttpResponse.SendErrorAsync(connection, request, 412, "ディレクトリ一覧の事前条件を満たしません。", listingHeaders).ConfigureAwait(false);
                    else
                        await HttpResponse.SendBytesAsync(connection, request, 200, "text/html; charset=utf-8", listing, listingHeaders).ConfigureAwait(false);
                    return;
                }
                if (mapped.HasTrailingSlash)
                {
                    await HttpResponse.SendErrorAsync(connection, request, 404, "指定されたディレクトリが見つかりません。", null).ConfigureAwait(false);
                    return;
                }
                // File.Exists は権限エラーも false にするため、直接 open して 403/404 を区別する。
                await ServeFileAsync(connection, request, mapped.PhysicalPath).ConfigureAwait(false);
            }
            catch (FileNotFoundException)
            {
                if (request.ResponseStarted) throw;
                await HttpResponse.SendErrorAsync(connection, request, 404, "指定されたファイルが見つかりません。", null).ConfigureAwait(false);
            }
            catch (DirectoryNotFoundException)
            {
                if (request.ResponseStarted) throw;
                await HttpResponse.SendErrorAsync(connection, request, 404, "指定されたディレクトリが見つかりません。", null).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
                if (request.ResponseStarted) throw;
                await HttpResponse.SendErrorAsync(connection, request, 403, "ファイルまたはディレクトリにアクセスできません。", null).ConfigureAwait(false);
            }
            catch (System.Security.SecurityException)
            {
                if (request.ResponseStarted) throw;
                await HttpResponse.SendErrorAsync(connection, request, 403, "ファイルまたはディレクトリの読み取りが許可されていません。", null).ConfigureAwait(false);
            }
            catch (PathTooLongException)
            {
                if (request.ResponseStarted) throw;
                await HttpResponse.SendErrorAsync(connection, request, 414, "ファイルシステムが扱えるパス長を超えています。", null).ConfigureAwait(false);
            }
        }

        /// <summary>許可するメソッドと指定の CORS ヘッダを含む 204 を送る。</summary>
        /// <param name="connection">接続。</param><param name="request">要求。</param>
        private static Task SendOptionsAsync(HttpConnection connection, HttpRequest request)
        {
            return HttpResponse.SendHeadersAsync(connection, request, 204, null, null,
                new Dictionary<string, string> { { "Allow", "GET, HEAD, OPTIONS" } });
        }

        /// <summary>ファイルを開き、条件付き要求と Range を評価して 64 KiB 単位で直接送信する。</summary>
        /// <param name="connection">接続。</param><param name="request">要求。</param><param name="filePath">検証済み絶対パス。</param>
        private static async Task ServeFileAsync(HttpConnection connection, HttpRequest request, string filePath)
        {
            using (FileStream file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
                TransferBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                long length = file.Length;
                DateTime rawModified = File.GetLastWriteTimeUtc(filePath);
                DateTime now = DateTime.UtcNow;
                // HTTP の Last-Modified は Date より未来にしてはいけない。弱 ETag には元の値を保持する。
                DateTime modified = rawModified > now ? now : rawModified;
                // サイズと mtime は完全な内容 hash ではないので、強い ETag と偽らず W/ を付ける。
                string entityTag = "W/\"" + length.ToString("x", CultureInfo.InvariantCulture) + "-" + rawModified.Ticks.ToString("x", CultureInfo.InvariantCulture) + "\"";
                string mime = MimeTypes.Get(filePath);
                Dictionary<string, string> headers = new Dictionary<string, string>
                {
                    { "Accept-Ranges", "bytes" },
                    { "Last-Modified", modified.ToString("r", CultureInfo.InvariantCulture) },
                    { "ETag", entityTag }
                };
                int conditionalStatus = request.IsHttp09 ? 200 : HttpValidators.Evaluate(request, entityTag, modified);
                if (conditionalStatus == 304)
                {
                    await HttpResponse.SendHeadersAsync(connection, request, 304, null, null, headers).ConfigureAwait(false);
                    return;
                }
                if (conditionalStatus == 412)
                {
                    await HttpResponse.SendErrorAsync(connection, request, 412, "指定されたファイルの事前条件を満たしません。", headers).ConfigureAwait(false);
                    return;
                }
                List<ByteRange> ranges = null;
                // Range の対象は GET のみ。If-Range は強い validator が必要だが、本サーバーは metadata の弱い
                // validator しか発行しないので、日付も含め If-Range の条件を成立扱いせず全体を 200 で返す。
                if (request.Method == "GET" && !request.IsHttp09 && request.GetHeader("If-Range") == null)
                    ranges = RangeParser.Parse(request.GetHeader("Range"), length);
                if (ranges != null && ranges.Count == 0)
                {
                    headers["Content-Range"] = "bytes */" + length.ToString(CultureInfo.InvariantCulture);
                    await HttpResponse.SendErrorAsync(connection, request, 416, "指定されたバイト範囲はファイルに含まれません。", headers).ConfigureAwait(false);
                    return;
                }
                if (ranges == null)
                {
                    await HttpResponse.SendHeadersAsync(connection, request, 200, mime, length, headers).ConfigureAwait(false);
                    if (request.Method != "HEAD") await SendFileRegionAsync(connection, file, 0, length, new byte[TransferBufferSize]).ConfigureAwait(false);
                    return;
                }
                if (ranges.Count == 1)
                {
                    ByteRange range = ranges[0];
                    headers["Content-Range"] = range.ContentRange(length);
                    await HttpResponse.SendHeadersAsync(connection, request, 206, mime, range.Length, headers).ConfigureAwait(false);
                    await SendFileRegionAsync(connection, file, range.Start, range.Length, new byte[TransferBufferSize]).ConfigureAwait(false);
                    return;
                }
                string boundary = "dnnt_" + Guid.NewGuid().ToString("N");
                List<byte[]> partHeaders = new List<byte[]>();
                long responseLength = 0;
                foreach (ByteRange range in ranges)
                {
                    byte[] partHeader = Encoding.ASCII.GetBytes("--" + boundary + "\r\nContent-Type: " + mime +
                        "\r\nContent-Range: " + range.ContentRange(length) + "\r\n\r\n");
                    partHeaders.Add(partHeader);
                    responseLength = checked(responseLength + partHeader.Length + range.Length + 2);
                }
                byte[] ending = Encoding.ASCII.GetBytes("--" + boundary + "--\r\n");
                responseLength = checked(responseLength + ending.Length);
                await HttpResponse.SendHeadersAsync(connection, request, 206, "multipart/byteranges; boundary=" + boundary, responseLength, headers).ConfigureAwait(false);
                byte[] buffer = new byte[TransferBufferSize];
                byte[] crlf = { 13, 10 };
                for (int i = 0; i < ranges.Count; i++)
                {
                    await connection.SendAsync(partHeaders[i], 0, partHeaders[i].Length).ConfigureAwait(false);
                    await SendFileRegionAsync(connection, file, ranges[i].Start, ranges[i].Length, buffer).ConfigureAwait(false);
                    await connection.SendAsync(crlf, 0, crlf.Length).ConfigureAwait(false);
                }
                await connection.SendAsync(ending, 0, ending.Length).ConfigureAwait(false);
            }
        }

        /// <summary>指定領域を seek し、ファイル全体をメモリに載せず送信する。</summary>
        /// <param name="connection">接続。</param><param name="file">open 済みファイル。</param><param name="offset">開始位置。</param>
        /// <param name="count">送信長。</param><param name="buffer">再利用バッファ。</param>
        private static async Task SendFileRegionAsync(HttpConnection connection, FileStream file, long offset, long count, byte[] buffer)
        {
            file.Seek(offset, SeekOrigin.Begin);
            while (count > 0)
            {
                int read = await file.ReadAsync(buffer, 0, (int)Math.Min(count, buffer.Length)).ConfigureAwait(false);
                if (read == 0) throw new EndOfStreamException("送信中にファイルが短縮されました。");
                await connection.SendAsync(buffer, 0, read).ConfigureAwait(false);
                count -= read;
            }
        }
    }

    /// <summary>1 接続のソケット、先読みバッファ、無通信期限をまとめる。読み書きは要求処理から直列に呼ぶ。</summary>
    internal sealed class HttpConnection : IDisposable
    {
        private static readonly Encoding HeaderEncoding = Encoding.GetEncoding(28591);
        private readonly Socket socket;
        private readonly byte[] input = new byte[16384];
        private int offset;
        private int available;
        private long lastActivity = Stopwatch.GetTimestamp();
        private int closed;

        /// <summary>受理済み socket に全通信共通設定を適用する。</summary><param name="socket">所有権を移す接続 socket。</param>
        internal HttpConnection(Socket socket)
        {
            this.socket = socket;
            socket.NoDelay = true;
            socket.LingerState = new LingerOption(false, 0);
            socket.ReceiveTimeout = HttpServer.IdleTimeoutSeconds * 1000;
            socket.SendTimeout = HttpServer.IdleTimeoutSeconds * 1000;
        }
        /// <summary>直近に正のバイト数の送受信が成功した時刻。</summary>
        internal long LastActivity { get { return Interlocked.Read(ref lastActivity); } }
        /// <summary>監視または終了処理によって閉じたか。</summary>
        internal bool IsClosed { get { return Volatile.Read(ref closed) != 0; } }

        /// <summary>先読みバッファを補充する。</summary><returns>読み込んだバイト数。0 は相手の EOF。</returns>
        private async Task<int> FillAsync()
        {
            offset = 0;
            available = await socket.ReceiveAsync(new ArraySegment<byte>(input), SocketFlags.None).ConfigureAwait(false);
            if (available > 0) Interlocked.Exchange(ref lastActivity, Stopwatch.GetTimestamp());
            return available;
        }

        /// <summary>LF まで読み、末尾 CR を除く。分割受信・先読みを保持し、単独 CR を拒む。</summary>
        /// <param name="limit">改行を除く行長の上限。</param><param name="overflowStatus">上限超過の HTTP status。</param>
        /// <returns>改行なしの行。行頭で EOF なら null。</returns>
        internal async Task<string> ReadLineAsync(int limit, int overflowStatus)
        {
            StringBuilder builder = null;
            int accumulated = 0;
            while (true)
            {
                if (offset == available && await FillAsync().ConfigureAwait(false) == 0)
                {
                    if (accumulated == 0) return null;
                    throw new HttpProtocolException(400, "HTTP 行の途中で接続が終了しました。");
                }
                int end = offset;
                while (end < available && input[end] != 10) end++;
                int count = end - offset;
                if (accumulated + count > limit + 1) throw new HttpProtocolException(overflowStatus, "HTTP 行が上限を超えています。");
                string part = HeaderEncoding.GetString(input, offset, count);
                accumulated += count;
                offset = end < available ? end + 1 : end;
                if (builder != null) builder.Append(part);
                if (end < available)
                {
                    string line = builder == null ? part : builder.ToString();
                    if (line.EndsWith("\r", StringComparison.Ordinal)) line = line.Substring(0, line.Length - 1);
                    if (line.Length > limit) throw new HttpProtocolException(overflowStatus, "HTTP 行が上限を超えています。");
                    if (line.IndexOf('\r') >= 0) throw new HttpProtocolException(400, "HTTP 行に不正な CR があります。");
                    return line;
                }
                if (builder == null) { builder = new StringBuilder(); builder.Append(part); }
            }
        }

        /// <summary>要求本文を所定バイト数だけ消費する。余分に読んだ次の要求はバッファに残る。</summary>
        /// <param name="count">消費バイト数。</param>
        internal async Task SkipAsync(long count)
        {
            while (count > 0)
            {
                if (offset == available && await FillAsync().ConfigureAwait(false) == 0)
                    throw new HttpProtocolException(400, "要求本文が指定長に達する前に終了しました。");
                int take = (int)Math.Min(count, available - offset);
                offset += take;
                count -= take;
            }
        }

        /// <summary>chunk 本体直後の CRLF を消費する。</summary>
        internal async Task ReadChunkEndAsync()
        {
            for (int i = 0; i < 2; i++)
            {
                if (offset == available && await FillAsync().ConfigureAwait(false) == 0)
                    throw new HttpProtocolException(400, "chunk 終端がありません。");
                if (input[offset++] != (i == 0 ? 13 : 10)) throw new HttpProtocolException(400, "chunk 終端は CRLF である必要があります。");
            }
        }

        /// <summary>部分 send を処理し、指定バイト全体を非同期送信する。</summary>
        /// <param name="data">送信配列。</param><param name="start">先頭位置。</param><param name="count">送信バイト数。</param>
        internal async Task SendAsync(byte[] data, int start, int count)
        {
            while (count > 0)
            {
                int sent = await socket.SendAsync(new ArraySegment<byte>(data, start, Math.Min(count, HttpServer.TransferBufferSize)), SocketFlags.None).ConfigureAwait(false);
                if (sent <= 0) throw new SocketException((int)SocketError.ConnectionReset);
                Interlocked.Exchange(ref lastActivity, Stopwatch.GetTimestamp());
                start += sent;
                count -= sent;
            }
        }

        /// <summary>linger の強制 RST を使わず socket を解放する。複数回呼出し可能。</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref closed, 1) == 0) socket.Dispose();
        }
    }

    /// <summary>要求行・検証済みヘッダ・framing と応答状態を保持する。</summary>
    internal sealed class HttpRequest
    {
        internal string Method = "GET";
        internal string Target = "/";
        internal string Version = "HTTP/1.1";
        internal bool IsHttp09;
        internal bool KeepAlive;
        internal bool Chunked;
        internal long ContentLength;
        internal bool ResponseStarted;
        internal readonly Dictionary<string, string> Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>ヘッダ値を得る。</summary><param name="name">ヘッダ名。</param><returns>存在しなければ null。</returns>
        internal string GetHeader(string name) { string value; return Headers.TryGetValue(name, out value) ? value : null; }
    }

    /// <summary>接続を閉じる必要がある HTTP 構文エラーを status とともに伝える。</summary>
    internal sealed class HttpProtocolException : Exception
    {
        internal int StatusCode { get; private set; }
        /// <summary>プロトコル違反を作る。</summary><param name="statusCode">HTTP status。</param><param name="message">日本語説明。</param>
        internal HttpProtocolException(int statusCode, string message) : base(message) { StatusCode = statusCode; }
    }

    /// <summary>HTTP/0.9～1.1 の要求行・ヘッダ・本文 framing を検証する。</summary>
    internal static class HttpProtocol
    {
        internal const int MaxLineLength = 16384;
        internal const int MaxHeaderBytes = 65536;
        internal const int MaxHeaderCount = 100;
        internal const long MaxBodyBytes = 64L * 1024 * 1024;

        /// <summary>要求を一つ読む。先読み済みの次要求を消費せず、HTTP/0.9 は要求行だけで完了する。</summary>
        /// <param name="connection">入力接続。</param><param name="request">解析結果の格納先。エラー時も可能な範囲で設定される。</param>
        /// <returns>要求があれば true、接続の正常 EOF なら false。</returns>
        internal static async Task<bool> ReadRequestAsync(HttpConnection connection, HttpRequest request)
        {
            string line = await connection.ReadLineAsync(MaxLineLength, 414).ConfigureAwait(false);
            int empty = 0;
            while (line != null && line.Length == 0 && empty++ < 2)
                line = await connection.ReadLineAsync(MaxLineLength, 414).ConfigureAwait(false);
            if (line == null) return false;
            string[] words = line.Split(' ');
            if (words.Length != 2 && words.Length != 3) throw new HttpProtocolException(400, "要求行の形式が不正です。");
            request.Method = words[0];
            request.Target = words[1];
            if (!IsToken(request.Method) || request.Target.Length == 0) throw new HttpProtocolException(400, "メソッドまたは対象 URI が不正です。");
            foreach (char character in request.Target)
                if (character <= 32 || character == 127) throw new HttpProtocolException(400, "URI に制御文字または空白があります。");
            if (words.Length == 2 || words[2] == "HTTP/0.9")
            {
                if (request.Method != "GET") throw new HttpProtocolException(400, "HTTP/0.9 は GET のみ使用可能です。");
                request.IsHttp09 = true;
                request.Version = "HTTP/0.9";
                request.KeepAlive = false;
                return true;
            }
            if (words[2] != "HTTP/1.0" && words[2] != "HTTP/1.1")
            {
                if (words[2].Length != 8 || !words[2].StartsWith("HTTP/", StringComparison.Ordinal) || words[2][6] != '.' ||
                    words[2][5] < '0' || words[2][5] > '9' || words[2][7] < '0' || words[2][7] > '9')
                    throw new HttpProtocolException(400, "HTTP バージョンの形式が不正です。");
                throw new HttpProtocolException(505, "HTTP/0.9, HTTP/1.0, HTTP/1.1 に対応しています。");
            }
            request.Version = words[2];
            int bytes = 0;
            int count = 0;
            while (true)
            {
                line = await connection.ReadLineAsync(MaxLineLength, 431).ConfigureAwait(false);
                if (line == null) throw new HttpProtocolException(400, "HTTP ヘッダの終端がありません。");
                bytes += line.Length + 2;
                if (bytes > MaxHeaderBytes) throw new HttpProtocolException(431, "HTTP ヘッダ全体が 64 KiB を超えています。");
                if (line.Length == 0) break;
                if (++count > MaxHeaderCount) throw new HttpProtocolException(431, "HTTP ヘッダが 100 フィールドを超えています。");
                string name;
                string value;
                ParseHeaderLine(line, out name, out value);
                string previous;
                if (request.Headers.TryGetValue(name, out previous))
                {
                    if (name.Equals("Host", StringComparison.OrdinalIgnoreCase)) throw new HttpProtocolException(400, "Host ヘッダが重複しています。");
                    request.Headers[name] = previous + ", " + value;
                }
                else request.Headers.Add(name, value);
            }
            string host = request.GetHeader("Host");
            if ((request.Version == "HTTP/1.1" && host == null) || (host != null && !IsValidAuthority(host)))
                throw new HttpProtocolException(400, "HTTP/1.1 には正しい Host ヘッダが一つ必要です。");
            string connectionHeader = request.GetHeader("Connection");
            request.KeepAlive = !HasToken(connectionHeader, "close") &&
                (request.Version == "HTTP/1.1" || HasToken(connectionHeader, "keep-alive"));
            string transfer = request.GetHeader("Transfer-Encoding");
            string contentLength = request.GetHeader("Content-Length");
            if (transfer != null && contentLength != null) throw new HttpProtocolException(400, "Transfer-Encoding と Content-Length は同時に指定できません。");
            if (transfer != null)
            {
                if (request.Version != "HTTP/1.1") throw new HttpProtocolException(400, "HTTP/1.0 で Transfer-Encoding は使用できません。");
                string[] codings = transfer.Split(',');
                if (!codings[codings.Length - 1].Trim().Equals("chunked", StringComparison.OrdinalIgnoreCase))
                    throw new HttpProtocolException(400, "要求の Transfer-Encoding の最終 coding は chunked である必要があります。");
                if (codings.Length != 1)
                {
                    for (int i = 0; i < codings.Length - 1; i++)
                        if (codings[i].Trim().Equals("chunked", StringComparison.OrdinalIgnoreCase))
                            throw new HttpProtocolException(400, "chunked を重複して使用できません。");
                    throw new HttpProtocolException(501, "要求本文の Transfer-Encoding は chunked のみ対応します。");
                }
                request.Chunked = true;
            }
            if (contentLength != null)
            {
                long length = -1;
                foreach (string field in contentLength.Split(','))
                {
                    long parsed;
                    string value = field.Trim();
                    if (!TryDecimal(value, out parsed)) throw new HttpProtocolException(400, "Content-Length は非負の十進整数で指定してください。");
                    if (length >= 0 && length != parsed) throw new HttpProtocolException(400, "Content-Length の指定が一致していません。");
                    length = parsed;
                }
                request.ContentLength = length;
                if (length > MaxBodyBytes) throw new HttpProtocolException(413, "静的サーバーが消費する要求本文の上限は 64 MiB です。");
            }
            string expect = request.GetHeader("Expect");
            if (expect != null && !expect.Equals("100-continue", StringComparison.OrdinalIgnoreCase))
                throw new HttpProtocolException(417, "Expect は 100-continue のみ対応します。");
            return true;
        }

        /// <summary>フィールド名/値を分離して検証する。obs-fold や名前末尾空白は拒否する。</summary>
        /// <param name="line">改行なしのヘッダ行。</param><param name="name">出力名。</param><param name="value">出力値（OWS 除去済み）。</param>
        internal static void ParseHeaderLine(string line, out string name, out string value)
        {
            int colon = line.IndexOf(':');
            if (colon <= 0) throw new HttpProtocolException(400, "HTTP ヘッダの区切りが不正です。");
            name = line.Substring(0, colon);
            if (!IsToken(name)) throw new HttpProtocolException(400, "HTTP ヘッダ名が不正です。");
            value = line.Substring(colon + 1).Trim(' ', '\t');
            foreach (char character in value)
                if ((character < 32 && character != '\t') || character == 127)
                    throw new HttpProtocolException(400, "HTTP ヘッダに制御文字があります。");
        }

        /// <summary>RFC token を検証する。</summary><param name="value">検証対象。</param><returns>1 文字以上の有効 token なら true。</returns>
        internal static bool IsToken(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            foreach (char c in value) if (!IsTokenCharacter(c)) return false;
            return true;
        }

        /// <summary>tchar を判定する。</summary><param name="c">文字。</param><returns>token に使える ASCII なら true。</returns>
        private static bool IsTokenCharacter(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || "!#$%&'*+-.^_`|~".IndexOf(c) >= 0;
        }

        /// <summary>カンマ区切り token 内を大文字小文字無視で検索する。</summary>
        /// <param name="value">ヘッダ値または null。</param><param name="token">探す token。</param><returns>存在すれば true。</returns>
        internal static bool HasToken(string value, string token)
        {
            if (value == null) return false;
            foreach (string item in value.Split(',')) if (item.Trim().Equals(token, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>符号なし十進整数を Int64 に変換する。超過は飽和させ、入力処理で overflow を起こさない。</summary>
        /// <param name="value">数字列。</param><param name="number">値。範囲超過は long.MaxValue。</param><returns>数字だけなら true。</returns>
        internal static bool TryDecimal(string value, out long number)
        {
            number = 0;
            if (value.Length == 0) return false;
            foreach (char c in value)
            {
                if (c < '0' || c > '9') return false;
                int digit = c - '0';
                if (number > (long.MaxValue - digit) / 10) number = long.MaxValue;
                else number = number * 10 + digit;
            }
            return true;
        }

        /// <summary>Host または absolute-form の authority を検証する。DNS 解決や接続は行わない。</summary>
        /// <param name="value">ホスト名と任意のポート。</param><returns>有効な authority なら true。</returns>
        internal static bool IsValidAuthority(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            foreach (char c in value) if (c <= 32 || c >= 127 || "/\\?#@,".IndexOf(c) >= 0) return false;
            string host = value;
            string port = null;
            if (value[0] == '[')
            {
                int closing = value.IndexOf(']');
                if (closing < 0) return false;
                IPAddress address;
                if (!IPAddress.TryParse(value.Substring(1, closing - 1), out address) || address.AddressFamily != AddressFamily.InterNetworkV6) return false;
                if (closing + 1 < value.Length)
                {
                    if (value[closing + 1] != ':') return false;
                    port = value.Substring(closing + 2);
                }
            }
            else
            {
                int colon = value.IndexOf(':');
                if (colon >= 0) { host = value.Substring(0, colon); port = value.Substring(colon + 1); }
                if (Uri.CheckHostName(host) == UriHostNameType.Unknown) return false;
            }
            if (port != null)
            {
                long number;
                if (!TryDecimal(port, out number) || number > 65535) return false;
            }
            return true;
        }

        /// <summary>CL/chunked の要求本文を最後まで消費して、次の Keep-Alive 要求との境界を保つ。</summary>
        /// <param name="connection">接続。</param><param name="request">framing 検証済み要求。</param>
        internal static async Task DiscardBodyAsync(HttpConnection connection, HttpRequest request)
        {
            if (request.IsHttp09) return;
            if ((request.Chunked || request.ContentLength > 0) && request.Version == "HTTP/1.1" &&
                request.GetHeader("Expect") != null)
                await HttpResponse.SendHeadersAsync(connection, request, 100, null, null, null).ConfigureAwait(false);
            if (!request.Chunked)
            {
                await connection.SkipAsync(request.ContentLength).ConfigureAwait(false);
                return;
            }
            long total = 0;
            while (true)
            {
                string line = await connection.ReadLineAsync(4096, 400).ConfigureAwait(false);
                if (line == null) throw new HttpProtocolException(400, "chunk size がありません。");
                int semicolon = line.IndexOf(';');
                string sizeText = (semicolon < 0 ? line : line.Substring(0, semicolon)).Trim(' ', '\t');
                if (sizeText.Length == 0) throw new HttpProtocolException(400, "chunk size が空です。");
                long size = 0;
                foreach (char c in sizeText)
                {
                    int digit = HexValue(c);
                    if (digit < 0) throw new HttpProtocolException(400, "chunk size は十六進整数で指定してください。");
                    size = size > (long.MaxValue - digit) / 16 ? long.MaxValue : size * 16 + digit;
                }
                if (semicolon >= 0 && !ValidateChunkExtensions(line, semicolon))
                    throw new HttpProtocolException(400, "chunk extension の構文が不正です。");
                if (size > MaxBodyBytes - total) throw new HttpProtocolException(413, "要求本文の合計が 64 MiB を超えています。");
                if (size == 0) break;
                total += size;
                await connection.SkipAsync(size).ConfigureAwait(false);
                await connection.ReadChunkEndAsync().ConfigureAwait(false);
            }
            int trailerBytes = 0;
            int trailerCount = 0;
            while (true)
            {
                string line = await connection.ReadLineAsync(MaxLineLength, 431).ConfigureAwait(false);
                if (line == null) throw new HttpProtocolException(400, "chunk trailer の終端がありません。");
                trailerBytes += line.Length + 2;
                if (trailerBytes > MaxHeaderBytes) throw new HttpProtocolException(431, "trailer が 64 KiB を超えています。");
                if (line.Length == 0) break;
                if (++trailerCount > MaxHeaderCount) throw new HttpProtocolException(431, "trailer のフィールド数が多すぎます。");
                string name;
                string value;
                ParseHeaderLine(line, out name, out value);
                if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) || name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Host", StringComparison.OrdinalIgnoreCase) || name.Equals("Connection", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Trailer", StringComparison.OrdinalIgnoreCase) || name.Equals("Expect", StringComparison.OrdinalIgnoreCase) || name.Equals("Upgrade", StringComparison.OrdinalIgnoreCase))
                    throw new HttpProtocolException(400, "framing またはルーティングを変更する trailer は使用できません。");
                // trailer は検証して消費するだけで、すでに評価した要求ヘッダへ合成しない。
            }
        }

        /// <summary>chunk extension の token/quoted-string を解析する。</summary>
        /// <param name="line">chunk-size 行。</param><param name="index">最初のセミコロン位置。</param><returns>構文が正しければ true。</returns>
        private static bool ValidateChunkExtensions(string line, int index)
        {
            while (index < line.Length)
            {
                if (line[index++] != ';') return false;
                while (index < line.Length && (line[index] == ' ' || line[index] == '\t')) index++;
                int start = index;
                while (index < line.Length && IsTokenCharacter(line[index])) index++;
                if (start == index) return false;
                while (index < line.Length && (line[index] == ' ' || line[index] == '\t')) index++;
                if (index < line.Length && line[index] == '=')
                {
                    index++;
                    while (index < line.Length && (line[index] == ' ' || line[index] == '\t')) index++;
                    if (index < line.Length && line[index] == '"')
                    {
                        index++;
                        bool ended = false;
                        while (index < line.Length)
                        {
                            char c = line[index++];
                            if (c == '"') { ended = true; break; }
                            if (c == '\\') { if (index == line.Length) return false; c = line[index++]; }
                            if ((c < 32 && c != '\t') || c == 127) return false;
                        }
                        if (!ended) return false;
                    }
                    else
                    {
                        start = index;
                        while (index < line.Length && IsTokenCharacter(line[index])) index++;
                        if (start == index) return false;
                    }
                    while (index < line.Length && (line[index] == ' ' || line[index] == '\t')) index++;
                }
            }
            return true;
        }

        /// <summary>十六進一桁を変換する。</summary><param name="c">文字。</param><returns>0～15、無効なら -1。</returns>
        internal static int HexValue(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }
    }

    /// <summary>全最終応答と 100 応答の共通ヘッダ、および本文あり/なしの応答を生成する。</summary>
    internal static class HttpResponse
    {
        internal static readonly Encoding Utf8 = new UTF8Encoding(false);
        private const string RequiredHeaders =
            "Access-Control-Allow-Origin: *\r\n" +
            "Access-Control-Allow-Methods: GET, HEAD, OPTIONS\r\n" +
            "Access-Control-Allow-Headers: *\r\n" +
            "Access-Control-Max-Age: 86400\r\n" +
            "Cross-Origin-Resource-Policy: cross-origin\r\n" +
            "Cross-Origin-Opener-Policy: same-origin\r\n" +
            "Cross-Origin-Embedder-Policy: require-corp\r\n" +
            "Cache-Control: no-store\r\n";

        /// <summary>ヘッダを送る。HTTP/0.9 は本文のみの規格なので何も書かない。</summary>
        /// <param name="connection">出力接続。</param><param name="request">要求の version/connection/head 状態。</param>
        /// <param name="status">HTTP status。</param><param name="type">MIME、なければ null。</param>
        /// <param name="length">表現長。1xx/204/304 では指定しない。</param><param name="extra">付加ヘッダ、または null。</param>
        internal static async Task SendHeadersAsync(HttpConnection connection, HttpRequest request, int status, string type, long? length, IDictionary<string, string> extra)
        {
            if (status >= 200) request.ResponseStarted = true;
            if (request.IsHttp09) return;
            StringBuilder header = new StringBuilder(768);
            header.Append(request.Version == "HTTP/1.0" ? "HTTP/1.0" : "HTTP/1.1").Append(' ').Append(status).Append(' ').Append(Reason(status)).Append("\r\n");
            header.Append("Date: ").Append(DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture)).Append("\r\n");
            header.Append("Server: dn_simple_http_test_server1/R01\r\n");
            header.Append(RequiredHeaders);
            if (type != null) header.Append("Content-Type: ").Append(type).Append("\r\n");
            if (length.HasValue && status >= 200 && status != 204 && status != 304)
                header.Append("Content-Length: ").Append(length.Value.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            if (status >= 200)
            {
                header.Append("Connection: ").Append(request.KeepAlive ? "keep-alive" : "close").Append("\r\n");
                if (request.KeepAlive) header.Append("Keep-Alive: timeout=300\r\n");
            }
            if (extra != null)
                foreach (KeyValuePair<string, string> field in extra) header.Append(field.Key).Append(": ").Append(field.Value).Append("\r\n");
            header.Append("\r\n");
            byte[] bytes = Encoding.ASCII.GetBytes(header.ToString());
            await connection.SendAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
        }

        /// <summary>メモリ内の表現を返す。HEAD/204/304 には本文を送らない。</summary>
        /// <param name="connection">接続。</param><param name="request">要求。</param><param name="status">status。</param>
        /// <param name="type">MIME。</param><param name="body">GET 相当の本文。</param><param name="extra">付加ヘッダ。</param>
        internal static async Task SendBytesAsync(HttpConnection connection, HttpRequest request, int status, string type, byte[] body, IDictionary<string, string> extra)
        {
            await SendHeadersAsync(connection, request, status, type, body.LongLength, extra).ConfigureAwait(false);
            if (request.Method != "HEAD" && status != 204 && status != 304)
                await connection.SendAsync(body, 0, body.Length).ConfigureAwait(false);
        }

        /// <summary>HTML escape 済みの日本語エラーページとコンソールログを出す。</summary>
        /// <param name="connection">接続。</param><param name="request">要求。</param><param name="status">4xx/5xx。</param>
        /// <param name="detail">公開してよい説明。</param><param name="extra">Allow/Content-Range 等の付加ヘッダ。</param>
        internal static Task SendErrorAsync(HttpConnection connection, HttpRequest request, int status, string detail, IDictionary<string, string> extra)
        {
            ConsoleUi.LogHttpError(status, request.Method, request.Target, detail);
            string title = status.ToString(CultureInfo.InvariantCulture) + " " + Reason(status);
            string html = "<!doctype html><html lang=\"ja\"><meta charset=\"utf-8\"><title>" + title +
                "</title><h1>" + title + "</h1><p>" + WebUtility.HtmlEncode(detail) + "</p><p><code>" +
                WebUtility.HtmlEncode(request.Target) + "</code></p><hr><p>dn_simple_http_test_server1</p></html>\n";
            return SendBytesAsync(connection, request, status, "text/html; charset=utf-8", Utf8.GetBytes(html), extra);
        }

        /// <summary>使用する status の標準 reason phrase。</summary><param name="status">status code。</param><returns>ASCII reason。</returns>
        internal static string Reason(int status)
        {
            switch (status)
            {
                case 100: return "Continue";
                case 200: return "OK";
                case 204: return "No Content";
                case 206: return "Partial Content";
                case 301: return "Moved Permanently";
                case 304: return "Not Modified";
                case 400: return "Bad Request";
                case 403: return "Forbidden";
                case 404: return "Not Found";
                case 405: return "Method Not Allowed";
                case 412: return "Precondition Failed";
                case 413: return "Content Too Large";
                case 414: return "URI Too Long";
                case 416: return "Range Not Satisfiable";
                case 417: return "Expectation Failed";
                case 431: return "Request Header Fields Too Large";
                case 500: return "Internal Server Error";
                case 501: return "Not Implemented";
                case 505: return "HTTP Version Not Supported";
                default: return "Error";
            }
        }
    }

    /// <summary>字句的な公開ルート制限を通過した物理パスと、再利用可能な正規化済み公開 URL を保持する。</summary>
    internal sealed class MappedPath
    {
        /// <summary>公開ルート以下の絶対パス。シンボリックリンクの実体へは解決していない。</summary>
        internal string PhysicalPath;
        /// <summary>UTF-8 percent-encode 済みの、先頭 / から始まる公開パス。</summary>
        internal string UrlPath;
        /// <summary>先頭 ? を含む query。指定がなければ空文字列。Location に使用できる ASCII で保持する。</summary>
        internal string Query;
        /// <summary>正規化後の URL パスが / で終わる場合は true。ルート / も true。</summary>
        internal bool HasTrailingSlash;
    }

    /// <summary>HTTP request-target を公開ルート内の物理パスへ対応付け、字句的 traversal を防ぐ。</summary>
    internal static class WebPath
    {
        /// <summary>不正列・過長 UTF-8・サロゲート符号位置の UTF-8 表現を置換せず拒否する decoder。</summary>
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
        /// <summary>実行 OS がファイル名として許可しない文字。Windows での追加検査に用いる。</summary>
        private static readonly char[] InvalidFileNameCharacters = Path.GetInvalidFileNameChars();
        /// <summary>拡張子を付けても Windows のデバイスとして扱われ得る固定名。</summary>
        private static readonly HashSet<string> WindowsDeviceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$"
        };

        /// <summary>
        /// origin-form または http(s) absolute-form を解析し、UTF-8 を一度だけ復号して dot segment を正規化する。
        /// 公開ルートからの字句的逸脱は禁止するが、ルート内のリンクが外部を指すことは仕様どおり許可する。
        /// </summary>
        /// <param name="rootPath">正規化済みの公開ディレクトリ絶対パス。</param>
        /// <param name="rootPrefix">rootPath に OS のディレクトリ区切りが末尾に一つ付いた比較用接頭辞。</param>
        /// <param name="requestTarget">要求行から ISO-8859-1 で保持した未復号の request-target。</param>
        /// <returns>存在確認前の物理パス、公開 URL パス、query と末尾 / の状態。</returns>
        /// <exception cref="HttpProtocolException">URI の構文不正、無効な UTF-8、危険な Windows 名または公開ルートからの逸脱。</exception>
        internal static MappedPath Map(string rootPath, string rootPrefix, string requestTarget)
        {
            if (string.IsNullOrEmpty(requestTarget) || requestTarget == "*")
                throw new HttpProtocolException(400, "静的ファイルの対象 URI は / から始まるパスで指定してください。");
            if (requestTarget.IndexOf('#') >= 0)
                throw new HttpProtocolException(400, "HTTP request-target に fragment は指定できません。ファイル名の # は %23 にしてください。");

            string pathAndQuery = ExtractPathAndQuery(requestTarget);
            int queryPosition = pathAndQuery.IndexOf('?');
            string rawPath = queryPosition < 0 ? pathAndQuery : pathAndQuery.Substring(0, queryPosition);
            string query = queryPosition < 0 ? string.Empty : PreserveQuery(pathAndQuery.Substring(queryPosition));
            if (rawPath.Length == 0) rawPath = "/";
            string decodedPath = DecodePath(rawPath);
            if (decodedPath.Length == 0 || decodedPath[0] != '/')
                throw new HttpProtocolException(400, "URI のパスは / から始まる必要があります。");
            foreach (char character in decodedPath)
            {
                if (char.IsControl(character))
                    throw new HttpProtocolException(400, "URI のファイルパスに制御文字は使用できません。");
                if (character == '\\')
                    throw new HttpProtocolException(403, "URI のファイルパスにバックスラッシュは使用できません。");
            }

            List<string> segments = new List<string>();
            foreach (string segment in decodedPath.Split('/'))
            {
                if (segment.Length == 0 || segment == ".") continue;
                if (segment == "..")
                {
                    if (segments.Count == 0)
                        throw new HttpProtocolException(403, "公開ディレクトリより上のパスにはアクセスできません。");
                    segments.RemoveAt(segments.Count - 1);
                    continue;
                }
                if (PlatformSupport.IsWindows) ValidateWindowsSegment(segment);
                segments.Add(segment);
            }

            string physicalPath = rootPath;
            try
            {
                foreach (string segment in segments) physicalPath = Path.Combine(physicalPath, segment);
                physicalPath = Path.GetFullPath(physicalPath);
            }
            catch (PathTooLongException)
            {
                throw new HttpProtocolException(414, "ファイルシステムが扱えるパス長を超えています。");
            }
            catch (ArgumentException)
            {
                throw new HttpProtocolException(400, "ファイルパスの形式が不正です。");
            }
            catch (NotSupportedException)
            {
                throw new HttpProtocolException(400, "ファイルパスの形式に対応していません。");
            }
            catch (System.Security.SecurityException)
            {
                throw new HttpProtocolException(403, "ファイルパスへのアクセスが許可されていません。");
            }
            StringComparison comparison = PlatformSupport.IsWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!physicalPath.Equals(rootPath, comparison) && !physicalPath.StartsWith(rootPrefix, comparison))
                throw new HttpProtocolException(403, "公開ディレクトリの外部にはアクセスできません。");

            // '%' を含む名前もここで再符号化する。二重復号による %252e 等の別解釈を起こさない。
            string[] encodedSegments = new string[segments.Count];
            for (int index = 0; index < segments.Count; index++)
                encodedSegments[index] = Uri.EscapeDataString(segments[index]);
            bool trailingSlash = segments.Count == 0 || decodedPath.EndsWith("/", StringComparison.Ordinal)
                || decodedPath.EndsWith("/.", StringComparison.Ordinal) || decodedPath.EndsWith("/..", StringComparison.Ordinal);
            string urlPath = "/" + string.Join("/", encodedSegments);
            if (trailingSlash && segments.Count > 0) urlPath += "/";
            return new MappedPath
            {
                PhysicalPath = physicalPath,
                UrlPath = urlPath,
                Query = query,
                HasTrailingSlash = trailingSlash
            };
        }

        /// <summary>absolute-form の authority を検証し、未正規化の path と query を抽出する。</summary>
        /// <param name="target">未復号の request-target。</param>
        /// <returns>origin-form と同じ、先頭 / から始まる path および任意の query。</returns>
        private static string ExtractPathAndQuery(string target)
        {
            if (target[0] == '/') return target;
            int authorityStart;
            if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) authorityStart = 7;
            else if (target.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) authorityStart = 8;
            else throw new HttpProtocolException(400, "request-target は絶対パスまたは http(s) の絶対 URI で指定してください。");
            int authorityEnd = authorityStart;
            while (authorityEnd < target.Length && target[authorityEnd] != '/' && target[authorityEnd] != '?') authorityEnd++;
            string authority = target.Substring(authorityStart, authorityEnd - authorityStart);
            if (!HttpProtocol.IsValidAuthority(authority))
                throw new HttpProtocolException(400, "絶対 URI のホスト名またはポート番号が不正です。");
            if (authorityEnd == target.Length) return "/";
            return target[authorityEnd] == '?' ? "/" + target.Substring(authorityEnd) : target.Substring(authorityEnd);
        }

        /// <summary>percent-encoding と ISO-8859-1 で保持された元 byte を一つの byte 列に戻し、厳密に UTF-8 復号する。</summary>
        /// <param name="rawPath">要求行上の、query を除いた未復号パス。</param>
        /// <returns>一度だけ復号した Unicode パス。</returns>
        private static string DecodePath(string rawPath)
        {
            byte[] bytes = new byte[rawPath.Length];
            int count = 0;
            for (int index = 0; index < rawPath.Length; index++)
            {
                char character = rawPath[index];
                if (character == '%')
                {
                    if (index + 2 >= rawPath.Length)
                        throw new HttpProtocolException(400, "URI の percent-encoding が途中で終了しています。");
                    int high = HttpProtocol.HexValue(rawPath[index + 1]);
                    int low = HttpProtocol.HexValue(rawPath[index + 2]);
                    if (high < 0 || low < 0)
                        throw new HttpProtocolException(400, "URI の percent-encoding は 2 桁の十六進数で指定してください。");
                    bytes[count++] = (byte)(high * 16 + low);
                    index += 2;
                }
                else
                {
                    if (character > 255)
                        throw new HttpProtocolException(400, "URI は UTF-8 の byte 列または percent-encoding で指定してください。");
                    bytes[count++] = (byte)character;
                }
            }
            try { return StrictUtf8.GetString(bytes, 0, count); }
            catch (DecoderFallbackException)
            {
                throw new HttpProtocolException(400, "URI のファイルパスが正しい UTF-8 ではありません。");
            }
        }

        /// <summary>query の ASCII 表現を保持し、元 byte が非 ASCII の部分だけを percent-encode する。</summary>
        /// <param name="query">先頭 ? を含む、要求行上の未復号 query。</param>
        /// <returns>Location に安全に格納できる、先頭 ? を含んだ ASCII query。</returns>
        private static string PreserveQuery(string query)
        {
            StringBuilder result = new StringBuilder(query.Length);
            foreach (char character in query)
            {
                if (character <= 32 || character == 127 || character > 255)
                    throw new HttpProtocolException(400, "URI の query に制御文字、空白または不正な byte があります。");
                if (character < 128) result.Append(character);
                else result.Append('%').Append(((int)character).ToString("X2", CultureInfo.InvariantCulture));
            }
            return result.ToString();
        }

        /// <summary>Windows の ADS、末尾正規化、DOS device 名によるファイル以外へのアクセスを拒否する。</summary>
        /// <param name="segment">復号済みの一つのファイル名。空文字列、.、.. は呼び出し元で処理済み。</param>
        private static void ValidateWindowsSegment(string segment)
        {
            if (segment.IndexOf(':') >= 0 || segment.IndexOfAny(InvalidFileNameCharacters) >= 0)
                throw new HttpProtocolException(403, "Windows のファイル名に使用できない文字または ADS の指定があります。");
            if (segment.EndsWith(".", StringComparison.Ordinal) || segment.EndsWith(" ", StringComparison.Ordinal))
                throw new HttpProtocolException(403, "Windows では末尾がドットまたは空白の名前を公開しません。");
            int dot = segment.IndexOf('.');
            string baseName = (dot < 0 ? segment : segment.Substring(0, dot)).TrimEnd(' ', '.');
            bool device = WindowsDeviceNames.Contains(baseName);
            if (!device && baseName.Length == 4 &&
                (baseName.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || baseName.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)))
            {
                char number = baseName[3];
                device = (number >= '1' && number <= '9') || number == '\u00b9' || number == '\u00b2' || number == '\u00b3';
            }
            if (device) throw new HttpProtocolException(403, "Windows の予約済みデバイス名にはアクセスできません。");
        }
    }

    /// <summary>index ファイルがないディレクトリを、公開 URL だけを用いた Apache/nginx 風の一覧 HTML にする。</summary>
    internal static class DirectoryListing
    {
        /// <summary>列挙時点で取得できた一項目の表示情報。取得できないメタデータは null のまま保持する。</summary>
        private sealed class ListingEntry
        {
            /// <summary>OS から取得したファイルまたはディレクトリの名前。</summary>
            internal string Name;
            /// <summary>ディレクトリである場合は true。ディレクトリへのリンクも含む。</summary>
            internal bool IsDirectory;
            /// <summary>取得できた場合の最終更新日時（UTC）。</summary>
            internal DateTime? ModifiedUtc;
            /// <summary>取得できた場合のファイル長。ディレクトリの場合は null。</summary>
            internal long? Length;
        }

        /// <summary>ディレクトリ先・名前順に、ファイル名、UTC 更新日時、byte 数を表示する UTF-8 HTML を作る。</summary>
        /// <param name="physicalDirectory">一覧化する実在ディレクトリ。アクセス拒否等は呼び出し元で HTTP エラーに変換する。</param>
        /// <param name="publicUrlPath">対応する percent-encode 済みの公開 URL パス。物理パスは HTML に表示しない。</param>
        /// <returns>BOM のない UTF-8 で符号化した完全な HTML 文書。</returns>
        internal static byte[] Create(string physicalDirectory, string publicUrlPath)
        {
            string directoryUrl = publicUrlPath.EndsWith("/", StringComparison.Ordinal) ? publicUrlPath : publicUrlPath + "/";
            List<ListingEntry> entries = new List<ListingEntry>();
            // トップレベルの列挙が失敗した場合は伝播させる。一覧全体を空として偽装しない。
            foreach (string path in Directory.GetFileSystemEntries(physicalDirectory))
            {
                ListingEntry entry = new ListingEntry { Name = Path.GetFileName(path) };
                ReadMetadata(path, entry);
                entries.Add(entry);
            }
            entries.Sort(CompareEntries);
            string displayUrl = Uri.UnescapeDataString(directoryUrl);
            string title = "Index of " + displayUrl;
            StringBuilder html = new StringBuilder(2048 + Math.Min(entries.Count, 4096) * 160);
            html.Append("<!doctype html>\n<html lang=\"ja\"><head><meta charset=\"utf-8\">")
                .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>")
                .Append(WebUtility.HtmlEncode(title)).Append("</title>\n<style>")
                .Append("body{max-width:1100px;margin:28px auto;padding:0 20px;color:#222;background:#fff;font:15px system-ui,sans-serif}")
                .Append("h1{font-size:24px;font-weight:500;overflow-wrap:anywhere}table{width:100%;border-collapse:collapse}")
                .Append("th,td{padding:8px 12px;border-bottom:1px solid #eee;text-align:left}th{border-bottom:1px solid #999}")
                .Append("td.name{overflow-wrap:anywhere}td.date{white-space:nowrap;font-family:monospace}td.size,th.size{text-align:right;white-space:nowrap}")
                .Append("a{color:#0645ad;text-decoration:none}a:hover{text-decoration:underline}footer{margin-top:20px;color:#666;font-size:12px}")
                .Append("</style></head><body>\n<h1>").Append(WebUtility.HtmlEncode(title))
                .Append("</h1>\n<table><thead><tr><th>Name</th><th>Last modified (UTC)</th><th class=\"size\">Size (bytes)</th></tr></thead><tbody>\n");
            if (directoryUrl != "/")
            {
                string trimmed = directoryUrl.TrimEnd('/');
                int separator = trimmed.LastIndexOf('/');
                string parentUrl = separator <= 0 ? "/" : trimmed.Substring(0, separator + 1);
                AppendRow(html, parentUrl, "../", null, null);
            }
            foreach (ListingEntry entry in entries)
            {
                string suffix = entry.IsDirectory ? "/" : string.Empty;
                string href = directoryUrl + Uri.EscapeDataString(entry.Name) + suffix;
                AppendRow(html, href, entry.Name + suffix, entry.ModifiedUtc, entry.Length);
            }
            html.Append("</tbody></table>\n<footer>dn_simple_http_test_server1</footer>\n</body></html>\n");
            return HttpResponse.Utf8.GetBytes(html.ToString());
        }

        /// <summary>メタデータだけの取得失敗を許容し、一覧から消えた項目も名前付きの行として残す。</summary>
        /// <param name="path">列挙済みの一項目の物理パス。</param><param name="entry">表示用情報の格納先。</param>
        private static void ReadMetadata(string path, ListingEntry entry)
        {
            try
            {
                FileAttributes attributes = File.GetAttributes(path);
                entry.IsDirectory = (attributes & FileAttributes.Directory) != 0;
                if (entry.IsDirectory)
                {
                    DirectoryInfo info = new DirectoryInfo(path);
                    if (info.Exists) entry.ModifiedUtc = info.LastWriteTimeUtc;
                }
                else
                {
                    FileInfo info = new FileInfo(path);
                    if (info.Exists)
                    {
                        entry.ModifiedUtc = info.LastWriteTimeUtc;
                        entry.Length = info.Length;
                    }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (System.Security.SecurityException) { }
        }

        /// <summary>ディレクトリを先にし、同種の項目は大文字小文字無視の名前順、同順位は序数順にする。</summary>
        /// <param name="first">比較する一方の項目。</param><param name="second">比較する他方の項目。</param>
        /// <returns>並び順を表す負数、0 または正数。</returns>
        private static int CompareEntries(ListingEntry first, ListingEntry second)
        {
            if (first.IsDirectory != second.IsDirectory) return first.IsDirectory ? -1 : 1;
            int order = StringComparer.OrdinalIgnoreCase.Compare(first.Name, second.Name);
            return order != 0 ? order : StringComparer.Ordinal.Compare(first.Name, second.Name);
        }

        /// <summary>表示名と href 属性をそれぞれ HTML escape して一覧の一行を追記する。</summary>
        /// <param name="html">出力先。</param><param name="href">URL encode 済みのリンク先。</param><param name="name">表示する名前。</param>
        /// <param name="modifiedUtc">取得できた更新日時。null は -。</param><param name="length">ファイル長。null は -。</param>
        private static void AppendRow(StringBuilder html, string href, string name, DateTime? modifiedUtc, long? length)
        {
            html.Append("<tr><td class=\"name\"><a href=\"").Append(WebUtility.HtmlEncode(href)).Append("\">")
                .Append(WebUtility.HtmlEncode(name)).Append("</a></td><td class=\"date\">")
                .Append(modifiedUtc.HasValue ? modifiedUtc.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : "-")
                .Append("</td><td class=\"size\">")
                .Append(length.HasValue ? length.Value.ToString("N0", CultureInfo.InvariantCulture) : "-")
                .Append("</td></tr>\n");
        }
    }

    /// <summary>ファイル内の両端を含む一つのバイト範囲。</summary>
    internal sealed class ByteRange
    {
        internal long Start;
        internal long End;
        /// <summary>バイト数。範囲は必ず非負で順序検証済み。</summary>
        internal long Length { get { return End - Start + 1; } }
        /// <summary>範囲を作る。</summary><param name="start">先頭。</param><param name="end">末尾（含む）。</param>
        internal ByteRange(long start, long end) { Start = start; End = end; }
        /// <summary>Content-Range 値を作る。</summary><param name="total">全ファイル長。</param><returns>bytes start-end/total。</returns>
        internal string ContentRange(long total)
        {
            return "bytes " + Start.ToString(CultureInfo.InvariantCulture) + "-" + End.ToString(CultureInfo.InvariantCulture) + "/" + total.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>byte range の解析、suffix/open-ended、桁あふれの抑止と重複範囲の統合。</summary>
    internal static class RangeParser
    {
        /// <summary>Range を解析する。無効構文/未知単位/過大な範囲個数は規格の許す範囲で無視する。</summary>
        /// <param name="header">Range ヘッダまたは null。</param><param name="length">表現全体の長さ。</param>
        /// <returns>null は通常200、空リストは416、それ以外は206。最大16指定を扱う。</returns>
        internal static List<ByteRange> Parse(string header, long length)
        {
            if (header == null) return null;
            int equals = header.IndexOf('=');
            if (equals < 0 || !header.Substring(0, equals).Trim().Equals("bytes", StringComparison.OrdinalIgnoreCase)) return null;
            string[] parts = header.Substring(equals + 1).Split(',');
            if (parts.Length > 16) return null;
            List<ByteRange> ranges = new List<ByteRange>();
            foreach (string raw in parts)
            {
                string value = raw.Trim();
                int dash = value.IndexOf('-');
                if (dash < 0 || value.IndexOf('-', dash + 1) >= 0) return null;
                string startText = value.Substring(0, dash).Trim();
                string endText = value.Substring(dash + 1).Trim();
                long first;
                long last;
                if (startText.Length == 0)
                {
                    long suffix;
                    if (!HttpProtocol.TryDecimal(endText, out suffix)) return null;
                    if (suffix == 0 || length == 0) continue;
                    first = suffix >= length ? 0 : length - suffix;
                    last = length - 1;
                }
                else
                {
                    if (!HttpProtocol.TryDecimal(startText, out first)) return null;
                    if (endText.Length == 0) last = long.MaxValue;
                    else if (!HttpProtocol.TryDecimal(endText, out last) || last < first) return null;
                    if (first >= length) continue;
                    last = Math.Min(last, length - 1);
                }
                ranges.Add(new ByteRange(first, last));
            }
            ranges.Sort(delegate(ByteRange first, ByteRange second)
            {
                int compare = first.Start.CompareTo(second.Start);
                return compare == 0 ? first.End.CompareTo(second.End) : compare;
            });
            List<ByteRange> merged = new List<ByteRange>();
            foreach (ByteRange range in ranges)
            {
                if (merged.Count == 0) { merged.Add(range); continue; }
                ByteRange previous = merged[merged.Count - 1];
                if (range.Start <= previous.End || (previous.End < long.MaxValue && range.Start == previous.End + 1))
                    previous.End = Math.Max(previous.End, range.End);
                else merged.Add(range);
            }
            return merged;
        }
    }

    /// <summary>GET/HEAD の条件付き要求を RFC の優先順で評価する。metadata ETag は弱いものとして扱う。</summary>
    internal static class HttpValidators
    {
        /// <summary>If-Match → If-Unmodified-Since → If-None-Match → If-Modified-Since の順に評価する。</summary>
        /// <param name="request">要求。</param><param name="entityTag">現在の弱 ETag。</param><param name="modifiedUtc">UTC 更新時刻。日時 validator を持たない一覧は null。</param>
        /// <returns>200、304 または 412。</returns>
        internal static int Evaluate(HttpRequest request, string entityTag, DateTime? modifiedUtc)
        {
            DateTime modifiedSeconds = modifiedUtc.HasValue
                ? new DateTime(modifiedUtc.Value.Ticks - modifiedUtc.Value.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc) : DateTime.MinValue;
            DateTime date;
            string match = request.GetHeader("If-Match");
            if (match != null)
            {
                // 存在する表現には * は一致する。一方、弱 ETag を強比較で一致させてはいけない。
                if (match.Trim() != "*") return 412;
            }
            else if (modifiedUtc.HasValue && TryDate(request.GetHeader("If-Unmodified-Since"), out date) && modifiedSeconds > date) return 412;
            string noneMatch = request.GetHeader("If-None-Match");
            if (noneMatch != null)
            {
                if (noneMatch.Trim() == "*" || WeakTagListMatches(noneMatch, entityTag)) return 304;
            }
            else if (modifiedUtc.HasValue && TryDate(request.GetHeader("If-Modified-Since"), out date) && modifiedSeconds <= date) return 304;
            return 200;
        }

        /// <summary>現在の ETag を opaque 部分で弱比較する。引用符内のカンマをリスト区切りと取り違えない。</summary>
        /// <param name="header">ETag のリスト。</param><param name="entityTag">サーバー発行 ETag。</param><returns>いずれかが一致すれば true。</returns>
        private static bool WeakTagListMatches(string header, string entityTag)
        {
            string current = entityTag.StartsWith("W/", StringComparison.Ordinal) ? entityTag.Substring(2) : entityTag;
            int index = 0;
            bool matched = false;
            while (index < header.Length)
            {
                while (index < header.Length && (header[index] == ' ' || header[index] == '\t' || header[index] == ',')) index++;
                if (index == header.Length) break;
                if (index + 1 < header.Length && header[index] == 'W' && header[index + 1] == '/') index += 2;
                if (index >= header.Length || header[index] != '"') return false;
                int start = index++;
                while (index < header.Length && header[index] != '"')
                {
                    char c = header[index++];
                    if (c < 33 || c == 127) return false;
                }
                if (index == header.Length) return false;
                index++;
                if (string.Equals(header.Substring(start, index - start), current, StringComparison.Ordinal)) matched = true;
                while (index < header.Length && (header[index] == ' ' || header[index] == '\t')) index++;
                if (index < header.Length && header[index] != ',') return false;
            }
            return matched;
        }

        /// <summary>HTTP 日付を UTC へ変換する。無効な条件日付は無視する。</summary>
        /// <param name="value">日付または null。</param><param name="utc">出力日時。</param><returns>解析成功なら true。</returns>
        private static bool TryDate(string value, out DateTime utc)
        {
            DateTimeOffset date;
            if (value != null && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date))
            {
                utc = date.UtcDateTime;
                return true;
            }
            utc = default(DateTime);
            return false;
        }
    }

    /// <summary>添付仕様の1,890定義行を順に登録する、大小文字を区別しない1,296種のMIME辞書。</summary>
    internal static class MimeTypes
    {
        private static readonly Dictionary<string, string> Types = CreateTypes();

        /// <summary>最長の既知拡張子を優先する。.dll.config は .config より先に照合する。</summary>
        /// <param name="filePath">ファイルパス。</param><returns>指定MIME、未登録なら application/octet-stream。</returns>
        internal static string Get(string filePath)
        {
            string name = Path.GetFileName(filePath);
            for (int index = name.IndexOf('.'); index >= 0; index = name.IndexOf('.', index + 1))
            {
                string value;
                if (Types.TryGetValue(name.Substring(index), out value)) return value;
            }
            return "application/octet-stream";
        }

        /// <summary>表を記載順に読み、後の定義で置換する。末尾の #overwrite 指示もこの順序で実現する。</summary>
        /// <returns>標準ライブラリだけで作った読み取り専用運用の辞書。</returns>
        private static Dictionary<string, string> CreateTypes()
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (StringReader reader = new StringReader(Table))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] words = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length >= 2 && words[0].StartsWith(".", StringComparison.Ordinal)) result[words[0]] = words[1];
                }
            }
            return result;
        }

        // 出典: ユーザー添付 [T261018_EL_VUFEJ_33]。拡張子/値/順序を変更せずハードコードする。
        // 添付表で示された元出典: https://gist.github.com/PawelGerr/cb5886ee26c41ae2386b
        private const string Table = @"
.123 application/vnd.lotus-1-2-3
.323 text/h323
.3dml text/vnd.in3d.3dml
.3ds image/x-3ds
.3g2 video/3gpp2
.3gp video/3gpp
.3gp2 video/3gpp2
.3gpp video/3gpp
.7z application/x-7z-compressed
.aa audio/audible
.aab application/x-authorware-bin
.aac audio/x-aac
.aaf application/octet-stream
.aam application/x-authorware-map
.aas application/x-authorware-seg
.aax audio/vnd.audible.aax
.abw application/x-abiword
.ac application/pkix-attr-cert
.ac3 audio/ac3
.aca application/octet-stream
.acc application/vnd.americandynamics.acc
.accda application/msaccess.addin
.accdb application/msaccess
.accdc application/msaccess.cab
.accde application/msaccess
.accdr application/msaccess.runtime
.accdt application/msaccess
.accdw application/msaccess.webapplication
.accft application/msaccess.ftemplate
.ace application/x-ace-compressed
.acu application/vnd.acucobol
.acutc application/vnd.acucorp
.acx application/internet-property-stream
.AddIn text/xml
.ade application/msaccess
.adobebridge application/x-bridge-url
.adp audio/adpcm
.ADT audio/vnd.dlna.adts
.ADTS audio/aac
.aep application/vnd.audiograph
.afm application/x-font-type1
.afp application/vnd.ibm.modcap
.ahead application/vnd.ahead.space
.ai application/postscript
.aif audio/x-aiff
.aifc audio/x-aiff
.aiff audio/x-aiff
.air application/vnd.adobe.air-application-installer-package+zip
.ait application/vnd.dvb.ait
.amc application/x-mpeg
.ami application/vnd.amiga.ami
.apk application/vnd.android.package-archive
.appcache text/cache-manifest
.application application/x-ms-application
.apr application/vnd.lotus-approach
.arc application/x-freearc
.art image/x-jg
.asa application/xml
.asax application/xml
.asc application/pgp-signature
.ascx application/xml
.asd application/octet-stream
.asf video/x-ms-asf
.ashx application/xml
.asi application/octet-stream
.asm text/x-asm
.asmx application/xml
.aso application/vnd.accpac.simply.aso
.aspx application/xml
.asr video/x-ms-asf
.asx video/x-ms-asf
.atc application/vnd.acucorp
.atom application/atom+xml
.atomcat application/atomcat+xml
.atomsvc application/atomsvc+xml
.atx application/vnd.antix.game-component
.au audio/basic
.avi video/x-msvideo
.aw application/applixware
.axs application/olescript
.azf application/vnd.airzip.filesecure.azf
.azs application/vnd.airzip.filesecure.azs
.azw application/vnd.amazon.ebook
.bas text/plain
.bat application/x-msdownload
.bcpio application/x-bcpio
.bdf application/x-font-bdf
.bdm application/vnd.syncml.dm+wbxml
.bed application/vnd.realvnc.bed
.bh2 application/vnd.fujitsu.oasysprs
.bin application/octet-stream
.blb application/x-blorb
.blorb application/x-blorb
.bmi application/vnd.bmi
.bmp image/bmp
.book application/vnd.framemaker
.box application/vnd.previewsystems.box
.boz application/x-bzip2
.bpk application/octet-stream
.btif image/prs.btif
.bz application/x-bzip
.bz2 application/x-bzip2
.c text/x-c
.c11amc application/vnd.cluetrust.cartomobile-config
.c11amz application/vnd.cluetrust.cartomobile-config-pkg
.c4d application/vnd.clonk.c4group
.c4f application/vnd.clonk.c4group
.c4g application/vnd.clonk.c4group
.c4p application/vnd.clonk.c4group
.c4u application/vnd.clonk.c4group
.cab application/vnd.ms-cab-compressed
.caf audio/x-caf
.calx application/vnd.ms-office.calx
.cap application/vnd.tcpdump.pcap
.car application/vnd.curl.car
.cat application/vnd.ms-pki.seccat
.cb7 application/x-cbr
.cba application/x-cbr
.cbr application/x-cbr
.cbt application/x-cbr
.cbz application/x-cbr
.cc text/x-c
.cct application/x-director
.ccxml application/ccxml+xml
.cd text/plain
.cdbcmsg application/vnd.contact.cmsg
.cdda audio/aiff
.cdf application/x-netcdf
.cdkey application/vnd.mediastation.cdkey
.cdmia application/cdmi-capability
.cdmic application/cdmi-container
.cdmid application/cdmi-domain
.cdmio application/cdmi-object
.cdmiq application/cdmi-queue
.cdx chemical/x-cdx
.cdxml application/vnd.chemdraw+xml
.cdy application/vnd.cinderella
.cer application/pkix-cert
.cfs application/x-cfs-compressed
.cgm image/cgm
.chat application/x-chat
.chm application/vnd.ms-htmlhelp
.chrt application/vnd.kde.kchart
.cif chemical/x-cif
.cii application/vnd.anser-web-certificate-issue-initiation
.cil application/vnd.ms-artgalry
.cla application/vnd.claymore
.class application/java-vm
.clkk application/vnd.crick.clicker.keyboard
.clkp application/vnd.crick.clicker.palette
.clkt application/vnd.crick.clicker.template
.clkw application/vnd.crick.clicker.wordbank
.clkx application/vnd.crick.clicker
.clp application/x-msclip
.cmc application/vnd.cosmocaller
.cmdf chemical/x-cmdf
.cml chemical/x-cml
.cmp application/vnd.yellowriver-custom-menu
.cmx image/x-cmx
.cnf text/plain
.cod application/vnd.rim.cod
.com application/x-msdownload
.conf text/plain
.config application/xml
.contact text/x-ms-contact
.coverage application/xml
.cpio application/x-cpio
.cpp text/x-c
.cpt application/mac-compactpro
.crd application/x-mscardfile
.crl application/pkix-crl
.crt application/x-x509-ca-cert
.cryptonote application/vnd.rig.cryptonote
.cs text/plain
.csdproj text/plain
.csh application/x-csh
.csml chemical/x-csml
.csp application/vnd.commonspace
.csproj text/plain
.css text/css
.cst application/x-director
.csv text/csv
.cu application/cu-seeme
.cur application/octet-stream
.curl text/vnd.curl
.cww application/prs.cww
.cxt application/x-director
.cxx text/x-c
.dae model/vnd.collada+xml
.daf application/vnd.mobius.daf
.dart application/vnd.dart
.dat application/octet-stream
.dataless application/vnd.fdsn.seed
.datasource application/xml
.davmount application/davmount+xml
.dbk application/docbook+xml
.dbproj text/plain
.dcr application/x-director
.dcurl text/vnd.curl.dcurl
.dd2 application/vnd.oma.dd2+xml
.ddd application/vnd.fujixerox.ddd
.deb application/x-debian-package
.def text/plain
.deploy application/octet-stream
.der application/x-x509-ca-cert
.dfac application/vnd.dreamfactory
.dgc application/x-dgc-compressed
.dgml application/xml
.dib image/bmp
.dic text/x-c
.dif video/x-dv
.dir application/x-director
.dis application/vnd.mobius.dis
.disco text/xml
.dist application/octet-stream
.distz application/octet-stream
.djv image/vnd.djvu
.djvu image/vnd.djvu
.dll application/x-msdownload
.dll.config text/xml
.dlm text/dlm
.dmg application/x-apple-diskimage
.dmp application/vnd.tcpdump.pcap
.dms application/octet-stream
.dna application/vnd.dna
.doc application/msword
.docm application/vnd.ms-word.document.macroenabled.12
.docx application/vnd.openxmlformats-officedocument.wordprocessingml.document
.dot application/msword
.dotm application/vnd.ms-word.template.macroenabled.12
.dotx application/vnd.openxmlformats-officedocument.wordprocessingml.template
.dp application/vnd.osgi.dp
.dpg application/vnd.dpgraph
.dra audio/vnd.dra
.dsc text/prs.lines.tag
.dsp application/octet-stream
.dssc application/dssc+der
.dsw text/plain
.dtb application/x-dtbook+xml
.dtd application/xml-dtd
.dts audio/vnd.dts
.dtsConfig text/xml
.dtshd audio/vnd.dts.hd
.dump application/octet-stream
.dv video/x-dv
.dvb video/vnd.dvb.file
.dvi application/x-dvi
.dwf model/vnd.dwf
.dwg image/vnd.dwg
.dwp application/octet-stream
.dxf image/vnd.dxf
.dxp application/vnd.spotfire.dxp
.dxr application/x-director
.ecelp4800 audio/vnd.nuera.ecelp4800
.ecelp7470 audio/vnd.nuera.ecelp7470
.ecelp9600 audio/vnd.nuera.ecelp9600
.ecma application/ecmascript
.edm application/vnd.novadigm.edm
.edx application/vnd.novadigm.edx
.efif application/vnd.picsel
.ei6 application/vnd.pg.osasli
.elc application/octet-stream
.emf application/x-msmetafile
.eml message/rfc822
.emma application/emma+xml
.emz application/x-msmetafile
.eol audio/vnd.digital-winds
.eot application/vnd.ms-fontobject
.eps application/postscript
.epub application/epub+zip
.es3 application/vnd.eszigno3+xml
.esa application/vnd.osgi.subsystem
.esf application/vnd.epson.esf
.et3 application/vnd.eszigno3+xml
.etl application/etl
.etx text/x-setext
.eva application/x-eva
.evy application/x-envoy
.exe application/x-msdownload
.exe.config text/xml
.exi application/exi
.ext application/vnd.novadigm.ext
.ez application/andrew-inset
.ez2 application/vnd.ezpix-album
.ez3 application/vnd.ezpix-package
.f text/x-fortran
.f4v video/x-f4v
.f77 text/x-fortran
.f90 text/x-fortran
.fbs image/vnd.fastbidsheet
.fcdt application/vnd.adobe.formscentral.fcdt
.fcs application/vnd.isac.fcs
.fdf application/vnd.fdf
.fe_launch application/vnd.denovo.fcselayout-link
.fg5 application/vnd.fujitsu.oasysgp
.fgd application/x-director
.fh image/x-freehand
.fh4 image/x-freehand
.fh5 image/x-freehand
.fh7 image/x-freehand
.fhc image/x-freehand
.fif application/fractals
.fig application/x-xfig
.filters Application/xml
.fla application/octet-stream
.flac audio/x-flac
.fli video/x-fli
.flo application/vnd.micrografx.flo
.flr x-world/x-vrml
.flv video/x-flv
.flw application/vnd.kde.kivio
.flx text/vnd.fmi.flexstor
.fly text/vnd.fly
.fm application/vnd.framemaker
.fnc application/vnd.frogans.fnc
.for text/x-fortran
.fpx image/vnd.fpx
.frame application/vnd.framemaker
.fsc application/vnd.fsc.weblaunch
.fsscript application/fsharp-script
.fst image/vnd.fst
.fsx application/fsharp-script
.ftc application/vnd.fluxtime.clip
.fti application/vnd.anser-web-funds-transfer-initiation
.fvt video/vnd.fvt
.fxp application/vnd.adobe.fxp
.fxpl application/vnd.adobe.fxp
.fzs application/vnd.fuzzysheet
.g2w application/vnd.geoplan
.g3 image/g3fax
.g3w application/vnd.geospace
.gac application/vnd.groove-account
.gam application/x-tads
.gbr application/rpki-ghostbusters
.gca application/x-gca-compressed
.gdl model/vnd.gdl
.generictest application/xml
.geo application/vnd.dynageo
.gex application/vnd.geometry-explorer
.ggb application/vnd.geogebra.file
.ggt application/vnd.geogebra.tool
.ghf application/vnd.groove-help
.gif image/gif
.gim application/vnd.groove-identity-message
.gml application/gml+xml
.gmx application/vnd.gmx
.gnumeric application/x-gnumeric
.gph application/vnd.flographit
.gpx application/gpx+xml
.gqf application/vnd.grafeq
.gqs application/vnd.grafeq
.gram application/srgs
.gramps application/x-gramps-xml
.gre application/vnd.geometry-explorer
.group text/x-ms-group
.grv application/vnd.groove-injector
.grxml application/srgs+xml
.gsf application/x-font-ghostscript
.gsm audio/x-gsm
.gtar application/x-gtar
.gtm application/vnd.groove-tool-message
.gtw model/vnd.gtw
.gv text/vnd.graphviz
.gxf application/gxf
.gxt application/vnd.geonext
.gz application/x-gzip
.h text/x-c
.h261 video/h261
.h263 video/h263
.h264 video/h264
.hal application/vnd.hal+xml
.hbci application/vnd.hbci
.hdf application/x-hdf
.hdml text/x-hdml
.hh text/x-c
.hhc application/x-oleobject
.hhk application/octet-stream
.hhp application/octet-stream
.hlp application/winhlp
.hpgl application/vnd.hp-hpgl
.hpid application/vnd.hp-hpid
.hpp text/plain
.hps application/vnd.hp-hps
.hqx application/mac-binhex40
.hta application/hta
.htc text/x-component
.htke application/vnd.kenameaapp
.htm text/html
.html text/html
.htt text/webviewhtml
.hvd application/vnd.yamaha.hv-dic
.hvp application/vnd.yamaha.hv-voice
.hvs application/vnd.yamaha.hv-script
.hxa application/xml
.hxc application/xml
.hxd application/octet-stream
.hxe application/xml
.hxf application/xml
.hxh application/octet-stream
.hxi application/octet-stream
.hxk application/xml
.hxq application/octet-stream
.hxr application/octet-stream
.hxs application/octet-stream
.hxt text/html
.hxv application/xml
.hxw application/octet-stream
.hxx text/plain
.i text/plain
.i2g application/vnd.intergeo
.icc application/vnd.iccprofile
.ice x-conference/x-cooltalk
.icm application/vnd.iccprofile
.ico image/x-icon
.ics text/calendar
.idl text/plain
.ief image/ief
.ifb text/calendar
.ifm application/vnd.shana.informed.formdata
.iges model/iges
.igl application/vnd.igloader
.igm application/vnd.insors.igm
.igs model/iges
.igx application/vnd.micrografx.igx
.iif application/vnd.shana.informed.interchange
.iii application/x-iphone
.imp application/vnd.accpac.simply.imp
.ims application/vnd.ms-ims
.in text/plain
.inc text/plain
.inf application/octet-stream
.ink application/inkml+xml
.inkml application/inkml+xml
.inl text/plain
.ins application/x-internet-signup
.install application/x-install-instructions
.iota application/vnd.astraea-software.iota
.ipa application/x-itunes-ipa
.ipfix application/ipfix
.ipg application/x-itunes-ipg
.ipk application/vnd.shana.informed.package
.ipproj text/plain
.ipsw application/x-itunes-ipsw
.iqy text/x-ms-iqy
.irm application/vnd.ibm.rights-management
.irp application/vnd.irepository.package+xml
.iso application/x-iso9660-image
.isp application/x-internet-signup
.ite application/x-itunes-ite
.itlp application/x-itunes-itlp
.itms application/x-itunes-itms
.itp application/vnd.shana.informed.formtemplate
.itpc application/x-itunes-itpc
.IVF video/x-ivf
.ivp application/vnd.immervision-ivp
.ivu application/vnd.immervision-ivu
.jad text/vnd.sun.j2me.app-descriptor
.jam application/vnd.jam
.jar application/java-archive
.java text/x-java-source
.jck application/liquidmotion
.jcz application/liquidmotion
.jfif image/pjpeg
.jisp application/vnd.jisp
.jlt application/vnd.hp-jlyt
.jnlp application/x-java-jnlp-file
.joda application/vnd.joost.joda-archive
.jpb application/octet-stream
.jpe image/jpeg
.jpeg image/jpeg
.jpg image/jpeg
.jpgm video/jpm
.jpgv video/jpeg
.jpm video/jpm
.js application/javascript
.json application/json
.jsonml application/jsonml+json
.jsx text/jscript
.jsxbin text/plain
.kar audio/midi
.karbon application/vnd.kde.karbon
.kfo application/vnd.kde.kformula
.kia application/vnd.kidspiration
.kml application/vnd.google-earth.kml+xml
.kmz application/vnd.google-earth.kmz
.kne application/vnd.kinar
.knp application/vnd.kinar
.kon application/vnd.kde.kontour
.kpr application/vnd.kde.kpresenter
.kpt application/vnd.kde.kpresenter
.kpxx application/vnd.ds-keypoint
.ksp application/vnd.kde.kspread
.ktr application/vnd.kahootz
.ktx image/ktx
.ktz application/vnd.kahootz
.kwd application/vnd.kde.kword
.kwt application/vnd.kde.kword
.lasxml application/vnd.las.las+xml
.latex application/x-latex
.lbd application/vnd.llamagraphics.life-balance.desktop
.lbe application/vnd.llamagraphics.life-balance.exchange+xml
.les application/vnd.hhe.lesson-player
.lha application/x-lzh-compressed
.library-ms application/windows-library+xml
.link66 application/vnd.route66.link66+xml
.list text/plain
.list3820 application/vnd.ibm.modcap
.listafp application/vnd.ibm.modcap
.lit application/x-ms-reader
.lnk application/x-ms-shortcut
.loadtest application/xml
.log text/plain
.lostxml application/lost+xml
.lpk application/octet-stream
.lrf application/octet-stream
.lrm application/vnd.ms-lrm
.lsf video/x-la-asf
.lst text/plain
.lsx video/x-la-asf
.ltf application/vnd.frogans.ltf
.lvp audio/vnd.lucent.voice
.lwp application/vnd.lotus-wordpro
.lzh application/x-lzh-compressed
.m13 application/x-msmediaview
.m14 application/x-msmediaview
.m1v video/mpeg
.m21 application/mp21
.m2a audio/mpeg
.m2t video/vnd.dlna.mpeg-tts
.m2ts video/vnd.dlna.mpeg-tts
.m2v video/mpeg
.m3a audio/mpeg
.m3u audio/x-mpegurl
.m3u8 application/vnd.apple.mpegurl
.m4a audio/m4a
.m4b audio/m4b
.m4p audio/m4p
.m4r audio/x-m4r
.m4u video/vnd.mpegurl
.m4v video/x-m4v
.ma application/mathematica
.mac image/x-macpaint
.mads application/mads+xml
.mag application/vnd.ecowin.chart
.mak text/plain
.maker application/vnd.framemaker
.man text/troff
.manifest application/x-ms-manifest
.map text/plain
.mar application/octet-stream
.master application/xml
.mathml application/mathml+xml
.mb application/mathematica
.mbk application/vnd.mobius.mbk
.mbox application/mbox
.mc1 application/vnd.medcalcdata
.mcd application/vnd.mcd
.mcurl text/vnd.curl.mcurl
.mda application/msaccess
.mdb application/x-msaccess
.mde application/msaccess
.mdi image/vnd.ms-modi
.mdp application/octet-stream
.me text/troff
.mesh model/mesh
.meta4 application/metalink4+xml
.metalink application/metalink+xml
.mets application/mets+xml
.mfm application/vnd.mfmp
.mfp application/x-shockwave-flash
.mft application/rpki-manifest
.mgp application/vnd.osgeo.mapguide.package
.mgz application/vnd.proteus.magazine
.mht message/rfc822
.mhtml message/rfc822
.mid audio/midi
.midi audio/midi
.mie application/x-mie
.mif application/vnd.mif
.mime message/rfc822
.mix application/octet-stream
.mj2 video/mj2
.mjp2 video/mj2
.mk text/plain
.mk3d video/x-matroska
.mka audio/x-matroska
.mks video/x-matroska
.mkv video/x-matroska
.mlp application/vnd.dolby.mlp
.mmd application/vnd.chipnuts.karaoke-mmd
.mmf application/vnd.smaf
.mmr image/vnd.fujixerox.edmics-mmr
.mng video/x-mng
.mno text/xml
.mny application/x-msmoney
.mobi application/x-mobipocket-ebook
.mod video/mpeg
.mods application/mods+xml
.mov video/quicktime
.movie video/x-sgi-movie
.mp2 audio/mpeg
.mp21 application/mp21
.mp2a audio/mpeg
.mp2v video/mpeg
.mp3 audio/mpeg
.mp4 video/mp4
.mp4a audio/mp4
.mp4s application/mp4
.mp4v video/mp4
.mpa video/mpeg
.mpc application/vnd.mophun.certificate
.mpe video/mpeg
.mpeg video/mpeg
.mpf application/vnd.ms-mediapackage
.mpg video/mpeg
.mpg4 video/mp4
.mpga audio/mpeg
.mpkg application/vnd.apple.installer+xml
.mpm application/vnd.blueice.multipass
.mpn application/vnd.mophun.application
.mpp application/vnd.ms-project
.mpt application/vnd.ms-project
.mpv2 video/mpeg
.mpy application/vnd.ibm.minipay
.mqv video/quicktime
.mqy application/vnd.mobius.mqy
.mrc application/marc
.mrcx application/marcxml+xml
.ms text/troff
.mscml application/mediaservercontrol+xml
.mseed application/vnd.fdsn.mseed
.mseq application/vnd.mseq
.msf application/vnd.epson.msf
.msh model/mesh
.msi application/x-msdownload
.msl application/vnd.mobius.msl
.mso application/octet-stream
.msty application/vnd.muvee.style
.mts model/vnd.mts
.mtx application/xml
.mus application/vnd.musician
.musicxml application/vnd.recordare.musicxml+xml
.mvb application/x-msmediaview
.mvc application/x-miva-compiled
.mwf application/vnd.mfer
.mxf application/mxf
.mxl application/vnd.recordare.musicxml
.mxml application/xv+xml
.mxp application/x-mmxp
.mxs application/vnd.triscape.mxs
.mxu video/vnd.mpegurl
.n3 text/n3
.nb application/mathematica
.nbp application/vnd.wolfram.player
.nc application/x-netcdf
.ncx application/x-dtbncx+xml
.nfo text/x-nfo
.n-gage application/vnd.nokia.n-gage.symbian.install
.ngdat application/vnd.nokia.n-gage.data
.nitf application/vnd.nitf
.nlu application/vnd.neurolanguage.nlu
.nml application/vnd.enliven
.nnd application/vnd.noblenet-directory
.nns application/vnd.noblenet-sealer
.nnw application/vnd.noblenet-web
.npx image/vnd.net-fpx
.nsc application/x-conference
.nsf application/vnd.lotus-notes
.ntf application/vnd.nitf
.nws message/rfc822
.nzb application/x-nzb
.oa2 application/vnd.fujitsu.oasys2
.oa3 application/vnd.fujitsu.oasys3
.oas application/vnd.fujitsu.oasys
.obd application/x-msbinder
.obj application/x-tgif
.ocx application/octet-stream
.oda application/oda
.odb application/vnd.oasis.opendocument.database
.odc application/vnd.oasis.opendocument.chart
.odf application/vnd.oasis.opendocument.formula
.odft application/vnd.oasis.opendocument.formula-template
.odg application/vnd.oasis.opendocument.graphics
.odh text/plain
.odi application/vnd.oasis.opendocument.image
.odl text/plain
.odm application/vnd.oasis.opendocument.text-master
.odp application/vnd.oasis.opendocument.presentation
.ods application/vnd.oasis.opendocument.spreadsheet
.odt application/vnd.oasis.opendocument.text
.oga audio/ogg
.ogg audio/ogg
.ogv video/ogg
.ogx application/ogg
.omdoc application/omdoc+xml
.one application/onenote
.onea application/onenote
.onepkg application/onenote
.onetmp application/onenote
.onetoc application/onenote
.onetoc2 application/onenote
.opf application/oebps-package+xml
.opml text/x-opml
.oprc application/vnd.palm
.orderedtest application/xml
.org application/vnd.lotus-organizer
.osdx application/opensearchdescription+xml
.osf application/vnd.yamaha.openscoreformat
.osfpvg application/vnd.yamaha.openscoreformat.osfpvg+xml
.otc application/vnd.oasis.opendocument.chart-template
.otf application/x-font-otf
.otg application/vnd.oasis.opendocument.graphics-template
.oth application/vnd.oasis.opendocument.text-web
.oti application/vnd.oasis.opendocument.image-template
.otp application/vnd.oasis.opendocument.presentation-template
.ots application/vnd.oasis.opendocument.spreadsheet-template
.ott application/vnd.oasis.opendocument.text-template
.oxps application/oxps
.oxt application/vnd.openofficeorg.extension
.p text/x-pascal
.p10 application/pkcs10
.p12 application/x-pkcs12
.p7b application/x-pkcs7-certificates
.p7c application/pkcs7-mime
.p7m application/pkcs7-mime
.p7r application/x-pkcs7-certreqresp
.p7s application/pkcs7-signature
.p8 application/pkcs8
.pas text/x-pascal
.paw application/vnd.pawaafile
.pbd application/vnd.powerbuilder6
.pbm image/x-portable-bitmap
.pcap application/vnd.tcpdump.pcap
.pcast application/x-podcast
.pcf application/x-font-pcf
.pcl application/vnd.hp-pcl
.pclxl application/vnd.hp-pclxl
.pct image/x-pict
.pcurl application/vnd.curl.pcurl
.pcx image/x-pcx
.pcz application/octet-stream
.pdb application/vnd.palm
.pdf application/pdf
.pfa application/x-font-type1
.pfb application/x-font-type1
.pfm application/x-font-type1
.pfr application/font-tdpfr
.pfx application/x-pkcs12
.pgm image/x-portable-graymap
.pgn application/x-chess-pgn
.pgp application/pgp-encrypted
.pic image/x-pict
.pict image/pict
.pkg application/octet-stream
.pkgdef text/plain
.pkgundef text/plain
.pki application/pkixcmp
.pkipath application/pkix-pkipath
.pko application/vnd.ms-pki.pko
.plb application/vnd.3gpp.pic-bw-large
.plc application/vnd.mobius.plc
.plf application/vnd.pocketlearn
.pls application/pls+xml
.pma application/x-perfmon
.pmc application/x-perfmon
.pml application/vnd.ctc-posml
.pmr application/x-perfmon
.pmw application/x-perfmon
.png image/png
.pnm image/x-portable-anymap
.pnt image/x-macpaint
.pntg image/x-macpaint
.pnz image/png
.portpkg application/vnd.macports.portpkg
.pot application/vnd.ms-powerpoint
.potm application/vnd.ms-powerpoint.template.macroenabled.12
.potx application/vnd.openxmlformats-officedocument.presentationml.template
.ppa application/vnd.ms-powerpoint
.ppam application/vnd.ms-powerpoint.addin.macroenabled.12
.ppd application/vnd.cups-ppd
.ppm image/x-portable-pixmap
.pps application/vnd.ms-powerpoint
.ppsm application/vnd.ms-powerpoint.slideshow\.macroenabled.12
.ppsx application/vnd.openxmlformats-officedocument.presentationml.slideshow
.ppt application/vnd.ms-powerpoint
.pptm application/vnd.ms-powerpoint.presentation.macroenabled.12
.pptx application/vnd.openxmlformats-officedocument.presentationml.presentation
.pqa application/vnd.palm
.prc application/x-mobipocket-ebook
.pre application/vnd.lotus-freelance
.prf application/pics-rules
.prm application/octet-stream
.prx application/octet-stream
.ps application/postscript
.psb application/vnd.3gpp.pic-bw-small
.psc1 application/PowerShell
.psd image/vnd.adobe.photoshop
.psess application/xml
.psf application/x-font-linux-psf
.pskcxml application/pskc+xml
.psm application/octet-stream
.psp application/octet-stream
.ptid application/vnd.pvi.ptid1
.pub application/x-mspublisher
.pvb application/vnd.3gpp.pic-bw-var
.pwn application/vnd.3m.post-it-notes
.pwz application/vnd.ms-powerpoint
.pya audio/vnd.ms-playready.media.pya
.pyv video/vnd.ms-playready.media.pyv
.qam application/vnd.epson.quickanime
.qbo application/vnd.intu.qbo
.qfx application/vnd.intu.qfx
.qht text/x-html-insertion
.qhtm text/x-html-insertion
.qps application/vnd.publishare-delta-tree
.qt video/quicktime
.qti image/x-quicktime
.qtif image/x-quicktime
.qtl application/x-quicktimeplayer
.qwd application/vnd.quark.quarkxpress
.qwt application/vnd.quark.quarkxpress
.qxb application/vnd.quark.quarkxpress
.qxd application/vnd.quark.quarkxpress
.qxl application/vnd.quark.quarkxpress
.qxt application/vnd.quark.quarkxpress
.ra audio/x-pn-realaudio
.ram audio/x-pn-realaudio
.rar application/x-rar-compressed
.ras image/x-cmu-raster
.rat application/rat-file
.rc text/plain
.rc2 text/plain
.rcprofile application/vnd.ipunplugged.rcprofile
.rct text/plain
.rdf application/rdf+xml
.rdlc application/xml
.rdz application/vnd.data-vision.rdz
.rep application/vnd.businessobjects
.res application/x-dtbresource+xml
.resx application/xml
.rf image/vnd.rn-realflash
.rgb image/x-rgb
.rgs text/plain
.rif application/reginfo+xml
.rip audio/vnd.rip
.ris application/x-research-info-systems
.rl application/resource-lists+xml
.rlc image/vnd.fujixerox.edmics-rlc
.rld application/resource-lists-diff+xml
.rm application/vnd.rn-realmedia
.rmi audio/midi
.rmp audio/x-pn-realaudio-plugin
.rms application/vnd.jcp.javame.midlet-rms
.rmvb application/vnd.rn-realmedia-vbr
.rnc application/relax-ng-compact-syntax
.roa application/rpki-roa
.roff text/troff
.rp9 application/vnd.cloanto.rp9
.rpm audio/x-pn-realaudio-plugin
.rpss application/vnd.nokia.radio-presets
.rpst application/vnd.nokia.radio-preset
.rq application/sparql-query
.rqy text/x-ms-rqy
.rs application/rls-services+xml
.rsd application/rsd+xml
.rss application/rss+xml
.rtf application/rtf
.rtx text/richtext
.ruleset application/xml
.s text/x-asm
.s3m audio/s3m
.saf application/vnd.yamaha.smaf-audio
.safariextz application/x-safari-safariextz
.sbml application/sbml+xml
.sc application/vnd.ibm.secure-container
.scd application/x-msschedule
.scm application/vnd.lotus-screencam
.scq application/scvp-cv-request
.scs application/scvp-cv-response
.sct text/scriptlet
.scurl text/vnd.curl.scurl
.sd2 audio/x-sd2
.sda application/vnd.stardivision.draw
.sdc application/vnd.stardivision.calc
.sdd application/vnd.stardivision.impress
.sdkd application/vnd.solent.sdkm+xml
.sdkm application/vnd.solent.sdkm+xml
.sdp application/sdp
.sdw application/vnd.stardivision.writer
.sea application/octet-stream
.searchConnector-ms application/windows-search-connector+xml
.see application/vnd.seemail
.seed application/vnd.fdsn.seed
.sema application/vnd.sema
.semd application/vnd.semd
.semf application/vnd.semf
.ser application/java-serialized-object
.setpay application/set-payment-initiation
.setreg application/set-registration-initiation
.settings application/xml
.sfd-hdstx application/vnd.hydrostatix.sof-data
.sfs application/vnd.spotfire.sfs
.sfv text/x-sfv
.sgi image/sgi
.sgimb application/x-sgimb
.sgl application/vnd.stardivision.writer-global
.sgm text/sgml
.sgml text/sgml
.sh application/x-sh
.shar application/x-shar
.shf application/shf+xml
.shtml text/html
.sid image/x-mrsid-image
.sig application/pgp-signature
.sil audio/silk
.silo model/mesh
.sis application/vnd.symbian.install
.sisx application/vnd.symbian.install
.sit application/x-stuffit
.sitemap application/xml
.sitx application/x-stuffitx
.skd application/vnd.koan
.skin application/xml
.skm application/vnd.koan
.skp application/vnd.koan
.skt application/vnd.koan
.sldm application/vnd.ms-powerpoint.slide.macroenabled.12
.sldx application/vnd.openxmlformats-officedocument.presentationml.slide
.slk application/vnd.ms-excel
.sln text/plain
.slt application/vnd.epson.salt
.slupkg-ms application/x-ms-license
.sm application/vnd.stepmania.stepchart
.smd audio/x-smd
.smf application/vnd.stardivision.math
.smi application/smil+xml
.smil application/smil+xml
.smv video/x-smv
.smx audio/x-smd
.smz audio/x-smd
.smzip application/vnd.stepmania.package
.snd audio/basic
.snf application/x-font-snf
.snippet application/xml
.snp application/octet-stream
.so application/octet-stream
.sol text/plain
.sor text/plain
.spc application/x-pkcs7-certificates
.spf application/vnd.yamaha.smaf-phrase
.spl application/x-futuresplash
.spot text/vnd.in3d.spot
.spp application/scvp-vp-response
.spq application/scvp-vp-request
.spx audio/ogg
.sql application/x-sql
.src application/x-wais-source
.srf text/plain
.srt application/x-subrip
.sru application/sru+xml
.srx application/sparql-results+xml
.ssdl application/ssdl+xml
.sse application/vnd.kodak-descriptor
.ssf application/vnd.epson.ssf
.SSISDeploymentManifest text/xml
.ssm application/streamingmedia
.ssml application/ssml+xml
.sst application/vnd.ms-pki.certstore
.st application/vnd.sailingtracker.track
.stc application/vnd.sun.xml.calc.template
.std application/vnd.sun.xml.draw\.template
.stf application/vnd.wt.stf
.sti application/vnd.sun.xml.impress.template
.stk application/hyperstudio
.stl application/vnd.ms-pki.stl
.str application/vnd.pg.format
.stw application/vnd.sun.xml.writer.template
.sub text/vnd.dvb.subtitle
.sus application/vnd.sus-calendar
.susp application/vnd.sus-calendar
.sv4cpio application/x-sv4cpio
.sv4crc application/x-sv4crc
.svc application/vnd.dvb.service
.svd application/vnd.svd
.svg image/svg+xml
.svgz image/svg+xml
.swa application/x-director
.swf application/x-shockwave-flash
.swi application/vnd.aristanetworks.swi
.sxc application/vnd.sun.xml.calc
.sxd application/vnd.sun.xml.draw
.sxg application/vnd.sun.xml.writer.global
.sxi application/vnd.sun.xml.impress
.sxm application/vnd.sun.xml.math
.sxw application/vnd.sun.xml.writer
.t text/troff
.t3 application/x-t3vm-image
.taglet application/vnd.mynfc
.tao application/vnd.tao.intent-module-archive
.tar application/x-tar
.tcap application/vnd.3gpp2.tcap
.tcl application/x-tcl
.teacher application/vnd.smart.teacher
.tei application/tei+xml
.teicorpus application/tei+xml
.testrunconfig application/xml
.testsettings application/xml
.tex application/x-tex
.texi application/x-texinfo
.texinfo application/x-texinfo
.text text/plain
.tfi application/thraud+xml
.tfm application/x-tex-tfm
.tga image/x-tga
.tgz application/x-compressed
.thmx application/vnd.ms-officetheme
.thn application/octet-stream
.tif image/tiff
.tiff image/tiff
.tlh text/plain
.tli text/plain
.tmo application/vnd.tmobile-livetv
.toc application/octet-stream
.torrent application/x-bittorrent
.tpl application/vnd.groove-tool-template
.tpt application/vnd.trid.tpt
.tr text/troff
.tra application/vnd.trueapp
.trm application/x-msterminal
.trx application/xml
.ts video/vnd.dlna.mpeg-tts
.tsd application/timestamped-data
.tsv text/tab-separated-values
.ttc application/x-font-ttf
.ttf application/x-font-ttf
.ttl text/turtle
.tts video/vnd.dlna.mpeg-tts
.twd application/vnd.simtech-mindmapper
.twds application/vnd.simtech-mindmapper
.txd application/vnd.genomatix.tuxedo
.txf application/vnd.mobius.txf
.txt text/plain
.u32 application/x-authorware-bin
.udeb application/x-debian-package
.ufd application/vnd.ufdl
.ufdl application/vnd.ufdl
.uls text/iuls
.ulx application/x-glulx
.umj application/vnd.umajin
.unityweb application/vnd.unity
.uoml application/vnd.uoml+xml
.uri text/uri-list
.uris text/uri-list
.urls text/uri-list
.user text/plain
.ustar application/x-ustar
.utz application/vnd.uiq.theme
.uu text/x-uuencode
.uva audio/vnd.dece.audio
.uvd application/vnd.dece.data
.uvf application/vnd.dece.data
.uvg image/vnd.dece.graphic
.uvh video/vnd.dece.hd
.uvi image/vnd.dece.graphic
.uvm video/vnd.dece.mobile
.uvp video/vnd.dece.pd
.uvs video/vnd.dece.sd
.uvt application/vnd.dece.ttml+xml
.uvu video/vnd.uvvu.mp4
.uvv video/vnd.dece.video
.uvva audio/vnd.dece.audio
.uvvd application/vnd.dece.data
.uvvf application/vnd.dece.data
.uvvg image/vnd.dece.graphic
.uvvh video/vnd.dece.hd
.uvvi image/vnd.dece.graphic
.uvvm video/vnd.dece.mobile
.uvvp video/vnd.dece.pd
.uvvs video/vnd.dece.sd
.uvvt application/vnd.dece.ttml+xml
.uvvu video/vnd.uvvu.mp4
.uvvv video/vnd.dece.video
.uvvx application/vnd.dece.unspecified
.uvvz application/vnd.dece.zip
.uvx application/vnd.dece.unspecified
.uvz application/vnd.dece.zip
.vb text/plain
.vbdproj text/plain
.vbk video/mpeg
.vbproj text/plain
.vbs text/vbscript
.vcard text/vcard
.vcd application/x-cdlink
.vcf text/x-vcard
.vcg application/vnd.groove-vcard
.vcproj Application/xml
.vcs text/x-vcalendar
.vcx application/vnd.vcx
.vcxproj Application/xml
.vddproj text/plain
.vdp text/plain
.vdproj text/plain
.vdx application/vnd.ms-visio.viewer
.vis application/vnd.visionary
.viv video/vnd.vivo
.vml text/xml
.vob video/x-ms-vob
.vor application/vnd.stardivision.writer
.vox application/x-authorware-bin
.vrml model/vrml
.vscontent application/xml
.vsct text/xml
.vsd application/vnd.visio
.vsf application/vnd.vsf
.vsi application/ms-vsi
.vsix application/vsix
.vsixlangpack text/xml
.vsixmanifest text/xml
.vsmdi application/xml
.vspscc text/plain
.vss application/vnd.visio
.vsscc text/plain
.vssettings text/xml
.vssscc text/plain
.vst application/vnd.visio
.vstemplate text/xml
.vsto application/x-ms-vsto
.vsw application/vnd.visio
.vsx application/vnd.visio
.vtu model/vnd.vtu
.vtx application/vnd.visio
.vxml application/voicexml+xml
.w3d application/x-director
.wad application/x-doom
.wav audio/x-wav
.wave audio/wav
.wax audio/x-ms-wax
.wbk application/msword
.wbmp image/vnd.wap.wbmp
.wbs application/vnd.criticaltools.wbs+xml
.wbxml application/vnd.wap.wbxml
.wcm application/vnd.ms-works
.wdb application/vnd.ms-works
.wdp image/vnd.ms-photo
.weba audio/webm
.webarchive application/x-safari-webarchive
.webm video/webm
.webp image/webp
.webtest application/xml
.wg application/vnd.pmi.widget
.wgt application/widget
.wiq application/xml
.wiz application/msword
.wks application/vnd.ms-works
.WLMP application/wlmoviemaker
.wlpginstall application/x-wlpg-detect
.wlpginstall3 application/x-wlpg3-detect
.wm video/x-ms-wm
.wma audio/x-ms-wma
.wmd application/x-ms-wmd
.wmf application/x-msmetafile
.wml text/vnd.wap.wml
.wmlc application/vnd.wap.wmlc
.wmls text/vnd.wap.wmlscript
.wmlsc application/vnd.wap.wmlscriptc
.wmp video/x-ms-wmp
.wmv video/x-ms-wmv
.wmx video/x-ms-wmx
.wmz application/x-ms-wmz
.woff application/font-woff
.wpd application/vnd.wordperfect
.wpl application/vnd.ms-wpl
.wps application/vnd.ms-works
.wqd application/vnd.wqd
.wri application/x-mswrite
.wrl model/vrml
.wrz x-world/x-vrml
.wsc text/scriptlet
.wsdl application/wsdl+xml
.wspolicy application/wspolicy+xml
.wtb application/vnd.webturbo
.wvx video/x-ms-wvx
.x application/directx
.x32 application/x-authorware-bin
.x3d model/x3d+xml
.x3db model/x3d+binary
.x3dbz model/x3d+binary
.x3dv model/x3d+vrml
.x3dvz model/x3d+vrml
.x3dz model/x3d+xml
.xaf x-world/x-vrml
.xaml application/xaml+xml
.xap application/x-silverlight-app
.xar application/vnd.xara
.xbap application/x-ms-xbap
.xbd application/vnd.fujixerox.docuworks.binder
.xbm image/x-xbitmap
.xdf application/xcap-diff+xml
.xdm application/vnd.syncml.dm+xml
.xdp application/vnd.adobe.xdp+xml
.xdr text/plain
.xdssc application/dssc+xml
.xdw application/vnd.fujixerox.docuworks
.xenc application/xenc+xml
.xer application/patch-ops-error+xml
.xfdf application/vnd.adobe.xfdf
.xfdl application/vnd.xfdl
.xht application/xhtml+xml
.xhtml application/xhtml+xml
.xhvml application/xv+xml
.xif image/vnd.xiff
.xla application/vnd.ms-excel
.xlam application/vnd.ms-excel.addin.macroenabled.12
.xlc application/vnd.ms-excel
.xld application/vnd.ms-excel
.xlf application/x-xliff+xml
.xlk application/vnd.ms-excel
.xll application/vnd.ms-excel
.xlm application/vnd.ms-excel
.xls application/vnd.ms-excel
.xlsb application/vnd.ms-excel.sheet.binary.macroenabled.12
.xlsm application/vnd.ms-excel.sheet.macroenabled.12
.xlsx application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
.xlt application/vnd.ms-excel
.xltm application/vnd.ms-excel.template.macroenabled.12
.xltx application/vnd.openxmlformats-officedocument.spreadsheetml.template
.xlw application/vnd.ms-excel
.xm audio/xm
.xml application/xml
.xmta application/xml
.xo application/vnd.olpc-sugar
.xof x-world/x-vrml
.XOML text/plain
.xop application/xop+xml
.xpi application/x-xpinstall
.xpl application/xproc+xml
.xpm image/x-xpixmap
.xpr application/vnd.is-xpr
.xps application/vnd.ms-xpsdocument
.xpw application/vnd.intercon.formnet
.xpx application/vnd.intercon.formnet
.xrm-ms text/xml
.xsc application/xml
.xsd text/xml
.xsf text/xml
.xsl application/xml
.xslt application/xslt+xml
.xsm application/vnd.syncml+xml
.xsn application/octet-stream
.xspf application/xspf+xml
.xss application/xml
.xtp application/octet-stream
.xul application/vnd.mozilla.xul+xml
.xvm application/xv+xml
.xvml application/xv+xml
.xwd image/x-xwindowdump
.xyz chemical/x-xyz
.xz application/x-xz
.yang application/yang
.yin application/yin+xml
.z application/x-compress
.z1 application/x-zmachine
.z2 application/x-zmachine
.z3 application/x-zmachine
.z4 application/x-zmachine
.z5 application/x-zmachine
.z6 application/x-zmachine
.z7 application/x-zmachine
.z8 application/x-zmachine
.zaz application/vnd.zzazz.deck+xml
.zip application/zip
.zir application/vnd.zul
.zirz application/vnd.zul
.zmm application/vnd.handheld-entertainment+xml
.323 text/h323
.3g2 video/3gpp2
.3gp video/3gpp
.3gp2 video/3gpp2
.3gpp video/3gpp
.7z application/x-7z-compressed
.aa audio/audible
.AAC audio/aac
.aaf application/octet-stream
.aax audio/vnd.audible.aax
.ac3 audio/ac3
.aca application/octet-stream
.accda application/msaccess.addin
.accdb application/msaccess
.accdc application/msaccess.cab
.accde application/msaccess
.accdr application/msaccess.runtime
.accdt application/msaccess
.accdw application/msaccess.webapplication
.accft application/msaccess.ftemplate
.acx application/internet-property-stream
.AddIn text/xml
.ade application/msaccess
.adobebridge application/x-bridge-url
.adp application/msaccess
.ADT audio/vnd.dlna.adts
.ADTS audio/aac
.afm application/octet-stream
.ai application/postscript
.aif audio/aiff
.aifc audio/aiff
.aiff audio/aiff
.air application/vnd.adobe.air-application-installer-package+zip
.amc application/mpeg
.anx application/annodex
.apk application/vnd.android.package-archive
.application application/x-ms-application
.art image/x-jg
.asa application/xml
.asax application/xml
.ascx application/xml
.asd application/octet-stream
.asf video/x-ms-asf
.ashx application/xml
.asi application/octet-stream
.asm text/plain
.asmx application/xml
.aspx application/xml
.asr video/x-ms-asf
.asx video/x-ms-asf
.atom application/atom+xml
.au audio/basic
.avi video/x-msvideo
.axa audio/annodex
.axs application/olescript
.axv video/annodex
.bas text/plain
.bcpio application/x-bcpio
.bin application/octet-stream
.bmp image/bmp
.c text/plain
.cab application/octet-stream
.caf audio/x-caf
.calx application/vnd.ms-office.calx
.cat application/vnd.ms-pki.seccat
.cc text/plain
.cd text/plain
.cdda audio/aiff
.cdf application/x-cdf
.cer application/x-x509-ca-cert
.cfg text/plain
.chm application/octet-stream
.class application/x-java-applet
.clp application/x-msclip
.cmd text/plain
.cmx image/x-cmx
.cnf text/plain
.cod image/cis-cod
.config application/xml
.contact text/x-ms-contact
.coverage application/xml
.cpio application/x-cpio
.cpp text/plain
.crd application/x-mscardfile
.crl application/pkix-crl
.crt application/x-x509-ca-cert
.cs text/plain
.csdproj text/plain
.csh application/x-csh
.csproj text/plain
.css text/css
.csv text/csv
.cur application/octet-stream
.cxx text/plain
.dat application/octet-stream
.datasource application/xml
.dbproj text/plain
.dcr application/x-director
.def text/plain
.deploy application/octet-stream
.der application/x-x509-ca-cert
.dgml application/xml
.dib image/bmp
.dif video/x-dv
.dir application/x-director
.disco text/xml
.divx video/divx
.dll application/x-msdownload
.dll.config text/xml
.dlm text/dlm
.doc application/msword
.docm application/vnd.ms-word.document.macroEnabled.12
.docx application/vnd.openxmlformats-officedocument.wordprocessingml.document
.dot application/msword
.dotm application/vnd.ms-word.template.macroEnabled.12
.dotx application/vnd.openxmlformats-officedocument.wordprocessingml.template
.dsp application/octet-stream
.dsw text/plain
.dtd text/xml
.dtsConfig text/xml
.dv video/x-dv
.dvi application/x-dvi
.dwf drawing/x-dwf
.dwg application/acad
.dwp application/octet-stream
.dxf application/x-dxf
.dxr application/x-director
.eml message/rfc822
.emz application/octet-stream
.eot application/vnd.ms-fontobject
.eps application/postscript
.es application/ecmascript
.etl application/etl
.etx text/x-setext
.evy application/envoy
.exe application/octet-stream
.exe.config text/xml
.fdf application/vnd.fdf
.fif application/fractals
.filters application/xml
.fla application/octet-stream
.flac audio/flac
.flr x-world/x-vrml
.flv video/x-flv
.fsscript application/fsharp-script
.fsx application/fsharp-script
.generictest application/xml
.gif image/gif
.gpx application/gpx+xml
.group text/x-ms-group
.gsm audio/x-gsm
.gtar application/x-gtar
.gz application/x-gzip
.h text/plain
.hdf application/x-hdf
.hdml text/x-hdml
.hhc application/x-oleobject
.hhk application/octet-stream
.hhp application/octet-stream
.hlp application/winhlp
.hpp text/plain
.hqx application/mac-binhex40
.hta application/hta
.htc text/x-component
.htm text/html
.html text/html
.htt text/webviewhtml
.hxa application/xml
.hxc application/xml
.hxd application/octet-stream
.hxe application/xml
.hxf application/xml
.hxh application/octet-stream
.hxi application/octet-stream
.hxk application/xml
.hxq application/octet-stream
.hxr application/octet-stream
.hxs application/octet-stream
.hxt text/html
.hxv application/xml
.hxw application/octet-stream
.hxx text/plain
.i text/plain
.ico image/x-icon
.ics application/octet-stream
.idl text/plain
.ief image/ief
.iii application/x-iphone
.inc text/plain
.inf application/octet-stream
.ini text/plain
.inl text/plain
.ins application/x-internet-signup
.ipa application/x-itunes-ipa
.ipg application/x-itunes-ipg
.ipproj text/plain
.ipsw application/x-itunes-ipsw
.iqy text/x-ms-iqy
.isp application/x-internet-signup
.ite application/x-itunes-ite
.itlp application/x-itunes-itlp
.itms application/x-itunes-itms
.itpc application/x-itunes-itpc
.IVF video/x-ivf
.jar application/java-archive
.java application/octet-stream
.jck application/liquidmotion
.jcz application/liquidmotion
.jfif image/pjpeg
.jnlp application/x-java-jnlp-file
.jpb application/octet-stream
.jpe image/jpeg
.jpeg image/jpeg
.jpg image/jpeg
.js application/javascript
.json application/json
.jsx text/jscript
.jsxbin text/plain
.latex application/x-latex
.library-ms application/windows-library+xml
.lit application/x-ms-reader
.loadtest application/xml
.lpk application/octet-stream
.lsf video/x-la-asf
.lst text/plain
.lsx video/x-la-asf
.lzh application/octet-stream
.m13 application/x-msmediaview
.m14 application/x-msmediaview
.m1v video/mpeg
.m2t video/vnd.dlna.mpeg-tts
.m2ts video/vnd.dlna.mpeg-tts
.m2v video/mpeg
.m3u audio/x-mpegurl
.m3u8 audio/x-mpegurl
.m4a audio/m4a
.m4b audio/m4b
.m4p audio/m4p
.m4r audio/x-m4r
.m4v video/x-m4v
.mac image/x-macpaint
.mak text/plain
.man application/x-troff-man
.manifest application/x-ms-manifest
.map text/plain
.master application/xml
.mbox application/mbox
.mda application/msaccess
.mdb application/x-msaccess
.mde application/msaccess
.mdp application/octet-stream
.me application/x-troff-me
.mfp application/x-shockwave-flash
.mht message/rfc822
.mhtml message/rfc822
.mid audio/mid
.midi audio/mid
.mix application/octet-stream
.mk text/plain
.mk3d video/x-matroska-3d
.mka audio/x-matroska
.mkv video/x-matroska
.mmf application/x-smaf
.mno text/xml
.mny application/x-msmoney
.mod video/mpeg
.mov video/quicktime
.movie video/x-sgi-movie
.mp2 video/mpeg
.mp2v video/mpeg
.mp3 audio/mpeg
.mp4 video/mp4
.mp4v video/mp4
.mpa video/mpeg
.mpe video/mpeg
.mpeg video/mpeg
.mpf application/vnd.ms-mediapackage
.mpg video/mpeg
.mpp application/vnd.ms-project
.mpv2 video/mpeg
.mqv video/quicktime
.ms application/x-troff-ms
.msg application/vnd.ms-outlook
.msi application/octet-stream
.mso application/octet-stream
.mts video/vnd.dlna.mpeg-tts
.mtx application/xml
.mvb application/x-msmediaview
.mvc application/x-miva-compiled
.mxp application/x-mmxp
.nc application/x-netcdf
.nsc video/x-ms-asf
.nws message/rfc822
.ocx application/octet-stream
.oda application/oda
.odb application/vnd.oasis.opendocument.database
.odc application/vnd.oasis.opendocument.chart
.odf application/vnd.oasis.opendocument.formula
.odg application/vnd.oasis.opendocument.graphics
.odh text/plain
.odi application/vnd.oasis.opendocument.image
.odl text/plain
.odm application/vnd.oasis.opendocument.text-master
.odp application/vnd.oasis.opendocument.presentation
.ods application/vnd.oasis.opendocument.spreadsheet
.odt application/vnd.oasis.opendocument.text
.oga audio/ogg
.ogg audio/ogg
.ogv video/ogg
.ogx application/ogg
.one application/onenote
.onea application/onenote
.onepkg application/onenote
.onetmp application/onenote
.onetoc application/onenote
.onetoc2 application/onenote
.opus audio/ogg
.orderedtest application/xml
.osdx application/opensearchdescription+xml
.otf application/font-sfnt
.otg application/vnd.oasis.opendocument.graphics-template
.oth application/vnd.oasis.opendocument.text-web
.otp application/vnd.oasis.opendocument.presentation-template
.ots application/vnd.oasis.opendocument.spreadsheet-template
.ott application/vnd.oasis.opendocument.text-template
.oxt application/vnd.openofficeorg.extension
.p10 application/pkcs10
.p12 application/x-pkcs12
.p7b application/x-pkcs7-certificates
.p7c application/pkcs7-mime
.p7m application/pkcs7-mime
.p7r application/x-pkcs7-certreqresp
.p7s application/pkcs7-signature
.pbm image/x-portable-bitmap
.pcast application/x-podcast
.pct image/pict
.pcx application/octet-stream
.pcz application/octet-stream
.pdf application/pdf
.pfb application/octet-stream
.pfm application/octet-stream
.pfx application/x-pkcs12
.pgm image/x-portable-graymap
.pic image/pict
.pict image/pict
.pkgdef text/plain
.pkgundef text/plain
.pko application/vnd.ms-pki.pko
.pls audio/scpls
.pma application/x-perfmon
.pmc application/x-perfmon
.pml application/x-perfmon
.pmr application/x-perfmon
.pmw application/x-perfmon
.png image/png
.pnm image/x-portable-anymap
.pnt image/x-macpaint
.pntg image/x-macpaint
.pnz image/png
.pot application/vnd.ms-powerpoint
.potm application/vnd.ms-powerpoint.template.macroEnabled.12
.potx application/vnd.openxmlformats-officedocument.presentationml.template
.ppa application/vnd.ms-powerpoint
.ppam application/vnd.ms-powerpoint.addin.macroEnabled.12
.ppm image/x-portable-pixmap
.pps application/vnd.ms-powerpoint
.ppsm application/vnd.ms-powerpoint.slideshow\.macroEnabled.12
.ppsx application/vnd.openxmlformats-officedocument.presentationml.slideshow
.ppt application/vnd.ms-powerpoint
.pptm application/vnd.ms-powerpoint.presentation.macroEnabled.12
.pptx application/vnd.openxmlformats-officedocument.presentationml.presentation
.prf application/pics-rules
.prm application/octet-stream
.prx application/octet-stream
.ps application/postscript
.psc1 application/PowerShell
.psd application/octet-stream
.psess application/xml
.psm application/octet-stream
.psp application/octet-stream
.pst application/vnd.ms-outlook
.pub application/x-mspublisher
.pwz application/vnd.ms-powerpoint
.qht text/x-html-insertion
.qhtm text/x-html-insertion
.qt video/quicktime
.qti image/x-quicktime
.qtif image/x-quicktime
.qtl application/x-quicktimeplayer
.qxd application/octet-stream
.ra audio/x-pn-realaudio
.ram audio/x-pn-realaudio
.rar application/x-rar-compressed
.ras image/x-cmu-raster
.rat application/rat-file
.rc text/plain
.rc2 text/plain
.rct text/plain
.rdlc application/xml
.reg application/octet-stream
.resx application/xml
.rf image/vnd.rn-realflash
.rgb image/x-rgb
.rgs text/plain
.rm application/vnd.rn-realmedia
.rmi audio/mid
.rmp application/vnd.rn-rn_music_package
.roff application/x-troff
.rpm audio/x-pn-realaudio-plugin
.rqy text/x-ms-rqy
.rtf application/rtf
.rtx text/richtext
.rvt application/octet-stream
.ruleset application/xml
.s text/plain
.safariextz application/x-safari-safariextz
.scd application/x-msschedule
.scr text/plain
.sct text/scriptlet
.sd2 audio/x-sd2
.sdp application/sdp
.sea application/octet-stream
.searchConnector-ms application/windows-search-connector+xml
.setpay application/set-payment-initiation
.setreg application/set-registration-initiation
.settings application/xml
.sgimb application/x-sgimb
.sgml text/sgml
.sh application/x-sh
.shar application/x-shar
.shtml text/html
.sit application/x-stuffit
.sitemap application/xml
.skin application/xml
.skp application/x-koan
.sldm application/vnd.ms-powerpoint.slide.macroEnabled.12
.sldx application/vnd.openxmlformats-officedocument.presentationml.slide
.slk application/vnd.ms-excel
.sln text/plain
.slupkg-ms application/x-ms-license
.smd audio/x-smd
.smi application/octet-stream
.smx audio/x-smd
.smz audio/x-smd
.snd audio/basic
.snippet application/xml
.snp application/octet-stream
.sol text/plain
.sor text/plain
.spc application/x-pkcs7-certificates
.spl application/futuresplash
.spx audio/ogg
.src application/x-wais-source
.srf text/plain
.SSISDeploymentManifest text/xml
.ssm application/streamingmedia
.sst application/vnd.ms-pki.certstore
.stl application/vnd.ms-pki.stl
.sv4cpio application/x-sv4cpio
.sv4crc application/x-sv4crc
.svc application/xml
.svg image/svg+xml
.swf application/x-shockwave-flash
.step application/step
.stp application/step
.t application/x-troff
.tar application/x-tar
.tcl application/x-tcl
.testrunconfig application/xml
.testsettings application/xml
.tex application/x-tex
.texi application/x-texinfo
.texinfo application/x-texinfo
.tgz application/x-compressed
.thmx application/vnd.ms-officetheme
.thn application/octet-stream
.tif image/tiff
.tiff image/tiff
.tlh text/plain
.tli text/plain
.toc application/octet-stream
.tr application/x-troff
.trm application/x-msterminal
.trx application/xml
.ts video/vnd.dlna.mpeg-tts
.tsv text/tab-separated-values
.ttf application/font-sfnt
.tts video/vnd.dlna.mpeg-tts
.txt text/plain
.u32 application/octet-stream
.uls text/iuls
.user text/plain
.ustar application/x-ustar
.vb text/plain
.vbdproj text/plain
.vbk video/mpeg
.vbproj text/plain
.vbs text/vbscript
.vcf text/x-vcard
.vcproj application/xml
.vcs text/plain
.vcxproj application/xml
.vddproj text/plain
.vdp text/plain
.vdproj text/plain
.vdx application/vnd.ms-visio.viewer
.vml text/xml
.vscontent application/xml
.vsct text/xml
.vsd application/vnd.visio
.vsi application/ms-vsi
.vsix application/vsix
.vsixlangpack text/xml
.vsixmanifest text/xml
.vsmdi application/xml
.vspscc text/plain
.vss application/vnd.visio
.vsscc text/plain
.vssettings text/xml
.vssscc text/plain
.vst application/vnd.visio
.vstemplate text/xml
.vsto application/x-ms-vsto
.vsw application/vnd.visio
.vsx application/vnd.visio
.vtt text/vtt
.vtx application/vnd.visio
.wasm application/wasm
.wav audio/wav
.wave audio/wav
.wax audio/x-ms-wax
.wbk application/msword
.wbmp image/vnd.wap.wbmp
.wcm application/vnd.ms-works
.wdb application/vnd.ms-works
.wdp image/vnd.ms-photo
.webarchive application/x-safari-webarchive
.webm video/webm
.webp image/webp
.webtest application/xml
.wiq application/xml
.wiz application/msword
.wks application/vnd.ms-works
.WLMP application/wlmoviemaker
.wlpginstall application/x-wlpg-detect
.wlpginstall3 application/x-wlpg3-detect
.wm video/x-ms-wm
.wma audio/x-ms-wma
.wmd application/x-ms-wmd
.wmf application/x-msmetafile
.wml text/vnd.wap.wml
.wmlc application/vnd.wap.wmlc
.wmls text/vnd.wap.wmlscript
.wmlsc application/vnd.wap.wmlscriptc
.wmp video/x-ms-wmp
.wmv video/x-ms-wmv
.wmx video/x-ms-wmx
.wmz application/x-ms-wmz
.woff application/font-woff
.woff2 application/font-woff2
.wpl application/vnd.ms-wpl
.wps application/vnd.ms-works
.wri application/x-mswrite
.wrl x-world/x-vrml
.wrz x-world/x-vrml
.wsc text/scriptlet
.wsdl text/xml
.wvx video/x-ms-wvx
.x application/directx
.xaf x-world/x-vrml
.xaml application/xaml+xml
.xap application/x-silverlight-app
.xbap application/x-ms-xbap
.xbm image/x-xbitmap
.xdr text/plain
.xht application/xhtml+xml
.xhtml application/xhtml+xml
.xla application/vnd.ms-excel
.xlam application/vnd.ms-excel.addin.macroEnabled.12
.xlc application/vnd.ms-excel
.xld application/vnd.ms-excel
.xlk application/vnd.ms-excel
.xll application/vnd.ms-excel
.xlm application/vnd.ms-excel
.xls application/vnd.ms-excel
.xlsb application/vnd.ms-excel.sheet.binary.macroEnabled.12
.xlsm application/vnd.ms-excel.sheet.macroEnabled.12
.xlsx application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
.xlt application/vnd.ms-excel
.xltm application/vnd.ms-excel.template.macroEnabled.12
.xltx application/vnd.openxmlformats-officedocument.spreadsheetml.template
.xlw application/vnd.ms-excel
.xml text/xml
.xmp application/octet-stream
.xmta application/xml
.xof x-world/x-vrml
.XOML text/plain
.xpm image/x-xpixmap
.xps application/vnd.ms-xpsdocument
.xrm-ms text/xml
.xsc application/xml
.xsd text/xml
.xsf text/xml
.xsl text/xml
.xslt text/xml
.xsn application/octet-stream
.xss application/xml
.xspf application/xspf+xml
.xtp application/octet-stream
.xwd image/x-xwindowdump
.z application/x-compress
.zip application/zip
.sh text/plain;charset=UTF-8 #overwrite
.md text/plain;charset=UTF-8 #overwrite
.config text/plain;charset=UTF-8 #overwrite
.ovpn application/x-openvpn-profile
";
    }

}
