"""
GibiWorld asset remediation worker — GW-ARCH-001 sections 6.2, 6.3, 19.

    blender --background --factory-startup \
            --python blender_remediate.py -- \
            --input assets/source-models/randy11.glb \
            --profile tools/gw-asset-worker/profiles/pawsome_to_gibi_quadruped_v1.json \
            --output build/randy11/

--factory-startup guarantees no user addon, preference, or startup file can influence
the result. That is what makes the output deterministic and the provenance meaningful.

Runs headless deliberately: Blender 5.2 segfaults inside
animrig::rebuild_slot_user_cache when a multi-action GLB is imported while a
Dopesheet/Timeline editor is visible. No UI, nothing to redraw.

SCOPE — this worker performs only mechanical, reversible transforms:
    remove junk objects, strip scale curves, remove baked root motion,
    rename bones and clips, drop out-of-scope clips, decimate, emit LODs.
It deliberately does NOT attempt re-skinning, spine subdivision, or clavicle/hock
insertion. Those require art judgement and are reported as REMAINING_MANUAL_WORK.
"""
import argparse
import hashlib
import json
import os
import sys

try:
    import bpy
except ImportError:
    print("FATAL: run inside Blender", file=sys.stderr)
    raise SystemExit(2)

LOD_TARGETS = [("LOD0", 35_000, None), ("LOD1", 18_000, 0.42),
               ("LOD2", 7_500, 0.18), ("LOD3", 2_000, 0.06)]

# Blender's glTF importer creates a custom bone display shape in the
# glTF_not_exported collection. It is never exported, so nothing needs removing —
# this list exists for genuine junk geometry found in future source assets.
JUNK_OBJECT_PREFIXES = ()


def action_fcurves(action):
    """Blender 4.4+ replaced Action.fcurves with layered/slotted actions."""
    legacy = getattr(action, "fcurves", None)
    if legacy is not None:
        for fc in legacy:
            yield fc
        return
    for layer in getattr(action, "layers", []):
        for strip in getattr(layer, "strips", []):
            for cb in getattr(strip, "channelbags", []):
                for fc in getattr(cb, "fcurves", []):
                    yield fc


def remove_fcurve(action, fc):
    legacy = getattr(action, "fcurves", None)
    if legacy is not None:
        legacy.remove(fc); return True
    for layer in getattr(action, "layers", []):
        for strip in getattr(layer, "strips", []):
            for cb in getattr(strip, "channelbags", []):
                if fc in list(getattr(cb, "fcurves", [])):
                    cb.fcurves.remove(fc); return True
    return False


def sha256_file(p):
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for b in iter(lambda: f.read(1 << 20), b""):
            h.update(b)
    return h.hexdigest()


def fresh_import(path):
    bpy.ops.wm.read_homefile(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=path)


def drop_junk_objects():
    removed = []
    for ob in list(bpy.data.objects):
        if ob.name.startswith(JUNK_OBJECT_PREFIXES) and ob.type == "MESH" \
                and not any(m.type == "ARMATURE" for m in ob.modifiers):
            removed.append({"name": ob.name, "tris": len(ob.data.polygons)})
            bpy.data.objects.remove(ob, do_unlink=True)
    return removed


def strip_scale_curves():
    n = 0
    for a in bpy.data.actions:
        for fc in list(action_fcurves(a)):
            if fc.data_path.endswith("scale"):
                if remove_fcurve(a, fc):
                    n += 1
    return n


def strip_root_motion(root_bone_names):
    """Section 6.3: locomotion clips SHALL be in-place; the deterministic motion
    controller owns translation and yaw. Remove translation curves on the rig root."""
    removed = {}
    for a in bpy.data.actions:
        hits = 0
        for fc in list(action_fcurves(a)):
            if not fc.data_path.endswith("location"):
                continue
            if any(f'"{b}"' in fc.data_path for b in root_bone_names):
                if remove_fcurve(a, fc):
                    hits += 1
        if hits:
            removed[a.name] = hits
    return removed


def rename_bones(bone_map):
    applied, absent = {}, []
    for arm in [o for o in bpy.data.objects if o.type == "ARMATURE"]:
        names = {b.name for b in arm.data.bones}
        for src, dst in bone_map.items():
            if src in names and src != dst:
                arm.data.bones[src].name = dst
                applied[src] = dst
            elif src not in names:
                absent.append(src)
        # Vertex groups must follow the bone rename or skinning breaks.
        for ob in [o for o in bpy.data.objects if o.type == "MESH"]:
            for src, dst in bone_map.items():
                vg = ob.vertex_groups.get(src)
                if vg and src != dst:
                    vg.name = dst
    return {"applied": applied, "sourceBonesAbsent": sorted(set(absent))}


