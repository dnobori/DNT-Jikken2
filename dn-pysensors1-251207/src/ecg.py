import asyncio
import sys
from typing import Optional, Tuple, Any

from bleak import BleakScanner, BleakClient
from bleakheart import HeartRate, PolarMeasurementData

# --- 設定 -------------------------------------------------------------

# スキャン／接続タイムアウト（秒）
CONNECT_TIMEOUT: float = 5.0

# 接続に失敗・切断後に再試行するまでの待ち時間（秒）
RETRY_INTERVAL: float = 5.0

# 「小さなメインループ」の周期（秒）
POLL_INTERVAL: float = 0.1  # 100 ms


async def find_polar_device() -> Optional[Any]:
    """
    Polar デバイス（名前に "polar" を含むもの）を 1 台スキャンして返す。
    成功: BLEDevice, 失敗: None
    """
    print(f"[SCAN] Searching for POLAR device (timeout={CONNECT_TIMEOUT}s)...", flush=True)

    try:
        device = await BleakScanner.find_device_by_filter(
            lambda dev, adv: dev.name and "polar" in dev.name.lower(),
            timeout=CONNECT_TIMEOUT,
        )
    except Exception as e:
        print(f"[SCAN] Error during scan: {e!r}", file=sys.stderr, flush=True)
        return None

    if device is None:
        print("[SCAN] POLAR device not found.", flush=True)
    else:
        print(f"[SCAN] Found device: {device.name} ({device.address})", flush=True)

    return device


async def small_loop(
    client: BleakClient,
    hr_queue: "asyncio.Queue[Tuple]",
    ecg_queue: "asyncio.Queue[Tuple]",
) -> None:
    """
    「小さなメインループ」: 100ms ごとに心拍・ECG をコンソールに表示し続ける。

    - 100ms のタイムスライスごとに hr_queue から心拍フレームを 1 つ取り出して表示
    - その間に溜まった ECG フレームをすべて捌き、直近の 1 フレームを要約表示
    - 例外が発生した場合はループを抜けて、外側の大ループで再接続を行う
    """
    print("[LOOP] Entering small loop (100 ms polling).", flush=True)

    while client.is_connected:
        try:
            # 100ms の間に心拍フレームを 1 つ待つ
            try:
                frame = await asyncio.wait_for(hr_queue.get(), timeout=POLL_INTERVAL)
            except asyncio.TimeoutError:
                frame = None

            if frame is not None:
                # HeartRate のフレーム形式 (unpack=True, instant_rate=True):
                # ('HR', tstamp_ns, (bpm, rr_ms), energy_kJ)
                try:
                    tag, tstamp, payload, energy = frame
                    if tag == "HR" and payload is not None:
                        bpm, rr_ms = payload
                        bpm_str = f"{bpm:.1f}" if not isinstance(bpm, int) else f"{bpm:d}"
                        rr_str = f"{rr_ms:.1f}"
                        print(
                            f"[HR ] {bpm_str:>6} bpm  RR={rr_str:>6} ms  t={tstamp}",
                            flush=True,
                        )
                    else:
                        # 想定外形式はそのまま出す
                        print(f"[HR ] Raw frame: {frame}", flush=True)
                except Exception as e:
                    print(f"[HR ] Failed to decode frame {frame!r}: {e}", file=sys.stderr, flush=True)
                finally:
                    hr_queue.task_done()

            # この 100ms 中に溜まった ECG フレームを全部取り出し、最後のものだけ表示
            last_ecg = None
            while not ecg_queue.empty():
                last_ecg = ecg_queue.get_nowait()
                ecg_queue.task_done()

            if last_ecg is not None:
                # ECG フレーム形式（ドキュメントより）:
                # ('ECG', tstamp_ns, [samples_in_microVolt])
                try:
                    tag, tstamp_ecg, samples = last_ecg
                    if tag == "ECG" and samples:
                        n = len(samples)
                        last_val = samples[-1]
                        # 先頭数サンプルを mV に換算して表示
                        preview_count = min(5, n)
                        preview = ", ".join(f"{s * 1e-3:.3f}" for s in samples[:preview_count])
                        print(
                            f"[ECG] t={tstamp_ecg}  {n:4d} samples "
                            f"(first {preview_count}: {preview} mV, last={last_val * 1e-3:.3f} mV)",
                            flush=True,
                        )
                    else:
                        print(f"[ECG] Raw frame: {last_ecg}", flush=True)
                except Exception as e:
                    print(f"[ECG] Failed to decode frame {last_ecg!r}: {e}", file=sys.stderr, flush=True)

            # ここまでで 100ms 経過している想定（wait_for の timeout ベース）
        except Exception as e:
            print(f"[LOOP] Error in small loop: {e!r}", file=sys.stderr, flush=True)
            break

    print("[LOOP] Leaving small loop (disconnected or error).", flush=True)


