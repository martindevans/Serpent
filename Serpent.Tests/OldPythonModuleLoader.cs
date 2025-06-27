using System.Reflection;
using System.Security.Cryptography;
using Wasmtime;

namespace Serpent.Tests;

public class OldPythonModuleLoader(string? cachePath = null) : FileCachedPythonModuleLoader(cachePath)
{
	// Must be older than the currently used one.
	// https://github.com/martindevans/Serpent/blob/99a76c95efe6a5e49c16258f31b0048a8c0a937f/Serpent/python3.13_async.wasm
	private const string EmbeddedWasmResourcePath = "Serpent.Tests.python3.13_async_99a76c95efe6a5e49c16258f31b0048a8c0a937f.wasm";
	private static readonly byte[] EmbeddedResourceMd5Hash = MD5.HashData(GetResourceStream());

	private static Stream GetResourceStream()
		=> Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedWasmResourcePath)!;

	protected override string ModuleName => EmbeddedWasmResourcePath;
	protected override Stream GetStream() => GetResourceStream();
	protected override byte[] Hash => EmbeddedResourceMd5Hash;
}
