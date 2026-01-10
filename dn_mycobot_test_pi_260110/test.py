import os
import sys
import time
import msvcrt

from pymycobot import MyCobot280
from pymycobot.robot_info import RobotLimit


# 接続先 COM ポート (定数)
COM_PORT = "COM7"
# 通信ボーレート (定数)
BAUD_RATE = 115200
# 1 回の移動量 (mm) = 1cm
MOVE_STEP_MM = 10.0
# 1 回の回転量 (度)
ROTATE_STEP_DEG = 15.0
# 移動速度 (1-100)
MOVE_SPEED = 50
# 移動モード (0: 角度補間, 1: 直線補間)
MOVE_MODE = 0
# 移動完了待ちタイムアウト (秒)
MOVE_TIMEOUT_SEC = 15
# 到達確認のポーリング間隔 (秒)
POLL_INTERVAL_SEC = 0.5
# 増分移動の到達確認タイムアウト (秒)
INCREMENT_TIMEOUT_SEC = 2.0
# 座標一致判定の許容誤差 (mm)
POSITION_TOLERANCE_MM = 2.0

# キー入力と移動・回転量の対応表
# X: 前後, Y: 左右, Z: 上下, Rx/Ry/Rz: 姿勢角度
# ロボット本体から見た方向に合わせる
KEY_TO_DELTA = {
    "w": (0.0, MOVE_STEP_MM, 0.0, 0.0, 0.0, 0.0),
    "s": (0.0, -MOVE_STEP_MM, 0.0, 0.0, 0.0, 0.0),
    "a": (-MOVE_STEP_MM, 0.0, 0.0, 0.0, 0.0, 0.0),
    "d": (MOVE_STEP_MM, 0.0, 0.0, 0.0, 0.0, 0.0),
    "q": (0.0, 0.0, MOVE_STEP_MM, 0.0, 0.0, 0.0),
    "e": (0.0, 0.0, -MOVE_STEP_MM, 0.0, 0.0, 0.0),
    "4": (0.0, 0.0, 0.0, 0.0, 0.0, -ROTATE_STEP_DEG),
    "6": (0.0, 0.0, 0.0, 0.0, 0.0, ROTATE_STEP_DEG),
    "8": (0.0, 0.0, 0.0, 0.0, ROTATE_STEP_DEG, 0.0),
    "2": (0.0, 0.0, 0.0, 0.0, -ROTATE_STEP_DEG, 0.0),
}

# 終了キー
EXIT_KEYS = {"x", "\x1b"}


def createRobotController():
    """ロボット制御インスタンスを生成する。
    引数: なし
    戻り値: MyCobot280 のインスタンス。失敗時は None。
    """
    try:
        controller = MyCobot280(COM_PORT, BAUD_RATE, debug=True)
    except Exception as exc:
        print(f"接続に失敗しました: {exc}")
        return None
    return controller


def getAxisIdAndIncrement(delta):
    """増分移動・回転用の軸 ID と増分量を取得する。
    引数: delta (tuple) X/Y/Z/Rx/Ry/Rz の増分タプル
    戻り値: (axis_id, increment) のタプル。判定不可時は (None, None)。
    """
    if not delta or len(delta) < 6:
        return None, None
    # 増分操作は単一軸のみを想定する
    nonZeroIndex = None
    for index, value in enumerate(delta[:6]):
        if value != 0.0:
            if nonZeroIndex is not None:
                return None, None
            nonZeroIndex = index
    if nonZeroIndex is None:
        return None, None
    return nonZeroIndex + 1, delta[nonZeroIndex]


def readSingleKey():
    """Enter 不要の単一キー入力を取得する。
    引数: なし
    戻り値: str キー 1 文字
    """
    while True:
        keyChar = msvcrt.getwch()
        if keyChar in ("\x00", "\xe0"):
            # 特殊キーは次の入力を読み捨てる
            msvcrt.getwch()
            continue
        return keyChar


def getCurrentCoords(controller):
    """現在座標を取得する。
    引数: controller (MyCobot280)
    戻り値: list [x, y, z, rx, ry, rz] の座標リスト。失敗時は None。
    """
    try:
        coords = controller.get_coords()
    except Exception as exc:
        print(f"座標取得に失敗しました: {exc}")
        return None
    if not isinstance(coords, (list, tuple)):
        print(f"座標取得に失敗しました: 不正な応答を受信しました。({coords})")
        return None
    if not coords or len(coords) < 6:
        print("座標取得に失敗しました: 応答が短すぎます。")
        return None
    return coords


