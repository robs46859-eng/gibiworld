"""
Restore real-world scale to Tripo-normalized assets — GW-ARCH-001 sections 5.3, 6.2, 9.2.

    blender --background --factory-startup \
            --python blender_rescale.py -- \
            --profile tools/gw-asset-worker/profiles/real_world_scale.json \
            --src assets/source-models --out build/scaled

Tripo fits every generated asset into a ~1 m bounding cube, so authored scale carries no
information. World distances are stored in METRES (section 14) and every safety gate in
sections 5.3/9.2 is expressed in metres, so an unscaled asset is not merely cosmetic —
it silently defeats clearance, slope, and corridor validation.

Scale is applied UNIFORMLY and then baked into the mesh, so no non-unit object scale
reaches the runtime (section 6.3 requires root transforms to remain identity).
"""
import argparse, json, os, sys, hashlib
import bpy, mathutils


def sha256(p):
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for b in iter(lambda: f.read(1 << 20), b""):
            h.update(b)
    return h.hexdigest()


def artifact(o):
    return any(c.name == "glTF_not_exported" for c in o.users_collection)


def scene_bounds():
    mn = [1e9] * 3; mx = [-1e9] * 3
    for ob in bpy.data.objects:
        if ob.type != "MESH" or artifact(ob):
            continue
        for c in ob.bound_box:
            w = ob.matrix_world @ mathutils.Vector(c)
            for i in range(3):
                mn[i] = min(mn[i], w[i]); mx[i] = max(mx[i], w[i])
    return [round(mx[i] - mn[i], 4) for i in range(3)]


def apply_scale(factor):
    """Scale every top-level object, then bake the transform so object scale returns
    to 1.0 and the runtime sees identity roots."""
    roots = [o for o in bpy.data.objects if o.parent is None and not artifact(o)]
    for ob in roots:
        ob.scale = (ob.scale[0] * factor, ob.scale[1] * factor, ob.scale[2] * factor)
    bpy.context.view_layer.update()
    for ob in bpy.data.objects:
        if artifact(ob):
            continue
        ob.select_set(True)
    if roots:
        bpy.context.view_layer.objects.active = roots[0]
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)


def process(src, factor, out):
    bpy.ops.wm.read_homefile(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=src)
    before = scene_bounds()
    apply_scale(factor)
    after = scene_bounds()
    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_yup=True,
                              export_apply=True, export_animations=True,
                              export_skins=True, export_morph=True,
                              export_cameras=False, export_lights=False)
    residual = sorted({tuple(round(v, 5) for v in o.scale)
                       for o in bpy.data.objects if not artifact(o)})
    return {"boundsBefore": before, "boundsAfter": after,
            "scaleFactor": factor, "residualObjectScales": [list(r) for r in residual],
            "outputBytes": os.path.getsize(out), "outputSha256": sha256(out)}


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("--profile", required=True)
    ap.add_argument("--src", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--pet", default=None, help="optional path to an already-remediated pet GLB")
    a = ap.parse_args(argv)

    prof = json.load(open(a.profile))
    report = {"blender": bpy.app.version_string, "assets": {}}

    for fname, spec in prof["props"].items():
        srcp = os.path.join(a.src, fname)
        if not os.path.exists(srcp):
            report["assets"][fname] = {"error": "source not found"}
            continue
        outp = os.path.join(a.out, fname)
        r = process(srcp, spec["uniformScaleFactor"], outp)
        r["catalogType"] = spec["catalogType"]
        r["targetM"] = spec["targetM"]
        r["constrainAxis"] = spec["constrainAxis"]
        report["assets"][fname] = r

    if a.pet and os.path.exists(a.pet):
        f = prof["referenceDog"]["uniformScaleFactor"]
        outp = os.path.join(a.out, os.path.basename(a.pet))
        r = process(a.pet, f, outp)
        r["catalogType"] = "PET"
        report["assets"][os.path.basename(a.pet)] = r

    with open(os.path.join(a.out, "scale-report.json"), "w") as fh:
        json.dump(report, fh, indent=2)
    print("RESCALE_OK")


if __name__ == "__main__":
    main(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
