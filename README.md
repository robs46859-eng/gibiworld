# GibiWorld

Mobile AR pet companion, spatial game platform, and secure Pawsome3D asset runtime.

**The architecture specification is binding.** `GibiWorld_Architecture_Specification.pdf`
(GW-ARCH-001 v1.0.0) and `docs/GW-ARCH-003-AR-Companion-Build-Specification.md` govern this repository.
Every statement containing SHALL, MUST, MUST NOT, REQUIRED, or EXACTLY is normative.
Deviations require a written ADR in `docs/adr/` and a new specification version — not a code comment.

## Current Status (2026-09-05)

Implementation milestones M0–M4 are authored and integrated in the repository:
- **Player-Controlled AR Fetch (W06, W07, W08)**: Trajectory preview, drag/tap aiming (`CompanionInputRouter`, `FetchAimView`), bounded 20 ms fixed-step parabolic solver (`ThrowSolver`), single transform ownership (`ToyController`), and state machine coordinator (`FetchSession`).
- **Traversable AR Dwelling (W10)**: Physically fitted doorway ($0.70\text{ m W} \times 0.90\text{ m H}$) and interior ($1.30\text{ m W} \times 1.50\text{ m D} \times 1.00\text{ m H}$) (`DwellingDefinition`), real visible entry/occupancy state machine (`DwellingInteraction`), and removal of renderer concealment shortcuts.
- **AI Pet Cues & Concurrency Guards (W09)**: Player cues (`Fetch`, `Come`, `Sit`, `Home`, `Pet`, `Pause`), monotonic generation/sequence `ActionToken` preventing stale callback clearing, and fatigue manner adaptation without denying legal repeat actions.
- **Bounded Local Navigation (W04)**: Deterministic 2D grid A* with swept capsule corridors and diagonal corner-cut rejection (`LocalGridNavigation`).
- **Production Build Guard & Scene Validation (W01)**: Package lock verification, direct ARCore enforcement, and production scene validation with `SandboxDemoDirector` disabled (`BuildGuard`, `SceneBuilder`, `SceneValidator`).
- **Production Services, Migration & Outbox (W15, W16)**: Forward migration `0004_companion_dwellings_sessions_events.sql` correcting asset version uniqueness and adding companion tables, OpenAPI 3.1 companion endpoints (`contracts/openapi/gibiworld.v1.yaml`), and persistent outbox (`OfflineOutbox`).
- **Model Supplement Track (W17)**: `NullIntentSource` zero-overhead fallback and `IntentEnvelopeValidator` schema/catalog/expiration checks.
- **Traceability & Tests**: All 39 requirements tracked by ID in `docs/TRACEABILITY.md` with corresponding EditMode acceptance test fixtures.

## Start here

- **[`docs/GW-ARCH-003-AR-Companion-Build-Specification.md`](docs/GW-ARCH-003-AR-Companion-Build-Specification.md)** — completion architecture for the AI GibiPet, functional dwelling, and player-controlled AR fetch, including asset specifications, service contracts, implementation backlog, and acceptance gates
- **[`HANDOFF.md`](HANDOFF.md)** and **[Gemini Spark implementation prompt](docs/GEMINI_SPARK_IMPLEMENTATION_PROMPT.md)** — current implementation handoff, baseline findings, and instructions
- **`CHECKLIST.md`** — build progress, findings on the supplied models, and what is needed next
- **`docs/TRACEABILITY.md`** — all 39 GW-ARCH-003 requirements and 40 GW-* requirements bound to artifact and test

## Layout

```
clients/gw-mobile      Unity IL2CPP client, pinned to Unity 6000.0.74f1 (§0)
contracts/             OpenAPI 3.1 + JSON Schemas — the single source of truth (§11)
services/              9 stateless TypeScript units (§3.1)
tools/gw-asset-worker  Blender/GLB validator and remediation worker (§6, §19)
db/migrations          PostgreSQL + PostGIS, forward-only (§10)
infra/                 OpenTofu, dashboards, runbooks (§10.1, §15)
docs/                  ADRs, security, privacy, accessibility, ops (§19, §20)
assets/source-models   Supplied Pawsome3D GLBs, unmodified
```

## Non-negotiables

These are the constraints most likely to be violated by well-intentioned changes:

- **Never** call a provider SDK outside its named adapter. Cyclic assembly references are a build failure. (§4)
- **Never** persist a world position computed from a device session. Store poses anchor-local. (§5.1)
- **Never** render a pet that has not passed all eight verification steps. Presets use the same verifier as Pawsome3D assets. (§6.4)
- **Never** let AI output name an animation, coordinate, force, price, score, or moderation result. Intents come from a server-published enum. (§8.2)
- **Never** couple locomotion distance to render frame rate. Motion runs at 50 Hz fixed. (§4.2)
- **Never** trust client wall-clock for ranking. Use server `startEpochMs` plus monotonic deltas. (§9.2)

## Package versions are frozen

`Packages/manifest.json` and `packages-lock.json` are pinned deliberately. An upgrade is a
release project requiring an ADR, dependency diff, recorded AR playback regression, asset
fixture validation, device matrix pass, and staged rollout — not a dependency bump. (§0, Appendix A)
