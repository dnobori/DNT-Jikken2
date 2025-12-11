#!/usr/bin/env python3
"""
witmotion_sensor_test.py

WITMOTION BLE 5.0 IMU (WT901BLECL / WT9011DCL) センサから
リアルタイムにデータを取得してコンソールに表示するテスト用スクリプト。

前提:
  * Ubuntu 24.04 + Python 3.11
  * pip install bleak
  * Linux 側で Bluetooth アダプタが有効になっていること
  * センサの電源が入り、通常通りアドバタイズされていること

このスクリプトは:
  * 起動時にスキャン専用スレッド (scan task) を 1 本起動する
  * スキャンで見つかった WT901BLE 系デバイスごとに 1 本のスレッド (device task) を起動する
  * それぞれのスレッドは例外が出ても終了せず、再試行し続ける
  * プログラム自体に終了処理は実装しない (プロセス終了はユーザーが行う前提)

WITMOTION BLE 5.0 プロトコル仕様 (公式ドキュメント) に基づき、
通知パケット 0x55 0x61 (加速度 + 角速度 + 姿勢角) をデコードして出力する。
"""

import asyncio
import datetime as _dt
import random
import sys
import threading
import time
import traceback
import atexit
import subprocess
import signal

from typing import Dict, Optional

from bleak import BleakClient, BleakScanner

# ---------------------------------------------------------------------------
# 定数設定
# ---------------------------------------------------------------------------

# スキャン間隔 / 接続再試行間隔 (秒)
BASE_INTERVAL_SEC = 5.0
JITTER_RATIO = 0.3  # ±30%

# 接続タイムアウト (秒)
CONNECT_TIMEOUT_SEC = 5.0

# WITMOTION BLE 5.0 固有 UUID
# 参考 (実際に WT901BLECL/WT9011DCL で使われている UUID) 
#   - Service UUID: 0000ffe5-0000-1000-8000-00805f9a34fb
#   - Notify Characteristic: 0000ffe4-0000-1000-8000-00805f9a34fb
#   - Write Characteristic:  0000ffe9-0000-1000-8000-00805f9a34fb
WITMOTION_SERVICE_UUID = "0000ffe5-0000-1000-8000-00805f9a34fb"
WITMOTION_NOTIFY_CHAR_UUID = "0000ffe4-0000-1000-8000-00805f9a34fb"
WITMOTION_WRITE_CHAR_UUID = "0000ffe9-0000-1000-8000-00805f9a34fb"

# デバイス名のプレフィックス
# WT901BLECL / WT9011DCL は Bluetooth 名が "WT901BLE+番号" になる旨がマニュアルに記載されている 
DEVICE_NAME_PREFIX = "WT901BLE"




# ---------------------------------------------------------------------------
# bluetoothctl を使った接続クリーンアップ
# ---------------------------------------------------------------------------

def _bluetoothctl_cmd(*args: str) -> str:
    """
    bluetoothctl を 1 回だけ起動してコマンドを実行し、
    標準出力を文字列で返すユーティリティ。

    失敗した場合は例外を投げず、標準エラーに出力して空文字列を返す。
    """
    try:
        cp = subprocess.run(
            ["bluetoothctl", *args],
            check=False,
            capture_output=True,
            text=True,
        )
        # デバッグしたければ cp.stderr も表示してよい
        return cp.stdout
    except FileNotFoundError:
        # bluetoothctl 自体が無い環境
        print(
            "[bluetoothctl] コマンドが見つかりません。Bluetooth クリーンアップをスキップします。",
            file=sys.stderr,
        )
        return ""
    except Exception as e:
        print(f"[bluetoothctl] 実行中に例外発生: {e!r}", file=sys.stderr)
        traceback.print_exc()
        return ""


def force_disconnect_witmotion_devices() -> None:
    """
    BlueZ に登録されている WT901BLE 系デバイスを列挙し、
    すべて bluetoothctl disconnect しておく。

    - プログラム起動直後: 前回の実行で残った接続をクリーンアップ
    - プログラム終了時  : 今回の実行で張った接続をクリーンアップ

    どちらで呼んでも副作用は「接続が切れるだけ」なので安全。
    """
    out = _bluetoothctl_cmd("devices")
    if not out:
        return

    targets = []  # [(mac, name), ...]
    for line in out.splitlines():
        line = line.strip()
        if not line.startswith("Device "):
            continue
        parts = line.split()
        if len(parts) < 3:
            continue
        mac = parts[1]
        name = " ".join(parts[2:])
        if name.startswith(DEVICE_NAME_PREFIX):
            targets.append((mac, name))

    if not targets:
        return

    print("bluetoothctl で WT901BLE 系デバイスの接続をクリーンアップします...")
    for mac, name in targets:
        print(f"  -> disconnect {mac} ({name})")
        _bluetoothctl_cmd("disconnect", mac)



