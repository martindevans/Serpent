using System.Reflection;
using System.Security.Cryptography;
using Wasmtime;
using Module = Wasmtime.Module;

namespace Serpent.Loading;

/// <summary>
/// Loads the embedded python wasm binary.
/// </summary>
public sealed class DefaultPythonModuleLoader
    : IPythonModuleLoader
{
    private const string EmbeddedWasmResourcePath = "Serpent.python3.13_async.wasm";
	private static readonly ReadOnlyMemory<byte> EmbeddedResourceMd5Hash = MD5.HashData(GetResourceStream());

	private static Stream GetResourceStream()
    {
        return Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedWasmResourcePath)!;
    }

    public Module LoadModule(Engine engine)
    {
        return Module.FromStream(engine, EmbeddedWasmResourcePath, GetResourceStream());
    }

    public ReadOnlySpan<byte> GetHash()
    {
        return EmbeddedResourceMd5Hash.Span;
    }
}
