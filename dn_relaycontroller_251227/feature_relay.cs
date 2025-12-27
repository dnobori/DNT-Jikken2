using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace dn_relaycontroller_251227;

/// <summary>
/// リレー制御スレッドと watchdog スレッドを起動・管理する。
/// </summary>
sealed class RelayWorker
{
    /// <summary>
    /// /keep_lock /unlock アクセス記録用の共有状態。
    /// </summary>
    readonly KeepLockState _keepLockState;

    /// <summary>
    /// ループが回った回数カウンタ (watchdog 用)。
    /// </summary>
    long _loopCounter;

    /// <summary>
    /// コンストラクタ。
    /// </summary>
    /// <param name="keepLockState">/keep_lock /unlock アクセス記録用の共有状態。</param>
    public RelayWorker(KeepLockState keepLockState)
    {
        _keepLockState = keepLockState ?? throw new ArgumentNullException(nameof(keepLockState));
    }

    /// <summary>
    /// リレー制御スレッドと watchdog スレッドを開始する。
    /// </summary>
    public void Start()
    {
        Thread relayThread = new(RelayLoopThreadProc)
        {
            IsBackground = false,
            Name = "RelayLoopThread",
        };

        Thread watchdogThread = new(WatchdogThreadProc)
        {
            IsBackground = true,
            Name = "RelayWatchdogThread",
        };

        relayThread.Start();
        watchdogThread.Start();
    }

