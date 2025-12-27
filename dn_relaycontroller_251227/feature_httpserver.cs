using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace dn_relaycontroller_251227;

/// <summary>
/// TcpListener を用いた、必要最小限の HTTP 0.9/1.0/1.1 サーバー。
/// </summary>
sealed class SimpleHttpServer
{
    /// <summary>
    /// listen 済みの TcpListener。
    /// </summary>
    readonly TcpListener _listener;

    /// <summary>
    /// /keep_lock アクセス記録用の共有状態。
    /// </summary>
    readonly KeepLockState _keepLockState;

    /// <summary>
    /// TCP コネクションのアイドルタイムアウト。
    /// </summary>
    readonly TimeSpan _connectionIdleTimeout;

    /// <summary>
    /// コンストラクタ。
    /// </summary>
    /// <param name="listener">listen 済みの TcpListener。</param>
    /// <param name="keepLockState">/keep_lock アクセス記録用の共有状態。</param>
    /// <param name="connectionIdleTimeout">TCP コネクションのアイドルタイムアウト。</param>
    public SimpleHttpServer(TcpListener listener, KeepLockState keepLockState, TimeSpan connectionIdleTimeout)
    {
        _listener = listener ?? throw new ArgumentNullException(nameof(listener));
        _keepLockState = keepLockState ?? throw new ArgumentNullException(nameof(keepLockState));
        _connectionIdleTimeout = connectionIdleTimeout;
    }

