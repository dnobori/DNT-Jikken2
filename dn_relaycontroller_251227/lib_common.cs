using System;
using System.Threading;

namespace dn_relaycontroller_251227;

/// <summary>
/// コンソール出力の共通処理。
/// </summary>
static class AppConsole
{
    /// <summary>
    /// ローカルタイムゾーンのタイムスタンプを付けて、コンソールに 1 行出力する。
    /// </summary>
    /// <param name="message">出力する英語メッセージ本文。</param>
    public static void WriteLine(string message)
    {
        string timeStamp = DateTimeOffset.Now.ToString("yyyy/MM/dd HH:mm:ss zzz");
        Console.WriteLine($"{timeStamp} {message}");
    }
}

/// <summary>
/// HTTP の /keep_lock への正常アクセスの記録・参照を行なうための共有状態。
/// </summary>
sealed class KeepLockState
{
    /// <summary>
    /// 直近の /keep_lock 正常アクセス時刻 (Environment.TickCount64 ミリ秒)。
    /// </summary>
    long _lastKeepLockAccessTickMs;

    /// <summary>
    /// /keep_lock の正常アクセスを現在時刻で記録する。
    /// </summary>
    public void MarkAccessNow()
    {
        long now = Environment.TickCount64;
        Interlocked.Exchange(ref _lastKeepLockAccessTickMs, now);
    }

    /// <summary>
    /// 直近の /keep_lock 正常アクセスが、指定時間幅以内に存在するか判定する。
    /// </summary>
    /// <param name="window">有効とみなす時間幅。</param>
    /// <returns>指定時間幅以内にアクセスがある場合は true。</returns>
    public bool WasAccessedWithin(TimeSpan window)
    {
        long last = Interlocked.Read(ref _lastKeepLockAccessTickMs);
        if (last == 0)
        {
            return false;
        }

        long now = Environment.TickCount64;
        long diff = now - last;
        if (diff < 0)
        {
            return false;
        }

        return diff <= (long)window.TotalMilliseconds;
    }
}

