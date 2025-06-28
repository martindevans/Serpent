using System.Text;
using Wasmtime;
using Wazzy.Async;
using Wazzy.WasiSnapshotPreview1.Clock;
using Wazzy.WasiSnapshotPreview1.FileSystem.Implementations.VirtualFileSystem.Files;
using Wazzy.WasiSnapshotPreview1.Random;

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
        var code = """
                   import sys
                   sys.exit(123)
                   """u8.ToArray();

        using var prebuild = LoadCachedBuilder();
        using var python = prebuild
            .Create()
            .WithStdErr(() => new ConsoleLog("", ConsoleColor.DarkRed, error: true))
            .WithStdOut(() => new ConsoleLog(""))
            .WithCode(code)
            .Build();

        var fuelStart = python.Fuel;
        var exitCode = python.Execute();
        var fuelEnd = python.Fuel;

		// Fuel should have been consumed
		Assert.IsTrue(fuelEnd < fuelStart);

		// It should exit immediately, as nothing suspends.
		Assert.IsFalse(python.IsSuspended);
		Assert.IsTrue(python.IsCompleted);
		Assert.IsNull(python.SuspendedReason);
		Assert.AreEqual(123, exitCode);

		// Resuming after completion is not allowed, check that this throws
        Assert.ThrowsException<InvalidOperationException>(() =>
        {
            // ReSharper disable once AccessToDisposedClosure
            python.Execute();
        });
    }

    [TestMethod]
    public void OutOfFuel()
    {
        var code = """
                   import sys
                   sys.exit(123)
                   """u8.ToArray();

        using var prebuild = LoadCachedBuilder();
        using var python = prebuild
                          .Create()
                          .WithStdErr(() => new ConsoleLog("", ConsoleColor.DarkRed, error: true))
                          .WithStdOut(() => new ConsoleLog(""))
                          .WithCode(code)
                          .Build();

		// No fuel available
        python.Fuel = 0;
		Assert.AreEqual(0ul, python.Fuel);

        try
        {
			// Execute, which should immediately fail
            python.Execute();
        }
        catch (TrapException trap)
        {
            Assert.AreEqual(TrapCode.OutOfFuel, trap.Type);
            Assert.AreEqual(0ul, python.Fuel);
            return;
        }

		Assert.Fail("Did not throw!");
    }


    [TestMethod]
	public void StdConsole()
	{
		var code = """
			import sys
			sys.stderr.write("Hello stderr")
			sys.stdout.write("Hello stdout")
			assert sys.stdin.readline() == "Hello from stdin"
			"""u8.ToArray();

		var stderr = new MemoryStream();
		var stdout = new MemoryStream();
		var stdin = new MemoryStream();

		using var prebuild = LoadCachedBuilder();
		var python = prebuild
			.Create()
			.WithStdErr(() => new InMemoryFile(0, [], backing: stderr))
			.WithStdOut(() => new InMemoryFile(0, [], backing: stdout))
			.WithStdIn(() => new InMemoryFile(0, "Hello from stdin"u8, backing: stdin))
			.WithCode(code)
			.Build();

		var exitCode = RunToCompletionSync(python);
		Assert.AreEqual(0, exitCode);
		Assert.IsTrue(python.IsCompleted);

		Assert.AreEqual("Hello stderr", Encoding.UTF8.GetString(stderr.ToArray()));
		Assert.AreEqual("Hello stdout", Encoding.UTF8.GetString(stdout.ToArray()));
		// stdin is tested on the python side, it'll return non-zero exit code
	}

	[TestMethod]
	public void SchedYield()
	{
		var code = """
			import os
			import sys
			os.sched_yield()
			sys.exit(321)
			"""u8.ToArray();

		using var prebuild = LoadCachedBuilder();
		var python = prebuild
			.Create()
			.WithStdErr(() => new ConsoleLog("", ConsoleColor.DarkRed, error: true))
			.WithStdOut(() => new ConsoleLog(""))
			.WithCode(code)
			.Build();

		// Run once, to the yield point
		var exitCode1 = python.Execute();
		Assert.IsNull(exitCode1);
		Assert.IsTrue(python.IsSuspended);
		Assert.IsInstanceOfType<SchedYieldSuspend>(python.SuspendedReason);

		// Run again, this time completing execution and exiting
        var exitCode2 = python.Execute();
		Assert.AreEqual(321, exitCode2);
        Assert.IsFalse(python.IsSuspended);
        Assert.IsTrue(python.IsCompleted);
        Assert.IsNull(python.SuspendedReason);
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

    [TestMethod]
    public void WithClock()
    {
        var code = """
                   import datetime
                   now = datetime.datetime.now()
                   print(now)
                   """u8.ToArray();

        var stdout = new MemoryStream();

        using var prebuild = LoadCachedBuilder();
        using var python = prebuild
                          .Create()
                          .WithClock<ManualClock>(() => new ManualClock(new DateTime(2134, 5, 7), TimeSpan.FromMicroseconds(1)))
                          .WithStdErr(() => new ConsoleLog("", ConsoleColor.DarkRed, error: true))
                          .WithStdOut(() => new InMemoryFile(0, [], backing: stdout))
                          .WithCode(code)
                          .Build();

        python.Execute();

        Assert.IsTrue(python.IsCompleted);
        Assert.IsFalse(python.IsSuspended);
        Assert.AreEqual("2134-05-06 23:00:00\n", Encoding.UTF8.GetString(stdout.ToArray()));
    }

    [TestMethod]
    public void WithRandom()
    {
        var code = """
                   import random
                   print(str(random.randint(0, 1000)))
                   """u8.ToArray();

        var stdout = new MemoryStream();

        using var prebuild = LoadCachedBuilder();
        using var python = prebuild
                          .Create()
                          .WithRandomSource(() => new SeededRandomSource(123))
                          .WithStdErr(() => new ConsoleLog("", ConsoleColor.DarkRed, error: true))
                          .WithStdOut(() => new InMemoryFile(0, [], backing: stdout))
                          .WithCode(code)
                          .Build();

        python.Execute();

        Assert.IsTrue(python.IsCompleted);
        Assert.IsFalse(python.IsSuspended);

        // In theory it's possible this test could break if the Python blob changes in the future. Python is initialising
        // it's internal RNG from the sseded random source, and then producing a number from that. If Python changes the
        // internals of that process the final value might change. The important thing is it doesn't change between runs.
        Assert.AreEqual("727\n", Encoding.UTF8.GetString(stdout.ToArray()));
    }
}