async def connect_and_stream(device: Any) -> None:
    """
    1 回の「接続～ストリーミング～切断」サイクルを実行する。

    - BleakClient で接続（タイムアウト 5 秒）
    - HeartRate / PolarMeasurementData を設定し、HR + ECG をキューへ
    - small_loop() を回してコンソール表示
    - 例外や切断があれば終了し、大ループ側に制御を返す
    """
    print(f"[CONN] Trying to connect to {device} ...", flush=True)

    # bleakheart にフレームを入れてもらうためのキュー
    hr_queue: asyncio.Queue = asyncio.Queue()
    ecg_queue: asyncio.Queue = asyncio.Queue()

    def _disconnected_callback(client: BleakClient) -> None:
        # デバイスが切断されたときに Bleak から呼ばれる
        print("[CONN] Sensor disconnected (disconnected_callback).", flush=True)

    try:
        async with BleakClient(
            device,
            disconnected_callback=_disconnected_callback,
            timeout=CONNECT_TIMEOUT,
        ) as client:
            print(
                f"[CONN] Connected: {client.is_connected}  "
                f"name={client.name}  addr={client.address}",
                flush=True,
            )
            if not client.is_connected:
                print("[CONN] client.is_connected is False, aborting this cycle.", flush=True)
                return

            # --- bleakheart オブジェクトの構築 ------------------------------
            # BLE 標準 HeartRate サービス
            heartrate = HeartRate(
                client,
                queue=hr_queue,
                instant_rate=False,
                unpack=True,
            )

            # Polar PMD API 経由の ECG
            pmd = PolarMeasurementData(client, ecg_queue=ecg_queue)

            # ECG の利用可能設定を一度問い合わせておく（130Hz, 14bit など）:contentReference[oaicite:7]{index=7}
            try:
                settings = await pmd.available_settings("ECG")
                print("[PMD] ECG settings reported by device:", flush=True)
                for k, v in settings.items():
                    print(f"       {k}: {v}", flush=True)
            except Exception as e:
                print(f"[PMD] Could not read ECG settings: {e!r}", file=sys.stderr, flush=True)

            # --- ストリーミング開始 ----------------------------------------
            try:
                await heartrate.start_notify()
                print("[HR ] Heart rate notifications started.", flush=True)
            except Exception as e:
                print(f"[HR ] Failed to start heart rate notifications: {e!r}", file=sys.stderr, flush=True)

            try:
                err_code, err_msg, _ = await pmd.start_streaming("ECG")
                if err_code != 0:
                    print(
                        f"[PMD] start_streaming('ECG') error {err_code}: {err_msg}",
                        file=sys.stderr,
                        flush=True,
                    )
                else:
                    print("[PMD] ECG streaming started.", flush=True)
            except Exception as e:
                print(f"[PMD] Failed to start ECG streaming: {e!r}", file=sys.stderr, flush=True)

            # --- 小さなメインループ ----------------------------------------
            await small_loop(client, hr_queue, ecg_queue)

            # --- 停止処理 ---------------------------------------------------
            if client.is_connected:
                print("[CONN] Stopping notifications / streams ...", flush=True)
                try:
                    await heartrate.stop_notify()
                except Exception as e:
                    print(f"[HR ] Error in stop_notify: {e!r}", file=sys.stderr, flush=True)

                try:
                    await pmd.stop_streaming("ECG")
                except Exception as e:
                    print(f"[PMD] Error in stop_streaming('ECG'): {e!r}", file=sys.stderr, flush=True)

            print("[CONN] Connection closed. Returning to outer loop.", flush=True)

    except Exception as e:
        # ここで例外が出てもプログラムは死なず、大ループに戻って再接続
        print(f"[CONN] Exception in connect_and_stream: {e!r}", file=sys.stderr, flush=True)


async def monitor_polar_forever() -> None:
    """
    「大ループ」: 永久に Polar H10 への接続を試み続ける。

    - 5 秒タイムアウトで Polar デバイスをスキャン
    - 見つからない／接続に失敗／通信中にエラー or 切断 → 5 秒待って再試行
    - いったん接続できたら connect_and_stream() が戻るまで待つ
    """
    print("[MAIN] Starting POLAR H10 monitor.", flush=True)

    while True:
        try:
            device = await find_polar_device()
            if device is None:
                print(f"[MAIN] Will retry scan in {RETRY_INTERVAL} seconds.", flush=True)
                await asyncio.sleep(RETRY_INTERVAL)
                continue

            # 1 回の接続～ストリーミングサイクル
            await connect_and_stream(device)

        except Exception as e:
            print(f"[MAIN] Exception in outer loop: {e!r}", file=sys.stderr, flush=True)

        # 正常終了／異常終了にかかわらず、5 秒待ってから再接続
        print(f"[MAIN] Reconnecting in {RETRY_INTERVAL} seconds ...", flush=True)
        await asyncio.sleep(RETRY_INTERVAL)


def main() -> None:
    """
    エントリーポイント。

    asyncio のイベントループ上で monitor_polar_forever() を動かし続ける。
    プログラム終了はユーザがプロセスを kill / Ctrl+C などで行う。
    """
    try:
        asyncio.run(monitor_polar_forever())
    except KeyboardInterrupt:
        # 明示的な終了処理は要求されていないが、Ctrl+C にも一応対応
        print("\n[MAIN] KeyboardInterrupt received, exiting.", flush=True)


if __name__ == "__main__":
    main()
