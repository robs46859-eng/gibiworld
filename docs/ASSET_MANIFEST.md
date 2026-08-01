# GibiWorld Asset Production Manifest

Everything that must be modeled, rigged, or animated, derived from GW-ARCH-001
§6.2 (species and budgets), §6.3 (skeleton and clips), §9.2 (course objects),
and §18 (release phases).

Rules that apply to **every** item below:

- glTF 2.0 binary `.glb`, one scene, **meters**, **+Y up**, no external URI, no cameras or lights
- Materials: **max 3**, PBR metallic-roughness only
- Textures: base/normal ≤ 2048, other maps ≤ 1024, total decoded ≤ 48 MiB, KTX2 preferred
- Preview image: **1024×1024** square WebP or PNG, per asset
- Transfer size ≤ 45 MiB compressed

---

## 1. Pet models

Species allowlist is server-controlled. ADR-005 default is dog and cat primary, small
mammals gated behind a later phase.

| # | Species | Skeleton profile | Phase | Notes |
|---|---|---|---|---|
| 1 | `dog` — **preset** | `GIBI_QUADRUPED_V1` | **P0** | The one asset that must exist for the vertical slice |
| 2 | `dog` — Pawsome3D | `GIBI_QUADRUPED_V1` | **P0** | Your `randy11.glb`, remediated |
| 3 | `cat` — preset | `GIBI_QUADRUPED_V1` | P1 | |
| 4 | `rabbit` — preset | `GIBI_SMALL_MAMMAL_V1` | P4 | Profile does not exist yet — must be authored |
| 5 | `guinea_pig` — preset | `GIBI_SMALL_MAMMAL_V1` | P4 | |
| 6 | `ferret` — preset | `GIBI_SMALL_MAMMAL_V1` | P4 | |
| 7 | `miniature_pig` — preset | `GIBI_SMALL_MAMMAL_V1` | P4 | |

**Per-pet LOD chain — 4 meshes each, all mandatory:**

| LOD | Triangle budget | Vertex budget | Screen transition |
|---|---|---|---|
| LOD0 | ≤ 35,000 | ≤ 45,000 | — |
| LOD1 | ≤ 18,000 | — | 0.42 |
| LOD2 | ≤ 7,500 | — | 0.18 |
| LOD3 | ≤ 2,000 | — | 0.06 |

LOD1–3 can be auto-generated once by the worker; only LOD0 must be hand-authored.

**Rig constraints:** ≤ 2 skinned meshes, ≤ 4 weights per vertex, ≤ 96 deform bones,
≤ 12 morph targets (≤ 4 active at once), **no scale animation**.
Shoulder height 0.12–1.10 m, total bounds ≤ 2.0 m on any axis.

**Skeleton — exact, case-sensitive names required:**

```
root · pelvis · spine_01 · spine_02 · chest · neck · head          (required)
clavicle_l · upper_front_l · lower_front_l · paw_front_l           (required)
clavicle_r · upper_front_r · lower_front_r · paw_front_r           (required)
upper_rear_l · lower_rear_l · hock_l · paw_rear_l                  (required)
upper_rear_r · lower_rear_r · hock_r · paw_rear_r                  (required)
jaw · eye_l · eye_r                          (required where anatomy has them)
tail_01..tail_06                             (optional, contiguous, max 6)
ear_l_01..03 · ear_r_01..03                  (optional, max 3 each)
```

---

## 2. Animation clips — the critical path

23 clips per skeleton profile. Your current models supply 8 by mapping; **15 must be
authored.** All locomotion is **in-place** — the motion controller owns translation and yaw.
Sampling ≤ 30 fps after key reduction. Root transform identity within 1e-4.

### Already covered by remapping your existing clips (8)

| Clip ID | Loop | Duration | Source clip | Review needed |
|---|---|---|---|---|
| `idle_a` | loop | 2–6 s | `idle` | Verify duration in range |
| `walk` | loop in-place | 0.6–1.5 s | `walk` | **Confirm in-place** — strip root translation |
| `run` | loop in-place | 0.4–1.0 s | `run` | **Confirm in-place**; must read as 3.8 m/s |
| `sit` | mixed | 0.4–4.0 s | `sit` | |
| `sleep` | mixed | 0.5–8.0 s | `sleep` | |
| `greet` | mixed | 0.5–8.0 s | `play` | Semantic approximation — art review |
| `pet_react` | mixed | 0.5–8.0 s | `photo` | Semantic approximation — art review |
| `success` | mixed | 0.5–8.0 s | `bark_speak` | Semantic approximation — art review |

