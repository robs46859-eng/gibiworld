# GibiWorld — Build Checklist

Spec: **GW-ARCH-001 v1.0.0**, baseline 2026-07-31. The document is binding. Every
statement containing SHALL / MUST / MUST NOT / REQUIRED / EXACTLY is normative, and a
deviation requires a written ADR and a new document version.

Status: `[x]` done · `[~]` partial · `[ ]` not started · `[!]` blocked on you

---

## Phase 0 — Foundation

- [x] **Monorepo tree** at `/Users/robert/gibiworld`, structured on §3.1 deployable units and §19 required artifacts
- [x] **Git initialized**, `.gitignore` covering Unity/Node/Python plus a secrets denylist
- [x] **Source models migrated** to `assets/source-models/` (3 GLBs, originals in `FurryWorldAR` untouched)
- [x] **Contracts authored** — the single source of truth everything else generates from
  - [x] OpenAPI 3.1, all **13 endpoints** from §11, error envelope, all 12 error codes
  - [x] `pet-manifest.schema.json` (§6.1)
  - [x] `spatial-object.schema.json` (§5.3) — `LOCAL_SESSION` structurally barred from persistence
  - [x] `agility-course.schema.json` (§9.2) — 1 start + 1 finish + 1–20 obstacles
  - [x] `ai-intent.schema.json` (§8.2) — `additionalProperties:false` makes GW-AI-002 structural
- [x] **Unity baseline pinned exactly** to §0: Unity `6000.0.74f1`, NSDK `4.1.0`, AR Foundation `6.4.1`, glTFast `6.16.1`, Addressables `1.22.3`
- [x] **Assembly graph** — 11 asmdefs per §4, inward-only references, `Gibi.Core` with zero dependencies, **cycle check passes**

## Phase 1 — Deterministic core

- [x] `GeoPosition` — float64 WGS84, structurally cannot be assigned into a Unity `Vector3` (§5.1)
- [x] `AnchorLocalPose` — validating factory; rejects NaN, zero, denormalized > 1e-4, and > 75 m from anchor (GW-AR-005)
- [x] `MonotonicClock` — `Stopwatch`-backed; wall-clock is absent from the ranked scoring path (GW-GAME-006)
- [x] `GibiId` — opaque prefixed ULIDs; detects sequential DB ids crossing the boundary (§10.2)
- [x] `AnchorEligibility` — 6-state machine, 1.0 s tracked dwell, 250 ms degrade, 3.0 s invalidation (GW-AR-002/003/004)
- [x] `SurfaceAcceptance` — hazard set **fails closed**: any unrecognized tag is treated as hazardous (GW-AR-006/007)
- [x] `AssetVerifier` — §6.4 steps 1–5 with constant-time digest compare and atomic digest-keyed promotion
- [x] `AssetLimits` — §6.2 budgets re-enforced client-side after parse (GW-ASSET-005)

## Phase 2 — Simulation and gameplay

- [x] `BehaviorArbiter` — 6 priority tiers at 10 Hz; safety bypasses tick cadence entirely (GW-GAME-001)
- [x] `DeterministicMotion` — 50 Hz fixed step; **no overload accepts `Time.deltaTime`**, so frame-rate coupling is a compile error (GW-GAME-002)
- [x] `RigLimits` — foot IK 0.18 m / 25°, head yaw 50° / pitch 30°, damped look-at that cannot snap (§6.3)
- [x] `TrainingStateMachine` — 6 states, kind timeouts only; no failure state exists (§9.1)
- [x] `GateCrossing` — swept-volume intersection with aperture and order checks (GW-GAME-003)
- [x] `PlayerSafetyGate` — 4.5 m/s sustained 10 s → passenger-safe, with hysteresis (§13.3)
- [x] **27 EditMode tests** written, each named for the GW-* requirement it discharges
- [ ] PlayMode tests and recorded AR playback fixtures
- [ ] Scene validator enforcing exactly one ARSession + XR Origin (GW-AR-001)

## Phase 3 — Asset pipeline

- [x] `glb_inspect.py` — structural validator, no Blender dependency, runs in CI
- [x] **Ran against all 3 supplied models** — see findings below
- [x] `pawsome_to_gibi_quadruped_v1.json` — retarget profile achieving **26/26 joint coverage**
- [x] `blender_remediate.py` — headless `--factory-startup` worker: strips scale curves, forces root identity, retargets bones and clips, generates LOD0–3, emits provenance
- [ ] Ed25519 signing service + pinned key distribution
- [ ] Malicious/boundary GLB fixture matrix (external URI, oversized, tampered digest)
- [!] **Blender MCP addon not installed** — see "What I need from you"

## Phase 4 — Backend and data

- [x] Migration `0001` — users, pet_assets, pet_entitlements, pets, pet_state
- [x] Migration `0002` — memories, sites (PostGIS + GIST), courses, runs, ledger, outbox, audit
- [x] **§0 age gate enforced in the schema** — under-13 rows cannot be `ACTIVE`
- [x] **Course immutability as a trigger**, not a convention (GW-GAME-007)
- [x] **Negative-balance rejection as a constraint trigger** (GW-API-006)
- [x] **Audit log physically append-only** via rewrite rules (§13.1)
- [ ] TypeScript service implementations (9 units scaffolded, not written)
- [ ] PawsomeAdapter deployment-only mapping + HMAC rotation runbook

