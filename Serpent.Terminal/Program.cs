using Serpent;
using Wasmtime;
using Wazzy.WasiSnapshotPreview1.FileSystem.Implementations.VirtualFileSystem.Files;

var e = new Engine(new Config().WithFuelConsumption(true).WithOptimizationLevel(OptimizationLevel.Speed));

var input = new MemoryStream();
var writer = new StreamWriter(input, leaveOpen:true);
writer.WriteLine("print('hello')");
writer.WriteLine("print(str(3 + 4))");
writer.Dispose();
input.Position = 0;

using var builder = PythonBuilder.Load(e);

var python = builder
    .Create()
    .WithStdIn(() => new InMemoryFile(0, input.ToArray()))
    .WithStdErr(() => new ConsoleLog("", ConsoleColor.DarkRed, error:true))
    .WithStdOut(() => new ConsoleLog(""))
    .Build(File.ReadAllBytes("code.py"));

python.Execute();
while (python.IsSuspended)
{
    python.Execute();
    await Task.Delay(256);
}