// A Program Code
// 
// Copyright (c) 2019- IPA CyberLab.
// Copyright (c) 2003-2018 Daiyuu Nobori.
// Copyright (c) 2013-2018 SoftEther VPN Project, University of Tsukuba, Japan.
// All Rights Reserved.
// 
// License: The Apache License, Version 2.0
// https://www.apache.org/licenses/LICENSE-2.0
// 
// THIS SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
// 
// THIS SOFTWARE IS DEVELOPED IN JAPAN, AND DISTRIBUTED FROM JAPAN, UNDER
// JAPANESE LAWS. YOU MUST AGREE IN ADVANCE TO USE, COPY, MODIFY, MERGE, PUBLISH,
// DISTRIBUTE, SUBLICENSE, AND/OR SELL COPIES OF THIS SOFTWARE, THAT ANY
// JURIDICAL DISPUTES WHICH ARE CONCERNED TO THIS SOFTWARE OR ITS CONTENTS,
// AGAINST US (IPA CYBERLAB, DAIYUU NOBORI, SOFTETHER VPN PROJECT OR OTHER
// SUPPLIERS), OR ANY JURIDICAL DISPUTES AGAINST US WHICH ARE CAUSED BY ANY KIND
// OF USING, COPYING, MODIFYING, MERGING, PUBLISHING, DISTRIBUTING, SUBLICENSING,
// AND/OR SELLING COPIES OF THIS SOFTWARE SHALL BE REGARDED AS BE CONSTRUED AND
// CONTROLLED BY JAPANESE LAWS, AND YOU MUST FURTHER CONSENT TO EXCLUSIVE
// JURISDICTION AND VENUE IN THE COURTS SITTING IN TOKYO, JAPAN. YOU MUST WAIVE
// ALL DEFENSES OF LACK OF PERSONAL JURISDICTION AND FORUM NON CONVENIENS.
// PROCESS MAY BE SERVED ON EITHER PARTY IN THE MANNER AUTHORIZED BY APPLICABLE
// LAW OR COURT RULE.

// Author: Daiyuu Nobori

#if true

#pragma warning disable CA2235 // Mark all non-serializable fields

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace dn_relaycontroller_251227;

/// <summary>
/// 本プログラムのエントリポイントクラス。
/// </summary>
public class Program
{
    /// <summary>
    /// エントリポイント。
    /// </summary>
    /// <param name="args">コマンドライン引数。</param>
    public static async Task Main(string[] args)
    {
        // 旧来エンコーディング対応 (Shift-JIS 等) を有効化する。
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        using CancellationTokenSource shutdownCts = new();

        // Ctrl+C で終了要求を受け取る。
        Console.CancelKeyPress += (_, e) =>
        {
            AppConsole.WriteLine("INFO: Ctrl+C received. Shutting down...");
            shutdownCts.Cancel();
        };

        KeepLockState keepLockState = new();

        // HTTP サーバーの bind/listen 試行 (失敗時は 10 秒ごとにリトライ)。
        TcpListener? listener = null;
        while (listener == null && shutdownCts.IsCancellationRequested == false)
        {
            try
            {
                listener = new TcpListener(IPAddress.Any, 7001);
                listener.Start();
                AppConsole.WriteLine("INFO: HTTP server listening on 0.0.0.0:7001");
            }
            catch (Exception ex)
            {
                AppConsole.WriteLine($"APPERROR: Failed to bind/listen on 0.0.0.0:7001. Detail: {ex}");
                listener = null;

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), shutdownCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        if (listener == null)
        {
            return;
        }

        // HTTP サーバー受け付けループ開始 (同時に多数接続を並列処理)。
        SimpleHttpServer httpServer = new(listener, keepLockState, TimeSpan.FromSeconds(60));
        Task httpServerTask = httpServer.RunAsync(shutdownCts.Token);

        // リレー制御スレッドと watchdog スレッド開始。
        RelayWorker relayWorker = new(keepLockState);
        relayWorker.Start();

        // プロセスを勝手に終了させないため、終了要求があるまで待機する。
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, shutdownCts.Token);
        }
        catch (OperationCanceledException)
        {
        }

        // HTTP サーバーを停止する。
        httpServer.Stop();

        try
        {
            await httpServerTask;
        }
        catch (Exception ex)
        {
            AppConsole.WriteLine($"APPERROR: HTTP server task ended with an error. Detail: {ex}");
        }
    }
}

#endif


