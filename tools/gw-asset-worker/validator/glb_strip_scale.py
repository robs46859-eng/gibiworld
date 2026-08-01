"""
Strip animated scale channels from a GLB — GW-ARCH-001 section 6.3:
"Scale curves are forbidden."

Blender's glTF exporter re-emits scale tracks even after every scale F-curve has been
removed in-scene, because it samples full TRS per bone. Removing them in Blender is
therefore not sufficient; the container itself must be rewritten.

Operates on the JSON chunk only. Binary buffer is left byte-identical, so the
orphaned sampler data remains but is unreferenced — no accessor renumbering, which
keeps the transform provably non-destructive to geometry and skinning.
"""
import json
import struct
import sys

GLB_MAGIC, CHUNK_JSON, CHUNK_BIN = 0x46546C67, 0x4E4F534A, 0x004E4942


def read_glb(path):
    data = open(path, "rb").read()
    magic, version, _ = struct.unpack_from("<III", data, 0)
    if magic != GLB_MAGIC or version != 2:
        raise ValueError("not a glTF 2.0 binary container")
    gltf = binchunk = None
    off = 12
    while off < len(data):
        clen, ctype = struct.unpack_from("<II", data, off)
        off += 8
        chunk = data[off:off + clen]
        if ctype == CHUNK_JSON:
            gltf = json.loads(chunk.decode("utf-8"))
        elif ctype == CHUNK_BIN:
            binchunk = chunk
        off += clen + (-clen % 4)
    return gltf, binchunk


def write_glb(path, gltf, binchunk):
    js = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
    js += b" " * (-len(js) % 4)
    parts = [struct.pack("<II", len(js), CHUNK_JSON), js]
    if binchunk is not None:
        pad = binchunk + b"\x00" * (-len(binchunk) % 4)
        parts += [struct.pack("<II", len(pad), CHUNK_BIN), pad]
    body = b"".join(parts)
    header = struct.pack("<III", GLB_MAGIC, 2, 12 + len(body))
    open(path, "wb").write(header + body)


def strip(path):
    gltf, binchunk = read_glb(path)
    removed, touched = 0, []
    for anim in gltf.get("animations", []):
        before = len(anim.get("channels", []))
        anim["channels"] = [c for c in anim.get("channels", [])
                            if c.get("target", {}).get("path") != "scale"]
        n = before - len(anim["channels"])
        if n:
            removed += n
            touched.append(anim.get("name"))
    # An animation left with no channels is invalid glTF; drop it.
    kept = [a for a in gltf.get("animations", []) if a.get("channels")]
    dropped_anims = len(gltf.get("animations", [])) - len(kept)
    if dropped_anims:
        gltf["animations"] = kept
    if removed:
        write_glb(path, gltf, binchunk)
    return {"file": path.split("/")[-1], "scaleChannelsRemoved": removed,
            "clipsTouched": touched, "emptyClipsDropped": dropped_anims}


if __name__ == "__main__":
    for p in sys.argv[1:]:
        print(json.dumps(strip(p)))
