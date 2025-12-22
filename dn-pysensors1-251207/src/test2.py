#!/usr/bin/env python3
import asyncio
import sys
import time
import datetime
import traceback
from typing import Optional
import logging

from bleak import BleakClient, BleakScanner, BleakError
from bleak.backends.device import BLEDevice

# ---- ログ設定：Bleak の内部ログも見えるようにする ----
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
)
logging.getLogger("bleak").setLevel(logging.DEBUG)

# --- BLE 関連定数（前回と同じ） ---

HR_SERVICE_UUID = "0000180d-0000-1000-8000-00805f9b34fb"
HR_MEASUREMENT_CHAR_UUID = "00002a37-0000-1000-8000-00805f9b34fb"

RECONNECT_INTERVAL = 5.0
CONNECT_TIMEOUT = 5.0
SCAN_TIMEOUT = 3.0

SMALL_LOOP_INTERVAL = 0.1
PRINT_INTERVAL = 1.0

TCP_HOST = "127.0.0.1"
TCP_PORT = 7001

# --- 共有状態 ---

CURRENT_BPM: Optional[int] = None
LAST_BPM_OK_TIME: int = 0

# ここを「アドレス文字列」ではなく BLEDevice に変更
POLAR_DEVICE: Optional[BLEDevice] = None


# --- HR Measurement のパースはそのまま ---

def parse_heart_rate_measurement(payload: bytes) -> int:
    if not payload:
        return 0
    flags = payload[0]
    use_uint16 = bool(flags & 0x01)
    if use_uint16:
        if len(payload) >= 3:
            return int.from_bytes(payload[1:3], byteorder="little")
        return 0
    else:
        if len(payload) >= 2:
            return int(payload[1])
        return 0


def heart_rate_notification_handler(sender, data: bytearray) -> None:
    global CURRENT_BPM, LAST_BPM_OK_TIME
    try:
        bpm = parse_heart_rate_measurement(bytes(data))
        if bpm > 0:
            CURRENT_BPM = bpm
            LAST_BPM_OK_TIME = int(time.time())
    except Exception as exc:
        print(f"[BLE] Error parsing HR data from {sender}: {exc}", flush=True)
        traceback.print_exc()


# --- Polar H10 の探索（BLEDevice を返すように変更） ---

async def find_polar_device(timeout: float = SCAN_TIMEOUT) -> Optional[BLEDevice]:
    print("[BLE] Scanning for Polar H10...", flush=True)

    def _filter(device: BLEDevice, advertisement_data) -> bool:
        name = (device.name or "").lower()
        if "polar" in name and "h10" in name:
            return True
        if name.startswith("polar h10"):
            return True
        return False

    try:
        device = await BleakScanner.find_device_by_filter(_filter, timeout=timeout)
    except Exception as exc:
        print(f"[BLE] Scan failed: {exc!r}", flush=True)
        traceback.print_exc()
        return None

    if device is None:
        print("[BLE] Polar H10 not found during scan.", flush=True)
        return None

    print(f"[BLE] Found device: name='{device.name}', address={device.address}", flush=True)
    return device


# --- BLE 大ループ / 小ループ ---