def buildTargetCoords(currentCoords, deltaX, deltaY, deltaZ, deltaRx, deltaRy, deltaRz):
    """現在座標と増分から目標座標を作成する。
    引数: currentCoords (list), deltaX (float), deltaY (float), deltaZ (float),
          deltaRx (float), deltaRy (float), deltaRz (float)
    戻り値: list [x, y, z, rx, ry, rz] の目標座標リスト
    """
    # [x, y, z, rx, ry, rz] の順で座標を保持する
    targetCoords = [
        currentCoords[0] + deltaX,
        currentCoords[1] + deltaY,
        currentCoords[2] + deltaZ,
        currentCoords[3] + deltaRx,
        currentCoords[4] + deltaRy,
        currentCoords[5] + deltaRz,
    ]
    return targetCoords


def isCoordsClose(currentCoords, targetCoords, toleranceMm):
    """現在座標と目標座標の近さを判定する。
    引数: currentCoords (list), targetCoords (list), toleranceMm (float)
    戻り値: bool 近い場合 True
    """
    if not currentCoords or not targetCoords:
        return False
    if len(currentCoords) < 6 or len(targetCoords) < 6:
        return False
    for index in range(6):
        if abs(currentCoords[index] - targetCoords[index]) > toleranceMm:
            return False
    return True


def isCoordsWithinRobotLimit(targetCoords):
    """MyCobot280 の座標範囲内かを判定する。
    引数: targetCoords (list)
    戻り値: bool 範囲内の場合 True
    """
    if not targetCoords or len(targetCoords) < 6:
        return False
    limitInfo = RobotLimit.robot_limit.get("MyCobot280")
    if not limitInfo:
        return True
    minCoords = limitInfo.get("coords_min")
    maxCoords = limitInfo.get("coords_max")
    if not minCoords or not maxCoords:
        return True
    for index in range(6):
        if targetCoords[index] < minCoords[index] or targetCoords[index] > maxCoords[index]:
            return False
    return True


def canReachTarget(controller, targetCoords):
    """逆運動学で到達可能性を簡易判定する。
    引数: controller (MyCobot280), targetCoords (list)
    戻り値: bool 到達可能と判断した場合 True
    """
    # 座標範囲外の場合は判定不能として移動を試みる
    if not isCoordsWithinRobotLimit(targetCoords):
        return True
    if not hasattr(controller, "solve_inv_kinematics"):
        return True
    if not hasattr(controller, "get_angles"):
        return True
    try:
        currentAngles = controller.get_angles()
        if not currentAngles or len(currentAngles) < 6:
            return True
        solvedAngles = controller.solve_inv_kinematics(targetCoords, currentAngles)
    except Exception as exc:
        print(f"逆運動学の判定に失敗しました: {exc}")
        return True
    if not solvedAngles:
        return False
    if hasattr(solvedAngles, "__len__") and len(solvedAngles) < 6:
        return False
    return True


def sendCoordsCommand(controller, targetCoords):
    """目標座標への移動指示を送る。
    引数: controller (MyCobot280), targetCoords (list)
    戻り値: 送信結果 (成功時は 1 など)。失敗時は None。
    """
    try:
        if hasattr(controller, "sync_send_coords"):
            try:
                return controller.sync_send_coords(
                    targetCoords, MOVE_SPEED, MOVE_MODE, MOVE_TIMEOUT_SEC
                )
            except TypeError:
                try:
                    return controller.sync_send_coords(targetCoords, MOVE_SPEED, MOVE_MODE)
                except TypeError:
                    return controller.sync_send_coords(targetCoords, MOVE_SPEED)
        try:
            return controller.send_coords(targetCoords, MOVE_SPEED, MOVE_MODE)
        except TypeError:
            return controller.send_coords(targetCoords, MOVE_SPEED)
    except Exception as exc:
        print(f"移動指示に失敗しました: {exc}")
        return None


def sendIncrementCoordCommand(controller, axisId, increment):
    """座標増分による移動指示を送る。
    引数: controller (MyCobot280), axisId (int), increment (float)
    戻り値: 送信結果 (成功時は 1 など)。失敗時は None。
    """
    try:
        return controller.jog_increment_coord(axisId, increment, MOVE_SPEED)
    except Exception as exc:
        print(f"増分移動指示に失敗しました: {exc}")
        return None


def waitUntilReached(controller, targetCoords, timeoutSec):
    """目標座標到達を待機する。
    引数: controller (MyCobot280), targetCoords (list), timeoutSec (float)
    戻り値: bool 到達した場合 True
    """
    startTime = time.monotonic()
    # 座標範囲外の場合は is_in_position が例外になるため回避する
    useIsInPosition = hasattr(controller, "is_in_position") and isCoordsWithinRobotLimit(targetCoords)
    while time.monotonic() - startTime <= timeoutSec:
        if useIsInPosition:
            try:
                result = controller.is_in_position(targetCoords, 1)
            except Exception as exc:
                print(f"到達確認に失敗しました: {exc}")
                useIsInPosition = False
                result = 0
            if result == 1:
                return True
            if result == -1:
                useIsInPosition = False
        if not useIsInPosition:
            currentCoords = getCurrentCoords(controller)
            if currentCoords is None:
                return False
            if isCoordsClose(currentCoords, targetCoords, POSITION_TOLERANCE_MM):
                return True
        time.sleep(POLL_INTERVAL_SEC)
    return False


