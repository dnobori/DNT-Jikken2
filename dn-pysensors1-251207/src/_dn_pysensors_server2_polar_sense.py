#!/usr/bin/env python3
"""
polar_hr_console.py

Linux + Python 3.11 + Bleak(BlueZ) で、近くの POLAR 心拍センサー(BLE)から
Heart Rate Measurement(0x2A37) の Notify を受け取り、最新心拍数をコンソールに常時表示します。

ポイント:
- 0x2A37 は多くの心拍センサーで "Notify" で配信され、"Read" は失敗することがあります。
- そのため「notify を購読して最新値を保持し、100ms周期で画面表示を更新」します。
"""

from __future__ import annotations

import asyncio
import os
import sys
import time
import traceback
from dataclasses import dataclass
from typing import Optional, Tuple

from bleak import BleakClient, BleakScanner
from bleak.backends.characteristic import BleakGATTCharacteristic
from bleak.backends.device import BLEDevice
from bleak.backends.scanner import AdvertisementData


# ---- BLE GATT UUIDs (Heart Rate Service / Heart Rate Measurement Characteristic) ----
HEART_RATE_SERVICE_UUID = "0000180d-0000-1000-8000-00805f9b34fb"
HEART_RATE_MEASUREMENT_UUID = "00002a37-0000-1000-8000-00805f9b34fb"

# ---- 動作仕様(ユーザー要求) ----
SCAN_TIMEOUT_SECONDS = 5.0
CONNECT_TIMEOUT_SECONDS = 5.0
RETRY_INTERVAL_SECONDS = 5.0
POLL_INTERVAL_SECONDS = 0.1  # 100ms


@dataclass(frozen=True)
class HeartRateSample:
    """
    心拍データ1件。

    Attributes:
        bpm: 心拍数(Beat Per Minute)
        rrIntervalsMs: RR-Interval(ミリ秒)の配列。対応していない/送られてこない場合は空。
        timestampMonotonic: 受信時刻(単調増加クロック)。経過時間の計測に使う。
    """
    bpm: int
    rrIntervalsMs: Tuple[float, ...]
    timestampMonotonic: float


class ConsoleUi:
    """
    コンソールへの表示を、見やすく(1行更新)行うためのユーティリティ。
    100ms周期で同じ行を更新し、状態メッセージは改行して表示する。
    """

    def __init__(self) -> None:
        self._lastDynamicLineLength = 0

    def PrintStatusLine(self, message: str) -> None:
        """状態メッセージを改行して表示する。"""
        self._ClearDynamicLine()
        print(message, flush=True)

    def PrintErrorLine(self, message: str) -> None:
        """エラーメッセージを改行して表示する。"""
        self._ClearDynamicLine()
        print(message, file=sys.stderr, flush=True)

    def UpdateDynamicLine(self, message: str) -> None:
        """
        1行を上書き更新する。

        Args:
            message: 表示したい1行文字列
        """
        # 以前の表示より短い場合に残骸が残らないよう、空白で埋める
        paddedMessage = message.ljust(self._lastDynamicLineLength)
        sys.stdout.write("\r" + paddedMessage)
        sys.stdout.flush()
        self._lastDynamicLineLength = max(self._lastDynamicLineLength, len(message))

    def _ClearDynamicLine(self) -> None:
        """動的行を消してカーソルを行頭へ戻す。"""
        if self._lastDynamicLineLength <= 0:
            return
        sys.stdout.write("\r" + (" " * self._lastDynamicLineLength) + "\r")
        sys.stdout.flush()
        self._lastDynamicLineLength = 0