## Phase 5 — Release gates

- [x] `docs/TRACEABILITY.md` — all **40 GW-* requirements** bound to artifact + test
- [ ] ADR-001 … ADR-007 (§20, required before code freeze)
- [ ] Threat model, privacy data-flow map, app-store privacy declarations
- [ ] SLO dashboards and alerts (§15)
- [ ] CI: format, compile, static analysis, secret scan, dependency audit, tests, OpenAPI compat, migration test

**Current traceability: 16 IMPL · 19 PART · 10 TODO (of 40 requirements)**

---

## Findings on your supplied models

Measured in **Blender 5.2 headless**. Raw data in `tools/gw-asset-worker/`.
Full breakdown of all nine assets: **`docs/ASSET_INVENTORY.md`**.

### Correction to an earlier finding

I previously reported a stray `Icosphere` "Tripo bounding proxy" inflating bounds to
2.0 m. **That was wrong.** It is Blender's own glTF importer creating a custom bone
*display* shape, parked in the `glTF_not_exported` collection. It is not in your asset,
not in the scene, and never exported. The inspector now ignores it, and the real pet
bounds — ~1.0 × 0.38 × 0.59 m — were always fine.

### Two systemic issues (see inventory for detail)

1. **Everything is normalized to a ~1 m cube.** Tripo never authored real-world scale.
   Your `toyball` is a **one-metre ball**; `startgate` is 0.42 m tall against a §9.2
   corridor minimum of 1.5 m. Every prop needs an explicit rescale.
2. **Rigid skin binding** — `randy11` binds *one* bone per vertex (§6.2 permits 4).
   Passes every automated gate; joints will still crease. Re-skinning is hand work.

### Remediation run — `randy11`, all four LODs

| LOD | Triangles | Limit | Result |
|---|---|---|---|
| LOD0 | 34,929 | 35,000 | **PASS** |
| LOD1 | 17,964 | 18,000 | **PASS** |
| LOD2 | 7,485 | 7,500 | **PASS** |
| LOD3 | 1,996 | 2,000 | **PASS** |

Applied automatically and verified by independent re-inspection:

- **1,275 scale F-curves** removed in-scene, then **200 scale channels per LOD** stripped
  at container level — Blender's exporter re-emits them regardless, so the GLB itself
  must be rewritten (`glb_strip_scale.py`)
- **51 root-motion curves across 17 clips** removed — locomotion is now in-place per §6.3
- **18 bones** retargeted onto `GIBI_QUADRUPED_V1`, vertex groups renamed in step
- **6 out-of-scope clips dropped** (`pee_legLift`, `poop_squat`, `eat`, `eating`,
  `drink`, `drinking`) and duplicate clip targets resolved deterministically
- Provenance emitted with SHA-256 for source and every output

**Scale curves: 0. Root motion: 0. All LOD budgets met.**

### What remains — genuinely manual

| Task | Why automation can't do it |
|---|---|
| Re-skin to 4 influences | Cannot invent smooth weights without artefacts |
| Subdivide `spine` → `spine_01` + `spine_02` | Weight redistribution needs judgement |
| Insert `clavicle_l/r` | No shoulder bone exists in the source |
| Insert `hock_l/r` | No hock exists; correct dog gait is impossible without it |
| Author 6 P0 clips | `idle_b`, `sit_idle`, `stand`, `pickup`, `carry`, `drop` |
| Rescale all 5 props | Requires a real-world size decision per prop |

## What I need from you

1. **Install the Blender MCP addon.** Blender is running but no addon is installed, so
   `localhost:9876` refuses connections. In Blender: `Edit → Preferences → Add-ons →
   Install from Disk…` → select `/Users/robert/gibiworld/blender-mcp-main/addon.py` →
   enable **Interface: Blender MCP** → in the 3D viewport press `N` → **BlenderMCP** tab →
   **Start MCP Server**. The headless validator works without this; interactive
   inspection needs it.

2. **Install Unity `6000.0.74f1`.** Your machine has `6000.5.2f1`. You chose to honor the
   spec pin, so the client project targets `6000.0.74f1` and will not open in `6000.5.2f1`
   without a version prompt that breaks §16 reproducibility.

3. **Confirm Niantic Spatial SDK access.** NSDK `4.1.0` is pinned in `manifest.json` but
   needs credentials to resolve. Until then the spatial layer builds against its adapter
   interface only.

4. **Decide who authors the 15 missing clips** — in-house, contractor, or a revised
   `GIBI_QUADRUPED_V1` profile with a smaller launch clip set (the latter needs an ADR).

---

## Security note

`/Users/robert/` contains a file whose **name embeds what looks like a live ElevenLabs API
key** (`Arkham-       - ELEVENLABS-API-KEY sk_5f9e…`). Filenames leak through backups,
screenshots, and shell history. Worth rotating that key and renaming the file. It is
outside this repo and I have not touched it.
