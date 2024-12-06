# Serpent
TODO: Rest of the readme.

## Updating CPython wasm binary and standard library.
Serpent uses pre-built binaries and python standard libraries from [Avril112113/build-cpython-wasi](https://github.com/Avril112113/build-cpython-wasi)  
Alternatively you can manually build [python/cpython](https://github.com/python/cpython), see notes below.  

**Only if you have built CPython manually;**  
After successfully building cpython, make sure to have the necessary files;  
`make -C ./cross-build/wasm32-wasip1 install DESTDIR=./tmp`  
After running the above, the following paths will be relevant below.  
`./cross-build/wasm32-wasip1/tmp/usr/local/lib/python.YY`  
`./cross-build/wasm32-wasip1/tmp/usr/local/bin/python3.13.wasm`

**Setting up files for Serpent**  
You should have a folder that looks like `./lib/python3.YY/**.py`.  
Zip the `python3.YY` folder and rename it to `lib.zip`.  

Now you have `python3.YY_async.wasm` and `lib.zip`.  
That's all that's needed to use a different python version.  
