// �Q�l��
// https://gup.monster/entry/2015/04/10/051454

#define EXPORT_
#define _CRT_SECURE_NO_WARNINGS

#include <stdio.h>
#include <stdlib.h>
#include <windows.h>
#include <tchar.h>

#include <dn-text-normalize-hotkey-dll.h>


// ���ׂẴX���b�h�ɃZ�b�g�����t�b�N�ɂȂ�̂�
// �O���[�o���ϐ������L����K�v������
// ���L�Z�O�����g
#pragma data_seg(".shareddata")
HHOOK hKeyHook = 0;
HWND g_hWnd = 0;        // �L�[�R�[�h�̑����̃E�C���h�E�n���h��
BOOL is_ctrl_key_pressed = FALSE;
#pragma data_seg()

HINSTANCE hInst;
EXPORT_API_ int SetHook(HWND hWnd)
{
	hKeyHook = SetWindowsHookEx(WH_KEYBOARD, KeyHookProc, hInst, 0);
	if (hKeyHook == NULL)
	{
		// �t�b�N���s
	}
	else
	{
		// �t�b�N����
		g_hWnd = hWnd;
	}
	return 0;
}

EXPORT_API_ int ResetHook()
{
	if (UnhookWindowsHookEx(hKeyHook) != 0)
	{
		// �t�b�N��������
	}
	else
	{
		// �t�b�N�������s
	}
	return 0;
}

EXPORT_API_ LRESULT CALLBACK KeyHookProc(int code, WPARAM vk, LPARAM bits)
{
	char msg[64] = { 0 };
	if (code < 0)    // ���܂莖
		return CallNextHookEx(hKeyHook, code, vk, bits);
	if (code == HC_ACTION)
	{
		//�ړI�̃E�C���h�E�ɃL�[�{�[�h���b�Z�[�W�ƁA�L�[�R�[�h�̓]��

		if ((bits & 0x80000000) == 0)
		{
			// �����ꂽ�I
			if (vk == VK_CONTROL)
			{
				// Ctrl �L�[�������ꂽ
				is_ctrl_key_pressed = TRUE;
			}
			else if (vk == 'Q')
			{
				// Q �L�[�������ꂽ
				if (is_ctrl_key_pressed)
				{
					// Ctrl + Q �������ꂽ
					MessageBox(NULL, "Pressed Ctrl + Q !!", NULL, MB_OK);

					// ���̃A�v���P�[�V�����ɂ͓`���Ȃ�
					return 1;
				}
			}
		}
		else
		{
			// �����ꂽ�I
			if (vk == VK_CONTROL)
			{
				// Ctrl �L�[�������ꂽ
				is_ctrl_key_pressed = FALSE;
			}
		}
	}
	return CallNextHookEx(hKeyHook, code, vk, bits);
}

// �G���g���|�C���g
BOOL APIENTRY DllMain(HMODULE hModule,
	DWORD  ul_reason_for_call,
	LPVOID lpReserved
)
{
	switch (ul_reason_for_call)
	{
	case DLL_PROCESS_ATTACH:
		// �A�^�b�`
		hInst = hModule;
		break;
	case DLL_PROCESS_DETACH:
		// �f�^�b�`
		break;
	}
	return TRUE;
}


