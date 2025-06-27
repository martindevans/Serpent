using Wasmtime;
using Wazzy.Async;
using Wazzy.Async.Extensions;
using Wazzy.WasiSnapshotPreview1.FileSystem.Implementations.VirtualFileSystem;
using Wazzy.WasiSnapshotPreview1.Process;

namespace Serpent;

public sealed class Python
    : IDisposable
{
    private readonly Store _store;
    private readonly Instance _instance;
    private readonly VirtualFileSystem _fs;
    private readonly Action _start;
    private readonly Memory _memory;

    private SavedStack? _stack;

    /// <summary>
    /// Check if this runtime is capable of being async suspended
    /// </summary>
    public bool IsAsync { get; }

    /// <summary>
    /// Check if this runtime is currently suspended in an async operation
    /// </summary>
    public bool IsSuspended { get; private set; }

    /// <summary>
    /// Check if this runtime is completed. A completed runtime cannot be used again and should be disposed.
    /// </summary>
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// Get the reason for the last suspend (if currently suspended)
    /// </summary>
    public IAsyncifySuspendReason? SuspendedReason { get; private set; }

    /// <summary>
    /// Get or set the amount of fuel in this runtime
    /// </summary>
    public ulong Fuel
    {
        get => _store.Fuel;
        set => _store.Fuel = value;
    }

    /// <summary>
    /// Get the current memory usage of this runtime
    /// </summary>
    public long MemoryBytes => _memory.GetLength();

    internal Python(Store store, Instance instance, VirtualFileSystem fs)
    {
        _store = store;
        _instance = instance;
        _fs = fs;
        _memory = _instance.GetMemory("memory")!;

        _start = _instance.GetFunction("_start")?.WrapAction()
             ?? throw new InvalidOperationException("WASM instance is missing required function `_start`");

        IsAsync = instance.IsAsyncCapable();
        IsSuspended = false;
    }

    public void Dispose()
    {
        _store.Dispose();
        _fs.Dispose();
    }

    public int? Execute()
    {
        if (IsCompleted)
            throw new InvalidOperationException("Cannot Execute() a completed Python runtime");

        // Begin resuming from a previous async suspend
        if (_stack.HasValue)
            _instance.StartRewind(_stack.Value);
        SuspendedReason = null;

        // This either makes the call, or does the resume
        try
        {
            _start.Invoke();
        }
        catch (WasmtimeException e) when (e.InnerException is ThrowExitProcessException t)
        {
            IsCompleted = true;
            IsSuspended = false;
            return t.ExitCode;
        }

        // Check if we've suspended
        IsSuspended = _instance.GetAsyncState() == AsyncState.Suspending;
        if (IsSuspended)
        {
            _stack = default;
            _stack = _instance.StopUnwind();

            SuspendedReason = _stack.Value.SuspendReason;

            return null;
        }

        IsCompleted = true;
        return 0;
    }
}