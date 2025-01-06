using System.Diagnostics;
using Serpent;
using Wasmtime;
using Wazzy.Async;
using Wazzy.WasiSnapshotPreview1.FileSystem.Implementations.VirtualFileSystem.Files;

var e = new Engine(new Config().WithFuelConsumption(true).WithOptimizationLevel(OptimizationLevel.Speed));

using var builder = PythonBuilder.Load(e, "cache.module");

var prebuild = builder
    .Create()
    .WithStdErr(() => new ConsoleLog("", ConsoleColor.DarkRed, error: true))
    .WithStdOut(() => new ConsoleLog(""))
    //.WithStdIn(() => new InMemoryFile(0, "print('hello world')"u8))
    //.WithMainFilePath("code.py")
    .WithCode(File.ReadAllBytes("code.py"));

var timer = new Stopwatch();
timer.Start();

var python = prebuild.Build();

timer.Stop();
Console.WriteLine($"Build: {timer.Elapsed.TotalMilliseconds}ms");

timer.Start();
{
    python.Execute();
    while (python.IsSuspended)
    {
        Console.Title = $"Memory Usage: {python.MemoryBytes:N0}B";

        if (python.SuspendedReason is TaskSuspend ts)
        {
            await ts.Task;
        }
        else if (python.SuspendedReason is SchedYieldSuspend)
        {
            Console.WriteLine("YIELD");
        }
        else
        {
            await Task.Delay(1);
        }

        python.Execute();
    }
}
timer.Stop();
Console.WriteLine($"Execute: {timer.Elapsed.TotalMilliseconds}ms");
Console.WriteLine($"Memory Usage: {python.MemoryBytes:N0}B");