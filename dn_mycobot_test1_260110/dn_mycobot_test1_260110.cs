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
using System.Buffers;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using Mycobot.csharp;

namespace dn_mycobot_test1_260110;

/// <summary>
/// myCobot 280 M5 をキーボードで操作するコンソールアプリ。
/// </summary>
public class Program
{
    // 接続先の COM ポート設定
    const string SerialPortName = "COM7";
    const int SerialBaudRate = 115200;
    const int SerialOpenWaitMsecs = 5000;

    // 移動制御パラメータ
    const int MoveStepMm = 10;
    const int MoveSpeed = 50;
    const int MoveModeLinear = 1;
    const int PositionToleranceMm = 2;
    const int MoveTimeoutMsecs = 3000;
    const int PositionCheckIntervalMsecs = 200;

    // 終了要求フラグ
    static bool RequestExit;

    /// <summary>
    /// アプリのエントリポイント。ロボット接続とキー操作ループを開始する。
    /// </summary>
    /// <param name="args">起動引数。</param>
    public static void Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        Console.WriteLine("myCobot 280 M5 制御を開始します。");
        Console.WriteLine($"接続先COMポート: {SerialPortName}");

        MyCobot mc = new MyCobot(SerialPortName, SerialBaudRate);
        try
        {
            if (!mc.Open())
            {
                Console.WriteLine("接続に失敗しました。COMポート名や接続状態を確認してください。");
                Environment.ExitCode = 1;
                return;
            }

            Console.WriteLine("シリアルポートを開きました。安定化を待機します。");
            Thread.Sleep(SerialOpenWaitMsecs);

            if (!TryGetCurrentCoords(mc, out int[] currentCoords, showError: true))
            {
                Console.WriteLine("現在座標の取得に失敗しました。");
                Environment.ExitCode = 1;
                return;
            }

            Console.WriteLine($"現在座標: {FormatCoords(currentCoords)}");
            WriteUsage();

            Console.CancelKeyPress += OnCancelKeyPress;

            while (!RequestExit)
            {
                ConsoleKeyInfo keyInfo = Console.ReadKey(true);

                if (keyInfo.Key == ConsoleKey.Escape)
                {
                    RequestExit = true;
                    continue;
                }

                int deltaX = 0;
                int deltaY = 0;
                int deltaZ = 0;
                bool isMoveKey = true;

                switch (keyInfo.Key)
                {
                    case ConsoleKey.W:
                        deltaX = MoveStepMm;
                        break;
                    case ConsoleKey.S:
                        deltaX = -MoveStepMm;
                        break;
                    case ConsoleKey.A:
                        deltaY = MoveStepMm;
                        break;
                    case ConsoleKey.D:
                        deltaY = -MoveStepMm;
                        break;
                    case ConsoleKey.Q:
                        deltaZ = MoveStepMm;
                        break;
                    case ConsoleKey.E:
                        deltaZ = -MoveStepMm;
                        break;
                    default:
                        isMoveKey = false;
                        break;
                }

                if (!isMoveKey)
                    continue;

                if (!TryMoveByDelta(mc, deltaX, deltaY, deltaZ))
                    Console.WriteLine("エラー: 移動処理に失敗しました。");
            }
        }
        finally
        {
            mc.Dispose();
            Console.WriteLine("終了処理を完了しました。");
        }
    }

    /// <summary>
    /// Ctrl+C 等の割り込み時に終了フラグを立てる。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">割り込みイベント情報。</param>
    static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        RequestExit = true;
    }

    /// <summary>
    /// 操作方法をコンソールに表示する。
    /// </summary>
    static void WriteUsage()
    {
        Console.WriteLine("操作キー: W=前進(+X) / S=後退(-X) / A=左(+Y) / D=右(-Y) / Q=上(+Z) / E=下(-Z) / ESC=終了");
    }

    /// <summary>
    /// 現在座標を基準に相対移動を指示し、到達確認を行なう。
    /// </summary>
    /// <param name="mc">制御対象のロボット。</param>
    /// <param name="deltaX">X 軸移動量 (mm)。</param>
    /// <param name="deltaY">Y 軸移動量 (mm)。</param>
    /// <param name="deltaZ">Z 軸移動量 (mm)。</param>
    /// <returns>移動完了が確認できた場合は true。</returns>
    static bool TryMoveByDelta(MyCobot mc, int deltaX, int deltaY, int deltaZ)
    {
        if (!TryGetCurrentCoords(mc, out int[] currentCoords, showError: true))
            return false;

        int[] targetCoords = new int[currentCoords.Length];
        Array.Copy(currentCoords, targetCoords, currentCoords.Length);
        targetCoords[0] += deltaX;
        targetCoords[1] += deltaY;
        targetCoords[2] += deltaZ;

        Console.WriteLine($"移動指令: {FormatCoords(targetCoords)}");

        mc.SendCoords(ToDoubleCoords(targetCoords), MoveSpeed, MoveModeLinear);

        if (TryWaitForTargetPosition(mc, targetCoords, out int[] actualCoords))
        {
            Console.WriteLine($"到達: {FormatCoords(actualCoords)}");
            return true;
        }

        if (actualCoords.Length == 6)
        {
            Console.WriteLine($"エラー: 指定位置に到達できませんでした (可動範囲外の可能性)。実座標: {FormatCoords(actualCoords)}");
        }
        else
        {
            Console.WriteLine("エラー: 指定位置に到達できませんでした (座標取得にも失敗)。");
        }

        return false;
    }

    /// <summary>
    /// 現在座標を取得する。
    /// </summary>
    /// <param name="mc">制御対象のロボット。</param>
    /// <param name="coords">取得した座標 (mm/度)。</param>
    /// <param name="showError">エラー表示を行なうかどうか。</param>
    /// <returns>取得に成功した場合は true。</returns>
    static bool TryGetCurrentCoords(MyCobot mc, out int[] coords, bool showError)
    {
        coords = Array.Empty<int>();

        try
        {
            int[] current = mc.GetCoords();
            if (current.Length != 6)
            {
                if (showError)
                    Console.WriteLine("エラー: 座標の取得結果が不正です。");
                return false;
            }

            coords = current;
            return true;
        }
        catch (Exception ex)
        {
            if (showError)
                Console.WriteLine($"エラー: 座標取得時に例外が発生しました。詳細: {ex}");
            return false;
        }
    }

    /// <summary>
    /// 目標座標に到達するまで一定時間監視する。
    /// </summary>
    /// <param name="mc">制御対象のロボット。</param>
    /// <param name="targetCoords">目標座標。</param>
    /// <param name="actualCoords">最終的に取得できた実座標。</param>
    /// <returns>目標到達が確認できた場合は true。</returns>
    static bool TryWaitForTargetPosition(MyCobot mc, int[] targetCoords, out int[] actualCoords)
    {
        actualCoords = Array.Empty<int>();
        long startTick = Environment.TickCount64;

        while (Environment.TickCount64 - startTick < MoveTimeoutMsecs)
        {
            Thread.Sleep(PositionCheckIntervalMsecs);

            if (!TryGetCurrentCoords(mc, out int[] currentCoords, showError: false))
                continue;

            actualCoords = currentCoords;

            if (IsPositionNear(currentCoords, targetCoords, PositionToleranceMm))
                return true;
        }

        return false;
    }

    /// <summary>
    /// XYZ 位置が指定誤差内に収まっているかどうか判定する。
    /// </summary>
    /// <param name="currentCoords">現在座標。</param>
    /// <param name="targetCoords">目標座標。</param>
    /// <param name="toleranceMm">許容誤差 (mm)。</param>
    /// <returns>誤差内であれば true。</returns>
    static bool IsPositionNear(int[] currentCoords, int[] targetCoords, int toleranceMm)
    {
        if (currentCoords.Length < 3 || targetCoords.Length < 3)
            return false;

        for (int i = 0; i < 3; i++)
        {
            if (Math.Abs(currentCoords[i] - targetCoords[i]) > toleranceMm)
                return false;
        }

        return true;
    }

    /// <summary>
    /// 座標配列を double 配列に変換する。
    /// </summary>
    /// <param name="coords">変換元の座標。</param>
    /// <returns>double 配列の座標。</returns>
    static double[] ToDoubleCoords(int[] coords)
    {
        double[] result = new double[coords.Length];
        for (int i = 0; i < coords.Length; i++)
            result[i] = coords[i];
        return result;
    }

    /// <summary>
    /// 座標配列を表示用文字列に整形する。
    /// </summary>
    /// <param name="coords">座標配列。</param>
    /// <returns>表示用文字列。</returns>
    static string FormatCoords(int[] coords)
    {
        if (coords.Length < 6)
            return "X=?, Y=?, Z=?, Rx=?, Ry=?, Rz=?";

        return $"X={coords[0]}mm, Y={coords[1]}mm, Z={coords[2]}mm, Rx={coords[3]}deg, Ry={coords[4]}deg, Rz={coords[5]}deg";
    }
}

#endif


