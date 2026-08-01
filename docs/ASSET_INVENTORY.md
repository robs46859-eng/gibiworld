# GibiWorld Source Asset Inventory

Measured in **Blender 5.2.0 LTS, headless**, against GW-ARCH-001 §6.1–6.3 and §9.2.
Raw data: `tools/gw-asset-worker/blender-inspection-report.json`.

All nine assets are **Tripo3D**-generated. Two systemic properties follow from that and
affect every item below.

---

## The two systemic issues

### 1. Everything is normalized to a ~1 metre bounding cube

Tripo fits each generated asset into a unit cube. Real-world scale was never authored,
so **every asset needs an explicit rescale** before it means anything in AR. §5.3 and
§9.2 are written in metres, so this is not cosmetic.

| Asset | Largest axis now | Plausible real size | Factor |
|---|---|---|---|
| `toyball.glb` | 0.998 m | ~0.07 m | **×0.07** |
| `startgate.glb` | 0.998 m | ~1.6 m | ×1.60 |
| `endarch.glb` | 0.998 m | ~1.6 m | ×1.60 |
| `playgroundladder.glb` | 0.998 m | ~0.9 m | ×0.90 |
| `dog-obstacle-course.glb` | 0.998 m | ~1.2 m | ×1.20 |

A **one-metre fetch ball** is the clearest symptom. The pets are the exception — their
skinned meshes measure ~1.0 × 0.30–0.50 × 0.59–0.87 m, which is a plausible dog.

**Gate impact:** §9.2 requires a navigation corridor ≥ 0.8 m wide and ≥ 1.5 m high.
`startgate` is currently 0.42 m tall and `endarch` 0.63 m — a dog could not pass through
either at authored scale.

### 2. Rigid skin binding on the hero pet

`randy11` and the two Pawsome3D dogs bind **one bone per vertex**. §6.2 permits four.
This *passes* every automated gate, so nothing will ever flag it — but joints will
crease and tear under animation. For a spec whose first pillar is **Present**, this is
the single largest quality gap in the set. Re-skinning is hand work.

Notably `randylow.glb` has **maxInfluences = 4** — proper smooth weights.

---

## Pets (rigged)

| File | Tris | Bones | Clips | Skin | Verdict |
|---|---|---|---|---|---|
| `randy11.glb` | 39,996 | 25 | 17 | **1 infl.** | Hero candidate. 20/26 joints mappable |
| `1783286266224-…glb` | 39,998 | 25 | 17 | **1 infl.** | Same rig, same clips as randy11 |
| `1783217001710-…glb` | 39,946 | 25 | 6 | **1 infl.** | Same rig, fewer clips |
| `randylow.glb` | 4,872 | 21 | 1 | 4 infl. | **Different rig** — see below |

### `randylow.glb` — usable mesh, unusable rig

At 4,872 triangles it sits perfectly in the **LOD2** budget (≤ 7,500) and has proper
4-influence skinning. But its skeleton is Tripo's *generic auto-rig*:

```
tripo::Root, tripo::Spine_0, tripo::Head_0..3,
tripo::0_Left_Limb_0..2, tripo::1_Left_Limb_0..3, …, bone_9, bone_20
```

Nothing identifies which limb is front-left. **0 of 26** required joints are present and
the mapping is genuinely ambiguous, so this rig cannot be retargeted mechanically.

**Recommendation:** treat `randylow` as a *geometry* donor — use its mesh as LOD2,
re-bound to the corrected `randy11` skeleton. Discard its armature.

## Props (static)

All are single-mesh, single-material, 1024×1024, ~4,200–4,900 triangles. Well inside
every §6.2 budget. None is rigged, which is correct.

| File | Tris | Dims (m) | Maps to | Blocker |
|---|---|---|---|---|
| `startgate.glb` | 4,808 | 1.00 × 0.46 × 0.42 | `START_GATE` | Rescale ×1.6; needs a gate-plane definition |
| `endarch.glb` | 4,874 | 1.00 × 0.08 × 0.63 | `FINISH_GATE` | Rescale ×1.6; 8 cm depth is very thin |
| `playgroundladder.glb` | 4,188 | 0.34 × 1.00 × 0.54 | `WEAVE` or `JUMP_LOW` | Rescale; assign a catalog type |
| `toyball.glb` | 4,837 | 0.99 × 1.00 × 1.00 | Fetch toy | Rescale ×0.07. 4,837 tris for a ball is heavy |
| `dog-obstacle-course.glb` | 4,745 | 0.83 × 1.00 × 0.27 | — | **Composite.** §4.1 requires courses be data-driven from individually placed catalog props, not one baked mesh |

### `dog-obstacle-course.glb` needs splitting

§4.1: *"Course scene content SHALL be data-driven and instantiated as pooled prefabs or
signed GLB props from the GibiWorld catalog."* A single baked course mesh cannot be
placed, versioned, or safety-validated per obstacle, and §9.2 scoring needs a gate plane
per obstacle. It must be split into individual catalog props — or kept purely as a
layout reference.

---

## P0 coverage

| P0 requirement | Status |
|---|---|
| Preset dog | **Have** — `randy11`, pending remediation |
| Pawsome3D dog | **Have** — two candidates |
| `START_GATE` | **Have** — rescale + gate plane |
| `FINISH_GATE` | **Have** (`endarch`) — rescale + gate plane |
| Fetch ball | **Have** (`toyball`) — rescale, consider decimating |
| Placement marker ring | **Missing** — UI object, §5.3 needs colour + icon + label |
| 6 P0 clips | **Missing** — `idle_b`, `sit_idle`, `stand`, `pickup`, `carry`, `drop` |

Prop coverage went from 0 to 4 of 5. The remaining P0 gap is **6 animation clips, one
marker ring, and a re-skin.**
