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

wchar_t dll_dir[520] = CLEAN;

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

void Run(wchar_t *exe_name, wchar_t *args)
{
	STARTUPINFOW info = CLEAN;
	PROCESS_INFORMATION ret = CLEAN;
	wchar_t tmp[2000] = CLEAN;

	wchar_t exe_fullpath[2000] = CLEAN;

	wsprintfW(exe_fullpath, L"%s\\%s", dll_dir, exe_name);

	wsprintfW(tmp, L"\"%s\" %s", exe_fullpath, args);

	if (CreateProcessW(NULL, tmp, NULL, NULL, FALSE,
		CREATE_NO_WINDOW | HIGH_PRIORITY_CLASS, NULL, NULL, &info, &ret))
	{
		CloseHandle(ret.hThread);
		CloseHandle(ret.hProcess);
	}
	else
	{
		wchar_t msg[2000] = CLEAN;

		wsprintfW(msg, L"Failed to exec %s", tmp);
		MessageBoxW(NULL, msg, L"DN hotkey util", MB_ICONEXCLAMATION);
	}
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
					Run(L"dn-text-normalize.exe", L"");

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



// �t�@�C���p�X����f�B���N�g�������擾����
void GetDirNameFromFilePathW(wchar_t *dst, wchar_t *filepath)
{
	wchar_t tmp[520] = CLEAN;
	UINT wp;
	UINT i;
	UINT len;
	// �����`�F�b�N
	if (dst == NULL || filepath == NULL)
	{
		return;
	}

	lstrcpyW(tmp, filepath);

	len = (UINT)lstrlenW(tmp);

	lstrcpyW(dst, L"");

	wp = 0;

	for (i = 0;i < len;i++)
	{
		wchar_t c = tmp[i];
		if (c == L'/' || c == L'\\')
		{
			tmp[wp++] = 0;
			wp = 0;
			lstrcatW(dst, tmp);
			tmp[wp++] = c;
		}
		else
		{
			tmp[wp++] = c;
		}
	}

	if (lstrlenW(dst) == 0)
	{
		lstrcpyW(dst, L"\\");
	}
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

		wchar_t dll_path[520] = CLEAN;
		GetModuleFileNameW(hInst, dll_path, sizeof(dll_path));

		GetDirNameFromFilePathW(dll_dir, dll_path);

		break;
	case DLL_PROCESS_DETACH:
		// �f�^�b�`
		break;
	}
	return TRUE;
}


