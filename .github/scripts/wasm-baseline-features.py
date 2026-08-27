#!/usr/bin/env python3
"""Drop two WebAssembly target-feature DECLARATIONS from a static archive.

Why this exists
---------------
rustc's objects declare `bulk-memory-opt` and `call-indirect-overlong` as used. Both are
implied by LLVM rather than chosen, so no combination of `-C target-feature` or `-C target-cpu`
removes them, and rustup's precompiled `std` and `compiler_builtins` carry them however this
crate is built.

emcc turns each declared feature into an `--enable-<name>` flag for `wasm-opt`. The binaryen in
Emscripten 3.1.56 -- what the .NET 9 and .NET 10 wasm workloads ship -- knows neither name and
exits with `Unknown option '--enable-bulk-memory-opt'`, taking the consumer's Blazor publish
down with it. Emscripten 6.0.2, which .NET 11 ships, knows both and needs none of this.

The entries are REMOVED, and that distinction is the whole design. The section's prefixes are
`+` (used), `-` (disallowed) and `=` (required), and flipping to `-` also works on 3.1.56 --
but it FORBIDS the feature, so wasm-ld then refuses to link the archive against a newer
Emscripten whose own objects use it: "Target feature 'bulk-memory-opt' used in ... is
disallowed by ...". Removing the entry instead says nothing either way, so the old toolchain
emits no flag it cannot parse and the new one unions the feature in from its own objects.
One archive, both toolchains. Measured on 3.1.56 and 6.0.2.

Removing the whole section is a third option and a wrong one: `+bulk-memory` goes with it and
binaryen rejects the module with "Bulk memory operations require bulk memory".

Delete this script once the oldest supported consumer workload ships Emscripten >= 6.

Usage: wasm-baseline-features.py <archive.a> [llvm-ar]
"""

import subprocess
import sys
import tempfile
from pathlib import Path

# A closed list, deliberately: every other feature in the section is one both toolchains agree
# on, and a wider net would drop a declaration something downstream relies on.
DROP = {b"bulk-memory-opt", b"call-indirect-overlong"}

SECTION_NAME = b"target_features"


def uleb(data: bytes, at: int) -> tuple[int, int]:
    """Decode an unsigned LEB128 at `at`. Returns (value, index just past it)."""
    value = shift = 0
    while True:
        byte = data[at]
        at += 1
        value |= (byte & 0x7F) << shift
        if not byte & 0x80:
            return value, at
        shift += 7


def emit_uleb(value: int) -> bytes:
    out = bytearray()
    while True:
        byte = value & 0x7F
        value >>= 7
        out.append(byte | (0x80 if value else 0))
        if not value:
            return bytes(out)


def rewrite(data: bytes) -> tuple[bytes, int]:
    """Return (new object bytes, entries dropped)."""
    at = 8  # magic + version
    while at < len(data):
        section_id = data[at]
        size, body = uleb(data, at + 1)
        end = body + size

        if section_id == 0:  # custom section
            name_len, name_at = uleb(data, body)
            if data[name_at:name_at + name_len] == SECTION_NAME:
                cursor = name_at + name_len
                count, cursor = uleb(data, cursor)

                kept, dropped = [], 0
                for _ in range(count):
                    prefix = data[cursor]
                    entry_len, after_len = uleb(data, cursor + 1)
                    name = data[after_len:after_len + entry_len]
                    if name in DROP:
                        dropped += 1
                    else:
                        kept.append(bytes([prefix]) + emit_uleb(entry_len) + name)
                    cursor = after_len + entry_len

                if not dropped:
                    return data, 0

                payload = (emit_uleb(name_len) + SECTION_NAME
                           + emit_uleb(len(kept)) + b"".join(kept)
                           + data[cursor:end])  # anything trailing, normally nothing
                section = bytes([0]) + emit_uleb(len(payload)) + payload
                return data[:at] + section + data[end:], dropped

        at = end
    return data, 0


def main() -> int:
    archive = Path(sys.argv[1]).resolve()
    ar = sys.argv[2] if len(sys.argv) > 2 else "llvm-ar"

    with tempfile.TemporaryDirectory() as work:
        subprocess.run([ar, "x", str(archive)], cwd=work, check=True)

        objects = sorted(Path(work).glob("*.o"))
        if not objects:
            print("error: the archive contains no objects", file=sys.stderr)
            return 1

        total = touched = 0
        for obj in objects:
            new, dropped = rewrite(obj.read_bytes())
            if dropped:
                obj.write_bytes(new)
                total += dropped
                touched += 1

        if not total:
            # Not a failure: a toolchain that stops declaring these leaves nothing to do, which
            # is exactly the signal that this script can be deleted.
            print(f"{archive.name}: nothing to drop; the toolchain no longer declares "
                  f"{', '.join(sorted(n.decode() for n in DROP))}")
            return 0

        archive.unlink()
        subprocess.run([ar, "crs", str(archive)] + [o.name for o in objects],
                       cwd=work, check=True)

    print(f"{archive.name}: dropped {total} declaration(s) across {touched} of "
          f"{len(objects)} objects")
    return 0


if __name__ == "__main__":
    sys.exit(main())
