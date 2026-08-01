# GibiWorld

Mobile AR pet companion, spatial game platform, and secure Pawsome3D asset runtime.

**The architecture specification is binding.** `GibiWorld_Architecture_Specification.pdf`
(GW-ARCH-001 v1.0.0) governs this repository. Every statement containing SHALL, MUST,
MUST NOT, REQUIRED, or EXACTLY is normative. Deviations require a written ADR in
`docs/adr/` and a new specification version — not a code comment.

## Start here

- **`CHECKLIST.md`** — build progress, findings on the supplied models, and what is needed next
- **`docs/TRACEABILITY.md`** — all 40 GW-* requirements bound to artifact and test

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
