"""
GibiWorld deep asset inspector — runs INSIDE Blender, headless.

    blender --background --factory-startup \
            --python tools/gw-asset-worker/validator/blender_inspect.py \
            -- --src assets/source-models --out report.json

Complements glb_inspect.py: that one parses the container without Blender and runs in
CI; this one measures what Blender actually builds — evaluated topology, real bone
hierarchy, per-vertex influence counts, and F-curve contents.

Runs headless deliberately. Blender 5.2 segfaults in
animrig::rebuild_slot_user_cache when a multi-action GLB is imported while a
Dopesheet/Timeline editor is visible; with no UI there is nothing to redraw.
"""
import bpy, mathutils, os, sys, json

REQUIRED = ["root","pelvis","spine_01","spine_02","chest","neck","head",
            "clavicle_l","upper_front_l","lower_front_l","paw_front_l",
            "clavicle_r","upper_front_r","lower_front_r","paw_front_r",
            "upper_rear_l","lower_rear_l","hock_l","paw_rear_l",
            "upper_rear_r","lower_rear_r","hock_r","paw_rear_r",
            "jaw","eye_l","eye_r"]

LIMITS = dict(lod0_tris=35000, lod0_verts=45000, materials=3, skinned=2,
              bones=96, morphs=12, clips=48, influences=4, bounds_axis=2.0)



def action_fcurves(action):
    """Blender 4.4+ replaced Action.fcurves with layered/slotted actions.
    Yield every F-curve regardless of which API this build exposes."""
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


def inspect(path):
    bpy.ops.wm.read_homefile(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=path)

    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    arms   = [o for o in bpy.data.objects if o.type == "ARMATURE"]

    per_mesh, total, skinned_n = [], 0, 0
    gmn = [1e9]*3; gmx = [-1e9]*3
    for ob in meshes:
        me = ob.data
        me.calc_loop_triangles()
        t = len(me.loop_triangles); total += t
        maxinf = 0
        for v in me.vertices:
            n = sum(1 for g in v.groups if g.weight > 0.0)
            if n > maxinf: maxinf = n
        is_skinned = any(m.type == "ARMATURE" for m in ob.modifiers)
        if is_skinned: skinned_n += 1
        bb = [ob.matrix_world @ mathutils.Vector(c) for c in ob.bound_box]
        mn = [min(p[i] for p in bb) for i in range(3)]
        mx = [max(p[i] for p in bb) for i in range(3)]
        for i in range(3):
            gmn[i] = min(gmn[i], mn[i]); gmx[i] = max(gmx[i], mx[i])
        per_mesh.append({
            "name": ob.name, "tris": t, "verts": len(me.vertices),
            "materials": [m.name if m else None for m in me.materials],
            "skinned": is_skinned, "maxInfluences": maxinf,
            "shapeKeys": len(me.shape_keys.key_blocks) if me.shape_keys else 0,
            "dims": [round(mx[i]-mn[i], 4) for i in range(3)],
        })

    bones = sorted(b.name for a in arms for b in a.data.bones)

    scale_clips, root_motion, clip_info = [], [], {}
    for a in bpy.data.actions:
        curves = list(action_fcurves(a))
        if any(fc.data_path.endswith("scale") for fc in curves):
            scale_clips.append(a.name)
        for fc in curves:
            if fc.data_path.endswith("location") and (
                    '"hips"' in fc.data_path or '"root"' in fc.data_path):
                root_motion.append(a.name); break
        fr = a.frame_range
        clip_info[a.name] = {
            "frames": round(fr[1] - fr[0], 1),
            "secAt24": round((fr[1] - fr[0]) / 24.0, 3),
            "fcurves": len(curves),
            "keyframes": sum(len(fc.keyframe_points) for fc in curves),
        }

    dims = [round(gmx[i] - gmn[i], 4) for i in range(3)]
    v = []
    if total > LIMITS["lod0_tris"]:
        v.append(f"LOD0_TRIANGLES {total} > {LIMITS['lod0_tris']}")
    if len(bpy.data.materials) > LIMITS["materials"]:
        v.append(f"MATERIAL_COUNT {len(bpy.data.materials)} > {LIMITS['materials']}")
    if skinned_n > LIMITS["skinned"]:
        v.append(f"SKINNED_MESH_COUNT {skinned_n} > {LIMITS['skinned']}")
    if len(bones) > LIMITS["bones"]:
        v.append(f"DEFORM_BONE_COUNT {len(bones)} > {LIMITS['bones']}")
    if len(bpy.data.actions) > LIMITS["clips"]:
        v.append(f"CLIP_COUNT {len(bpy.data.actions)} > {LIMITS['clips']}")
    if scale_clips:
        v.append(f"SCALE_CURVE_FORBIDDEN in {len(scale_clips)} clips")
    if max(dims) > LIMITS["bounds_axis"]:
        v.append(f"BOUNDS_AXIS {max(dims)} > {LIMITS['bounds_axis']} m")
    for m in per_mesh:
        if m["maxInfluences"] > LIMITS["influences"]:
            v.append(f"WEIGHTS_PER_VERTEX {m['name']} {m['maxInfluences']} > 4")
    missing = [b for b in REQUIRED if b not in bones]
    if missing:
        v.append(f"SKELETON_PROFILE missing {len(missing)}/{len(REQUIRED)} joints")

    return {
        "file": os.path.basename(path),
        "sizeBytes": os.path.getsize(path),
        "totalTriangles": total,
        "meshCount": len(meshes), "skinnedMeshCount": skinned_n,
        "meshes": per_mesh,
        "boundsXYZ_m": dims,
        "boneCount": len(bones), "bones": bones,
        "missingRequiredJoints": missing,
        "presentRequiredJoints": [b for b in REQUIRED if b in bones],
        "actionCount": len(bpy.data.actions),
        "clipsWithScaleCurves": sorted(scale_clips),
        "clipsWithRootMotion": sorted(set(root_motion)),
        "clips": clip_info,
        "materialCount": len(bpy.data.materials),
        "imageSizes": sorted({f"{i.size[0]}x{i.size[1]}" for i in bpy.data.images}),
        "violations": v,
    }


def main(argv):
    src = argv[argv.index("--src") + 1]
    out = argv[argv.index("--out") + 1]
    reports = [inspect(os.path.join(src, f))
               for f in sorted(os.listdir(src)) if f.lower().endswith(".glb")]
    payload = {"blender": bpy.app.version_string, "reports": reports}
    with open(out, "w") as fh:
        json.dump(payload, fh, indent=2)
    print("WROTE", out)


if __name__ == "__main__":
    main(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
