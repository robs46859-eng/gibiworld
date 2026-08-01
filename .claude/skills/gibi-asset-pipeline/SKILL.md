---
name: gibi-asset-pipeline
description: Inspect, remediate, and rescale GibiWorld pet and prop GLBs against GW-ARCH-001 §6.2/§6.3/§9.2. Use when new models are added to assets/source-models, when asked to validate/check/fix a model, when a model fails a budget, or when props need real-world scale.
---

# GibiWorld asset pipeline

All source models are **Tripo3D**-generated, which drives two rules that override
intuition.

## Rule 1 — always run Blender headless

```
blender --background --factory-startup --python <script> -- <args>
```

Blender 5.2 **segfaults** importing a multi-action GLB while a Dopesheet or Timeline is
visible (`animrig::rebuild_slot_user_cache`). Headless has no UI to redraw. It is also
what §19's isolated-worker model requires, so this is correct regardless.

## Rule 2 — nothing has real scale until you give it one

Tripo normalises every asset into a **~1 m bounding cube**. Authored dimensions carry no
information. This silently defeats every metre-denominated gate in §5.3 and §9.2 — a
1-metre fetch ball and a 0.42 m start gate both pass a naive import. **Never trust a
dimension you have not rescaled.**

## Workflow

### Inspect

```bash
cd /Users/robert/gibiworld
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup \
  --python tools/gw-asset-worker/validator/blender_inspect.py \
  -- --src assets/source-models --out build/inspection.json
```

Reports triangles, bones, clips, scale curves, root motion, and §6.2 violations.
The container-only parser `glb_inspect.py` runs in CI without Blender, but sees less.

**Two things only Blender reveals:**

- **Max influences per vertex.** `randy11` binds **1** bone per vertex. That passes
  §6.2's limit of 4, so no gate flags it — but joints will crease. Re-skinning is hand
  work and is the largest quality gap against the "Present" pillar.
- **Root motion.** Every source clip translates `hips`. §6.3 requires in-place
  locomotion. The container parser missed this because it looked for a node named `root`.

**Ignore the `Icosphere`.** Blender's glTF importer fabricates a bone display shape and
parks it in the `glTF_not_exported` collection. It is not in the asset and never exports.
Filter by `users_collection`, or triangle counts and bounds come out wrong.

### Remediate a pet

```bash
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup \
  --python tools/gw-asset-worker/validator/blender_remediate.py \
  -- --input assets/source-models/<pet>.glb \
     --profile tools/gw-asset-worker/profiles/pawsome_to_gibi_quadruped_v1.json \
     --output build/<pet>
python3 tools/gw-asset-worker/validator/glb_strip_scale.py build/<pet>/*.glb
```

The strip step is **not optional**. Blender's exporter re-emits scale tracks even after
every scale F-curve is deleted in-scene, so the container itself must be rewritten. §6.3
forbids scale curves outright.

### Rescale props

```bash
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup \
  --python tools/gw-asset-worker/validator/blender_rescale.py \
  -- --profile tools/gw-asset-worker/profiles/real_world_scale.json \
     --src assets/source-models --out build/scaled
```

**Everything scales from the dog.** Real agility equipment is defined as a function of
shoulder height, so the pet is the reference and props derive from it. Reference:
**0.50 m at the shoulder**, medium/border-collie class. Equipment dimensions come from
NADAC Equipment Specifications (2013-01-15): weave poles 0.610 m centre-to-centre,
tunnel 0.610 m diameter, jumps 1.219–1.524 m wide, A-frame apex 1.422–1.524 m,
dog walk 1.168–1.270 m.

Scale is applied uniformly then **baked to identity**, so no non-unit object scale reaches
the runtime (§6.3 requires identity roots).

## Verify, always

Re-inspect the output. Do not trust the remediation report alone:

```bash
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup \
  --python tools/gw-asset-worker/validator/blender_inspect.py \
  -- --src build/<pet> --out /tmp/after.json
```

Budgets: LOD0 ≤ 35,000 · LOD1 ≤ 18,000 · LOD2 ≤ 7,500 · LOD3 ≤ 2,000 triangles.
Scale curves must be **0**. Root motion must be **0**.

## Identify props by rendering them, not by filename

Filenames mislead. Renders corrected three of my own guesses: `startgate` is multi-lane
racing starting stalls, `playgroundladder` is an A-frame, and `dog-obstacle-course` is
weave poles + dog walk + A-frame baked into one mesh. Use `BLENDER_WORKBENCH` for
headless renders — EEVEE needs a GPU context macOS won't provide in background mode.
