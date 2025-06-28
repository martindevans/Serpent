using Serpent.Loading;
using System.Reflection;
using System.Security.Cryptography;
using Wasmtime;
using Module = Wasmtime.Module;

namespace Serpent.Tests;

public class OldPythonModuleLoader
    : IPythonModuleLoader
{
    // Must be older than the currently used one.
	// https://github.com/martindevans/Serpent/blob/99a76c95efe6a5e49c16258f31b0048a8c0a937f/Serpent/python3.13_async.wasm
	private const string EmbeddedWasmResourcePath = "Serpent.Tests.python3.13_async_99a76c95efe6a5e49c16258f31b0048a8c0a937f.wasm";
	private static readonly byte[] EmbeddedResourceMd5Hash = MD5.HashData(GetResourceStream());

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
        return EmbeddedResourceMd5Hash.AsSpan();
    }
}
