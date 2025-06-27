using System.Reflection;
using System.Security.Cryptography;
using Wasmtime;
using Module = Wasmtime.Module;

namespace Serpent;

public sealed class DefaultPythonModuleLoader(string? cachePath = null) : FileCachedPythonModuleLoader(cachePath)
{
	private const string EmbeddedWasmResourcePath = "Serpent.python3.13_async.wasm";
	private static readonly byte[] EmbeddedResourceMd5Hash = MD5.HashData(GetResourceStream());

	private static Stream GetResourceStream()
		=> Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedWasmResourcePath)!;

	protected override string ModuleName => EmbeddedWasmResourcePath;
	protected override Stream GetStream() => GetResourceStream();
	protected override byte[] Hash => EmbeddedResourceMd5Hash;
}
