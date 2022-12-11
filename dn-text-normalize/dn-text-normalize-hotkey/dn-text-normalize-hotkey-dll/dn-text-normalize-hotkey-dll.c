// 参考元
// https://gup.monster/entry/2015/04/10/051454

#define EXPORT_
#define _CRT_SECURE_NO_WARNINGS

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
BOOL is_ctrl_key_pressed = FALSE;
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

EXPORT_API_ LRESULT CALLBACK KeyHookProc(int code, WPARAM vk, LPARAM bits)
{
	char msg[64] = { 0 };
	if (code < 0)    // 決まり事
		return CallNextHookEx(hKeyHook, code, vk, bits);
	if (code == HC_ACTION)
	{
		//目的のウインドウにキーボードメッセージと、キーコードの転送

		if ((bits & 0x80000000) == 0)
		{
			// 押された！
			if (vk == VK_CONTROL)
			{
				// Ctrl キーが押された
				is_ctrl_key_pressed = TRUE;
			}
			else if (vk == 'Q')
			{
				// Q キーが押された
				if (is_ctrl_key_pressed)
				{
					// Ctrl + Q が押された
					MessageBox(NULL, "Pressed Ctrl + Q !!", NULL, MB_OK);

					// 他のアプリケーションには伝えない
					return 1;
				}
			}
		}
		else
		{
			// 離された！
			if (vk == VK_CONTROL)
			{
				// Ctrl キーが離された
				is_ctrl_key_pressed = FALSE;
			}
		}
	}
	return CallNextHookEx(hKeyHook, code, vk, bits);
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