    /// <summary>
    /// リレー制御のメインループスレッド。
    /// </summary>
    void RelayLoopThreadProc()
    {
        RelayDeviceController relay = new();

        // 初期化時に必ず OFF (リセット) を試行する。
        bool? lastConfirmedOn = null;
        if (relay.TrySetRelayState(isOn: false))
        {
            lastConfirmedOn = false;
            AppConsole.WriteLine("INFO: Relay state changed: OFF");
        }

        while (true)
        {
            try
            {
                Interlocked.Increment(ref _loopCounter);

                TimeSpan accessWindow = TimeSpan.FromSeconds(5);
                // (a) keep_lock / unlock の直近アクセス有無を確認する。
                bool hasRecentAccess = _keepLockState.WasAnyAccessWithin(accessWindow);
                bool shouldOn = hasRecentAccess && _keepLockState.WasKeepLockAccessedWithin(accessWindow);

                // (b-2) unlock が keep_lock より後なら強制 OFF。
                if (_keepLockState.IsUnlockAfterKeepLock())
                {
                    shouldOn = false;
                }

                // 0.1 秒ごとに状態を判定するが、実際の書き込みは状態変化時または未確定時に限定する。
                if (lastConfirmedOn == null || lastConfirmedOn.Value != shouldOn)
                {
                    if (relay.TrySetRelayState(shouldOn))
                    {
                        bool? prev = lastConfirmedOn;
                        lastConfirmedOn = shouldOn;

                        if (prev == null || prev.Value != shouldOn)
                        {
                            AppConsole.WriteLine($"INFO: Relay state changed: {(shouldOn ? "ON" : "OFF")}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppConsole.WriteLine($"APPERROR: Unhandled error in relay loop. Detail: {ex}");
                Thread.Sleep(TimeSpan.FromSeconds(0.9));
            }

            Thread.Sleep(TimeSpan.FromSeconds(0.1));
        }
    }

    /// <summary>
    /// watchdog timer スレッド。
    /// </summary>
    void WatchdogThreadProc()
    {
        long lastCounter = Interlocked.Read(ref _loopCounter);
        DateTimeOffset lastChanged = DateTimeOffset.UtcNow;

        while (true)
        {
            Thread.Sleep(TimeSpan.FromSeconds(1));

            long cur = Interlocked.Read(ref _loopCounter);
            if (cur != lastCounter)
            {
                lastCounter = cur;
                lastChanged = DateTimeOffset.UtcNow;
                continue;
            }

            if ((DateTimeOffset.UtcNow - lastChanged) >= TimeSpan.FromSeconds(30))
            {
                AppConsole.WriteLine("APPERROR: Relay loop thread seems frozen for 30 seconds. Forcing process termination.");

                // 直ちにプロセスを強制終了する。
                Environment.FailFast("APPERROR: Relay loop thread frozen.");
            }
        }
    }
}

/// <summary>
/// USB リレー装置を仮想シリアルデバイスとして制御する。
/// </summary>
sealed class RelayDeviceController
{
    /// <summary>
    /// /dev/serial/by-id/ の固定パス。
    /// </summary>
    const string SerialByIdDir = "/dev/serial/by-id/";

    /// <summary>
    /// 直近に解決したリレーデバイスパス (シンボリックリンク)。
    /// </summary>
    string? _cachedDevicePath;

    /// <summary>
    /// 指定状態にリレーを設定する (失敗時はリトライする)。
    /// </summary>
    /// <param name="isOn">ON にする場合 true。OFF の場合 false。</param>
    /// <returns>書き込みに成功した場合 true。</returns>
    public bool TrySetRelayState(bool isOn)
    {
        // Windows 等でのビルド互換性のため、実行環境が Linux 以外の場合は失敗扱いとする。
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) == false)
        {
            AppConsole.WriteLine("APPERROR: Relay control is supported only on Linux.");
            return false;
        }

        byte command = isOn ? (byte)'1' : (byte)'0';

        for (int attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                string? devicePath = GetOrFindDevicePath();
                if (string.IsNullOrEmpty(devicePath))
                {
                    AppConsole.WriteLine("APPERROR: USB-RELAY device not found under /dev/serial/by-id/.");
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                    continue;
                }

                // 念のため 3 回繰り返す。
                for (int i = 0; i < 3; i++)
                {
                    SendOneByte(devicePath, command);

                    // 繰り返すたびに 0.5 秒待機。
                    if (i < 2)
                    {
                        Thread.Sleep(TimeSpan.FromMilliseconds(500));
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                AppConsole.WriteLine($"APPERROR: Relay operation failed (attempt {attempt}/5). Detail: {ex}");
                _cachedDevicePath = null; // 次回は再探索させる
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
        }

        return false;
    }

    /// <summary>
    /// デバイスパスを取得する (必要に応じて再探索する)。
    /// </summary>
    /// <returns>デバイスパス。見つからない場合 null。</returns>
    string? GetOrFindDevicePath()
    {
        if (string.IsNullOrEmpty(_cachedDevicePath) == false && File.Exists(_cachedDevicePath))
        {
            return _cachedDevicePath;
        }

        _cachedDevicePath = FindRelayDevicePath();
        return _cachedDevicePath;
    }

    /// <summary>
    /// /dev/serial/by-id/ を列挙し、USB-RELAY を含むデバイスを辞書順最小で選択する。
    /// </summary>
    /// <returns>見つかった場合はデバイスパス。見つからない場合 null。</returns>
    static string? FindRelayDevicePath()
    {
        if (Directory.Exists(SerialByIdDir) == false)
        {
            return null;
        }

        string? bestPath = null;
        string? bestName = null;

        foreach (string path in Directory.EnumerateFileSystemEntries(SerialByIdDir))
        {
            string name = Path.GetFileName(path);

            if (name.Contains("USB-RELAY", StringComparison.Ordinal))
            {
                if (bestName == null || string.CompareOrdinal(name, bestName) < 0)
                {
                    bestName = name;
                    bestPath = path;
                }
            }
        }

        return bestPath;
    }

    /// <summary>
    /// シリアルデバイスを open → 1 バイト送付 → flush → 0.5 秒待機 → close を行なう。
    /// </summary>
    /// <param name="devicePath">デバイスファイルパス。</param>
    /// <param name="value">送付する 1 バイト。</param>
    static void SendOneByte(string devicePath, byte value)
    {
#if NET6_0_OR_GREATER
        // System.IO.Ports.SerialPort を使用する (stty/printf 等の外部プロセスは使用しない)。
        using System.IO.Ports.SerialPort port = new(devicePath)
        {
            BaudRate = 9600,
            DataBits = 8,
            Parity = System.IO.Ports.Parity.None,
            StopBits = System.IO.Ports.StopBits.One,
            Handshake = System.IO.Ports.Handshake.None,
            ReadTimeout = 60000,
            WriteTimeout = 60000,
            DtrEnable = false,
            RtsEnable = false,
        };

        port.Open();
        port.Write(new byte[] { value }, 0, 1);
        port.BaseStream.Flush();

        // 念のため 0.5 秒待機してから close する。
        Thread.Sleep(TimeSpan.FromMilliseconds(500));
        port.Close();
#else
        throw new PlatformNotSupportedException("APPERROR: .NET 6.0 or later is required.");
#endif
    }
}