### Must be authored (15) — this is your schedule risk

| # | Clip ID | Loop | Duration | Purpose |
|---|---|---|---|---|
| 1 | `idle_b` | loop | 2–8 s | Secondary quiet motion, breaks idle repetition |
| 2 | `trot` | loop in-place | 0.5–1.2 s | 2.0 m/s reference gait |
| 3 | `turn_l_90` | non-loop | 0.25–0.8 s | Root rotation disabled at import |
| 4 | `turn_r_90` | non-loop | 0.25–0.8 s | Root rotation disabled at import |
| 5 | `sit_idle` | loop | 0.4–4.0 s | Hold after sit |
| 6 | `stand` | mixed | 0.4–4.0 s | Sit → stand transition |
| 7 | `down` | mixed | 0.4–4.0 s | Lie-down transition |
| 8 | `down_idle` | loop | 0.4–4.0 s | Hold while lying |
| 9 | `rise` | mixed | 0.4–4.0 s | Down → stand transition |
| 10 | `jump_takeoff` | mixed | 0.15–1.0 s | Course phase 1 |
| 11 | `jump_air` | mixed | 0.15–1.0 s | Course phase 2 |
| 12 | `jump_land` | mixed | 0.15–1.0 s | Course phase 3 |
| 13 | `pickup` | mixed | 0.2–2.0 s | Mouth socket interaction |
| 14 | `carry` | loop | 0.2–2.0 s | Fetch return |
| 15 | `drop` | mixed | 0.2–2.0 s | Mouth socket release |

### Must be deleted (6)

`pee_legLift`, `poop_squat`, `eat`, `eating`, `drink`, `drinking` — outside §1.2 scope
and absent from the §6.3 clip table. §1.2 explicitly excludes hunger mechanics.

**P0 minimum clip set:** `idle_a`, `walk`, `sit`, `pickup`, `carry`, `drop` — enough for
placement, direct cues, and fetch. The jump trio and `trot` are only needed once agility
courses land in P2.

---

## 3. Course obstacle props

Data-driven, instantiated as pooled prefabs or signed GLB props from the GibiWorld
catalog. A course payload may **never** name a Resources path or execute an animation event.

| # | Type | Phase | Constraints |
|---|---|---|---|
| 1 | `START_GATE` | P0 | Exactly one per course. Must define a gate plane with aperture width and height |
| 2 | `FINISH_GATE` | P0 | Exactly one per course |
| 3 | `JUMP_LOW` | P2 | Corridor ≥ 0.8 m wide, ≥ 1.5 m high |
| 4 | `JUMP_MED` | P2 | |
| 5 | `WEAVE` | P2 | |
| 6 | `TUNNEL` | P2 | Occlusion-aware; interior must not break depth |
| 7 | `PAUSE_TABLE` | P2 | Slope ≤ 7° for ranked placement |

Ranked courses hold 1–20 ordered obstacles between the two gates.

## 4. Toys and interaction objects

| # | Item | Phase | Notes |
|---|---|---|---|
| 1 | Fetch ball | **P0** | Required by the fetch loop and `pickup`/`carry`/`drop` |
| 2 | Rope toy | P1 | |
| 3 | Placement marker ring | **P0** | UI object — status by color **and** icon **and** label; color alone is insufficient |

---

## Totals

| Scope | Pet LOD0 meshes | Clips to author | Props |
|---|---|---|---|
| **P0 vertical slice** | 2 (preset dog + remediated Pawsome3D dog) | **6** | 3 |
| Through P2 beta | 3 (+ cat) | 15 | 10 |
| Through P4 expansion | 7 | 15 × 2 profiles = 30 | 10 |

**The honest read:** P0 needs 6 new clips and 3 simple props. That is a small, well-defined
art package. The 15-clip full set and the `GIBI_SMALL_MAMMAL_V1` profile are what turn this
into a real production schedule — and the small-mammal profile does not exist yet in the
spec, so it needs authoring plus an ADR before any P4 species work begins.