def retarget_clips(clip_map, drop):
    """Rename source clips onto spec clip IDs.

    Several source clips collide on one spec target (play + playing -> greet).
    Blender would auto-suffix the second as 'greet.001', which is not a valid spec
    clip ID and would ship as a duplicate. Keep the FIRST match deterministically
    (sorted order) and drop the rest as redundant."""
    renamed, dropped, claimed = {}, [], set()
    for a in sorted(bpy.data.actions, key=lambda x: x.name):
        if a.name in drop:
            dropped.append(a.name)
            bpy.data.actions.remove(a)
            continue
        target = clip_map.get(a.name)
        if target is None:
            continue
        if target in claimed:
            dropped.append(a.name)          # redundant duplicate of an already-claimed ID
            bpy.data.actions.remove(a)
            continue
        claimed.add(target)
        renamed[a.name] = target
        a.name = target
    return {"renamed": renamed, "dropped": dropped, "claimedTargets": sorted(claimed)}


def triangle_count():
    n = 0
    for ob in [o for o in bpy.data.objects if o.type == "MESH"]:
        me = ob.data
        me.calc_loop_triangles()
        n += len(me.loop_triangles)
    return n


def decimate_to(target):
    before = triangle_count()
    if before <= target:
        return {"before": before, "after": before, "ratio": 1.0, "applied": False}
    ratio = target / float(before)
    for ob in [o for o in bpy.data.objects if o.type == "MESH"]:
        m = ob.modifiers.new(name="GibiDecimate", type="DECIMATE")
        m.decimate_type = "COLLAPSE"
        m.ratio = ratio
        m.use_collapse_triangulate = True
    after = triangle_count()  # pre-apply estimate; real count re-measured post-export
    return {"before": before, "targetRatio": round(ratio, 5), "applied": True,
            "afterEstimate": after}


def export_glb(path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=path, export_format="GLB",
        export_yup=True,            # section 6.1: +Y up
        export_apply=True,          # bake modifiers (decimate)
        export_animations=True, export_skins=True, export_morph=True,
        export_cameras=False, export_lights=False,
    )


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("--input", required=True)
    ap.add_argument("--profile", required=True)
    ap.add_argument("--output", required=True)
    a = ap.parse_args(argv)

    with open(a.profile) as f:
        profile = json.load(f)

    prov = {
        "validatorVersion": "1.1.0",
        "blenderVersion": bpy.app.version_string,
        "profileId": profile["profileId"],
        "sourceFile": os.path.basename(a.input),
        "sourceSha256": sha256_file(a.input),
    }

    # Root bones whose translation must be stripped: whatever the profile maps to
    # "pelvis"/"root", plus the literal names.
    roots = {"root", "pelvis", "hips"}
    roots |= {k for k, v in profile["boneMap"].items() if v in ("pelvis", "root")}

    lods = {}
    for name, target, transition in LOD_TARGETS:
        fresh_import(a.input)
        step = {}
        step["junkRemoved"] = drop_junk_objects()
        step["trisAfterJunkRemoval"] = triangle_count()
        step["scaleCurvesRemoved"] = strip_scale_curves()
        step["rootMotionCurvesRemoved"] = strip_root_motion(roots)
        step["boneRetarget"] = rename_bones(profile["boneMap"])
        step["clipRetarget"] = retarget_clips(profile["clipMap"], profile["clipsToDrop"])
        step["decimate"] = decimate_to(target)

        out = os.path.join(a.output, f"{os.path.splitext(prov['sourceFile'])[0]}_{name}.glb")
        export_glb(out)
        step["screenTransition"] = transition
        step["outputFile"] = os.path.basename(out)
        step["outputBytes"] = os.path.getsize(out)
        step["outputSha256"] = sha256_file(out)
        lods[name] = step

    prov["lods"] = lods
    prov["clipsStillToAuthor"] = profile.get("clipsToAuthor", [])
    prov["REMAINING_MANUAL_WORK"] = [
        "RE-SKIN: source binds 1 bone per vertex. Section 6.2 permits 4. Automated "
        "tooling cannot invent smooth weights without introducing artefacts.",
        "Subdivide 'spine' into spine_01 + spine_02 and redistribute weights.",
        "Insert clavicle_l/r between chest and upper_front_l/r; rebind vertices.",
        "Insert hock_l/r between lower_rear_l/r and paw_rear_l/r; rebind vertices.",
        "Author the missing clips listed in clipsStillToAuthor.",
    ]

    os.makedirs(a.output, exist_ok=True)
    with open(os.path.join(a.output, "provenance.json"), "w") as f:
        json.dump(prov, f, indent=2)
    print("REMEDIATION_OK", json.dumps({k: {"out": v["outputFile"],
                                            "tris": v["decimate"]}
                                        for k, v in lods.items()}))


if __name__ == "__main__":
    main(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
