#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
dn_pysensors_server1.py

[概要]
  - PART1: WITMOTION WT901BLECL / WT9011DCL ([A], [B]) から軸センサデータを取得し、メモリに60秒分保持
  - PART2: POLAR H10 ([C]) から心拍データ (BPM, RR, 内部タイムスタンプ) を取得し、メモリに60秒分保持
  - PART3: TCP 7001 で簡易 HTTP サーバを起動し、/realtime にアクセスがあったら
           上記 PART1/2 の最新60秒分を Data1 / JSON1 仕様の JSON として text/plain で返す

[スレッド構成]
  - メインスレッド:
      - PART1 スレッド起動
      - PART2 スレッド起動
      - PART3 スレッド起動
      - PART1 統計出力スレッド起動
      - PART2 統計出力スレッド起動
      - 以後は Ctrl+C まで待機
  - PART1 スレッド:
      - P2 相当の処理 (WITMOTION スキャン & 接続 & 通知購読)
  - PART2 スレッド:
      - P1 相当の処理 (POLAR H10 スキャン & 接続 & 心拍/ECG ストリーミング)
  - PART3 スレッド:
      - HTTP サーバ (0.0.0.0:7001, /realtime のみ 200, それ以外は 404)

[インストール手順 (例: Ubuntu 24.04 + Python 3.11)]
  1) 必要なパッケージ (BlueZ など)
     sudo apt update
     sudo apt install -y python3.11 python3.11-venv bluetooth bluez bluez-tools

  2) venv の作成
     python3.11 -m venv .venv
     source .venv/bin/activate

  3) Python ライブラリのインストール
     pip install --upgrade pip
     pip install bleak bleakheart

  4) 本ファイル dn_pysensors_server1.py を保存し、実行権限を付与 (任意)
     chmod +x dn_pysensors_server1.py

  5) 実行
     # センサー ([A], [B], [C]) の電源 ON・ペアリング済みであることを確認してから:
     ./dn_pysensors_server1.py
     # または
     python dn_pysensors_server1.py

[注意]
  - プログラム終了はユーザーが Ctrl+C (KeyboardInterrupt) で行う前提。
  - メモリ上に保持するデータは「直近60秒分」のみ。
  - デバイスが 30 秒以上データを送っていない場合は「CurrentConnectedDevices」から除外。
