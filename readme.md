# Simple CPython wasi build script
This script was made out of the pains of building CPython for wasi.  
Despite tooling making it mostly easy, there were some things that took a while to figure out.  

Instructions:  
- Clone this repo  
- Open in devcontainer (in vscode)  
- Wait patiently  
- Read `./build-wasi.sh` to customize build  
- Run `./build-wasi.sh`  
- Wait patiently  
- Check `./out/`  
    `./for_external_builds/` this is used if you were to link to libpython, which is helpful for utilizing wasm imports.  
    `./lib/` this is the python standard library and needs to be made available by the wasm host.  
    `./python3.*.wasm` the unmodified CPython wasi build.  
    `./python3.*_async.wasm` Asyncified and optimized with wasm-opt, if enabled.  
- Test with `wasmtime run --wasm max-wasm-stack=8388608 --dir ./out/lib::/lib --env PYTHONHOME=/ ./out/python3.13.wasm`

Only supports CPython 3.13+ as that's when they added `Tools/wasm/wasi.py`  
It would be possible to support older versions if we didn't using that script.  
