using System.Security.Cryptography;
using Wasmtime;

namespace Serpent;

/// <summary>
/// Caches the wasmtime module at a given path for future loads to be faster.
/// </summary>
/// <param name="cachePath">Path to save and load the cached module from. If null, disables caching.</param>
public abstract class FileCachedPythonModuleLoader(string? cachePath = null) : IPythonModuleLoader
{
	/// <summary>
	/// Load a wasmtime module when it isn't already cached.
	/// </summary>
	protected abstract Module LoadModuleForCache(Engine engine);

	/// <summary>
	/// Prepended to the cached file to ensure outdated wasm blobs are not loaded.
	/// This should be a hash of the input wasm blob and should be a fixed size.
	/// A good option is the MD5 hash algorithm.
	/// </summary>
	protected abstract byte[] Hash { get; }

	public Module LoadModule(Engine engine)
	{
		var module = LoadCache(engine);
		if (module != null) return module;

		module = LoadModuleForCache(engine);
		SaveCache(module);
		return module;
	}

	private Module? LoadCache(Engine engine)
	{
		if (string.IsNullOrEmpty(cachePath) || !File.Exists(cachePath))
			return null;

		try {
			using var stream = File.OpenRead(cachePath);
			var hash = new byte[Hash.Length];
			stream.ReadExactly(hash);
			if (!hash.SequenceEqual(Hash))
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
