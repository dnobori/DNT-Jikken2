#include <stdio.h>
#include <stdlib.h>
#include <windows.h>
#include <tchar.h>

#include <dn-text-normalize-hotkey-dll.h>

int main()
{
	printf("Hello\n");
	SetHook(NULL);
	Sleep(INFINITE);
	return 0;
}

