using Serpent.Loading;
using Wasmtime;

namespace Serpent.Tests;

[TestClass]
public class PythonModuleLoaderTests
{
	[TestMethod]
	public void InvalidCache()
	{
		// Write some junk to the cache file
		const string cachePath = $"Tests_{nameof(BuilderTests)}_{nameof(InvalidCache)}.cached.wtmodule";
		File.WriteAllBytes(cachePath, [ 1, 2, 3, 4, 5 ]);

		var engine = new Engine(new Config().WithFuelConsumption(true).WithOptimizationLevel(OptimizationLevel.Speed));

		var oldModifiedTime = File.GetLastWriteTimeUtc(cachePath);

		// Load using the cache which we know is invalid.
		new FileCache(cachePath, new DefaultPythonModuleLoader()).LoadModule(engine);

		// Check that the cache has been updated
		Assert.IsTrue(File.GetLastWriteTimeUtc(cachePath) > oldModifiedTime, "cache was not ignored when it should have been.");
	}

	[TestMethod]
	public void OutdatedCache()
	{
		// Ensure there is no cache
		const string cachePath = $"Tests_{nameof(BuilderTests)}_{nameof(OutdatedCache)}.cached.wtmodule";
		if (File.Exists(cachePath))
			File.Delete(cachePath);

		var engine = new Engine(new Config().WithFuelConsumption(true).WithOptimizationLevel(OptimizationLevel.Speed));

		// Load an outdated version of python with that cache path
		new FileCache(cachePath, new OldPythonModuleLoader()).LoadModule(engine);

		var oldModifiedTime = File.GetLastWriteTimeUtc(cachePath);

		// Load an update version
        new FileCache(cachePath, new DefaultPythonModuleLoader()).LoadModule(engine);

		// Ensure the cache was changed when the new version was loaded
		Assert.IsTrue(File.GetLastWriteTimeUtc(cachePath) > oldModifiedTime, "cache was not ignored when it should have been.");
	}
}
