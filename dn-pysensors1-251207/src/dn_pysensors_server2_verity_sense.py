#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
dn_pysensors_server2_verity_sense.py

目的:
  (part2) Linux マシン上で BLE 心拍センサー「Polar Verity Sense」(最大1台) をスキャンして接続し、
          Heart Rate Measurement(0x2A37) の Notify で心拍数(BPM)を受信してメモリに 60 秒分保持する。
          併せて Battery Level(0x2A19) を 60 秒に 1 回程度取得し、各データフレームに含める。

  (part3) 0.0.0.0:7001 (IPv4) で簡易 HTTP サーバを起動し、/realtime (/realtime/) にアクセスが来たら、
          直近 60 秒分のデータを [DATA1] の PySensorFrameHistory と互換の JSON を text/plain で返す。
          それ以外のパスは 404 を返す。

重要仕様:
  - 起動後は Ctrl+C で強制終了されるまで常駐動作する。
  - part2 と part3 は別スレッドで動作する。
  - 共有データ構造に対する参照/更新は単純ロックで保護する。
  - /realtime 応答の ListOfData は TimeStamp 降順(新しいものが先頭)とする。
  - 60 秒より古い受信データは破棄し、メモリ使用量が増え続けないようにする。
  - Web サーバの bind/listen に失敗した場合でもクラッシュせず、5秒毎に再試行する。
  - Web クライアントは毎秒 100 回程度アクセスする可能性があるため、/realtime 応答生成は軽量化する
    (ListOfData はキャッシュ済み JSON を使って高速に返す)。

動作環境:
  - Ubuntu 24.04 / Raspberry Pi 4 想定
  - Python 3.11
  - bleak (BlueZ バックエンド)

インストール例:
  # Bluetooth サービスの準備(既に動作している場合は不要)
  sudo apt update
  sudo apt install -y bluez bluetooth

  # venv 作成(任意)
  python3 -m venv .venv
  source .venv/bin/activate

  # 必要 Python ライブラリ
  pip install --upgrade pip
  pip install bleak

実行:
  python3 dn_pysensors_server2_verity_sense.py

補足:
  - デバイスが見つからない場合は環境変数で絞り込みが可能:
      TARGET_NAME_KEYWORDS="Polar,Verity"
      TARGET_DEVICE_ADDRESS="AA:BB:CC:DD:EE:FF"
