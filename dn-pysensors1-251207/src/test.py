#!/usr/bin/env python3
import asyncio
import sys
import time
import datetime
from typing import Optional

from bleak import BleakClient, BleakScanner, BleakError

# --- BLE 関連定数 ---

# Heart Rate Service / Measurement Characteristic (Bluetooth SIG 標準)
HR_SERVICE_UUID = "0000180d-0000-1000-8000-00805f9b34fb"
HR_MEASUREMENT_CHAR_UUID = "00002a37-0000-1000-8000-00805f9b34fb"

# 接続リトライ間隔（大ループ）
RECONNECT_INTERVAL = 5.0  # 秒
CONNECT_TIMEOUT = 5.0     # 秒 (BleakClient の接続タイムアウト)
SCAN_TIMEOUT = 3.0        # 秒 (デバイス探索タイムアウト)

# 小ループの周期 / コンソール出力周期
SMALL_LOOP_INTERVAL = 0.1  # 100 ms
PRINT_INTERVAL = 1.0       # 1000 ms

# TCP サーバー設定
TCP_HOST = "127.0.0.1"
TCP_PORT = 7001

# --- 共有状態 ---

CURRENT_BPM: Optional[int] = None    # 最新の BPM。未取得なら None
LAST_BPM_OK_TIME: int = 0           # 最後に取得できた UTC epoch 秒。未取得なら 0

POLAR_ADDRESS: Optional[str] = None  # 一度見つけた Polar H10 のアドレスをキャッシュ


# --- Heart Rate Measurement のパース ---

def parse_heart_rate_measurement(payload: bytes) -> int:
    """
    Heart Rate Measurement (0x2A37) のバイト列から BPM を取り出す。
    RR 間隔などはここでは使用しない。
    """
    if not payload:
        return 0

    flags = payload[0]
    use_uint16 = bool(flags & 0x01)

    if use_uint16:
        # 16bit BPM
        if len(payload) >= 3:
            return int.from_bytes(payload[1:3], byteorder="little")
        return 0
    else:
        # 8bit BPM
        if len(payload) >= 2:
            return int(payload[1])
        return 0


def heart_rate_notification_handler(sender: int, data: bytearray) -> None:
    """
    Polar H10 から Heart Rate Measurement 通知を受け取ったときに呼ばれる。
    グローバルな CurrentBpm / LastBpmOkTime を更新する。
    """
    global CURRENT_BPM, LAST_BPM_OK_TIME

    try:
        bpm = parse_heart_rate_measurement(bytes(data))
        if bpm > 0:
            CURRENT_BPM = bpm
            # time.time() は UTC エポック秒
            LAST_BPM_OK_TIME = int(time.time())
    except Exception as exc:
        print(f"[BLE] Error parsing HR data from {sender}: {exc}", flush=True)


# --- Polar H10 の探索 ---

async def find_polar_address(timeout: float = SCAN_TIMEOUT) -> Optional[str]:
    """
    「Polar」「H10」という文字列を含む名前の BLE デバイスをスキャンし、
    最初に見つかったもののアドレスを返す。
    見つからなければ None。
    """
    print("[BLE] Scanning for Polar H10...", flush=True)

    def _filter(device, advertisement_data) -> bool:
        name = (device.name or "").lower()
        if "polar" in name and "h10" in name:
            return True
        # "Polar H10 N" なども一応拾えるように
        if name.startswith("polar h10"):
            return True
        return False

    try:
        device = await BleakScanner.find_device_by_filter(_filter, timeout=timeout)
    except Exception as exc:
        print(f"[BLE] Scan failed: {exc}", flush=True)
        return None

    if device is None:
        print("[BLE] Polar H10 not found during scan.", flush=True)
        return None

    print(f"[BLE] Found device: name='{device.name}', address={device.address}", flush=True)
    return device.address


# --- メインの BLE 大ループ／小ループ ---