def ParseHeartRateMeasurement(data: bytes) -> Optional[Tuple[int, Tuple[float, ...]]]:
    """
    Heart Rate Measurement (0x2A37) のペイロードを解析して心拍数を返す。

    仕様(代表例):
    - data[0] は Flags
      bit0: 心拍値のサイズ (0=uint8, 1=uint16)
      bit3: Energy Expended 有無
      bit4: RR-Interval 有無
    - Flags の後ろに心拍値が続き、必要に応じて Energy Expended(2byte) と RR-Interval(2byte×N) が続く。

    Args:
        data: Notify で受信した生データ

    Returns:
        (bpm, rrIntervalsMs) もしくは解析不能なら None
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

        # Energy Expended(2byte) がある場合は読み飛ばす (心拍表示だけが目的なので)
        if hasEnergyExpended:
            if offset + 2 > len(data):
                return None
            offset += 2

        rrIntervalsMsList = []
        if hasRrInterval:
            # RR-Interval は 1/1024秒 単位が標準。ミリ秒へ換算する。
            while offset + 2 <= len(data):
                rrRaw = int.from_bytes(data[offset:offset + 2], byteorder="little", signed=False)
                rrIntervalsMsList.append(rrRaw * 1000.0 / 1024.0)
                offset += 2

        return bpm, tuple(rrIntervalsMsList)
    except Exception:
        # 想定外のフォーマットでもプログラム全体を落とさない
        return None


class PolarHeartRateConsoleApp:
    """
    「接続維持 + 心拍取得 + 表示」をバックグラウンドスレッドで永続実行するアプリ本体。

    ユーザー要求仕様:
    - 起動するとバックグラウンドスレッドが常駐稼働
    - 大ループ: 5秒周期で接続試行(タイムアウト5秒)、失敗時はエラー表示して再試行
    - 接続成功後: 小ループ(100ms)で最新心拍の表示を更新
    - 例外発生時でも落ちずに回復し、大ループ先頭から再開
    - 終了機能は不要(ユーザーがプロセスを終了)
    """

    def __init__(self) -> None:
        self._ui = ConsoleUi()

        # 通知で受けた心拍を入れるキュー (最新優先)。小ループが100msごとに「取得を試みる」ために使う。
        self._heartRateQueue: "asyncio.Queue[HeartRateSample]" = asyncio.Queue(maxsize=1)

        # 最後に表示したサンプル(なければ None)
        self._lastSample: Optional[HeartRateSample] = None

        # 対象デバイスの名前フィルタ (例: "Polar")
        # 追加で絞り込みたい場合は環境変数 TARGET_NAME_KEYWORDS="Polar,H10" のように指定できる。
        keywordsText = os.environ.get("TARGET_NAME_KEYWORDS", "Polar")
        self._targetNameKeywords = tuple(
            keyword.strip().lower() for keyword in keywordsText.split(",") if keyword.strip()
        )

        # どうしても名前が取れない/一致しない環境向けに、MACアドレス固定にも対応する。
        # 例: TARGET_DEVICE_ADDRESS="AA:BB:CC:DD:EE:FF"
        self._targetDeviceAddress = os.environ.get("TARGET_DEVICE_ADDRESS", "").strip()

    def Start(self) -> None:
        """
        バックグラウンドスレッドを起動する。

        Returns:
            None
        """
        import threading

        workerThread = threading.Thread(
            target=self._RunBackgroundThread,
            name="PolarHeartRateWorker",
            daemon=True,  # メインスレッド終了で一緒に終了させる(ユーザーがプロセス終了する前提)
        )
        workerThread.start()

        self._ui.PrintStatusLine("POLAR 心拍モニタ: 取得開始 (終了したい場合は Ctrl+C などでプロセスを停止してください)")

    def _RunBackgroundThread(self) -> None:
        """
        バックグラウンドスレッド本体。asyncio ループを1回だけ起動して永続実行する。

        Bleak の注意点:
        - asyncio.run() を複数回呼ぶと、バックグラウンドタスクが壊れて不安定になる可能性があるため、
          1回だけ呼び、その中で大ループを回し続ける。
        """
        try:
            asyncio.run(self._MainAsync())
        except Exception as ex:
            # バックグラウンドスレッドが落ちると復帰できないので、最後の砦としてログを出す
            self._ui.PrintErrorLine(f"[FATAL] 予期しない例外でバックグラウンドスレッドが停止しました: {ex}")
            self._ui.PrintErrorLine(traceback.format_exc())

    async def _MainAsync(self) -> None:
        """
        大ループ(接続試行ループ)を永続実行する。

        Returns:
            None (永続ループ)
        """
        while True:
            try:
                targetDevice = await self._FindTargetDeviceAsync()

                if targetDevice is None:
                    self._ui.PrintErrorLine(
                        f"[CONNECT] POLAR が見つかりません。{RETRY_INTERVAL_SECONDS:.0f}秒後に再試行します。"
                    )
                    await asyncio.sleep(RETRY_INTERVAL_SECONDS)
                    continue

                await self._ConnectAndRunLoopAsync(targetDevice)

            except Exception as ex:
                # どんな例外でも落ちず、5秒待って最初からやり直す
                self._ui.PrintErrorLine(f"[RECOVER] 大ループで例外: {ex}")
                self._ui.PrintErrorLine(traceback.format_exc())
                await asyncio.sleep(RETRY_INTERVAL_SECONDS)

    async def _FindTargetDeviceAsync(self) -> Optional[BLEDevice]:
        """
        周辺の BLE デバイスから、最初に見つかった POLAR を返す。

        Returns:
            BLEDevice or None
        """
        if self._targetDeviceAddress:
            def filterByAddress(device: BLEDevice, adv: AdvertisementData) -> bool:
                return (device.address or "").lower() == self._targetDeviceAddress.lower()

            return await BleakScanner.find_device_by_filter(filterByAddress, timeout=SCAN_TIMEOUT_SECONDS)

        def filterPolar(device: BLEDevice, adv: AdvertisementData) -> bool:
            deviceName = (device.name or "").lower()
            if deviceName:
                for keyword in self._targetNameKeywords:
                    if keyword and keyword in deviceName:
                        return True

            # 名前が取れない/一致しない場合の保険: 広告に Heart Rate Service UUID が含まれるか
            serviceUuids = [uuid.lower() for uuid in (adv.service_uuids or [])]
            return HEART_RATE_SERVICE_UUID in serviceUuids

        return await BleakScanner.find_device_by_filter(filterPolar, timeout=SCAN_TIMEOUT_SECONDS)

    async def _ConnectAndRunLoopAsync(self, device: BLEDevice) -> None:
        """
        デバイスへ接続し、Notify購読して小ループ(100ms表示更新)を回す。

        Args:
            device: 接続対象デバイス

        Returns:
            None (切断/例外で戻る。呼び出し元の大ループが再接続を行う)
        """
        disconnectedEvent = asyncio.Event()

        def OnDisconnected(client: BleakClient) -> None:
            disconnectedEvent.set()

        # BleakClient は BLEDevice を渡すのが推奨 (アドレス指定より不具合が少ない)
        client = BleakClient(device, disconnected_callback=OnDisconnected, pair=False)

        # 直前のデータが残っていると「接続したのに古い値」が出るので、接続のたびに初期化
        self._lastSample = None
        self._DrainQueue()

        try:
            self._ui.PrintStatusLine(f"[CONNECT] 接続試行: name={device.name!r} address={device.address}")

            # 接続タイムアウト5秒: connect() 自体に timeout 引数が無いので wait_for で担保する
            await asyncio.wait_for(client.connect(), timeout=CONNECT_TIMEOUT_SECONDS)

            self._ui.PrintStatusLine("[CONNECT] 接続成功。Heart Rate Notify を開始します。")

            # Heart Rate Measurement の Notify を購読
            await client.start_notify(HEART_RATE_MEASUREMENT_UUID, self._HandleHeartRateNotification)

            # 小ループ: 100msごとに最新値を「取得して」表示
            await self._RunDisplayLoopAsync(client, disconnectedEvent)

        except asyncio.TimeoutError:
            self._ui.PrintErrorLine(f"[CONNECT] 接続タイムアウト({CONNECT_TIMEOUT_SECONDS:.0f}秒)。再試行します。")
        except Exception as ex:
            self._ui.PrintErrorLine(f"[CONNECT] 接続/通知開始で例外: {ex}")
            self._ui.PrintErrorLine(traceback.format_exc())
        finally:
            # 可能なら後始末。ここで失敗しても大ループで復帰する。
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
                # 次の接続試行まで少し待つ
                await asyncio.sleep(RETRY_INTERVAL_SECONDS)

    async def _RunDisplayLoopAsync(self, client: BleakClient, disconnectedEvent: asyncio.Event) -> None:
        """
        小ループ: 100ms周期で最新心拍を表示する。
        Notify で来たサンプルをキューから取り出すことで「取得を試みる」動作にする。

        Args:
            client: 接続済み BleakClient
            disconnectedEvent: 切断通知イベント

        Returns:
            None (切断/例外で戻る)
        """
        while True:
            # 切断されたら抜ける
            if disconnectedEvent.is_set() or (not client.is_connected):
                self._ui.PrintErrorLine("[DISCONNECT] デバイスが切断されました。再接続します。")
                return

            try:
                # 100msごとに「最新データを取得してみる」
                newestSample = self._TryGetNewestSampleFromQueue()
                if newestSample is not None:
                    self._lastSample = newestSample

                # 表示する(新しい値がない場合も、最後の値を表示し続ける)
                self._RenderHeartRateLine(client)

            except Exception as ex:
                self._ui.PrintErrorLine(f"[RUN] 小ループで例外: {ex}")
                self._ui.PrintErrorLine(traceback.format_exc())
                return

            await asyncio.sleep(POLL_INTERVAL_SECONDS)

    def _HandleHeartRateNotification(self, sender: BleakGATTCharacteristic, data: bytearray) -> None:
        """
        Heart Rate Measurement(2A37) Notify 受信ハンドラ。
        - ここでは「解析してキューへ入れる」だけに留め、重い処理をしない(取りこぼし防止)。

        Args:
            sender: 通知元キャラクタリスティック
            data: 受信データ

        Returns:
            None
        """
        parsed = ParseHeartRateMeasurement(bytes(data))
        if parsed is None:
            return

        bpm, rrIntervalsMs = parsed
        sample = HeartRateSample(
            bpm=bpm,
            rrIntervalsMs=rrIntervalsMs,
            timestampMonotonic=time.monotonic(),
        )

        # 最新優先: キューが満杯なら古いものを捨てて入れる
        try:
            self._heartRateQueue.put_nowait(sample)
        except asyncio.QueueFull:
            try:
                _ = self._heartRateQueue.get_nowait()
                self._heartRateQueue.task_done()
            except asyncio.QueueEmpty:
                pass
            try:
                self._heartRateQueue.put_nowait(sample)
            except asyncio.QueueFull:
                # ここに来るのは稀だが、安全のため黙って捨てる
                pass

    def _TryGetNewestSampleFromQueue(self) -> Optional[HeartRateSample]:
        """
        キューから「最新」サンプルを取り出す。
        (キュー内に複数ある場合は最後まで取り出して最新だけ返す)

        Returns:
            HeartRateSample or None
        """
        newestSample: Optional[HeartRateSample] = None
        while True:
            try:
                newestSample = self._heartRateQueue.get_nowait()
                self._heartRateQueue.task_done()
            except asyncio.QueueEmpty:
                break
        return newestSample

    def _DrainQueue(self) -> None:
        """キューを空にする(接続し直した時に古いデータが出ないようにする)。"""
        while True:
            try:
                _ = self._heartRateQueue.get_nowait()
                self._heartRateQueue.task_done()
            except asyncio.QueueEmpty:
                break

    def _RenderHeartRateLine(self, client: BleakClient) -> None:
        """
        現在の状態(接続/最新心拍/経過時間など)を1行で表示する。

        Args:
            client: 接続済み BleakClient

        Returns:
            None
        """
        deviceName = getattr(client, "name", "") or ""
        deviceAddress = getattr(client, "address", "") or ""

        if self._lastSample is None:
            self._ui.UpdateDynamicLine(
                f"[RUN] Connected: {deviceName} ({deviceAddress}) | HR: --- bpm (waiting for notify...)"
            )
            return

        ageSeconds = max(0.0, time.monotonic() - self._lastSample.timestampMonotonic)
        rrText = ""
        if self._lastSample.rrIntervalsMs:
            # RR-Interval は複数来ることがあるので末尾(最新)を表示
            rrText = f" | RR: {self._lastSample.rrIntervalsMs[-1]:.1f} ms"

        self._ui.UpdateDynamicLine(
            f"[RUN] Connected: {deviceName} ({deviceAddress}) | HR: {self._lastSample.bpm:3d} bpm"
            f" | age: {ageSeconds:4.1f} s{rrText}"
        )


def main() -> None:
    """エントリポイント。バックグラウンドスレッドを起動し、メインスレッドは永続待機する。"""
    app = PolarHeartRateConsoleApp()
    app.Start()

    # 終了機能は不要という仕様なので、メインスレッドはただ待つ。
    # (Ctrl+C 等でプロセスを止めれば終了する)
    while True:
        time.sleep(3600)


if __name__ == "__main__":
    main()
