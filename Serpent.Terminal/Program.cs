using Serpent;
using Wasmtime;
using Wazzy.WasiSnapshotPreview1.FileSystem.Implementations.VirtualFileSystem.Files;

var e = new Engine(new Config().WithFuelConsumption(true).WithOptimizationLevel(OptimizationLevel.Speed));

using var builder = PythonBuilder.Load(e);

var python = builder
    .Create()
    .WithStdErr(() => new ConsoleLog("", ConsoleColor.DarkRed, error:true))
    .WithStdOut(() => new ConsoleLog(""))
    .WithMainFilePath("code.py")
    .WithCode(File.ReadAllBytes("code.py"))
    .Build();

python.Execute();
while (python.IsSuspended)
{
    python.Execute();
    await Task.Delay(256);
}