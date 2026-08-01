"""
GibiWorld GLB structural inspector — GW-ARCH-001 sections 6.1, 6.2, 6.3.

Parses the glTF 2.0 binary container directly. No Blender dependency, so this runs
in CI and in the quarantine worker before any scene is ever constructed.

Section 6.1 requires: one scene; meters; +Y up; no external URI; no cameras/lights/scripts.
Section 6.2 requires the geometry/material/texture/rig budgets.
Section 6.3 requires the exact skeleton joint names and in-place locomotion.
"""
import json
import struct
import sys
from pathlib import Path

GLB_MAGIC = 0x46546C67
CHUNK_JSON = 0x4E4F534A
CHUNK_BIN = 0x004E4942

MAX_TRANSFER_BYTES = 45 * 1024 * 1024

REQUIRED_JOINTS = {
    "root": ["root", "pelvis", "spine_01", "spine_02", "chest", "neck", "head"],
    "front_left": ["clavicle_l", "upper_front_l", "lower_front_l", "paw_front_l"],
    "front_right": ["clavicle_r", "upper_front_r", "lower_front_r", "paw_front_r"],
    "rear_left": ["upper_rear_l", "lower_rear_l", "hock_l", "paw_rear_l"],
    "rear_right": ["upper_rear_r", "lower_rear_r", "hock_r", "paw_rear_r"],
}
FACE_JOINTS = ["jaw", "eye_l", "eye_r"]
OPTIONAL_TAIL = [f"tail_{i:02d}" for i in range(1, 7)]
OPTIONAL_EARS = [f"ear_{s}_{i:02d}" for s in ("l", "r") for i in range(1, 4)]

REQUIRED_CLIPS = [
    "idle_a", "idle_b", "walk", "trot", "run", "turn_l_90", "turn_r_90",
    "sit", "sit_idle", "stand", "down", "down_idle", "rise",
    "jump_takeoff", "jump_air", "jump_land", "pickup", "carry", "drop",
    "success", "greet", "pet_react", "sleep",
]
LOCOMOTION_CLIPS = {"walk", "trot", "run"}

LIMITS = dict(
    lod0_triangles=35_000, lod0_vertices=45_000,
    lod1_triangles=18_000, lod2_triangles=7_500, lod3_triangles=2_000,
    skinned_meshes=2, weights_per_vertex=4, deform_bones=96,
    materials=3, base_normal_tex=2048, other_tex=1024,
    decoded_texture_bytes=48 * 1024 * 1024,
    morph_targets=12, clips=48, total_keyframes=300_000,
)


def read_glb(path: Path):
    data = path.read_bytes()
    if len(data) < 12:
        raise ValueError("file shorter than GLB header")
    magic, version, length = struct.unpack_from("<III", data, 0)
    if magic != GLB_MAGIC:
        raise ValueError("not a GLB container")
    if version != 2:
        raise ValueError(f"glTF binary version {version}, expected 2")

    gltf, bin_chunk = None, None
    offset = 12
    while offset < min(length, len(data)):
        clen, ctype = struct.unpack_from("<II", data, offset)
        offset += 8
        chunk = data[offset:offset + clen]
        if ctype == CHUNK_JSON:
            gltf = json.loads(chunk.decode("utf-8"))
        elif ctype == CHUNK_BIN:
            bin_chunk = chunk
        offset += clen + (-clen % 4)
    if gltf is None:
        raise ValueError("no JSON chunk")
    return gltf, bin_chunk, len(data)


def count_triangles(gltf):
    """Sum triangles across all primitives. Mode 4 == TRIANGLES."""
    accessors = gltf.get("accessors", [])
    total_tris = 0
    total_verts = 0
    for mesh in gltf.get("meshes", []):
        for prim in mesh.get("primitives", []):
            mode = prim.get("mode", 4)
            idx = prim.get("indices")
            pos = prim.get("attributes", {}).get("POSITION")
            if pos is not None and pos < len(accessors):
                total_verts += accessors[pos].get("count", 0)
            if idx is not None and idx < len(accessors):
                n = accessors[idx].get("count", 0)
            elif pos is not None and pos < len(accessors):
                n = accessors[pos].get("count", 0)
            else:
                n = 0
            if mode == 4:
                total_tris += n // 3
            elif mode in (5, 6):
                total_tris += max(0, n - 2)
    return total_tris, total_verts


