using Serpent;
using Wasmtime;
using Wazzy.WasiSnapshotPreview1.FileSystem.Implementations.VirtualFileSystem.Files;

var e = new Engine(new Config().WithFuelConsumption(true).WithOptimizationLevel(OptimizationLevel.Speed));

using var builder = PythonBuilder.Load(e, "cache.module");

var python = builder
    .Create()
    .WithStdErr(() => new ConsoleLog("", ConsoleColor.DarkRed, error:true))
    .WithStdOut(() => new ConsoleLog(""))
    .WithMainFilePath("code.py")
    .WithCode(File.ReadAllBytes("code.py"))
    .Build();

var delay = 1;
python.Execute();
while (python.IsSuspended)
{
    python.Execute();

    await Task.Delay(delay);
    Thread.Yield();

    delay <<= 1;
    if (delay > 256)
        delay = 1;
}