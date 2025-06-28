using Wasmtime;

namespace Serpent.Loading;

/// <summary>
/// Cache loaded module to disk. This compiled module is almost instantaneous to load
/// but it is <b>not portable</b>; do not move it between machines!
/// </summary>
public sealed class FileCache
    : IPythonModuleLoader
{
    private readonly string _cachePath;
    private readonly IPythonModuleLoader _upstream;

    public FileCache(string cachePath, IPythonModuleLoader upstream)
    {
        _cachePath = cachePath;
        _upstream = upstream;
    }

    public Module LoadModule(Engine engine)
    {
        var module = TryLoadCache(engine);
        if (module != null)
            return module;

        module = _upstream.LoadModule(engine);
        SaveCache(module);

        return module;
    }

    public ReadOnlySpan<byte> GetHash()
    {
        return _upstream.GetHash();
    }

    private Module? TryLoadCache(Engine engine)
    {
        if (string.IsNullOrEmpty(_cachePath) || !File.Exists(_cachePath))
            return null;

        try
        {
            using var stream = File.OpenRead(_cachePath);

            var expectedHash = _upstream.GetHash();

            // Read the hash from the start of the file
            Span<byte> fileHash = stackalloc byte[expectedHash.Length];
            stream.ReadExactly(fileHash);

            // Check if the hashes match
            if (!fileHash.SequenceEqual(expectedHash))
                return null;

            // Read the rest of the file contents and deserialize into module
            var contents = new byte[stream.Length - stream.Position];
            stream.ReadExactly(contents);
            return Module.Deserialize(engine, "python", contents);
        }
        catch
        {
            // Failed to load the cached module for some reason, delete it.
            TryDelete(_cachePath);
        }

        return null;
    }

    private void SaveCache(Module module)
    {
        try
        {
            using var stream = File.OpenWrite(_cachePath);
            stream.Write(_upstream.GetHash());
            stream.Write(module.Serialize());
        }
        catch
        {
            // Something went wrong saving the cache. Delete it just for safety.
            TryDelete(_cachePath);
        }
    }

    private static void TryDelete(string path)
    {
        if (!File.Exists(path))
            return;

        try
        {
            File.Delete(path);
        }
        catch
        {
            // We tried our best to delete it
        }
    }
}