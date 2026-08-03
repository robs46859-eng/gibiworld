"""
Measure the entry aperture of a shelter prop and render inspection previews.

    blender --background --factory-startup --python measure_aperture.py -- \
            --src build/scaled/luxurydoghouse.glb --out tools/gw-asset-worker/previews

Why this exists: a SHELTER prop is the first catalog type whose usefulness depends on a
NEGATIVE space. Bounds tell you the box; they say nothing about whether a 0.54 m dog can
get through the door. GW-ARCH-002 requires the pet path to an entry marker, so the entry
has to be a measured number, not an assumption.

Method: cast rays along -Y (into the front face) on a grid. A ray that travels deeper than
the wall thickness before its first hit -- or misses entirely -- is inside the opening.
"""
import argparse, json, os, sys
import bpy, mathutils


def mesh_objects():
    return [o for o in bpy.data.objects if o.type == "MESH"
            and not any(c.name == "glTF_not_exported" for c in o.users_collection)]


def world_bounds():
    mn = [1e9] * 3; mx = [-1e9] * 3
    for ob in mesh_objects():
        for c in ob.bound_box:
            w = ob.matrix_world @ mathutils.Vector(c)
            for i in range(3):
                mn[i] = min(mn[i], w[i]); mx[i] = max(mx[i], w[i])
    return mn, mx


def measure(res=160):
    """
    Grid-sample the front face and find the doorway.

    A naive "first hit is far away -> opening" test FAILS on this asset class: a pitched
    roof with an overhang means rays high on the face sail over the wall entirely and
    strike the roof underside deep inside, which reads as a two-metre-wide door.

    So instead:
      1. Scan only the lower band, below the eaves, where the wall is vertical.
      2. Locate the wall plane from the MEDIAN first-hit depth -- the wall is by far the
         most common surface, so the median lands on it regardless of trim and detail.
      3. An opening is a cell whose first hit is materially behind that plane.
      4. Keep only the largest connected cluster, so window recesses and decorative
         inset panels do not inflate the result.
    """
    mn, mx = world_bounds()
    height = mx[2] - mn[2]
    start_y = mn[1] - 0.10
    direction = mathutils.Vector((0.0, 1.0, 0.0))
    deps = bpy.context.evaluated_depsgraph_get()

    # Step 1: the wall band. Above ~62% of height the roof takes over on a pitched shell.
    z_lo, z_hi = mn[2], mn[2] + height * 0.62

    depths = {}
    for ix in range(res):
        x = mn[0] + (mx[0] - mn[0]) * (ix + 0.5) / res
        for iz in range(res):
            z = z_lo + (z_hi - z_lo) * (iz + 0.5) / res
            hit, loc, _, _, _, _ = bpy.context.scene.ray_cast(
                deps, mathutils.Vector((x, start_y, z)), direction)
            if hit:
                depths[(ix, iz)] = loc.y - start_y

    if not depths:
        return None, mn, mx

    # Step 2: wall plane = median first-hit depth.
    ordered = sorted(depths.values())
    wall_depth = ordered[len(ordered) // 2]
    threshold = wall_depth + max(0.08, (mx[1] - mn[1]) * 0.12)

    candidates = {c for c, d in depths.items() if d > threshold}
    if not candidates:
        return None, mn, mx

    # Step 4: largest connected cluster (4-neighbour flood fill).
    best, seen = set(), set()
    for cell in candidates:
        if cell in seen:
            continue
        stack, comp = [cell], set()
        seen.add(cell)
        while stack:
            cx, cz = stack.pop()
            comp.add((cx, cz))
            for nb in ((cx + 1, cz), (cx - 1, cz), (cx, cz + 1), (cx, cz - 1)):
                if nb in candidates and nb not in seen:
                    seen.add(nb); stack.append(nb)
        if len(comp) > len(best):
            best = comp

    xs = [mn[0] + (mx[0] - mn[0]) * (ix + 0.5) / res for ix, _ in best]
    zs = [z_lo + (z_hi - z_lo) * (iz + 0.5) / res for _, iz in best]

    return {
        "apertureWidthM": round(max(xs) - min(xs), 4),
        "apertureHeightM": round(max(zs) - min(zs), 4),
        "apertureFloorM": round(min(zs), 4),
        "apertureTopM": round(max(zs), 4),
        "apertureCentreXM": round((max(xs) + min(xs)) / 2.0, 4),
        "wallPlaneDepthM": round(wall_depth, 4),
        "samplesInsideOpening": len(best),
        "clustersFound": None,
    }, mn, mx


def render(out_dir, name, mn, mx):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = 720
    scene.render.resolution_y = 720
    scene.render.film_transparent = False

    centre = mathutils.Vector([(mn[i] + mx[i]) / 2.0 for i in range(3)])
    span = max(mx[i] - mn[i] for i in range(3))

    cam_data = bpy.data.cameras.new("cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = span * 1.35
    cam = bpy.data.objects.new("cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam

    views = {
        "front": (mathutils.Vector((0, -1, 0)), (1.5708, 0, 0)),
        "threequarter": (mathutils.Vector((-0.8, -1, 0.55)).normalized(), (1.05, 0, -0.68)),
    }
    written = []
    for label, (offset, rot) in views.items():
        cam.location = centre + offset * span * 3.0
        cam.rotation_euler = rot
        path = os.path.join(out_dir, f"{name}_{label}.png")
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        written.append(path)
    return written


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", required=True)
    ap.add_argument("--out", required=True)
    a = ap.parse_args(argv)

    bpy.ops.wm.read_homefile(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=a.src)

    aperture, mn, mx = measure()
    os.makedirs(a.out, exist_ok=True)
    name = os.path.splitext(os.path.basename(a.src))[0]
    images = render(a.out, name, mn, mx)

    report = {
        "source": a.src,
        "boundsM": [round(mx[i] - mn[i], 4) for i in range(3)],
        "aperture": aperture,
        "previews": images,
    }
    print("APERTURE_JSON " + json.dumps(report))


if __name__ == "__main__":
    main(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
