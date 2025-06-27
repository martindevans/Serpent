using System.Reflection;
using Wasmtime;
using Module = Wasmtime.Module;

namespace Serpent;

public class DefaultPythonModuleLoader(string? cachePath = null) : IPythonModuleLoader
{
	public const string EmbeddedWasmResourcePath = "Serpent.python3.13_async.wasm";

	public Module LoadModule(Engine engine)
	{
		var module = LoadCache(engine, cachePath);
		if (module == null)
		{
			module = Module.FromStream(engine, EmbeddedWasmResourcePath, Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedWasmResourcePath)!);
			SaveCache(module, cachePath);
		}
		return module;
	}

	private static void TryDelete(string path)
	{
		if (File.Exists(path))
		{
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

	private static Module? LoadCache(Engine engine, string? path)
	{
		if (string.IsNullOrEmpty(path) || !File.Exists(path))
			return null;

		try
		{
			return Module.DeserializeFile(engine, "python", path);
		}
		catch
		{
			// Failed to load the cached module for some reason, delete it.
			TryDelete(path);
		}

		return null;
	}

	private static void SaveCache(Module module, string? path)
	{
		if (string.IsNullOrEmpty(path))
			return;

		try
		{
			File.WriteAllBytes(path, module.Serialize());
		}
		catch
		{
			// Something went wrong saving the cache.
		}
	}
}
