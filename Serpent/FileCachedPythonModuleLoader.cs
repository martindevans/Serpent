using System.Security.Cryptography;
using Wasmtime;

namespace Serpent;

public abstract class FileCachedPythonModuleLoader(string? cachePath = null) : IPythonModuleLoader
{
	protected abstract string ModuleName { get; }
	protected abstract Stream GetStream();

	protected abstract byte[] Hash { get; }

	public Module LoadModule(Engine engine)
	{
		var module = LoadCache(engine);
		if (module != null) return module;

		module = Module.FromStream(engine, ModuleName, GetStream());
		SaveCache(module);
		return module;
	}

	private Module? LoadCache(Engine engine)
	{
		if (string.IsNullOrEmpty(cachePath) || !File.Exists(cachePath))
			return null;

		try {
			using var stream = File.OpenRead(cachePath);
			var md5Hash = new byte[MD5.HashSizeInBytes];
			stream.ReadExactly(md5Hash);
			if (!md5Hash.SequenceEqual(Hash))
				return null;
			var contents = new byte[stream.Length - stream.Position];
			stream.ReadExactly(contents);
			return Module.Deserialize(engine, "python", contents);
		}
		catch
		{
			// Failed to load the cached module for some reason, delete it.
			TryDelete(cachePath);
		}

		return null;
	}

	private void SaveCache(Module module)
	{
		if (string.IsNullOrEmpty(cachePath))
			return;

		try
		{
			using var stream = File.OpenWrite(cachePath);
			stream.Write(Hash);
			stream.Write(module.Serialize());
		}
		catch
		{
			// Something went wrong saving the cache.
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