# ---------------------------------------------------------------------------
# ユーティリティ
# ---------------------------------------------------------------------------

def jittered_interval(base: float = BASE_INTERVAL_SEC) -> float:
    """
    5 秒 ± 30% のような乱数付きインターバルを返す。
    """
    r = random.uniform(1.0 - JITTER_RATIO, 1.0 + JITTER_RATIO)
    return max(0.1, base * r)


def utc_now_iso_ms() -> str:
    """
    現在の UTC 時刻を ISO8601 文字列 (ミリ秒単位) で返す。
    センサー内部の RTC ではなく、Linux ホスト側のシステム時刻を使用する。
    """
    now = _dt.datetime.now(_dt.timezone.utc)
    # Python 3.11 なら timespec="milliseconds" を使える
    return now.isoformat(timespec="milliseconds")


def format_sample_line(
    mac: str,
    timestamp: str,
    payload: Dict[str, float],
) -> str:
    """
    1 サンプルを 1 行の JSON 風テキストとして整形する。
    形式:
      [MACアドレス] [ISO8601 UTC タイムスタンプ] {"key": value, ...}
    """
    kv_parts = []
    for k in sorted(payload.keys()):
        v = payload[k]
        # 小数点以下 6 桁程度あれば十分
        kv_parts.append(f'"{k}": {v:.6f}')
    payload_str = "{%s}" % ", ".join(kv_parts)
    return f"[{mac}] {timestamp} {payload_str}"


def decode_imu_packet(data: bytes) -> Optional[Dict[str, float]]:
    """
    WITMOTION BLE 5.0 プロトコルのデフォルト IMU パケット (Flag=0x61) をデコードする。

    パケット形式 (20 bytes) は公式 BLE5.0 プロトコル仕様に従う :
      0: 0x55  (ヘッダ)
      1: 0x61  (Flag: 加速度+角速度+角度)
      2-3:  ax (Int16, LSB first)
      4-5:  ay
      6-7:  az
      8-9:  wx
      10-11: wy
      12-13: wz
      14-15: roll
      16-17: pitch
      18-19: yaw

    物理量への変換も公式ドキュメントの式に準拠する。
    """
    if len(data) < 20:
        return None
    if data[0] != 0x55:
        return None
    flag = data[1]
    if flag != 0x61:
        # 他のフラグ (0x71 など) はここでは無視する
        return None

    # 2 バイトずつ Int16 little-endian でデコード
    raw_values = []
    for i in range(2, 20, 2):
        # signed=True で -32768..32767
        raw = int.from_bytes(data[i : i + 2], byteorder="little", signed=True)
        raw_values.append(raw)

    if len(raw_values) != 9:
        return None

    ax_raw, ay_raw, az_raw, wx_raw, wy_raw, wz_raw, roll_raw, pitch_raw, yaw_raw = raw_values

    # 単位変換
    # ax,ay,az: g -> m/s^2 (g=9.8)
    g = 9.8
    ax = ax_raw / 32768.0 * 16.0 * g
    ay = ay_raw / 32768.0 * 16.0 * g
    az = az_raw / 32768.0 * 16.0 * g

    # wx,wy,wz: ±2000 deg/s
    wx = wx_raw / 32768.0 * 2000.0
    wy = wy_raw / 32768.0 * 2000.0
    wz = wz_raw / 32768.0 * 2000.0

    # roll,pitch,yaw: ±180 deg
    roll = roll_raw / 32768.0 * 180.0
    pitch = pitch_raw / 32768.0 * 180.0
    yaw = yaw_raw / 32768.0 * 180.0

    return {
        "ax_m_s2": ax,
        "ay_m_s2": ay,
        "az_m_s2": az,
        "wx_deg_s": wx,
        "wy_deg_s": wy,
        "wz_deg_s": wz,
        "roll_deg": roll,
        "pitch_deg": pitch,
        "yaw_deg": yaw,
    }



# ---------------------------------------------------------------------------
# 出力周波数設定 (WIT 標準プロトコル)
# ---------------------------------------------------------------------------

# WT901BLECL / WT9011DCL マニュアルに記載されている Set Return Rate:
#   FF AA 03 RATE 00
#   RATE:
#     0x06: 10Hz (default)
#     0x07: 20Hz
#     0x08: 50Hz
#     0x09: 100Hz
#     0x0A: 200Hz
RATE_CODE_TABLE: Dict[int, int] = {
    10: 0x06,
    20: 0x07,
    50: 0x08,
    100: 0x09,
    200: 0x0A,
}

