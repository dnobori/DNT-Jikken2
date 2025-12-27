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
/// HTTP の /keep_lock /unlock への正常アクセスの記録・参照を行なうための共有状態。
/// </summary>
sealed class KeepLockState
{
    /// <summary>
    /// 直近の /keep_lock 正常アクセス時刻 (Environment.TickCount64 ミリ秒)。
    /// </summary>
    long _lastKeepLockAccessTickMs;

    /// <summary>
    /// 直近の /unlock 正常アクセス時刻 (Environment.TickCount64 ミリ秒)。
    /// </summary>
    long _lastUnlockAccessTickMs;

    /// <summary>
    /// /keep_lock の正常アクセスを現在時刻で記録する。
    /// </summary>
    public void MarkKeepLockAccessNow()
    {
        long now = Environment.TickCount64;
        Interlocked.Exchange(ref _lastKeepLockAccessTickMs, now);
    }

    /// <summary>
    /// /unlock の正常アクセスを現在時刻で記録する。
    /// </summary>
    public void MarkUnlockAccessNow()
    {
        long now = Environment.TickCount64;
        Interlocked.Exchange(ref _lastUnlockAccessTickMs, now);
    }

    /// <summary>
    /// 直近の /keep_lock 正常アクセスが、指定時間幅以内に存在するか判定する。
    /// </summary>
    /// <param name="window">有効とみなす時間幅。</param>
    /// <returns>指定時間幅以内にアクセスがある場合は true。</returns>
    public bool WasKeepLockAccessedWithin(TimeSpan window)
    {
        long last = Interlocked.Read(ref _lastKeepLockAccessTickMs);
        long now = Environment.TickCount64;
        return IsWithinWindow(last, now, window);
    }

    /// <summary>
    /// 直近の /keep_lock または /unlock 正常アクセスが、指定時間幅以内に存在するか判定する。
    /// </summary>
    /// <param name="window">有効とみなす時間幅。</param>
    /// <returns>指定時間幅以内にアクセスがある場合は true。</returns>
    public bool WasAnyAccessWithin(TimeSpan window)
    {
        long lastKeepLock = Interlocked.Read(ref _lastKeepLockAccessTickMs);
        long lastUnlock = Interlocked.Read(ref _lastUnlockAccessTickMs);
        long now = Environment.TickCount64;

        if (IsWithinWindow(lastKeepLock, now, window))
        {
            return true;
        }

        return IsWithinWindow(lastUnlock, now, window);
    }

    /// <summary>
    /// 最後の /unlock アクセスが、最後の /keep_lock アクセスより後かどうか判定する。
    /// </summary>
    /// <returns>unlock が keep_lock より後の場合 true。</returns>
    public bool IsUnlockAfterKeepLock()
    {
        long lastKeepLock = Interlocked.Read(ref _lastKeepLockAccessTickMs);
        long lastUnlock = Interlocked.Read(ref _lastUnlockAccessTickMs);

        if (lastUnlock == 0)
        {
            return false;
        }

        return lastUnlock > lastKeepLock;
    }

    /// <summary>
    /// 指定時刻が現在時刻から指定時間幅以内か判定する。
    /// </summary>
    /// <param name="lastTickMs">基準時刻 (Environment.TickCount64 ミリ秒)。</param>
    /// <param name="nowTickMs">現在時刻 (Environment.TickCount64 ミリ秒)。</param>
    /// <param name="window">有効とみなす時間幅。</param>
    /// <returns>指定時間幅以内なら true。</returns>
    static bool IsWithinWindow(long lastTickMs, long nowTickMs, TimeSpan window)
    {
        if (lastTickMs == 0)
        {
            return false;
        }

        long diff = nowTickMs - lastTickMs;
        if (diff < 0)
        {
            return false;
        }

        return diff <= (long)window.TotalMilliseconds;
    }
}
