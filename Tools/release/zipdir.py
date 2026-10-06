#!/usr/bin/env python3
"""zipdir.py OUT.zip DIR ENTRY [ENTRY...]: zip ENTRYs (relative to DIR), keeping Unix permissions and
symlinks so executables and .app bundles still work after unzipping. Standard library only."""
import os
import stat
import sys
import zipfile

def add(zf, base, rel):
    full = os.path.join(base, rel)
    st = os.lstat(full)
    info = zipfile.ZipInfo(rel + ("/" if stat.S_ISDIR(st.st_mode) else ""))
    info.date_time = (2026, 1, 1, 0, 0, 0)          # stable archives
    info.create_system = 3                          # Unix, so external_attr carries the mode
    info.external_attr = (st.st_mode & 0xFFFF) << 16
    if stat.S_ISLNK(st.st_mode):
        zf.writestr(info, os.readlink(full))
    elif stat.S_ISDIR(st.st_mode):
        info.external_attr |= 0x10
        zf.writestr(info, b"")
        for name in sorted(os.listdir(full)):
            add(zf, base, os.path.join(rel, name))
    else:
        info.compress_type = zipfile.ZIP_DEFLATED
        with open(full, "rb") as f:
            zf.writestr(info, f.read(), compresslevel=9)

def main():
    out, base, entries = sys.argv[1], sys.argv[2], sys.argv[3:]
    if os.path.exists(out):
        os.remove(out)
    with zipfile.ZipFile(out, "w") as zf:
        for e in entries:
            add(zf, base, e)

if __name__ == "__main__":
    main()