async def ble_main_loop() -> None:
    global POLAR_DEVICE
    while True:
        loop_start = time.time()
        try:
            # 1) デバイス未確定ならスキャン
            if POLAR_DEVICE is None:
                POLAR_DEVICE = await find_polar_device()
                if POLAR_DEVICE is None:
                    print(f"[BLE] Will retry scan in {RECONNECT_INTERVAL} seconds...", flush=True)
                    await asyncio.sleep(RECONNECT_INTERVAL)
                    continue

            print(f"[BLE] Trying to connect to {POLAR_DEVICE.address} ({POLAR_DEVICE.name})...", flush=True)

            # BleakClient に BLEDevice を渡す
            client_kwargs = {
                "timeout": CONNECT_TIMEOUT,
                # 必要であれば、ペアリングをここでやることも可能
                # "pair": True,
            }

            try:
                async with BleakClient(POLAR_DEVICE, **client_kwargs) as client:
                    print("[BLE] Connected to Polar H10.", flush=True)

                    try:
                        services = await client.get_services()
                        char = services.get_characteristic(HR_MEASUREMENT_CHAR_UUID)
                        if char is None:
                            print("[BLE] Heart Rate Measurement characteristic not found.", flush=True)
                            raise BleakError("Heart Rate Measurement characteristic not found")
                    except Exception as exc:
                        print(f"[BLE] Service discovery failed: {exc!r}", flush=True)
                        traceback.print_exc()
                        raise

                    await client.start_notify(HR_MEASUREMENT_CHAR_UUID, heart_rate_notification_handler)
                    print("[BLE] Notification started on Heart Rate Measurement.", flush=True)

                    last_print = 0.0
                    while True:
                        await asyncio.sleep(SMALL_LOOP_INTERVAL)

                        if not client.is_connected:
                            print("[BLE] Disconnected from device.", flush=True)
                            break

                        now = time.time()
                        if now - last_print >= PRINT_INTERVAL:
                            last_print = now
                            bpm = CURRENT_BPM if CURRENT_BPM is not None else 0
                            print(f"[BLE] CurrentBpm={bpm}, LastBpmOkTime={LAST_BPM_OK_TIME}", flush=True)

            except BleakError as exc:
                print(f"[BLE] BleakError in connection block: {exc!r}", flush=True)
                traceback.print_exc()
            except Exception as exc:
                print(f"[BLE] Unexpected exception in connection block: {exc!r}", flush=True)
                traceback.print_exc()

        except Exception as exc:
            print(f"[BLE] Outer loop exception: {exc!r}", flush=True)
            traceback.print_exc()

        POLAR_DEVICE = None
        elapsed = time.time() - loop_start
        sleep_time = max(0.0, RECONNECT_INTERVAL - elapsed)
        print(f"[BLE] Reconnecting in {sleep_time:.1f} seconds...", flush=True)
        await asyncio.sleep(sleep_time)


# --- TCP サーバー部分（前回とほぼ同じ） ---

async def handle_tcp_client(reader: asyncio.StreamReader, writer: asyncio.StreamWriter) -> None:
    global CURRENT_BPM, LAST_BPM_OK_TIME
    addr = writer.get_extra_info("peername")
    print(f"[TCP] Client connected: {addr}", flush=True)
    try:
        while True:
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
        # クライアント正常切断
        pass
    except Exception as exc:
        print(f"[TCP] Error while handling client {addr}: {exc!r}", flush=True)
        traceback.print_exc()
    finally:
        print(f"[TCP] Client disconnected: {addr}", flush=True)
        try:
            writer.close()
            if hasattr(writer, "wait_closed"):
                await writer.wait_closed()
        except Exception:
            pass


async def start_tcp_server() -> None:
    server = await asyncio.start_server(handle_tcp_client, host=TCP_HOST, port=TCP_PORT)
    sockets = server.sockets or []
    if sockets:
        addr_str = ", ".join(str(sock.getsockname()) for sock in sockets)
        print(f"[TCP] Listening on {addr_str}", flush=True)

    async with server:
        await server.serve_forever()


# --- エントリーポイント ---

async def main() -> None:
    ble_task = asyncio.create_task(ble_main_loop(), name="ble_main_loop")
    tcp_task = asyncio.create_task(start_tcp_server(), name="tcp_server")
    await asyncio.gather(ble_task, tcp_task)


def configure_event_loop() -> None:
    if sys.platform.startswith("win"):
        try:
            asyncio.set_event_loop_policy(asyncio.WindowsSelectorEventLoopPolicy())
        except AttributeError:
            pass


if __name__ == "__main__":
    configure_event_loop()
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        print("KeyboardInterrupt: exiting.", flush=True)
