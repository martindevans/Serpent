using Wasmtime;
using Wazzy.Extensions;
using Wazzy.WasiSnapshotPreview1.Clock;
using Wazzy.WasiSnapshotPreview1.Environment;
using Wazzy.WasiSnapshotPreview1.FileSystem.Implementations.VirtualFileSystem;
using Wazzy.WasiSnapshotPreview1.FileSystem.Implementations.VirtualFileSystem.Builder;
using Wazzy.WasiSnapshotPreview1.FileSystem.Implementations.VirtualFileSystem.Files;
using Wazzy.WasiSnapshotPreview1.Poll;
using Wazzy.WasiSnapshotPreview1.Process;
using Wazzy.WasiSnapshotPreview1.Random;

namespace Serpent;

public class PythonBuilder
{
    private readonly Engine _engine;
    private readonly Module _module;

    private Func<IWasiClock> _clock;
    private Func<IWasiRandomSource> _random;
    private Func<IFile> _stdin;
    private Func<IFile> _stdout;
    private Func<IFile> _stderr;
    private Func<IReadOnlyDictionary<string, string>> _env;
    private Action<DirectoryBuilder> _fs;
    private ulong _fuel;
    private int _memorySize;

    public bool Async { get; }

    public PythonBuilder(Engine engine, bool async = true)
    {
        _engine = engine;
        Async = async;

        var path = async ? "Serpent.python3.11_async.wasm" : "Serpent.python3.11.wasm";
        _module = Module.FromStream(_engine, path, System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(path)!);

        _clock = () => new RealtimeClock();
        _random = () => new CryptoRandomSource();
        _stdin = () => new ZeroFile();
        _stdout = () => new ZeroFile();
        _stderr = () => new ZeroFile();
        _env = () => new Dictionary<string, string>();
        _fs = _ => { };
        _fuel = 10_000_000_000;
        _memorySize = 100_000_000;
    }

    public PythonBuilder WithFuel(ulong amount)
    {
        _fuel = amount;
        return this;
    }

    public PythonBuilder WithMemoryLimit(int bytes)
    {
        _memorySize = bytes;
        return this;
    }

    public PythonBuilder WithClock<TClock>(Func<IWasiClock> clock)
        where TClock : class, IWasiClock, IVFSClock
    {
        _clock = clock;
        return this;
    }

    public PythonBuilder WithRandomSource(Func<IWasiRandomSource> random)
    {
        _random = random;
        return this;
    }

    public PythonBuilder WithStdIn(Func<IFile> stdin)
    {
        _stdin = stdin;
        return this;
    }

    public PythonBuilder WithStdOut(Func<IFile> stdout)
    {
        _stdout = stdout;
        return this;
    }

    public PythonBuilder WithStdErr(Func<IFile> stderr)
    {
        _stderr = stderr;
        return this;
    }

    public PythonBuilder WithEnv(Func<IReadOnlyDictionary<string, string>> env)
    {
        _env = env;
        return this;
    }

    public PythonBuilder WithFilesystem(Action<DirectoryBuilder> fs)
    {
        _fs = fs;
        return this;
    }

    public Python Build(ReadOnlyMemory<byte> python)
    {
        return Build((ReadOnlyMemory<byte>?)python);
    }

    public Python BuildInteractive()
    {
        return Build(default);
    }

    private Python Build(ReadOnlyMemory<byte>? python)
    {
        // Setup environment variables
        var env = new Dictionary<string, string>(_env());
        env.TryAdd("PYTHONHOME", "/opt/wasi-python/lib/python3.11");
        env.TryAdd("PYTHONPATH", "/opt/wasi-python/lib/python3.11");
        env.TryAdd("PYTHONDONTWRITEBYTECODE", "1");
        env.TryAdd("PYTHONUNBUFFERED", "1");

        // Specify main.py if some code was supplied, otherwise start in interactive mode
        var environment = python.HasValue
                        ? new BasicEnvironment(env, [ "python", "main.py" ])
                        : new BasicEnvironment(env, [ "python" ]);

        // Build virtual filesystem
        var builder = new VirtualFileSystemBuilder();
        builder.WithPipes(_stdin(), _stdout(), _stderr());
        builder.WithClock((IVFSClock)_clock());
        builder.WithVirtualRoot(dir =>
        {
            var archive = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Serpent.opt.zip")!;
            dir.MapReadonlyZipArchiveDirectory("opt", archive);

            dir.CreateInMemoryFile("main.py", python, isReadOnly: true);

            _fs(dir);
        });
        var fs = builder.Build();

        // Instantiate module
        var clock = _clock();
        var linker = new Linker(_engine);
        linker.DefineFeature(clock);
        linker.DefineFeature(_random());
        linker.DefineFeature(environment);
        linker.DefineFeature(new ThrowExitProcess());
        linker.DefineFeature(new AsyncifyPoll(clock));
        linker.DefineFeature(new AsyncifyYieldProcess());
        linker.DefineFeature(fs);

        // Set sensible limits on the store
        var store = new Store(_engine)
        {
            Fuel = _fuel,
        };
        store.SetLimits(_memorySize, 20_000, 1, 1, 1);

        return new Python(store, linker.Instantiate(store, _module), fs);
    }
}