// dn_dotnet_http_proxy.cs
// .NET Framework 4.8 / Win32 コンソールアプリ用ソースコード
// BCL のみ使用

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DnDotNetHttpProxy
{
    /// <summary>
    /// プログラムエントリポイントクラス。
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// メイン関数。プロキシサーバーを起動する。
        /// </summary>
        /// <param name="args">コマンドライン引数 (未使用)</param>
        private static void Main(string[] args)
        {
            try
            {
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

                // INI 設定ファイル読み込み
                ProxyConfig config = ProxyConfig.Load(baseDirectory);

                Console.WriteLine("dn_dotnet_http_proxy starting.");
                Console.WriteLine("Listening TCP port: 3128");
                if (!string.IsNullOrEmpty(config.BasicAuthUsername) ||
                    !string.IsNullOrEmpty(config.BasicAuthPassword))
                {
                    Console.WriteLine("Proxy Basic authentication: ENABLED");
                }
                else
                {
                    Console.WriteLine("Proxy Basic authentication: DISABLED");
                }

                Console.WriteLine("Press Ctrl+C to terminate.");

                // プロキシサーバー起動 (非同期だがここでブロック)
                ProxyServer server = new ProxyServer(config, baseDirectory);
                server.RunAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                // ここまで例外が上がってくるのは致命的エラー
                Console.Error.WriteLine("Fatal error: " + ex);
            }
        }
    }

    /// <summary>
    /// プロキシサーバーの設定情報を格納するクラス。
    /// INI ファイル dn_dotnet_http_proxy.ini から読み込む。
    /// </summary>
    internal sealed class ProxyConfig
    {
        /// <summary>
        /// 接続タイムアウト (秒)。既定値 15。
        /// </summary>
        public int TcpConnectTimeoutSeconds { get; set; } = 15;

        /// <summary>
        /// 送信タイムアウト (秒)。既定値 720。
        /// </summary>
        public int TcpSendTimeoutSeconds { get; set; } = 720;

        /// <summary>
        /// 受信タイムアウト (秒)。既定値 720。
        /// </summary>
        public int TcpReceiveTimeoutSeconds { get; set; } = 720;

        /// <summary>
        /// プロキシ Basic 認証用ユーザー名。未設定の場合は認証不要。
        /// </summary>
        public string BasicAuthUsername { get; set; }

        /// <summary>
        /// プロキシ Basic 認証用パスワード。未設定の場合は認証不要。
        /// </summary>
        public string BasicAuthPassword { get; set; }

        /// <summary>
        /// 指定ディレクトリから INI ファイルを読み込み ProxyConfig インスタンスを返す。
        /// </summary>
        /// <param name="baseDirectory">実行ファイルのベースディレクトリ</param>
        /// <returns>読み込まれた設定を持つ ProxyConfig インスタンス</returns>
        public static ProxyConfig Load(string baseDirectory)
        {
            var config = new ProxyConfig();

            // INI ファイル名は仕様通り固定
            string iniPath = Path.Combine(baseDirectory, "dn_dotnet_http_proxy.ini");

            if (!File.Exists(iniPath))
            {
                // INI ファイルが無い場合は既定値のまま
                return config;
            }

            try
            {
                string[] lines = File.ReadAllLines(iniPath, Encoding.UTF8);
                string currentSection = string.Empty;

                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim();

                    // 空行・コメント行は無視
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }

                    if (line.StartsWith(";", StringComparison.Ordinal) ||
                        line.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    // セクション行 [xxx]
                    if (line.StartsWith("[", StringComparison.Ordinal) &&
                        line.EndsWith("]", StringComparison.Ordinal))
                    {
                        currentSection = line.Substring(1, line.Length - 2)
                            .Trim()
                            .ToLowerInvariant();
                        continue;
                    }

                    // key=value 形式
                    int equalIndex = line.IndexOf('=');
                    if (equalIndex <= 0)
                    {
                        continue;
                    }

                    string key = line.Substring(0, equalIndex).Trim().ToLowerInvariant();
                    string value = line.Substring(equalIndex + 1).Trim();

                    if (currentSection == "generic")
                    {
                        int intValue;
                        if (key == "tcp_connect_timeout_secs" &&
                            int.TryParse(value, out intValue))
                        {
                            config.TcpConnectTimeoutSeconds = intValue;
                        }
                        else if (key == "tcp_send_timeout" &&
                                 int.TryParse(value, out intValue))
                        {
                            config.TcpSendTimeoutSeconds = intValue;
                        }
                        else if (key == "tcp_recv_timeout" &&
                                 int.TryParse(value, out intValue))
                        {
                            config.TcpReceiveTimeoutSeconds = intValue;
                        }
                    }
                    else if (currentSection == "security")
                    {
                        if (key == "basic_auth_username")
                        {
                            config.BasicAuthUsername = value;
                        }
                        else if (key == "basic_auth_password")
                        {
                            config.BasicAuthPassword = value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error reading ini file: " + ex);
                // INI 読み込み失敗時も既定値で動作継続
            }

            return config;
        }
    }

    /// <summary>
    /// クライアントから受信した 1 件の HTTP リクエストを表現するクラス。
    /// CONNECT / 通常 HTTP の両方に共通で利用。
    /// </summary>
    internal sealed class HttpRequest
    {
        /// <summary>
        /// HTTP メソッド (GET, POST, CONNECT など)。
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// リクエストターゲット (パス or 絶対 URL or CONNECT の host:port)。
        /// </summary>
        public string RequestTarget { get; set; }

        /// <summary>
        /// HTTP バージョン文字列 (例: HTTP/1.1)。
        /// </summary>
        public string HttpVersion { get; set; }

        /// <summary>
        /// ヘッダーの辞書。キーは大文字小文字を区別しない。
        /// </summary>
        public Dictionary<string, string> Headers { get; private set; }

        /// <summary>
        /// ヘッダーのリスト。順序保持用。重複ヘッダーもそのまま保持する。
        /// </summary>
        public List<KeyValuePair<string, string>> HeaderList { get; private set; }

        /// <summary>
        /// ヘッダー読取時にすでに受信済のボディ先頭部分 (ヘッダー以降の余りバイト)。
        /// </summary>
        public byte[] BodyInitialBytes { get; set; }

        /// <summary>
        /// コンストラクタ。ヘッダーのコンテナを初期化する。
        /// </summary>
        public HttpRequest()
        {
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            HeaderList = new List<KeyValuePair<string, string>>();
            BodyInitialBytes = new byte[0];
        }

        /// <summary>
        /// ヘッダーを追加する。辞書 / リスト両方に追加。
        /// </summary>
        /// <param name="name">ヘッダー名</param>
        /// <param name="value">ヘッダー値</param>
        public void AddHeader(string name, string value)
        {
            Headers[name] = value;
            HeaderList.Add(new KeyValuePair<string, string>(name, value));
        }
    }

    /// <summary>
    /// HTTP/HTTPS プロキシサーバー本体クラス。
    /// </summary>
    internal sealed class ProxyServer
    {
        private readonly ProxyConfig _config;
        private readonly string _baseDirectory;
        private TcpListener _listener;

        // ログ出力の排他制御用オブジェクト
        private static readonly object LogLock = new object();

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="config">プロキシ設定</param>
        /// <param name="baseDirectory">実行ファイルのベースディレクトリ</param>
        public ProxyServer(ProxyConfig config, string baseDirectory)
        {
            if (config == null) throw new ArgumentNullException("config");
            if (baseDirectory == null) throw new ArgumentNullException("baseDirectory");

            _config = config;
            _baseDirectory = baseDirectory;
        }

        /// <summary>
        /// プロキシサーバーのメインループ。
        /// TCP ポート 3128 で待ち受け、各クライアントを非同期で処理する。
        /// </summary>
        /// <returns>完了しない Task (終了時のみ完了)</returns>
        public async Task RunAsync()
        {
            _listener = new TcpListener(IPAddress.Any, 3128);
            _listener.Start();

            Console.WriteLine("Proxy server is running.");

            while (true)
            {
                try
                {
                    // クライアント接続を待ち受け (非同期)
                    TcpClient client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);

                    // 各クライアントは別スレッド/タスクで処理
                    _ = Task.Run(() => HandleClientAsync(client));
                }
                catch (Exception ex)
                {
                    // 待受けループの例外はログに出力しつつ継続
                    LogError("Error accepting client", ex);
                }
            }
        }

        /// <summary>
        /// 1 クライアントとのプロキシ処理を行う。
        /// HTTP/HTTPS (CONNECT) を判別し、それぞれに応じた処理を行う。
        /// </summary>
        /// <param name="client">接続済み TcpClient</param>
        /// <returns>非同期 Task</returns>
        private async Task HandleClientAsync(TcpClient client)
        {
            IPEndPoint clientEndPoint = null;

            try
            {
                clientEndPoint = client.Client.RemoteEndPoint as IPEndPoint;

                client.SendTimeout = _config.TcpSendTimeoutSeconds * 1000;
                client.ReceiveTimeout = _config.TcpReceiveTimeoutSeconds * 1000;

                using (client)
                {
                    NetworkStream clientStream = client.GetStream();

                    HttpRequest request;
                    try
                    {
                        // クライアントから HTTP リクエストヘッダーを読み取る
                        request = await ReadHttpRequestAsync(clientStream).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        LogError("Error reading HTTP request from client", ex);
                        return;
                    }

                    // 接続終了などで何も読めなかった場合
                    if (request == null)
                    {
                        return;
                    }

                    string host;
                    int port;
                    bool isHttps;
                    string urlForLog;

                    // リクエストから接続先ホストとログ用 URL を決定
                    if (!TryGetTargetHostAndUrl(request, out host, out port, out isHttps, out urlForLog))
                    {
                        await SendSimpleErrorResponseAsync(
                            clientStream,
                            request.HttpVersion,
                            "400 Bad Request",
                            "Bad request").ConfigureAwait(false);
                        return;
                    }

                    // Basic 認証要求が有効な場合、Proxy-Authorization ヘッダーを検証
                    if (RequiresProxyAuthentication())
                    {
                        if (!IsProxyAuthorized(request.Headers))
                        {
                            LogError("Proxy authentication failed", null);
                            await SendProxyAuthRequiredAsync(clientStream, request.HttpVersion)
                                .ConfigureAwait(false);
                            return;
                        }
                    }

                    TcpClient serverClient;
                    try
                    {
                        // 接続先サーバーへ TCP 接続
                        serverClient = await ConnectToServerAsync(host, port).ConfigureAwait(false);
                    }
                    catch (TimeoutException tex)
                    {
                        LogError("Connect timeout: " + host + ":" + port, tex);
                        await SendSimpleErrorResponseAsync(
                            clientStream,
                            request.HttpVersion,
                            "504 Gateway Timeout",
                            "Gateway Timeout").ConfigureAwait(false);
                        return;
                    }
                    catch (Exception ex)
                    {
                        LogError("Error connecting to target server " + host + ":" + port, ex);
                        await SendSimpleErrorResponseAsync(
                            clientStream,
                            request.HttpVersion,
                            "502 Bad Gateway",
                            "Bad Gateway").ConfigureAwait(false);
                        return;
                    }

                    using (serverClient)
                    {
                        serverClient.SendTimeout = _config.TcpSendTimeoutSeconds * 1000;
                        serverClient.ReceiveTimeout = _config.TcpReceiveTimeoutSeconds * 1000;

                        NetworkStream serverStream = serverClient.GetStream();

                        // ログ出力 (接続先 URL)
                        LogConnection(clientEndPoint, urlForLog);

                        if (isHttps)
                        {
                            // CONNECT メソッド (HTTPS トンネル)
                            await SendConnectEstablishedResponseAsync(
                                clientStream,
                                request.HttpVersion).ConfigureAwait(false);

                            // CONNECT リクエスト以降は TLS のバイナリをそのまま双方向に転送
                            await BridgeBidirectionalAsync(clientStream, serverStream)
                                .ConfigureAwait(false);
                        }
                        else
                        {
                            // HTTP プロキシ (通常 HTTP)
                            await ForwardHttpRequestToServerAsync(
                                request,
                                serverStream).ConfigureAwait(false);

                            // 以降のボディや後続リクエストは双方向コピーで転送
                            await BridgeBidirectionalAsync(clientStream, serverStream)
                                .ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 1 クライアント処理の最上位例外ハンドラ
                LogError("Unhandled exception in HandleClientAsync", ex);
            }
        }

        /// <summary>
        /// クライアントから HTTP リクエストヘッダーを読み取り、HttpRequest オブジェクトを生成する。
        /// ヘッダー終端 (\r\n\r\n) まで読み取り、余剰バイトは BodyInitialBytes として保持。
        /// </summary>
        /// <param name="clientStream">クライアントからの NetworkStream</param>
        /// <returns>読み取った HttpRequest。データが無い場合は null。</returns>
        private async Task<HttpRequest> ReadHttpRequestAsync(NetworkStream clientStream)
        {
            byte[] buffer = new byte[8192];

            using (var memoryStream = new MemoryStream())
            {
                int headerEndIndex = -1;

                while (true)
                {
                    int bytesRead;
                    try
                    {
                        bytesRead = await clientStream.ReadAsync(buffer, 0, buffer.Length)
                            .ConfigureAwait(false);
                    }
                    catch
                    {
                        return null;
                    }

                    if (bytesRead <= 0)
                    {
                        // 接続終了
                        if (memoryStream.Length == 0)
                        {
                            // 何も受信していない
                            return null;
                        }
                        else
                        {
                            throw new InvalidOperationException("Incomplete HTTP request headers.");
                        }
                    }

                    long previousLength = memoryStream.Length;
                    memoryStream.Write(buffer, 0, bytesRead);

                    if (memoryStream.Length > 16 * 1024)
                    {
                        // ヘッダーサイズの簡易上限 (DoS 防止)
                        throw new InvalidOperationException("HTTP header too long.");
                    }

                    byte[] internalBuffer = memoryStream.GetBuffer();

                    long searchStart = previousLength - 3;
                    if (searchStart < 0) searchStart = 0;

                    for (long i = searchStart; i <= memoryStream.Length - 4; i++)
                    {
                        // \r\n\r\n (CRLFCRLF) を検索
                        if (internalBuffer[i] == 13 &&
                            internalBuffer[i + 1] == 10 &&
                            internalBuffer[i + 2] == 13 &&
                            internalBuffer[i + 3] == 10)
                        {
                            headerEndIndex = (int)(i + 4);
                            break;
                        }
                    }

                    if (headerEndIndex != -1)
                    {
                        break;
                    }
                }

                if (headerEndIndex == -1)
                {
                    return null;
                }

                byte[] headerBytes = new byte[headerEndIndex];
                Array.Copy(memoryStream.GetBuffer(), 0, headerBytes, 0, headerEndIndex);

                byte[] remainingBytes = null;
                long remainingLength = memoryStream.Length - headerEndIndex;
                if (remainingLength > 0)
                {
                    remainingBytes = new byte[remainingLength];
                    Array.Copy(memoryStream.GetBuffer(), headerEndIndex, remainingBytes, 0, remainingLength);
                }
                else
                {
                    remainingBytes = new byte[0];
                }

                string headerText = Encoding.ASCII.GetString(headerBytes);
                string[] headerLines = headerText.Split(
                    new[] { "\r\n" },
                    StringSplitOptions.None);

                int lineIndex = 0;

                // 先頭の空行をスキップ
                while (lineIndex < headerLines.Length &&
                       headerLines[lineIndex].Length == 0)
                {
                    lineIndex++;
                }

                if (lineIndex >= headerLines.Length)
                {
                    throw new InvalidOperationException("Empty HTTP request.");
                }

                string requestLine = headerLines[lineIndex++];
                string[] requestParts = requestLine.Split(new[] { ' ' }, 3);

                if (requestParts.Length < 3)
                {
                    throw new InvalidOperationException("Invalid HTTP request line: " + requestLine);
                }

                var request = new HttpRequest
                {
                    Method = requestParts[0],
                    RequestTarget = requestParts[1],
                    HttpVersion = requestParts[2],
                    BodyInitialBytes = remainingBytes
                };

                // ヘッダー部分を解析
                for (; lineIndex < headerLines.Length; lineIndex++)
                {
                    string line = headerLines[lineIndex];
                    if (string.IsNullOrEmpty(line))
                    {
                        // 空行でヘッダー終端
                        break;
                    }

                    int colonIndex = line.IndexOf(':');
                    if (colonIndex <= 0)
                    {
                        continue;
                    }

                    string name = line.Substring(0, colonIndex).Trim();
                    string value = line.Substring(colonIndex + 1).Trim();

                    request.AddHeader(name, value);
                }

                return request;
            }
        }

        /// <summary>
        /// HttpRequest から接続先ホスト・ポート・URL を決定する。
        /// CONNECT の場合は HTTPS、その他は HTTP として扱う。
        /// </summary>
        /// <param name="request">解析済み HTTP リクエスト</param>
        /// <param name="host">接続先ホスト名</param>
        /// <param name="port">接続先ポート番号</param>
        /// <param name="isHttps">HTTPS (CONNECT) の場合 true</param>
        /// <param name="urlForLog">ログ出力用 URL 文字列</param>
        /// <returns>成功時 true、失敗時 false</returns>
        private bool TryGetTargetHostAndUrl(
            HttpRequest request,
            out string host,
            out int port,
            out bool isHttps,
            out string urlForLog)
        {
            host = null;
            port = 0;
            isHttps = false;
            urlForLog = null;

            if (request == null ||
                string.IsNullOrEmpty(request.Method) ||
                string.IsNullOrEmpty(request.RequestTarget))
            {
                return false;
            }

            // CONNECT メソッド (HTTPS)
            if (string.Equals(request.Method, "CONNECT", StringComparison.OrdinalIgnoreCase))
            {
                isHttps = true;

                string authority = request.RequestTarget;
                int colonIndex = authority.LastIndexOf(':');
                if (colonIndex > 0 && colonIndex < authority.Length - 1)
                {
                    host = authority.Substring(0, colonIndex);
                    string portPart = authority.Substring(colonIndex + 1);
                    if (!int.TryParse(portPart, out port))
                    {
                        port = 443;
                    }
                }
                else
                {
                    host = authority;
                    port = 443;
                }

                urlForLog = "https://" + host;
                return true;
            }

            // 通常 HTTP の場合
            isHttps = false;

            Uri uri;
            // リクエストターゲットが絶対 URL の場合 (プロキシ仕様通り)
            if (Uri.TryCreate(request.RequestTarget, UriKind.Absolute, out uri))
            {
                host = uri.Host;
                port = uri.Port > 0 ? uri.Port : 80;
                urlForLog = uri.ToString();
                return true;
            }

            // 相対パス形式の場合は Host ヘッダーから取得
            string hostHeader;
            if (!request.Headers.TryGetValue("Host", out hostHeader) ||
                string.IsNullOrEmpty(hostHeader))
            {
                return false;
            }

            string hostOnly = hostHeader;
            int colonIndex2 = hostOnly.LastIndexOf(':');

            // IPv6 アドレス [::1]:80 のようなケースを考慮
            int bracketIndex = hostOnly.IndexOf(']');
            if (colonIndex2 > 0 &&
                colonIndex2 < hostOnly.Length - 1 &&
                (bracketIndex < 0 || bracketIndex < colonIndex2))
            {
                host = hostOnly.Substring(0, colonIndex2);
                int portValue;
                if (int.TryParse(hostOnly.Substring(colonIndex2 + 1), out portValue))
                {
                    port = portValue;
                }
            }
            else
            {
                host = hostOnly;
            }

            if (port == 0)
            {
                port = 80;
            }

            string path = request.RequestTarget;
            if (!path.StartsWith("/", StringComparison.Ordinal))
            {
                path = "/" + path;
            }

            urlForLog = "http://" + hostHeader + path;
            return true;
        }

        /// <summary>
        /// プロキシ Basic 認証が有効かどうかを取得する。
        /// ユーザー名・パスワードが両方設定されている場合に有効。
        /// </summary>
        /// <returns>認証が必要な場合 true</returns>
        private bool RequiresProxyAuthentication()
        {
            return !string.IsNullOrEmpty(_config.BasicAuthUsername) &&
                   !string.IsNullOrEmpty(_config.BasicAuthPassword);
        }

        /// <summary>
        /// クライアントからの Proxy-Authorization ヘッダーを検証する。
        /// </summary>
        /// <param name="headers">HTTP ヘッダー辞書</param>
        /// <returns>認証成功時 true / 失敗時 false</returns>
        private bool IsProxyAuthorized(IDictionary<string, string> headers)
        {
            if (headers == null)
            {
                return false;
            }

            string headerValue;
            if (!headers.TryGetValue("Proxy-Authorization", out headerValue) ||
                string.IsNullOrEmpty(headerValue))
            {
                return false;
            }

            string[] parts = headerValue.Split(new[] { ' ' }, 2);
            if (parts.Length != 2)
            {
                return false;
            }

            if (!parts[0].Equals("Basic", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string base64 = parts[1].Trim();
            if (base64.Length == 0)
            {
                return false;
            }

            string decoded;
            try
            {
                byte[] rawBytes = Convert.FromBase64String(base64);
                decoded = Encoding.ASCII.GetString(rawBytes);
            }
            catch
            {
                return false;
            }

            int colonIndex = decoded.IndexOf(':');
            if (colonIndex < 0)
            {
                return false;
            }

            string username = decoded.Substring(0, colonIndex);
            string password = decoded.Substring(colonIndex + 1);

            return string.Equals(username, _config.BasicAuthUsername, StringComparison.Ordinal) &&
                   string.Equals(password, _config.BasicAuthPassword, StringComparison.Ordinal);
        }

        /// <summary>
        /// 指定ホスト・ポートへ TCP 接続を行う。
        /// 接続タイムアウト時間は設定値に従う。
        /// </summary>
        /// <param name="host">接続先ホスト名</param>
        /// <param name="port">接続先ポート番号</param>
        /// <returns>接続済み TcpClient</returns>
        private async Task<TcpClient> ConnectToServerAsync(string host, int port)
        {
            var tcpClient = new TcpClient();
            tcpClient.SendTimeout = _config.TcpSendTimeoutSeconds * 1000;
            tcpClient.ReceiveTimeout = _config.TcpReceiveTimeoutSeconds * 1000;

            Task connectTask = tcpClient.ConnectAsync(host, port);
            Task timeoutTask = Task.Delay(_config.TcpConnectTimeoutSeconds * 1000);

            Task completedTask = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);

            if (completedTask == timeoutTask)
            {
                tcpClient.Close();
                throw new TimeoutException("TCP connect timeout.");
            }

            // ConnectAsync で例外が発生していればここで rethrow
            await connectTask.ConfigureAwait(false);

            return tcpClient;
        }

        /// <summary>
        /// プロキシ認証が必要な場合の 407 レスポンスを送信する。
        /// </summary>
        /// <param name="clientStream">クライアントストリーム</param>
        /// <param name="httpVersion">HTTP バージョン文字列</param>
        /// <returns>非同期 Task</returns>
        private async Task SendProxyAuthRequiredAsync(NetworkStream clientStream, string httpVersion)
        {
            if (string.IsNullOrEmpty(httpVersion))
            {
                httpVersion = "HTTP/1.1";
            }

            string response =
                httpVersion + " 407 Proxy Authentication Required\r\n" +
                "Proxy-Authenticate: Basic realm=\"dn_dotnet_http_proxy\"\r\n" +
                "Content-Length: 0\r\n" +
                "Connection: close\r\n" +
                "\r\n";

            byte[] bytes = Encoding.ASCII.GetBytes(response);

            try
            {
                await clientStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                await clientStream.FlushAsync().ConfigureAwait(false);
            }
            catch
            {
                // 書き込み失敗時は特に何もしない
            }
        }

        /// <summary>
        /// CONNECT メソッド成功時の 200 応答を送信する。
        /// 以降はクライアントとサーバーをそのままブリッジする。
        /// </summary>
        /// <param name="clientStream">クライアントストリーム</param>
        /// <param name="httpVersion">HTTP バージョン文字列</param>
        /// <returns>非同期 Task</returns>
        private async Task SendConnectEstablishedResponseAsync(NetworkStream clientStream, string httpVersion)
        {
            if (string.IsNullOrEmpty(httpVersion))
            {
                httpVersion = "HTTP/1.1";
            }

            string response =
                httpVersion + " 200 Connection Established\r\n" +
                "Proxy-Agent: dn_dotnet_http_proxy\r\n" +
                "\r\n";

            byte[] bytes = Encoding.ASCII.GetBytes(response);

            try
            {
                await clientStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                await clientStream.FlushAsync().ConfigureAwait(false);
            }
            catch
            {
                // 書き込み失敗時は特に何もしない
            }
        }

        /// <summary>
        /// エラー応答 (4xx / 5xx) を簡易 HTML でクライアントへ送信する。
        /// </summary>
        /// <param name="clientStream">クライアントストリーム</param>
        /// <param name="httpVersion">HTTP バージョン文字列 (null/空の場合は HTTP/1.1)</param>
        /// <param name="status">ステータス行 (例: "502 Bad Gateway")</param>
        /// <param name="message">簡易メッセージ</param>
        /// <returns>非同期 Task</returns>
        private async Task SendSimpleErrorResponseAsync(
            NetworkStream clientStream,
            string httpVersion,
            string status,
            string message)
        {
            if (string.IsNullOrEmpty(httpVersion))
            {
                httpVersion = "HTTP/1.1";
            }

            string body =
                "<html><body><h1>" + status + "</h1><p>" +
                message +
                "</p></body></html>";

            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("{0} {1}\r\n", httpVersion, status);
            sb.Append("Content-Type: text/html; charset=utf-8\r\n");
            sb.AppendFormat("Content-Length: {0}\r\n", bodyBytes.Length);
            sb.Append("Connection: close\r\n");
            sb.Append("\r\n");

            byte[] headerBytes = Encoding.ASCII.GetBytes(sb.ToString());

            try
            {
                await clientStream.WriteAsync(headerBytes, 0, headerBytes.Length)
                    .ConfigureAwait(false);
                await clientStream.WriteAsync(bodyBytes, 0, bodyBytes.Length)
                    .ConfigureAwait(false);
                await clientStream.FlushAsync().ConfigureAwait(false);
            }
            catch
            {
                // 書き込み失敗時は特に何もしない
            }
        }

        /// <summary>
        /// HTTP リクエストを接続先サーバーへ転送する。
        /// 絶対 URL の場合はパス部分に変換し、Proxy-Authorization など不要ヘッダーを除去する。
        /// ヘッダー直後にすでに読み込み済のボディバイト (BodyInitialBytes) も転送する。
        /// </summary>
        /// <param name="request">HTTP リクエストデータ</param>
        /// <param name="serverStream">接続先サーバーの NetworkStream</param>
        /// <returns>非同期 Task</returns>
        private async Task ForwardHttpRequestToServerAsync(
            HttpRequest request,
            NetworkStream serverStream)
        {
            string outRequestTarget = request.RequestTarget;

            Uri absoluteUri;
            if (Uri.TryCreate(request.RequestTarget, UriKind.Absolute, out absoluteUri))
            {
                // HTTP/1.1 仕様上、サーバーへの送信時はパスのみ (origin-form) にする
                string pathAndQuery = absoluteUri.PathAndQuery;
                if (string.IsNullOrEmpty(pathAndQuery))
                {
                    pathAndQuery = "/";
                }

                // フラグメント (# 以降) はサーバーへ送らないのが仕様
                outRequestTarget = pathAndQuery;
            }

            using (var writer = new StreamWriter(serverStream, Encoding.ASCII, 8192, true))
            {
                writer.NewLine = "\r\n";

                // リクエストラインを送信
                await writer
                    .WriteLineAsync(
                        string.Format(
                            "{0} {1} {2}",
                            request.Method,
                            outRequestTarget,
                            request.HttpVersion))
                    .ConfigureAwait(false);

                // ヘッダー送信 (Proxy-Authorization, Proxy-Connection は除外)
                foreach (KeyValuePair<string, string> header in request.HeaderList)
                {
                    string name = header.Key;
                    if (name.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (name.Equals("Proxy-Connection", StringComparison.OrdinalIgnoreCase))
                    {
                        // サーバーには不要なので破棄
                        continue;
                    }

                    string value = header.Value;
                    await writer
                        .WriteLineAsync(name + ": " + value)
                        .ConfigureAwait(false);
                }

                // ヘッダー終端 (空行)
                await writer.WriteLineAsync().ConfigureAwait(false);
                await writer.FlushAsync().ConfigureAwait(false);
            }

            // すでに読み込み済みのボディ先頭バイトをサーバーへ送信
            if (request.BodyInitialBytes != null && request.BodyInitialBytes.Length > 0)
            {
                await serverStream
                    .WriteAsync(
                        request.BodyInitialBytes,
                        0,
                        request.BodyInitialBytes.Length)
                    .ConfigureAwait(false);
                await serverStream.FlushAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// クライアントとサーバーの両方向ストリームを非同期にコピーし続ける。
        /// いずれか片側が終了したら両側をクローズする。
        /// </summary>
        /// <param name="clientStream">クライアントストリーム</param>
        /// <param name="serverStream">サーバーストリーム</param>
        /// <returns>非同期 Task</returns>
        private async Task BridgeBidirectionalAsync(
            NetworkStream clientStream,
            NetworkStream serverStream)
        {
            Task clientToServer = CopyStreamAsync(clientStream, serverStream);
            Task serverToClient = CopyStreamAsync(serverStream, clientStream);

            // どちらか一方が終わるまで待つ
            await Task.WhenAny(clientToServer, serverToClient).ConfigureAwait(false);

            try
            {
                clientStream.Close();
            }
            catch
            {
            }

            try
            {
                serverStream.Close();
            }
            catch
            {
            }

            try
            {
                await Task.WhenAll(clientToServer, serverToClient).ConfigureAwait(false);
            }
            catch
            {
                // コピー中の例外はここでは無視 (ログは CopyStreamAsync 側で処理)
            }
        }

        /// <summary>
        /// 一方向のストリームコピーを行う。
        /// 読み取り / 書き込み中の例外はログ出力のみ行い、呼び出し元には再スローしない。
        /// </summary>
        /// <param name="input">入力ストリーム</param>
        /// <param name="output">出力ストリーム</param>
        /// <returns>非同期 Task</returns>
        private async Task CopyStreamAsync(Stream input, Stream output)
        {
            byte[] buffer = new byte[81920];

            try
            {
                while (true)
                {
                    int bytesRead = await input.ReadAsync(buffer, 0, buffer.Length)
                        .ConfigureAwait(false);
                    if (bytesRead <= 0)
                    {
                        break;
                    }

                    await output.WriteAsync(buffer, 0, bytesRead).ConfigureAwait(false);
                    await output.FlushAsync().ConfigureAwait(false);
                }
            }
            catch (IOException)
            {
                // ネットワーク切断などは想定内なので特に何もしない
            }
            catch (ObjectDisposedException)
            {
                // ストリームクローズ後の読み書きも無視
            }
            catch (Exception ex)
            {
                // 予期しない例外のみログ
                LogError("Error during stream copy", ex);
            }
        }

        /// <summary>
        /// 接続ログをファイルおよびコンソールに出力する。
        /// ログファイルは log_files\YYYYMMDD.log の形式。
        /// </summary>
        /// <param name="clientEndPoint">クライアントの IP/ポート</param>
        /// <param name="url">接続先 URL (HTTPS の場合は https://host)</param>
        private void LogConnection(IPEndPoint clientEndPoint, string url)
        {
            try
            {
                DateTimeOffset now = DateTimeOffset.Now;
                string timestamp = now.ToString("o"); // ISO 8601 (オフセット付き)
                string clientInfo = "unknown";

                if (clientEndPoint != null)
                {
                    clientInfo = clientEndPoint.Address + ":" + clientEndPoint.Port;
                }

                string logLine =
                    timestamp + "\t" +
                    clientInfo + "\t" +
                    url;

                Console.WriteLine(logLine);

                string logDirectory = Path.Combine(_baseDirectory, "log_files");
                Directory.CreateDirectory(logDirectory);

                string fileName = now.ToString("yyyyMMdd") + ".log";
                string logPath = Path.Combine(logDirectory, fileName);

                lock (LogLock)
                {
                    File.AppendAllText(logPath, logLine + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                // ログ出力失敗自体はプログラム継続を優先
                Console.Error.WriteLine("Failed to write log: " + ex);
            }
        }

        /// <summary>
        /// エラー用ログをコンソールおよび (可能なら) ログファイルへ出力する。
        /// </summary>
        /// <param name="message">エラーメッセージ</param>
        /// <param name="exception">例外オブジェクト (null 可)</param>
        private void LogError(string message, Exception exception)
        {
            try
            {
                string fullMessage = message;

                if (exception != null)
                {
                    fullMessage += " - " + exception.GetType().FullName + ": " + exception.Message;
                }

                DateTimeOffset now = DateTimeOffset.Now;
                string timestamp = now.ToString("o");

                string logLine = timestamp + "\tERROR\t" + fullMessage;

                Console.Error.WriteLine(logLine);

                string logDirectory = Path.Combine(_baseDirectory, "log_files");
                Directory.CreateDirectory(logDirectory);

                string fileName = now.ToString("yyyyMMdd") + ".log";
                string logPath = Path.Combine(logDirectory, fileName);

                lock (LogLock)
                {
                    File.AppendAllText(logPath, logLine + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // ログ出力に失敗した場合は諦める
            }
        }
    }
}
