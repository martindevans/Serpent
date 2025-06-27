using Wasmtime;
using Wazzy.Async;
using Wazzy.WasiSnapshotPreview1.FileSystem.Implementations.VirtualFileSystem.Files;

namespace Serpent.Tests;

[TestClass]
public class EndToEndTests
{
	private static PythonBuilder LoadCachedBuilder()
	{
		var engine = new Engine(new Config().WithFuelConsumption(true).WithOptimizationLevel(OptimizationLevel.Speed));
		return PythonBuilder.Load(engine, $"Tests_{nameof(EndToEndTests)}.cached.wtmodule");
	}

	private static int? RunToCompletionSync(Python python)
	{
		var exitCode = python.Execute();
		while (python.IsSuspended)
		{
			switch (python.SuspendedReason) {
				case TaskSuspend ts:
					ts.Task.Wait();
					break;
				case SchedYieldSuspend:
					break;
			}

			exitCode = python.Execute();
		}

		return exitCode;
	}

	[ClassInitialize]
	public static void Setup(TestContext testContext)
	{
		LoadCachedBuilder().Dispose();
	}

	[TestMethod]
	public void SingleExecuteExit()
	{
		using var prebuild = LoadCachedBuilder();
		var python = prebuild
			.Create()
			.WithStdErr(() => new ConsoleLog("", ConsoleColor.DarkRed, error: true))
			.WithStdOut(() => new ConsoleLog(""))
			.WithCode(Array.Empty<byte>())
			.Build();

		var exitCode = python.Execute();
		// It should exit immediately, as nothing suspends.
		Assert.IsFalse(python.IsSuspended);
		Assert.IsTrue(python.IsCompleted);
		Assert.IsNull(python.SuspendedReason);
		Assert.AreEqual(0, exitCode);
	}

	[TestMethod]
	public void SchedYield()
	{
		var code = """
			import os
			os.sched_yield()
			"""u8.ToArray();

		using var prebuild = LoadCachedBuilder();
		var python = prebuild
			.Create()
			.WithStdErr(() => new ConsoleLog("", ConsoleColor.DarkRed, error: true))
			.WithStdOut(() => new ConsoleLog(""))
			.WithCode(code)
			.Build();

		var exitCode = python.Execute();
		Assert.IsNull(exitCode);
		Assert.IsTrue(python.IsSuspended);
		Assert.IsInstanceOfType<SchedYieldSuspend>(python.SuspendedReason);
	}

	[TestMethod]
	public void Filesystem()
	{
		var code = """
			with open("test/test.txt", "r", encoding="utf8") as f:
				assert f.read() == "Hello world!"
			with open("test/test.txt", "w", encoding="utf8") as f:
				f.write("Goodbye")
			with open("test/test.txt", "a", encoding="utf8") as f:
				f.write(" world.")
			with open("test/test.txt", "r", encoding="utf8") as f:
				assert f.read() == "Goodbye world."
			"""u8.ToArray();

		using var prebuild = LoadCachedBuilder();
		var python = prebuild
			.Create()
			.WithStdErr(() => new ConsoleLog("", ConsoleColor.DarkRed, error: true))
			.WithStdOut(() => new ConsoleLog(""))
			.WithCode(code)
			.WithFilesystem(builder => {
				builder.CreateVirtualDirectory("test", testDir => {
					testDir.CreateInMemoryFile("test.txt", "Hello world!"u8.ToArray());
				});
			})
			.Build();

		var exitCode = RunToCompletionSync(python);
		Assert.AreEqual(0, exitCode);
		Assert.IsTrue(python.IsCompleted);
	}
}
