using System;
using System.Runtime.InteropServices;
using System.Text;

internal static partial class Interop
{
    internal static partial class Libraries
    {
        internal const string GlobalizationNative = "System.Globalization.Native";
        internal const string SystemNative = "System.Native";
    }
    // https://msdn.microsoft.com/en-us/library/windows/desktop/ms681382.aspx
    internal partial class Errors
    {
        internal const int ERROR_SUCCESS = 0x0;
        internal const int ERROR_INVALID_FUNCTION = 0x1;
        internal const int ERROR_FILE_NOT_FOUND = 0x2;
        internal const int ERROR_PATH_NOT_FOUND = 0x3;
        internal const int ERROR_ACCESS_DENIED = 0x5;
        internal const int ERROR_INVALID_HANDLE = 0x6;
        internal const int ERROR_NOT_ENOUGH_MEMORY = 0x8;
        internal const int ERROR_INVALID_DATA = 0xD;
        internal const int ERROR_INVALID_DRIVE = 0xF;
        internal const int ERROR_NO_MORE_FILES = 0x12;
        internal const int ERROR_NOT_READY = 0x15;
        internal const int ERROR_BAD_COMMAND = 0x16;
        internal const int ERROR_BAD_LENGTH = 0x18;
        internal const int ERROR_SHARING_VIOLATION = 0x20;
        internal const int ERROR_LOCK_VIOLATION = 0x21;
        internal const int ERROR_HANDLE_EOF = 0x26;
        internal const int ERROR_BAD_NETPATH = 0x35;
        internal const int ERROR_NETWORK_ACCESS_DENIED = 0x41;
        internal const int ERROR_BAD_NET_NAME = 0x43;
        internal const int ERROR_FILE_EXISTS = 0x50;
        internal const int ERROR_INVALID_PARAMETER = 0x57;
        internal const int ERROR_BROKEN_PIPE = 0x6D;
        internal const int ERROR_SEM_TIMEOUT = 0x79;
        internal const int ERROR_CALL_NOT_IMPLEMENTED = 0x78;
        internal const int ERROR_INSUFFICIENT_BUFFER = 0x7A;
        internal const int ERROR_INVALID_NAME = 0x7B;
        internal const int ERROR_NEGATIVE_SEEK = 0x83;
        internal const int ERROR_DIR_NOT_EMPTY = 0x91;
        internal const int ERROR_BAD_PATHNAME = 0xA1;
        internal const int ERROR_LOCK_FAILED = 0xA7;
        internal const int ERROR_BUSY = 0xAA;
        internal const int ERROR_ALREADY_EXISTS = 0xB7;
        internal const int ERROR_BAD_EXE_FORMAT = 0xC1;
        internal const int ERROR_ENVVAR_NOT_FOUND = 0xCB;
        internal const int ERROR_FILENAME_EXCED_RANGE = 0xCE;
        internal const int ERROR_EXE_MACHINE_TYPE_MISMATCH = 0xD8;
        internal const int ERROR_PIPE_BUSY = 0xE7;
        internal const int ERROR_NO_DATA = 0xE8;
        internal const int ERROR_PIPE_NOT_CONNECTED = 0xE9;
        internal const int ERROR_MORE_DATA = 0xEA;
        internal const int ERROR_NO_MORE_ITEMS = 0x103;
        internal const int ERROR_DIRECTORY = 0x10B;
        internal const int ERROR_PARTIAL_COPY = 0x12B;
        internal const int ERROR_ARITHMETIC_OVERFLOW = 0x216;
        internal const int ERROR_PIPE_CONNECTED = 0x217;
        internal const int ERROR_PIPE_LISTENING = 0x218;
        internal const int ERROR_OPERATION_ABORTED = 0x3E3;
        internal const int ERROR_IO_INCOMPLETE = 0x3E4;
        internal const int ERROR_IO_PENDING = 0x3E5;
        internal const int ERROR_NO_TOKEN = 0x3f0;
        internal const int ERROR_SERVICE_DOES_NOT_EXIST = 0x424;
        internal const int ERROR_DLL_INIT_FAILED = 0x45A;
        internal const int ERROR_COUNTER_TIMEOUT = 0x461;
        internal const int ERROR_NO_ASSOCIATION = 0x483;
        internal const int ERROR_DDE_FAIL = 0x484;
        internal const int ERROR_DLL_NOT_FOUND = 0x485;
        internal const int ERROR_NOT_FOUND = 0x490;
        internal const int ERROR_NETWORK_UNREACHABLE = 0x4CF;
        internal const int ERROR_NON_ACCOUNT_SID = 0x4E9;
        internal const int ERROR_NOT_ALL_ASSIGNED = 0x514;
        internal const int ERROR_UNKNOWN_REVISION = 0x519;
        internal const int ERROR_INVALID_OWNER = 0x51B;
        internal const int ERROR_INVALID_PRIMARY_GROUP = 0x51C;
        internal const int ERROR_NO_SUCH_PRIVILEGE = 0x521;
        internal const int ERROR_PRIVILEGE_NOT_HELD = 0x522;
        internal const int ERROR_INVALID_ACL = 0x538;
        internal const int ERROR_INVALID_SECURITY_DESCR = 0x53A;
        internal const int ERROR_INVALID_SID = 0x539;
        internal const int ERROR_BAD_IMPERSONATION_LEVEL = 0x542;
        internal const int ERROR_CANT_OPEN_ANONYMOUS = 0x543;
        internal const int ERROR_NO_SECURITY_ON_OBJECT = 0x546;
        internal const int ERROR_CANNOT_IMPERSONATE = 0x558;
        internal const int ERROR_CLASS_ALREADY_EXISTS = 0x582;
        internal const int ERROR_EVENTLOG_FILE_CHANGED = 0x5DF;
        internal const int ERROR_TRUSTED_RELATIONSHIP_FAILURE = 0x6FD;
        internal const int ERROR_RESOURCE_LANG_NOT_FOUND = 0x717;
        internal const int EFail = unchecked((int)0x80004005);
        internal const int E_FILENOTFOUND = unchecked((int)0x80070002);
    }
    /// <summary>Common Unix errno error codes.</summary>
    internal enum Error
    {
        // These values were defined in src/Native/System.Native/fxerrno.h
        //
        // They compare against values obtained via Interop.Sys.GetLastError() not Marshal.GetLastWin32Error()
        // which obtains the raw errno that varies between unixes. The strong typing as an enum is meant to
        // prevent confusing the two. Casting to or from int is suspect. Use GetLastErrorInfo() if you need to
        // correlate these to the underlying platform values or obtain the corresponding error message.
        // 

