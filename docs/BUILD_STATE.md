# GibiWorld — Current Build State

**Date:** 2026-08-02 · **Phase:** P0 (local placement only, §18) · **Spec:** GW-ARCH-001

---

## Short summary

The client is a Unity 6 AR Foundation app whose ARWorld scene is generated from code, not
hand-authored. Today's 20 commits took it from "black screen" to a build that starts a
session, detects and draws planes, shows a reticle, and places a signed, Ed25519-verified
pet GLB on a tapped floor — with placement gated by a deterministic, unit-testable
eligibility state machine.

The Niantic Spatial SDK is installed and is the **active Android XR loader**, but no C#
calls it: VPS, Lightship semantic segmentation, and Lightship meshing are all dormant.
Google Maps Platform is not present at all. Everything spatial today is AR Foundation over
ARCore.

Most of today's commits were the same class of bug: **figures governing course publication
were being applied to casual pet placement**, making placement impossible indoors. That
pattern is now named in three separate code comments.

---

## 1. Platform and package baseline

| Item | Value | Notes |
|---|---|---|
| Unity | `6000.0.74f1` | §0 pin; local machine has `6000.5.2f1` — **blocker** |
| AR Foundation / ARCore / ARKit | `6.4.2` | ADR-008 supersedes the §0 figure of 6.4.1 |
| Niantic Spatial SDK | `4.1.0-26051913` (git tag) | Installed; **needs credentials to resolve** |
| Render pipeline | URP `17.0.4` | AR background renderer feature configured |
| Input | Input System only | Not "Both" — required for AR correctness |

Package versions are frozen by §0; an upgrade requires an ADR.

## 2. Assembly graph

Eleven asmdefs, inward-only references, `Gibi.Core` with zero dependencies.

```
Core ← Spatial ← Gameplay ← UI
  ↖ AssetRuntime   ↖ Pets
     AI · Networking · Telemetry · Editor · Tests
```

**The load-bearing rule:** `Gibi.Spatial` is the *only* assembly that may reference
AR Foundation. Gameplay reaches surfaces through `ISurfaceProbe`, which is why every
placement gate runs in EditMode against `FakeSurfaceProbe` with no device attached.

## 3. Scene construction

Scenes are built by `Gibi.Editor.SceneBuilder` (menu, or `-executeMethod ... BuildAllForCI`)
so builds are reproducible from a signed commit per §16.

- **Bootstrap** — service container, lifecycle, persistent UI root, the single
  `AudioListener`. No AR trackables; the session must not start before safety gates exist.
- **ARWorld** — exactly one `ARSession`, exactly one `XROrigin` (Floor mode). Camera carries
  `ARCameraManager` / `ARCameraBackground` / `AROcclusionManager`. Origin carries
  `ARPlaneManager` (+ generated translucent visualizer prefab), `ARRaycastManager`,
  `ARAnchorManager`; `ARMeshManager` sits as a *child*. Then `ARSessionDriver`,
  `ARSurfaceProbe`, `PlacementController`, `PlacementRing`, `TapToPlace`, `P0SessionDriver`.
- **PetSandbox** — `PetRoot`, authored primitive colliders (mesh colliders forbidden, §6.3),
  spatial audio emitter (0.5–8 m, §7), effects pool, and the root where the verified GLB
  is instantiated.

## 4. Spatial pipeline

**Session → eligibility.** `ARSessionDriver` reads the provider frame and feeds
`AnchorEligibility`, a pure state machine over `Unavailable → Scanning → LocalReady →
VpsLimited → VpsTracked → Degraded`. Thresholds: 1.0 s tracked dwell, 250 ms degrade grace,
3.0 s run invalidation, 0.35 m pose-jump threshold. Scoring authority derives from state
only — never from device VPS state.

**Finding surfaces.** `ARSurfaceProbe` raycasts with `PlaneWithinPolygon | Depth`.

**Classifying them.** ARCore's plane classification flags map onto the spec's `SemanticTag`:

- `Floor` → `Floor`
- `WallFace` / `Ceiling` → `Unknown`
- Table / Seat / Other / unclassified, if `HorizontalUp` → `Indoor`
- everything else → `Unknown`

`SurfaceAcceptance` then fails closed: anything outside the safe allowlist is a hazard.

**Gates, in order:** motion (4.5 m/s) → anchor state → surface hit → camera start volume
(0.7 m pets / 1.5 m courses) → lighting confidence → hazard → slope (12° pets / 7° ranked)
→ clearance radius (0.45 m pets / 1.5 m courses) → clearance height. Rejections carry a
stable code, a localisation key, an icon ID, a colour, and a haptic — colour alone is
insufficient per §5.3.

## 5. Asset and safety layers

- **Asset runtime** — Ed25519 signature verification in-client, canonical compact-JSON
  manifest flattening, preset catalog. `randy11` is signed and loads on device.
- **Behaviour** — local-first with required periodic connectivity; deterministic motion;
  positive redirection policy; ephemeral inference only.
- **Consent** — age assurance and verifiable guardian consent; teens require no guardian
  consent (guardian watchlist removed).

---

## 6. Open gaps

| # | Gap | Impact |
|---|---|---|
| 1 | **Hazard classes unreachable.** `Sky`/`Person`/`Vehicle`/`Water`/`Road`/`Rail` are declared but no code path emits them — plane classification doesn't produce them. | §13.3's "no detected person/vehicle intersection" is **not enforced**. Needs semantic segmentation (Lightship, or ARKit people-occlusion stencil). |
| 2 | **`lightingConfidence` hardcoded to `1.0f`.** | The 0.35 gate can never fire. Lighting coaching comes only from `ARSession.notTrackingReason`. |
| 3 | **`clearanceHeightM` returns the spec minimum constant.** | Height gate never fires. Self-documented; waiting on mesh-based headroom. |
| 4 | **No iOS XR provider entry.** `XRGeneralSettingsPerBuildTarget` has Android + Standalone only. | An iOS build would not start a session. `NsdkARKitLoader` exists but is unassigned. |
| 5 | **NSDK unresolvable without credentials.** | Spatial layer builds against its adapter interface only. |
| 6 | **`TargetSiteAnchor` never assigned.** | VPS path dormant; state tops out at `LocalReady`, scoring at `PracticeOnly`, persistence always refused. Correct for P0, but untested. |
| 7 | **Unity version mismatch.** Machine has `6000.5.2f1`, project targets `6000.0.74f1`. | Opening prompts a version upgrade that breaks §16 reproducibility. |
| 8 | **15 animation clips unauthored.** | Needs an owner decision: in-house, contractor, or a smaller launch clip set via ADR. |

## 7. Housekeeping done 2026-08-02

- Untracked `clients/gw-mobile/.utmp/` — 111 files, 8.3 MB of CMake/ninja/`.o` Android build
  intermediates committed by accident. **Scanned: no credentials, keystores, or API keys.**
  They leaked machine-local absolute paths and NDK versions only. Now removed from the index
  and gitignored; the files remain on disk.

## 8. Still worth a look

- `clients/gw-mobile/Assets/XR/Settings 1/` — a duplicate-looking folder next to `Settings/`.
  Left alone pending your call.
- The ElevenLabs-key-in-a-filename issue flagged in `CHECKLIST.md` is outside this repo and
  still unrotated.