def waitAfterIncrement(controller, targetCoords, timeoutSec):
    """増分移動の完了を簡易確認する。
    引数: controller (MyCobot280), targetCoords (list), timeoutSec (float)
    戻り値: bool 到達した場合 True
    """
    startTime = time.monotonic()
    while True:
        currentCoords = getCurrentCoords(controller)
        if currentCoords is None:
            return False
        if isCoordsClose(currentCoords, targetCoords, POSITION_TOLERANCE_MM):
            return True
        if time.monotonic() - startTime >= timeoutSec:
            break
        time.sleep(POLL_INTERVAL_SEC)
    return False


def handleMoveKey(controller, keyChar):
    """移動キー入力に基づいてロボットを移動させる。
    引数: controller (MyCobot280), keyChar (str)
    戻り値: なし
    """
    delta = KEY_TO_DELTA.get(keyChar)
    if not delta:
        return
    axisId, increment = getAxisIdAndIncrement(delta)
    if axisId is None:
        print("移動方向の判定に失敗しました。")
        return

    # 現在座標を取得し、目標座標を算出する
    currentCoords = getCurrentCoords(controller)
    if not currentCoords:
        print("現在座標の取得に失敗しました。")
        return

    targetCoords = buildTargetCoords(
        currentCoords,
        delta[0],
        delta[1],
        delta[2],
        delta[3],
        delta[4],
        delta[5],
    )

    if not canReachTarget(controller, targetCoords):
        print("エラー: これ以上アームが届かない可能性があります。")
        return

    if axisId <= 3:
        print(
            "移動指示: "
            f"Δx={delta[0]:.1f}mm, Δy={delta[1]:.1f}mm, Δz={delta[2]:.1f}mm"
        )
    else:
        print(
            "回転指示: "
            f"Δrx={delta[3]:.1f}deg, Δry={delta[4]:.1f}deg, Δrz={delta[5]:.1f}deg"
        )
    print(f"目標座標: {targetCoords}")

    # 座標全体の送信は座標範囲チェックで失敗する場合があるため、増分移動を優先する
    if hasattr(controller, "jog_increment_coord"):
        sendResult = sendIncrementCoordCommand(controller, axisId, increment)
        if sendResult is None:
            print("エラー: 増分移動指示の送信に失敗しました。")
            return
        #reached = waitAfterIncrement(controller, targetCoords, INCREMENT_TIMEOUT_SEC)
    else:
        sendResult = sendCoordsCommand(controller, targetCoords)
        if sendResult is None:
            print("エラー: 移動指示の送信に失敗しました。")
            return
        #reached = waitUntilReached(controller, targetCoords, MOVE_TIMEOUT_SEC)
    #if not reached:
    #    print("エラー: これ以上アームが届かない可能性があります。")


def printUsage():
    """操作方法を表示する。
    引数: なし
    戻り値: なし
    """
    print("操作方法: W/S/A/D/Q/E で 1cm 移動")
    print("W: 左, S: 右, A: 後, D: 前, Q: 上, E: 下")
    print("テンキー4: 水平向きを時計回りに 15 度")
    print("テンキー6: 水平向きを反時計回りに 15 度")
    print("テンキー8: 垂直向きを上に 15 度")
    print("テンキー2: 垂直向きを下に 15 度")
    print("終了: X または Esc")


def main():
    """アプリケーションのエントリポイント。
    引数: なし
    戻り値: なし
    """
    if os.name != "nt":
        print("このプログラムは Windows 環境での実行を想定しています。")
        return

    print("myCobot 280 M5 キーボード制御を開始します。")
    print(f"接続先 COM ポート: {COM_PORT}")

    controller = createRobotController()
    if controller is None:
        print("接続に失敗したため終了します。")
        return
    print("ロボットに接続しました。")

    time.sleep(0.5)
    currentCoords = getCurrentCoords(controller)
    if currentCoords:
        print(f"現在座標: {currentCoords}")
    else:
        print("現在座標の取得に失敗しました。")

    printUsage()

    try:
        while True:
            keyChar = readSingleKey()
            normalizedKey = keyChar.lower()
            if normalizedKey in EXIT_KEYS:
                print("終了します。")
                break
            if normalizedKey in KEY_TO_DELTA:
                print(f"☆ 入力キー: {normalizedKey.upper()}")
                handleMoveKey(controller, normalizedKey)
    except KeyboardInterrupt:
        print("\n中断しました。")


if __name__ == "__main__":
    main()