# 全センサに適用したいターゲット周波数（ここを変えれば一括変更できる）
DEFAULT_TARGET_HZ = 50  # 例: 100Hz。50Hz にしたければ 50 にする。


async def configure_output_rate(client: BleakClient, mac: str,
                                target_hz: int = DEFAULT_TARGET_HZ) -> None:
    """
    センサ内部の「Return rate (出力周波数)」を設定する。

    プロトコル (WT901BLECL / WT9011DCL 系):
      Unlock        : FF AA 69 88 B5
      Set rate      : FF AA 03 RATE 00
      Save settings : FF AA 00 00 00

    Unlock -> Set rate -> Save の順で送る。
    """
    code = RATE_CODE_TABLE.get(target_hz)
    if code is None:
        print(f"[{mac}] 指定した出力レート {target_hz} Hz には対応していません", file=sys.stderr)
        return

    try:
        print(f"[{mac}] 出力レートを {target_hz} Hz に設定します")

        # 1. Unlock（念のため。Zenn の記事などでは省略しても動いていますが、
        #    公式 WIT 標準プロトコルでは unlock -> 設定 -> save が推奨）
        unlock_cmd = bytes([0xFF, 0xAA, 0x69, 0x88, 0xB5])
        await client.write_gatt_char(WITMOTION_WRITE_CHAR_UUID, unlock_cmd, response=True)
        await asyncio.sleep(0.05)

        # 2. Return rate 設定
        rate_cmd = bytes([0xFF, 0xAA, 0x03, code, 0x00])
        await client.write_gatt_char(WITMOTION_WRITE_CHAR_UUID, rate_cmd, response=True)
        await asyncio.sleep(0.05)

        # 3. Save（電源を切っても設定を保持したいなら有効。
        #    毎回書き込みたくないなら、このブロックをコメントアウトしてもよい）
        save_cmd = bytes([0xFF, 0xAA, 0x00, 0x00, 0x00])
        await client.write_gatt_char(WITMOTION_WRITE_CHAR_UUID, save_cmd, response=True)
        await asyncio.sleep(0.05)

        print(f"[{mac}] 出力レート設定完了 ({target_hz} Hz)")
    except Exception as e:
        print(f"[{mac}] 出力レート設定中に例外発生: {e!r}", file=sys.stderr)
        traceback.print_exc()



# ---------------------------------------------------------------------------
# デバイス管理
# ---------------------------------------------------------------------------

class DeviceRegistry:
    """
    すでにデバイスタスクを起動した MAC アドレスの集合を管理するクラス。
    新しいデバイスを見つけたときに、二重でスレッドを起動しないために使用する。
    """

    def __init__(self) -> None:
        self._lock = threading.Lock()
        self._known_devices: Dict[str, threading.Thread] = {}

    def has_device(self, mac: str) -> bool:
        mac = mac.upper()
        with self._lock:
            return mac in self._known_devices

    def register_device(self, mac: str, thread: threading.Thread) -> None:
        mac = mac.upper()
        with self._lock:
            self._known_devices[mac] = thread

    def get_device_threads(self) -> Dict[str, threading.Thread]:
        with self._lock:
            return dict(self._known_devices)


# ---------------------------------------------------------------------------
# デバイスタスク (各デバイスごとに 1 本)
# ---------------------------------------------------------------------------

async def device_session(address: str, name: Optional[str]) -> None:
    """
    1 つの WT901BLE 系デバイスに対するメイン処理 (非同期)。

    大ループ:
      - 接続を試行し続ける
      - 失敗しても例外を握りつぶして一定時間待って再試行

    小ループ:
      - BleakClient で接続が成功した後、
        Notify キャラクタリスティックからの通知を受け取り続ける
      - データをデコードし、1 サンプルごとにコンソールに表示
    """
    mac = address.upper()
    device_label = f"{name or 'WT901BLE'} ({mac})"

    while True:
        delay = jittered_interval(BASE_INTERVAL_SEC)
        try:
            print(f"[{mac}] 接続試行開始 (timeout={CONNECT_TIMEOUT_SEC}s) name={name!r}")
            # timeout を指定して接続を試みる
            async with BleakClient(address, timeout=CONNECT_TIMEOUT_SEC) as client:
                # 接続直後に状態を確認
                if not client.is_connected:
                    raise RuntimeError("BleakClient.is_connected が False です")

                print(f"[{mac}] 接続成功: {device_label}")

                # 接続直後に出力周波数を変更 (例: DEFAULT_TARGET_HZ = 100 なら 100Hz)
                await configure_output_rate(client, mac, target_hz=DEFAULT_TARGET_HZ)

                # 小ループ: 通知購読と受信処理
                await run_notification_loop(client, mac, device_label)
        except asyncio.CancelledError:
            # 将来的にキャンセル制御したくなった場合のために re-raise
            raise
        except Exception as e:
            # 例外内容を出力してから大ループ先頭へ戻る
            print(f"[{mac}] 接続または受信中に例外発生: {e!r}", file=sys.stderr)
            traceback.print_exc()

        # 接続が切れた / 失敗した場合は、次の接続試行までランダム待ち
        print(f"[{mac}] 再接続まで {delay:.1f} 秒待機します")
        try:
            await asyncio.sleep(delay)
        except asyncio.CancelledError:
            raise