"""

import asyncio
import datetime
import json
import random
import socket
import subprocess
import sys
import threading
import time
import traceback
from typing import Any, Dict, List, Optional, Tuple

from bleak import BleakClient, BleakScanner
from bleakheart import HeartRate, PolarMeasurementData

# ============================================================================
# 共有データ構造 (PART1, PART2, PART3 で共通に利用する)
# ============================================================================

# 型エイリアス (Python 辞書で C# のクラス構造を表現する)
# ---------------------------------------------------------------------------
# PySensorAxisFrameData:
#   - [DATA1] における軸センサーデータ1フレーム分の構造
PySensorAxisFrameData = Dict[str, float]

# PySensorHeartData:
#   - [DATA1] における心拍データ1フレーム分の構造
PySensorHeartData = Dict[str, int | float]

# PySensorFrame:
#   - [DATA1] の PySensorFrame に相当
#   - 追加で内部利用用に "_epoch" (float, time.time() 秒) を持つ
PySensorFrame = Dict[str, Any]

# PySensorDevice:
#   - [DATA1] の PySensorDevice に相当
PySensorDevice = Dict[str, Any]

# 共有状態
FRAME_HISTORY: List[PySensorFrame] = []  # 直近60秒分のフレーム
DEVICE_LAST_SEEN: Dict[str, Dict[str, Any]] = {}  # key: DeviceId (MAC, 大文字)

# ロック: FRAME_HISTORY / DEVICE_LAST_SEEN / FRAME_COUNTER へのアクセスを保護
HISTORY_LOCK = threading.Lock()

# 全フレームで共通の連番 (プログラム起動からのフレーム数)
FRAME_COUNTER: int = 0

# データ保持期間 (秒)
HISTORY_MAX_AGE_SEC = 60.0

# デバイスを「接続中」とみなす閾値 (秒)
DEVICE_ALIVE_THRESHOLD_SEC = 30.0


# ============================================================================
# 共通ユーティリティ
# ============================================================================

def utc_now_epoch_and_str() -> Tuple[float, str]:
    """
    現在の UTC 時刻を取得するユーティリティ。

    Returns
    -------
    (epoch_sec, timestamp_str)
      epoch_sec:
        time.time() と同様の float (秒)。内部保持用。
      timestamp_str:
        「YYYYMMDD_HHMMSS.MMM」形式の文字列。JSON出力用。
    """
    now = datetime.datetime.utcnow()
    epoch = time.time()
    # ミリ秒を 3 桁でゼロ埋め
    ms = int(now.microsecond / 1000)
    ts = now.strftime("%Y%m%d_%H%M%S") + f".{ms:03d}"
    return epoch, ts


def infer_witmotion_device_type(name: str) -> str:
    """
    WITMOTION デバイス名から DeviceType を推定する。

    Parameters
    ----------
    name : str
        BLE デバイスの名前 (例: "WT901BLE5", "WT9011DCL_xxx" など)

    Returns
    -------
    str
        "WT901BLECL" または "WT9011DCL" のいずれか。
        判別できない場合は "WT901BLECL" をデフォルトとする。
    """
    name_u = (name or "").upper()
    if "9011" in name_u or "DCL" in name_u:
        return "WT9011DCL"
    return "WT901BLECL"


def _prune_old_frames_locked(now_epoch: float) -> None:
    """
    FRAME_HISTORY から「now_epoch から HISTORY_MAX_AGE_SEC 秒より古い」フレームを
    すべて削除する。

    ※ _epoch が必ずしも昇順とは限らない（複数スレッドからの追加順のため）ので、
       リスト全体をフィルタリングする形に変更する。
    """
    threshold = now_epoch - HISTORY_MAX_AGE_SEC
    if not FRAME_HISTORY:
        return

    # in-place でフィルタリングして、古い要素を完全に捨てる
    FRAME_HISTORY[:] = [
        f for f in FRAME_HISTORY
        if f.get("_epoch", 0.0) >= threshold
    ]


def _update_device_last_seen_locked(
    device_type: str,
    device_id: str,
    timestamp_str: str,
    epoch: float,
) -> None:
    """
    DEVICE_LAST_SEEN を更新する (内部専用)。

    Parameters
    ----------
    device_type : str
        "Polar_H10" / "WT901BLECL" / "WT9011DCL" のいずれか。
    device_id : str
        Bluetooth MAC アドレス (大文字, "D5:2D:CF:03:96:E8" など)。
    timestamp_str : str
        「YYYYMMDD_HHMMSS.MMM」形式の文字列。LastDataReceivedTimeStamp に入る。
    epoch : float
        time.time() による秒単位 (内部保持用)。
    """
    DEVICE_LAST_SEEN[device_id] = {
        "DeviceType": device_type,
        "DeviceId": device_id,
        "LastDataReceivedTimeStamp": timestamp_str,
        "LastDataEpoch": epoch,
    }


def record_axis_frame(device_type: str, device_id: str, axis: PySensorAxisFrameData) -> None:
    """
    PART1 (WITMOTION) からの 1 フレーム分の軸データを共有履歴に登録する。

    Parameters
    ----------
    device_type : str
        "WT901BLECL" または "WT9011DCL".
    device_id : str
        Bluetooth MAC アドレス (大文字)。
    axis : PySensorAxisFrameData
        1 フレーム分の軸センサーデータ (ax_m_s2, ay_m_s2, ...)

    Returns
    -------
    None
    """
    global FRAME_COUNTER
    epoch, ts = utc_now_epoch_and_str()

    frame: PySensorFrame = {
        "FrameNumber": 0,  # 後でロック内で設定
        "TimeStamp": ts,
        "DeviceType": device_type,
        "DeviceId": device_id,
        "DataType": "Axis",
        "HeartData": None,
        "AxisData": {
            # キーは [DATA1] に合わせる
            "ax_m_s2": float(axis.get("ax_m_s2", 0.0)),
            "ay_m_s2": float(axis.get("ay_m_s2", 0.0)),
            "az_m_s2": float(axis.get("az_m_s2", 0.0)),
            "pitch_deg": float(axis.get("pitch_deg", 0.0)),
            "yaw_deg": float(axis.get("yaw_deg", 0.0)),
            "roll_deg": float(axis.get("roll_deg", 0.0)),
            "wx_deg_s": float(axis.get("wx_deg_s", 0.0)),
            "wy_deg_s": float(axis.get("wy_deg_s", 0.0)),
            "wz_deg_s": float(axis.get("wz_deg_s", 0.0)),
        },
        "_epoch": epoch,  # 内部用
    }

    with HISTORY_LOCK:
        FRAME_COUNTER += 1
        frame["FrameNumber"] = FRAME_COUNTER
        FRAME_HISTORY.append(frame)
        _update_device_last_seen_locked(device_type, device_id, ts, epoch)
        _prune_old_frames_locked(epoch)


def record_heart_frame(device_id: str, bpm: float, rr_ms: float, internal_ts: int) -> None:
    """
    PART2 (POLAR H10) からの 1 フレーム分の心拍データを共有履歴に登録する。

    Parameters
    ----------
    device_id : str
        Bluetooth MAC アドレス (大文字)。
    bpm : float
        心拍数 (bpm)。
    rr_ms : float
        R-R 間隔 (ms)。
    internal_ts : int
        Polar H10 の内部タイムスタンプ (ナノ秒など, [P1_OUTPUT] の "t" 相当)。

    Returns
    -------
    None
    """
    global FRAME_COUNTER
    epoch, ts = utc_now_epoch_and_str()

    # C# 側の型が int なので丸めて int 化
    bpm_i = int(round(bpm))
    rr_i = int(round(rr_ms))

    frame: PySensorFrame = {
        "FrameNumber": 0,  # 後でロック内で設定
        "TimeStamp": ts,
        "DeviceType": "Polar_H10",
        "DeviceId": device_id,
        "DataType": "Heart",
        "HeartData": {
            "Bpm": bpm_i,
            "RrMsecs": rr_i,
            "InternalTimeStamp": int(internal_ts),
        },
        "AxisData": None,
        "_epoch": epoch,
    }

    with HISTORY_LOCK:
        FRAME_COUNTER += 1
        frame["FrameNumber"] = FRAME_COUNTER
        FRAME_HISTORY.append(frame)
        _update_device_last_seen_locked("Polar_H10", device_id, ts, epoch)
        _prune_old_frames_locked(epoch)


def build_history_json_text() -> str:
    """
    現在の 60 秒分のデータを JSON テキスト (text/plain) として構築する。

    Returns
    -------
    str
        [JSON1] と同等の JSON (indent=2, 改行入り)。
        - CurrentTime
        - CurrentConnectedDevices[]
        - ListOfData[] (PySensorFrame)
        - ListOfData は TimeStamp 降順 (最新が先頭) となる。
    """
    now_epoch, now_ts = utc_now_epoch_and_str()

    with HISTORY_LOCK:
        # CurrentConnectedDevices を作成 (30 秒以内にデータを受信したデバイスのみ)
        devices: List[PySensorDevice] = []
        for dev in DEVICE_LAST_SEEN.values():
            if now_epoch - dev["LastDataEpoch"] <= DEVICE_ALIVE_THRESHOLD_SEC:
                devices.append(
                    {
                        "DeviceType": dev["DeviceType"],
                        "DeviceId": dev["DeviceId"],
                        "LastDataReceivedTimeStamp": dev["LastDataReceivedTimeStamp"],
                    }
                )

        # ListOfData: 60 秒以内のフレームを TimeStamp 降順に並べる
        frames_desc: List[Dict[str, Any]] = []
        for frame in reversed(FRAME_HISTORY):
            if now_epoch - frame["_epoch"] > HISTORY_MAX_AGE_SEC:
                continue
            frames_desc.append(
                {
                    "FrameNumber": frame["FrameNumber"],
                    "TimeStamp": frame["TimeStamp"],
                    "DeviceType": frame["DeviceType"],
                    "DeviceId": frame["DeviceId"],
                    "DataType": frame["DataType"],
                    # None の場合は JSON ではフィールドを省略してよいが、
                    # Newtonsoft.Json での互換性を優先して null をそのまま出しても問題ない。
                    "HeartData": frame["HeartData"],
                    "AxisData": frame["AxisData"],
                }
            )

    history_obj = {
        "CurrentTime": now_ts,
        "CurrentConnectedDevices": devices,
        "ListOfData": frames_desc,
    }

    # indent=2 で [JSON1] と同様に見やすい整形 JSON を返す
    return json.dumps(history_obj, ensure_ascii=False, indent=2)


# ============================================================================
# PART1: WITMOTION WT901BLECL / WT9011DCL ([A], [B]) の処理 (P2 ベース)
# ============================================================================

# WITMOTION 用定数設定
# ---------------------------------------------------------------------------
PART1_BASE_INTERVAL_SEC = 5.0  # スキャン / 再接続間隔の基準値 (秒)
PART1_JITTER_RATIO = 0.3       # ±30% のジッタ
PART1_CONNECT_TIMEOUT_SEC = 5.0

# WITMOTION 固有 UUID
WITMOTION_SERVICE_UUID = "0000ffe5-0000-1000-8000-00805f9a34fb"
WITMOTION_NOTIFY_CHAR_UUID = "0000ffe4-0000-1000-8000-00805f9a34fb"
WITMOTION_WRITE_CHAR_UUID = "0000ffe9-0000-1000-8000-00805f9a34fb"

# デバイス名のプレフィックス
PART1_DEVICE_NAME_PREFIX = "WT901BLE"

# 出力レート設定
RATE_CODE_TABLE: Dict[int, int] = {
    10: 0x06,
    20: 0x07,
    50: 0x08,
    100: 0x09,
    200: 0x0A,
}
PART1_DEFAULT_TARGET_HZ = 50  # デフォルトの要求周波数 (Hz)


def part1_jittered_interval(base: float = PART1_BASE_INTERVAL_SEC) -> float:
    """
    PART1 用: 5 秒 ± 30% のような乱数付きインターバルを返す。

    Returns
    -------
    float
        待機秒数 (最小 0.1 秒)。
    """
    r = random.uniform(1.0 - PART1_JITTER_RATIO, 1.0 + PART1_JITTER_RATIO)
    return max(0.1, base * r)


def part1_bluetoothctl_cmd(*args: str) -> str:
    """
    bluetoothctl を 1 回だけ起動してコマンドを実行し、
    標準出力を文字列で返すユーティリティ。

    - 失敗した場合は例外を投げず、標準エラーに出力して空文字列を返す。
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
            "[PART1] [bluetoothctl] コマンドが見つかりません。Bluetooth クリーンアップをスキップします。",
            file=sys.stderr,
            flush=True,
        )
        return ""
    except Exception as e:
        print(f"[PART1] [bluetoothctl] 実行中に例外発生: {e!r}", file=sys.stderr, flush=True)
        traceback.print_exc()
        return ""


