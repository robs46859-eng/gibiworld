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

All three GLBs share one rig (25 deform bones, `Khronos glTF Blender I/O v5.1.20`) and
**fail the spec identically**. They are close — every failure is mechanical, none is structural.

| Check | Result |
|---|---|
| Transfer size | **Pass** — ~2 MB against a 45 MiB limit |
| Materials / skins / bones | **Pass** — 1 / 1 / 25 against limits of 3 / 2 / 96 |
| External URIs, cameras, lights | **Pass** — none present |
| LOD0 triangles | **Fail** — 39,946 / 39,996 / 39,998 against a 35,000 limit (~14% over) |
| Scale curves | **Fail** — every clip animates scale; §6.3 forbids this outright |
| Skeleton profile | **Fail** — 20 of 24 required joints missing; rig uses `back_leg_lower.L`, spec requires `lower_rear_l` |
| Clip inventory | **Fail** — `randy11` has 4 of 23 required clips; the Pawsome3D pair has 0 of 23 |
| Out-of-scope clips | **Fail** — `pee_legLift`, `poop_squat`, `eat`, `drink` are outside §1.2 and absent from the §6.3 table |

**Remediation path, already encoded in the profile:** decimate ~14%, strip scale curves,
insert a synthetic `root` above `pelvis`, apply the 30-entry bone map, rename 11 clips,
drop 6 out-of-scope clips, and author the **15 missing clips** (`idle_b`, `trot`,
`turn_l_90/r_90`, `sit_idle`, `stand`, `down`, `down_idle`, `rise`, `jump_takeoff/air/land`,
`pickup`, `carry`, `drop`).

That last item is the real schedule risk: **15 new animation clips is art work, not code.**
Everything else `blender_remediate.py` does automatically.

---

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
