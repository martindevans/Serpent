using Serpent.Loading;
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

		using var prebuild = PythonBuilder.Load(engine, new DefaultPythonModuleLoader());
		using var python = prebuild
			.Create()
            .WithFuel(1_234_567_890)
			.Build();

		// The provided python build should be async capable.
		Assert.IsTrue(python.IsAsync);

		// It doesn't start suspended, even before the first execute, as IsSuspended is for async only.
		Assert.IsFalse(python.IsSuspended);

		// It doesn't start completed (obviously)
		Assert.IsFalse(python.IsCompleted);

		// Check the fuel we configured it with is available
        Assert.AreEqual(1_234_567_890ul, python.Fuel);

		// Check the memory is in some sensible range (0 to 50MB). Memory is initialised with some
		// pages pre-allocated, so we can't assume this is zero.
		Assert.IsTrue(python.MemoryBytes >= 0);
		Assert.IsTrue(python.MemoryBytes <= 50_000_000);
    }
}