        SUCCESS = 0,

        E2BIG = 0x10001,           // Argument list too long.
        EACCES = 0x10002,           // Permission denied.
        EADDRINUSE = 0x10003,           // Address in use.
        EADDRNOTAVAIL = 0x10004,           // Address not available.
        EAFNOSUPPORT = 0x10005,           // Address family not supported.
        EAGAIN = 0x10006,           // Resource unavailable, try again (same value as EWOULDBLOCK),
        EALREADY = 0x10007,           // Connection already in progress.
        EBADF = 0x10008,           // Bad file descriptor.
        EBADMSG = 0x10009,           // Bad message.
        EBUSY = 0x1000A,           // Device or resource busy.
        ECANCELED = 0x1000B,           // Operation canceled.
        ECHILD = 0x1000C,           // No child processes.
        ECONNABORTED = 0x1000D,           // Connection aborted.
        ECONNREFUSED = 0x1000E,           // Connection refused.
        ECONNRESET = 0x1000F,           // Connection reset.
        EDEADLK = 0x10010,           // Resource deadlock would occur.
        EDESTADDRREQ = 0x10011,           // Destination address required.
        EDOM = 0x10012,           // Mathematics argument out of domain of function.
        EDQUOT = 0x10013,           // Reserved.
        EEXIST = 0x10014,           // File exists.
        EFAULT = 0x10015,           // Bad address.
        EFBIG = 0x10016,           // File too large.
        EHOSTUNREACH = 0x10017,           // Host is unreachable.
        EIDRM = 0x10018,           // Identifier removed.
        EILSEQ = 0x10019,           // Illegal byte sequence.
        EINPROGRESS = 0x1001A,           // Operation in progress.
        EINTR = 0x1001B,           // Interrupted function.
        EINVAL = 0x1001C,           // Invalid argument.
        EIO = 0x1001D,           // I/O error.
        EISCONN = 0x1001E,           // Socket is connected.
        EISDIR = 0x1001F,           // Is a directory.
        ELOOP = 0x10020,           // Too many levels of symbolic links.
        EMFILE = 0x10021,           // File descriptor value too large.
        EMLINK = 0x10022,           // Too many links.
        EMSGSIZE = 0x10023,           // Message too large.
        EMULTIHOP = 0x10024,           // Reserved.
        ENAMETOOLONG = 0x10025,           // Filename too long.
        ENETDOWN = 0x10026,           // Network is down.
        ENETRESET = 0x10027,           // Connection aborted by network.
        ENETUNREACH = 0x10028,           // Network unreachable.
        ENFILE = 0x10029,           // Too many files open in system.
        ENOBUFS = 0x1002A,           // No buffer space available.
        ENODEV = 0x1002C,           // No such device.
        ENOENT = 0x1002D,           // No such file or directory.
        ENOEXEC = 0x1002E,           // Executable file format error.
        ENOLCK = 0x1002F,           // No locks available.
        ENOLINK = 0x10030,           // Reserved.
        ENOMEM = 0x10031,           // Not enough space.
        ENOMSG = 0x10032,           // No message of the desired type.
        ENOPROTOOPT = 0x10033,           // Protocol not available.
        ENOSPC = 0x10034,           // No space left on device.
        ENOSYS = 0x10037,           // Function not supported.
        ENOTCONN = 0x10038,           // The socket is not connected.
        ENOTDIR = 0x10039,           // Not a directory or a symbolic link to a directory.
        ENOTEMPTY = 0x1003A,           // Directory not empty.
        ENOTRECOVERABLE = 0x1003B,           // State not recoverable.
        ENOTSOCK = 0x1003C,           // Not a socket.
        ENOTSUP = 0x1003D,           // Not supported (same value as EOPNOTSUP).
        ENOTTY = 0x1003E,           // Inappropriate I/O control operation.
        ENXIO = 0x1003F,           // No such device or address.
        EOVERFLOW = 0x10040,           // Value too large to be stored in data type.
        EOWNERDEAD = 0x10041,           // Previous owner died.
        EPERM = 0x10042,           // Operation not permitted.
        EPIPE = 0x10043,           // Broken pipe.
        EPROTO = 0x10044,           // Protocol error.
        EPROTONOSUPPORT = 0x10045,           // Protocol not supported.
        EPROTOTYPE = 0x10046,           // Protocol wrong type for socket.
        ERANGE = 0x10047,           // Result too large.
        EROFS = 0x10048,           // Read-only file system.
        ESPIPE = 0x10049,           // Invalid seek.
        ESRCH = 0x1004A,           // No such process.
        ESTALE = 0x1004B,           // Reserved.
        ETIMEDOUT = 0x1004D,           // Connection timed out.
        ETXTBSY = 0x1004E,           // Text file busy.
        EXDEV = 0x1004F,           // Cross-device link.
        ESOCKTNOSUPPORT = 0x1005E,           // Socket type not supported.
        EPFNOSUPPORT = 0x10060,           // Protocol family not supported.
        ESHUTDOWN = 0x1006C,           // Socket shutdown.
        EHOSTDOWN = 0x10070,           // Host is down.
        ENODATA = 0x10071,           // No data available.