async def run_notification_loop(client: BleakClient, mac: str, device_label: str) -> None:
    """
    接続済みクライアントに対して Notify を開始し、
    切断されるまでデータ受信を続ける。
    """
    latest_error: Optional[BaseException] = None

    def handle_notification(sender: int, data: bytes) -> None:
        """
        Notify キャラクタリスティックから 1 パケット受信するごとに呼ばれるコールバック。

        ここでは:
          * パケットをデコード
          * ホスト側 UTC 時刻でタイムスタンプを付与
          * 1 行のテキストとしてコンソールに即時出力
        """
        nonlocal latest_error
        try:
            sample = decode_imu_packet(data)
            if sample is None:
                # Flag != 0x61 など、IMU データでなければ無視
                return
            ts = utc_now_iso_ms()
            line = format_sample_line(mac, ts, sample)
            print(line, flush=True)
        except Exception as ex:
            # decode 中の例外が出てもループを止めない
            latest_error = ex
            print(f"[{mac}] 通知処理中に例外発生: {ex!r}", file=sys.stderr)
            traceback.print_exc()

    # Notify を開始
    print(f"[{mac}] Notify 開始: char={WITMOTION_NOTIFY_CHAR_UUID}")
    await client.start_notify(WITMOTION_NOTIFY_CHAR_UUID, handle_notification)

    try:
        # 接続が維持されている間、ひたすら待ち続ける
        # (Notify の処理はコールバック内で行う)
        while True:
            if not client.is_connected:
                print(f"[{mac}] 接続が切断されました (is_connected=False)")
                break
            await asyncio.sleep(1.0)

            # もし decode 中に例外が発生して latest_error に入っていれば表示だけしてクリア
            if latest_error is not None:
                print(f"[{mac}] 直近の通知処理例外: {latest_error!r}", file=sys.stderr)
                latest_error = None

    finally:
        # 切断時に Notify を停止しておく (失敗しても無視)
        try:
            print(f"[{mac}] Notify 停止")
            await client.stop_notify(WITMOTION_NOTIFY_CHAR_UUID)
        except Exception:
            pass


def device_task_thread(address: str, name: Optional[str]) -> None:
    """
    デバイスタスク用のスレッドエントリポイント。
    各スレッドは独自の asyncio イベントループを持ち、device_session をずっと回し続ける。
    """
    # このスレッド内専用のイベントループで非同期処理をまわす
    try:
        asyncio.run(device_session(address, name))
    except KeyboardInterrupt:
        # メインスレッド以外で KeyboardInterrupt が飛んでくることは稀だが、
        # 念のため握りつぶしてスレッドを終わらせる。
        print(f"[{address}] device_task_thread: KeyboardInterrupt 受信、スレッド終了")
    except Exception as e:
        # asyncio.run 自体で致命的な例外が起きた場合もログを出して終了
        print(f"[{address}] device_task_thread: 予期しない例外で終了: {e!r}", file=sys.stderr)
        traceback.print_exc()


# ---------------------------------------------------------------------------
# スキャンタスク
# ---------------------------------------------------------------------------

