import sys
import os
import random
import datetime
import time

from pathlib import Path

for i in range(0, 100):
    time.sleep(0.01)
    #os.sched_yield()

# prefix components:
space =  '    '
branch = '│   '
# pointers:
tee =    '├── '
last =   '└── '


def tree(dir_path: Path, prefix: str=''):
    """A recursive generator, given a directory Path object
    will yield a visual tree structure line by line
    with each line prefixed by the same characters
    """    
    contents = list(dir_path.iterdir())
    # contents each get pointers that are ├── with a final └── :
    pointers = [tee] * (len(contents) - 1) + [last]
    for pointer, path in zip(pointers, contents):
        yield prefix + pointer + path.name
        if path.is_dir(): # extend the prefix and recurse:
            extension = branch if pointer == tee else space 
            # i.e. space because last, └── , above so no more |
            yield from tree(path, prefix=prefix+extension)

print('Hello from python on', sys.platform, os.name)
print('random: ' + str(random.randint(0, 999)))
print('time: ' + str(datetime.datetime.now().time()))

print()
print('### Arguments ###')
print(sys.argv)

#for i in range(0, 4):
#    print(i)
#    time.sleep(1)

print()
print('### Env Vars ###')
for k, v in sorted(os.environ.items()):
    print(k + ': ' + v)

print()
print("### Recursive Files")
print('/')
for line in tree(Path('/')):
    print(line)

print()
print("### Touch");
p = Path("indicator")
if p.exists():
    print("second run")
else:
    p.touch()

exit(17)