def part1_force_disconnect_witmotion_devices() -> None:
    """
    BlueZ に登録されている WT901BLE 系デバイスを列挙し、
    すべて bluetoothctl disconnect しておく。

    - プログラム起動直後に呼び出し、
      前回終了時に残っている接続をクリーンアップする目的。
    """
    out = part1_bluetoothctl_cmd("devices")
    if not out:
        return

    targets: List[Tuple[str, str]] = []
    for line in out.splitlines():
        line = line.strip()
        if not line.startswith("Device "):
            continue
        parts = line.split()
        if len(parts) < 3:
            continue
        mac = parts[1]
        name = " ".join(parts[2:])
        if name.startswith(PART1_DEVICE_NAME_PREFIX):
            targets.append((mac, name))

    if not targets:
        return

    print("[PART1] bluetoothctl で WT901BLE 系デバイスの接続をクリーンアップします...", flush=True)
    for mac, name in targets:
        print(f"[PART1]   -> disconnect {mac} ({name})", flush=True)
        part1_bluetoothctl_cmd("disconnect", mac)


def decode_imu_packet(data: bytes) -> Optional[PySensorAxisFrameData]:
    """
    WITMOTION BLE 5.0 プロトコルのデフォルト IMU パケット (Flag=0x61) をデコードする。

    Parameters
    ----------
    data : bytes
        Notify で受信した 1 パケット (20 bytes 以上を想定)。

    Returns
    -------
    Optional[PySensorAxisFrameData]
        正常にデコードできた場合は軸データの辞書、
        想定外・他種パケットの場合は None。
    """
    if len(data) < 20:
        return None
    if data[0] != 0x55:
        return None
    if data[1] != 0x61:
        # IMU パケット以外は無視
        return None

    raw_values: List[int] = []
    for i in range(2, 20, 2):
        raw = int.from_bytes(data[i:i + 2], byteorder="little", signed=True)
        raw_values.append(raw)
    if len(raw_values) != 9:
        return None

    ax_raw, ay_raw, az_raw, wx_raw, wy_raw, wz_raw, roll_raw, pitch_raw, yaw_raw = raw_values

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


