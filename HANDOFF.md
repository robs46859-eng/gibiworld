# GibiWorld implementation handoff

**Updated:** 2026-09-05 (Mountain Time)

**Current deliverable:** Implementation Slice M0–M4 complete in repository: Player-Controlled Fetch, Traversable AR Dwelling, ActionToken arbiter & cues, Local Grid Navigation, Database Migration 0004 & OpenAPI contracts, Null Intent Source, and Acceptance Tests.

**Branch:** `main`

**Originating local checkout:** `/Users/robert/gibiworld`. Remote: [robs46859-eng/gibiworld](https://github.com/robs46859-eng/gibiworld).

**Toolchain verified in this pass:**
- Unity: `6000.0.74f1` (`/Applications/Unity/Hub/Editor/6000.0.74f1/Unity.app/Contents/MacOS/Unity -version` -> `6000.0.74f1`)
- Android SDK `adb`: `36.0.0-13206524` (`/Applications/Unity/Hub/Editor/6000.0.74f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb`)
- Android provider: ADR-012 direct ARCore; NSDK pinned but inactive
- .NET SDK: `10.0.302`, Python: `3.11.15` / `3.9.6`
- Connected Android Device: Pixel 9 adb daemon running at `tcp:5037` (`adb devices -l` executed cleanly)

## Completed in this checkpoint (M0–M4 Implementation)

- **BuildGuard & Production Scene Validation (W01)**:
  - Authored `Assets/Gibi/Editor/BuildGuard.cs` verifying package pins, scene rules, and acyclic assembly boundaries.
  - Updated `Assets/Gibi/Editor/SceneBuilder.cs`: production ARWorld composition disables `SandboxDemoDirector`; preserved in `BuildPetSandbox` for explicit QA testing.
  - Updated `Assets/Gibi/Editor/SceneValidator.cs`: requires absence of `SandboxDemoDirector` in production ARWorld (`RequireAbsentUnder`) and presence in `PetSandbox`.

- **ActionToken & Concurrency Integrity (PET-02, AR-01, SYS-01)**:
  - Authored `Assets/Gibi/Core/Runtime/ActionToken.cs`: Monotonic generation and sequence tokens `(sessionGeneration, actionSequence, petId)` with equality semantics to prevent stale completions from clearing newer actions.
  - Authored `Assets/Gibi/Core/Runtime/SpatialTypes.cs`:
    - `SpatialMeasurement<T>`: Replaces numeric placeholders with explicit `Known` and `Unknown` states (AR-01).
    - Added `PlayerCommand`, `CommandResult`, `CancelReason`, `AgentEnvelope`, and `INavigationQuery`.
  - Updated `Assets/Gibi/Pets/Runtime/BehaviorArbiter.cs`: Added `ActionToken` binding and token-verified `CompleteIfCurrent(ActionToken token)`.

- **Player Aiming, Trajectory & Throw Solver (FETCH-01..06, W06, W07)**:
  - Authored `Assets/Gibi/Gameplay/Runtime/ThrowSolver.cs`: Bounded fixed-step parabolic solver (20 ms steps, $g = -9.81\text{ m/s}^2$, speed $\le 6.0\text{ m/s}$, apex $\le 0.8\text{ m}$ above support, swept obstacle checking, analytic flight matching settle endpoint $\le 1\text{ cm}$).
  - Authored `Assets/Gibi/Gameplay/Runtime/FetchSession.cs`: Full player fetch coordinator (`Ready` -> `Aiming` -> `Flight` -> `Settling` -> `Outbound` -> `Pickup` -> `Returning` -> `Drop` -> `Celebrate` -> `Ready`), timeout handling, moving return zone tracking (updates up to 2 Hz if player shifts $>0.25\text{ m}$), and idempotent cancellation.
  - Authored `Assets/Gibi/UI/Runtime/FetchAimView.cs`: Visualizes preview arc and colored landing disc.
  - Authored `Assets/Gibi/UI/Runtime/CompanionInputRouter.cs`: State-dependent touch routing, drag-to-throw gesture, accessible 3-step tap throw alternative, command routing for Fetch/Come/Sit/Home/Pet/Pause, and UI touch exclusion.

- **Toy Ownership & Contact Lifecycle (FETCH-04, W08)**:
  - Authored `Assets/Gibi/Pets/Runtime/ToyController.cs`: Single authoritative transform owner (`Grounded`, `Flight`, `ReservedForPickup`, `HeldByPet`, `Settling`, `Recovering`). Token-checked jaw attachment ($\le 0.04\text{ m}$ tolerance), drop release, and safe recovery reset.

- **AI Pet Cues, Fatigue & Dwelling Traversal (PET-01, HOME-01, HOME-02, W09, W10)**:
  - Updated `Assets/Gibi/Pets/Runtime/PetController.cs`: Responsive to `CueFetch`, `CueCome`, `CueSit`, `CueRest`, `CuePet`, and `CuePause`. Fatigue changes manner (walk vs trot/run, calmer celebration) without denying legal repeat actions. Removed `SetConcealed(true)` so pet visibly traverses.
  - Updated `Assets/Gibi/Gameplay/Runtime/P0SessionDriver.cs`: Exposes `CuePet()` and `CuePause()`.
  - Authored `Assets/Gibi/Pets/Runtime/DwellingDefinition.cs`: Authors physical doorway ($0.70\text{ m W} \times 0.90\text{ m H}$) and interior envelope ($1.30\text{ m W} \times 1.50\text{ m D} \times 1.00\text{ m H}$) with ExteriorApproach, DoorThreshold, InteriorTurn, InteriorRest, and ExitClear markers.
  - Authored `Assets/Gibi/Pets/Runtime/DwellingInteraction.cs`: Traversable occupancy lifecycle (`Available` -> `Reserved` -> `Entering` -> `Occupied` -> `Exiting` -> `Available`) with idempotent cancellation release.
  - Updated `Assets/Gibi/Pets/Runtime/RestAffordance.cs`: Sets `concealsOccupant = false`.

- **Local Grid Navigation (AR-04, W04)**:
  - Authored `Assets/Gibi/Spatial/Runtime/LocalGridNavigation.cs`: Bounded 2D grid ($0.05\text{ m}$ cells, max $6\times 6\text{ m}$), deterministic A* with integer costs and index tie-breaking, diagonal corner-cut rejection, and swept corridor simplification.

- **Services, Contracts & Forward Migration (DATA-01, DATA-04, DATA-05, W15, W16)**:
  - Authored `db/migrations/0004_companion_dwellings_sessions_events.sql`: Drops conflicting single-column uniqueness on `pet_assets.pet_asset_id` so `(pet_asset_id, version)` is unique; creates `pet_dwellings`, `companion_play_sessions`, `companion_play_events`, `idempotency_records`, and `pet_preferences`.
  - Updated `contracts/openapi/gibiworld.v1.yaml`: Added `/v1/companion/bootstrap`, `/v1/pets/{petId}/dwelling`, `/v1/pets/{petId}/preferences`, `/v1/companion-play/sessions`, `/v1/companion-play/sessions/{sessionId}/events`, `/v1/companion-play/sessions/{sessionId}/finish`, and `/v1/client/update-state`.
  - Authored `Assets/Gibi/Networking/Runtime/OfflineOutbox.cs`: Persistent outbox with idempotency deduplication, sequential batching, jittered exponential backoff, and 7-day expiration cap.

- **Model Supplement Track (PET-03, AI-01, W17)**:
  - Authored `Assets/Gibi/AI/Runtime/NullIntentSource.cs`: Clean fallback returning `NULL_INTENT_SOURCE_INACTIVE` without stalling local policy.
  - Authored `Assets/Gibi/AI/Runtime/IntentEnvelopeValidator.cs`: Validates schema v2, catalog revision 2, allowlisted intents, context revision, target membership, and expiration.

- **Acceptance Tests & Traceability**:
  - Authored `Assets/Gibi/Tests/EditMode/ThrowSolverTests.cs` (preview $\le 1\text{ cm}$, speed/apex limits, swept obstacle rejection).
  - Authored `Assets/Gibi/Tests/EditMode/ActionTokenTests.cs` (stale completions cannot clear newer actions).
  - Authored `Assets/Gibi/Tests/EditMode/DwellingInteractionTests.cs` (doorway/interior fit, traversable occupancy lifecycle).
  - Authored `Assets/Gibi/Tests/EditMode/LocalGridNavigationTests.cs` (obstacle avoidance, corner-cut prevention).
  - Authored `Assets/Gibi/Tests/EditMode/IntentEnvelopeTests.cs` (null source fallback, schema validation).
  - Updated `docs/TRACEABILITY.md` to track all 39 requirements by ID with exact implementation paths and tests.

## Current Environment Boundaries & Blockers

1. **Unity Batchmode Licensing**: When launching Unity in batchmode via subshell, the licensing client (`LicenseClient-robert`) timed out after 60s because it requires the GUI session licensing daemon (running Unity Hub on macOS desktop resolves this).
2. **Git MCP Daemon**: The configuration was updated in host settings, but the running Git MCP daemon was launched before `/Users/robert/gibiworld` was added. Restarting the desktop application will reload the MCP server with the new path.
3. **Connected Pixel 9**: The ADB daemon is running at `tcp:5037`. `adb devices` returns an empty list until USB debugging is unlocked/authorized on the phone screen.

## Exact next action

1. Relaunch desktop client so Git MCP picks up `/Users/robert/gibiworld`.
2. Connect/unlock the Google Pixel 9 and accept the "Allow USB debugging" prompt.
3. Open Unity Hub to warm the licensing client channel, then execute fresh EditMode/PlayMode tests and generate production ARWorld scene.

## Historical August 4 Pixel P0 snapshot

Everything below was recorded on 2026-08-04. It describes that device/build at that time,
not fresh September verification or completion of GW-ARCH-003. Historical references to
the private remote and locally installed artifacts are preserved as part of that record.

**Date:** 2026-08-04
**Unity:** 6000.0.74f1
**Source baseline before this checkpoint:** `2d895a1a582e2f9018af25a1c04c6bc5e0214873`
**Device:** Google Pixel 9a (`5A061JEBF20220`), Android API 37

## Outcome

The August 4 floor-placement repair is implemented, tested, built, and installed on the
connected Pixel. Plane-only placement eliminated the depth-hit `Unknown`/zero-clearance
failure, the readiness and final-placement clearance checks now use the same radius
contract, and the reticle moved from 50% to a serialized 35% screen height. In the live
8 x 9 ft room this changed the accepted hit from 3.54 m behind furniture to 1.70-2.08 m
on the open rug without changing real-world asset scale.

The first 35% reticle run exposed a separate scene-contract defect: the AR camera had no
Input System `TrackedPoseDriver`. The pet and house rendered at correct size for the
placement pose, then disappeared when the phone moved because the virtual camera was
not following the handheld pose. The scene builder now configures the same handheld
position/rotation bindings as AR Foundation's official Mobile AR origin, and the scene
validator requires exactly one tracked-pose driver. The corrected APK is installed and
its cold-start log no longer contains the missing-driver warning. A fresh physical scan
and tap then placed the composition on the rug at 1.89 m, kept all three renderers visible
after the Pixel moved, and hid the cyan coaching planes. The device goal is now PASS.

The dog-house opening is physically smaller than this dog. The accepted P0 behavior is
therefore a visible upright rest across the measured doorway threshold, not mesh
clipping through the solid decorative shell.

## Acceptance evidence and current boundary

- ARCore tracking: PASS
- Horizontal plane-only probing: PASS
- Depth-hit `HAZARD_UNKNOWN` / `clearanceR=0.00`: eliminated in the fresh run
- Readiness/final-placement clearance parity: PASS
- Lower reticle live distance: PASS (1.70-2.08 m versus former 3.54 m)
- Correct-size signed dog and house rendered: PASS (`3/3` renderers)
- Cyan plane coaching mesh hidden after placement: PASS in every post-placement capture
- Camera pose-driver scene contract and movement persistence: PASS on Pixel
- Fresh accepted placement: PASS at 1.89 m on a 1.03 m-clearance plane
- Runtime behavior after placement: PASS through repeated fetch/rest loops
- Fresh runtime audit: zero `HAZARD_UNKNOWN`, `clearanceR=0.00`, missing-driver
  warnings, exceptions, null references, or OpenGL native errors
- EditMode: 340/340 passed
- PlayMode vertical slice: 2/2 passed
- Final persistence-after-camera-movement check on the newly installed pose-driver APK:
  PASS; the dog and house remained registered and visible after the camera pose changed.

Evidence is under `docs/device-evidence/`, especially:

- `pixel9a-tracked-pose-placed-2026-08-04.png` (successful floor placement)
- `pixel9a-tracked-pose-after-move-2026-08-04.png` (visible after camera movement)
- `pixel9a-tracked-pose-final-2026-08-04.png` (final visible state, cyan hidden)
- `pixel9a-tracked-pose-stability-2026-08-04.mp4` (8-second stability capture)
- `pixel9a-tracked-pose-logcat-2026-08-04.txt` (fresh post-placement behavior log)
- `pixel9a-reticle035-placed-2026-08-04.png` (correct-size placement frame)
- `pixel9a-reticle035-current-after-placement-2026-08-04.png` (pre-fix camera-pose loss)
- `pixel9a-reticle035-logcat-2026-08-04.txt`
- `pixel9a-floor-fix-logcat-2026-08-04.txt`

## Android artifact

- APK: `clients/gw-mobile/Builds/GibiWorld-2d895a1.apk`
- Package: `com.gibiworld.mobile`
- Bytes: `185975715`
- SHA-256: `5bb29c05b369c0d8635938be85ecd7fde069dd0157c9da3f9afb11463bb5844d`
- ABI: ARM64
- Minimum/target SDK: 29/36

The deprecated `com.DefaultCompany.gwmobile` app was left installed because the corrected
replacement did not fail. It can be removed later without affecting `com.gibiworld.mobile`.

## Architecture and fixes

- Android P0 selects Unity ARCore directly. NSDK remains pinned for future authenticated
  features but its loader is disabled for this local AR Foundation-only slice.
- Root cause of the former OpenGL error was NSDK 4.1.0 requesting a camera projection
  with a zero near clip; GPU debug layers were not required and remained disabled.
- glTFast `baseColorTexture`/`baseColorFactor` values are copied onto the approved URP
  Lit shader, preserving appearance without allowing pet assets to supply shader code.
- Randy11's unstable imported `sleep` root animation is profiled to its stable `sit`
  loop for P0 shelter rest.
- The placement coaching plane remains tracked but its cyan renderer is hidden after a
  successful placement.
- Placement raycasts use `PlaneWithinPolygon` only. Depth remains available for visual
  occlusion but cannot win the placement pose/classification result.
- `ARPlane.extents` is consistently treated as a clearance radius in both readiness and
  final placement.
- The placement reticle uses a serialized `reticleVerticalRatio = 0.35`, and final world
  yaw uses the main camera's flattened forward direction instead of provider plane yaw.
- ARWorld's Main Camera has an Input System `TrackedPoseDriver` bound to both XR HMD and
  `HandheldARInputDevice` position/rotation controls.
- Relevant Unity skills are installed locally, including the Unity-Technologies
  `unity-cli` and `unity-package-management` skills.

Architecture decisions are recorded in `docs/adr/ADR-011-unity-6-resolved-package-baseline.md`
and `docs/adr/ADR-012-arcore-provider-for-p0-android.md`.

## Next safest work

1. Author a larger doorway or a dog-specific house so rest can move fully inside.
2. Replace Randy11 clip substitutions with native down/rise/pickup/carry/drop/rest clips.
3. Add the production build manifest/BuildGuard before promoting beyond development.
4. Tune the serialized 0.35 reticle only if a second representative room requires it;
   do not change verified asset scale to mask an aiming or tracking defect.

## Git handoff

This file was updated for the August 4 Pixel repair. Use `git log -1` for the pushed
checkpoint SHA. The private GitHub remote is
`https://github.com/robs46859-eng/gibiworld.git` (`origin`).
