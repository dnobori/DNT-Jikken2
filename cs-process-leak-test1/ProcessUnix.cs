// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;

namespace MyDiagnostics
{
    /// <summary>
    /// Provides a string parser that may be used instead of String.Split 
    /// to avoid unnecessary string and array allocations.
    /// </summary>
    internal struct StringParser
    {
        /// <summary>The string being parsed.</summary>
        private readonly string _buffer;

        /// <summary>The separator character used to separate subcomponents of the larger string.</summary>
        private readonly char _separator;

        /// <summary>true if empty subcomponents should be skipped; false to treat them as valid entries.</summary>
        private readonly bool _skipEmpty;

        /// <summary>The starting index from which to parse the current entry.</summary>
        private int _startIndex;

        /// <summary>The ending index that represents the next index after the last character that's part of the current entry.</summary>
        private int _endIndex;

        /// <summary>Initialize the StringParser.</summary>
        /// <param name="buffer">The string to parse.</param>
        /// <param name="separator">The separator character used to separate subcomponents of <paramref name="buffer"/>.</param>
        /// <param name="skipEmpty">true if empty subcomponents should be skipped; false to treat them as valid entries.  Defaults to false.</param>
        public StringParser(string buffer, char separator, bool skipEmpty = false)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }
            _buffer = buffer;
            _separator = separator;
            _skipEmpty = skipEmpty;
            _startIndex = -1;
            _endIndex = -1;
        }

        /// <summary>Moves to the next component of the string.</summary>
        /// <returns>true if there is a next component to be parsed; otherwise, false.</returns>
        public bool MoveNext()
        {
            if (_buffer == null)
            {
                throw new InvalidOperationException();
            }

            while (true)
            {
                if (_endIndex >= _buffer.Length)
                {
                    _startIndex = _endIndex;
                    return false;
                }

                int nextSeparator = _buffer.IndexOf(_separator, _endIndex + 1);
                _startIndex = _endIndex + 1;
                _endIndex = nextSeparator >= 0 ? nextSeparator : _buffer.Length;

                if (!_skipEmpty || _endIndex >= _startIndex + 1)
                {
                    return true;
                }
            }
        }

        /// <summary>
        /// Moves to the next component of the string.  If there isn't one, it throws an exception.
        /// </summary>
        public void MoveNextOrFail()
        {
            if (!MoveNext())
            {
                ThrowForInvalidData();
            }
        }

        /// <summary>
        /// Moves to the next component of the string and returns it as a string.
        /// </summary>
        /// <returns></returns>
        public string MoveAndExtractNext()
        {
            MoveNextOrFail();
            return _buffer.Substring(_startIndex, _endIndex - _startIndex);
        }

        /// <summary>
        /// Moves to the next component of the string, which must be enclosed in the only set of top-level parentheses
        /// in the string.  The extracted value will be everything between (not including) those parentheses.
        /// </summary>
        /// <returns></returns>
        public string MoveAndExtractNextInOuterParens()
        {
            // Move to the next position
            MoveNextOrFail();

            // After doing so, we should be sitting at a the opening paren.
            if (_buffer[_startIndex] != '(')
            {
                ThrowForInvalidData();
            }

            // Since we only allow for one top-level set of parentheses, find the last
            // parenthesis in the string; it's paired with the opening one we just found.
            int lastParen = _buffer.LastIndexOf(')');
            if (lastParen == -1 || lastParen < _startIndex)
            {
                ThrowForInvalidData();
            }

            // Extract the contents of the parens, then move our ending position to be after the paren
            string result = _buffer.Substring(_startIndex + 1, lastParen - _startIndex - 1);
            _endIndex = lastParen + 1;

            return result;
        }

        /// <summary>
        /// Gets the current subcomponent of the string as a string.
        /// </summary>
        public string ExtractCurrent()
        {
            if (_buffer == null || _startIndex == -1)
            {
                throw new InvalidOperationException();
            }
            return _buffer.Substring(_startIndex, _endIndex - _startIndex);
        }

        /// <summary>Moves to the next component and parses it as an Int32.</summary>
        public unsafe int ParseNextInt32()
        {
            MoveNextOrFail();

            bool negative = false;
            int result = 0;

            fixed (char* bufferPtr = _buffer)
            {
                char* p = bufferPtr + _startIndex;
                char* end = bufferPtr + _endIndex;

                if (p == end)
                {
                    ThrowForInvalidData();
                }

                if (*p == '-')
                {
                    negative = true;
                    p++;
                    if (p == end)
                    {
                        ThrowForInvalidData();
                    }
                }

                while (p != end)
                {
                    int d = *p - '0';
                    if (d < 0 || d > 9)
                    {
                        ThrowForInvalidData();
                    }
                    result = negative ? checked((result * 10) - d) : checked((result * 10) + d);

                    p++;
                }
            }

            System.Diagnostics.Debug.Assert(result == int.Parse(ExtractCurrent()), "Expected manually parsed result to match Parse result");
            return result;
        }

        /// <summary>Moves to the next component and parses it as an Int64.</summary>
        public unsafe long ParseNextInt64()
        {
            MoveNextOrFail();

            bool negative = false;
            long result = 0;

            fixed (char* bufferPtr = _buffer)
            {
                char* p = bufferPtr + _startIndex;
                char* end = bufferPtr + _endIndex;

                if (p == end)
                {
                    ThrowForInvalidData();
                }

                if (*p == '-')
                {
                    negative = true;
                    p++;
                    if (p == end)
                    {
                        ThrowForInvalidData();
                    }
                }

                while (p != end)
                {
                    int d = *p - '0';
                    if (d < 0 || d > 9)
                    {
                        ThrowForInvalidData();
                    }
                    result = negative ? checked((result * 10) - d) : checked((result * 10) + d);

                    p++;
                }
            }

            System.Diagnostics.Debug.Assert(result == long.Parse(ExtractCurrent()), "Expected manually parsed result to match Parse result");
            return result;
        }

        /// <summary>Moves to the next component and parses it as a UInt32.</summary>
        public unsafe uint ParseNextUInt32()
        {
            MoveNextOrFail();
            if (_startIndex == _endIndex)
            {
                ThrowForInvalidData();
            }

            uint result = 0;
            fixed (char* bufferPtr = _buffer)
            {
                char* p = bufferPtr + _startIndex;
                char* end = bufferPtr + _endIndex;
                while (p != end)
                {
                    int d = *p - '0';
                    if (d < 0 || d > 9)
                    {
                        ThrowForInvalidData();
                    }
                    result = (uint)checked((result * 10) + d);

                    p++;
                }
            }

            System.Diagnostics.Debug.Assert(result == uint.Parse(ExtractCurrent()), "Expected manually parsed result to match Parse result");
            return result;
        }

        /// <summary>Moves to the next component and parses it as a UInt64.</summary>
        public unsafe ulong ParseNextUInt64()
        {
            MoveNextOrFail();

            ulong result = 0;
            fixed (char* bufferPtr = _buffer)
            {
                char* p = bufferPtr + _startIndex;
                char* end = bufferPtr + _endIndex;
                while (p != end)
                {
                    int d = *p - '0';
                    if (d < 0 || d > 9)
                    {
                        ThrowForInvalidData();
                    }
                    result = checked((result * 10ul) + (ulong)d);

                    p++;
                }
            }

            System.Diagnostics.Debug.Assert(result == ulong.Parse(ExtractCurrent()), "Expected manually parsed result to match Parse result");
            return result;
        }

        /// <summary>Moves to the next component and parses it as a Char.</summary>
        public char ParseNextChar()
        {
            MoveNextOrFail();

            if (_endIndex - _startIndex != 1)
            {
                ThrowForInvalidData();
            }
            char result = _buffer[_startIndex];

            System.Diagnostics.Debug.Assert(result == char.Parse(ExtractCurrent()), "Expected manually parsed result to match Parse result");
            return result;
        }

        internal delegate T ParseRawFunc<T>(string buffer, ref int startIndex, ref int endIndex);

        /// <summary>
        /// Moves to the next component and hands the raw buffer and indexing data to a selector function
        /// that can validate and return the appropriate data from the component.
        /// </summary>
        internal T ParseRaw<T>(ParseRawFunc<T> selector)
        {
            MoveNextOrFail();
            return selector(_buffer, ref _startIndex, ref _endIndex);
        }

        /// <summary>
        /// Gets the current subcomponent and all remaining components of the string as a string.
        /// </summary>
        public string ExtractCurrentToEnd()
        {
            if (_buffer == null || _startIndex == -1)
            {
                throw new InvalidOperationException();
            }
            return _buffer.Substring(_startIndex);
        }

        /// <summary>Throws unconditionally for invalid data.</summary>
        private static void ThrowForInvalidData()
        {
            throw new InvalidDataException();
        }
    }

    // Class of safe handle which uses 0 or -1 as an invalid handle.
    public abstract class SafeHandleZeroOrMinusOneIsInvalid : SafeHandle
    {
        protected SafeHandleZeroOrMinusOneIsInvalid(bool ownsHandle) : base(IntPtr.Zero, ownsHandle)
        {
        }

        public override bool IsInvalid => handle == IntPtr.Zero || handle == new IntPtr(-1);
    }

    public sealed partial class SafeProcessHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal static readonly SafeProcessHandle InvalidHandle = new SafeProcessHandle();

        internal SafeProcessHandle()
            : this(IntPtr.Zero)
        {
        }

        internal SafeProcessHandle(IntPtr handle)
            : this(handle, true)
        {
        }

        public SafeProcessHandle(IntPtr existingHandle, bool ownsHandle)
            : base(ownsHandle)
        {
            SetHandle(existingHandle);
        }

        internal void InitialSetHandle(IntPtr h)
        {
            System.Diagnostics.Debug.Assert(IsInvalid, "Safe handle should only be set once");
            base.handle = h;
        }
    }

    public sealed partial class SafeProcessHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        // On Windows, SafeProcessHandle represents the actual OS handle for the process.
        // On Unix, there's no such concept.  Instead, the implementation manufactures
        // a WaitHandle that it manually sets when the process completes; SafeProcessHandle
        // then just wraps that same WaitHandle instance.  This allows consumers that use
        // Process.{Safe}Handle to initalize and use a WaitHandle to successfull use it on
        // Unix as well to wait for the process to complete.

        private readonly Microsoft.Win32.SafeHandles.SafeWaitHandle _handle;
        private readonly bool _releaseRef;

        internal SafeProcessHandle(int processId, Microsoft.Win32.SafeHandles.SafeWaitHandle handle) :
            this(handle.DangerousGetHandle(), ownsHandle: false)
        {
            ProcessId = processId;
            _handle = handle;
            handle.DangerousAddRef(ref _releaseRef);
        }

        internal int ProcessId { get; }

        protected override bool ReleaseHandle()
        {
            if (_releaseRef)
            {
                _handle.DangerousRelease();
            }
            return true;
        }
    }

    internal sealed class ProcessWaitHandle : WaitHandle
    {
        internal ProcessWaitHandle(ProcessWaitState processWaitState)
        {
            // Get the wait state's event, and use that event's safe wait handle
            // in place of ours.  This will let code register for completion notifications
            // on this ProcessWaitHandle and be notified when the wait state's handle completes.
            ManualResetEvent mre = processWaitState.EnsureExitedEvent();
            this.SetSafeWaitHandle(mre.GetSafeWaitHandle());
        }

        protected override void Dispose(bool explicitDisposing)
        {
            // ProcessWaitState will dispose the handle
            this.SafeWaitHandle = null;
            base.Dispose(explicitDisposing);
        }
    }

    public partial class Process : IDisposable
    {
        private static readonly UTF8Encoding s_utf8NoBom =
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private static volatile bool s_initialized = false;
        private static readonly object s_initializedGate = new object();
        private static readonly Interop.Sys.SigChldCallback s_sigChildHandler = OnSigChild;
        private static readonly ReaderWriterLockSlim s_processStartLock = new ReaderWriterLockSlim();
        private static int s_childrenUsingTerminalCount;

        /// <summary>
        /// Puts a Process component in state to interact with operating system processes that run in a 
        /// special mode by enabling the native property SeDebugPrivilege on the current thread.
        /// </summary>
        public static void EnterDebugMode()
        {
            // Nop.
        }

        /// <summary>
        /// Takes a Process component out of the state that lets it interact with operating system processes 
        /// that run in a special mode.
        /// </summary>
        public static void LeaveDebugMode()
        {
            // Nop.
        }

        public static Process Start(string fileName, string userName, SecureString password, string domain)
        {
            throw new PlatformNotSupportedException("SR.ProcessStartWithPasswordAndDomainNotSupported");
        }

        public static Process Start(string fileName, string arguments, string userName, SecureString password, string domain)
        {
            throw new PlatformNotSupportedException("SR.ProcessStartWithPasswordAndDomainNotSupported");
        }

        /// <summary>Terminates the associated process immediately.</summary>
        public void Kill()
        {
            EnsureState(State.HaveId);

            // Check if we know the process has exited. This avoids us targetting another
            // process that has a recycled PID. This only checks our internal state, the Kill call below
            // activly checks if the process is still alive.
            if (GetHasExited(refresh: false))
            {
                return;
            }

            int killResult = Interop.Sys.Kill(_processId, Interop.Sys.Signals.SIGKILL);
            if (killResult != 0)
            {
                Interop.Error error = Interop.Sys.GetLastError();

                // Don't throw if the process has exited.
                if (error == Interop.Error.ESRCH)
                {
                    return;
                }

                throw new Win32Exception(); // same exception as on Windows
            }
        }

        private bool GetHasExited(bool refresh)
            => GetWaitState().GetExited(out _, refresh);

        //private IEnumerable<Exception> KillTree()
        //{
        //    List<Exception> exceptions = null;
        //    KillTree(ref exceptions);
        //    return exceptions ?? Enumerable.Empty<Exception>();
        //}

        //private void KillTree(ref List<Exception> exceptions)
        //{
        //    // If the process has exited, we can no longer determine its children.
        //    // If we know the process has exited, stop already.
        //    if (GetHasExited(refresh: false))
        //    {
        //        return;
        //    }

        //    // Stop the process, so it won't start additional children.
        //    // This is best effort: kill can return before the process is stopped.
        //    int stopResult = Interop.Sys.Kill(_processId, Interop.Sys.Signals.SIGSTOP);
        //    if (stopResult != 0)
        //    {
        //        Interop.Error error = Interop.Sys.GetLastError();
        //        // Ignore 'process no longer exists' error.
        //        if (error != Interop.Error.ESRCH)
        //        {
        //            AddException(ref exceptions, new Win32Exception());
        //        }
        //        return;
        //    }

        //    IReadOnlyList<Process> children = GetChildProcesses();

        //    int killResult = Interop.Sys.Kill(_processId, Interop.Sys.Signals.SIGKILL);
        //    if (killResult != 0)
        //    {
        //        Interop.Error error = Interop.Sys.GetLastError();
        //        // Ignore 'process no longer exists' error.
        //        if (error != Interop.Error.ESRCH)
        //        {
        //            AddException(ref exceptions, new Win32Exception());
        //        }
        //    }

        //    foreach (Process childProcess in children)
        //    {
        //        childProcess.KillTree(ref exceptions);
        //        childProcess.Dispose();
        //    }

        //    void AddException(ref List<Exception> list, Exception e)
        //    {
        //        if (list == null)
        //        {
        //            list = new List<Exception>();
        //        }
        //        list.Add(e);
        //    }
        //}

        /// <summary>Discards any information about the associated process.</summary>
        private void RefreshCore()
        {
            // Nop.  No additional state to reset.
        }

        /// <summary>Additional logic invoked when the Process is closed.</summary>
        private void CloseCore()
        {
            if (_waitStateHolder != null)
            {
                _waitStateHolder.Dispose();
                _waitStateHolder = null;
            }
        }

        /// <summary>Additional configuration when a process ID is set.</summary>
        partial void ConfigureAfterProcessIdSet()
        {
            // Make sure that we configure the wait state holder for this process object, which we can only do once we have a process ID.
            System.Diagnostics.Debug.Assert(_haveProcessId, $"{nameof(ConfigureAfterProcessIdSet)} should only be called once a process ID is set");
            // Initialize WaitStateHolder for non-child processes
            GetWaitState();
        }

        /// <devdoc>
        ///     Make sure we are watching for a process exit.
        /// </devdoc>
        /// <internalonly/>
        private void EnsureWatchingForExit()
        {
            if (!_watchingForExit)
            {
                lock (this)
                {
                    if (!_watchingForExit)
                    {
                        System.Diagnostics.Debug.Assert(_waitHandle == null);
                        System.Diagnostics.Debug.Assert(_registeredWaitHandle == null);
                        System.Diagnostics.Debug.Assert(Associated, "Process.EnsureWatchingForExit called with no associated process");
                        _watchingForExit = true;
                        try
                        {
                            _waitHandle = new ProcessWaitHandle(GetWaitState());
                            _registeredWaitHandle = ThreadPool.RegisterWaitForSingleObject(_waitHandle,
                                new WaitOrTimerCallback(CompletionCallback), _waitHandle, -1, true);
                        }
                        catch
                        {
                            _waitHandle?.Dispose();
                            _waitHandle = null;
                            _watchingForExit = false;
                            throw;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Instructs the Process component to wait the specified number of milliseconds for the associated process to exit.
        /// </summary>
        private bool WaitForExitCore(int milliseconds)
        {
            bool exited = GetWaitState().WaitForExit(milliseconds);
            System.Diagnostics.Debug.Assert(exited || milliseconds != Timeout.Infinite);

            if (exited && milliseconds == Timeout.Infinite) // if we have a hard timeout, we cannot wait for the streams
            {
                if (_output != null)
                {
                    _output.WaitUtilEOF();
                }
                if (_error != null)
                {
                    _error.WaitUtilEOF();
                }
            }

            return exited;
        }

        /// <summary>Checks whether the process has exited and updates state accordingly.</summary>
        private void UpdateHasExited()
        {
            int? exitCode;
            _exited = GetWaitState().GetExited(out exitCode, refresh: true);
            if (_exited && exitCode != null)
            {
                _exitCode = exitCode.Value;
            }
        }

        /// <summary>Gets the time that the associated process exited.</summary>
        private DateTime ExitTimeCore
        {
            get { return GetWaitState().ExitTime; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the associated process priority
        /// should be temporarily boosted by the operating system when the main window
        /// has focus.
        /// </summary>
        private bool PriorityBoostEnabledCore
        {
            get { return false; } //Nop
            set { } // Nop
        }


        /// <summary>Gets the ID of the current process.</summary>
        private static int GetCurrentProcessId()
        {
            return Interop.Sys.GetPid();
        }


        private bool Equals(Process process) =>
            Id == process.Id;

        partial void ThrowIfExited(bool refresh)
        {
            // Don't allocate a ProcessWaitState.Holder unless we're refreshing.
            if (_waitStateHolder == null && !refresh)
            {
                return;
            }

            if (GetHasExited(refresh))
            {
                throw new InvalidOperationException("SR.ProcessHasExited" + _processId.ToString());
            }
        }

        /// <summary>
        /// Gets a short-term handle to the process, with the given access.  If a handle exists,
        /// then it is reused.  If the process has exited, it throws an exception.
        /// </summary>
        private SafeProcessHandle GetProcessHandle()
        {
            if (_haveProcessHandle)
            {
                ThrowIfExited(refresh: true);

                return _processHandle;
            }

            EnsureState(State.HaveNonExitedId | State.IsLocal);
            return new SafeProcessHandle(_processId, GetSafeWaitHandle());
        }

        /// <summary>
        /// Starts the process using the supplied start info. 
        /// With UseShellExecute option, we'll try the shell tools to launch it(e.g. "open fileName")
        /// </summary>
        /// <param name="startInfo">The start info with which to start the process.</param>
        private bool StartCore(System.Diagnostics.ProcessStartInfo startInfo)
        {
            EnsureInitialized();

            string filename;
            string[] argv;

            if (startInfo.UseShellExecute)
            {
                if (startInfo.RedirectStandardInput || startInfo.RedirectStandardOutput || startInfo.RedirectStandardError)
                {
                    throw new InvalidOperationException("SR.CantRedirectStreams");
                }
            }

            int stdinFd = -1, stdoutFd = -1, stderrFd = -1;
            string[] envp = CreateEnvp(startInfo);
            string cwd = !string.IsNullOrWhiteSpace(startInfo.WorkingDirectory) ? startInfo.WorkingDirectory : null;

            bool setCredentials = !string.IsNullOrEmpty(startInfo.UserName);
            uint userId = 0;
            uint groupId = 0;
            uint[] groups = null;

            // .NET applications don't echo characters unless there is a Console.Read operation.
            // Unix applications expect the terminal to be in an echoing state by default.
            // To support processes that interact with the terminal (e.g. 'vi'), we need to configure the
            // terminal to echo. We keep this configuration as long as there are children possibly using the terminal.
            bool usesTerminal = !(startInfo.RedirectStandardInput &&
                                  startInfo.RedirectStandardOutput &&
                                  startInfo.RedirectStandardError);

            if (startInfo.UseShellExecute)
            {
                string verb = startInfo.Verb;
                if (verb != string.Empty &&
                    !string.Equals(verb, "open", StringComparison.OrdinalIgnoreCase))
                {
                    throw new Win32Exception(Interop.Errors.ERROR_NO_ASSOCIATION);
                }

                // On Windows, UseShellExecute of executables and scripts causes those files to be executed.
                // To achieve this on Unix, we check if the file is executable (x-bit).
                // Some files may have the x-bit set even when they are not executable. This happens for example
                // when a Windows filesystem is mounted on Linux. To handle that, treat it as a regular file
                // when exec returns ENOEXEC (file format cannot be executed).
                bool isExecuting = false;
                filename = ResolveExecutableForShellExecute(startInfo.FileName, cwd);
                if (filename != null)
                {
                    argv = ParseArgv(startInfo);

                    isExecuting = ForkAndExecProcess(filename, argv, envp, cwd,
                        startInfo.RedirectStandardInput, startInfo.RedirectStandardOutput, startInfo.RedirectStandardError,
                        setCredentials, userId, groupId, groups,
                        out stdinFd, out stdoutFd, out stderrFd, usesTerminal,
                        throwOnNoExec: false); // return false instead of throwing on ENOEXEC
                }

                // use default program to open file/url
                if (!isExecuting)
                {
                    throw new Exception("!isExecuting");
                }
            }
            else
            {
                filename = ResolvePath(startInfo.FileName);
                argv = ParseArgv(startInfo);
                if (Directory.Exists(filename))
                {
                    throw new Win32Exception("SR.DirectoryNotValidAsInput");
                }

                ForkAndExecProcess(filename, argv, envp, cwd,
                    startInfo.RedirectStandardInput, startInfo.RedirectStandardOutput, startInfo.RedirectStandardError,
                    setCredentials, userId, groupId, groups,
                    out stdinFd, out stdoutFd, out stderrFd, usesTerminal);
            }

            // Configure the parent's ends of the redirection streams.
            // We use UTF8 encoding without BOM by-default(instead of Console encoding as on Windows)
            // as there is no good way to get this information from the native layer
            // and we do not want to take dependency on Console contract.
            if (startInfo.RedirectStandardInput)
            {
                System.Diagnostics.Debug.Assert(stdinFd >= 0);
                _standardInput = new StreamWriter(OpenStream(stdinFd, FileAccess.Write),
                    startInfo.StandardInputEncoding ?? s_utf8NoBom, StreamBufferSize)
                { AutoFlush = true };
            }
            if (startInfo.RedirectStandardOutput)
            {
                System.Diagnostics.Debug.Assert(stdoutFd >= 0);
                _standardOutput = new StreamReader(OpenStream(stdoutFd, FileAccess.Read),
                    startInfo.StandardOutputEncoding ?? s_utf8NoBom, true, StreamBufferSize);
            }
            if (startInfo.RedirectStandardError)
            {
                System.Diagnostics.Debug.Assert(stderrFd >= 0);
                _standardError = new StreamReader(OpenStream(stderrFd, FileAccess.Read),
                    startInfo.StandardErrorEncoding ?? s_utf8NoBom, true, StreamBufferSize);
            }

            return true;
        }

        private bool ForkAndExecProcess(
            string filename, string[] argv, string[] envp, string cwd,
            bool redirectStdin, bool redirectStdout, bool redirectStderr,
            bool setCredentials, uint userId, uint groupId, uint[] groups,
            out int stdinFd, out int stdoutFd, out int stderrFd,
            bool usesTerminal, bool throwOnNoExec = true)
        {
            if (string.IsNullOrEmpty(filename))
            {
                throw new Win32Exception(Interop.Error.ENOENT.Info().RawErrno);
            }

            // Lock to avoid races with OnSigChild
            // By using a ReaderWriterLock we allow multiple processes to start concurrently.
            s_processStartLock.EnterReadLock();
            try
            {
                if (usesTerminal)
                {
                    ConfigureTerminalForChildProcesses(1);
                }

                int childPid;

                // Invoke the shim fork/execve routine.  It will create pipes for all requested
                // redirects, fork a child process, map the pipe ends onto the appropriate stdin/stdout/stderr
                // descriptors, and execve to execute the requested process.  The shim implementation
                // is used to fork/execve as executing managed code in a forked process is not safe (only
                // the calling thread will transfer, thread IDs aren't stable across the fork, etc.)
                int errno = Interop.Sys.ForkAndExecProcess(
                    filename, argv, envp, cwd,
                    redirectStdin, redirectStdout, redirectStderr,
                    setCredentials, userId, groupId, groups,
                    out childPid,
                    out stdinFd, out stdoutFd, out stderrFd);

                if (errno == 0)
                {
                    // Ensure we'll reap this process.
                    // note: SetProcessId will set this if we don't set it first.
                    _waitStateHolder = new ProcessWaitState.Holder(childPid, isNewChild: true, usesTerminal);

                    // Store the child's information into this Process object.
                    System.Diagnostics.Debug.Assert(childPid >= 0);
                    SetProcessId(childPid);
                    SetProcessHandle(new SafeProcessHandle(_processId, GetSafeWaitHandle()));

                    return true;
                }
                else
                {
                    if (!throwOnNoExec &&
                        new Interop.ErrorInfo(errno).Error == Interop.Error.ENOEXEC)
                    {
                        return false;
                    }

                    throw new Win32Exception(errno);
                }
            }
            finally
            {
                s_processStartLock.ExitReadLock();

                if (_waitStateHolder == null && usesTerminal)
                {
                    // We failed to launch a child that could use the terminal.
                    s_processStartLock.EnterWriteLock();
                    ConfigureTerminalForChildProcesses(-1);
                    s_processStartLock.ExitWriteLock();
                }
            }
        }

        // -----------------------------
        // ---- PAL layer ends here ----
        // -----------------------------

        /// <summary>Finalizable holder for the underlying shared wait state object.</summary>
        private ProcessWaitState.Holder _waitStateHolder;

        /// <summary>Size to use for redirect streams and stream readers/writers.</summary>
        private const int StreamBufferSize = 4096;

        /// <summary>Converts the filename and arguments information from a ProcessStartInfo into an argv array.</summary>
        /// <param name="psi">The ProcessStartInfo.</param>
        /// <param name="resolvedExe">Resolved executable to open ProcessStartInfo.FileName</param>
        /// <param name="ignoreArguments">Don't pass ProcessStartInfo.Arguments</param>
        /// <returns>The argv array.</returns>
        private static string[] ParseArgv(System.Diagnostics.ProcessStartInfo psi, string resolvedExe = null, bool ignoreArguments = false)
        {
            if (string.IsNullOrEmpty(resolvedExe) &&
                (ignoreArguments || (string.IsNullOrEmpty(psi.Arguments) && psi.ArgumentList.Count == 0)))
            {
                return new string[] { psi.FileName };
            }

            var argvList = new List<string>();
            if (!string.IsNullOrEmpty(resolvedExe))
            {
                argvList.Add(resolvedExe);
                if (resolvedExe.Contains("kfmclient"))
                {
                    argvList.Add("openURL"); // kfmclient needs OpenURL
                }
            }

            argvList.Add(psi.FileName);

            if (!ignoreArguments)
            {
                if (!string.IsNullOrEmpty(psi.Arguments))
                {
                    ParseArgumentsIntoList(psi.Arguments, argvList);
                }
                else
                {
                    argvList.AddRange(psi.ArgumentList);
                }
            }
            return argvList.ToArray();
        }

        /// <summary>Converts the environment variables information from a ProcessStartInfo into an envp array.</summary>
        /// <param name="psi">The ProcessStartInfo.</param>
        /// <returns>The envp array.</returns>
        private static string[] CreateEnvp(System.Diagnostics.ProcessStartInfo psi)
        {
            var envp = new string[psi.Environment.Count];
            int index = 0;
            foreach (var pair in psi.Environment)
            {
                envp[index++] = pair.Key + "=" + pair.Value;
            }
            return envp;
        }

        private static string ResolveExecutableForShellExecute(string filename, string workingDirectory)
        {
            // Determine if filename points to an executable file.
            // filename may be an absolute path, a relative path or a uri.

            string resolvedFilename = null;
            // filename is an absolute path
            if (Path.IsPathRooted(filename))
            {
                if (File.Exists(filename))
                {
                    resolvedFilename = filename;
                }
            }
            // filename is a uri
            else if (Uri.TryCreate(filename, UriKind.Absolute, out Uri uri))
            {
                if (uri.IsFile && uri.Host == "" && File.Exists(uri.LocalPath))
                {
                    resolvedFilename = uri.LocalPath;
                }
            }
            // filename is relative
            else
            {
                // The WorkingDirectory property specifies the location of the executable.
                // If WorkingDirectory is an empty string, the current directory is understood to contain the executable.
                workingDirectory = workingDirectory != null ? Path.GetFullPath(workingDirectory) :
                                                              Directory.GetCurrentDirectory();
                string filenameInWorkingDirectory = Path.Combine(workingDirectory, filename);
                // filename is a relative path in the working directory
                if (File.Exists(filenameInWorkingDirectory))
                {
                    resolvedFilename = filenameInWorkingDirectory;
                }
                // find filename on PATH
                else
                {
                    resolvedFilename = FindProgramInPath(filename);
                }
            }

            if (resolvedFilename == null)
            {
                return null;
            }

            if (Interop.Sys.Access(resolvedFilename, Interop.Sys.AccessMode.X_OK) == 0)
            {
                return resolvedFilename;
            }
            else
            {
                return null;
            }
        }

        /// <summary>Resolves a path to the filename passed to ProcessStartInfo. </summary>
        /// <param name="filename">The filename.</param>
        /// <returns>The resolved path. It can return null in case of URLs.</returns>
        private static string ResolvePath(string filename)
        {
            // Follow the same resolution that Windows uses with CreateProcess:
            // 1. First try the exact path provided
            // 2. Then try the file relative to the executable directory
            // 3. Then try the file relative to the current directory
            // 4. then try the file in each of the directories specified in PATH
            // Windows does additional Windows-specific steps between 3 and 4,
            // and we ignore those here.

            // If the filename is a complete path, use it, regardless of whether it exists.
            if (Path.IsPathRooted(filename))
            {
                // In this case, it doesn't matter whether the file exists or not;
                // it's what the caller asked for, so it's what they'll get
                return filename;
            }

            // Then check the executable's directory
            string path = null;// GetExePath(); TODO
            if (path != null)
            {
                try
                {
                    path = Path.Combine(Path.GetDirectoryName(path), filename);
                    if (File.Exists(path))
                    {
                        return path;
                    }
                }
                catch (ArgumentException) { } // ignore any errors in data that may come from the exe path
            }

            // Then check the current directory
            path = Path.Combine(Directory.GetCurrentDirectory(), filename);
            if (File.Exists(path))
            {
                return path;
            }

            // Then check each directory listed in the PATH environment variables
            return FindProgramInPath(filename);
        }

        /// <summary>
        /// Gets the path to the program
        /// </summary>
        /// <param name="program"></param>
        /// <returns></returns>
        private static string FindProgramInPath(string program)
        {
            string path;
            string pathEnvVar = Environment.GetEnvironmentVariable("PATH");
            if (pathEnvVar != null)
            {
                var pathParser = new StringParser(pathEnvVar, ':', skipEmpty: true);
                while (pathParser.MoveNext())
                {
                    string subPath = pathParser.ExtractCurrent();
                    path = Path.Combine(subPath, program);
                    if (File.Exists(path))
                    {
                        return path;
                    }
                }
            }
            return null;
        }

        /// <summary>Opens a stream around the specified file descriptor and with the specified access.</summary>
        /// <param name="fd">The file descriptor.</param>
        /// <param name="access">The access mode.</param>
        /// <returns>The opened stream.</returns>
        private static FileStream OpenStream(int fd, FileAccess access)
        {
            System.Diagnostics.Debug.Assert(fd >= 0);
            return new FileStream(
                new Microsoft.Win32.SafeHandles.SafeFileHandle((IntPtr)fd, ownsHandle: true),
                access, StreamBufferSize, isAsync: false);
        }

        /// <summary>Parses a command-line argument string into a list of arguments.</summary>
        /// <param name="arguments">The argument string.</param>
        /// <param name="results">The list into which the component arguments should be stored.</param>
        /// <remarks>
        /// This follows the rules outlined in "Parsing C++ Command-Line Arguments" at 
        /// https://msdn.microsoft.com/en-us/library/17w5ykft.aspx.
        /// </remarks>
        private static void ParseArgumentsIntoList(string arguments, List<string> results)
        {
            // Iterate through all of the characters in the argument string.
            for (int i = 0; i < arguments.Length; i++)
            {
                while (i < arguments.Length && (arguments[i] == ' ' || arguments[i] == '\t'))
                    i++;

                if (i == arguments.Length)
                    break;

                results.Add(GetNextArgument(arguments, ref i));
            }
        }

        private static string GetNextArgument(string arguments, ref int i)
        {
            var currentArgument = new StringBuilder();
            bool inQuotes = false;

            while (i < arguments.Length)
            {
                // From the current position, iterate through contiguous backslashes.
                int backslashCount = 0;
                while (i < arguments.Length && arguments[i] == '\\')
                {
                    i++;
                    backslashCount++;
                }

                if (backslashCount > 0)
                {
                    if (i >= arguments.Length || arguments[i] != '"')
                    {
                        // Backslashes not followed by a double quote:
                        // they should all be treated as literal backslashes.
                        currentArgument.Append('\\', backslashCount);
                    }
                    else
                    {
                        // Backslashes followed by a double quote:
                        // - Output a literal slash for each complete pair of slashes
                        // - If one remains, use it to make the subsequent quote a literal.
                        currentArgument.Append('\\', backslashCount / 2);
                        if (backslashCount % 2 != 0)
                        {
                            currentArgument.Append('"');
                            i++;
                        }
                    }

                    continue;
                }

                char c = arguments[i];

                // If this is a double quote, track whether we're inside of quotes or not.
                // Anything within quotes will be treated as a single argument, even if
                // it contains spaces.
                if (c == '"')
                {
                    if (inQuotes && i < arguments.Length - 1 && arguments[i + 1] == '"')
                    {
                        // Two consecutive double quotes inside an inQuotes region should result in a literal double quote 
                        // (the parser is left in the inQuotes region).
                        // This behavior is not part of the spec of code:ParseArgumentsIntoList, but is compatible with CRT 
                        // and .NET Framework.
                        currentArgument.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }

                    i++;
                    continue;
                }

                // If this is a space/tab and we're not in quotes, we're done with the current
                // argument, it should be added to the results and then reset for the next one.
                if ((c == ' ' || c == '\t') && !inQuotes)
                {
                    break;
                }

                // Nothing special; add the character to the current argument.
                currentArgument.Append(c);
                i++;
            }

            return currentArgument.ToString();
        }

        /// <summary>Gets the wait state for this Process object.</summary>
        private ProcessWaitState GetWaitState()
        {
            if (_waitStateHolder == null)
            {
                EnsureState(State.HaveId);
                _waitStateHolder = new ProcessWaitState.Holder(_processId);
            }
            return _waitStateHolder._state;
        }

        private Microsoft.Win32.SafeHandles.SafeWaitHandle GetSafeWaitHandle()
            => GetWaitState().EnsureExitedEvent().GetSafeWaitHandle();

        public IntPtr MainWindowHandle => IntPtr.Zero;

        private bool CloseMainWindowCore() => false;

        public string MainWindowTitle => string.Empty;

        public bool Responding => true;

        private bool WaitForInputIdleCore(int milliseconds) => throw new InvalidOperationException("SR.InputIdleUnkownError");

        private static void EnsureInitialized()
        {
            if (s_initialized)
            {
                return;
            }

            lock (s_initializedGate)
            {
                if (!s_initialized)
                {
                    if (!Interop.Sys.InitializeTerminalAndSignalHandling())
                    {
                        throw new Win32Exception();
                    }

                    // Register our callback.
                    Interop.Sys.RegisterForSigChld(s_sigChildHandler);

                    s_initialized = true;
                }
            }
        }

        private static void OnSigChild(bool reapAll)
        {
            // Lock to avoid races with Process.Start
            s_processStartLock.EnterWriteLock();
            try
            {
                ProcessWaitState.CheckChildren(reapAll);
            }
            finally
            {
                s_processStartLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// This method is called when the number of child processes that are using the terminal changes.
        /// It updates the terminal configuration if necessary.
        /// </summary>
        internal static void ConfigureTerminalForChildProcesses(int increment)
        {
            System.Diagnostics.Debug.Assert(increment != 0);

            int childrenUsingTerminalRemaining = Interlocked.Add(ref s_childrenUsingTerminalCount, increment);
            if (increment > 0)
            {
                System.Diagnostics.Debug.Assert(s_processStartLock.IsReadLockHeld);

                // At least one child is using the terminal.
                Interop.Sys.ConfigureTerminalForChildProcess(childUsesTerminal: true);
            }
            else
            {
                System.Diagnostics.Debug.Assert(s_processStartLock.IsWriteLockHeld);

                if (childrenUsingTerminalRemaining == 0)
                {
                    // No more children are using the terminal.
                    Interop.Sys.ConfigureTerminalForChildProcess(childUsesTerminal: false);
                }
            }
        }
    }
}