async def part1_configure_output_rate(
    client: BleakClient,
    mac: str,
    target_hz: int = PART1_DEFAULT_TARGET_HZ,
) -> None:
    """
    WITMOTION センサ内部の出力周波数を設定する。

    Parameters
    ----------
    client : BleakClient
        接続済みの BLE クライアント。
    mac : str
        MAC アドレス (ログ用)。
    target_hz : int
        希望する出力周波数 (10,20,50,100,200 のいずれか)。

    Returns
    -------
    None
    """
    code = RATE_CODE_TABLE.get(target_hz)
    if code is None:
        print(f"[PART1] [{mac}] 指定した出力レート {target_hz} Hz には対応していません", file=sys.stderr, flush=True)
        return

    try:
        print(f"[PART1] [{mac}] 出力レートを {target_hz} Hz に設定します", flush=True)

        unlock_cmd = bytes([0xFF, 0xAA, 0x69, 0x88, 0xB5])
        await client.write_gatt_char(WITMOTION_WRITE_CHAR_UUID, unlock_cmd, response=True)
        await asyncio.sleep(0.05)

        rate_cmd = bytes([0xFF, 0xAA, 0x03, code, 0x00])
        await client.write_gatt_char(WITMOTION_WRITE_CHAR_UUID, rate_cmd, response=True)
        await asyncio.sleep(0.05)

        save_cmd = bytes([0xFF, 0xAA, 0x00, 0x00, 0x00])
        await client.write_gatt_char(WITMOTION_WRITE_CHAR_UUID, save_cmd, response=True)
        await asyncio.sleep(0.05)

        print(f"[PART1] [{mac}] 出力レート設定完了 ({target_hz} Hz)", flush=True)
    except Exception as e:
        print(f"[PART1] [{mac}] 出力レート設定中に例外発生: {e!r}", file=sys.stderr, flush=True)
        traceback.print_exc()


