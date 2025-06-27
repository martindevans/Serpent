using Wasmtime;

namespace Serpent;

public interface IPythonModuleLoader
{
	/// <summary>
	/// Loads a python wasm blob for the PythonBuilder.
	/// </summary>
	/// <param name="engine">The wasmtime engine to load the module with.</param>
	/// <returns>The wasmtime module</returns>
	public Module LoadModule(Engine engine);
}
