using Wasmtime;

namespace Serpent.Tests;

[TestClass]
public class PythonModuleLoaderTests
{
	[TestMethod]
	public void InvalidCache()
	{
		const string cachePath = $"Tests_{nameof(BuilderTests)}_{nameof(InvalidCache)}.cached.wtmodule";
		File.WriteAllBytes(cachePath, []);

		var engine = new Engine(new Config().WithFuelConsumption(true).WithOptimizationLevel(OptimizationLevel.Speed));

		var oldModifiedTime = File.GetLastWriteTimeUtc(cachePath);
		new DefaultPythonModuleLoader(cachePath).LoadModule(engine);
		Assert.IsTrue(File.GetLastWriteTimeUtc(cachePath) > oldModifiedTime, "cache was not ignored when it should have been.");
	}

	[TestMethod]
	public void OutdatedCache()
	{
		const string cachePath = $"Tests_{nameof(BuilderTests)}_{nameof(OutdatedCache)}.cached.wtmodule";
		if (File.Exists(cachePath))
			File.Delete(cachePath);

		var engine = new Engine(new Config().WithFuelConsumption(true).WithOptimizationLevel(OptimizationLevel.Speed));

		new OldPythonModuleLoader(cachePath).LoadModule(engine);
		var oldModifiedTime = File.GetLastWriteTimeUtc(cachePath);
		new DefaultPythonModuleLoader(cachePath).LoadModule(engine);
		Assert.IsTrue(File.GetLastWriteTimeUtc(cachePath) > oldModifiedTime, "cache was not ignored when it should have been.");
	}
}
