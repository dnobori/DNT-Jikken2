// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyDiagnostics
{
    internal static partial class PasteArguments
    {        /// <summary>
             /// Repastes a set of arguments into a linear string that parses back into the originals under pre- or post-2008 VC parsing rules.
             /// On Unix: the rules for parsing the executable name (argv[0]) are ignored.
             /// </summary>
        internal static string Paste(IEnumerable<string> arguments, bool pasteFirstArgumentUsingArgV0Rules)
        {
            var stringBuilder = new StringBuilder();
            foreach (string argument in arguments)
            {
                AppendArgument(stringBuilder, argument);
            }
            return stringBuilder.ToString();
        }

        internal static void AppendArgument(StringBuilder stringBuilder, string argument)
        {
            if (stringBuilder.Length != 0)
            {
                stringBuilder.Append(' ');
            }

            // Parsing rules for non-argv[0] arguments:
            //   - Backslash is a normal character except followed by a quote.
            //   - 2N backslashes followed by a quote ==> N literal backslashes followed by unescaped quote
            //   - 2N+1 backslashes followed by a quote ==> N literal backslashes followed by a literal quote
            //   - Parsing stops at first whitespace outside of quoted region.
            //   - (post 2008 rule): A closing quote followed by another quote ==> literal quote, and parsing remains in quoting mode.
            if (argument.Length != 0 && ContainsNoWhitespaceOrQuotes(argument))
            {
                // Simple case - no quoting or changes needed.
                stringBuilder.Append(argument);
            }
            else
            {
                stringBuilder.Append(Quote);
                int idx = 0;
                while (idx < argument.Length)
                {
                    char c = argument[idx++];
                    if (c == Backslash)
                    {
                        int numBackSlash = 1;
                        while (idx < argument.Length && argument[idx] == Backslash)
                        {
                            idx++;
                            numBackSlash++;
                        }

                        if (idx == argument.Length)
                        {
                            // We'll emit an end quote after this so must double the number of backslashes.
                            stringBuilder.Append(Backslash, numBackSlash * 2);
                        }
                        else if (argument[idx] == Quote)
                        {
                            // Backslashes will be followed by a quote. Must double the number of backslashes.
                            stringBuilder.Append(Backslash, numBackSlash * 2 + 1);
                            stringBuilder.Append(Quote);
                            idx++;
                        }
                        else
                        {
                            // Backslash will not be followed by a quote, so emit as normal characters.
                            stringBuilder.Append(Backslash, numBackSlash);
                        }

                        continue;
                    }

                    if (c == Quote)
                    {
                        // Escape the quote so it appears as a literal. This also guarantees that we won't end up generating a closing quote followed
                        // by another quote (which parses differently pre-2008 vs. post-2008.)
                        stringBuilder.Append(Backslash);
                        stringBuilder.Append(Quote);
                        continue;
                    }

                    stringBuilder.Append(c);
                }

                stringBuilder.Append(Quote);
            }
        }

        private static bool ContainsNoWhitespaceOrQuotes(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsWhiteSpace(c) || c == Quote)
                {
                    return false;
                }
            }

            return true;
        }

        private const char Quote = '\"';
        private const char Backslash = '\\';
    }

    public delegate void DataReceivedEventHandler(object sender, DataReceivedEventArgs e);

    public class DataReceivedEventArgs : EventArgs
    {
        private readonly string _data;

        internal DataReceivedEventArgs(string data)
        {
            _data = data;
        }

        public string Data
        {
            get { return _data; }
        }
    }


    internal sealed class AsyncStreamReader : IDisposable
    {
        private const int DefaultBufferSize = 1024;  // Byte buffer size

        private readonly Stream _stream;
        private readonly Decoder _decoder;
        private readonly byte[] _byteBuffer;
        private readonly char[] _charBuffer;

        // Delegate to call user function.
        private readonly Action<string> _userCallBack;

        private readonly CancellationTokenSource _cts;
        private Task _readToBufferTask;
        private readonly Queue<string> _messageQueue;
        private StringBuilder _sb;
        private bool _bLastCarriageReturn;
        private bool _cancelOperation;

        // Cache the last position scanned in sb when searching for lines.
        private int _currentLinePos;

        // Creates a new AsyncStreamReader for the given stream. The
        // character encoding is set by encoding and the buffer size,
        // in number of 16-bit characters, is set by bufferSize.
        internal AsyncStreamReader(Stream stream, Action<string> callback, Encoding encoding)
        {
            System.Diagnostics.Debug.Assert(stream != null && encoding != null && callback != null, "Invalid arguments!");
            System.Diagnostics.Debug.Assert(stream.CanRead, "Stream must be readable!");

            _stream = stream;
            _userCallBack = callback;
            _decoder = encoding.GetDecoder();
            _byteBuffer = new byte[DefaultBufferSize];

            // This is the maximum number of chars we can get from one iteration in loop inside ReadBuffer.
            // Used so ReadBuffer can tell when to copy data into a user's char[] directly, instead of our internal char[].
            int maxCharsPerBuffer = encoding.GetMaxCharCount(DefaultBufferSize);
            _charBuffer = new char[maxCharsPerBuffer];

            _cts = new CancellationTokenSource();
            _messageQueue = new Queue<string>();
        }

        // User calls BeginRead to start the asynchronous read
        internal void BeginReadLine()
        {
            _cancelOperation = false;

            if (_sb == null)
            {
                _sb = new StringBuilder(DefaultBufferSize);
                _readToBufferTask = Task.Run((Func<Task>)ReadBufferAsync);
            }
            else
            {
                FlushMessageQueue(rethrowInNewThread: false);
            }
        }

        internal void CancelOperation()
        {
            _cancelOperation = true;
        }

        // This is the async callback function. Only one thread could/should call this.
        private async Task ReadBufferAsync()
        {
            while (true)
            {
                try
                {
                    int bytesRead = await _stream.ReadAsync(new Memory<byte>(_byteBuffer), _cts.Token).ConfigureAwait(false);
                    if (bytesRead == 0)
                        break;

                    int charLen = _decoder.GetChars(_byteBuffer, 0, bytesRead, _charBuffer, 0);
                    _sb.Append(_charBuffer, 0, charLen);
                    MoveLinesFromStringBuilderToMessageQueue();
                }
                catch (IOException)
                {
                    // We should ideally consume errors from operations getting cancelled
                    // so that we don't crash the unsuspecting parent with an unhandled exc.
                    // This seems to come in 2 forms of exceptions (depending on platform and scenario),
                    // namely OperationCanceledException and IOException (for errorcode that we don't
                    // map explicitly).
                    break; // Treat this as EOF
                }
                catch (OperationCanceledException)
                {
                    // We should consume any OperationCanceledException from child read here
                    // so that we don't crash the parent with an unhandled exc
                    break; // Treat this as EOF
                }

                // If user's delegate throws exception we treat this as EOF and
                // completing without processing current buffer content
                if (FlushMessageQueue(rethrowInNewThread: true))
                {
                    return;
                }
            }

            // We're at EOF, process current buffer content and flush message queue.
            lock (_messageQueue)
            {
                if (_sb.Length != 0)
                {
                    _messageQueue.Enqueue(_sb.ToString());
                    _sb.Length = 0;
                }
                _messageQueue.Enqueue(null);
            }

            FlushMessageQueue(rethrowInNewThread: true);
        }

        // Read lines stored in StringBuilder and the buffer we just read into.
        // A line is defined as a sequence of characters followed by
        // a carriage return ('\r'), a line feed ('\n'), or a carriage return
        // immediately followed by a line feed. The resulting string does not
        // contain the terminating carriage return and/or line feed. The returned
        // value is null if the end of the input stream has been reached.
        private void MoveLinesFromStringBuilderToMessageQueue()
        {
            int currentIndex = _currentLinePos;
            int lineStart = 0;
            int len = _sb.Length;

            // skip a beginning '\n' character of new block if last block ended 
            // with '\r'
            if (_bLastCarriageReturn && (len > 0) && _sb[0] == '\n')
            {
                currentIndex = 1;
                lineStart = 1;
                _bLastCarriageReturn = false;
            }

            while (currentIndex < len)
            {
                char ch = _sb[currentIndex];
                // Note the following common line feed chars:
                // \n - UNIX   \r\n - DOS   \r - Mac
                if (ch == '\r' || ch == '\n')
                {
                    string line = _sb.ToString(lineStart, currentIndex - lineStart);
                    lineStart = currentIndex + 1;
                    // skip the "\n" character following "\r" character
                    if ((ch == '\r') && (lineStart < len) && (_sb[lineStart] == '\n'))
                    {
                        lineStart++;
                        currentIndex++;
                    }

                    lock (_messageQueue)
                    {
                        _messageQueue.Enqueue(line);
                    }
                }
                currentIndex++;
            }
            if ((len > 0) && _sb[len - 1] == '\r')
            {
                _bLastCarriageReturn = true;
            }
            // Keep the rest characters which can't form a new line in string builder.
            if (lineStart < len)
            {
                if (lineStart == 0)
                {
                    // we found no breaklines, in this case we cache the position
                    // so next time we don't have to restart from the beginning
                    _currentLinePos = currentIndex;
                }
                else
                {
                    _sb.Remove(0, lineStart);
                    _currentLinePos = 0;
                }
            }
            else
            {
                _sb.Length = 0;
                _currentLinePos = 0;
            }
        }

        // If everything runs without exception, returns false.
        // If an exception occurs and rethrowInNewThread is true, returns true.
        // If an exception occurs and rethrowInNewThread is false, the exception propagates.
        private bool FlushMessageQueue(bool rethrowInNewThread)
        {
            try
            {
                // Keep going until we're out of data to process.
                while (true)
                {
                    // Get the next line (if there isn't one, we're done) and 
                    // invoke the user's callback with it.
                    string line;
                    lock (_messageQueue)
                    {
                        if (_messageQueue.Count == 0)
                        {
                            break;
                        }
                        line = _messageQueue.Dequeue();
                    }

                    if (!_cancelOperation)
                    {
                        _userCallBack(line); // invoked outside of the lock
                    }
                }
                return false;
            }
            catch (Exception e)
            {
                // If rethrowInNewThread is true, we can't let the exception propagate synchronously on this thread,
                // so propagate it in a thread pool thread and return true to indicate to the caller that this failed.
                // Otherwise, let the exception propagate.
                if (rethrowInNewThread)
                {
                    ThreadPool.QueueUserWorkItem(edi => ((ExceptionDispatchInfo)edi).Throw(), ExceptionDispatchInfo.Capture(e));
                    return true;
                }
                throw;
            }
        }

        // Wait until we hit EOF. This is called from Process.WaitForExit
        // We will lose some information if we don't do this.
        internal void WaitUtilEOF()
        {
            if (_readToBufferTask != null)
            {
                _readToBufferTask.GetAwaiter().GetResult();
                _readToBufferTask = null;
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
        }
    }

    /// <devdoc>
    ///     Specifies the reason a thread is waiting.
    /// </devdoc>
    public enum ThreadWaitReason
    {
        /// <devdoc>
        ///     Thread is waiting for the scheduler.
        /// </devdoc>
        Executive,

        /// <devdoc>
        ///     Thread is waiting for a free virtual memory page.
        /// </devdoc>
        FreePage,

        /// <devdoc>
        ///     Thread is waiting for a virtual memory page to arrive in memory.
        /// </devdoc>
        PageIn,

        /// <devdoc>
        ///     Thread is waiting for a system allocation.
        /// </devdoc>
        SystemAllocation,

        /// <devdoc>
        ///     Thread execution is delayed.
        /// </devdoc>
        ExecutionDelay,

        /// <devdoc>
        ///     Thread execution is suspended.
        /// </devdoc>
        Suspended,

        /// <devdoc>
        ///     Thread is waiting for a user request.
        /// </devdoc>
        UserRequest,

        /// <devdoc>
        ///     Thread is waiting for event pair high.
        /// </devdoc>
        EventPairHigh,

        /// <devdoc>
        ///     Thread is waiting for event pair low.
        /// </devdoc>
        EventPairLow,

        /// <devdoc>
        ///     Thread is waiting for a local procedure call to arrive.
        /// </devdoc>
        LpcReceive,

        /// <devdoc>
        ///     Thread is waiting for reply to a local procedure call to arrive.
        /// </devdoc>
        LpcReply,

        /// <devdoc>
        ///     Thread is waiting for virtual memory.
        /// </devdoc>
        VirtualMemory,

        /// <devdoc>
        ///     Thread is waiting for a virtual memory page to be written to disk.
        /// </devdoc>
        PageOut,

        /// <devdoc>
        ///     Thread is waiting for an unknown reason.
        /// </devdoc>
        Unknown
    }

    /// <devdoc>
    ///     This data structure contains information about a thread in a process that
    ///     is collected in bulk by querying the operating system.  The reason to
    ///     make this a separate structure from the ProcessThread component is so that we
    ///     can throw it away all at once when Refresh is called on the component.
    /// </devdoc>
    /// <internalonly/>
    internal sealed class ThreadInfo
    {
        internal ulong _threadId;
        internal int _processId;
        internal int _basePriority;
        internal int _currentPriority;
        internal IntPtr _startAddress;
        internal ThreadState _threadState;
        internal ThreadWaitReason _threadWaitReason;
    }

    /// <summary>
    /// This data structure contains information about a process that is collected
    /// in bulk by querying the operating system.  The reason to make this a separate
    /// structure from the process component is so that we can throw it away all at once
    /// when Refresh is called on the component.
    /// </summary>
    internal sealed class ProcessInfo
    {
        internal readonly List<ThreadInfo> _threadInfoList = new List<ThreadInfo>();
        internal int BasePriority { get; set; }
        internal string ProcessName { get; set; }
        internal int ProcessId { get; set; }
        internal long PoolPagedBytes { get; set; }
        internal long PoolNonPagedBytes { get; set; }
        internal long VirtualBytes { get; set; }
        internal long VirtualBytesPeak { get; set; }
        internal long WorkingSetPeak { get; set; }
        internal long WorkingSet { get; set; }
        internal long PageFileBytesPeak { get; set; }
        internal long PageFileBytes { get; set; }
        internal long PrivateBytes { get; set; }
        internal int SessionId { get; set; }
        internal int HandleCount { get; set; }

        internal ProcessInfo()
        {
            BasePriority = 0;
            ProcessName = "";
            ProcessId = 0;
            PoolPagedBytes = 0;
            PoolNonPagedBytes = 0;
            VirtualBytes = 0;
            VirtualBytesPeak = 0;
            WorkingSet = 0;
            WorkingSetPeak = 0;
            PageFileBytes = 0;
            PageFileBytesPeak = 0;
            PrivateBytes = 0;
            SessionId = 0;
            HandleCount = 0;
        }
    }
    
    /// <devdoc>
         ///    <para>
         ///       Provides access to local and remote
         ///       processes. Enables you to start and stop system processes.
         ///    </para>
         /// </devdoc>
    public partial class Process : Component
    {
        private bool _haveProcessId;
        private int _processId;
        private bool _haveProcessHandle;
        private SafeProcessHandle _processHandle;
        private bool _isRemoteMachine;
        private string _machineName;
        private ProcessInfo _processInfo;

        //private ProcessThreadCollection _threads;
        //private ProcessModuleCollection _modules;

        private bool _haveWorkingSetLimits;
        private IntPtr _minWorkingSet;
        private IntPtr _maxWorkingSet;

        private bool _haveProcessorAffinity;
        private IntPtr _processorAffinity;

        private bool _havePriorityClass;
        private System.Diagnostics.ProcessPriorityClass _priorityClass;

        private System.Diagnostics.ProcessStartInfo _startInfo;

        private bool _watchForExit;
        private bool _watchingForExit;
        private EventHandler _onExited;
        private bool _exited;
        private int _exitCode;

        private DateTime? _startTime;
        private DateTime _exitTime;
        private bool _haveExitTime;

        private bool _priorityBoostEnabled;
        private bool _havePriorityBoostEnabled;

        private bool _raisedOnExited;
        private RegisteredWaitHandle _registeredWaitHandle;
        private WaitHandle _waitHandle;
        private StreamReader _standardOutput;
        private StreamWriter _standardInput;
        private StreamReader _standardError;
        private bool _disposed;

        private static object s_createProcessLock = new object();

        private bool _standardInputAccessed;

        private StreamReadMode _outputStreamReadMode;
        private StreamReadMode _errorStreamReadMode;

        // Support for asynchronously reading streams
        public event DataReceivedEventHandler OutputDataReceived;
        public event DataReceivedEventHandler ErrorDataReceived;

        // Abstract the stream details
        internal AsyncStreamReader _output;
        internal AsyncStreamReader _error;
        internal bool _pendingOutputRead;
        internal bool _pendingErrorRead;

        private static int s_cachedSerializationSwitch = 0;

        /// <devdoc>
        ///    <para>
        ///       Initializes a new instance of the <see cref='System.Diagnostics.Process'/> class.
        ///    </para>
        /// </devdoc>
        public Process()
        {
            // This class once inherited a finalizer. For backward compatibility it has one so that 
            // any derived class that depends on it will see the behaviour expected. Since it is
            // not used by this class itself, suppress it immediately if this is not an instance
            // of a derived class it doesn't suffer the GC burden of finalization.
            if (GetType() == typeof(Process))
            {
                GC.SuppressFinalize(this);
            }

            _machineName = ".";
            _outputStreamReadMode = StreamReadMode.Undefined;
            _errorStreamReadMode = StreamReadMode.Undefined;
        }

        private Process(string machineName, bool isRemoteMachine, int processId, ProcessInfo processInfo)
        {
            GC.SuppressFinalize(this);
            _processInfo = processInfo;
            _machineName = machineName;
            _isRemoteMachine = isRemoteMachine;
            _processId = processId;
            _haveProcessId = true;
            _outputStreamReadMode = StreamReadMode.Undefined;
            _errorStreamReadMode = StreamReadMode.Undefined;
        }

        public SafeProcessHandle SafeHandle
        {
            get
            {
                EnsureState(State.Associated);
                return GetOrOpenProcessHandle();
            }
        }

        public IntPtr Handle => SafeHandle.DangerousGetHandle();

        /// <devdoc>
        ///     Returns whether this process component is associated with a real process.
        /// </devdoc>
        /// <internalonly/>
        bool Associated
        {
            get { return _haveProcessId || _haveProcessHandle; }
        }

        /// <devdoc>
        ///    <para>
        ///       Gets the base priority of
        ///       the associated process.
        ///    </para>
        /// </devdoc>
        public int BasePriority
        {
            get
            {
                EnsureState(State.HaveProcessInfo);
                return _processInfo.BasePriority;
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Gets
        ///       the
        ///       value that was specified by the associated process when it was terminated.
        ///    </para>
        /// </devdoc>
        public int ExitCode
        {
            get
            {
                EnsureState(State.Exited);
                return _exitCode;
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Gets a
        ///       value indicating whether the associated process has been terminated.
        ///    </para>
        /// </devdoc>
        public bool HasExited
        {
            get
            {
                if (!_exited)
                {
                    EnsureState(State.Associated);
                    UpdateHasExited();
                    if (_exited)
                    {
                        RaiseOnExited();
                    }
                }
                return _exited;
            }
        }

        /// <summary>Gets the time the associated process was started.</summary>
        public DateTime StartTime
        {
            get
            {
                //if (!_startTime.HasValue)
                //{
                //    _startTime = StartTimeCore;
                //}
                return _startTime.Value;
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Gets the time that the associated process exited.
        ///    </para>
        /// </devdoc>
        public DateTime ExitTime
        {
            get
            {
                if (!_haveExitTime)
                {
                    EnsureState(State.Exited);
                    _exitTime = ExitTimeCore;
                    _haveExitTime = true;
                }
                return _exitTime;
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Gets
        ///       the unique identifier for the associated process.
        ///    </para>
        /// </devdoc>
        public int Id
        {
            get
            {
                EnsureState(State.HaveId);
                return _processId;
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Gets
        ///       the name of the computer on which the associated process is running.
        ///    </para>
        /// </devdoc>
        public string MachineName
        {
            get
            {
                EnsureState(State.Associated);
                return _machineName;
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Gets or sets the maximum allowable working set for the associated
        ///       process.
        ///    </para>
        /// </devdoc>
        public IntPtr MaxWorkingSet
        {
            get
            {
                EnsureWorkingSetLimits();
                return _maxWorkingSet;
            }
            set
            {
                SetWorkingSetLimits(null, value);
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Gets or sets the minimum allowable working set for the associated
        ///       process.
        ///    </para>
        /// </devdoc>
        public IntPtr MinWorkingSet
        {
            get
            {
                EnsureWorkingSetLimits();
                return _minWorkingSet;
            }
            set
            {
                SetWorkingSetLimits(value, null);
            }
        }

        //public ProcessModuleCollection Modules
        //{
        //    get
        //    {
        //        if (_modules == null)
        //        {
        //            EnsureState(State.HaveNonExitedId | State.IsLocal);
        //            _modules = ProcessManager.GetModules(_processId);
        //        }
        //        return _modules;
        //    }
        //}

        public long NonpagedSystemMemorySize64
        {
            get
            {
                EnsureState(State.HaveProcessInfo);
                return _processInfo.PoolNonPagedBytes;
            }
        }

        [ObsoleteAttribute("This property has been deprecated.  Please use System.Diagnostics.Process.NonpagedSystemMemorySize64 instead.  https://go.microsoft.com/fwlink/?linkid=14202")]
        public int NonpagedSystemMemorySize
        {
            get
            {
                EnsureState(State.HaveProcessInfo);
                return unchecked((int)_processInfo.PoolNonPagedBytes);
            }
        }


        public long PagedMemorySize64
        {
            get
            {
                EnsureState(State.HaveProcessInfo);
                return _processInfo.PageFileBytes;
            }
        }

        [ObsoleteAttribute("This property has been deprecated.  Please use System.Diagnostics.Process.PagedMemorySize64 instead.  https://go.microsoft.com/fwlink/?linkid=14202")]
        public int PagedMemorySize
        {
            get
            {
                EnsureState(State.HaveProcessInfo);
                return unchecked((int)_processInfo.PageFileBytes);
            }
        }


        public long PagedSystemMemorySize64
        {
            get
            {
                EnsureState(State.HaveProcessInfo);
                return _processInfo.PoolPagedBytes;
            }
        }

        [ObsoleteAttribute("This property has been deprecated.  Please use System.Diagnostics.Process.PagedSystemMemorySize64 instead.  https://go.microsoft.com/fwlink/?linkid=14202")]
        public int PagedSystemMemorySize
        {
            get
            {
                EnsureState(State.HaveProcessInfo);
                return unchecked((int)_processInfo.PoolPagedBytes);
            }
        }


        public long PeakPagedMemorySize64
        {
            get
            {
                EnsureState(State.HaveProcessInfo);
                return _processInfo.PageFileBytesPeak;
            }
        }

        [ObsoleteAttribute("This property has been deprecated.  Please use System.Diagnostics.Process.PeakPagedMemorySize64 instead.  https://go.microsoft.com/fwlink/?linkid=14202")]
        public int PeakPagedMemorySize
        {
            get
            {
                EnsureState(State.HaveProcessInfo);
                return unchecked((int)_processInfo.PageFileBytesPeak);
            }
        }

        public long PeakWorkingSet64
        {
            get
            {
                EnsureState(State.HaveProcessInfo);
                return _processInfo.WorkingSetPeak;
            }
        }

        [ObsoleteAttribute("This property has been deprecated.  Please use System.Diagnostics.Process.PeakWorkingSet64 instead.  https://go.microsoft.com/fwlink/?linkid=14202")]
        public int PeakWorkingSet
        {
            get
            {
                EnsureState(State.HaveProcessInfo);
                return unchecked((int)_processInfo.WorkingSetPeak);
            }
        }

        public long PeakVirtualMemorySize64
        {
            get
            {
                EnsureState(State.HaveProcessInfo);
                return _processInfo.VirtualBytesPeak;
            }
        }

        [ObsoleteAttribute("This property has been deprecated.  Please use System.Diagnostics.Process.PeakVirtualMemorySize64 instead.  https://go.microsoft.com/fwlink/?linkid=14202")]
        public int PeakVirtualMemorySize
        {
            get
            {
                EnsureState(State.HaveProcessInfo);
                return unchecked((int)_processInfo.VirtualBytesPeak);
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Gets or sets a value indicating whether the associated process priority
        ///       should be temporarily boosted by the operating system when the main window
        ///       has focus.
        ///    </para>
        /// </devdoc>
        public bool PriorityBoostEnabled
        {
            get
            {
                if (!_havePriorityBoostEnabled)
                {
                    _priorityBoostEnabled = PriorityBoostEnabledCore;
                    _havePriorityBoostEnabled = true;
                }
                return _priorityBoostEnabled;
            }
            set
            {
                PriorityBoostEnabledCore = value;
                _priorityBoostEnabled = value;
                _havePriorityBoostEnabled = true;
            }
        }

        ///// <devdoc>
        /////    <para>
        /////       Gets or sets the overall priority category for the
        /////       associated process.
        /////    </para>
        ///// </devdoc>
        //public ProcessPriorityClass PriorityClass
        //{
        //    get
        //    {
        //        if (!_havePriorityClass)
        //        {
        //            _priorityClass = PriorityClassCore;
        //            _havePriorityClass = true;
        //        }
        //        return _priorityClass;
        //    }
        //    set
        //    {
        //        if (!Enum.IsDefined(typeof(ProcessPriorityClass), value))
        //        {
        //            throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(ProcessPriorityClass));
        //        }

        //        PriorityClassCore = value;
        //        _priorityClass = value;
        //        _havePriorityClass = true;
        //    }
        //}

        public long PrivateMemorySize64
        {
            get
            {
                EnsureState(State.HaveProcessInfo);
                return _processInfo.PrivateBytes;
            }
        }

        [ObsoleteAttribute("This property has been deprecated.  Please use System.Diagnostics.Process.PrivateMemorySize64 instead.  https://go.microsoft.com/fwlink/?linkid=14202")]
        public int PrivateMemorySize
        {
            get
            {
                EnsureState(State.HaveProcessInfo);
                return unchecked((int)_processInfo.PrivateBytes);
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Gets
        ///       the friendly name of the process.
        ///    </para>
        /// </devdoc>
        public string ProcessName
        {
            get
            {
                EnsureState(State.HaveProcessInfo);
                return _processInfo.ProcessName;
            }
        }

        ///// <devdoc>
        /////    <para>
        /////       Gets
        /////       or sets which processors the threads in this process can be scheduled to run on.
        /////    </para>
        ///// </devdoc>
        //public IntPtr ProcessorAffinity
        //{
        //    get
        //    {
        //        if (!_haveProcessorAffinity)
        //        {
        //            _processorAffinity = ProcessorAffinityCore;
        //            _haveProcessorAffinity = true;
        //        }
        //        return _processorAffinity;
        //    }
        //    set
        //    {
        //        ProcessorAffinityCore = value;
        //        _processorAffinity = value;
        //        _haveProcessorAffinity = true;
        //    }
        //}

        public int SessionId
        {
            get
            {
                EnsureState(State.HaveProcessInfo);
                return _processInfo.SessionId;
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Gets or sets the properties to pass into the <see cref='System.Diagnostics.Process.Start(System.Diagnostics.ProcessStartInfo)'/> method for the <see cref='System.Diagnostics.Process'/>.
        ///    </para>
        /// </devdoc>
        public System.Diagnostics.ProcessStartInfo StartInfo
        {
            get
            {
                if (_startInfo == null)
                {
                    if (Associated)
                    {
                        throw new InvalidOperationException("SR.CantGetProcessStartInfo");
                    }

                    _startInfo = new System.Diagnostics.ProcessStartInfo();
                }
                return _startInfo;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (Associated)
                {
                    throw new InvalidOperationException("SR.CantSetProcessStartInfo");
                }

                _startInfo = value;
            }
        }

        ///// <devdoc>
        /////    <para>
        /////       Gets the set of threads that are running in the associated
        /////       process.
        /////    </para>
        ///// </devdoc>
        //public ProcessThreadCollection Threads
        //{
        //    get
        //    {
        //        if (_threads == null)
        //        {
        //            EnsureState(State.HaveProcessInfo);
        //            int count = _processInfo._threadInfoList.Count;
        //            ProcessThread[] newThreadsArray = new ProcessThread[count];
        //            for (int i = 0; i < count; i++)
        //            {
        //                newThreadsArray[i] = new ProcessThread(_isRemoteMachine, _processId, (ThreadInfo)_processInfo._threadInfoList[i]);
        //            }

        //            ProcessThreadCollection newThreads = new ProcessThreadCollection(newThreadsArray);
        //            _threads = newThreads;
        //        }
        //        return _threads;
        //    }
        //}

        public int HandleCount
        {
            get
            {
                EnsureState(State.HaveProcessInfo);
                EnsureHandleCountPopulated();
                return _processInfo.HandleCount;
            }
        }

        partial void EnsureHandleCountPopulated();

        public long VirtualMemorySize64
        {
            get
            {
                EnsureState(State.HaveProcessInfo);
                return _processInfo.VirtualBytes;
            }
        }

        [ObsoleteAttribute("This property has been deprecated.  Please use System.Diagnostics.Process.VirtualMemorySize64 instead.  https://go.microsoft.com/fwlink/?linkid=14202")]
        public int VirtualMemorySize
        {
            get
            {
                EnsureState(State.HaveProcessInfo);
                return unchecked((int)_processInfo.VirtualBytes);
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Gets or sets whether the <see cref='System.Diagnostics.Process.Exited'/>
        ///       event is fired
        ///       when the process terminates.
        ///    </para>
        /// </devdoc>
        public bool EnableRaisingEvents
        {
            get
            {
                return _watchForExit;
            }
            set
            {
                if (value != _watchForExit)
                {
                    if (Associated)
                    {
                        if (value)
                        {
                            EnsureWatchingForExit();
                        }
                        else
                        {
                            StopWatchingForExit();
                        }
                    }
                    _watchForExit = value;
                }
            }
        }


        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public StreamWriter StandardInput
        {
            get
            {
                if (_standardInput == null)
                {
                    throw new InvalidOperationException("SR.CantGetStandardIn");
                }

                _standardInputAccessed = true;
                return _standardInput;
            }
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public StreamReader StandardOutput
        {
            get
            {
                if (_standardOutput == null)
                {
                    throw new InvalidOperationException("SR.CantGetStandardOut");
                }

                if (_outputStreamReadMode == StreamReadMode.Undefined)
                {
                    _outputStreamReadMode = StreamReadMode.SyncMode;
                }
                else if (_outputStreamReadMode != StreamReadMode.SyncMode)
                {
                    throw new InvalidOperationException("SR.CantMixSyncAsyncOperation");
                }

                return _standardOutput;
            }
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public StreamReader StandardError
        {
            get
            {
                if (_standardError == null)
                {
                    throw new InvalidOperationException("SR.CantGetStandardError");
                }

                if (_errorStreamReadMode == StreamReadMode.Undefined)
                {
                    _errorStreamReadMode = StreamReadMode.SyncMode;
                }
                else if (_errorStreamReadMode != StreamReadMode.SyncMode)
                {
                    throw new InvalidOperationException("SR.CantMixSyncAsyncOperation");
                }

                return _standardError;
            }
        }

        public long WorkingSet64
        {
            get
            {
                EnsureState(State.HaveProcessInfo);
                return _processInfo.WorkingSet;
            }
        }

        [ObsoleteAttribute("This property has been deprecated.  Please use System.Diagnostics.Process.WorkingSet64 instead.  https://go.microsoft.com/fwlink/?linkid=14202")]
        public int WorkingSet
        {
            get
            {
                EnsureState(State.HaveProcessInfo);
                return unchecked((int)_processInfo.WorkingSet);
            }
        }

        public event EventHandler Exited
        {
            add
            {
                _onExited += value;
            }
            remove
            {
                _onExited -= value;
            }
        }

        /// <devdoc>
        ///     This is called from the threadpool when a process exits.
        /// </devdoc>
        /// <internalonly/>
        private void CompletionCallback(object waitHandleContext, bool wasSignaled)
        {
            System.Diagnostics.Debug.Assert(waitHandleContext != null, "Process.CompletionCallback called with no waitHandleContext");
            lock (this)
            {
                // Check the exited event that we get from the threadpool
                // matches the event we are waiting for.
                if (waitHandleContext != _waitHandle)
                {
                    return;
                }
                StopWatchingForExit();
                RaiseOnExited();
            }
        }

        /// <internalonly/>
        /// <devdoc>
        ///    <para>
        ///       Free any resources associated with this component.
        ///    </para>
        /// </devdoc>
        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    //Dispose managed and unmanaged resources
                    Close();
                }
                _disposed = true;
            }
        }

        public bool CloseMainWindow()
        {
            return CloseMainWindowCore();
        }

        public bool WaitForInputIdle()
        {
            return WaitForInputIdle(int.MaxValue);
        }

        public bool WaitForInputIdle(int milliseconds)
        {
            return WaitForInputIdleCore(milliseconds);
        }

        public ISynchronizeInvoke SynchronizingObject { get; set; }

        /// <devdoc>
        ///    <para>
        ///       Frees any resources associated with this component.
        ///    </para>
        /// </devdoc>
        public void Close()
        {
            if (Associated)
            {
                // We need to lock to ensure we don't run concurrently with CompletionCallback.
                // Without this lock we could reset _raisedOnExited which causes CompletionCallback to
                // raise the Exited event a second time for the same process.
                lock (this)
                {
                    // This sets _waitHandle to null which causes CompletionCallback to not emit events.
                    StopWatchingForExit();
                }

                if (_haveProcessHandle)
                {
                    _processHandle.Dispose();
                    _processHandle = null;
                    _haveProcessHandle = false;
                }
                _haveProcessId = false;
                _isRemoteMachine = false;
                _machineName = ".";
                _raisedOnExited = false;

                // Only call close on the streams if the user cannot have a reference on them.
                // If they are referenced it is the user's responsibility to dispose of them.
                try
                {
                    if (_standardOutput != null && (_outputStreamReadMode == StreamReadMode.AsyncMode || _outputStreamReadMode == StreamReadMode.Undefined))
                    {
                        if (_outputStreamReadMode == StreamReadMode.AsyncMode)
                        {
                            _output?.CancelOperation();
                            _output?.Dispose();
                        }
                        _standardOutput.Close();
                    }

                    if (_standardError != null && (_errorStreamReadMode == StreamReadMode.AsyncMode || _errorStreamReadMode == StreamReadMode.Undefined))
                    {
                        if (_errorStreamReadMode == StreamReadMode.AsyncMode)
                        {
                            _error?.CancelOperation();
                            _error?.Dispose();
                        }
                        _standardError.Close();
                    }

                    if (_standardInput != null && !_standardInputAccessed)
                    {
                        _standardInput.Close();
                    }
                }
                finally
                {
                    _standardOutput = null;
                    _standardInput = null;
                    _standardError = null;

                    _output = null;
                    _error = null;

                    CloseCore();
                    Refresh();
                }
            }
        }

        // Checks if the process hasn't exited on Unix systems.
        // This is used to detect recycled child PIDs.
        partial void ThrowIfExited(bool refresh);

        /// <devdoc>
        ///     Helper method for checking preconditions when accessing properties.
        /// </devdoc>
        /// <internalonly/>
        private void EnsureState(State state)
        {
            if ((state & State.Associated) != (State)0)
                if (!Associated)
                    throw new InvalidOperationException("SR.NoAssociatedProcess");

            if ((state & State.HaveId) != (State)0)
            {
                if (!_haveProcessId)
                {
                    ////if (_haveProcessHandle)
                    //{
                    //    //SetProcessId(ProcessManager.GetProcessIdFromHandle(_processHandle));
                    //}
                    //else
                    {
                        EnsureState(State.Associated);
                        throw new InvalidOperationException("SR.ProcessIdRequired");
                    }
                }
                if ((state & State.HaveNonExitedId) == State.HaveNonExitedId)
                {
                    ThrowIfExited(refresh: false);
                }
            }

            if ((state & State.IsLocal) != (State)0 && _isRemoteMachine)
            {
                throw new NotSupportedException("SR.NotSupportedRemote");
            }

            if ((state & State.HaveProcessInfo) != (State)0)
            {
                if (_processInfo == null)
                {
                    if ((state & State.HaveNonExitedId) != State.HaveNonExitedId)
                    {
                        EnsureState(State.HaveNonExitedId);
                    }
                    //_processInfo = ProcessManager.GetProcessInfo(_processId, _machineName);
                    if (_processInfo == null)
                    {
                        throw new InvalidOperationException("SR.NoProcessInfo");
                    }
                }
            }

            if ((state & State.Exited) != (State)0)
            {
                if (!HasExited)
                {
                    throw new InvalidOperationException("SR.WaitTillExit");
                }

                if (!_haveProcessHandle)
                {
                    throw new InvalidOperationException("SR.NoProcessHandle");
                }
            }
        }

        /// <devdoc>
        ///     Make sure we have obtained the min and max working set limits.
        /// </devdoc>
        /// <internalonly/>
        private void EnsureWorkingSetLimits()
        {
            if (!_haveWorkingSetLimits)
            {
                //GetWorkingSetLimits(out _minWorkingSet, out _maxWorkingSet);
                //_haveWorkingSetLimits = true;
            }
        }

        /// <devdoc>
        ///     Helper to set minimum or maximum working set limits.
        /// </devdoc>
        /// <internalonly/>
        private void SetWorkingSetLimits(IntPtr? min, IntPtr? max)
        {
            //SetWorkingSetLimitsCore(min, max, out _minWorkingSet, out _maxWorkingSet);
            //_haveWorkingSetLimits = true;
        }

        ///// <devdoc>
        /////    <para>
        /////       Returns a new <see cref='System.Diagnostics.Process'/> component given a process identifier and
        /////       the name of a computer in the network.
        /////    </para>
        ///// </devdoc>
        //public static Process GetProcessById(int processId, string machineName)
        //{
        //    if (!ProcessManager.IsProcessRunning(processId, machineName))
        //    {
        //        throw new ArgumentException(SR.Format(SR.MissingProccess, processId.ToString()));
        //    }

        //    return new Process(machineName, ProcessManager.IsRemoteMachine(machineName), processId, null);
        //}

        ///// <devdoc>
        /////    <para>
        /////       Returns a new <see cref='System.Diagnostics.Process'/> component given the
        /////       identifier of a process on the local computer.
        /////    </para>
        ///// </devdoc>
        //public static Process GetProcessById(int processId)
        //{
        //    return GetProcessById(processId, ".");
        //}

        ///// <devdoc>
        /////    <para>
        /////       Creates an array of <see cref='System.Diagnostics.Process'/> components that are
        /////       associated
        /////       with process resources on the
        /////       local computer. These process resources share the specified process name.
        /////    </para>
        ///// </devdoc>
        //public static Process[] GetProcessesByName(string processName)
        //{
        //    return GetProcessesByName(processName, ".");
        //}

        ///// <devdoc>
        /////    <para>
        /////       Creates a new <see cref='System.Diagnostics.Process'/>
        /////       component for each process resource on the local computer.
        /////    </para>
        ///// </devdoc>
        //public static Process[] GetProcesses()
        //{
        //    return GetProcesses(".");
        //}

        ///// <devdoc>
        /////    <para>
        /////       Creates a new <see cref='System.Diagnostics.Process'/>
        /////       component for each
        /////       process resource on the specified computer.
        /////    </para>
        ///// </devdoc>
        //public static Process[] GetProcesses(string machineName)
        //{
        //    bool isRemoteMachine = ProcessManager.IsRemoteMachine(machineName);
        //    ProcessInfo[] processInfos = ProcessManager.GetProcessInfos(machineName);
        //    Process[] processes = new Process[processInfos.Length];
        //    for (int i = 0; i < processInfos.Length; i++)
        //    {
        //        ProcessInfo processInfo = processInfos[i];
        //        processes[i] = new Process(machineName, isRemoteMachine, processInfo.ProcessId, processInfo);
        //    }
        //    return processes;
        //}

        /// <devdoc>
        ///    <para>
        ///       Returns a new <see cref='System.Diagnostics.Process'/>
        ///       component and associates it with the current active process.
        ///    </para>
        /// </devdoc>
        public static Process GetCurrentProcess()
        {
            return new Process(".", false, GetCurrentProcessId(), null);
        }

        /// <devdoc>
        ///    <para>
        ///       Raises the <see cref='System.Diagnostics.Process.Exited'/> event.
        ///    </para>
        /// </devdoc>
        protected void OnExited()
        {
            EventHandler exited = _onExited;
            if (exited != null)
            {
                exited(this, EventArgs.Empty);
            }
        }

        /// <devdoc>
        ///     Raise the Exited event, but make sure we don't do it more than once.
        /// </devdoc>
        /// <internalonly/>
        private void RaiseOnExited()
        {
            if (!_raisedOnExited)
            {
                lock (this)
                {
                    if (!_raisedOnExited)
                    {
                        _raisedOnExited = true;
                        OnExited();
                    }
                }
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Discards any information about the associated process
        ///       that has been cached inside the process component. After <see cref='System.Diagnostics.Process.Refresh'/> is called, the
        ///       first request for information for each property causes the process component
        ///       to obtain a new value from the associated process.
        ///    </para>
        /// </devdoc>
        public void Refresh()
        {
            _processInfo = null;
            //_threads = null;
            //_modules = null;
            _exited = false;
            _haveWorkingSetLimits = false;
            _haveProcessorAffinity = false;
            _havePriorityClass = false;
            _haveExitTime = false;
            _havePriorityBoostEnabled = false;
            RefreshCore();
        }

        /// <summary>
        /// Opens a long-term handle to the process, with all access.  If a handle exists,
        /// then it is reused.  If the process has exited, it throws an exception.
        /// </summary>
        private SafeProcessHandle GetOrOpenProcessHandle()
        {
            if (!_haveProcessHandle)
            {
                //Cannot open a new process handle if the object has been disposed, since finalization has been suppressed.            
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }

                SetProcessHandle(GetProcessHandle());
            }
            return _processHandle;
        }

        /// <devdoc>
        ///     Helper to associate a process handle with this component.
        /// </devdoc>
        /// <internalonly/>
        private void SetProcessHandle(SafeProcessHandle processHandle)
        {
            _processHandle = processHandle;
            _haveProcessHandle = true;
            if (_watchForExit)
            {
                EnsureWatchingForExit();
            }
        }

        /// <devdoc>
        ///     Helper to associate a process id with this component.
        /// </devdoc>
        /// <internalonly/>
        private void SetProcessId(int processId)
        {
            _processId = processId;
            _haveProcessId = true;
            ConfigureAfterProcessIdSet();
        }

        /// <summary>Additional optional configuration hook after a process ID is set.</summary>
        partial void ConfigureAfterProcessIdSet();

        /// <devdoc>
        ///    <para>
        ///       Starts a process specified by the <see cref='System.Diagnostics.Process.StartInfo'/> property of this <see cref='System.Diagnostics.Process'/>
        ///       component and associates it with the
        ///    <see cref='System.Diagnostics.Process'/> . If a process resource is reused 
        ///       rather than started, the reused process is associated with this <see cref='System.Diagnostics.Process'/>
        ///       component.
        ///    </para>
        /// </devdoc>
        public bool Start()
        {
            Close();

            System.Diagnostics.ProcessStartInfo startInfo = StartInfo;
            if (startInfo.FileName.Length == 0)
            {
                throw new InvalidOperationException("SR.FileNameMissing");
            }
            if (startInfo.StandardInputEncoding != null && !startInfo.RedirectStandardInput)
            {
                throw new InvalidOperationException("SR.StandardInputEncodingNotAllowed");
            }
            if (startInfo.StandardOutputEncoding != null && !startInfo.RedirectStandardOutput)
            {
                throw new InvalidOperationException("SR.StandardOutputEncodingNotAllowed");
            }
            if (startInfo.StandardErrorEncoding != null && !startInfo.RedirectStandardError)
            {
                throw new InvalidOperationException("SR.StandardErrorEncodingNotAllowed");
            }
            if (!string.IsNullOrEmpty(startInfo.Arguments) && startInfo.ArgumentList.Count > 0)
            {
                throw new InvalidOperationException("SR.ArgumentAndArgumentListInitialized");
            }

            //Cannot start a new process and store its handle if the object has been disposed, since finalization has been suppressed.            
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            //SerializationGuard.ThrowIfDeserializationInProgress("AllowProcessCreation", ref s_cachedSerializationSwitch);

            return StartCore(startInfo);
        }

        /// <devdoc>
        ///    <para>
        ///       Starts a process resource by specifying the name of a
        ///       document or application file. Associates the process resource with a new <see cref='System.Diagnostics.Process'/>
        ///       component.
        ///    </para>
        /// </devdoc>
        public static Process Start(string fileName)
        {
            return Start(new System.Diagnostics.ProcessStartInfo(fileName));
        }

        /// <devdoc>
        ///    <para>
        ///       Starts a process resource by specifying the name of an
        ///       application and a set of command line arguments. Associates the process resource
        ///       with a new <see cref='System.Diagnostics.Process'/>
        ///       component.
        ///    </para>
        /// </devdoc>
        public static Process Start(string fileName, string arguments)
        {
            return Start(new System.Diagnostics.ProcessStartInfo(fileName, arguments));
        }

        /// <devdoc>
        ///    <para>
        ///       Starts a process resource specified by the process start
        ///       information passed in, for example the file name of the process to start.
        ///       Associates the process resource with a new <see cref='System.Diagnostics.Process'/>
        ///       component.
        ///    </para>
        /// </devdoc>
        public static Process Start(System.Diagnostics.ProcessStartInfo startInfo)
        {
            Process process = new Process();
            if (startInfo == null)
                throw new ArgumentNullException(nameof(startInfo));

            process.StartInfo = startInfo;
            return process.Start() ?
                process :
                null;
        }

        /// <devdoc>
        ///     Make sure we are not watching for process exit.
        /// </devdoc>
        /// <internalonly/>
        private void StopWatchingForExit()
        {
            if (_watchingForExit)
            {
                RegisteredWaitHandle rwh = null;
                WaitHandle wh = null;

                lock (this)
                {
                    if (_watchingForExit)
                    {
                        _watchingForExit = false;

                        wh = _waitHandle;
                        _waitHandle = null;

                        rwh = _registeredWaitHandle;
                        _registeredWaitHandle = null;
                    }
                }

                if (rwh != null)
                {
                    rwh.Unregister(null);
                }

                if (wh != null)
                {
                    wh.Dispose();
                }
            }
        }

        public override string ToString()
        {
            if (Associated)
            {
                string processName = ProcessName;
                if (processName.Length != 0)
                {
                    return string.Format(CultureInfo.CurrentCulture, "{0} ({1})", base.ToString(), processName);
                }
            }
            return base.ToString();
        }

        /// <devdoc>
        ///    <para>
        ///       Instructs the <see cref='System.Diagnostics.Process'/> component to wait
        ///       indefinitely for the associated process to exit.
        ///    </para>
        /// </devdoc>
        public void WaitForExit()
        {
            WaitForExit(Timeout.Infinite);
        }

        /// <summary>
        /// Instructs the Process component to wait the specified number of milliseconds for 
        /// the associated process to exit.
        /// </summary>
        public bool WaitForExit(int milliseconds)
        {
            bool exited = WaitForExitCore(milliseconds);
            if (exited && _watchForExit)
            {
                RaiseOnExited();
            }
            return exited;
        }

        /// <devdoc>
        /// <para>
        /// Instructs the <see cref='System.Diagnostics.Process'/> component to start
        /// reading the StandardOutput stream asynchronously. The user can register a callback
        /// that will be called when a line of data terminated by \n,\r or \r\n is reached, or the end of stream is reached
        /// then the remaining information is returned. The user can add an event handler to OutputDataReceived.
        /// </para>
        /// </devdoc>
        public void BeginOutputReadLine()
        {
            if (_outputStreamReadMode == StreamReadMode.Undefined)
            {
                _outputStreamReadMode = StreamReadMode.AsyncMode;
            }
            else if (_outputStreamReadMode != StreamReadMode.AsyncMode)
            {
                throw new InvalidOperationException("SR.CantMixSyncAsyncOperation");
            }

            if (_pendingOutputRead)
                throw new InvalidOperationException("SR.PendingAsyncOperation");

            _pendingOutputRead = true;
            // We can't detect if there's a pending synchronous read, stream also doesn't.
            if (_output == null)
            {
                if (_standardOutput == null)
                {
                    throw new InvalidOperationException("SR.CantGetStandardOut");
                }

                Stream s = _standardOutput.BaseStream;
                _output = new AsyncStreamReader(s, OutputReadNotifyUser, _standardOutput.CurrentEncoding);
            }
            _output.BeginReadLine();
        }


        /// <devdoc>
        /// <para>
        /// Instructs the <see cref='System.Diagnostics.Process'/> component to start
        /// reading the StandardError stream asynchronously. The user can register a callback
        /// that will be called when a line of data terminated by \n,\r or \r\n is reached, or the end of stream is reached
        /// then the remaining information is returned. The user can add an event handler to ErrorDataReceived.
        /// </para>
        /// </devdoc>
        public void BeginErrorReadLine()
        {
            if (_errorStreamReadMode == StreamReadMode.Undefined)
            {
                _errorStreamReadMode = StreamReadMode.AsyncMode;
            }
            else if (_errorStreamReadMode != StreamReadMode.AsyncMode)
            {
                throw new InvalidOperationException("SR.CantMixSyncAsyncOperation");
            }

            if (_pendingErrorRead)
            {
                throw new InvalidOperationException("SR.PendingAsyncOperation");
            }

            _pendingErrorRead = true;
            // We can't detect if there's a pending synchronous read, stream also doesn't.
            if (_error == null)
            {
                if (_standardError == null)
                {
                    throw new InvalidOperationException("SR.CantGetStandardError");
                }

                Stream s = _standardError.BaseStream;
                _error = new AsyncStreamReader(s, ErrorReadNotifyUser, _standardError.CurrentEncoding);
            }
            _error.BeginReadLine();
        }

        /// <devdoc>
        /// <para>
        /// Instructs the <see cref='System.Diagnostics.Process'/> component to cancel the asynchronous operation
        /// specified by BeginOutputReadLine().
        /// </para>
        /// </devdoc>
        public void CancelOutputRead()
        {
            if (_output != null)
            {
                _output.CancelOperation();
            }
            else
            {
                throw new InvalidOperationException("SR.NoAsyncOperation");
            }

            _pendingOutputRead = false;
        }

        /// <devdoc>
        /// <para>
        /// Instructs the <see cref='System.Diagnostics.Process'/> component to cancel the asynchronous operation
        /// specified by BeginErrorReadLine().
        /// </para>
        /// </devdoc>
        public void CancelErrorRead()
        {
            if (_error != null)
            {
                _error.CancelOperation();
            }
            else
            {
                throw new InvalidOperationException("SR.NoAsyncOperation");
            }

            _pendingErrorRead = false;
        }

        internal void OutputReadNotifyUser(string data)
        {
            // To avoid race between remove handler and raising the event
            DataReceivedEventHandler outputDataReceived = OutputDataReceived;
            if (outputDataReceived != null)
            {
                DataReceivedEventArgs e = new DataReceivedEventArgs(data);
                outputDataReceived(this, e);  // Call back to user informing data is available
            }
        }

        internal void ErrorReadNotifyUser(string data)
        {
            // To avoid race between remove handler and raising the event
            DataReceivedEventHandler errorDataReceived = ErrorDataReceived;
            if (errorDataReceived != null)
            {
                DataReceivedEventArgs e = new DataReceivedEventArgs(data);
                errorDataReceived(this, e); // Call back to user informing data is available.
            }
        }

        private static void AppendArguments(StringBuilder stringBuilder, Collection<string> argumentList)
        {
            if (argumentList.Count > 0)
            {
                foreach (string argument in argumentList)
                {
                    PasteArguments.AppendArgument(stringBuilder, argument);
                }
            }
        }

        /// <summary>
        /// This enum defines the operation mode for redirected process stream.
        /// We don't support switching between synchronous mode and asynchronous mode.
        /// </summary>
        private enum StreamReadMode
        {
            Undefined,
            SyncMode,
            AsyncMode
        }

        /// <summary>A desired internal state.</summary>
        private enum State
        {
            HaveId = 0x1,
            IsLocal = 0x2,
            HaveNonExitedId = HaveId | 0x4,
            HaveProcessInfo = 0x8,
            Exited = 0x10,
            Associated = 0x20,
        }
    }
}
