using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace cs_process_leak_test1
{
    public static class Internal
    {
        public static unsafe int ForkAndExecProcess(
            string filename, string[] argv, string[] envp, string cwd,
            bool redirectStdin, bool redirectStdout, bool redirectStderr,
            bool setUser, uint userId, uint groupId, uint[] groups,
            out int lpChildPid, out int stdinFd, out int stdoutFd, out int stderrFd, bool shouldThrow = true)
        {
            byte** argvPtr = null, envpPtr = null;
            int result = -1;
            try
            {
                AllocNullTerminatedArray(argv, ref argvPtr);
                AllocNullTerminatedArray(envp, ref envpPtr);
                fixed (uint* pGroups = groups)
                {
                    result = ForkAndExecProcessCall(
                        filename, argvPtr, envpPtr, cwd,
                        redirectStdin ? 1 : 0, redirectStdout ? 1 : 0, redirectStderr ? 1 : 0,
                        setUser ? 1 : 0, userId, groupId, pGroups, groups?.Length ?? 0,
                        out lpChildPid, out stdinFd, out stdoutFd, out stderrFd);
                }
                return result == 0 ? 0 : Marshal.GetLastWin32Error();
            }
            finally
            {
                FreeArray(envpPtr, envp.Length);
                FreeArray(argvPtr, argv.Length);
            }
        }
        private static unsafe void AllocNullTerminatedArray(string[] arr, ref byte** arrPtr)
        {
            int arrLength = arr.Length + 1; // +1 is for null termination

            // Allocate the unmanaged array to hold each string pointer.
            // It needs to have an extra element to null terminate the array.
            arrPtr = (byte**)Marshal.AllocHGlobal(sizeof(IntPtr) * arrLength);
            Debug.Assert(arrPtr != null);

            // Zero the memory so that if any of the individual string allocations fails,
            // we can loop through the array to free any that succeeded.
            // The last element will remain null.
            for (int i = 0; i < arrLength; i++)
            {
                arrPtr[i] = null;
            }

            // Now copy each string to unmanaged memory referenced from the array.
            // We need the data to be an unmanaged, null-terminated array of UTF8-encoded bytes.
            for (int i = 0; i < arr.Length; i++)
            {
                byte[] byteArr = Encoding.UTF8.GetBytes(arr[i]);

                arrPtr[i] = (byte*)Marshal.AllocHGlobal(byteArr.Length + 1); //+1 for null termination
                Debug.Assert(arrPtr[i] != null);

                Marshal.Copy(byteArr, 0, (IntPtr)arrPtr[i], byteArr.Length); // copy over the data from the managed byte array
                arrPtr[i][byteArr.Length] = (byte)'\0'; // null terminate
            }
        }

        private static unsafe void FreeArray(byte** arr, int length)
        {
            if (arr != null)
            {
                // Free each element of the array
                for (int i = 0; i < length; i++)
                {
                    if (arr[i] != null)
                    {
                        Marshal.FreeHGlobal((IntPtr)arr[i]);
                        arr[i] = null;
                    }
                }

                // And then the array itself
                Marshal.FreeHGlobal((IntPtr)arr);
            }
        }

        [DllImport("System.Native", EntryPoint = "SystemNative_ForkAndExecProcess", SetLastError = true)]
        internal static extern unsafe int ForkAndExecProcessCall(
            string filename, byte** argv, byte** envp, string cwd,
            int redirectStdin, int redirectStdout, int redirectStderr,
            int setUser, uint userId, uint groupId, uint* groups, int groupsLength,
            out int lpChildPid, out int stdinFd, out int stdoutFd, out int stderrFd);

        [DllImport("System.Native", EntryPoint = "SystemNative_WaitPidExitedNoHang", SetLastError = true)]
        internal static extern int WaitPidExitedNoHang(int pid, out int exitCode);
    }

    class Program
    {
        static List<object> tmp = new List<object>();
        static void Main(string[] args)
        {
            for (int i = 0; ; i++)
            {
                if ((i % 100) == 0)
                {
                    long mem = GC.GetTotalMemory(false);
                    Console.WriteLine(mem);
                    GC.Collect();
                }

                try
                {
                    /*using (var proc = Process.Start("/bin/true"))
                    {
                        proc.WaitForExit();
                        proc.Kill(false);
                    }*/

                    int r = Internal.ForkAndExecProcess("/bin/true", new string[] { }, new string[] { },
                        "/", false, false, false, false, 0, 0, null, out int pid, out _, out _, out _);

                    if (r == 0)
                    {
                        Thread.Sleep(10);
                        Internal.WaitPidExitedNoHang(pid, out _);
                    }

                    //Console.WriteLine($"r = {r}, pid = {pid}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }
    }
}