"""

from __future__ import annotations

import asyncio
import json
import os
import sys
import threading
import time
import traceback
from dataclasses import dataclass
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from typing import Any, Optional, Tuple, List

from bleak import BleakClient, BleakScanner
from bleak.backends.characteristic import BleakGATTCharacteristic
from bleak.backends.device import BLEDevice
from bleak.backends.scanner import AdvertisementData


# ----------------------------
# BLE GATT UUID 定義
# ----------------------------
HEART_RATE_SERVICE_UUID = "0000180d-0000-1000-8000-00805f9b34fb"
HEART_RATE_MEASUREMENT_UUID = "00002a37-0000-1000-8000-00805f9b34fb"

BATTERY_SERVICE_UUID = "0000180f-0000-1000-8000-00805f9b34fb"
BATTERY_LEVEL_UUID = "00002a19-0000-1000-8000-00805f9b34fb"


# ----------------------------
# 動作仕様パラメータ
# ----------------------------
SCAN_TIMEOUT_SECONDS = 5.0
CONNECT_TIMEOUT_SECONDS = 5.0
RETRY_INTERVAL_SECONDS = 5.0

BATTERY_POLL_INTERVAL_SECONDS = 60.0

# メモリ保持期間(受信からこの秒数を超えたフレームは破棄)
DATA_RETENTION_SECONDS = 60.0

# 「接続中」判定に使う秒数(最後の受信から 30 秒以内なら生きているとみなす)
DEVICE_ALIVE_SECONDS = 30.0

# 統計表示間隔
STATS_PRINT_INTERVAL_SECONDS = 1.0

# part3 HTTP
HTTP_BIND_HOST = "0.0.0.0"
HTTP_BIND_PORT = 7001
HTTP_RETRY_INTERVAL_SECONDS = 5.0

# JSON で出力する DeviceType / DataType 固定値
DEVICE_TYPE_POLAR_VERITY = "Polar_Verity"
DATA_TYPE_HEART = "Heart"


# ----------------------------
# ユーティリティ
# ----------------------------
def GetUtcTimeStampString() -> str:
    """
    UTC 現在時刻を、C# 側想定の形式 "YYYYMMDD_HHMMSS.MMM" で返す。

    Returns:
        str: 例) "20251208_112233.012"
    """
    now = datetime.now(timezone.utc)
    # %f はマイクロ秒(6桁)なので、末尾3桁を落としてミリ秒にする
    return now.strftime("%Y%m%d_%H%M%S.%f")[:-3]


def ClampInt(value: int, minValue: int, maxValue: int) -> int:
    """
    int 値を範囲内に丸める。

    Args:
        value: 入力値
        minValue: 下限
        maxValue: 上限

    Returns:
        int: minValue〜maxValue に丸めた値
    """
    if value < minValue:
        return minValue
    if value > maxValue:
        return maxValue
    return value


def PrintPart2Status(message: str) -> None:
    """part2 のステータスメッセージを標準出力に出す(必ず [PART2] プレフィックスを付与)。"""
    print(f"[PART2] {message}", flush=True)


def PrintPart2Error(message: str) -> None:
    """part2 のエラーメッセージを標準エラーに出す(必ず [PART2] プレフィックスを付与)。"""
    print(f"[PART2] {message}", file=sys.stderr, flush=True)


def PrintPart3Status(message: str) -> None:
    """part3 のステータスメッセージを標準出力に出す。"""
    print(f"[PART3] {message}", flush=True)


def PrintPart3Error(message: str) -> None:
    """part3 のエラーメッセージを標準エラーに出す。"""
    print(f"[PART3] {message}", file=sys.stderr, flush=True)


def ParseHeartRateMeasurement(data: bytes) -> Optional[Tuple[int, Tuple[float, ...]]]:
    """
    Heart Rate Measurement (0x2A37) の Notify ペイロードを解析して心拍数を返す。

    仕様(代表例):
      - data[0] は Flags
        bit0: 心拍値のサイズ (0=uint8, 1=uint16)
        bit3: Energy Expended 有無
        bit4: RR-Interval 有無
      - Flags の後ろに心拍値が続き、必要に応じて Energy Expended(2byte) と RR-Interval(2byte×N) が続く。

    Args:
        data: Notify で受信した生データ(bytes)

    Returns:
        Optional[Tuple[int, Tuple[float, ...]]]:
          解析成功なら (bpm, rrIntervalsMs)。
          解析不能なら None。
    """
    if not data or len(data) < 2:
        return None

    flags = data[0]
    isUint16 = (flags & 0x01) != 0
    hasEnergyExpended = (flags & 0x08) != 0
    hasRrInterval = (flags & 0x10) != 0

    offset = 1

    try:
        if isUint16:
            if offset + 2 > len(data):
                return None
            bpm = int.from_bytes(data[offset:offset + 2], byteorder="little", signed=False)
            offset += 2
        else:
            bpm = int(data[offset])
            offset += 1

        if hasEnergyExpended:
            if offset + 2 > len(data):
                return None
            offset += 2

        rrIntervalsMsList: List[float] = []
        if hasRrInterval:
            # RR-Interval は 1/1024秒 単位が標準。ミリ秒へ換算する。
            while offset + 2 <= len(data):
                rrRaw = int.from_bytes(data[offset:offset + 2], byteorder="little", signed=False)
                rrIntervalsMsList.append(rrRaw * 1000.0 / 1024.0)
                offset += 2

        return bpm, tuple(rrIntervalsMsList)
    except Exception:
        # 想定外フォーマットでも全体を落とさない
        return None


def IndentJsonForOuterObject(jsonText: str, outerIndentSpaces: int) -> str:
    """
    JSON 配列/オブジェクトの文字列を、外側 JSON の値として埋め込むためにインデントを調整する。

    例:
      outerIndentSpaces=2 のとき、内部 JSON の改行の直後に "  " を追加して
      次行以降が外側の 2 スペース分だけ右にずれるようにする。

    Args:
        jsonText: json.dumps(..., indent=2) などで生成した JSON 文字列
        outerIndentSpaces: 外側のインデント(スペース数)

    Returns:
        str: インデント調整済み文字列
    """
    if "\n" not in jsonText:
        return jsonText
    prefix = " " * outerIndentSpaces
    return jsonText.replace("\n", "\n" + prefix)


# ----------------------------
# 共有データ構造
# ----------------------------
@dataclass(frozen=True)
class FrameEntry:
    """
    受信フレームを内部で保持するためのエントリ。

    Attributes:
        ReceivedMonotonic: 受信時刻(単調増加クロック)。60秒保持の判定に使う。
        FrameJsonObject: C# の PySensorFrame 相当の JSON オブジェクト(dict)
    """
    ReceivedMonotonic: float
    FrameJsonObject: dict[str, Any]


class SharedSensorHistory:
    """
    part2 が書き込み、part3 が読み取る共有ヒストリ。

    - 単純ロックで保護する (R/Wロック不要)
    - /realtime 高速応答のため、ListOfData は JSON 文字列をキャッシュしておく
      (新規データ受信・古いデータ破棄のタイミングのみ再生成)
    """

    def __init__(self) -> None:
        self._lock = threading.Lock()

        # 起動後のフレーム連番
        self._nextFrameNumber: int = 0

        # 直近 60 秒分のフレーム(受信順に昇順で格納)
        self._frames: List[FrameEntry] = []

        # 直近にデータ受信できたデバイス(最大 1 台)
        self._deviceIdUpper: str = ""
        self._lastDataReceivedTimestamp: str = ""
        self._lastDataReceivedMonotonic: Optional[float] = None

        # 直近のバッテリー残量(0-100)。未取得の場合は None
        self._lastBatteryLevel: Optional[int] = None

        # キャッシュ済み JSON 断片
        # - ListOfData は「TimeStamp 降順」を満たすように反転した配列を JSON 化して保存する
        self._cachedListOfDataJson: str = "[]"
        self._cachedListOfDataJsonIndented: str = "[]"

        # - 生存判定を満たす場合に使うデバイス配列 JSON (デバイス死活判定はリクエスト時に行う)
        self._cachedConnectedDevicesJson: str = "[]"
        self._cachedConnectedDevicesJsonIndented: str = "[]"

    def SetBatteryLevel(self, batteryLevel: int) -> None:
        """
        直近のバッテリー残量を更新する。

        Args:
            batteryLevel: 0〜100 (0=電池切れ、100=フル)

        Returns:
            None
        """
        level = ClampInt(int(batteryLevel), 0, 100)
        with self._lock:
            self._lastBatteryLevel = level
            # バッテリー値だけでは過去フレームは更新しない仕様。
            # 新規受信フレームに対して、この値を埋め込む。

    def AddHeartRateFrame(self, deviceIdUpper: str, bpm: int) -> None:
        """
        心拍フレームを 1 件追加する(パート2からのみ呼ばれる想定)。

        ここで行うこと:
          - FrameNumber を採番
          - TimeStamp を付与(UTC, ms)
          - BatteryLevel(最後に取得した値)も HeartData に含める
          - 古いデータ(60秒超)を破棄
          - /realtime 高速応答のために JSON キャッシュを更新

        Args:
            deviceIdUpper: デバイス MAC アドレス(大文字)
            bpm: 心拍数(BPM)

        Returns:
            None
        """
        receivedMonotonic = time.monotonic()
        receivedTimestamp = GetUtcTimeStampString()

        with self._lock:
            frameNumber = self._nextFrameNumber
            self._nextFrameNumber += 1

            # BatteryLevel は「最後に読めた値」を全フレームに付与する。
            # 仕様上は 0〜100 のみなので、未取得時は 0 として扱う(=最悪ケースで互換性優先)。
            batteryLevel = self._lastBatteryLevel if self._lastBatteryLevel is not None else 0

            heartDataObject: dict[str, Any] = {
                "Bpm": int(bpm),
                "BatteryLevel": int(batteryLevel),
            }

            # 重要: JSON のフィールド名は [DATA1] の C# クラスと一致させる
            frameObject: dict[str, Any] = {
                "FrameNumber": int(frameNumber),
                "TimeStamp": receivedTimestamp,
                "DeviceType": DEVICE_TYPE_POLAR_VERITY,
                "DeviceId": deviceIdUpper,
                "DataType": DATA_TYPE_HEART,
                "HeartData": heartDataObject,
            }

            self._frames.append(FrameEntry(ReceivedMonotonic=receivedMonotonic, FrameJsonObject=frameObject))

            # デバイスの「最後に受信できた時刻」を更新
            self._deviceIdUpper = deviceIdUpper
            self._lastDataReceivedTimestamp = receivedTimestamp
            self._lastDataReceivedMonotonic = receivedMonotonic

            # 古いデータを破棄
            self._PurgeOldFramesLocked(nowMonotonic=receivedMonotonic)

            # JSON キャッシュ更新
            self._RebuildJsonCacheLocked()

    def PurgeOldFrames(self) -> None:
        """
        60 秒を超えた古いフレームを破棄する。
        (part2 の 1 秒タイマーから定期的に呼ばれ、無限増加を防ぐ)

        Returns:
            None
        """
        nowMonotonic = time.monotonic()
        with self._lock:
            removedAny = self._PurgeOldFramesLocked(nowMonotonic=nowMonotonic)
            if removedAny:
                self._RebuildJsonCacheLocked()

    def GetAliveDeviceCount(self) -> int:
        """
        「最後にデータを受信してから 30 秒以内」のデバイス数を返す。
        [C] は最大 1 台なので戻り値は 0 または 1。

        Returns:
            int: 0 または 1
        """
        nowMonotonic = time.monotonic()
        with self._lock:
            if not self._deviceIdUpper or self._lastDataReceivedMonotonic is None:
                return 0
            if (nowMonotonic - self._lastDataReceivedMonotonic) <= DEVICE_ALIVE_SECONDS:
                return 1
            return 0

    def BuildRealtimeJsonText(self) -> str:
        """
        part3 (/realtime) 応答用の JSON(text) を生成する。

        重要:
          - 生成は高速化のため、ListOfData はキャッシュ済み JSON を利用する。
          - CurrentTime はリクエスト時刻を使うため、ここで都度埋める。
          - CurrentConnectedDevices は「最後に受信から 30 秒以内」なら 1 件、そうでなければ []。

        Returns:
            str: JSON 文字列(改行・インデント付き)
        """
        currentTime = GetUtcTimeStampString()
        nowMonotonic = time.monotonic()

        # ロック内では「キャッシュ文字列と最低限の状態」だけ取得し、文字列結合はロック外で行う
        with self._lock:
            listOfDataJsonIndented = self._cachedListOfDataJsonIndented
            connectedDevicesJsonIndentedAlive = self._cachedConnectedDevicesJsonIndented

            deviceIdUpper = self._deviceIdUpper
            lastReceivedMonotonic = self._lastDataReceivedMonotonic

        isAlive = (
            bool(deviceIdUpper)
            and (lastReceivedMonotonic is not None)
            and ((nowMonotonic - lastReceivedMonotonic) <= DEVICE_ALIVE_SECONDS)
        )
        connectedDevicesJsonIndented = connectedDevicesJsonIndentedAlive if isAlive else "[]"

        # [JSON1] と同様に、改行入りの見やすい JSON を返す
        jsonText = (
            "{\n"
            f'  "CurrentTime": "{currentTime}",\n'
            f'  "CurrentConnectedDevices": {connectedDevicesJsonIndented},\n'
            f'  "ListOfData": {listOfDataJsonIndented}\n'
            "}"
        )
        return jsonText

    def _PurgeOldFramesLocked(self, nowMonotonic: float) -> bool:
        """
        ロック取得済みで呼び出すこと。
        古いフレームを先頭から削除する。

        Args:
            nowMonotonic: 現在の単調増加クロック

        Returns:
            bool: 何か削除したら True
        """
        removedAny = False
        # self._frames は受信順(昇順)なので、古いものは先頭側に溜まる
        while self._frames:
            ageSeconds = nowMonotonic - self._frames[0].ReceivedMonotonic
            if ageSeconds <= DATA_RETENTION_SECONDS:
                break
            self._frames.pop(0)
            removedAny = True
        return removedAny

    def _RebuildJsonCacheLocked(self) -> None:
        """
        ロック取得済みで呼び出すこと。
        /realtime 高速応答のための JSON キャッシュを更新する。

        Returns:
            None
        """
        # ListOfData は TimeStamp 降順(新しい順)が必須なので reverse して作る
        framesDescending = [entry.FrameJsonObject for entry in reversed(self._frames)]
        listJson = json.dumps(framesDescending, ensure_ascii=False, indent=2)

        # 外側 JSON へ埋め込むため、改行後の行頭に外側インデント(2スペース)を追加した版も作っておく
        self._cachedListOfDataJson = listJson
        self._cachedListOfDataJsonIndented = IndentJsonForOuterObject(listJson, outerIndentSpaces=2)

        if self._deviceIdUpper and self._lastDataReceivedTimestamp:
            deviceObject: dict[str, Any] = {
                "DeviceType": DEVICE_TYPE_POLAR_VERITY,
                "DeviceId": self._deviceIdUpper,
                "LastDataReceivedTimeStamp": self._lastDataReceivedTimestamp,
            }
            devicesJson = json.dumps([deviceObject], ensure_ascii=False, indent=2)
            self._cachedConnectedDevicesJson = devicesJson
            self._cachedConnectedDevicesJsonIndented = IndentJsonForOuterObject(devicesJson, outerIndentSpaces=2)
        else:
            self._cachedConnectedDevicesJson = "[]"
            self._cachedConnectedDevicesJsonIndented = "[]"


# ----------------------------
# part2: BLE 受信ワーカー
# ----------------------------
@dataclass(frozen=True)
class HeartRateSample:
    """
    心拍通知 1 件(内部処理用)。

    Attributes:
        Bpm: 心拍数(BPM)
        ReceivedMonotonic: 受信時刻(単調増加クロック)
    """
    Bpm: int
    ReceivedMonotonic: float


class PolarVeritySenseReceiver:
    """
    part2 の本体:
      - BLE デバイス探索
      - 接続維持
      - HR Notify 購読
      - 受信フレームを SharedSensorHistory に格納
      - 1秒ごとの統計表示(指定書式)
      - 60秒ごとのバッテリー残量取得

    ユーザー要求:
      - 例外で落ちずに復帰し続ける
      - 1秒に1回だけ統計メッセージを出す(リアルタイムの bpm 出力はしない)
      - スキャン/接続/例外などのデバッグ表示は P1 踏襲 + "[PART2] " プレフィックス
    """

    def __init__(self, sharedHistory: SharedSensorHistory) -> None:
        self._sharedHistory = sharedHistory

        # スキャンの絞り込み:
        # - 名前キーワード(カンマ区切り)
        # - MAC アドレス固定
        keywordsText = os.environ.get("TARGET_NAME_KEYWORDS", "Polar")
        self._targetNameKeywords = tuple(
            keyword.strip().lower() for keyword in keywordsText.split(",") if keyword.strip()
        )
        self._targetDeviceAddress = os.environ.get("TARGET_DEVICE_ADDRESS", "").strip()

        # 接続中のみ有効な Notify 受信用キュー
        self._sampleQueue: Optional["asyncio.Queue[HeartRateSample]"] = None

        # 1秒統計用: 直近 1 秒の受信件数
        self._dataCountThisSecond: int = 0

    def Start(self) -> None:
        """
        part2 のバックグラウンドスレッドを起動する。

        Returns:
            None
        """
        workerThread = threading.Thread(
            target=self._RunBackgroundThread,
            name="Part2PolarVeritySense",
            daemon=True,  # Ctrl+C 終了時にスレッドも終了させる
        )
        workerThread.start()
        PrintPart2Status("Polar Verity Sense 受信スレッドを開始しました。")

    def _RunBackgroundThread(self) -> None:
        """
        part2 スレッド本体。asyncio ループを 1 回だけ作り、永続実行する。

        Returns:
            None (無限ループ)
        """
        try:
            asyncio.run(self._MainAsync())
        except Exception as ex:
            # ここに来るのは「致命的に例外が外へ漏れた」場合
            PrintPart2Error(f"[FATAL] バックグラウンドスレッドが停止しました: {ex}")
            PrintPart2Error(traceback.format_exc())

    async def _MainAsync(self) -> None:
        """
        part2 のメイン async:
          - 永続 stats ループ(1秒ごと)を開始
          - 大ループ: デバイス探索→接続→受信→切断→復帰 を繰り返す

        Returns:
            None (無限ループ)
        """
        # stats ループは常時動かし続ける(未接続時も 0 を出すため)
        statsTask = asyncio.create_task(self._StatsAndHousekeepingLoopAsync())

        try:
            while True:
                try:
                    targetDevice = await self._FindTargetDeviceAsync()

                    if targetDevice is None:
                        PrintPart2Error(
                            f"[CONNECT] POLAR が見つかりません。{RETRY_INTERVAL_SECONDS:.0f}秒後に再試行します。"
                        )
                        await asyncio.sleep(RETRY_INTERVAL_SECONDS)
                        continue

                    await self._ConnectAndReceiveLoopAsync(targetDevice)

                except Exception as ex:
                    PrintPart2Error(f"[RECOVER] 大ループで例外: {ex}")
                    PrintPart2Error(traceback.format_exc())
                    await asyncio.sleep(RETRY_INTERVAL_SECONDS)
        finally:
            # 通常ここには来ないが、念のため
            statsTask.cancel()

    async def _FindTargetDeviceAsync(self) -> Optional[BLEDevice]:
        """
        周辺 BLE デバイスから Polar(心拍サービス含む/名前キーワード一致) を 1 台探して返す。

        Returns:
            Optional[BLEDevice]: 見つかれば BLEDevice、見つからなければ None
        """
        if self._targetDeviceAddress:
            # MAC アドレス固定探索
            def filterByAddress(device: BLEDevice, adv: AdvertisementData) -> bool:
                return (device.address or "").lower() == self._targetDeviceAddress.lower()

            return await BleakScanner.find_device_by_filter(filterByAddress, timeout=SCAN_TIMEOUT_SECONDS)

        # 名前キーワードまたは Heart Rate Service UUID を含む広告で探索
        def filterPolar(device: BLEDevice, adv: AdvertisementData) -> bool:
            deviceName = (device.name or "").lower()
            if deviceName:
                for keyword in self._targetNameKeywords:
                    if keyword and keyword in deviceName:
                        return True

            serviceUuids = [uuid.lower() for uuid in (adv.service_uuids or [])]
            return HEART_RATE_SERVICE_UUID in serviceUuids

        return await BleakScanner.find_device_by_filter(filterPolar, timeout=SCAN_TIMEOUT_SECONDS)

    async def _ConnectAndReceiveLoopAsync(self, device: BLEDevice) -> None:
        """
        デバイスへ接続し、Notify 購読して受信ループを実行する。
        切断/例外が起きたら戻り、大ループ側で再試行される。

        Args:
            device: 接続対象 BLEDevice

        Returns:
            None
        """
        disconnectedEvent = asyncio.Event()

        def OnDisconnected(client: BleakClient) -> None:
            disconnectedEvent.set()

        # 通知の受信は「接続中だけ」なので、接続ごとにキューを作り直す
        self._sampleQueue = asyncio.Queue()

        deviceIdUpper = (device.address or "").upper()

        client = BleakClient(device, disconnected_callback=OnDisconnected, pair=False)

        try:
            PrintPart2Status(f"[CONNECT] 接続試行: name={device.name!r} address={device.address}")

            # connect() 自体に timeout 引数が無いので wait_for で担保
            await asyncio.wait_for(client.connect(), timeout=CONNECT_TIMEOUT_SECONDS)

            PrintPart2Status("[CONNECT] 接続成功。Heart Rate Notify を開始します。")

            # 接続直後にバッテリーレベルを一度取得しておく(失敗しても継続)
            await self._TryReadAndStoreBatteryLevelAsync(client)

            # Heart Rate Measurement Notify を購読開始
            await client.start_notify(HEART_RATE_MEASUREMENT_UUID, self._HandleHeartRateNotification)

            # 受信処理タスク(キューから取り出して共有履歴へ格納)
            consumerTask = asyncio.create_task(self._ConsumeSamplesLoopAsync(deviceIdUpper, disconnectedEvent))

            # バッテリー 60 秒ポーリング(接続中のみ)
            batteryTask = asyncio.create_task(self._BatteryPollLoopAsync(client, disconnectedEvent))

            # 切断 or タスク異常終了を待つ
            waitDisconnectTask = asyncio.create_task(disconnectedEvent.wait())
            done, pending = await asyncio.wait(
                [waitDisconnectTask, consumerTask, batteryTask],
                return_when=asyncio.FIRST_COMPLETED,
            )

            # タスクが例外で終わった場合はログに出す
            for finished in done:
                if finished is waitDisconnectTask:
                    continue
                ex = finished.exception()
                if ex is not None:
                    PrintPart2Error(f"[RUN] 接続中タスクで例外: {ex}")
                    PrintPart2Error(traceback.format_exc())

            # ここに来たら切断扱いで復帰
            if disconnectedEvent.is_set() or (not client.is_connected):
                PrintPart2Error("[DISCONNECT] デバイスが切断されました。再接続します。")
            else:
                PrintPart2Error("[DISCONNECT] 通信が終了しました。再接続します。")

            # 残タスクを止める
            for task in pending:
                task.cancel()

        except asyncio.TimeoutError:
            PrintPart2Error(f"[CONNECT] 接続タイムアウト({CONNECT_TIMEOUT_SECONDS:.0f}秒)。再試行します。")
        except Exception as ex:
            PrintPart2Error(f"[CONNECT] 接続/通知開始で例外: {ex}")
            PrintPart2Error(traceback.format_exc())
        finally:
            # 後始末(失敗しても落とさない)
            try:
                if client.is_connected:
                    try:
                        await client.stop_notify(HEART_RATE_MEASUREMENT_UUID)
                    except Exception:
                        pass
                    try:
                        await client.disconnect()
                    except Exception:
                        pass
            finally:
                # 少し待ってから再試行
                await asyncio.sleep(RETRY_INTERVAL_SECONDS)

    def _HandleHeartRateNotification(self, sender: BleakGATTCharacteristic, data: bytearray) -> None:
        """
        Heart Rate Measurement(2A37) Notify 受信ハンドラ。

        注意:
          - Notify コールバック内では重い処理をしない(取りこぼし防止)。
          - 解析してキューへ積むだけにする。

        Args:
            sender: 通知元 characteristic
            data: 受信データ

        Returns:
            None
        """
        parsed = ParseHeartRateMeasurement(bytes(data))
        if parsed is None:
            return

        bpm, _rrIntervals = parsed

        if self._sampleQueue is None:
            return

        sample = HeartRateSample(Bpm=int(bpm), ReceivedMonotonic=time.monotonic())

        # asyncio.Queue は同一イベントループスレッド内で使う前提で put_nowait 可能
        try:
            self._sampleQueue.put_nowait(sample)
        except Exception:
            # 万が一でも落とさない
            pass

    async def _ConsumeSamplesLoopAsync(self, deviceIdUpper: str, disconnectedEvent: asyncio.Event) -> None:
        """
        キューからサンプルを取り出して共有履歴へ格納するループ。

        Args:
            deviceIdUpper: MAC アドレス(大文字)
            disconnectedEvent: 切断通知イベント

        Returns:
            None (切断/キャンセルで終了)
        """
        if self._sampleQueue is None:
            return

        while True:
            if disconnectedEvent.is_set():
                return

            sample = await self._sampleQueue.get()
            try:
                # 1 秒統計用カウント
                self._dataCountThisSecond += 1

                # 共有履歴へ登録
                self._sharedHistory.AddHeartRateFrame(deviceIdUpper=deviceIdUpper, bpm=sample.Bpm)
            finally:
                self._sampleQueue.task_done()

    async def _BatteryPollLoopAsync(self, client: BleakClient, disconnectedEvent: asyncio.Event) -> None:
        """
        バッテリー残量を 60 秒ごとに取得するループ。

        Args:
            client: 接続済み BleakClient
            disconnectedEvent: 切断通知イベント

        Returns:
            None (切断/キャンセルで終了)
        """
        while True:
            if disconnectedEvent.is_set() or (not client.is_connected):
                return

            await asyncio.sleep(BATTERY_POLL_INTERVAL_SECONDS)

            if disconnectedEvent.is_set() or (not client.is_connected):
                return

            await self._TryReadAndStoreBatteryLevelAsync(client)

    async def _TryReadAndStoreBatteryLevelAsync(self, client: BleakClient) -> None:
        """
        Battery Level(0x2A19) を Read して共有履歴へ保存する。
        取得できない場合でも例外で落とさない。

        Args:
            client: 接続済み BleakClient

        Returns:
            None
        """
        try:
            # 0x2A19 は通常 1 byte (0-100%)
            raw = await client.read_gatt_char(BATTERY_LEVEL_UUID)
            if not raw:
                return
            level = int(raw[0])
            level = ClampInt(level, 0, 100)

            self._sharedHistory.SetBatteryLevel(level)
            PrintPart2Status(f"[BATTERY] BatteryLevel = {level}")
        except Exception as ex:
            # デバイス仕様や権限で読めない場合があるため、警告に留める
            PrintPart2Error(f"[BATTERY] バッテリーレベル取得に失敗: {ex}")

    async def _StatsAndHousekeepingLoopAsync(self) -> None:
        """
        1 秒ごとに統計を出力し、古いフレームの破棄も行う常駐ループ。

        出力形式(要求仕様):
          PART2: NUM_DEVICES = 0/1, DATA_PER_SECOND = N

        Returns:
            None (無限ループ)
        """
        while True:
            try:
                await asyncio.sleep(STATS_PRINT_INTERVAL_SECONDS)

                numDevices = self._sharedHistory.GetAliveDeviceCount()
                dataPerSecond = self._dataCountThisSecond

                # 次の 1 秒のためにリセット
                self._dataCountThisSecond = 0

                # 指定の統計メッセージ(この行だけは [PART2] ではなく "PART2:" を要求通りにする)
                print(f"PART2: NUM_DEVICES = {numDevices}, DATA_PER_SECOND = {dataPerSecond}", flush=True)

                # 60 秒より古いデータを破棄
                self._sharedHistory.PurgeOldFrames()

            except Exception as ex:
                # stats ループが落ちると致命的なので、必ず復帰する
                PrintPart2Error(f"[RECOVER] stats ループで例外: {ex}")
                PrintPart2Error(traceback.format_exc())


# ----------------------------
# part3: HTTP サーバ
# ----------------------------
class RealtimeRequestHandler(BaseHTTPRequestHandler):
    """
    /realtime 専用の簡易 HTTP ハンドラ。

    - /realtime または /realtime/ のみ 200 OK
    - それ以外は 404 Not Found
    - 認証なし
    - text/plain で JSON を返す
    - ログ(アクセスログ)は大量アクセス時に邪魔なので無効化
    """

    # クラス変数として共有状態を保持する (CreateHandlerClass() で注入)
    SharedHistory: SharedSensorHistory

    # HTTP/1.1 で応答する(keep-alive を許可しやすい)
    protocol_version = "HTTP/1.1"

    def do_GET(self) -> None:  # noqa: N802 (Microsoft 風命名優先)
        """
        GET リクエスト処理。

        Returns:
            None
        """
        if self.path not in ("/realtime", "/realtime/"):
            self.send_error(404, "Not Found")
            return

        try:
            bodyText = self.SharedHistory.BuildRealtimeJsonText()
            bodyBytes = bodyText.encode("utf-8")

            self.send_response(200)
            self.send_header("Content-Type", "text/plain; charset=utf-8")
            self.send_header("Content-Length", str(len(bodyBytes)))
            self.send_header("Cache-Control", "no-cache, no-store, must-revalidate")
            self.end_headers()
            self.wfile.write(bodyBytes)
        except Exception as ex:
            # 例外でもサーバ全体を落とさない
            PrintPart3Error(f"/realtime 応答生成で例外: {ex}")
            PrintPart3Error(traceback.format_exc())
            self.send_error(500, "Internal Server Error")

    def do_HEAD(self) -> None:  # noqa: N802
        """
        HEAD リクエスト処理。

        Returns:
            None
        """
        if self.path not in ("/realtime", "/realtime/"):
            self.send_error(404, "Not Found")
            return

        try:
            bodyText = self.SharedHistory.BuildRealtimeJsonText()
            bodyBytes = bodyText.encode("utf-8")

            self.send_response(200)
            self.send_header("Content-Type", "text/plain; charset=utf-8")
            self.send_header("Content-Length", str(len(bodyBytes)))
            self.send_header("Cache-Control", "no-cache, no-store, must-revalidate")
            self.end_headers()
        except Exception:
            self.send_error(500, "Internal Server Error")

    def log_message(self, format: str, *args: Any) -> None:
        """
        BaseHTTPRequestHandler のデフォルトアクセスログを抑止する。
        (100 req/sec などの大量アクセスでコンソールが埋まるため)

        Args:
            format: フォーマット文字列
            *args: 引数

        Returns:
            None
        """
        return


def CreateHandlerClass(sharedHistory: SharedSensorHistory) -> type[RealtimeRequestHandler]:
    """
    SharedSensorHistory をハンドラに注入した RequestHandler クラスを生成する。

    Args:
        sharedHistory: 共有履歴

    Returns:
        type[RealtimeRequestHandler]: 注入済みハンドラクラス
    """

    class InjectedHandler(RealtimeRequestHandler):
        SharedHistory = sharedHistory

    return InjectedHandler


class RealtimeHttpServer:
    """
    part3 の HTTP サーバ(0.0.0.0:7001) を起動して維持する。

    要求仕様:
      - bind/listen に失敗した場合もクラッシュせず、5 秒ごとに再試行し続ける
      - IPv4 のみで listen する
    """

    def __init__(self, sharedHistory: SharedSensorHistory) -> None:
        self._sharedHistory = sharedHistory

    def Start(self) -> None:
        """
        part3 のバックグラウンドスレッドを起動する。

        Returns:
            None
        """
        workerThread = threading.Thread(
            target=self._RunBackgroundThread,
            name="Part3HttpServer",
            daemon=True,
        )
        workerThread.start()
        PrintPart3Status(f"HTTP サーバスレッドを開始しました: http://{HTTP_BIND_HOST}:{HTTP_BIND_PORT}/realtime")

    def _RunBackgroundThread(self) -> None:
        """
        サーバスレッド本体。bind/listen 失敗時は 5 秒ごとに再試行する。

        Returns:
            None (無限ループ)
        """
        handlerClass = CreateHandlerClass(self._sharedHistory)

        # allow_reuse_address を有効にして、再起動時の TIME_WAIT 等での失敗を減らす
        ThreadingHTTPServer.allow_reuse_address = True

        while True:
            httpServer: Optional[ThreadingHTTPServer] = None
            try:
                httpServer = ThreadingHTTPServer((HTTP_BIND_HOST, HTTP_BIND_PORT), handlerClass)
                httpServer.daemon_threads = True  # リクエスト処理スレッドを daemon にして、Ctrl+C で素直に終われるようにする

                PrintPart3Status("HTTP サーバを起動しました。/realtime のみ有効です。")
                httpServer.serve_forever(poll_interval=0.5)

            except OSError as ex:
                # 典型例: [Errno 98] Address already in use
                PrintPart3Error(f"bind/listen に失敗しました。{HTTP_RETRY_INTERVAL_SECONDS:.0f}秒後に再試行します: {ex}")
                time.sleep(HTTP_RETRY_INTERVAL_SECONDS)

            except Exception as ex:
                PrintPart3Error(f"HTTP サーバで例外: {ex}")
                PrintPart3Error(traceback.format_exc())
                time.sleep(HTTP_RETRY_INTERVAL_SECONDS)

            finally:
                if httpServer is not None:
                    try:
                        httpServer.server_close()
                    except Exception:
                        pass


# ----------------------------
# main
# ----------------------------
def main() -> None:
    """
    エントリポイント。
    part2 / part3 を別スレッドで開始し、メインスレッドは永続待機する。

    Returns:
        None
    """
    sharedHistory = SharedSensorHistory()

    part2 = PolarVeritySenseReceiver(sharedHistory)
    part3 = RealtimeHttpServer(sharedHistory)

    part2.Start()
    part3.Start()

    PrintPart3Status("終了したい場合は Ctrl+C でプロセスを停止してください。")

    # 終了契機は Ctrl+C のみという仕様なので、メインスレッドは待機し続ける
    try:
        while True:
            time.sleep(3600)
    except KeyboardInterrupt:
        print("\n[MAIN] Ctrl+C を検出しました。プロセスを終了します。", flush=True)


if __name__ == "__main__":
    main()