        // Custom Error codes to track errors beyond kernel interface.
        EHOSTNOTFOUND = 0x20001,           // Name lookup failed

        // POSIX permits these to have the same value and we make them always equal so
        // that CoreFX cannot introduce a dependency on distinguishing between them that
        // would not work on all platforms.
        EOPNOTSUPP = ENOTSUP,            // Operation not supported on socket.
        EWOULDBLOCK = EAGAIN,             // Operation would block.
    }


    // Represents a platform-agnostic Error and underlying platform-specific errno
    internal struct ErrorInfo
    {
        private Error _error;
        private int _rawErrno;

        internal ErrorInfo(int errno)
        {
            _error = Interop.Sys.ConvertErrorPlatformToPal(errno);
            _rawErrno = errno;
        }

        internal ErrorInfo(Error error)
        {
            _error = error;
            _rawErrno = -1;
        }

        internal Error Error
        {
            get { return _error; }
        }

        internal int RawErrno
        {
            get { return _rawErrno == -1 ? (_rawErrno = Interop.Sys.ConvertErrorPalToPlatform(_error)) : _rawErrno; }
        }

        internal string GetErrorMessage()
        {
            return Interop.Sys.StrError(RawErrno);
        }

        public override string ToString()
        {
            return $"RawErrno: {RawErrno} Error: {Error} GetErrorMessage: {GetErrorMessage()}"; // No localization required; text is member names used for debugging purposes
        }
    }