async def scan_once_and_spawn_tasks(registry: DeviceRegistry) -> None:
    """
    1 回分の BLE スキャンを実行し、新規に見つかった WT901BLE 系デバイスごとに
    デバイスタスクスレッドを起動する。

    bleak のバージョン差異を吸収するため、
    - まず BleakScanner.discover(..., return_adv=True) を試みる
    - TypeError になった場合は古いバージョンとみなして BleakScanner.discover(...) にフォールバックする
    """
    print("BLE スキャン開始...")

    try:
        # 新しめの bleak (>=0.19) では return_adv=True が使える
        scan_result = await BleakScanner.discover(timeout=4.0, return_adv=True)
        # scan_result: dict[str, tuple[BLEDevice, AdvertisementData]]
        items = list(scan_result.values())
    except TypeError:
        # 古い bleak では return_adv が使えないので、従来通りの list[BLEDevice] を使う
        devices = await BleakScanner.discover(timeout=4.0)
        items = [(d, None) for d in devices]

    for ble_device, adv_data in items:
        try:
            address = ble_device.address
            mac = address.upper()

            # デバイス名: BLEDevice.name を優先し、無ければ広告(local_name)を使う
            name = ble_device.name or ""
            if (not name) and adv_data is not None and getattr(adv_data, "local_name", None):
                name = adv_data.local_name or ""
            name = name or ""

            # デバッグ用: 見つけた全デバイスを表示
            # （多すぎる場合はこの print をコメントアウトして構いません）
            print(f"  発見: addr={mac} name={name!r}")

            # WITMOTION センサかどうかの判定
            is_candidate = False
            if name.startswith(DEVICE_NAME_PREFIX):
                is_candidate = True
            elif adv_data is not None:
                # 広告に含まれる service_uuids から判定する (ある場合のみ)
                service_uuids = getattr(adv_data, "service_uuids", None) or []
                for u in service_uuids:
                    if u.lower() == WITMOTION_SERVICE_UUID.lower():
                        is_candidate = True
                        break

            if not is_candidate:
                continue

            if registry.has_device(mac):
                # すでにデバイスタスク起動済み
                continue

            print(
                f"[{mac}] 新しい WITMOTION BLE センサを検出: "
                f"name={name!r}"
            )

            # デバイスタスク用スレッドを起動
            t = threading.Thread(
                target=device_task_thread,
                args=(address, name),
                daemon=True,
            )
            registry.register_device(mac, t)
            t.start()
        except Exception as e:
            print(f"スキャン結果処理中に例外発生: {e!r}", file=sys.stderr)
            traceback.print_exc()


def scan_task_thread(registry: DeviceRegistry) -> None:
    """
    スキャンタスク用スレッドのエントリポイント。
    永久ループで BLE スキャンを繰り返し、新規デバイスがあればタスクを起動する。
    """
    while True:
        try:
            asyncio.run(scan_once_and_spawn_tasks(registry))
        except KeyboardInterrupt:
            # スキャンスレッドで Ctrl+C が飛んでも、ここでは終了せず継続させたい場合は無視する
            print("scan_task_thread: KeyboardInterrupt を無視して継続します", file=sys.stderr)
        except Exception as e:
            print(f"scan_task_thread: スキャン中に例外発生: {e!r}", file=sys.stderr)
            traceback.print_exc()

        # 次のスキャンまでランダムインターバルで待機
        delay = jittered_interval(BASE_INTERVAL_SEC)
        print(f"次のスキャンまで {delay:.1f} 秒待機します")
        try:
            time.sleep(delay)
        except KeyboardInterrupt:
            # メインプロセスが終了されるまで基本的には動き続ける想定だが、
            # ユーザーが Ctrl+C を押した場合はここでスレッドが終了する。
            print("scan_task_thread: KeyboardInterrupt 受信、スレッド終了")
            return


# ---------------------------------------------------------------------------
# エントリポイント
# ---------------------------------------------------------------------------
def main() -> None:
    print("witmotion_sensor_test.py 起動")
    print("  - WITMOTION WT901BLECL / WT9011DCL BLE 5.0 センサを自動検出して接続します")
    print("  - プログラムの終了はユーザーがプロセスを終了させてください (例: Ctrl+C, kill)")
    print()

    # 前回の実行などで残っている WT901BLE 接続を一度すべて切っておく
    force_disconnect_witmotion_devices()

    registry = DeviceRegistry()

    # スキャン専用スレッドを起動
    scan_thread = threading.Thread(
        target=scan_task_thread,
        args=(registry,),
        daemon=True,
    )
    scan_thread.start()

    # メインスレッドは特に処理を持たず、ただ待機しているだけ
    try:
        while True:
            # たまに登録済みデバイス一覧を表示してもよい
            time.sleep(60.0)
            threads = registry.get_device_threads()
            if threads:
                names = ", ".join(sorted(threads.keys()))
                print(f"現在アクティブなデバイスタスク: {names}")
            else:
                print("まだデバイスタスクは起動していません")
    except KeyboardInterrupt:
        # スクリプト自体は Ctrl+C で終了できるようにしておく
        print("\nメインスレッド: KeyboardInterrupt を受信しました。プログラムを終了します。")


if __name__ == "__main__":
    main()
