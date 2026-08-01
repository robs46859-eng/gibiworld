"""
GibiWorld asset remediation worker — GW-ARCH-001 sections 6.2, 6.3, 19.

Runs INSIDE Blender, headless, in the quarantine container:

    blender --background --factory-startup --python blender_remediate.py -- \
            --input  quarantine/<sha256>.glb \
            --profile profiles/pawsome_to_gibi_quadruped_v1.json \
            --output staging/<sha256>/

--factory-startup is mandatory: it guarantees no user addon, preference, or startup
file can influence the output, which is what makes the build deterministic and the
provenance record meaningful.

The worker NEVER executes anything supplied by the asset. It only reads geometry and
animation data and writes a canonicalized result.
"""
import argparse
import hashlib
import json
import sys
from pathlib import Path

try:
    import bpy
except ImportError:
    print("FATAL: must run inside Blender (blender --background --python ...)", file=sys.stderr)
    raise SystemExit(2)

LOD_TARGETS = [
    ("LOD0", 35_000, None),
    ("LOD1", 18_000, 0.42),
    ("LOD2",  7_500, 0.18),
    ("LOD3",  2_000, 0.06),
]


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def sha256_file(p: Path) -> str:
    h = hashlib.sha256()
    with p.open("rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def import_glb(path: Path):
    bpy.ops.import_scene.gltf(filepath=str(path))


def triangle_count() -> int:
    total = 0
    for ob in bpy.data.objects:
        if ob.type != "MESH":
            continue
        me = ob.data
        me.calc_loop_triangles()
        total += len(me.loop_triangles)
    return total


def strip_scale_curves() -> int:
    """Section 6.3: 'Scale curves are forbidden.' Remove every scale F-curve."""
    removed = 0
    for action in bpy.data.actions:
        for fc in list(action.fcurves):
            if fc.data_path.endswith("scale"):
                action.fcurves.remove(fc)
                removed += 1
    return removed


def force_root_identity(tol=1e-4) -> bool:
    """Section 6.3: 'Root transforms SHALL remain identity within 1e-4.'"""
    changed = False
    for ob in bpy.data.objects:
        if ob.type != "ARMATURE":
            continue
        if any(abs(v) > tol for v in ob.location):
            ob.location = (0, 0, 0); changed = True
        if any(abs(v) > tol for v in ob.rotation_euler):
            ob.rotation_euler = (0, 0, 0); changed = True
        if any(abs(v - 1.0) > tol for v in ob.scale):
            ob.scale = (1, 1, 1); changed = True
    return changed


def rename_bones(bone_map: dict) -> dict:
    """Apply the retarget map. Joint names are CASE-SENSITIVE (section 6.3)."""
    applied, missing = {}, []
    for arm in [o for o in bpy.data.objects if o.type == "ARMATURE"]:
        names = {b.name for b in arm.data.bones}
        for src, dst in bone_map.items():
            if src in names:
                arm.data.bones[src].name = dst
                applied[src] = dst
            else:
                missing.append(src)
    return {"applied": applied, "sourceBonesNotFound": sorted(set(missing))}


def retarget_clips(clip_map: dict, drop: dict) -> dict:
    renamed, dropped = {}, []
    for action in list(bpy.data.actions):
        nm = action.name
        if nm in drop:
            bpy.data.actions.remove(action)
            dropped.append(nm)
        elif nm in clip_map:
            action.name = clip_map[nm]
            renamed[nm] = clip_map[nm]
    return {"renamed": renamed, "dropped": dropped}


def decimate_to(target_tris: int) -> dict:
    """Non-destructive ratio decimation, applied per mesh object."""
    before = triangle_count()
    if before <= target_tris:
        return {"before": before, "after": before, "ratio": 1.0, "applied": False}

    ratio = target_tris / float(before)
    for ob in [o for o in bpy.data.objects if o.type == "MESH"]:
        mod = ob.modifiers.new(name="GibiDecimate", type="DECIMATE")
        mod.decimate_type = "COLLAPSE"
        mod.ratio = ratio
        mod.use_collapse_triangulate = True
        bpy.context.view_layer.objects.active = ob
        bpy.ops.object.modifier_apply(modifier=mod.name)

    return {"before": before, "after": triangle_count(), "ratio": ratio, "applied": True}


def export_glb(out: Path):
    out.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=str(out),
        export_format="GLB",
        export_yup=True,              # section 6.1: +Y up
        export_apply=True,
        export_animations=True,
        export_skins=True,
        export_morph=True,
        export_cameras=False,         # section 6.1: no cameras
        export_lights=False,          # section 6.1: no lights
    )


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("--input", required=True)
    ap.add_argument("--profile", required=True)
    ap.add_argument("--output", required=True)
    args = ap.parse_args(argv)

    src = Path(args.input)
    profile = json.loads(Path(args.profile).read_text())
    outdir = Path(args.output)

    provenance = {
        "validatorVersion": "1.0.0",
        "blenderVersion": bpy.app.version_string,
        "profileId": profile["profileId"],
        "sourceFile": src.name,
        "sourceSha256": sha256_file(src),
    }

    reset_scene()
    import_glb(src)

    provenance["trianglesOnImport"] = triangle_count()
    provenance["scaleCurvesRemoved"] = strip_scale_curves()
    provenance["rootForcedIdentity"] = force_root_identity()
    provenance["boneRetarget"] = rename_bones(profile["boneMap"])
    provenance["clipRetarget"] = retarget_clips(profile["clipMap"], profile["clipsToDrop"])

    lods = {}
    for name, target, transition in LOD_TARGETS:
        if name != "LOD0":
            reset_scene(); import_glb(src)
            strip_scale_curves(); force_root_identity()
            rename_bones(profile["boneMap"])
            retarget_clips(profile["clipMap"], profile["clipsToDrop"])
        stats = decimate_to(target)
        out = outdir / f"{src.stem}_{name}.glb"
        export_glb(out)
        stats["screenTransition"] = transition
        stats["outputSha256"] = sha256_file(out)
        lods[name] = stats

    provenance["lods"] = lods
    provenance["clipsStillToAuthor"] = profile["clipsToAuthor"]

    (outdir / "provenance.json").write_text(json.dumps(provenance, indent=2))
    print(json.dumps(provenance, indent=2))


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    main(argv)