    internal static partial class Sys
    {
        /// <summary>
        /// Reaps a terminated child.
        /// </summary>
        /// <returns>
        /// 1) when a child is reaped, its process id is returned
        /// 2) if pid is not a child or there are no unwaited-for children, -1 is returned (errno=ECHILD)
        /// 3) if the child has not yet terminated, 0 is returned
        /// 4) on error, -1 is returned.
        /// </returns>
        [DllImport(Libraries.SystemNative, EntryPoint = "SystemNative_WaitPidExitedNoHang", SetLastError = true)]
        internal static extern int WaitPidExitedNoHang(int pid, out int exitCode);

        internal enum Signals : int
        {
            None = 0,
            SIGKILL = 9,
            SIGSTOP = 19
        }

        [DllImport(Libraries.SystemNative, EntryPoint = "SystemNative_Kill", SetLastError = true)]
        internal static extern int Kill(int pid, Signals signal);

        internal static Error GetLastError()
        {
            return ConvertErrorPlatformToPal(Marshal.GetLastWin32Error());
        }

        internal static ErrorInfo GetLastErrorInfo()
        {
            return new ErrorInfo(Marshal.GetLastWin32Error());
        }

        internal static unsafe string StrError(int platformErrno)
        {
            int maxBufferLength = 1024; // should be long enough for most any UNIX error
            byte* buffer = stackalloc byte[maxBufferLength];
            byte* message = StrErrorR(platformErrno, buffer, maxBufferLength);

            if (message == null)
            {
                // This means the buffer was not large enough, but still contains
                // as much of the error message as possible and is guaranteed to
                // be null-terminated. We're not currently resizing/retrying because
                // maxBufferLength is large enough in practice, but we could do
                // so here in the future if necessary.
                message = buffer;
            }

            return Marshal.PtrToStringAnsi((IntPtr)message)!;
        }

        [DllImport(Libraries.SystemNative, EntryPoint = "SystemNative_ConvertErrorPlatformToPal")]
        internal static extern Error ConvertErrorPlatformToPal(int platformErrno);

        [DllImport(Libraries.SystemNative, EntryPoint = "SystemNative_ConvertErrorPalToPlatform")]
        internal static extern int ConvertErrorPalToPlatform(Error error);

