using Wasmtime;

namespace Serpent.Loading;

public interface IPythonModuleLoader
{
	/// <summary>
	/// Loads a python wasm <see cref="Module"/>
	/// </summary>
	/// <param name="engine">The wasmtime engine to load the module with.</param>
	/// <returns>The wasmtime module</returns>
	Module LoadModule(Engine engine);

    /// <summary>
    /// Get the hash of the wasm module this loader will load
    /// </summary>
    /// <returns></returns>
    ReadOnlySpan<byte> GetHash();
}