async def ble_main_loop() -> None:
    """
    要求仕様の「メインタスク」に相当。
    - 大ループ: Polar H10 への接続を 5 秒以上の間隔で繰り返し試行
    - 接続成功時: Heart Rate Measurement 通知を購読し、小さなメインループを回す
    """
    global POLAR_ADDRESS

    while True:
        loop_start = time.time()

        try:
            # 1) アドレス未確定ならスキャン
            if POLAR_ADDRESS is None:
                POLAR_ADDRESS = await find_polar_address()
                if POLAR_ADDRESS is None:
                    print(f"[BLE] Will retry scan in {RECONNECT_INTERVAL} seconds...", flush=True)
                    await asyncio.sleep(RECONNECT_INTERVAL)
                    continue

            print(f"[BLE] Trying to connect to {POLAR_ADDRESS}...", flush=True)

            try:
                # 接続タイムアウト 5 秒
                async with BleakClient(POLAR_ADDRESS, timeout=CONNECT_TIMEOUT) as client:
                    print("[BLE] Connected to Polar H10.", flush=True)

                    # Heart Rate Measurement characteristic を確認
                    try:
                        services = await client.get_services()
                        char = services.get_characteristic(HR_MEASUREMENT_CHAR_UUID)
                        if char is None:
                            print("[BLE] Heart Rate Measurement characteristic not found.", flush=True)
                            raise BleakError("Heart Rate Measurement characteristic not found")
                    except Exception as exc:
                        print(f"[BLE] Service discovery failed: {exc}", flush=True)
                        raise

                    # 通知を購読開始
                    await client.start_notify(HR_MEASUREMENT_CHAR_UUID, heart_rate_notification_handler)
                    print("[BLE] Notification started on Heart Rate Measurement.", flush=True)

                    # 小さなメインループ (100ms 周期)
                    last_print = 0.0
                    while True:
                        await asyncio.sleep(SMALL_LOOP_INTERVAL)

                        # 接続状態チェック
                        if not client.is_connected:
                            print("[BLE] Disconnected from device.", flush=True)
                            break

                        now = time.time()
                        if now - last_print >= PRINT_INTERVAL:
                            last_print = now
                            bpm = CURRENT_BPM if CURRENT_BPM is not None else 0
                            print(f"[BLE] CurrentBpm={bpm}, LastBpmOkTime={LAST_BPM_OK_TIME}", flush=True)

            except BleakError as exc:
                print(f"[BLE] BleakError: {exc}", flush=True)
            except Exception as exc:
                print(f"[BLE] Unexpected exception in connection block: {exc}", flush=True)

        except Exception as exc:
            # 大ループでの想定外例外
            print(f"[BLE] Outer loop exception: {exc}", flush=True)

        # 何らかの理由でここに来たら、再スキャンからやり直し
        POLAR_ADDRESS = None

        elapsed = time.time() - loop_start
        sleep_time = max(0.0, RECONNECT_INTERVAL - elapsed)
        print(f"[BLE] Reconnecting in {sleep_time:.1f} seconds...", flush=True)
        await asyncio.sleep(sleep_time)


# --- TCP サーバー (127.0.0.1:7001) ---

async def handle_tcp_client(reader: asyncio.StreamReader,
                            writer: asyncio.StreamWriter) -> None:
    """
    要求仕様の TCP サーバーの「1 クライアント処理」に相当。
    クライアントから 1 バイト受信するごとに、
    "CurrentBpm,YYYYMMDDHHMMSS\r\n" 形式の文字列を返す。
    LastBpmOkTime が 0 の場合は "0" を送る。
    """
    global CURRENT_BPM, LAST_BPM_OK_TIME

    addr = writer.get_extra_info("peername")
    print(f"[TCP] Client connected: {addr}", flush=True)
    try:
        while True:
            # 常に 1 バイト読む（接続が閉じられた場合は IncompleteReadError）
            data = await reader.readexactly(1)
            if not data:
                break

            bpm = CURRENT_BPM if CURRENT_BPM is not None else 0
            ts = LAST_BPM_OK_TIME

            if ts <= 0:
                ts_str = "0"
            else:
                dt = datetime.datetime.utcfromtimestamp(ts)
                ts_str = dt.strftime("%Y%m%d%H%M%S")

            line = f"{bpm},{ts_str}\r\n"
            writer.write(line.encode("ascii"))
            await writer.drain()

    except asyncio.IncompleteReadError:
        # クライアントが正常終了した場合
        pass
    except Exception as exc:
        print(f"[TCP] Error while handling client {addr}: {exc}", flush=True)
    finally:
        print(f"[TCP] Client disconnected: {addr}", flush=True)
        try:
            writer.close()
            await writer.wait_closed()
        except Exception:
            pass


async def start_tcp_server() -> None:
    """
    127.0.0.1:7001 に bind/listen し、複数クライアントの同時接続に対応する。
    """
    server = await asyncio.start_server(handle_tcp_client, host=TCP_HOST, port=TCP_PORT)
    sockets = server.sockets or []
    if sockets:
        addr_str = ", ".join(str(sock.getsockname()) for sock in sockets)
        print(f"[TCP] Listening on {addr_str}", flush=True)

    async with server:
        await server.serve_forever()


# --- エントリーポイント ---

async def main() -> None:
    """
    BLE 大ループタスクと TCP サーバータスクを同時に走らせる。
    """
    ble_task = asyncio.create_task(ble_main_loop(), name="ble_main_loop")
    tcp_task = asyncio.create_task(start_tcp_server(), name="tcp_server")

    # どちらかが終了しても例外として伝播するように gather
    await asyncio.gather(ble_task, tcp_task)


def configure_event_loop() -> None:
    """
    Windows での asyncio のイベントループポリシーを明示的に Selector にしておく。
    bleak の WinRT バックエンドとの相性を良くするためのおまじない。
    """
    if sys.platform.startswith("win"):
        try:
            asyncio.set_event_loop_policy(asyncio.WindowsSelectorEventLoopPolicy())
        except AttributeError:
            # 古い Python の場合はそのまま
            pass


if __name__ == "__main__":
    configure_event_loop()
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        # 明示的な終了機能は不要だが、Ctrl+C だけは一応受け付けておく
        print("KeyboardInterrupt: exiting.", flush=True)

