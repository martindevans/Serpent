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

    public bool IsAsync { get; }
    public bool IsSuspended { get; private set; }

    public ulong Fuel
    {
        get => _store.Fuel;
        set => _store.Fuel = value;
    }

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
        // Begin resuming from a previous async suspend
        if (_stack.HasValue)
            _instance.StartRewind(_stack.Value);

        // This either makes the call, or does the resume
        try
        {
            _start.Invoke();
        }
        catch (WasmtimeException e) when (e.InnerException is ThrowExitProcessException t)
        {
            IsSuspended = false;
            return t.ExitCode;
        }

        // Check if we've suspended
        if (_instance.GetAsyncState() == AsyncState.Suspending)
        {
            IsSuspended = true;

            _stack = default;
            _stack = _instance.StopUnwind();
            return null;
        }

        return 0;
    }
}