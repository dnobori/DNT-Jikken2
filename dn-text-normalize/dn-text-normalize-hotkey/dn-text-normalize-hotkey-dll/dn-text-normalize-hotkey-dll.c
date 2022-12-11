// 参考元
// https://gup.monster/entry/2015/04/10/051454

#define EXPORT_

#include <stdio.h>
#include <stdlib.h>
#include <windows.h>
#include <tchar.h>

#include <dn-text-normalize-hotkey-dll.h>


// すべてのスレッドにセットされるフックになるので
// グローバル変数を共有する必要がある
// 共有セグメント
#pragma data_seg(".shareddata")
HHOOK hKeyHook = 0;
HWND g_hWnd = 0;        // キーコードの送り先のウインドウハンドル
#pragma data_seg()

HINSTANCE hInst;
EXPORT_API_ int SetHook(HWND hWnd)
{
	hKeyHook = SetWindowsHookEx(WH_KEYBOARD, KeyHookProc, hInst, 0);
	if (hKeyHook == NULL)
	{
		// フック失敗
	}
	else
	{
		// フック成功
		g_hWnd = hWnd;
	}
	return 0;
}

EXPORT_API_ int ResetHook()
{
	if (UnhookWindowsHookEx(hKeyHook) != 0)
	{
		// フック解除成功
	}
	else
	{
		// フック解除失敗
	}
	return 0;
}

EXPORT_API_ LRESULT CALLBACK KeyHookProc(int nCode, WPARAM wp, LPARAM lp)
{
	TCHAR msg[64] = { 0 };
	if (nCode < 0)    // 決まり事
		return CallNextHookEx(hKeyHook, nCode, wp, lp);
	if (nCode == HC_ACTION)
	{
		//目的のウインドウにキーボードメッセージと、キーコードの転送

		// どこで押してもHOOKする
		// ボタンが押された状態の時限定(離しはスルー)
		if ((lp & 0x80000000) == 0)
		{
			// 通常キー
			if ((lp & 0x20000000) == 0)
			{
				// Enter以外
				if (wp != VK_RETURN)
				{
					//_stprintf_s(msg, "pressed %c !", int(wp));
					//MessageBox(NULL, msg, NULL, MB_OK);
					//PostMessage(g_hWnd, WM_KEYDOWN, wp, 0);
				}
			}
			// システムキー(Alt(+何か)、もしくはF10の時)
			else
			{
				//MessageBox(NULL, TEXT("システムキーが押されたよ！"), NULL, MB_OK);
				//PostMessage(g_hWnd, WM_SYSKEYDOWN, wp, 0);
			}
		}
	}
	return CallNextHookEx(hKeyHook, nCode, wp, lp);
}

// エントリポイント
BOOL APIENTRY DllMain(HMODULE hModule,
	DWORD  ul_reason_for_call,
	LPVOID lpReserved
)
{
	switch (ul_reason_for_call)
	{
	case DLL_PROCESS_ATTACH:
		// アタッチ
		hInst = hModule;
		break;
	case DLL_PROCESS_DETACH:
		// デタッチ
		break;
	}
	return TRUE;
}


