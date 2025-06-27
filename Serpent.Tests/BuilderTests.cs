using Wasmtime;

namespace Serpent.Tests;

[TestClass]
public class BuilderTests
{
	private static PythonBuilder LoadCachedBuilder()
	{
		var engine = new Engine(new Config().WithFuelConsumption(true).WithOptimizationLevel(OptimizationLevel.Speed));
		return PythonBuilder.Load(engine, $"Tests_{nameof(BuilderTests)}.cached.wtmodule");
	}

	[ClassInitialize]
	public static void Setup(TestContext testContext)
	{
		LoadCachedBuilder().Dispose();
	}

	[TestMethod]
	public void FreshSimpleBuild()
	{
		var engine = new Engine(new Config().WithFuelConsumption(true).WithOptimizationLevel(OptimizationLevel.Speed));

		using var prebuild = PythonBuilder.Load(engine);
		var python = prebuild
			.Create()
			.Build();

		// The provided python build should be async capable.
		Assert.IsTrue(python.IsAsync);
		// It doesn't start suspended, even before the first execute, as IsSuspended is for async only.
		Assert.IsFalse(python.IsSuspended);
		// It doesn't start completed (obviously)
		Assert.IsFalse(python.IsCompleted);
	}

	[TestMethod]
	public void InvalidCache()
	{
		const string cachePath = $"Tests_{nameof(BuilderTests)}_{nameof(InvalidCache)}.cached.wtmodule";
		File.WriteAllBytes(cachePath, []);

		var oldModifiedTime = File.GetLastWriteTimeUtc(cachePath);
		var engine = new Engine(new Config().WithFuelConsumption(true).WithOptimizationLevel(OptimizationLevel.Speed));
		var prebuild = PythonBuilder.Load(engine, cachePath);
		var python = prebuild.Create().Build();
		Assert.IsTrue(File.GetLastWriteTimeUtc(cachePath) > oldModifiedTime, "cache was not ignored when it should have been.");
	}
}