class Part1DeviceRegistry:
    """
    PART1 用: すでにデバイスタスクを起動した MAC アドレスを記録するクラス。

    - key: MAC アドレス (大文字)
    - value: スレッドオブジェクト
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


async def part1_run_notification_loop(
    client: BleakClient,
    mac: str,
    device_label: str,
    device_type: str,
) -> None:
    """
    接続済み WITMOTION クライアントに対して Notify を開始し、
    切断されるまでデータ受信を続ける。

    Parameters
    ----------
    client : BleakClient
        接続済みクライアント。
    mac : str
        MAC アドレス (大文字)。
    device_label : str
        ログ表示用のデバイス名。
    device_type : str
        "WT901BLECL" / "WT9011DCL" のいずれか。
    """
    latest_error: Optional[BaseException] = None

    def handle_notification(sender: int, data: bytes) -> None:
        """
        Notify キャラクタリスティックから 1 パケット受信するごとに呼ばれるコールバック。

        - デコードに成功した IMU データを record_axis_frame() で共有履歴に登録する。
        - 1サンプルごとのコンソール出力は行わない (画面が煩雑になるのを避ける)。
        """
        nonlocal latest_error
        try:
            sample = decode_imu_packet(data)
            if sample is None:
                return
            record_axis_frame(device_type, mac, sample)
        except Exception as ex:
            latest_error = ex
            print(f"[PART1] [{mac}] 通知処理中に例外発生: {ex!r}", file=sys.stderr, flush=True)
            traceback.print_exc()

    print(f"[PART1] [{mac}] Notify 開始: char={WITMOTION_NOTIFY_CHAR_UUID}", flush=True)
    await client.start_notify(WITMOTION_NOTIFY_CHAR_UUID, handle_notification)

    try:
        while True:
            if not client.is_connected:
                print(f"[PART1] [{mac}] 接続が切断されました (is_connected=False)", flush=True)
                break
            await asyncio.sleep(1.0)
            if latest_error is not None:
                print(f"[PART1] [{mac}] 直近の通知処理例外: {latest_error!r}", file=sys.stderr, flush=True)
                latest_error = None
    finally:
        try:
            print(f"[PART1] [{mac}] Notify 停止", flush=True)
            await client.stop_notify(WITMOTION_NOTIFY_CHAR_UUID)
        except Exception:
            pass


async def part1_device_session(address: str, name: Optional[str]) -> None:
    """
    1 つの WITMOTION デバイスに対するメイン処理 (非同期)。

    - 大ループ:
        - 接続試行を繰り返す (5 秒 ±30% 間隔)。
    - 小ループ:
        - 接続成功後、Notify を購読し続けて軸データを record_axis_frame() で登録。

    Parameters
    ----------
    address : str
        BLE デバイスのアドレス。
    name : Optional[str]
        BLE デバイス名。
    """
    mac = address.upper()
    device_label = f"{name or 'WT901BLE'} ({mac})"
    device_type = infer_witmotion_device_type(name or "")

    while True:
        delay = part1_jittered_interval(PART1_BASE_INTERVAL_SEC)
        try:
            print(f"[PART1] [{mac}] 接続試行開始 (timeout={PART1_CONNECT_TIMEOUT_SEC}s) name={name!r}", flush=True)
            async with BleakClient(address, timeout=PART1_CONNECT_TIMEOUT_SEC) as client:
                if not client.is_connected:
                    raise RuntimeError("BleakClient.is_connected が False です")

                print(f"[PART1] [{mac}] 接続成功: {device_label}", flush=True)

                await part1_configure_output_rate(client, mac, target_hz=PART1_DEFAULT_TARGET_HZ)

                await part1_run_notification_loop(client, mac, device_label, device_type)
        except asyncio.CancelledError:
            raise
        except Exception as e:
            print(f"[PART1] [{mac}] 接続または受信中に例外発生: {e!r}", file=sys.stderr, flush=True)
            traceback.print_exc()

        print(f"[PART1] [{mac}] 再接続まで {delay:.1f} 秒待機します", flush=True)
        try:
            await asyncio.sleep(delay)
        except asyncio.CancelledError:
            raise


def part1_device_task_thread(address: str, name: Optional[str]) -> None:
    """
    PART1 用デバイスタスクスレッドのエントリポイント。

    - 各 WITMOTION デバイスごとに 1 スレッド。
    - スレッド内に専用の asyncio イベントループを持つ。
    """
    try:
        asyncio.run(part1_device_session(address, name))
    except KeyboardInterrupt:
        print(f"[PART1] [{address}] device_task_thread: KeyboardInterrupt 受信、スレッド終了", flush=True)
    except Exception as e:
        print(f"[PART1] [{address}] device_task_thread: 予期しない例外で終了: {e!r}", file=sys.stderr, flush=True)
        traceback.print_exc()


async def part1_scan_once_and_spawn_tasks(registry: Part1DeviceRegistry) -> None:
    """
    1 回分の BLE スキャンを実行し、新規 WITMOTION デバイスごとに
    デバイスタスクスレッドを起動する。

    Parameters
    ----------
    registry : Part1DeviceRegistry
        すでにタスクを起動したデバイスの MAC を保持するレジストリ。
    """
    print("[PART1] BLE スキャン開始...", flush=True)

    try:
        # 新しい bleak (>=0.19) 対応
        scan_result = await BleakScanner.discover(timeout=4.0, return_adv=True)
        items = list(scan_result.values())  # [(BLEDevice, AdvertisementData), ...]
    except TypeError:
        # 古いバージョンの場合
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

            is_candidate = False
            if name.startswith(PART1_DEVICE_NAME_PREFIX):
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
                continue

            print(f"[PART1] [{mac}] 新しい WITMOTION BLE センサを検出: name={name!r}", flush=True)

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


def part1_scan_task_thread(registry: Part1DeviceRegistry) -> None:
    """
    PART1 用スキャンタスクスレッドのエントリポイント。

    - 永久ループで BLE スキャンを繰り返し、新規デバイスがあればタスクを起動する。
    """
    while True:
        try:
            asyncio.run(part1_scan_once_and_spawn_tasks(registry))
        except KeyboardInterrupt:
            print("[PART1] scan_task_thread: KeyboardInterrupt を無視して継続します", file=sys.stderr, flush=True)
        except Exception as e:
            print(f"[PART1] scan_task_thread: スキャン中に例外発生: {e!r}", file=sys.stderr, flush=True)
            traceback.print_exc()

        delay = part1_jittered_interval(PART1_BASE_INTERVAL_SEC)
        print(f"[PART1] 次のスキャンまで {delay:.1f} 秒待機します", flush=True)
        try:
            time.sleep(delay)
        except KeyboardInterrupt:
            print("[PART1] scan_task_thread: KeyboardInterrupt 受信、スレッド終了", flush=True)
            return


def part1_main_thread() -> None:
    """
    PART1 全体のスレッドエントリーポイント。

    - 起動時に前回の接続を bluetoothctl でクリーンアップ。
    - スキャンタスクスレッドを起動。
    - 自身は 60秒ごとに起動済みデバイス一覧の簡易ログを出す。
    """
    print("[PART1] WITMOTION センサ監視スレッド起動", flush=True)

    part1_force_disconnect_witmotion_devices()

    registry = Part1DeviceRegistry()
    scan_thread = threading.Thread(
        target=part1_scan_task_thread,
        args=(registry,),
        daemon=True,
    )
    scan_thread.start()

    try:
        while True:
            time.sleep(60.0)
            threads = registry.get_device_threads()
            if threads:
                names = ", ".join(sorted(threads.keys()))
                print(f"[PART1] 現在アクティブなデバイスタスク: {names}", flush=True)
            else:
                print("[PART1] まだデバイスタスクは起動していません", flush=True)
    except KeyboardInterrupt:
        print("\n[PART1] KeyboardInterrupt を受信しました。PART1 スレッドを終了します。", flush=True)


# ============================================================================
# PART2: POLAR H10 ([C]) の処理 (P1 ベース)
# ============================================================================

# PART2 用定数
POLAR_CONNECT_TIMEOUT: float = 5.0
POLAR_RETRY_INTERVAL: float = 5.0
POLAR_POLL_INTERVAL: float = 0.1  # 100 ms


async def part2_find_polar_device() -> Optional[Any]:
    """
    Polar デバイス（名前に "polar" を含むもの）を 1 台スキャンして返す。

    Returns
    -------
    Optional[Any]
        成功時: BLEDevice, 失敗時: None
    """
    print(f"[PART2] [SCAN] Searching for POLAR device (timeout={POLAR_CONNECT_TIMEOUT}s)...", flush=True)

    try:
        device = await BleakScanner.find_device_by_filter(
            lambda dev, adv: dev.name and "polar" in dev.name.lower(),
            timeout=POLAR_CONNECT_TIMEOUT,
        )
    except Exception as e:
        print(f"[PART2] [SCAN] Error during scan: {e!r}", file=sys.stderr, flush=True)
        return None

    if device is None:
        print("[PART2] [SCAN] POLAR device not found.", flush=True)
    else:
        print(f"[PART2] [SCAN] Found device: {device.name} ({device.address})", flush=True)

    return device


async def part2_small_loop(
    client: BleakClient,
    hr_queue: "asyncio.Queue[Tuple]",
    ecg_queue: "asyncio.Queue[Tuple]",
    device_mac: str,
) -> None:
    """
    PART2 用「小さなメインループ」: 100ms ごとに心拍データを処理し続ける。

    - 100ms ごとに hr_queue から心拍フレームを 1 つ取り出し、record_heart_frame() で登録
    - その間に溜まった ECG フレームは読み出して破棄 (画面出力はしない)
    - 例外が発生した場合はループを抜け、外側の大ループで再接続

    Parameters
    ----------
    client : BleakClient
        接続済みクライアント。
    hr_queue : asyncio.Queue
        HeartRate フレームを受け取るキュー。
    ecg_queue : asyncio.Queue
        ECG フレームを受け取るキュー。
    device_mac : str
        Polar H10 の MAC アドレス (大文字)。
    """
    print("[PART2] [LOOP] Entering small loop (100 ms polling).", flush=True)

    while client.is_connected:
        try:
            # 100ms の間に心拍フレームを 1 つ待つ
            try:
                frame = await asyncio.wait_for(hr_queue.get(), timeout=POLAR_POLL_INTERVAL)
            except asyncio.TimeoutError:
                frame = None

            if frame is not None:
                try:
                    tag, tstamp, payload, energy = frame
                    if tag == "HR" and payload is not None:
                        bpm, rr_ms = payload
                        record_heart_frame(device_mac, bpm, rr_ms, tstamp)
                    # 想定外形式は無視 (画面出力しない)
                except Exception as e:
                    print(f"[PART2] [HR ] Failed to decode frame {frame!r}: {e}", file=sys.stderr, flush=True)
                finally:
                    hr_queue.task_done()

            # この 100ms 中に溜まった ECG フレームを全部取り出して捨てる
            while not ecg_queue.empty():
                _ = ecg_queue.get_nowait()
                ecg_queue.task_done()
        except Exception as e:
            print(f"[PART2] [LOOP] Error in small loop: {e!r}", file=sys.stderr, flush=True)
            break

    print("[PART2] [LOOP] Leaving small loop (disconnected or error).", flush=True)


async def part2_connect_and_stream(device: Any) -> None:
    """
    1 回の「接続～ストリーミング～切断」サイクルを実行する。

    - BleakClient で接続（タイムアウト 5 秒）
    - HeartRate / PolarMeasurementData を設定し、HR + ECG をキューへ
    - part2_small_loop() を回して心拍データを record_heart_frame() で登録
    - 例外や切断があれば終了し、大ループ側に制御を返す
    """
    print(f"[PART2] [CONN] Trying to connect to {device} ...", flush=True)

    hr_queue: asyncio.Queue = asyncio.Queue()
    ecg_queue: asyncio.Queue = asyncio.Queue()

    def _disconnected_callback(client: BleakClient) -> None:
        print("[PART2] [CONN] Sensor disconnected (disconnected_callback).", flush=True)

    try:
        async with BleakClient(
            device,
            disconnected_callback=_disconnected_callback,
            timeout=POLAR_CONNECT_TIMEOUT,
        ) as client:
            print(
                f"[PART2] [CONN] Connected: {client.is_connected}  "
                f"name={client.name}  addr={client.address}",
                flush=True,
            )
            if not client.is_connected:
                print("[PART2] [CONN] client.is_connected is False, aborting this cycle.", flush=True)
                return

            heartrate = HeartRate(
                client,
                queue=hr_queue,
                instant_rate=False,
                unpack=True,
            )

            pmd = PolarMeasurementData(client, ecg_queue=ecg_queue)

            try:
                settings = await pmd.available_settings("ECG")
                print("[PART2] [PMD] ECG settings reported by device:", flush=True)
                for k, v in settings.items():
                    print(f"[PART2]        {k}: {v}", flush=True)
            except Exception as e:
                print(f"[PART2] [PMD] Could not read ECG settings: {e!r}", file=sys.stderr, flush=True)

            try:
                await heartrate.start_notify()
                print("[PART2] [HR ] Heart rate notifications started.", flush=True)
            except Exception as e:
                print(f"[PART2] [HR ] Failed to start heart rate notifications: {e!r}", file=sys.stderr, flush=True)

            try:
                err_code, err_msg, _ = await pmd.start_streaming("ECG")
                if err_code != 0:
                    print(
                        f"[PART2] [PMD] start_streaming('ECG') error {err_code}: {err_msg}",
                        file=sys.stderr,
                        flush=True,
                    )
                else:
                    print("[PART2] [PMD] ECG streaming started.", flush=True)
            except Exception as e:
                print(f"[PART2] [PMD] Failed to start ECG streaming: {e!r}", file=sys.stderr, flush=True)

            await part2_small_loop(client, hr_queue, ecg_queue, device.address.upper())

            if client.is_connected:
                print("[PART2] [CONN] Stopping notifications / streams ...", flush=True)
                try:
                    await heartrate.stop_notify()
                except Exception as e:
                    print(f"[PART2] [HR ] Error in stop_notify: {e!r}", file=sys.stderr, flush=True)

                try:
                    await pmd.stop_streaming("ECG")
                except Exception as e:
                    print(f"[PART2] [PMD] Error in stop_streaming('ECG'): {e!r}", file=sys.stderr, flush=True)

            print("[PART2] [CONN] Connection closed. Returning to outer loop.", flush=True)

    except Exception as e:
        print(f"[PART2] [CONN] Exception in connect_and_stream: {e!r}", file=sys.stderr, flush=True)


async def part2_monitor_polar_forever() -> None:
    """
    PART2 用「大ループ」:
      - 永久に Polar H10 への接続を試み続ける。

    - 5 秒タイムアウトで Polar デバイスをスキャン
    - 見つからない／接続に失敗／通信中にエラー or 切断 → 5 秒待って再試行
    - いったん接続できたら part2_connect_and_stream() が戻るまで待つ
    """
    print("[PART2] [MAIN] Starting POLAR H10 monitor.", flush=True)

    while True:
        try:
            device = await part2_find_polar_device()
            if device is None:
                print(f"[PART2] [MAIN] Will retry scan in {POLAR_RETRY_INTERVAL} seconds.", flush=True)
                await asyncio.sleep(POLAR_RETRY_INTERVAL)
                continue

            await part2_connect_and_stream(device)
        except Exception as e:
            print(f"[PART2] [MAIN] Exception in outer loop: {e!r}", file=sys.stderr, flush=True)

        print(f"[PART2] [MAIN] Reconnecting in {POLAR_RETRY_INTERVAL} seconds ...", flush=True)
        await asyncio.sleep(POLAR_RETRY_INTERVAL)


def part2_main_thread() -> None:
    """
    PART2 全体のスレッドエントリーポイント。

    - asyncio のイベントループ上で part2_monitor_polar_forever() を動かし続ける。
    - プログラム終了はメインプロセスの Ctrl+C に依存。
    """
    try:
        asyncio.run(part2_monitor_polar_forever())
    except KeyboardInterrupt:
        print("\n[PART2] [MAIN] KeyboardInterrupt received, exiting.", flush=True)


# ============================================================================
# PART1 / PART2 統計出力スレッド
# ============================================================================

def part1_stats_thread() -> None:
    """
    PART1 の統計情報を 1 秒に 1 回コンソール出力するスレッド。

    出力形式:
      PART1: NUM_DEVICES = <生きている軸デバイス数>, DATA_PER_SECOND = <直近1秒のAxisフレーム数合計>
    """
    while True:
        time.sleep(1.0)
        now = time.time()
        with HISTORY_LOCK:
            # NUM_DEVICES: DeviceType が WITMOTION 系 & 30秒以内にデータを受信したデバイス数
            num_devices = 0
            for dev in DEVICE_LAST_SEEN.values():
                if dev["DeviceType"] in ("WT901BLECL", "WT9011DCL"):
                    if now - dev["LastDataEpoch"] <= DEVICE_ALIVE_THRESHOLD_SEC:
                        num_devices += 1

            # DATA_PER_SECOND: DataType == "Axis" かつ 1秒以内のフレーム数
            data_per_sec = sum(
                1
                for f in FRAME_HISTORY
                if f["DataType"] == "Axis" and now - f["_epoch"] <= 1.0
            )

        print(f"PART1: NUM_DEVICES = {num_devices}, DATA_PER_SECOND = {data_per_sec}", flush=True)


def part2_stats_thread() -> None:
    """
    PART2 の統計情報を 1 秒に 1 回コンソール出力するスレッド。

    出力形式:
      PART2: NUM_DEVICES = <Polarデバイス数(0 or 1)>, DATA_PER_SECOND = <直近1秒のHeartフレーム数>
    """
    while True:
        time.sleep(1.0)
        now = time.time()
        with HISTORY_LOCK:
            num_devices = 0
            for dev in DEVICE_LAST_SEEN.values():
                if dev["DeviceType"] == "Polar_H10":
                    if now - dev["LastDataEpoch"] <= DEVICE_ALIVE_THRESHOLD_SEC:
                        num_devices += 1

            data_per_sec = sum(
                1
                for f in FRAME_HISTORY
                if f["DataType"] == "Heart" and now - f["_epoch"] <= 1.0
            )

        print(f"PART2: NUM_DEVICES = {num_devices}, DATA_PER_SECOND = {data_per_sec}", flush=True)


# ============================================================================
# PART3: HTTP サーバ (0.0.0.0:7001, /realtime)
# ============================================================================

HTTP_LISTEN_ADDR = "0.0.0.0"
HTTP_LISTEN_PORT = 7001
HTTP_RETRY_SEC = 5.0


def part3_handle_client(conn: socket.socket, addr: Tuple[str, int]) -> None:
    """
    1 クライアントの HTTP リクエストを処理する関数。

    Parameters
    ----------
    conn : socket.socket
        accept() で得たクライアントソケット。
    addr : (str, int)
        クライアントアドレス (host, port)。
    """
    try:
        conn.settimeout(5.0)
        data = b""
        while b"\r\n\r\n" not in data and len(data) < 8192:
            chunk = conn.recv(1024)
            if not chunk:
                break
            data += chunk

        if not data:
            return

        try:
            text = data.decode("ascii", errors="ignore")
            lines = text.split("\r\n")
            request_line = lines[0]
        except Exception:
            return

        parts = request_line.split()
        if len(parts) < 2:
            return

        method, path = parts[0], parts[1]

        if path in ("/realtime", "/realtime/"):
            body = build_history_json_text()
            body_bytes = body.encode("utf-8")
            headers = [
                "HTTP/1.1 200 OK",
                "Content-Type: text/plain; charset=utf-8",
                f"Content-Length: {len(body_bytes)}",
                "Connection: close",
                "",
                "",
            ]
            header_bytes = "\r\n".join(headers).encode("ascii")
            conn.sendall(header_bytes + body_bytes)
        else:
            body_bytes = b"404 Not Found\n"
            headers = [
                "HTTP/1.1 404 Not Found",
                "Content-Type: text/plain; charset=utf-8",
                f"Content-Length: {len(body_bytes)}",
                "Connection: close",
                "",
                "",
            ]
            header_bytes = "\r\n".join(headers).encode("ascii")
            conn.sendall(header_bytes + body_bytes)
    except Exception as e:
        print(f"[PART3] Error handling client {addr}: {e!r}", file=sys.stderr, flush=True)
    finally:
        try:
            conn.close()
        except Exception:
            pass


def part3_http_server_thread() -> None:
    """
    PART3: HTTP サーバスレッドのエントリーポイント。

    - 0.0.0.0:7001 を IPv4 で listen。
    - bind / listen に失敗した場合は 5 秒ごとに再試行 (クラッシュしない)。
    - /realtime または /realtime/ にアクセスされた場合のみ 200 + JSON、
      その他は 404 Not Found。
    """
    while True:
        sock = None
        try:
            sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            sock.bind((HTTP_LISTEN_ADDR, HTTP_LISTEN_PORT))
            sock.listen(64)
            print(f"[PART3] HTTP server listening on {HTTP_LISTEN_ADDR}:{HTTP_LISTEN_PORT}", flush=True)

            while True:
                try:
                    conn, addr = sock.accept()
                except KeyboardInterrupt:
                    raise
                except Exception as e:
                    print(f"[PART3] accept() error: {e!r}", file=sys.stderr, flush=True)
                    continue

                t = threading.Thread(target=part3_handle_client, args=(conn, addr), daemon=True)
                t.start()
        except KeyboardInterrupt:
            print("[PART3] KeyboardInterrupt received. HTTP server thread exiting.", flush=True)
            break
        except OSError as e:
            print(
                f"[PART3] Failed to bind/listen on {HTTP_LISTEN_ADDR}:{HTTP_LISTEN_PORT}: {e!r}",
                file=sys.stderr,
                flush=True,
            )
            print(f"[PART3] Retry bind/listen in {HTTP_RETRY_SEC} seconds...", flush=True)
            time.sleep(HTTP_RETRY_SEC)
        except Exception as e:
            print(f"[PART3] Unexpected error in HTTP server thread: {e!r}", file=sys.stderr, flush=True)
            traceback.print_exc()
            print(f"[PART3] Retry bind/listen in {HTTP_RETRY_SEC} seconds...", flush=True)
            time.sleep(HTTP_RETRY_SEC)
        finally:
            if sock is not None:
                try:
                    sock.close()
                except Exception:
                    pass


# ============================================================================
# メインエントリーポイント
# ============================================================================

def main() -> None:
    """
    プログラムのメインエントリーポイント。

    - PART1 / PART2 / PART3 / 統計スレッドを起動し、以後は Ctrl+C まで待機する。
    """
    print("dn_pysensors_server1.py 起動", flush=True)
    print("  - PART1: WITMOTION WT901BLECL / WT9011DCL 軸センサ監視", flush=True)
    print("  - PART2: POLAR H10 心拍センサ監視", flush=True)
    print("  - PART3: HTTP サーバ (0.0.0.0:7001, /realtime)", flush=True)
    print("  - プログラムの終了はユーザーが Ctrl+C で行ってください。", flush=True)
    print("", flush=True)

    # PART1 スレッド
    t_part1 = threading.Thread(target=part1_main_thread, daemon=True)
    t_part1.start()

    # PART2 スレッド
    t_part2 = threading.Thread(target=part2_main_thread, daemon=True)
    t_part2.start()

    # PART3 (HTTP サーバ) スレッド
    t_part3 = threading.Thread(target=part3_http_server_thread, daemon=True)
    t_part3.start()

    # 統計スレッド
    t_stats1 = threading.Thread(target=part1_stats_thread, daemon=True)
    t_stats1.start()

    t_stats2 = threading.Thread(target=part2_stats_thread, daemon=True)
    t_stats2.start()

    # メインスレッドは待機のみ
    try:
        while True:
            time.sleep(1.0)
    except KeyboardInterrupt:
        print("\n[MAIN] KeyboardInterrupt を受信しました。プログラムを終了します。", flush=True)


if __name__ == "__main__":
    main()
