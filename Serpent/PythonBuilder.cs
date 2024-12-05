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
using Wazzy.WasiSnapshotPreview1.Socket;
using Module = Wasmtime.Module;

namespace Serpent;

public sealed class PythonBuilder
    : IDisposable
{
    private readonly Module _module;
    private readonly Engine _engine;

    private PythonBuilder(Module module, Engine engine)
    {
        _module = module;
        _engine = engine;
    }

    public static PythonBuilder Load(Engine engine)
    {
        const string path = "Serpent.python3.11_async.wasm";
        var module = Module.FromStream(engine, path, System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(path)!);
        return new PythonBuilder(module, engine);
    }

    public InnerBuilder Create()
    {
        return new InnerBuilder(_engine, _module);
    }

    public void Dispose()
    {
        _module.Dispose();
    }

    public sealed class InnerBuilder
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
        private Func<IWasiSocket> _socket;
        private ulong _fuel;
        private int _memorySize;
        private string? _pythonCachePath;

        private ReadOnlyMemory<byte>? _pythonCode;
        private string _mainFilePath;

        internal InnerBuilder(Engine engine, Module module)
        {
            _engine = engine;
            _module = module;

            _clock = () => new RealtimeClock();
            _random = () => new CryptoRandomSource();
            _stdin = () => new ZeroFile();
            _stdout = () => new ZeroFile();
            _stderr = () => new ZeroFile();
            _env = () => new Dictionary<string, string>();
            _pythonCachePath = default;
            _fs = _ => { };
            _socket = () => new NonFunctionalSocket();
            _fuel = 10_000_000_000;
            _memorySize = 100_000_000;

            _pythonCode = default;
            _mainFilePath = "main.py";
        }

        public InnerBuilder WithFuel(ulong amount)
        {
            _fuel = amount;
            return this;
        }

        public InnerBuilder WithMemoryLimit(int bytes)
        {
            _memorySize = bytes;
            return this;
        }

        public InnerBuilder WithClock<TClock>(Func<IWasiClock> clock)
            where TClock : class, IWasiClock, IVFSClock
        {
            _clock = clock;
            return this;
        }

        public InnerBuilder WithRandomSource(Func<IWasiRandomSource> random)
        {
            _random = random;
            return this;
        }

        public InnerBuilder WithStdIn(Func<IFile> stdin)
        {
            _stdin = stdin;
            return this;
        }

        public InnerBuilder WithStdOut(Func<IFile> stdout)
        {
            _stdout = stdout;
            return this;
        }

        public InnerBuilder WithStdErr(Func<IFile> stderr)
        {
            _stderr = stderr;
            return this;
        }

        public InnerBuilder WithEnv(Func<IReadOnlyDictionary<string, string>> env)
        {
            _env = env;
            return this;
        }

        public InnerBuilder WithFilesystem(Action<DirectoryBuilder> fs)
        {
            _fs = fs;
            return this;
        }

        public InnerBuilder WithSocket(Func<IWasiSocket> socket)
        {
            _socket = socket;
            return this;
        }

        /// <summary>
        /// Enable caching of .pyc files in the given folder. This should be mapped to a directory that is persistent between runs.
        /// See <see href="https://docs.python.org/3/using/cmdline.html#envvar-PYTHONPYCACHEPREFIX">PYTHONPYCACHEPREFIX</see>
        /// </summary>
        public InnerBuilder WithPythonCachePath(string vfsPath)
        {
            _pythonCachePath = vfsPath;
            return this;
        }

        /// <summary>
        /// Writes and sets the main file.
        /// Defaults to `/main.py`, you may use <see cref="WithMainFilePath"/> to change this.
        /// </summary>
        public InnerBuilder WithCode(ReadOnlyMemory<byte> pythonCode)
        {
            _pythonCode = pythonCode;
            return this;
        }

        /// <summary>
        /// Sets which file is run from the virtual file system.
        /// <see cref="WithCode"/> can be used to set the file contents.
        /// </summary>
        public InnerBuilder WithMainFilePath(string vfsPath)
        {
            _mainFilePath = vfsPath;
            return this;
        }

        public Python Build()
        {
            // Setup environment variables
            var env = new Dictionary<string, string>(_env());
            env.TryAdd("PYTHONHOME", "/opt/wasi-python/lib/python3.11");
            env.TryAdd("PYTHONPATH", "/opt/wasi-python/lib/python3.11");
            env.TryAdd("PYTHONUNBUFFERED", "1");
            if (_pythonCachePath != null)
                env.TryAdd("PYTHONPYCACHEPREFIX", _pythonCachePath);
            else
                env.TryAdd("PYTHONDONTWRITEBYTECODE", "1");

            // Tell it to run the python file.
            // argv[0] is the 'command used' which doesn't apply here, so we use a reasonable default.
            // argv[1] is the file path to run, omitting this would enter interactive mode and use stdin.
            var environment = new BasicEnvironment(env, ["python", _mainFilePath]);

            // Build virtual filesystem
            var builder = new VirtualFileSystemBuilder();
            builder.WithPipes(_stdin(), _stdout(), _stderr());
            builder.WithClock((IVFSClock)_clock());
            builder.WithVirtualRoot(dir =>
            {
                var archive = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Serpent.opt.zip")!;
                dir.MapReadonlyZipArchiveDirectory("opt", archive);

                if (_pythonCode != null)
                    dir.CreateInMemoryFile(_mainFilePath, _pythonCode, isReadOnly: true);

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
            linker.DefineFeature(_socket());

            // Set sensible limits on the store
            var store = new Store(_engine)
            {
                Fuel = _fuel,
            };
            store.SetLimits(_memorySize, 20_000, 1, 1, 1);

            return new Python(store, linker.Instantiate(store, _module), fs);
        }
    }
}