    /// <summary>
    /// サーバーの accept ループを開始する。
    /// </summary>
    /// <param name="cancel">停止要求。</param>
    public async Task RunAsync(CancellationToken cancel = default)
    {
        using CancellationTokenRegistration reg = cancel.Register(() =>
        {
            try
            {
                _listener.Stop();
            }
            catch
            {
            }
        });

        while (cancel.IsCancellationRequested == false)
        {
            TcpClient? client = null;
            try
            {
                client = await _listener.AcceptTcpClientAsync();
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException ex)
            {
                if (cancel.IsCancellationRequested)
                {
                    break;
                }

                AppConsole.WriteLine($"APPERROR: Failed to accept a TCP client. Detail: {ex}");
                continue;
            }
            catch (Exception ex)
            {
                if (cancel.IsCancellationRequested)
                {
                    break;
                }

                AppConsole.WriteLine($"APPERROR: Failed to accept a TCP client. Detail: {ex}");
                continue;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await HandleClientAsync(client, _keepLockState, _connectionIdleTimeout, cancel);
                }
                catch (Exception ex)
                {
                    AppConsole.WriteLine($"APPERROR: Unhandled error in HTTP client task. Detail: {ex}");
                }
            });
        }
    }

    /// <summary>
    /// サーバーを停止する。
    /// </summary>
    public void Stop()
    {
        try
        {
            _listener.Stop();
        }
        catch
        {
        }
    }

    /// <summary>
    /// 1 クライアント接続を処理する。
    /// </summary>
    /// <param name="client">TCP クライアント。</param>
    /// <param name="keepLockState">/keep_lock アクセス記録用の共有状態。</param>
    /// <param name="idleTimeout">アイドルタイムアウト。</param>
    /// <param name="serverCancel">サーバー停止要求。</param>
    static async Task HandleClientAsync(TcpClient client, KeepLockState keepLockState, TimeSpan idleTimeout, CancellationToken serverCancel)
    {
        using (client)
        {
            client.NoDelay = true;
            client.ReceiveTimeout = (int)idleTimeout.TotalMilliseconds;
            client.SendTimeout = (int)idleTimeout.TotalMilliseconds;

            using NetworkStream stream = client.GetStream();
            using HttpReadBuffer readBuffer = new(stream, idleTimeout, serverCancel);

            try
            {
                while (serverCancel.IsCancellationRequested == false)
                {
                    string? requestLine = await readBuffer.ReadLineAsync(maxLineBytes: 8192);
                    if (requestLine == null)
                    {
                        break;
                    }

                    if (requestLine.Length == 0)
                    {
                        continue;
                    }

                    if (TryParseRequestLine(requestLine, out HttpRequest req, out string parseError) == false)
                    {
                        await WriteHttpResponseAsync(stream, httpMajor: 1, httpMinor: 1, statusCode: 400, reasonPhrase: "Bad Request",
                            contentType: "text/plain", body: Encoding.UTF8.GetBytes("Bad Request."), keepAlive: false, isHead: false, serverCancel);
                        AppConsole.WriteLine($"APPERROR: Invalid HTTP request line. Detail: {parseError}");
                        break;
                    }

                    if (req.IsHttp09 == false)
                    {
                        // ヘッダ読み取り
                        int totalHeaderBytes = 0;
                        while (true)
                        {
                            string? headerLine = await readBuffer.ReadLineAsync(maxLineBytes: 8192);
                            if (headerLine == null)
                            {
                                return;
                            }

                            totalHeaderBytes += headerLine.Length;
                            if (totalHeaderBytes > 65536)
                            {
                                await WriteHttpResponseAsync(stream, req.HttpMajor, req.HttpMinor, 431, "Request Header Fields Too Large",
                                    "text/plain", Encoding.UTF8.GetBytes("Header too large."), keepAlive: false, isHead: false, serverCancel);
                                return;
                            }

                            if (headerLine.Length == 0)
                            {
                                break;
                            }

                            ParseHeaderLine(headerLine, req.Headers);
                        }

                        // GET 以外が body を伴っていた場合でも、次リクエストを正しく読めるように読み捨てる。
                        if (TryGetContentLength(req.Headers, out int contentLength) && contentLength > 0)
                        {
                            await readBuffer.DiscardBytesAsync(contentLength);
                        }
                    }

                    // リクエスト処理 (/keep_lock のみ)
                    bool keepAlive = ShouldKeepAlive(req);
                    bool isHead = req.Method.Equals("HEAD", StringComparison.OrdinalIgnoreCase);

                    if (req.Path.Equals("/keep_lock", StringComparison.Ordinal) &&
                        (req.Method.Equals("GET", StringComparison.OrdinalIgnoreCase) || isHead))
                    {
                        keepLockState.MarkAccessNow();

                        if (req.IsHttp09)
                        {
                            byte[] body = Encoding.UTF8.GetBytes("Ok.");
                            await stream.WriteAsync(body, 0, body.Length, serverCancel);
                            await stream.FlushAsync(serverCancel);
                            break;
                        }

                        await WriteHttpResponseAsync(stream, req.HttpMajor, req.HttpMinor, 200, "OK",
                            contentType: "text/plain", body: Encoding.UTF8.GetBytes("Ok."), keepAlive: keepAlive, isHead: isHead, serverCancel);

                        if (keepAlive == false)
                        {
                            break;
                        }
                    }
                    else if (req.Path.Equals("/keep_lock", StringComparison.Ordinal))
                    {
                        if (req.IsHttp09)
                        {
                            byte[] body = Encoding.UTF8.GetBytes("Not Found.");
                            await stream.WriteAsync(body, 0, body.Length, serverCancel);
                            await stream.FlushAsync(serverCancel);
                            break;
                        }

                        // /keep_lock に対して GET/HEAD 以外の場合は 405 を返す。
                        Dictionary<string, string> extraHeaders = new(StringComparer.OrdinalIgnoreCase)
                        {
                            ["Allow"] = "GET, HEAD",
                        };

                        await WriteHttpResponseAsync(stream, req.HttpMajor, req.HttpMinor, 405, "Method Not Allowed",
                            contentType: "text/plain", body: Encoding.UTF8.GetBytes("Method Not Allowed."), keepAlive: keepAlive, isHead: isHead, serverCancel, extraHeaders);

                        if (keepAlive == false)
                        {
                            break;
                        }
                    }
                    else
                    {
                        if (req.IsHttp09)
                        {
                            byte[] body = Encoding.UTF8.GetBytes("Not Found.");
                            await stream.WriteAsync(body, 0, body.Length, serverCancel);
                            await stream.FlushAsync(serverCancel);
                            break;
                        }

                        await WriteHttpResponseAsync(stream, req.HttpMajor, req.HttpMinor, 404, "Not Found",
                            contentType: "text/plain", body: Encoding.UTF8.GetBytes("Not Found."), keepAlive: keepAlive, isHead: isHead, serverCancel);

                        if (keepAlive == false)
                        {
                            break;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // アイドルタイムアウト、またはサーバー停止要求によるキャンセルとして扱い、静かに切断する。
            }
        }
    }

    /// <summary>
    /// HTTP レスポンスを書き込む。
    /// </summary>
    /// <param name="stream">送信先ストリーム。</param>
    /// <param name="httpMajor">HTTP メジャーバージョン。</param>
    /// <param name="httpMinor">HTTP マイナーバージョン。</param>
    /// <param name="statusCode">HTTP ステータスコード。</param>
    /// <param name="reasonPhrase">理由句。</param>
    /// <param name="contentType">Content-Type。</param>
    /// <param name="body">本文。</param>
    /// <param name="keepAlive">Keep-Alive を有効にするか。</param>
    /// <param name="isHead">HEAD リクエストか。</param>
    /// <param name="cancel">キャンセル。</param>
    /// <param name="extraHeaders">追加ヘッダ (任意)。</param>
    static async Task WriteHttpResponseAsync(
        NetworkStream stream,
        int httpMajor,
        int httpMinor,
        int statusCode,
        string reasonPhrase,
        string contentType,
        byte[] body,
        bool keepAlive,
        bool isHead,
        CancellationToken cancel,
        Dictionary<string, string>? extraHeaders = null)
    {
        int contentLength = body.Length;

        StringBuilder sb = new();
        sb.Append($"HTTP/{httpMajor}.{httpMinor} {statusCode} {reasonPhrase}\r\n");
        sb.Append($"Date: {DateTimeOffset.UtcNow:R}\r\n");
        sb.Append("Server: dn_relaycontroller_251227\r\n");
        sb.Append($"Content-Type: {contentType}\r\n");
        sb.Append($"Content-Length: {contentLength}\r\n");
        sb.Append($"Connection: {(keepAlive ? "keep-alive" : "close")}\r\n");

        if (extraHeaders != null)
        {
            foreach ((string key, string value) in extraHeaders)
            {
                sb.Append($"{key}: {value}\r\n");
            }
        }

        sb.Append("\r\n");

        byte[] headerBytes = Encoding.ASCII.GetBytes(sb.ToString());
        await stream.WriteAsync(headerBytes, 0, headerBytes.Length, cancel);

        if (isHead == false)
        {
            await stream.WriteAsync(body, 0, body.Length, cancel);
        }

        await stream.FlushAsync(cancel);
    }

    /// <summary>
    /// リクエスト行のパースを行なう。
    /// </summary>
    /// <param name="requestLine">リクエスト行。</param>
    /// <param name="req">パース結果。</param>
    /// <param name="error">失敗時の説明。</param>
    /// <returns>成功した場合 true。</returns>
    static bool TryParseRequestLine(string requestLine, out HttpRequest req, out string error)
    {
        req = default;
        error = "";

        string[] parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2)
        {
            string method = parts[0];
            string target = parts[1];
            req = HttpRequest.CreateHttp09(method, target);
            return true;
        }

        if (parts.Length != 3)
        {
            error = "Invalid token count.";
            return false;
        }

        string httpVersion = parts[2];
        if (httpVersion.StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase) == false)
        {
            error = "Missing HTTP/ prefix.";
            return false;
        }

        string ver = httpVersion.Substring("HTTP/".Length);
        int dot = ver.IndexOf('.');
        if (dot <= 0 || dot == ver.Length - 1)
        {
            error = "Invalid HTTP version format.";
            return false;
        }

        if (int.TryParse(ver.Substring(0, dot), out int major) == false ||
            int.TryParse(ver.Substring(dot + 1), out int minor) == false)
        {
            error = "Invalid HTTP version numbers.";
            return false;
        }

        req = HttpRequest.CreateHttp1x(parts[0], parts[1], major, minor);
        return true;
    }

    /// <summary>
    /// ヘッダ行をパースして辞書に格納する。
    /// </summary>
    /// <param name="headerLine">ヘッダ行。</param>
    /// <param name="headers">ヘッダ辞書。</param>
    static void ParseHeaderLine(string headerLine, Dictionary<string, string> headers)
    {
        int idx = headerLine.IndexOf(':');
        if (idx <= 0)
        {
            return;
        }

        string name = headerLine.Substring(0, idx).Trim();
        string value = headerLine.Substring(idx + 1).Trim();

        if (name.Length == 0)
        {
            return;
        }

        headers[name] = value;
    }

    /// <summary>
    /// Content-Length を取得する。
    /// </summary>
    /// <param name="headers">ヘッダ辞書。</param>
    /// <param name="contentLength">取得結果。</param>
    /// <returns>取得できた場合 true。</returns>
    static bool TryGetContentLength(Dictionary<string, string> headers, out int contentLength)
    {
        contentLength = 0;
        if (headers.TryGetValue("Content-Length", out string? value) == false)
        {
            return false;
        }

        return int.TryParse(value, out contentLength);
    }

    /// <summary>
    /// Keep-Alive の可否を判定する。
    /// </summary>
    /// <param name="req">HTTP リクエスト。</param>
    /// <returns>Keep-Alive を行なう場合 true。</returns>
    static bool ShouldKeepAlive(HttpRequest req)
    {
        if (req.IsHttp09)
        {
            return false;
        }

        if (req.Headers.TryGetValue("Connection", out string? connection))
        {
            if (HeaderValueContainsToken(connection, "close"))
            {
                return false;
            }

            if (HeaderValueContainsToken(connection, "keep-alive"))
            {
                return true;
            }
        }

        // HTTP/1.1 はデフォルトで keep-alive、HTTP/1.0 はデフォルトで close。
        if (req.HttpMajor == 1 && req.HttpMinor >= 1)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// ヘッダ値に、指定トークンが含まれるか (カンマ区切り想定) を判定する。
    /// </summary>
    /// <param name="headerValue">ヘッダ値。</param>
    /// <param name="token">検索トークン。</param>
    /// <returns>含まれる場合 true。</returns>
    static bool HeaderValueContainsToken(string headerValue, string token)
    {
        foreach (string part in headerValue.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Trim().Equals(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// HTTP リクエストの最小表現。
    /// </summary>
    readonly struct HttpRequest
    {
        /// <summary>
        /// メソッド。
        /// </summary>
        public readonly string Method;

        /// <summary>
        /// 要求ターゲット (例: /keep_lock?x=1)。
        /// </summary>
        public readonly string Target;

        /// <summary>
        /// パス部 (例: /keep_lock)。
        /// </summary>
        public readonly string Path;

        /// <summary>
        /// HTTP 0.9 相当かどうか。
        /// </summary>
        public readonly bool IsHttp09;

        /// <summary>
        /// HTTP メジャーバージョン。
        /// </summary>
        public readonly int HttpMajor;

        /// <summary>
        /// HTTP マイナーバージョン。
        /// </summary>
        public readonly int HttpMinor;

        /// <summary>
        /// ヘッダ辞書 (大小文字区別なし)。
        /// </summary>
        public readonly Dictionary<string, string> Headers;

        HttpRequest(string method, string target, bool isHttp09, int httpMajor, int httpMinor)
        {
            Method = method;
            Target = target;
            Path = ExtractPath(target);
            IsHttp09 = isHttp09;
            HttpMajor = httpMajor;
            HttpMinor = httpMinor;
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// HTTP/0.9 のリクエストを生成する。
        /// </summary>
        /// <param name="method">メソッド。</param>
        /// <param name="target">要求ターゲット。</param>
        /// <returns>生成したリクエスト。</returns>
        public static HttpRequest CreateHttp09(string method, string target)
            => new(method, target, isHttp09: true, httpMajor: 0, httpMinor: 9);

        /// <summary>
        /// HTTP/1.x のリクエストを生成する。
        /// </summary>
        /// <param name="method">メソッド。</param>
        /// <param name="target">要求ターゲット。</param>
        /// <param name="httpMajor">HTTP メジャーバージョン。</param>
        /// <param name="httpMinor">HTTP マイナーバージョン。</param>
        /// <returns>生成したリクエスト。</returns>
        public static HttpRequest CreateHttp1x(string method, string target, int httpMajor, int httpMinor)
            => new(method, target, isHttp09: false, httpMajor: httpMajor, httpMinor: httpMinor);

        /// <summary>
        /// ターゲットからパス部を抽出する。
        /// </summary>
        /// <param name="target">要求ターゲット。</param>
        /// <returns>パス部。</returns>
        static string ExtractPath(string target)
        {
            int q = target.IndexOf('?');
            if (q >= 0)
            {
                return target.Substring(0, q);
            }
            return target;
        }
    }

    /// <summary>
    /// NetworkStream からの行読み取り/読み捨てを、アイドルタイムアウト付きで行なう。
    /// </summary>
    sealed class HttpReadBuffer : IDisposable
    {
        readonly NetworkStream _stream;
        readonly TimeSpan _idleTimeout;
        readonly CancellationTokenSource _idleCts;
        readonly byte[] _buffer;
        int _pos;
        int _len;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="stream">読み取り元ストリーム。</param>
        /// <param name="idleTimeout">アイドルタイムアウト。</param>
        /// <param name="cancel">停止要求。</param>
        public HttpReadBuffer(NetworkStream stream, TimeSpan idleTimeout, CancellationToken cancel)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _idleTimeout = idleTimeout;
            _idleCts = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            _idleCts.CancelAfter(_idleTimeout);
            _buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(8192);
        }

        /// <summary>
        /// CRLF/LF 区切りで 1 行読み取る。切断された場合は null を返す。
        /// </summary>
        /// <param name="maxLineBytes">最大行長 (バイト)。</param>
        /// <returns>1 行 (末尾改行を除去済み) または null。</returns>
        public async Task<string?> ReadLineAsync(int maxLineBytes)
        {
            byte[]? pooledLine = null;
            int pooledLen = 0;

            try
            {
                while (true)
                {
                    int available = _len - _pos;
                    int nlAbs = Array.IndexOf(_buffer, (byte)'\n', _pos, available);
                    if (nlAbs >= 0)
                    {
                        int linePartLen = nlAbs - _pos; // '\n' は除外

                        // '\n' まで消費 (この時点では _pos はまだ更新しないようにし、デコードに使用する)。
                        int consumeCount = linePartLen + 1;

                        if (pooledLine != null)
                        {
                            if (pooledLen + linePartLen > maxLineBytes)
                            {
                                throw new InvalidOperationException("APPERROR: HTTP line too long.");
                            }

                            Buffer.BlockCopy(_buffer, _pos, pooledLine, pooledLen, linePartLen);
                            pooledLen += linePartLen;

                            ConsumeBytes(consumeCount);

                            // CRLF の CR を除去
                            if (pooledLen > 0 && pooledLine[pooledLen - 1] == (byte)'\r')
                            {
                                pooledLen--;
                            }

                            return Encoding.ASCII.GetString(pooledLine, 0, pooledLen);
                        }
                        else
                        {
                            if (linePartLen > maxLineBytes)
                            {
                                throw new InvalidOperationException("APPERROR: HTTP line too long.");
                            }

                            int decodeLen = linePartLen;
                            if (decodeLen > 0 && _buffer[_pos + decodeLen - 1] == (byte)'\r')
                            {
                                decodeLen--;
                            }

                            string line = Encoding.ASCII.GetString(_buffer, _pos, decodeLen);
                            ConsumeBytes(consumeCount);
                            return line;
                        }
                    }

                    // 改行が見つからない場合は、残りを退避して追加読み込み。
                    if (available > 0)
                    {
                        pooledLine ??= System.Buffers.ArrayPool<byte>.Shared.Rent(maxLineBytes);

                        if (pooledLen + available > maxLineBytes)
                        {
                            throw new InvalidOperationException("APPERROR: HTTP line too long.");
                        }

                        Buffer.BlockCopy(_buffer, _pos, pooledLine, pooledLen, available);
                        pooledLen += available;
                        ConsumeBytes(available);
                    }

                    if (await ReadMoreAsync() == false)
                    {
                        return null;
                    }
                }
            }
            finally
            {
                if (pooledLine != null)
                {
                    System.Buffers.ArrayPool<byte>.Shared.Return(pooledLine);
                }
            }
        }

        /// <summary>
        /// 指定バイト数だけ読み捨てる。
        /// </summary>
        /// <param name="byteCount">読み捨てバイト数。</param>
        public async Task DiscardBytesAsync(int byteCount)
        {
            if (byteCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(byteCount));
            }

            int remaining = byteCount;

            while (remaining > 0)
            {
                int available = _len - _pos;
                if (available > 0)
                {
                    int take = Math.Min(available, remaining);
                    ConsumeBytes(take);
                    remaining -= take;
                    continue;
                }

                if (await ReadMoreAsync() == false)
                {
                    throw new InvalidOperationException("APPERROR: Connection closed while discarding body.");
                }
            }
        }

        /// <summary>
        /// 追加読み込みを行なう。
        /// </summary>
        /// <returns>読み込みできた場合 true。切断された場合 false。</returns>
        async Task<bool> ReadMoreAsync()
        {
            if (_pos > 0 && _pos < _len)
            {
                Buffer.BlockCopy(_buffer, _pos, _buffer, 0, _len - _pos);
                _len -= _pos;
                _pos = 0;
            }
            else if (_pos >= _len)
            {
                _pos = 0;
                _len = 0;
            }

            if (_len >= _buffer.Length)
            {
                throw new InvalidOperationException("APPERROR: Internal buffer full.");
            }

            int read = await _stream.ReadAsync(_buffer.AsMemory(_len, _buffer.Length - _len), _idleCts.Token);
            if (read <= 0)
            {
                return false;
            }

            _len += read;
            _idleCts.CancelAfter(_idleTimeout); // 受信できたのでタイムアウトをリセット
            return true;
        }

        /// <summary>
        /// 内部バッファから指定バイト数を消費する。
        /// </summary>
        /// <param name="count">消費バイト数。</param>
        void ConsumeBytes(int count)
        {
            _pos += count;
            if (_pos >= _len)
            {
                _pos = 0;
                _len = 0;
            }
        }

        /// <summary>
        /// 破棄処理。
        /// </summary>
        public void Dispose()
        {
            _idleCts.Dispose();
            System.Buffers.ArrayPool<byte>.Shared.Return(_buffer);
        }
    }
}
