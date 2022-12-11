#pragma once
#ifdef EXPORT_
#define EXPORT_API_ __declspec(dllexport)
#else
#define EXPORT_API_ __declspec(dllimport)
#endif 

EXPORT_API_ LRESULT CALLBACK KeyHookProc(int, WPARAM, LPARAM);
EXPORT_API_ int SetHook(HWND hWnd);
EXPORT_API_ int ResetHook();