        [DllImport(Libraries.SystemNative, EntryPoint = "SystemNative_StrErrorR")]
        private static extern unsafe byte* StrErrorR(int platformErrno, byte* buffer, int bufferSize);

        /// <summary>
        /// Returns the pid of a terminated child without reaping it.
        /// </summary>
        /// <returns>
        /// 1) returns the process id of a terminated child process
        /// 2) if no children are terminated, 0 is returned
        /// 3) on error, -1 is returned
        /// </returns>
        [DllImport(Libraries.SystemNative, EntryPoint = "SystemNative_WaitIdAnyExitedNoHangNoWait", SetLastError = true)]
        internal static extern int WaitIdAnyExitedNoHangNoWait();

        internal delegate void SigChldCallback(bool reapAll);

        [DllImport(Libraries.SystemNative, EntryPoint = "SystemNative_RegisterForSigChld")]
        internal static extern void RegisterForSigChld(SigChldCallback handler);

        [DllImport(Libraries.SystemNative, EntryPoint = "SystemNative_GetPid")]
        internal static extern int GetPid();
        internal static unsafe int ForkAndExecProcess(
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
                    result = ForkAndExecProcess(
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

        [DllImport(Libraries.SystemNative, EntryPoint = "SystemNative_ForkAndExecProcess", SetLastError = true)]
        private static extern unsafe int ForkAndExecProcess(
            string filename, byte** argv, byte** envp, string cwd,
            int redirectStdin, int redirectStdout, int redirectStderr,
            int setUser, uint userId, uint groupId, uint* groups, int groupsLength,
            out int lpChildPid, out int stdinFd, out int stdoutFd, out int stderrFd);

        private static unsafe void AllocNullTerminatedArray(string[] arr, ref byte** arrPtr)
        {
            int arrLength = arr.Length + 1; // +1 is for null termination

            // Allocate the unmanaged array to hold each string pointer.
            // It needs to have an extra element to null terminate the array.
            arrPtr = (byte**)Marshal.AllocHGlobal(sizeof(IntPtr) * arrLength);
            System.Diagnostics.Debug.Assert(arrPtr != null);

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
                System.Diagnostics.Debug.Assert(arrPtr[i] != null);

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

        internal enum AccessMode : int
        {
            F_OK = 0,   /* Check for existence */
            X_OK = 1,   /* Check for execute */
            W_OK = 2,   /* Check for write */
            R_OK = 4,   /* Check for read */
        }

        [DllImport(Libraries.SystemNative, EntryPoint = "SystemNative_Access", SetLastError = true)]
        internal static extern int Access(string path, AccessMode mode);

        [DllImport(Libraries.SystemNative, EntryPoint = "SystemNative_InitializeTerminalAndSignalHandling", SetLastError = true)]
        internal static extern bool InitializeTerminalAndSignalHandling();

        [DllImport(Libraries.SystemNative, EntryPoint = "SystemNative_SetKeypadXmit")]
        internal static extern void SetKeypadXmit(string terminfoString);

        [DllImport(Libraries.SystemNative, EntryPoint = "SystemNative_ConfigureTerminalForChildProcess")]
        internal static extern unsafe void ConfigureTerminalForChildProcess(bool childUsesTerminal);
    }

    internal static uint GetCurrentProcessId() => (uint)Sys.GetPid();
}

// NOTE: extension method can't be nested inside Interop class.
internal static class InteropErrorExtensions
{
    // Intended usage is e.g. Interop.Error.EFAIL.Info() for brevity
    // vs. new Interop.ErrorInfo(Interop.Error.EFAIL) for synthesizing
    // errors. Errors originated from the system should be obtained
    // via GetLastErrorInfo(), not GetLastError().Info() as that will
    // convert twice, which is not only inefficient but also lossy if
    // we ever encounter a raw errno that no equivalent in the Error
    // enum.
    public static Interop.ErrorInfo Info(this Interop.Error error)
    {
        return new Interop.ErrorInfo(error);
    }
}