def inspect(path: Path):
    gltf, _bin, size = read_glb(path)
    findings = []          # normative violations
    notes = []             # informational

    _seen = set()
    def viol(code, detail):
        key = (code, detail)
        if key in _seen:
            return
        _seen.add(key)
        findings.append({"code": code, "detail": detail})

    # ---- Section 6.2: transfer size ----
    if size > MAX_TRANSFER_BYTES:
        viol("TRANSFER_SIZE", f"{size} bytes exceeds 45 MiB")

    # ---- Section 6.1: single scene ----
    scenes = gltf.get("scenes", [])
    if len(scenes) != 1:
        viol("SCENE_COUNT", f"{len(scenes)} scenes, exactly 1 required")

    # ---- Section 6.1: no external URI (GW-ASSET-004) ----
    for i, buf in enumerate(gltf.get("buffers", [])):
        if "uri" in buf and not buf["uri"].startswith("data:"):
            viol("EXTERNAL_URI", f"buffer[{i}] -> {buf['uri'][:64]}")
    for i, img in enumerate(gltf.get("images", [])):
        if "uri" in img and not img["uri"].startswith("data:"):
            viol("EXTERNAL_URI", f"image[{i}] -> {img['uri'][:64]}")

    # ---- Section 6.1: no cameras or lights ----
    if gltf.get("cameras"):
        viol("CAMERA_PRESENT", f"{len(gltf['cameras'])} cameras")
    ext = gltf.get("extensions", {})
    if "KHR_lights_punctual" in ext:
        n = len(ext["KHR_lights_punctual"].get("lights", []))
        if n:
            viol("LIGHT_PRESENT", f"{n} punctual lights")

    # ---- Section 6.2: geometry budgets ----
    tris, verts = count_triangles(gltf)
    if tris > LIMITS["lod0_triangles"]:
        viol("LOD0_TRIANGLES", f"{tris} > {LIMITS['lod0_triangles']}")
    if verts > LIMITS["lod0_vertices"]:
        viol("LOD0_VERTICES", f"{verts} > {LIMITS['lod0_vertices']}")

    # ---- Section 6.2: materials ----
    mats = gltf.get("materials", [])
    if len(mats) > LIMITS["materials"]:
        viol("MATERIAL_COUNT", f"{len(mats)} > {LIMITS['materials']}")
    for i, m in enumerate(mats):
        # PBR metallic-roughness ONLY.
        if "extensions" in m:
            for e in m["extensions"]:
                if e not in ("KHR_materials_emissive_strength",):
                    viol("MATERIAL_EXTENSION", f"materials[{i}] uses {e}")
        if "KHR_materials_pbrSpecularGlossiness" in m.get("extensions", {}):
            viol("MATERIAL_NOT_METALLIC_ROUGHNESS", f"materials[{i}]")

    # ---- Section 6.2: skinned meshes and deform bones ----
    skins = gltf.get("skins", [])
    if len(skins) > LIMITS["skinned_meshes"]:
        viol("SKINNED_MESH_COUNT", f"{len(skins)} > {LIMITS['skinned_meshes']}")
    for i, sk in enumerate(skins):
        n = len(sk.get("joints", []))
        if n > LIMITS["deform_bones"]:
            viol("DEFORM_BONE_COUNT", f"skins[{i}] has {n} > {LIMITS['deform_bones']}")

    # ---- Section 6.2: >4 weights per vertex ----
    for mesh in gltf.get("meshes", []):
        for prim in mesh.get("primitives", []):
            if "WEIGHTS_1" in prim.get("attributes", {}):
                viol("WEIGHTS_PER_VERTEX", "WEIGHTS_1 present implies >4 influences")

    # ---- Section 6.2: morph targets ----
    for mesh in gltf.get("meshes", []):
        for prim in mesh.get("primitives", []):
            n = len(prim.get("targets", []))
            if n > LIMITS["morph_targets"]:
                viol("MORPH_TARGET_COUNT", f"{n} > {LIMITS['morph_targets']}")

    # ---- Section 6.2: textures ----
    for i, tex in enumerate(gltf.get("images", [])):
        if tex.get("mimeType") not in (None, "image/jpeg", "image/png", "image/webp"):
            notes.append(f"images[{i}] mimeType {tex.get('mimeType')}")

    # ---- Section 6.2/6.3: animation clips ----
    anims = gltf.get("animations", [])
    if len(anims) > LIMITS["clips"]:
        viol("CLIP_COUNT", f"{len(anims)} > {LIMITS['clips']}")

    clip_names = [a.get("name", f"<unnamed_{i}>") for i, a in enumerate(anims)]

    # Section 6.3: scale curves are FORBIDDEN.
    for a in anims:
        for ch in a.get("channels", []):
            if ch.get("target", {}).get("path") == "scale":
                viol("SCALE_CURVE_FORBIDDEN", f"clip '{a.get('name')}' animates scale")

    # Section 6.3: locomotion clips must be in-place (no root translation).
    node_names = {i: n.get("name", "") for i, n in enumerate(gltf.get("nodes", []))}
    for a in anims:
        nm = (a.get("name") or "").lower()
        if nm in LOCOMOTION_CLIPS:
            for ch in a.get("channels", []):
                t = ch.get("target", {})
                if t.get("path") == "translation" and node_names.get(t.get("node"), "") == "root":
                    viol("LOCOMOTION_NOT_IN_PLACE", f"clip '{nm}' translates root")

    # ---- Section 6.3: skeleton profile ----
    joint_names = set()
    for sk in skins:
        for j in sk.get("joints", []):
            joint_names.add(node_names.get(j, ""))

    missing = []
    for group, names in REQUIRED_JOINTS.items():
        for n in names:
            if n not in joint_names:
                missing.append(n)

    return {
        "file": path.name,
        "sizeBytes": size,
        "triangles": tris,
        "vertices": verts,
        "materials": len(mats),
        "skins": len(skins),
        "deformBones": max([len(s.get("joints", [])) for s in skins], default=0),
        "meshes": len(gltf.get("meshes", [])),
        "animations": len(anims),
        "clipNames": clip_names,
        "images": len(gltf.get("images", [])),
        "generator": gltf.get("asset", {}).get("generator", ""),
        "missingRequiredJoints": missing,
        "presentJointSample": sorted(j for j in joint_names if j)[:20],
        "violations": findings,
        "notes": notes,
    }


if __name__ == "__main__":
    out = []
    for arg in sys.argv[1:]:
        p = Path(arg)
        try:
            out.append(inspect(p))
        except Exception as e:
            out.append({"file": p.name, "error": str(e)})
    print(json.dumps(out, indent=2))
