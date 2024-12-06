# Serpent

A WASM based Python environment for dotnet.

## Updating CPython wasm binary and standard library.
Serpent uses pre-built binaries and python standard libraries from [Avril112113/build-cpython-wasi](https://github.com/Avril112113/build-cpython-wasi). This repo contains a GitHub action which builds the wasm binary.  
Alternatively you can manually build [python/cpython](https://github.com/python/cpython).

#### Manually Building CPython

After successfully building cpython, make sure to have the necessary files:

> `make -C ./cross-build/wasm32-wasip1 install DESTDIR=./tmp`

Running this will produce:

> `./cross-build/wasm32-wasip1/tmp/usr/local/lib/python3.YY`  
> `./cross-build/wasm32-wasip1/tmp/usr/local/bin/python3.13.wasm`

Extracting files for Serpent:

1. Zip the Python3.YY folder, zip it and rename it to `lib.zip`. Replace [lib.zip](https://github.com/martindevans/Serpent/blob/master/Serpent/lib.zip).
2. Take `python3.13.wasm` and asyncify it with [binaryen](https://github.com/WebAssembly/binaryen):

> ./wasm-opt python3.13.wasm --asyncify -O2 --output python3.13_async.wasm

3. Take `python3.13_async.wasm` and replace `ython3.13_async.wasm` [here](https://github.com/martindevans/Serpent/tree/master/Serpent).
