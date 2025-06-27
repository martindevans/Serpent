using System.Reflection;
using System.Security.Cryptography;
using Wasmtime;
using Module = Wasmtime.Module;

namespace Serpent;

/// <summary>
/// Loads the provided python 3.13 wasm binary.
/// </summary>
/// <param name="cachePath">Path to save and load the cached module from. If null, disables caching.</param>
public sealed class DefaultPythonModuleLoader(string? cachePath = null) : FileCachedPythonModuleLoader(cachePath)
{
	private const string EmbeddedWasmResourcePath = "Serpent.python3.13_async.wasm";
	private static readonly byte[] EmbeddedResourceMd5Hash = MD5.HashData(GetResourceStream());

	private static Stream GetResourceStream()
		=> Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedWasmResourcePath)!;

	protected override Module LoadModuleForCache(Engine engine)
		=> Module.FromStream(engine, EmbeddedWasmResourcePath, GetResourceStream());

	protected override byte[] Hash => EmbeddedResourceMd5Hash;
}
