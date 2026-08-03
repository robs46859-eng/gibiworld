# GibiWorld — Asset Production Tracker

Art and asset-authoring work. Split out from `BUILD_GATES.md` because it runs on a
different clock, needs different people, and none of it is unblocked by writing code.

The **pipeline tooling** that processes these assets is a build gate and stays in
`BUILD_GATES.md` (B5). This file tracks the assets themselves.

**Spec:** GW-ARCH-001 §6.2, §6.3, §9.2 · **Last verified:** 2026-08-02

**Status key:** `PASS` done and verified · `PART` partially done · `OPEN` not started ·
`BLOCK` blocked on an owner decision

---

## At a glance

| Item | Status | Blocked on |
|---|---|---|
| `randy11` geometry + LODs | `PASS` | — |
| `randy11` skinning | `BLOCK` | Rigging hand work |
| `randy11` skeleton completeness | `BLOCK` | Rigging hand work |
| Animation clip set | `BLOCK` | Who authors them |
| Prop scale | `BLOCK` | One size decision per prop |
| Remaining species | `OPEN` | Launch allowlist decision |

---

## Done

Verified in Blender 5.2 headless, re-inspected independently after remediation.

- `PASS` — **All four LODs within budget**

  | LOD | Triangles | Limit | Result |
  |---|---|---|---|
  | LOD0 | 34,929 | 35,000 | PASS |
  | LOD1 | 17,964 | 18,000 | PASS |
  | LOD2 | 7,485 | 7,500 | PASS |
  | LOD3 | 1,996 | 2,000 | PASS |

- `PASS` — **1,275 scale F-curves removed** in-scene, then 200 scale channels per LOD
  stripped at container level. Blender's exporter re-emits them regardless, so the GLB
  itself must be rewritten (`glb_strip_scale.py`).
- `PASS` — **51 root-motion curves across 17 clips removed.** Locomotion is in-place per §6.3.
- `PASS` — **18 bones retargeted** onto `GIBI_QUADRUPED_V1`, vertex groups renamed in step;
  26/26 joint coverage.
- `PASS` — **6 out-of-scope clips dropped** (`pee_legLift`, `poop_squat`, `eat`, `eating`,
  `drink`, `drinking`); duplicate clip targets resolved deterministically.
- `PASS` — Provenance emitted with SHA-256 for source and every output.

**Net: scale curves 0, root motion 0, all LOD budgets met.**

---

## Blocked — rigging

Hand work. Automation cannot do these without artefacts.

- `BLOCK` — **Re-skin to 4 influences.** `randy11` binds *one* bone per vertex; §6.2 permits
  4. This passes every automated gate, which is exactly why it is dangerous — joints will
  crease visibly in play and no test will catch it.
- `BLOCK` — **Subdivide `spine` → `spine_01` + `spine_02`.** Weight redistribution needs
  judgement.
- `BLOCK` — **Insert `clavicle_l/r`.** No shoulder bone exists in the source.
- `BLOCK` — **Insert `hock_l/r`.** No hock exists. Correct dog gait is impossible without
  it — this is the one that will read as "wrong" to any player who has met a dog.

---

## Blocked — animation

- `BLOCK` — **15 clips unauthored.** Six are P0-critical: `idle_b`, `sit_idle`, `stand`,
  `pickup`, `carry`, `drop`.

  **Decision needed:** in-house, contractor, or a reduced launch clip set. The third option
  needs an ADR revising the `GIBI_QUADRUPED_V1` profile.

---

## Blocked — props

- `BLOCK` — **Rescale all 5 props.** Tripo normalised everything to a ~1 m cube; no
  real-world scale was ever authored.

  | Prop | Current | Problem |
  |---|---|---|
  | `toyball` | ~1 m | A one-metre ball |
  | `startgate` | 0.42 m | §9.2 corridor minimum is 1.5 m |
  | `weavepoles` | ~1 m cube | Unscaled |
  | `woodenramp` | ~1 m cube | Unscaled |
  | `playgroundladder` | ~1 m cube | Unscaled |

  **Decision needed:** one real-world size per prop. Everything else is mechanical.

---

## Not started

- `OPEN` — **Remaining species.** Only `randy11` has been through the pipeline. Scope
  depends on the launch allowlist decision.
- `OPEN` — **LOD verification for props** once rescaled.

---

## Reference

- Full nine-asset breakdown: `docs/ASSET_INVENTORY.md`
- Retarget profile: `tools/gw-asset-worker/profiles/pawsome_to_gibi_quadruped_v1.json`
- Remediation worker: `tools/gw-asset-worker/blender_remediate.py`
- Raw measurements: `tools/gw-asset-worker/`

### A correction worth remembering

An earlier report claimed a stray `Icosphere` "Tripo bounding proxy" was inflating bounds
to 2.0 m. **That was wrong.** It is Blender's own glTF importer creating a custom bone
*display* shape, parked in the `glTF_not_exported` collection. Not in the asset, not in the
scene, never exported. Real pet bounds — ~1.0 × 0.38 × 0.59 m — were always fine.
