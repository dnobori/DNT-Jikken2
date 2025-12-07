#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
dn_pysensors_server1.py

Linux 上で Bluetooth センサからリアルタイムにデータを取得し、
直近 60 秒分の履歴を簡易 HTTP サーバ (/realtime) で配信する常駐プログラム。

構成:
  PART1: WITMOTION IMU センサ ([A] WT901BLECL, [B] WT9011DCL) をスキャン＆受信
  PART2: Polar H10 心拍センサ ([C]) をスキャン＆受信
  PART3: TCP 7001 番ポートで HTTP サーバを起動し、PART1/2 が蓄積した
         メモリ上のデータを JSON (PySensorFrameHistory 相当) として返す

各 PART は別スレッドで実行され、プログラムはユーザが Ctrl+C で
強制終了するまで動作し続ける。
"""

from __future__ import annotations

import asyncio
import datetime as dt
import json
import random
import subprocess
import sys
import threading
import time
import traceback
from dataclasses import dataclass
from typing import Any, Dict, List, Optional, Tuple

from http.server import BaseHTTPRequestHandler, HTTPServer
import socketserver

from bleak import BleakClient, BleakScanner
from bleakheart import HeartRate, PolarMeasurementData

# =============================================================================
# 共通ユーティリティ / データ構造
# =============================================================================


def utc_now() -> dt.datetime:
    """UTC の現在時刻を返す。"""
    return dt.datetime.now(dt.timezone.utc)


def format_timestamp_utc_ms(t: dt.datetime) -> str:
    """
    C# 側の PySensorFrameHistory と同じ書式のタイムスタンプ文字列を生成する。
    形式: "YYYYMMDD_HHMMSS.MMM" (UTC、ミリ秒精度)
    """
    t_utc = t.astimezone(dt.timezone.utc)
    return t_utc.strftime("%Y%m%d_%H%M%S.") + f"{t_utc.microsecond // 1000:03d}"


@dataclass
class AxisData:
    """
    PySensorAxisFrameData に対応する Python 側の内部データ構造。

    各値は [P2_OUTPUT] に現れる軸データそのもの (物理単位)。
    """

    ax_m_s2: float
    ay_m_s2: float
    az_m_s2: float
    pitch_deg: float
    yaw_deg: float
    roll_deg: float
    wx_deg_s: float
    wy_deg_s: float
    wz_deg_s: float


@dataclass
class HeartData:
    """
    PySensorHeartData に対応する Python 側の内部データ構造。
    """

    bpm: int              # 心拍数 (BPM)
    rr_msecs: int         # R-R 間隔 (ミリ秒)
    internal_timestamp: int  # Polar H10 が報告する内部タイムスタンプ (ナノ秒など 64bit 値)


@dataclass
class InternalFrame:
    """
    PySensorFrame の Python 内部表現。

    timestamp_dt はメモリから古いフレームを削除するためだけに使い、
    HTTP 応答の JSON には含めない。
    """

    frame_number: int
    timestamp_str: str
    timestamp_dt: dt.datetime
    device_type: str   # "Polar_H10" / "WT901BLECL" / "WT9011DCL"
    device_id: str     # MAC アドレス (大文字, "AA:BB:...:FF")
    data_type: str     # "Heart" または "Axis"
    axis_data: Optional[AxisData] = None
    heart_data: Optional[HeartData] = None


@dataclass
class InternalDeviceInfo:
    """
    PySensorDevice の Python 内部表現。
    """

    device_type: str
    device_id: str
    last_seen_dt: dt.datetime





class SensorDataStore:
    """
    PART1 / PART2 が取得したデータを保持し、PART3(HTTP) が参照する共有データ構造。

    - スレッドセーフにするため、内部更新はすべて Lock で保護する。
    - フレームは「ほぼ」timestamp_dt 昇順だが、順序には依存しない削除ロジックとする。
    - 60 秒より古いフレームは最大 1 秒に 1 回だけ一括削除する。
    - デバイスごとの最終受信時刻も保持し、30 秒以内のものだけを「接続中」とみなす。
    """

    def __init__(self) -> None:
        # クリティカルセクション用ロック
        self._lock = threading.Lock()
        # 受信済みフレームのリスト
        self._frames: List[InternalFrame] = []
        # PySensorFrame.FrameNumber に対応する連番カウンタ
        self._frame_counter: int = 0
        # デバイスごとの最終受信時刻
        # key: (device_type, device_id), value: datetime(UTC)
        self._device_last_seen: Dict[Tuple[str, str], dt.datetime] = {}
        # 直近で古いデータ削除を行なった時刻
        self._last_prune_time: dt.datetime = utc_now()

    # ------------------------------------------------------------------
    # 内部ユーティリティ
    # ------------------------------------------------------------------

    def _next_frame_number_locked(self) -> int:
        """
        フレーム番号カウンタをインクリメントして返す。

        呼び出し元は self._lock を取得済みであること。
        """
        self._frame_counter += 1
        return self._frame_counter

    def _prune_frames_locked(self, now: dt.datetime) -> None:
        """
        60 秒より古いフレームを削除する。

        - 呼び出し元は self._lock を取得済みであること。
        - パフォーマンスのため、最大 1 秒に 1 回だけ実行する。
        """
        # 1 秒未満の間隔で呼ばれた場合はスキップ
        if (now - self._last_prune_time) < dt.timedelta(seconds=1):
            return

        self._last_prune_time = now
        cutoff = now - dt.timedelta(seconds=60)

        # リストの順序に依存せず、60 秒より古いフレームをすべて捨てる
        # （データ件数的に十分軽量）
        self._frames = [f for f in self._frames if f.timestamp_dt >= cutoff]

    # ------------------------------------------------------------------
    # 公開 API: フレーム追加
    # ------------------------------------------------------------------

    def add_axis_frame(self, device_type: str, device_id: str, axis_data: AxisData) -> None:
        """
        軸センサーデータ 1 フレームを履歴に追加する。

        Args:
            device_type: "WT901BLECL" または "WT9011DCL"
            device_id  : Bluetooth MAC アドレス (大文字)
            axis_data  : 加速度・角速度・角度など 1 サンプル分
        """
        device_id = device_id.upper()

        # now / ts_str の取得もロック内で行うことで、timestamp_dt の順序が
        # self._frames への追加順と一致しやすくなる（厳密な保証は不要だが、
        # 無駄なズレを減らせる）
        with self._lock:
            now = utc_now()
            ts_str = format_timestamp_utc_ms(now)

            frame_number = self._next_frame_number_locked()
            frame = InternalFrame(
                frame_number=frame_number,
                timestamp_str=ts_str,
                timestamp_dt=now,
                device_type=device_type,
                device_id=device_id,
                data_type="Axis",
                axis_data=axis_data,
                heart_data=None,
            )
            self._frames.append(frame)
            self._device_last_seen[(device_type, device_id)] = now
            self._prune_frames_locked(now)

    def add_heart_frame(self, device_id: str, heart_data: HeartData) -> None:
        """
        心拍データ 1 フレームを履歴に追加する。

        Args:
            device_id : Bluetooth MAC アドレス (大文字)
            heart_data: 心拍数・R-R 間隔・内部タイムスタンプ
        """
        device_id = device_id.upper()
        device_type = "Polar_H10"

        with self._lock:
            now = utc_now()
            ts_str = format_timestamp_utc_ms(now)

            frame_number = self._next_frame_number_locked()
            frame = InternalFrame(
                frame_number=frame_number,
                timestamp_str=ts_str,
                timestamp_dt=now,
                device_type=device_type,
                device_id=device_id,
                data_type="Heart",
                axis_data=None,
                heart_data=heart_data,
            )
            self._frames.append(frame)
            self._device_last_seen[(device_type, device_id)] = now
            self._prune_frames_locked(now)

    # ------------------------------------------------------------------
    # 公開 API: デバイス数カウント / スナップショット生成
    # ------------------------------------------------------------------

    def count_alive_devices(self, device_types: List[str], alive_within_sec: float = 30.0) -> int:
        """
        指定された DeviceType 群について、最後の受信が alive_within_sec 秒以内の
        デバイス数を返す。

        PART1 / PART2 の統計表示 (NUM_DEVICES) 用。
        """
        now = utc_now()
        cutoff = now - dt.timedelta(seconds=alive_within_sec)
        types_set = set(device_types)

        with self._lock:
            cnt = 0
            for (dtype, _did), last_dt in self._device_last_seen.items():
                if dtype in types_set and last_dt >= cutoff:
                    cnt += 1
        return cnt

    def build_history_snapshot(self) -> Dict[str, Any]:
        """
        PySensorFrameHistory 相当の構造を JSON 変換可能な dict として返す。

        戻り値の構造:
            {
              "CurrentTime": str,
              "CurrentConnectedDevices": [ PySensorDevice... ],
              "ListOfData": [ PySensorFrame... ]  (TimeStamp 降順)
            }
        """
        now = utc_now()
        current_time_str = format_timestamp_utc_ms(now)
        cutoff_devices = now - dt.timedelta(seconds=30)

        with self._lock:
            # ここでも一応、古いフレームを整理しておく
            self._prune_frames_locked(now)

            frames_copy = list(self._frames)
            device_infos: List[InternalDeviceInfo] = []
            for (dtype, did), last_dt in self._device_last_seen.items():
                if last_dt >= cutoff_devices:
                    device_infos.append(InternalDeviceInfo(dtype, did, last_dt))

        # 以下は元の実装のままで OK
        devices_json: List[Dict[str, Any]] = []
        for dev in device_infos:
            devices_json.append(
                {
                    "DeviceType": dev.device_type,
                    "DeviceId": dev.device_id,
                    "LastDataReceivedTimeStamp": format_timestamp_utc_ms(dev.last_seen_dt),
                }
            )

        frames_sorted = sorted(frames_copy, key=lambda f: f.timestamp_dt, reverse=True)

        frames_json: List[Dict[str, Any]] = []
        for fr in frames_sorted:
            item: Dict[str, Any] = {
                "FrameNumber": fr.frame_number,
                "TimeStamp": fr.timestamp_str,
                "DeviceType": fr.device_type,
                "DeviceId": fr.device_id,
                "DataType": fr.data_type,
            }
            if fr.data_type == "Axis" and fr.axis_data is not None:
                item["AxisData"] = {
                    "ax_m_s2": fr.axis_data.ax_m_s2,
                    "ay_m_s2": fr.axis_data.ay_m_s2,
                    "az_m_s2": fr.axis_data.az_m_s2,
                    "pitch_deg": fr.axis_data.pitch_deg,
                    "yaw_deg": fr.axis_data.yaw_deg,
                    "roll_deg": fr.axis_data.roll_deg,
                    "wx_deg_s": fr.axis_data.wx_deg_s,
                    "wy_deg_s": fr.axis_data.wy_deg_s,
                    "wz_deg_s": fr.axis_data.wz_deg_s,
                }
            elif fr.data_type == "Heart" and fr.heart_data is not None:
                item["HeartData"] = {
                    "Bpm": fr.heart_data.bpm,
                    "RrMsecs": fr.heart_data.rr_msecs,
                    "InternalTimeStamp": fr.heart_data.internal_timestamp,
                }
            frames_json.append(item)

        return {
            "CurrentTime": current_time_str,
            "CurrentConnectedDevices": devices_json,
            "ListOfData": frames_json,
        }




class PartStats:
    """
    PART1 / PART2 用の統計カウンタ。

    各 PART から increment() で 1 件ごとに通知してもらい、
    1 秒ごとに NUM_DEVICES / DATA_PER_SECOND をコンソール出力する。
    """

    def __init__(self, part_name: str, store: SensorDataStore, device_types: List[str]) -> None:
        self.part_name = part_name
        self.store = store
        self.device_types = list(device_types)
        self._lock = threading.Lock()
        self._counter: int = 0

    def increment(self, n: int = 1) -> None:
        """このパートでデータを n 件受信したときに呼び出す。"""
        if n <= 0:
            return
        with self._lock:
            self._counter += n

    def logging_loop(self) -> None:
        """
        1 秒ごとに NUM_DEVICES / DATA_PER_SECOND を出力し続ける無限ループ。
        """
        while True:
            time.sleep(1.0)
            with self._lock:
                data_per_second = self._counter
                self._counter = 0
            num_devices = self.store.count_alive_devices(self.device_types, alive_within_sec=30.0)
            print(
                f"{self.part_name}: NUM_DEVICES = {num_devices}, DATA_PER_SECOND = {data_per_second}",
                flush=True,
            )


# 共有データストアと統計カウンタ (モジュール全体で 1 つずつ)
GLOBAL_STORE = SensorDataStore()
PART1_STATS = PartStats("PART1", GLOBAL_STORE, ["WT901BLECL", "WT9011DCL"])
PART2_STATS = PartStats("PART2", GLOBAL_STORE, ["Polar_H10"])

# =============================================================================
# PART1: WITMOTION IMU センサ ([A], [B]) の取得
#   元コード: witmotion_sensor_test.py ([P2])
# =============================================================================

# スキャン間隔 / 接続再試行間隔 (秒)
BASE_INTERVAL_SEC = 5.0
JITTER_RATIO = 0.3  # ±30%

# 接続タイムアウト (秒)
CONNECT_TIMEOUT_SEC = 5.0

# WITMOTION BLE 5.0 固有 UUID
WITMOTION_SERVICE_UUID = "0000ffe5-0000-1000-8000-00805f9a34fb"
WITMOTION_NOTIFY_CHAR_UUID = "0000ffe4-0000-1000-8000-00805f9a34fb"
WITMOTION_WRITE_CHAR_UUID = "0000ffe9-0000-1000-8000-00805f9a34fb"

# デバイス名のプレフィックス (ドキュメントによると "WT901BLE+番号")
DEVICE_NAME_PREFIX = "WT901BLE"

# WT901BLECL / WT9011DCL の出力レート設定用テーブル
RATE_CODE_TABLE: Dict[int, int] = {
    10: 0x06,
    20: 0x07,
    50: 0x08,
    100: 0x09,
    200: 0x0A,
}
# すべてのセンサに対して設定したい出力レート (Hz)
DEFAULT_TARGET_HZ = 50

# MAC アドレスごとに DeviceType を明示的に上書きしたい場合のテーブル
# 例:
#   DEVICE_TYPE_OVERRIDES = {
#       "D9:91:90:3A:C1:4A": "WT9011DCL",
#   }
DEVICE_TYPE_OVERRIDES: Dict[str, str] = {}


def jittered_interval(base: float = BASE_INTERVAL_SEC) -> float:
    """5 秒 ± 30% のような乱数付きインターバルを返す。"""
    r = random.uniform(1.0 - JITTER_RATIO, 1.0 + JITTER_RATIO)
    return max(0.1, base * r)


def resolve_witmotion_device_type(mac: str, name: Optional[str]) -> str:
    """
    WT901BLE 系デバイスの DeviceType 文字列を推定する。

    - DEVICE_TYPE_OVERRIDES に登録されていればその値を優先
    - そうでなければ、名前に "WT9011" を含む場合は "WT9011DCL"
    - それ以外は "WT901BLECL" (既定値)
    """
    mac_up = mac.upper()
    override = DEVICE_TYPE_OVERRIDES.get(mac_up)
    if override in ("WT901BLECL", "WT9011DCL"):
        return override

    nm = (name or "").upper()
    if "WT9011" in nm:
        return "WT9011DCL"
    return "WT901BLECL"


def _bluetoothctl_cmd(*args: str) -> str:
    """
    bluetoothctl を 1 回起動してコマンドを実行し、標準出力を文字列で返す。

    失敗しても例外は投げず、エラーメッセージを出力して空文字列を返す。
    """
    try:
        cp = subprocess.run(
            ["bluetoothctl", *args],
            check=False,
            capture_output=True,
            text=True,
        )
        return cp.stdout
    except FileNotFoundError:
        print(
            "[PART1][bluetoothctl] コマンドが見つかりません。Bluetooth クリーンアップをスキップします。",
            file=sys.stderr,
            flush=True,
        )
        return ""
    except Exception as e:
        print(f"[PART1][bluetoothctl] 実行中に例外発生: {e!r}", file=sys.stderr, flush=True)
        traceback.print_exc()
        return ""


def force_disconnect_witmotion_devices() -> None:
    """
    BlueZ に登録されている WT901BLE 系デバイスを列挙し、
    すべて bluetoothctl disconnect しておく。

    前回実行時の接続が残っていても、ここで一度切断することで
    Bleak からの再接続が安定しやすくなる。
    """
    out = _bluetoothctl_cmd("devices")
    if not out:
        return

    targets: List[Tuple[str, str]] = []  # (mac, name)
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

    print("[PART1] bluetoothctl で WT901BLE 系デバイスの接続をクリーンアップします...", flush=True)
    for mac, name in targets:
        print(f"[PART1]   -> disconnect {mac} ({name})", flush=True)
        _bluetoothctl_cmd("disconnect", mac)


def decode_imu_packet(data: bytes) -> Optional[Dict[str, float]]:
    """
    WITMOTION BLE 5.0 プロトコルの IMU パケット (Flag=0x61) をデコードする。

    戻り値:
        {"ax_m_s2": ..., "ay_m_s2": ..., "az_m_s2": ...,
         "wx_deg_s": ..., "wy_deg_s": ..., "wz_deg_s": ...,
         "roll_deg": ..., "pitch_deg": ..., "yaw_deg": ...}
        を含む dict。対象外パケットのときは None。
    """
    if len(data) < 20:
        return None
    if data[0] != 0x55:
        return None
    flag = data[1]
    if flag != 0x61:
        # 他のフラグ (0x71 など) はここでは無視する
        return None

    raw_values: List[int] = []
    for i in range(2, 20, 2):
        raw = int.from_bytes(data[i : i + 2], byteorder="little", signed=True)
        raw_values.append(raw)
    if len(raw_values) != 9:
        return None

    ax_raw, ay_raw, az_raw, wx_raw, wy_raw, wz_raw, roll_raw, pitch_raw, yaw_raw = raw_values

    # 単位変換 (WIT 標準プロトコル準拠)
    g = 9.8
    ax = ax_raw / 32768.0 * 16.0 * g
    ay = ay_raw / 32768.0 * 16.0 * g
    az = az_raw / 32768.0 * 16.0 * g

    wx = wx_raw / 32768.0 * 2000.0
    wy = wy_raw / 32768.0 * 2000.0
    wz = wz_raw / 32768.0 * 2000.0

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


async def configure_output_rate(client: BleakClient, mac: str, target_hz: int = DEFAULT_TARGET_HZ) -> None:
    """
    センサ内部の「Return rate (出力周波数)」を設定する。

    Unlock -> Set rate -> Save の順で送信する。
    """
    code = RATE_CODE_TABLE.get(target_hz)
    if code is None:
        print(f"[PART1][{mac}] 指定した出力レート {target_hz} Hz には対応していません", file=sys.stderr, flush=True)
        return

    try:
        print(f"[PART1][{mac}] 出力レートを {target_hz} Hz に設定します", flush=True)

        # 1. Unlock
        unlock_cmd = bytes([0xFF, 0xAA, 0x69, 0x88, 0xB5])
        await client.write_gatt_char(WITMOTION_WRITE_CHAR_UUID, unlock_cmd, response=True)
        await asyncio.sleep(0.05)

        # 2. Return rate 設定
        rate_cmd = bytes([0xFF, 0xAA, 0x03, code, 0x00])
        await client.write_gatt_char(WITMOTION_WRITE_CHAR_UUID, rate_cmd, response=True)
        await asyncio.sleep(0.05)

        # 3. Save 設定
        save_cmd = bytes([0xFF, 0xAA, 0x00, 0x00, 0x00])
        await client.write_gatt_char(WITMOTION_WRITE_CHAR_UUID, save_cmd, response=True)
        await asyncio.sleep(0.05)

        print(f"[PART1][{mac}] 出力レート設定完了 ({target_hz} Hz)", flush=True)
    except Exception as e:
        print(f"[PART1][{mac}] 出力レート設定中に例外発生: {e!r}", file=sys.stderr, flush=True)
        traceback.print_exc()


class DeviceRegistry:
    """
    すでにデバイスタスクを起動した MAC アドレスの集合を管理するクラス。

    1 デバイスにつき 1 本だけ device_task_thread を起動するために用いる。
    """

    def __init__(self) -> None:
        self._lock = threading.Lock()
        self._known_devices: Dict[str, threading.Thread] = {}

    def has_device(self, mac: str) -> bool:
        mac_up = mac.upper()
        with self._lock:
            return mac_up in self._known_devices

    def register_device(self, mac: str, thread: threading.Thread) -> None:
        mac_up = mac.upper()
        with self._lock:
            self._known_devices[mac_up] = thread

    def get_device_threads(self) -> Dict[str, threading.Thread]:
        with self._lock:
            return dict(self._known_devices)


async def part1_run_notification_loop(
    client: BleakClient,
    mac: str,
    device_label: str,
    device_type: str,
) -> None:
    """
    接続済みクライアントに対して Notify を開始し、
    切断されるまで IMU データを受信し続ける小ループ。
    """
    latest_error: Optional[BaseException] = None

    def handle_notification(sender: int, data: bytes) -> None:
        """
        Notify コールバック。1 パケット (最大 20 バイト) ごとに呼ばれる。
        ここでは decode_imu_packet() で軸データに変換し、GLOBAL_STORE に格納する。
        """
        nonlocal latest_error
        try:
            sample = decode_imu_packet(data)
            if sample is None:
                return

            axis = AxisData(
                ax_m_s2=sample["ax_m_s2"],
                ay_m_s2=sample["ay_m_s2"],
                az_m_s2=sample["az_m_s2"],
                pitch_deg=sample["pitch_deg"],
                yaw_deg=sample["yaw_deg"],
                roll_deg=sample["roll_deg"],
                wx_deg_s=sample["wx_deg_s"],
                wy_deg_s=sample["wy_deg_s"],
                wz_deg_s=sample["wz_deg_s"],
            )
            GLOBAL_STORE.add_axis_frame(device_type=device_type, device_id=mac, axis_data=axis)
            PART1_STATS.increment(1)
        except Exception as ex:
            latest_error = ex
            print(f"[PART1][{mac}] 通知処理中に例外発生: {ex!r}", file=sys.stderr, flush=True)
            traceback.print_exc()

    print(f"[PART1][{mac}] Notify 開始: char={WITMOTION_NOTIFY_CHAR_UUID}", flush=True)
    await client.start_notify(WITMOTION_NOTIFY_CHAR_UUID, handle_notification)

    try:
        while True:
            if not client.is_connected:
                print(f"[PART1][{mac}] 接続が切断されました (is_connected=False)", flush=True)
                break
            await asyncio.sleep(1.0)

            if latest_error is not None:
                print(f"[PART1][{mac}] 直近の通知処理例外: {latest_error!r}", file=sys.stderr, flush=True)
                latest_error = None
    finally:
        try:
            print(f"[PART1][{mac}] Notify 停止", flush=True)
            await client.stop_notify(WITMOTION_NOTIFY_CHAR_UUID)
        except Exception:
            pass


async def part1_device_session(address: str, name: Optional[str]) -> None:
    """
    1 つの WITMOTION センサに対する大ループ。

    - 永久に接続を試行し続ける
    - 接続に成功したら part1_run_notification_loop() で受信を続ける
    - 例外や切断時には数秒待ってから再接続
    """
    mac = address.upper()
    device_label = f"{name or 'WT901BLE'} ({mac})"
    device_type = resolve_witmotion_device_type(mac, name)

    while True:
        delay = jittered_interval(BASE_INTERVAL_SEC)
        try:
            print(f"[PART1][{mac}] 接続試行開始 (timeout={CONNECT_TIMEOUT_SEC}s) name={name!r}", flush=True)
            async with BleakClient(address, timeout=CONNECT_TIMEOUT_SEC) as client:
                if not client.is_connected:
                    raise RuntimeError("BleakClient.is_connected が False です")

                print(f"[PART1][{mac}] 接続成功: {device_label}", flush=True)

                await configure_output_rate(client, mac, target_hz=DEFAULT_TARGET_HZ)

                await part1_run_notification_loop(client, mac, device_label, device_type)
        except asyncio.CancelledError:
            raise
        except Exception as e:
            print(f"[PART1][{mac}] 接続または受信中に例外発生: {e!r}", file=sys.stderr, flush=True)
            traceback.print_exc()

        print(f"[PART1][{mac}] 再接続まで {delay:.1f} 秒待機します", flush=True)
        try:
            await asyncio.sleep(delay)
        except asyncio.CancelledError:
            raise


def part1_device_task_thread(address: str, name: Optional[str]) -> None:
    """
    デバイスタスク用スレッドのエントリーポイント。

    各スレッドは独自の asyncio イベントループを持ち、part1_device_session() を
    永久に回し続ける。
    """
    try:
        asyncio.run(part1_device_session(address, name))
    except KeyboardInterrupt:
        print(f"[PART1][{address}] device_task_thread: KeyboardInterrupt 受信、スレッド終了", flush=True)
    except Exception as e:
        print(f"[PART1][{address}] device_task_thread: 予期しない例外で終了: {e!r}", file=sys.stderr, flush=True)
        traceback.print_exc()


async def part1_scan_once_and_spawn_tasks(registry: DeviceRegistry) -> None:
    """
    1 回分の BLE スキャンを実行し、新規に見つかった WT901BLE 系デバイスごとに
    デバイスタスクスレッドを起動する。
    """
    print("[PART1] BLE スキャン開始...", flush=True)

    try:
        # 新しめの bleak (>=0.19) では return_adv=True が利用できる
        scan_result = await BleakScanner.discover(timeout=4.0, return_adv=True)
        items = list(scan_result.values())  # type: ignore[arg-type]
    except TypeError:
        # 古いバージョンでは従来通りの API を使う
        devices = await BleakScanner.discover(timeout=4.0)
        items = [(d, None) for d in devices]

    for ble_device, adv_data in items:
        try:
            address = ble_device.address
            mac = address.upper()
            name = ble_device.name or ""
            if (not name) and adv_data is not None and getattr(adv_data, "local_name", None):
                name = adv_data.local_name or ""
            name = name or ""

            print(f"[PART1]   発見: addr={mac} name={name!r}", flush=True)

            # WITMOTION センサかどうか判定
            is_candidate = False
            if name.startswith(DEVICE_NAME_PREFIX):
                is_candidate = True
            elif adv_data is not None:
                service_uuids = getattr(adv_data, "service_uuids", None) or []
                for u in service_uuids:
                    if u.lower() == WITMOTION_SERVICE_UUID.lower():
                        is_candidate = True
                        break

            if not is_candidate:
                continue

            if registry.has_device(mac):
                # 既にデバイスタスク起動済み
                continue

            print(f"[PART1][{mac}] 新しい WITMOTION BLE センサを検出: name={name!r}", flush=True)

            t = threading.Thread(
                target=part1_device_task_thread,
                args=(address, name),
                daemon=True,
            )
            registry.register_device(mac, t)
            t.start()
        except Exception as e:
            print(f"[PART1] スキャン結果処理中に例外発生: {e!r}", file=sys.stderr, flush=True)
            traceback.print_exc()


def part1_scan_task_main(registry: DeviceRegistry) -> None:
    """
    スキャンタスク用スレッドのエントリーポイント。

    永久ループで BLE スキャンを繰り返し、新規デバイスがあれば device_task_thread
    を起動する。
    """
    while True:
        try:
            asyncio.run(part1_scan_once_and_spawn_tasks(registry))
        except KeyboardInterrupt:
            print("[PART1] scan_task_thread: KeyboardInterrupt を無視して継続します", file=sys.stderr, flush=True)
        except Exception as e:
            print(f"[PART1] scan_task_thread: スキャン中に例外発生: {e!r}", file=sys.stderr, flush=True)
            traceback.print_exc()

        delay = jittered_interval(BASE_INTERVAL_SEC)
        print(f"[PART1] 次のスキャンまで {delay:.1f} 秒待機します", flush=True)
        try:
            time.sleep(delay)
        except KeyboardInterrupt:
            print("[PART1] scan_task_thread: KeyboardInterrupt 受信、スレッド終了", flush=True)
            return


def part1_main_thread() -> None:
    """
    PART1 スレッドのエントリーポイント。

    - 起動時に bluetoothctl で WT901BLE 系デバイスの残存接続をクリーンアップ
    - DeviceRegistry を用意し、スキャンタスクを回し続ける
    """
    print("[PART1] WITMOTION センサ監視スレッドを開始します。", flush=True)
    force_disconnect_witmotion_devices()

    registry = DeviceRegistry()
    part1_scan_task_main(registry)


# =============================================================================
# PART2: Polar H10 心拍センサ ([C]) の取得
#   元コード: monitor_polar_forever.py ([P1])
# =============================================================================

# Polar H10 用タイムアウト / インターバル設定
POLAR_CONNECT_TIMEOUT: float = 5.0
POLAR_RETRY_INTERVAL: float = 5.0
POLAR_POLL_INTERVAL: float = 0.1  # 100 ms


async def part2_find_polar_device() -> Optional[Any]:
    """
    Polar デバイス (名前に "polar" を含むもの) を 1 台スキャンして返す。
    成功: BLEDevice, 失敗: None
    """
    print(f"[PART2][SCAN] Searching for POLAR device (timeout={POLAR_CONNECT_TIMEOUT}s)...", flush=True)

    try:
        device = await BleakScanner.find_device_by_filter(
            lambda dev, adv: dev.name and "polar" in dev.name.lower(),
            timeout=POLAR_CONNECT_TIMEOUT,
        )
    except Exception as e:
        print(f"[PART2][SCAN] Error during scan: {e!r}", file=sys.stderr, flush=True)
        return None

    if device is None:
        print("[PART2][SCAN] POLAR device not found.", flush=True)
    else:
        print(f"[PART2][SCAN] Found device: {device.name} ({device.address})", flush=True)

    return device


async def part2_heart_stream_loop(
    client: BleakClient,
    hr_queue: "asyncio.Queue[Tuple]",
    ecg_queue: "asyncio.Queue[Tuple]",
    device_mac: str,
) -> None:
    """
    「小さなメインループ」: 100ms ごとに心拍フレームをキューから取り出し、
    GLOBAL_STORE に蓄積していく。

    - 100ms のタイムスライスごとに hr_queue から心拍フレームを 1 つ取り出す
    - ECG フレームはメモリ肥大化を防ぐために捨てる (タスク完了だけ行う)
    """
    print("[PART2][LOOP] Entering heart stream loop (100 ms polling).", flush=True)

    while client.is_connected:
        try:
            try:
                frame = await asyncio.wait_for(hr_queue.get(), timeout=POLAR_POLL_INTERVAL)
            except asyncio.TimeoutError:
                frame = None

            if frame is not None:
                try:
                    # HeartRate のフレーム形式 (unpack=True, instant_rate=True):
                    # ('HR', tstamp_ns, (bpm, rr_ms), energy_kJ)
                    tag, tstamp, payload, _energy = frame
                    if tag == "HR" and payload is not None:
                        bpm, rr_ms = payload
                        # bpm, rr_ms は float のこともあるので丸めて int に変換
                        bpm_int = int(round(float(bpm)))
                        rr_int = int(round(float(rr_ms)))

                        heart = HeartData(
                            bpm=bpm_int,
                            rr_msecs=rr_int,
                            internal_timestamp=int(tstamp),
                        )
                        GLOBAL_STORE.add_heart_frame(device_id=device_mac, heart_data=heart)
                        PART2_STATS.increment(1)
                except Exception as e:
                    print(f"[PART2][HR ] Failed to decode frame {frame!r}: {e!r}", file=sys.stderr, flush=True)
                finally:
                    hr_queue.task_done()

            # ECG フレームは内容を使わないが、キューだけは捌いておく
            while not ecg_queue.empty():
                _ecg_frame = ecg_queue.get_nowait()
                ecg_queue.task_done()

        except Exception as e:
            print(f"[PART2][LOOP] Error in heart stream loop: {e!r}", file=sys.stderr, flush=True)
            break

    print("[PART2][LOOP] Leaving heart stream loop (disconnected or error).", flush=True)


async def part2_connect_and_stream(device: Any) -> None:
    """
    1 回の「接続～ストリーミング～切断」サイクルを実行する。

    - BleakClient で接続
    - bleakheart.HeartRate / PolarMeasurementData をセットアップして HR+ECG を取得
    - 心拍フレームを part2_heart_stream_loop() で GLOBAL_STORE へ格納
    """
    print(f"[PART2][CONN] Trying to connect to {device} ...", flush=True)

    hr_queue: asyncio.Queue = asyncio.Queue()
    ecg_queue: asyncio.Queue = asyncio.Queue()

    def _disconnected_callback(client: BleakClient) -> None:
        print("[PART2][CONN] Sensor disconnected (disconnected_callback).", flush=True)

    device_mac = getattr(device, "address", "").upper()

    try:
        async with BleakClient(
            device,
            disconnected_callback=_disconnected_callback,
            timeout=POLAR_CONNECT_TIMEOUT,
        ) as client:
            print(
                f"[PART2][CONN] Connected: {client.is_connected}  "
                f"name={client.name}  addr={client.address}",
                flush=True,
            )
            if not client.is_connected:
                print("[PART2][CONN] client.is_connected is False, aborting this cycle.", flush=True)
                return

            # bleakheart オブジェクト構築
            heartrate = HeartRate(
                client,
                queue=hr_queue,
                instant_rate=False,
                unpack=True,
            )

            pmd = PolarMeasurementData(client, ecg_queue=ecg_queue)

            # ECG 設定を問い合わせ (デバッグ用)
            try:
                settings = await pmd.available_settings("ECG")
                print("[PART2][PMD] ECG settings reported by device:", flush=True)
                for k, v in settings.items():
                    print(f"[PART2][PMD]   {k}: {v}", flush=True)
            except Exception as e:
                print(f"[PART2][PMD] Could not read ECG settings: {e!r}", file=sys.stderr, flush=True)

            # ストリーミング開始
            try:
                await heartrate.start_notify()
                print("[PART2][HR ] Heart rate notifications started.", flush=True)
            except Exception as e:
                print(f"[PART2][HR ] Failed to start heart rate notifications: {e!r}", file=sys.stderr, flush=True)

            try:
                err_code, err_msg, _ = await pmd.start_streaming("ECG")
                if err_code != 0:
                    print(
                        f"[PART2][PMD] start_streaming('ECG') error {err_code}: {err_msg}",
                        file=sys.stderr,
                        flush=True,
                    )
                else:
                    print("[PART2][PMD] ECG streaming started.", flush=True)
            except Exception as e:
                print(f"[PART2][PMD] Failed to start ECG streaming: {e!r}", file=sys.stderr, flush=True)

            # 小ループ
            await part2_heart_stream_loop(client, hr_queue, ecg_queue, device_mac)

            # 停止処理
            if client.is_connected:
                print("[PART2][CONN] Stopping notifications / streams ...", flush=True)
                try:
                    await heartrate.stop_notify()
                except Exception as e:
                    print(f"[PART2][HR ] Error in stop_notify: {e!r}", file=sys.stderr, flush=True)

                try:
                    await pmd.stop_streaming("ECG")
                except Exception as e:
                    print(f"[PART2][PMD] Error in stop_streaming('ECG'): {e!r}", file=sys.stderr, flush=True)

            print("[PART2][CONN] Connection closed. Returning to outer loop.", flush=True)

    except Exception as e:
        print(f"[PART2][CONN] Exception in connect_and_stream: {e!r}", file=sys.stderr, flush=True)


async def part2_monitor_polar_forever() -> None:
    """
    Polar H10 への接続を永久に試み続ける大ループ。
    """
    print("[PART2][MAIN] Starting POLAR H10 monitor.", flush=True)

    while True:
        try:
            device = await part2_find_polar_device()
            if device is None:
                print(f"[PART2][MAIN] Will retry scan in {POLAR_RETRY_INTERVAL} seconds.", flush=True)
                await asyncio.sleep(POLAR_RETRY_INTERVAL)
                continue

            await part2_connect_and_stream(device)
        except Exception as e:
            print(f"[PART2][MAIN] Exception in outer loop: {e!r}", file=sys.stderr, flush=True)

        print(f"[PART2][MAIN] Reconnecting in {POLAR_RETRY_INTERVAL} seconds ...", flush=True)
        await asyncio.sleep(POLAR_RETRY_INTERVAL)


def part2_main_thread() -> None:
    """
    PART2 スレッドのエントリーポイント。

    asyncio のイベントループ上で part2_monitor_polar_forever() を動かし続ける。
    """
    try:
        asyncio.run(part2_monitor_polar_forever())
    except KeyboardInterrupt:
        print("\n[PART2][MAIN] KeyboardInterrupt received, exiting PART2 thread.", flush=True)


# =============================================================================
# PART3: HTTP サーバ (/realtime)
# =============================================================================


class ThreadingHTTPServer(socketserver.ThreadingMixIn, HTTPServer):
    """
    1 接続ごとにスレッドを分ける HTTP サーバ。

    多数のクライアントから高頻度アクセスされた場合でもブロッキングしにくい。
    """
    daemon_threads = True


class RealtimeRequestHandler(BaseHTTPRequestHandler):
    """
    /realtime または /realtime/ への GET に対して、GLOBAL_STORE に蓄積された
    センサーデータの JSON (PySensorFrameHistory) を text/plain として返す。

    それ以外のパスには 404 Not Found を返す。
    """

    # HTTP/1.1 で応答
    protocol_version = "HTTP/1.1"

    def do_GET(self) -> None:  # type: ignore[override]
        """
        GET リクエストを処理する。
        """
        path = self.path.split("?", 1)[0]  # クエリは無視する

        if path in ("/realtime", "/realtime/"):
            self._handle_realtime()
        else:
            self.send_error(404, "Not Found")

    def _handle_realtime(self) -> None:
        """
        /realtime へのアクセスを処理し、JSON を返す。
        """
        try:
            payload = GLOBAL_STORE.build_history_snapshot()
            body = json.dumps(payload, ensure_ascii=False, indent=2)
            body_bytes = body.encode("utf-8")
        except Exception as e:
            msg = f"Internal Server Error: {e!r}\n"
            msg_bytes = msg.encode("utf-8")
            self.send_response(500)
            self.send_header("Content-Type", "text/plain; charset=utf-8")
            self.send_header("Content-Length", str(len(msg_bytes)))
            self.send_header("Connection", "close")
            self.end_headers()
            try:
                self.wfile.write(msg_bytes)
            except Exception:
                pass
            print(f"[PART3] Error while building JSON: {e!r}", file=sys.stderr, flush=True)
            traceback.print_exc()
            return

        self.send_response(200)
        self.send_header("Content-Type", "text/plain; charset=utf-8")
        self.send_header("Content-Length", str(len(body_bytes)))
        self.send_header("Connection", "close")
        self.end_headers()
        try:
            self.wfile.write(body_bytes)
        except Exception as e:
            # クライアント側の切断などは致命的ではない
            print(f"[PART3] Error while sending response: {e!r}", file=sys.stderr, flush=True)

    # アクセス頻度が高くなる可能性があるため、標準のログ出力は抑制する
    def log_message(self, format: str, *args: Any) -> None:  # type: ignore[override]
        # 必要に応じて以下のように有効化してもよい
        # sys.stderr.write("[PART3] " + (format % args) + "\n")
        pass


def part3_http_server_thread() -> None:
    """
    PART3 スレッドのエントリーポイント。

    TCP ポート 7001 (IPv4 0.0.0.0:7001) で HTTP サーバを起動する。
    bind に失敗した場合は 5 秒間隔で再試行する。
    """
    host = "0.0.0.0"
    port = 7001
    addr = (host, port)

    while True:
        try:
            httpd = ThreadingHTTPServer(addr, RealtimeRequestHandler)
        except OSError as e:
            print(
                f"[PART3] Failed to bind {host}:{port} ({e!r}); retry in 5 seconds.",
                file=sys.stderr,
                flush=True,
            )
            time.sleep(5.0)
            continue

        try:
            print(f"[PART3] HTTP server started on http://{host}:{port}/realtime", flush=True)
            httpd.serve_forever()
        except KeyboardInterrupt:
            print("[PART3] KeyboardInterrupt received; shutting down HTTP server.", flush=True)
            httpd.server_close()
            break
        except Exception as e:
            print(f"[PART3] HTTP server error: {e!r}", file=sys.stderr, flush=True)
            traceback.print_exc()
            httpd.server_close()
            # 再度 bind からやり直す
            time.sleep(5.0)


# =============================================================================
# メイン関数
# =============================================================================


def main() -> None:
    """
    エントリーポイント。

    - PART1: WITMOTION IMU センサ監視スレッド
    - PART2: Polar H10 心拍センサ監視スレッド
    - PART3: HTTP サーバスレッド
    - STATS1/2: PART1/2 の統計表示スレッド

    を起動し、メインスレッドは単に待機する。
    """
    print("dn_pysensors_server1.py を起動します。", flush=True)
    print("  PART1: WITMOTION WT901BLECL / WT9011DCL IMU センサ", flush=True)
    print("  PART2: Polar H10 心拍センサ", flush=True)
    print("  PART3: HTTP サーバ (0.0.0.0:7001/realtime)", flush=True)
    print("プログラムの終了は Ctrl+C などでプロセスを終了してください。", flush=True)

    # PART1 スレッド
    t_part1 = threading.Thread(target=part1_main_thread, name="PART1-Thread", daemon=True)
    t_part1.start()

    # PART2 スレッド
    t_part2 = threading.Thread(target=part2_main_thread, name="PART2-Thread", daemon=True)
    t_part2.start()

    # PART3 (HTTP サーバ) スレッド
    t_part3 = threading.Thread(target=part3_http_server_thread, name="PART3-HTTP-Thread", daemon=True)
    t_part3.start()

    # PART1 統計スレッド
    t_stats1 = threading.Thread(
        target=PART1_STATS.logging_loop,
        name="PART1-Stats-Thread",
        daemon=True,
    )
    t_stats1.start()

    # PART2 統計スレッド
    t_stats2 = threading.Thread(
        target=PART2_STATS.logging_loop,
        name="PART2-Stats-Thread",
        daemon=True,
    )
    t_stats2.start()

    # メインスレッドは何もせずに待機するだけ
    try:
        while True:
            time.sleep(60.0)
    except KeyboardInterrupt:
        print("\n[MAIN] KeyboardInterrupt received; exiting main thread.", flush=True)


if __name__ == "__main__":
    main()
