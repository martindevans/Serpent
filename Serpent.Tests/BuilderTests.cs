using Wasmtime;
using Wazzy.WasiSnapshotPreview1.FileSystem.Implementations.VirtualFileSystem.Files;

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

		var engine = new Engine(new Config().WithFuelConsumption(true).WithOptimizationLevel(OptimizationLevel.Speed));
		var prebuild = PythonBuilder.Load(engine, cachePath);
		var python = prebuild.Create().Build();
	}

	[TestMethod]
	public void WithStdConsole()
	{
		using var prebuild = LoadCachedBuilder();
		var python = prebuild
			.Create()
			.WithStdErr(() => new ConsoleLog("", ConsoleColor.DarkRed, error: true))
			.WithStdOut(() => new ConsoleLog(""))
			.Build();
	}

	[TestMethod]
	public void WithCodeBuild()
	{
		using var prebuild = LoadCachedBuilder();
		var python = prebuild
			.Create()
			.WithCode(Array.Empty<byte>())
			.Build();
	}

	[TestMethod]
	public void WithMainFileBuild()
	{
		using var prebuild = LoadCachedBuilder();
		var python = prebuild
			.Create()
			.WithMainFilePath("my_main.py")
			.Build();
	}

	[TestMethod]
	public void WithCodeAndMainFileBuild()
	{
		using var prebuild = LoadCachedBuilder();
		var python = prebuild
			.Create()
			.WithCode(Array.Empty<byte>())
			.WithMainFilePath("my_main.py")
			.Build();
	}

	[TestMethod]
	public void WithFilesystem()
	{
		using var prebuild = LoadCachedBuilder();
		var python = prebuild
			.Create()
			.WithFilesystem(builder => {
				builder.CreateVirtualDirectory("test", testDir => {
					testDir.CreateInMemoryFile("test.txt", "Hello world!"u8.ToArray());
				});
			})
			.Build();
	}
}
