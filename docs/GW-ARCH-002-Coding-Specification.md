# GW-ARCH-002 — GibiWorld Coding Specification

| Field | Value |
|---|---|
| Specification | **GW-ARCH-002** |
| Version | 1.0.0 |
| Baseline date | 2026-08-03 |
| Supersedes | GW-ARCH-001 v1.0.0 **for implementation matters only** |
| Absorbs | ADR-008 (AR Foundation 6.4.2) |
| Scope | Code, contracts, build configuration, tests |
| Out of scope | Product scope, experience pillars, release phases, device QA matrix, commercial terms, operational runbooks — those remain in GW-ARCH-001 §1, §2, §17.1, §18 and are unchanged |

**Normative language.** SHALL / MUST / MUST NOT / REQUIRED / EXACTLY are normative. Every
normative statement here is either (a) implementable in code, (b) checkable by a compiler,
analyzer, or test, or (c) a build setting written from code. If a statement cannot be
mechanically verified, it does not belong in this document.

**Relationship to GW-ARCH-001.** GW-ARCH-001 remains the product and governance baseline.
Where the two documents state a different value for an implementation fact, **GW-ARCH-002
governs**, and §1.4 below records every such divergence with the evidence that produced it.

---

## 1. Verified as-built baseline

Every value in §1.1–§1.3 was read from disk on 2026-08-03. Values are **measured**, not
quoted from prior documents.

### 1.1 Toolchain

| Item | Value | Source of truth |
|---|---|---|
| Unity editor | `6000.0.74f1` (rev `7685f01dc6be`) | `ProjectSettings/ProjectVersion.txt` |
| Editor installed locally | `6000.0.74f1` — **and only that version** | `/Applications/Unity/Hub/Editor/` |
| NSDK | `4.1.0-26051913`, fingerprint `ee58c671595129854bf6c26aa0bfd952e6ff6e57` | `Library/PackageCache/com.nianticspatial.nsdk@ee58c6715951/package.json` |
| AR Foundation / ARCore / ARKit | `6.4.2` | `Packages/manifest.json`, NSDK `package.json` dependency floor |
| URP | `17.0.4` | `Packages/manifest.json` |
| glTFast | `6.16.1` | `Packages/manifest.json` |
| Addressables | `1.22.3` | `Packages/manifest.json` |
| Burst | `1.8.17` | `Packages/manifest.json` |
| Input System | `1.11.2` | `Packages/manifest.json` |

The NSDK package **resolves successfully**. It is present, extracted, and fingerprinted in
`Library/PackageCache`. Package resolution is not blocked and never was after ADR-008
corrected the package name and dropped the npm scoped registry.

### 1.2 Player settings — as read from `ProjectSettings/ProjectSettings.asset`

| Key | On-disk value | Meaning | Verdict |
|---|---|---|---|
| `scriptingBackend` | `Android: 1`, `iPhone: 1` | IL2CPP | conforms |
| `apiCompatibilityLevel` | `6` | .NET Standard 2.1 | conforms |
| `managedStrippingLevel` | `Android: 1`, `iPhone: 1` | Low | conforms |
| `m_ActiveColorSpace` | `1` | Linear | conforms |
| `m_MTRendering` | `1` | multithreaded rendering on | conforms |
| `accelerometerFrequency` | `0` | disabled | conforms |
| `AndroidMinSdkVersion` | `29` | API 29 | conforms |
| `iOSTargetOSVersionString` | `14.0` | iOS 14 floor | conforms (see §1.4-C) |
| `cameraUsageDescription` | set, names AR | — | conforms |
| `locationUsageDescription` | set, when-in-use wording | — | conforms |
| `microphoneUsageDescription` | empty | not requested | conforms |
| `bluetoothUsageDescription` | empty | not requested | conforms |
| `stripEngineCode` | `1` | on | conforms |
| `m_BuildTargetGraphicsAPIs` → `AndroidPlayer` | `m_APIs: 0b000000`, `m_Automatic: 0` | **OpenGLES3 only** (`0x0B` = 11) | conforms to code, contradicts doc (§1.4-B) |
| `m_BuildTargetGraphicsAPIs` → `iOSSupport` | `m_APIs: 10000000`, `m_Automatic: 1` | Metal (`0x10` = 16), **automatic** | §1.4-F |
| `AndroidTargetArchitectures` | `3` | ARMv7 \| ARM64 | §1.4-G |
| `activeInputHandler` | `2` | **Both** | **non-conforming** (§1.4-A) |
| `applicationIdentifier` | `{}` | **unset** | **non-conforming** (§1.4-D) |
| `companyName` / `productName` | `DefaultCompany` / `gw-mobile` | placeholders | **non-conforming** (§1.4-D) |
| `iOSRequireARKit` | `0` | no App Store device filter | **non-conforming** (§1.4-E) |
| `scriptingDefineSymbols` | `Android: NIANTICSPATIAL_NSDK_AR_LOADER_ENABLED` | Android only | §1.4-H |

### 1.3 XR provider configuration

`Assets/XR/XRGeneralSettingsPerBuildTarget.asset` contains `Keys: 0700000001000000` —
build target group **7 (Android)** and **1 (Standalone)**. There is **no iOS entry**.

| Group | Loader list | `m_AutomaticLoading` | `m_AutomaticRunning` | `m_InitManagerOnStart` |
|---|---|---|---|---|
| Android | `NsdkARCoreLoader` (`8848d099…`) — sole loader | `0` | `0` | `1` |
| Standalone | *empty* | `0` | `0` | `1` |
| iOS | **absent** | — | — | — |

Loader assets present but **unreferenced**: `ARCoreLoader`, `ARKitLoader`,
`NsdkARKitLoader`, `NsdkSimulationLoader`, `NsdkStandaloneLoader`, `OpenXRLoader`,
`SimulationLoader`.

`Assets/XR/Settings/NSDK Settings.asset`:

```
_useNsdkDepth: 1   _preferLidarIfAvailable: 1   _useNsdkMeshing: 1
_useNsdkSceneSegmentation: 1   _useNsdkScanning: 1
_useNsdkVps2: 1   _useNsdkDeviceMapping: 1
_locationAndCompassDataSource: 0
_spoofLocationInfo: 37.795322, -122.39243   (San Francisco default)
_devicePlaybackSettings._usePlayback: 0
```

`Assets/XR/Settings/AuthBuildSettings.asset`:

```
_authEnvironment: 2
_refreshToken: <empty>       _accessToken: <empty>
_refreshExpiresAt: 0         _accessExpiresAt: 0
_accessTokenOverride: <empty>
_useDeveloperAuthentication: 0
```

**Every NSDK runtime feature flag is enabled and zero lines of runtime C# call NSDK.**
A repository-wide search of `clients/gw-mobile/Assets/**/*.cs` for `Niantic`, `Lightship`,
`NSDK`, or `nsdk` returns matches **only inside editor comments** in
`Editor/ProjectSettingsApplier.cs` and `Editor/UrpSetup.cs`. `Gibi.Spatial.asmdef`
references `Unity.XR.ARFoundation`, `Unity.XR.ARSubsystems`, `Unity.XR.CoreUtils` and
nothing from NSDK.

`Assets/Gibi/AI/Runtime/` contains an `.asmdef` and **no source files**. `Gibi.AI` compiles
to an empty assembly. Requirements GW-AI-001 … GW-AI-006 have no client implementation.

### 1.4 Divergence register — resolved by this document

Each entry states the observation, the resolution, and where the fix belongs.

**A. `activeInputHandler` is `2` (Both), not `1` (Input System only).**
`Editor/ProjectSettingsApplier.cs` writes `1` through `SerializedObject` and logs a restart
requirement. The on-disk value is `2`, so the write has not persisted. `docs/BUILD_STATE.md`
asserts "Input System only — not Both", which the file contradicts.
**Resolution:** the value SHALL be `1`. §8.4 makes it a build-blocking assertion rather than
a menu action that can silently fail to persist.

**B. Android graphics API is OpenGLES3 only; `ProjectSettings/GibiBuildSettings.md` says
"Vulkan, OpenGLES3".** The code is correct — NSDK's Unity setup instructions require Vulkan
be removed. The table is wrong.
**Resolution:** Android graphics APIs SHALL be exactly `[OpenGLES3]` with
`SetUseDefaultGraphicsAPIs(false)`. §7.6 explains why this does **not** prevent GPU compute
for the NPC brain.

**C. iOS deployment target is 14.0; `GibiBuildSettings.md` says 13.0.** 14.0 is the NSDK
floor and is correct. The table is wrong.
**Resolution:** iOS deployment target SHALL be `14.0`.

**D. `applicationIdentifier` is `{}` and `companyName` is `DefaultCompany`.** Any build
produced today ships as `com.DefaultCompany.gw-mobile`. This poisons ARCore/Play install
identity, iOS provisioning, and any signed-build reproducibility claim under §8.6.
**Resolution:** §8.2 makes bundle identifiers normative and §8.4 makes an unset identifier a
build failure.

**E. `iOSRequireARKit` is `0`.** The App Store will not restrict distribution to
ARKit-capable devices, and `arkit` is absent from `UIRequiredDeviceCapabilities`.
**Resolution:** SHALL be `1`.

**F. iOS graphics APIs are marked `m_Automatic: 1`.** `ProjectSettingsApplier` calls
`SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false)`, so this asset is stale relative to the
applier — the same staleness class as (A).
**Resolution:** iOS SHALL be `[Metal]` with automatic disabled.

**G. `AndroidTargetArchitectures: 3` (ARMv7 | ARM64).** ARMv7 is retained from NSDK's
setup guidance. Every accelerated inference backend in §7 is ARM64-only, and a 32-bit split
cannot load the model.
**Resolution:** the LLM-enabled build variant SHALL be `ARM64` only. See §8.3 for the
two-variant rule.

**H. `NIANTICSPATIAL_NSDK_AR_LOADER_ENABLED` is defined for Android only.** Correct today
because there is no iOS XR entry. It becomes wrong the moment §8.5's iOS provider block is
added.
**Resolution:** the define SHALL be applied to every build target that carries an NSDK
loader, written from code in the same pass that writes the loader list.

**I. `docs/BUILD_STATE.md` states "NSDK unresolvable without credentials" and "Unity version
mismatch — machine has 6000.5.2f1".** Both are stale. NSDK is resolved and cached;
`6000.0.74f1` is the only editor installed. What is actually missing is **runtime NSDK
authentication** (`AuthBuildSettings` tokens are empty), which blocks VPS2/scanning/mapping
at session start, not at package resolution or compile time.
**Resolution:** §8.7 states the NSDK auth contract in terms of what the code must do when
credentials are absent.

**J. `Assets/XR/Settings 1/`** — the duplicate folder flagged in `BUILD_STATE.md` §8 is no
longer present. No action.

---

## 2. Assembly graph

### 2.1 Declared graph

References point **inward only**. A cyclic reference is a build failure, enforced by
`Editor/AssemblyGraphCheck.cs`.

```
Gibi.Core                    (no references)
  ├── Gibi.Spatial           Core, Unity.XR.ARFoundation, Unity.XR.ARSubsystems, Unity.XR.CoreUtils
  ├── Gibi.AssetRuntime      Core, glTFast
  │     └── Gibi.Pets        Core, AssetRuntime, Unity.Animation.Rigging
  │           ├── Gibi.Gameplay   Core, Spatial, Pets
  │           └── Gibi.AI          Core, Pets, Networking
  │                 └── Gibi.AI.LocalBrain   Core, Gibi.AI              ← NEW (§7)
  ├── Gibi.Networking       Core
  ├── Gibi.Telemetry        Core
  └── Gibi.UI               public facades only

Gibi.Editor                 editor-only
Gibi.Tests.EditMode         all
Gibi.Tests.PlayMode         all
```

### 2.2 Reference rules — mechanically checked

| # | Rule | Check |
|---|---|---|
| R1 | No cyclic assembly references. | `AssemblyGraphCheck.Validate()` |
| R2 | `Gibi.Spatial` is the **only** assembly that may reference AR Foundation, AR Subsystems, or NSDK. | asmdef reference scan |
| R3 | `Gibi.Core` SHALL have zero references, engine types excepted. | asmdef reference scan |
| R4 | **`Gibi.AI.LocalBrain` MUST NOT reference `Gibi.Networking`.** | asmdef reference scan |
| R5 | `Gibi.AI.LocalBrain` MUST NOT reference `Gibi.Spatial`. | asmdef reference scan |
| R6 | No assembly outside `Gibi.AI.LocalBrain` may `DllImport` the inference native library. | Roslyn analyzer over `DllImport` attributes |
| R7 | `Gibi.UI` may reference only public facade types. | analyzer over `internal`/namespace depth |

**R4 is the load-bearing rule of §7.** It converts "the on-device brain's context never
leaves the device" from a policy into a compile-time property: an assembly that cannot see
the networking layer cannot transmit. It is the same structural move as GW-ARCH-001 §4's
adapter rule and §8.2's `additionalProperties:false`.

### 2.3 New assemblies introduced by this specification

| Assembly | References | Contains |
|---|---|---|
| `Gibi.AI` (populate) | `Gibi.Core`, `Gibi.Pets`, `Gibi.Networking` | `IIntentSource`, `AiIntentEnvelope`, `AiIntentValidator`, `AiContextBuilder`, `RemoteIntentSource` |
| `Gibi.AI.LocalBrain` | `Gibi.Core`, `Gibi.AI` | `LocalLlmIntentSource`, `BrainBackendSelector`, `NpcBrainNative`, `ConstrainedDecoder`, `ModelResidency` |

---

## 3. Core type contracts

These types already exist and are load-bearing. This section fixes their contracts so no
future change can weaken them silently.

### 3.1 `Gibi.Core`

| Type | Contract |
|---|---|
| `GeoPosition` | float64 WGS84. SHALL expose **no** implicit or explicit conversion to `UnityEngine.Vector3`. Assigning a geographic value into engine float space SHALL be a compile error. |
| `AnchorLocalPose` | Validating factory only; the constructor SHALL be private. Rejects NaN, zero quaternion, `|1 − ‖q‖| > 1e-4`, and `‖p‖ > 75 m`. Returns `Result<AnchorLocalPose>`. |
| `MonotonicClock : IMonotonicClock` | `Stopwatch`-backed. `DateTime`, `DateTimeOffset`, and `Time.time` SHALL NOT appear anywhere in the ranked-scoring call graph. Enforced by analyzer over the `Gibi.Gameplay` scoring namespace. |
| `GibiId` | Opaque prefixed ULID, `^(pet\|spo\|crs\|req\|toy\|site\|run)_[0-9A-HJKMNP-TV-Z]{26}$`. Parsing a value that matches `^[0-9]+$` SHALL fail — a sequential database id crossing the API boundary is a defect, not a value. |
| `Result<T>` | No exception-based control flow across assembly boundaries. |
| `DeviceTier` | `A \| B \| C`, resolved once at bootstrap from RAM, SoC class, and thermal headroom. Consumed by §7.4 residency and §8 quality tiers. |

### 3.2 Determinism boundary

The following SHALL be deterministic given identical inputs and clock: behavior arbitration,
locomotion, gate crossing, surface acceptance, anchor eligibility, training state, safety
gates, scoring.

`DeterministicMotion` SHALL expose **no overload accepting `Time.deltaTime`**. Frame-rate
coupling is therefore a compile error, not a code-review finding. This property SHALL be
preserved. The fixed step is **50 Hz**; the arbiter tick is **10 Hz**.

---

## 4. Frame and tick contract

Per-frame order in `Gibi.Gameplay`'s driver:

1. Read platform input and AR provider frame.
2. Update trackables; snapshot anchor quality.
3. Apply floating-origin and anchor corrections with smoothing limits (≤ 2 mm anchor-local error).
4. **10 Hz** — behavior arbitration (`BehaviorArbiter.Tick()`).
5. **50 Hz FixedUpdate** — navigation and motion.
6. Animator + Animation Rigging foot/gaze constraints.
7. URP render: camera, depth occlusion, transparents, UI, accessibility overlays.
8. Sample telemetry **after** presentation.

Normative additions:

- Safety proposals SHALL bypass the tick cadence entirely (`BehaviorArbiter.ForceSafety`)
  so GW-GAME-001's one-tick bound holds regardless of tick phase.
- **No step in this list may block on I/O, inference, or a network call.** Step 4 SHALL
  drain already-completed results only. See §7.3.

### 4.1 Behavior priority ladder

| Priority | Layer | Owner |
|---|---|---|
| 0 | `SafetyOverride` | client, deterministic |
| 1 | `SessionRule` | authoritative session |
| 2 | `PlayerCue` | client, validated input |
| 3 | `AiIntent` | **remote proposal or local brain** — identical priority, identical validator |
| 3.5 | `Redirection` | client, deterministic (`RedirectionPolicy`) |
| 4 | `NeedsScheduler` | client, deterministic |
| 5 | `AmbientAnimation` | client |

Layers 0, 1, and 2 are **locking**: a strictly lower priority cannot preempt them.

**The local LLM enters at priority 3 and nowhere else.** It has no elevated path, no
override, and no ability to reach layers 0–2. Adding one would require changing
`BehaviorLayer`, which is covered by `SpecComplianceTests`.

---

## 5. Spatial code contracts

### 5.1 Coordinate frames

| Frame | Representation | May persist? |
|---|---|---|
| WGS84 | float64 lat/lon/alt | server only |
| `VPS_SITE` | map-relative pose from NSDK VPS2 | **yes** |
| `AR_LOCAL` | Unity float pose under `XROrigin` | **no** |
| `ANCHOR_LOCAL` | `Vector3` + normalized quaternion | yes, relative to one site anchor |
| `PET_LOCAL` | root-space meters | no |

`spatial-object.schema.json` SHALL keep `anchorFrame: LOCAL_SESSION` structurally barred
from every persistence endpoint. A world position computed from a device session SHALL NOT
be persisted.

### 5.2 `AnchorEligibility` — six states, fixed thresholds

```
Unavailable → Scanning → LocalReady → VpsLimited → VpsTracked → Degraded
```

| Constant | Value | Requirement |
|---|---|---|
| tracked dwell before `VpsTracked` | 1.0 s | GW-AR-002 |
| degrade grace | 250 ms | GW-AR-003 |
| run invalidation | 3.0 s | GW-GAME-008 |
| pose-jump threshold | 0.35 m | GW-AR-008 |

Scoring authority SHALL derive from the state machine only. Device VPS state alone SHALL
NOT authorize placement or scoring.

### 5.3 `SurfaceAcceptance` — fails closed

Gate order, evaluated in sequence, first failure wins:

`motion (4.5 m/s sustained 10 s) → anchor state → surface hit → camera start volume
(0.7 m pets / 1.5 m courses) → lighting confidence → hazard class → slope (12° pets /
7° ranked) → clearance radius (0.45 m pets / 1.5 m courses) → clearance height`

Any unrecognized semantic tag SHALL be treated as hazardous. Every rejection SHALL carry a
stable code, a localization key, an icon id, a color, **and** a haptic — color alone is
insufficient.

### 5.4 Three gaps this specification closes

| Gap | Present behavior | Required behavior |
|---|---|---|
| Hazard classes `Sky`/`Person`/`Vehicle`/`Water`/`Road`/`Rail` are declared but no code path emits them; plane classification cannot produce them. | GW-ARCH-001 §13.3's "no detected person/vehicle intersection" is **unenforced**. | `ISemanticProbe` SHALL be introduced in `Gibi.Spatial` with two implementations: `NsdkSemanticProbe` (Lightship segmentation) and `ArkitPeopleStencilProbe`. Where neither is available, `SurfaceAcceptance` SHALL treat the person/vehicle predicate as **unknown → hazardous** for ranked placement, and permissive only for `LocalReady` private play. Silent `NONE` is forbidden. |
| `lightingConfidence` is hardcoded `1.0f`; the 0.35 gate can never fire. | dead gate | SHALL be sourced from `ARCameraFrameEventArgs.lightEstimation` where available; where unavailable, SHALL return `float.NaN` and the gate SHALL treat `NaN` as **fail** for ranked and **pass with coaching** for local. A constant is forbidden. |
| `clearanceHeightM` returns the spec minimum constant; the height gate never fires. | dead gate | SHALL be computed from `ARMeshManager` headroom raycast. Until meshing is wired, SHALL return `float.NaN` with the same NaN semantics as above. |

The pattern is normative: **an unimplemented probe returns `NaN` and fails closed for
ranked. It never returns a value that makes a safety gate pass.**

### 5.5 Placement figures

Course-publication figures SHALL NOT be applied to casual pet placement. The two figure sets
are distinct and SHALL live in distinct constant classes (`CourseGeometryLimits`,
`PetPlacementLimits`) so a cross-application is a visible type error rather than a tuning
mistake. This is the single largest bug class in the project's history and it SHALL be
prevented structurally, not by comment.

---

## 6. Asset runtime code contracts

### 6.1 Verification algorithm — eight steps, no reordering

1. Fetch manifest over TLS from the authenticated asset endpoint. Reject unknown schema
   version, issuer, key id, asset version, or compatibility range.
2. Canonicalize (RFC 8785) excluding the signature; verify **Ed25519** against the pinned
   key named by `keyId`.
3. Confirm entitlement status is `ACTIVE` and names the exact `petAssetId` + `assetVersion`.
4. Download to a temporary cache key; stream SHA-256; enforce `Content-Length` and a
   **45 MiB** hard limit.
5. Compare digest with **constant-time** equality. On mismatch: delete temporary bytes, emit
   `asset_integrity_failed`, disable automatic retry for that URL.
6. Parse with glTFast under an import policy that disallows external URIs. Re-enforce node,
   mesh, material, texture, animation, and bounds limits **client-side**.
7. Instantiate under `PetAssetRoot`, replace materials with the approved URP shader family,
   bind the Gibi controller, then **atomically** promote the cache entry by digest.
8. Never persist signed URLs, access tokens, or raw authorization headers in logs,
   analytics, crash reports, or player-visible diagnostics.

Presets and Pawsome3D assets SHALL use the **same** verifier instance. Issuer is data, never
a branch.

### 6.2 Client-side limits (`AssetLimits`)

| Dimension | Limit |
|---|---|
| LOD0 / LOD1 / LOD2 / LOD3 | 35,000 / 18,000 / 7,500 / 2,000 triangles |
| LOD screen transitions | 0.42 / 0.18 / 0.06 |
| Skinned meshes | ≤ 2, ≤ 4 weights/vertex, ≤ 96 deform bones |
| Materials | ≤ 3, PBR metallic-roughness only |
| Textures | base/normal ≤ 2048, others ≤ 1024, decoded total ≤ 48 MiB |
| Morph targets | ≤ 12, ≤ 4 simultaneous |
| GLB transfer | ≤ 45 MiB |
| Animation clips | ≤ 48, ≤ 300,000 keyframes, **no animation events** |
| Scale curves | **forbidden** — count SHALL be 0 |
| Root transform | identity within 1e-4 |

### 6.3 Real-world scale — normative

Generated source assets normalize to a unit cube and carry **no real-world scale**. The
importer SHALL NOT trust source units.

- The reference adult dog SHALL be **0.50 m at the shoulder**. Species scale factors SHALL
  be derived from this reference and stored in the manifest, never inferred at runtime.
- `manifest.bounds` SHALL be expressed in meters after the authored rescale, and the client
  SHALL reject an asset whose measured post-import bounds differ from `manifest.bounds` by
  more than 2%.
- Shoulder height outside 0.12–1.10 m, or total bounds > 2.0 m on any axis, SHALL reject.
- Props SHALL carry an explicit `realWorldScale` field. An absent field SHALL reject —
  defaulting to 1.0 reproduces the one-metre-ball defect.
- Course props SHALL additionally satisfy the corridor minimum: 0.8 m wide × 1.5 m high.

A skinning bind of one influence per vertex passes every automated gate and still creases at
the joints. `glb_inspect.py` SHALL emit a **warning-class** finding `RIGID_SKIN_BINDING`
when `max_influences == 1`, and the release gate SHALL treat it as a blocking finding for
any asset in the launch allowlist.

---

## 7. NPC brain — offline LLM with GPU/NPU acceleration

This section is new. It specifies a fully offline language-model NPC brain running on the
device's GPU or TPU, with the Pixel/Tensor line as the primary target.

### 7.0 The one rule that governs everything below

> **The local model is a supplement that selects from a fixed enum. It is never
> load-bearing, never awaited, and never able to widen what the pet can do.**

`LocalBehaviorLibrary` already produces a valid intent immediately, with no network and no
waiting. The LLM does not replace it and does not gate it. There is no fallback path because
there is no failure state: a crash, a timeout, a cold model, a thermal throttle, and a
deliberate airplane-mode session are **indistinguishable from the player's side**.

Everything in §7 is subordinate to that sentence.

### 7.1 Interfaces

Declared in `Gibi.AI`, implemented in `Gibi.AI.LocalBrain` (local) and `Gibi.AI` (remote).

```csharp
namespace Gibi.AI
{
    /// Anything that can propose a priority-3 intent. Remote and local are peers.
    public interface IIntentSource
    {
        /// Non-blocking. Returns a ticket, or Ticket.None if the source declined.
        IntentTicket Submit(in AiContext context, long requestedAtMs);

        /// Non-blocking. Returns false if not ready. NEVER waits.
        bool TryCollect(IntentTicket ticket, out AiIntentEnvelope envelope);

        void Cancel(IntentTicket ticket);

        IntentSourceHealth Health { get; }
    }

    public readonly struct AiIntentEnvelope
    {
        public readonly int    SchemaVersion;      // const 1
        public readonly string RequestId;          // req_<ULID>
        public readonly string PetId;              // pet_<ULID>
        public readonly int    ContextRevision;
        public readonly string Intent;             // MUST be a member of the published enum
        public readonly string TargetId;           // (toy|spo|pet)_<ULID>, optional
        public readonly float  Valence;            // [-1, 1]
        public readonly float  Arousal;            // [0, 1]
        public readonly string UtteranceKey;       // ^pet\.[a-z_]{1,32}\.[0-9]{2}$
        public readonly MemoryProposal[] Proposals;// <= 3
        public readonly long   ExpiresAtMs;
        public readonly IntentProvenance Provenance; // Remote | LocalGpu | LocalNpu | LocalCpu
    }
}
```

`AiIntentValidator.Validate(in AiIntentEnvelope)` SHALL be the **single** validation entry
point and SHALL be applied identically to remote and local envelopes. `Provenance` SHALL
NOT appear in any validation branch — it exists only for telemetry and test assertions. A
`switch` on `Provenance` inside the validator SHALL be a build failure enforced by analyzer.

### 7.2 Backend selection

`BrainBackendSelector` resolves once at bootstrap and never re-resolves mid-session.

| Rank | Backend | Runtime | Target hardware | Precondition |
|---|---|---|---|---|
| 1 | `LocalNpu` | LiteRT-LM + Google Tensor ML SDK | Tensor G5 TPU (Pixel 10 class) | SDK present, `.litertlm` TPU-compiled artifact resolves, `DeviceTier == A` |
| 2 | `LocalGpu` | LiteRT-LM GPU delegate (OpenCL) | PowerVR DXT (Tensor G5), Mali (Tensor G1–G4), Adreno, Xclipse | `libOpenCL.so` dlopen succeeds, ≥ 6 GiB RAM |
| 3 | `LocalGpuVk` | llama.cpp Vulkan backend, GGUF | any Vulkan 1.1+ device | fallback only where OpenCL is unavailable |
| 4 | `LocalCpu` | LiteRT-LM XNNPACK, 4 threads pinned to big cores | any ARM64 | `DeviceTier ∈ {A, B}` |
| 5 | `Disabled` | — | — | everything else |

Normative rules:

- Selection SHALL be **capability-probed**, never inferred from a device name string or SoC
  allowlist. Probe = attempt to create the backend and run one 8-token warm-up; failure
  demotes to the next rank.
- Probe SHALL run on the loading thread, off the frame path, with a **1200 ms** ceiling.
  Exceeding it demotes.
- `Disabled` is a **first-class, fully supported** configuration. Feature parity with
  `Disabled` is asserted by `OfflineParityTests` and is a release gate.
- Vulkan is ranked below OpenCL deliberately: Adreno and Mali Vulkan compute paths have
  documented load failures and poor throughput relative to their OpenCL paths.
- Backend, model id, and quantization SHALL be recorded in the §8.6 build/session manifest.

### 7.3 Threading and frame safety

```
Unity main thread            Inference thread (native, detached)
──────────────────           ────────────────────────────────────
10 Hz arbiter tick
  ├─ local intent chosen  ← always, immediately, unconditionally
  ├─ Submit(ctx)   ──────────────►  enqueue, return ticket
  └─ TryCollect(t) ◄────────────── SPSC ring buffer (lock-free)
        └─ if not ready: proceed with the local intent. No wait. No log. No UI.
```

- The native library SHALL NOT call back into managed code. Reverse P/Invoke from a
  non-Unity thread under IL2CPP is a crash source and is forbidden.
- Results SHALL be delivered through a **single-producer / single-consumer ring buffer** of
  fixed capacity **4**. Overflow SHALL drop the **oldest** entry.
- `Submit` and `TryCollect` SHALL each complete in **< 50 µs** on the main thread. Asserted
  by a PlayMode performance test.
- The inference thread SHALL run at below-normal priority and SHALL be suspended whenever
  `ARSession.state != SessionTracking`.
- Supplement budget: **2500 ms** (`AiSupplementPolicy.SupplementBudgetMs`). A result older
  than the budget is discarded exactly as if it had never arrived, and increments
  `LateArrivals` — a diagnostic counter that is never player-visible.

### 7.4 Model residency and memory budget

GW-ARCH-001 §7 caps peak resident memory at 1.2 GiB (Tier A/B) and 900 MiB (Tier C). The
brain SHALL fit inside that ceiling, not beside it.

| Tier | Model class | Weights (resident) | KV cache | Total brain ceiling |
|---|---|---|---|---|
| A | ~1 B parameters, int4 (e.g. Gemma-class 1B IT) | ≤ 420 MiB | ≤ 96 MiB | **≤ 520 MiB** |
| B | ~270 M parameters, int4 | ≤ 180 MiB | ≤ 48 MiB | **≤ 230 MiB** |
| C | — | — | — | **`Disabled`** |

- Weights SHALL be memory-mapped read-only, never copied into managed heap.
- The runtime SHALL subscribe to `Application.lowMemory` and **unload the model, demoting to
  `Disabled` within one 10 Hz tick**. Demotion is silent.
- Context window SHALL be capped at **512 tokens** in, **48 tokens** out. The prompt is
  assembled from structured state, not conversation history; there is no growing transcript.
- KV cache SHALL be reset at every session start and at every pet switch.

### 7.5 Model distribution and trust

The model is an asset and SHALL be treated as one.

- Model artifacts SHALL NOT be embedded in the APK/IPA. Android SHALL use Play Asset
  Delivery (fast-follow) or Addressables remote; iOS SHALL use on-demand resources.
- Each artifact SHALL carry a `model-manifest.json` validated by
  `contracts/schemas/model-manifest.schema.json` with `additionalProperties: false`, fields:
  `schemaVersion`, `modelId`, `modelVersion`, `backendTarget`, `quantization`,
  `paramCount`, `sha256`, `sizeBytes`, `intentEnumRevision`, `keyId`, `signature`.
- The manifest SHALL be **Ed25519-signed and verified by the existing `AssetVerifier`**,
  reusing steps 1–5 and 8 unchanged. There SHALL NOT be a second signature verifier in the
  codebase.
- `intentEnumRevision` SHALL match the server-published enum revision. A mismatch SHALL
  demote to `Disabled` rather than load. A model that was tuned against a different enum is
  a model that will fight the constrained decoder.
- Digest mismatch SHALL delete the bytes, emit `asset_integrity_failed`, and disable
  automatic retry for that URL — identical to §6.1 step 5.

### 7.6 Why an OpenGLES3 render path does not block GPU inference

Unity's Android graphics API is fixed to OpenGLES3 because NSDK requires Vulkan be removed
(§1.4-B). This constrains **Unity's rendering context only**.

- The OpenCL backend obtains its own `cl_context` via `dlopen("libOpenCL.so")` and shares
  nothing with Unity's GL context.
- The Vulkan fallback creates its own `VkInstance` and `VkDevice`. A Vulkan compute instance
  coexisting with a GLES3 render context in one process is supported.
- Neither backend SHALL create a shared texture, shared image, or interop surface with
  Unity's renderer. Zero-copy interop is **forbidden** — it is the only construct that would
  make the render API a real constraint.
- The native library SHALL `dlopen` its accelerator loader lazily and SHALL NOT link it at
  load time. A missing `libOpenCL.so` SHALL demote, never crash the process.

**GPU contention is the real cost, not API incompatibility.** The brain SHALL therefore
cap its GPU duty cycle at **20%** of wall time measured over a 5 s sliding window, and SHALL
suspend entirely at thermal status `Serious` or worse, resuming only at `Nominal` after a
30 s hysteresis. Frame time is the budget the brain borrows from; §7.9 makes exceeding it a
test failure.

Thermal status SHALL be read through `IThermalSignal` in `Gibi.Core`, with two
implementations and **no third**:

| Platform | Source | Availability |
|---|---|---|
| Android | `PowerManager.getCurrentThermalStatus()` via `AndroidJavaObject` | API 29 — **exactly** the `AndroidMinSdkVersion` already pinned, so no floor change is needed |
| iOS | `UnityEngine.Apple.ThermalState` via `Device.thermalState` | iOS 11+ |

Adding `com.unity.adaptiveperformance` for this would be a package addition against a frozen
manifest and SHALL NOT be done for thermal alone. Where `IThermalSignal` cannot resolve a
value it SHALL return `Serious` — the brain suspends rather than assumes headroom.

### 7.7 Native ABI

A flat C ABI. Every call is non-blocking. No callbacks, no managed delegates, no exceptions
across the boundary.

```c
typedef struct {
    const char* model_path;        /* mmap'd, read-only                       */
    const char* grammar_path;      /* compiled intent grammar, §7.8           */
    int32_t     backend;           /* 1=NPU 2=GPU_CL 3=GPU_VK 4=CPU           */
    int32_t     max_context_tokens;/* <= 512                                  */
    int32_t     max_output_tokens; /* <= 48                                   */
    int32_t     thread_count;      /* CPU backend only                        */
    uint64_t    sampling_seed;     /* 0 = greedy; nonzero = seeded top-k      */
    float       gpu_duty_cap;      /* 0.0 .. 1.0, §7.6                        */
} GibiBrainConfig;

typedef enum {
    GIBI_BRAIN_OK          = 0,
    GIBI_BRAIN_PENDING     = 1,
    GIBI_BRAIN_EMPTY       = 2,   /* nothing ready; not an error              */
    GIBI_BRAIN_BUDGET_MISS = 3,
    GIBI_BRAIN_BACKEND_LOST= 4,   /* demote to next rank                      */
    GIBI_BRAIN_OOM         = 5,
    GIBI_BRAIN_INVALID     = 6
} GibiBrainStatus;

int32_t gibi_brain_create (const GibiBrainConfig* cfg, void** out_handle);
int32_t gibi_brain_probe  (void* h, int32_t timeout_ms);      /* warm-up, §7.2 */
int64_t gibi_brain_submit (void* h, const uint8_t* ctx, int32_t len); /* -> ticket */
int32_t gibi_brain_poll   (void* h, int64_t ticket, uint8_t* out, int32_t cap, int32_t* out_len);
int32_t gibi_brain_cancel (void* h, int64_t ticket);
int32_t gibi_brain_stats  (void* h, GibiBrainStats* out);
void    gibi_brain_destroy(void* h);
```

- `gibi_brain_submit` SHALL return within 50 µs and SHALL NOT allocate on the calling thread.
- `gibi_brain_poll` returning `GIBI_BRAIN_EMPTY` is the **normal** case and SHALL NOT log.
- `GIBI_BRAIN_BACKEND_LOST` SHALL demote one rank per §7.2 and SHALL NOT retry the lost
  backend for the remainder of the session.
- The context payload SHALL be a fixed-layout binary struct, **not** JSON and **not** free
  text. There is no string prompt crossing the ABI that a future change could widen.
- Library names: `libgibibrain.so` (`Plugins/Android/libs/arm64-v8a/`),
  `libgibibrain.a` static (`Plugins/iOS/`). No 32-bit variant exists.

### 7.8 Constrained decoding — the structural guarantee

`additionalProperties: false` makes GW-AI-002 structural for the remote provider. The local
equivalent is **logit masking at the sampler**.

- The decoder SHALL be driven by a compiled grammar that admits **exactly** the token
  sequences forming a valid `ai-intent` object. Tokens outside the grammar SHALL have their
  logits set to `-inf` before sampling.
- The `intent` field's grammar SHALL be generated from the server-published enum at build
  time into `grammar.bin`, versioned by `intentEnumRevision`. It SHALL NOT be hand-written.
- The `utteranceKey` field SHALL be constrained to the compiled key table. **The model
  selects a localization key; it never produces prose.** No token the model emits ever
  reaches a player as text.
- `targetId` SHALL be constrained to the ids present in the submitted context. The model
  cannot name an object that was not offered to it.
- `valence` and `arousal` SHALL be constrained to a fixed decimal grammar within range.
- `memoryProposals[].factType` SHALL be constrained to
  `FAVORITE_TOY | PREFERRED_TRICK | PLAY_TIME_OF_DAY | FAVORITE_PLACE_TAG`.

Consequence: **a malformed or out-of-enum local intent is not merely rejected downstream —
it is unrepresentable.** `AiIntentValidator` still runs on every local envelope as
defence in depth, and a validator rejection of a locally-produced envelope SHALL be treated
as a grammar defect and raise a build-gate-visible counter.

### 7.9 Determinism, seeding, and replay

- Test and CI configurations SHALL use `sampling_seed = 0` (greedy). Identical context SHALL
  produce an identical envelope, byte for byte.
- Shipping configurations MAY use seeded top-k. The seed SHALL be derived from
  `personalitySeed ^ tickIndex` so a given pet in a given situation is reproducible, and
  SHALL be recorded in the envelope for replay.
- LLM output SHALL NOT enter any deterministic path. Physics, navigation, scoring, gate
  crossing, and safety SHALL produce identical results with the brain enabled and disabled.
  `OfflineParityTests` SHALL assert this over a recorded fixture.

### 7.10 Local-only context

`Gibi.AI.LocalBrain` may see state the remote provider must never receive. This is safe
**only** because R4 makes transmission impossible.

| Field | Remote provider | Local brain |
|---|---|---|
| rotating opaque pseudonym | yes | yes |
| approved memory facts | yes | yes |
| coarse time bucket | yes | yes |
| pet state (bond, energy, personality seed) | yes | yes |
| nearby game object **types** | yes | yes |
| care profile flags | **never** | yes |
| `EngagementEstimate` (arousal, perseveration, settling, fatigue) | **never** | yes |
| camera frames, precise coordinates, contacts, advertising id, raw voice | **never** | **never** |

- `LocalOnlyContext` SHALL be a `readonly ref struct`. It cannot be boxed, stored on the
  heap, captured in a closure, or serialized. Combined with R4 this gives two independent
  structural barriers.
- `EngagementEstimate` SHALL remain ephemeral: never written to disk, never transmitted,
  never in telemetry, recomputed from scratch every launch.
- Care profiles SHALL enter as **parameter reweighting**, never as a described attribute.
  The prompt SHALL NOT contain a sentence about the player. The model receives a smaller
  menu and different weights; it never learns why, because it never needs to.
- Camera and microphone SHALL NOT be read by the brain even locally. No permission is
  requested for it — `microphoneUsageDescription` stays empty (§1.2).

### 7.11 Behavioral constraints the brain SHALL enforce

These are code constraints, not guidance. Each has a test.

| Constraint | Implementation |
|---|---|
| Fatigue modulates **how** the pet responds, never **whether**. | `pet_state.energy` SHALL be an input to animation quality only. Any code path where `energy` gates action availability SHALL fail `CareAndRedirectionTests`. |
| Repetition is never extinguished; it is given texture. | `DeterministicMotion` SHALL apply seeded micro-variation to repeated actions. A repetition counter SHALL NOT be able to return "unavailable". |
| No counter, streak, or badge is surfaced for repetition. | Internal counters SHALL be `internal` and SHALL have no `Gibi.UI` reference path. Enforced by R7. |
| The pet never asks the player to confirm a state. | The `utteranceKey` table SHALL contain no interrogative keys. Asserted by a table test over the key catalog. |
| Redirection is always additive. | `RedirectionPolicy` SHALL only ever emit `Propose`; it SHALL have no method that removes or disables an available action. |
| Redirection outranks AI intent but never safety. | Priority 3.5, fixed. `SpecComplianceTests` asserts the ladder. |
| The pet cannot be trained into unkindness. | The intent enum contains no such intent, and §7.8 makes an off-enum intent unrepresentable. |

### 7.12 Memory proposals while offline

A locally-produced `memoryProposal` SHALL NOT be applied directly to `pet_memories`.

- Local proposals SHALL be written to the **offline outbox** with
  `provenance = LOCAL_BRAIN` and SHALL be applied only after the server validates the fact
  type and value on the next successful sync.
- A memory the player deletes SHALL be tombstoned and absent from **both** remote context
  and local brain context within 24 h. The local KV cache SHALL be reset on deletion so the
  fact cannot survive in an in-flight context.
- Memories SHALL remain individually visible and individually deletable.

### 7.13 iOS parity

The interface is platform-neutral. `BrainBackendSelector` on iOS resolves:

| Rank | Backend | Runtime | Hardware |
|---|---|---|---|
| 1 | `LocalNpu` | LiteRT-LM Core ML delegate | Apple Neural Engine, A14+ |
| 2 | `LocalGpu` | Metal compute | any Metal 2 device |
| 3 | `LocalCpu` | XNNPACK, 4 threads | ARM64 |
| 4 | `Disabled` | — | — |

No `#if UNITY_ANDROID` branch SHALL appear above the ABI. Platform divergence lives entirely
inside `libgibibrain`.

---

## 8. Build configuration as code

Unity rewrites `ProjectSettings/*.asset` on open. Hand-edited YAML drifts silently — §1.4-A
and §1.4-F are that drift, observed. Therefore:

> **Every normative setting SHALL be written by `Gibi.Editor.ProjectSettingsApplier` and
> SHALL be re-asserted by `Gibi.Editor.BuildGuard` at the start of every build. A build whose
> settings do not match the applier's intent SHALL fail, not warn.**

### 8.1 Settings written from code — Android

| Setting | Value | Rationale |
|---|---|---|
| Scripting backend | IL2CPP | §3.1 |
| API compatibility | .NET Standard 2.1 | build size |
| Managed stripping | Low | glTFast reflection |
| Color space | Linear | URP |
| Graphics APIs | `[OpenGLES3]`, automatic **off** | NSDK requires Vulkan removed |
| Target architectures | see §8.3 | |
| Min SDK | 29 | ARCore depth |
| Target SDK | highest installed at build time, recorded in the manifest | Play policy |
| MTRendering | on | frame budget |
| GPU skinning | on | frame budget |
| Accelerometer frequency | disabled | minimize sensor capture |
| Active input handler | **1 (Input System only)** | Both is unsupported on Android |
| Static batching / dynamic batching | off / on | draw-call budget |
| `stripEngineCode` | on | build size |
| Scripting defines | `NIANTICSPATIAL_NSDK_AR_LOADER_ENABLED` on every target carrying an NSDK loader | §1.4-H |

### 8.2 Settings written from code — iOS

| Setting | Value |
|---|---|
| Scripting backend | IL2CPP |
| Graphics APIs | `[Metal]`, automatic **off** |
| Deployment target | **14.0** |
| `requiresFullScreen` | true |
| **`iOSRequireARKit`** | **true** |
| Camera usage string | present, names AR |
| Location usage string | present, when-in-use only |
| Microphone usage string | **empty** |
| Bluetooth usage string | **empty** |

### 8.2.1 Application identity — normative

`applicationIdentifier`, `companyName`, and `productName` SHALL be set explicitly for both
targets before any build is produced. `overrideDefaultApplicationIdentifier` SHALL be `1`.
A bundle identifier equal to `com.DefaultCompany.*` or an empty
`applicationIdentifier` map SHALL fail the build (§8.4).

### 8.3 Target architectures — two variants

NSDK's setup guidance suggests enabling ARMv7 and ARM64. Every accelerated inference backend
in §7 is ARM64-only.

| Variant | Architectures | Brain | Use |
|---|---|---|---|
| `gw-mobile-arm64` | ARM64 | enabled per §7.2 | **default**; the shipping variant |
| `gw-mobile-compat` | ARMv7 \| ARM64 | forced `Disabled` | only if a device on the §17.1 matrix proves 32-bit-only |

`AndroidBuildApkPerCpuArchitecture` SHALL be `1` for `gw-mobile-compat`. Shipping a fat
binary containing an ARM64-only native library under a 32-bit split SHALL fail the build.

### 8.4 `BuildGuard` — build-blocking assertions

Runs as `IPreprocessBuildWithReport`, order 0. Each failure throws
`BuildFailedException` with the setting name, expected value, and actual value.

| # | Assertion |
|---|---|
| G1 | `activeInputHandler == 1`. |
| G2 | Android graphics APIs `== [OpenGLES3]` and `GetUseDefaultGraphicsAPIs(Android) == false`. |
| G3 | iOS graphics APIs `== [Metal]` and `GetUseDefaultGraphicsAPIs(iOS) == false`. |
| G4 | `applicationIdentifier` set for the target and not `com.DefaultCompany.*`. |
| G5 | `iOSRequireARKit == true` for iOS builds. |
| G6 | Scripting backend IL2CPP, stripping Low, color space Linear. |
| G7 | `XRGeneralSettingsPerBuildTarget` contains an entry for the active target with a non-empty loader list. |
| G8 | The NSDK scripting define is present on every target that carries an NSDK loader, and absent on every target that does not. |
| G9 | `AssemblyGraphCheck` passes: no cycles, R2–R7 hold. |
| G10 | `SceneValidator.ValidateAll()` returns zero errors — exactly one `ARSession` and exactly one `XROrigin` in `ARWorld` (GW-AR-001). |
| G11 | For `gw-mobile-arm64`: target architectures `== ARM64`. |
| G12 | No `Assets/**` file matches the secret denylist; no `.utmp/`, `Library/`, or build-intermediate path is staged. |
| G13 | Every enabled NSDK feature flag in `NSDK Settings.asset` has at least one runtime C# consumer, **or** is explicitly listed in `NsdkFeatureWaivers.cs` with an owner and expiry date. |

**G13 exists because §1.3 found every NSDK feature enabled and zero code consuming any of
them.** Enabled subsystems cost battery, thermal headroom, and startup latency whether or
not anything reads their output — and thermal headroom is exactly what §7.6 borrows from.

### 8.5 iOS XR provider block — required

`XRGeneralSettingsPerBuildTarget` SHALL gain an iOS entry. Without it an iOS build starts no
session, which G7 now catches at build time rather than on device.

| Key | Loader | `m_AutomaticLoading` | `m_AutomaticRunning` | `m_InitManagerOnStart` |
|---|---|---|---|---|
| Android (7) | `NsdkARCoreLoader` | 0 | 0 | 1 |
| iOS (4) | `NsdkARKitLoader` | 0 | 0 | 1 |
| Standalone (1) | `NsdkSimulationLoader` (editor play mode) | 0 | 0 | 1 |

`m_AutomaticLoading = 0` is correct: `ARSession` drives loader lifecycle. The loader list
SHALL be written by `ProjectSettingsApplier`, not hand-edited, for the same reason as §8.

### 8.6 Reproducibility manifest

`AndroidBuild`/`IosBuild` SHALL emit `build-manifest.json` beside the artifact containing:
git commit (short + full), `m_EditorVersionWithRevision`, SHA-256 of `packages-lock.json`,
Addressables catalog hash, resolved NSDK fingerprint, target architectures, graphics APIs,
active scripting defines, **selected brain backend policy**, **model id + version +
quantization + `intentEnumRevision`**, and the `BuildGuard` assertion results.

A build without a complete manifest SHALL NOT be promoted past `dev`.

### 8.7 NSDK authentication contract

`AuthBuildSettings` currently carries no tokens (§1.3). This is a **runtime** condition, not
a compile-time one, and the code SHALL handle it as such.

- `INsdkAuthGate.IsAuthenticated` SHALL be checked before any VPS2, scanning, meshing, or
  device-mapping call.
- When unauthenticated: `AnchorEligibility` SHALL top out at `LocalReady`, scoring SHALL be
  `PracticeOnly`, and `MayPersistPlacement` SHALL be `false`. This is the correct P0
  behaviour and SHALL be reachable and tested with credentials present, not only absent.
- The unauthenticated path SHALL NOT surface an error to the player. Consistent with §7.0,
  the absence of a server capability is not an event.
- `_useDeveloperAuthentication` SHALL be `0` in every build promoted past `dev`.
- Tokens SHALL NOT be committed. `AuthBuildSettings.asset` SHALL be added to the secret
  denylist scanned by G12.

---

## 9. Contract and schema rules

`contracts/openapi/` (OpenAPI 3.1) and `contracts/schemas/` are the **single source of
truth**. C# DTOs and TypeScript types SHALL be generated, never hand-written.

| Schema | Structural guarantee |
|---|---|
| `pet-manifest.schema.json` | issuer, digest, key id, rig, bounds, compatibility all required |
| `spatial-object.schema.json` | `LOCAL_SESSION` barred from persistence |
| `agility-course.schema.json` | exactly 1 start + 1 finish + 1–20 obstacles |
| `ai-intent.schema.json` | `additionalProperties: false` makes GW-AI-002 structural |
| `model-manifest.schema.json` | **new**, §7.5 |

Additional rules:

- Public ids SHALL be opaque prefixed ULIDs. A sequential id crossing the API boundary SHALL
  fail contract tests.
- Every mutable aggregate SHALL carry a bigint `revision` incremented in the same transaction
  as the change.
- Every POST that creates value SHALL accept `Idempotency-Key`, scoped to subject + method +
  route, retained ≥ 24 h. Same key + same canonical body returns the original response; same
  key + different body returns `IDEMPOTENCY_CONFLICT`.
- Retry policy: connection failure, 408, 429, 5xx only. Backoff `0.5 / 1 / 2 / 4 s` plus
  0–250 ms jitter, maximum 4 attempts. `ASSET_SIGNATURE_INVALID` and validation 4xx SHALL
  NOT be retried without fresh user action.
- Inventory SHALL be an append-only ledger with a transactional non-negative constraint.
- The audit log SHALL be physically append-only via rewrite rules, not convention.

---

## 10. Test bindings

Every GW-* requirement binds to an artifact and a named test. A missing result is a failed
release gate.

### 10.1 New tests required by this specification

| Test | Asserts |
|---|---|
| `BuildGuardTests.G1_through_G13` | each build assertion fires on a deliberately broken setting |
| `AssemblyGraphTests.LocalBrain_cannot_reference_Networking` | R4 |
| `AssemblyGraphTests.LocalBrain_cannot_reference_Spatial` | R5 |
| `AssemblyGraphTests.Only_LocalBrain_declares_the_native_import` | R6 |
| `IntentValidatorTests.Validator_has_no_provenance_branch` | §7.1, via IL inspection |
| `ConstrainedDecoderTests.Off_enum_intent_is_unrepresentable` | §7.8, fuzzed over 10⁵ grammar walks |
| `ConstrainedDecoderTests.Model_cannot_emit_free_text` | every emitted `utteranceKey` is in the compiled table |
| `ConstrainedDecoderTests.Model_cannot_name_an_unoffered_target` | `targetId` ⊆ submitted context |
| `BrainBudgetTests.Submit_and_collect_under_50us` | §7.3 |
| `BrainBudgetTests.Late_result_is_indistinguishable_from_absent` | §7.3 |
| `BrainResidencyTests.LowMemory_demotes_within_one_tick` | §7.4 |
| `BrainThermalTests.Serious_thermal_suspends_inference` | §7.6 |
| `OfflineParityTests.Deterministic_paths_identical_brain_on_and_off` | §7.9 |
| `OfflineParityTests.Airplane_mode_full_playthrough` | GW-GAME-005 |
| `ModelTrustTests.Tampered_model_digest_rejects_and_does_not_retry` | §7.5 |
| `ModelTrustTests.Enum_revision_mismatch_demotes_to_disabled` | §7.5 |
| `LocalOnlyContextTests.Cannot_be_boxed_or_serialized` | §7.10, compile-fail fixture |
| `CareAndRedirectionTests.Energy_never_gates_action_availability` | §7.11 |
| `CareAndRedirectionTests.Repetition_never_becomes_unavailable` | §7.11 |
| `UtteranceCatalogTests.No_interrogative_keys_exist` | §7.11 |
| `SurfaceAcceptanceTests.Unimplemented_probe_returns_NaN_and_fails_ranked` | §5.4 |
| `PlacementLimitsTests.Course_figures_cannot_type_check_against_pet_placement` | §5.5 |
| `AssetScaleTests.Missing_realWorldScale_rejects` | §6.3 |
| `AssetScaleTests.Post_import_bounds_within_two_percent_of_manifest` | §6.3 |

### 10.2 CI pipeline order

`format → compile → static analysis (R1–R7) → secret scan → dependency audit →
EditMode tests → BuildGuard dry-run → PlayMode tests → asset validator fixtures →
model trust fixtures → OpenAPI compatibility → migration test → Android arm64 smoke build →
iOS smoke build`

A failure at any stage stops the pipeline. `BuildGuard` runs as a **dry-run stage** as well
as inside the build so a settings regression is caught before compile time is spent.

---

## 11. Implementation order

Dependencies flow downward; each row is buildable and testable on completion of the rows
above it.

| # | Work | Unblocks |
|---|---|---|
| 1 | `BuildGuard` + `ProjectSettingsApplier` rewrite; fix §1.4 A, D, E, F, H | every build claim in this document |
| 2 | iOS XR provider block (§8.5); `Gibi.Spatial` NSDK adapter behind `INsdkAuthGate` | an iOS build that starts a session |
| 3 | `ISemanticProbe`, `NaN` gate semantics (§5.4) | GW-ARCH-001 §13.3 actually enforced |
| 4 | Split `CourseGeometryLimits` / `PetPlacementLimits` (§5.5) | the project's largest bug class, structurally |
| 5 | Populate `Gibi.AI`: `IIntentSource`, `AiIntentValidator`, `AiContextBuilder`, `RemoteIntentSource` | GW-AI-001 … GW-AI-006 |
| 6 | `Gibi.AI.LocalBrain` C# side + ABI stub returning `GIBI_BRAIN_EMPTY` | §7 wiring proven with no model present |
| 7 | `libgibibrain` CPU backend + grammar compiler | §7.8 testable on desktop |
| 8 | OpenCL GPU backend; Tensor TPU backend | §7.2 ranks 1–2 |
| 9 | `model-manifest.schema.json` + `AssetVerifier` reuse + Play Asset Delivery | §7.5 |
| 10 | PlayMode tests and recorded AR playback fixtures | the standing Phase 2 gap |
| 11 | Ed25519 signing service + malicious/boundary GLB fixture matrix | GW-ASSET-001/002/004/006 to `IMPL` |

Step 6 is deliberately ordered before step 7. Wiring the brain with a backend that returns
"nothing ready" on every poll is the cheapest possible proof that §7.0 holds: if the game is
indistinguishable from today with the brain permanently empty, the supplement is genuinely
not load-bearing.

---

## Appendix A — Constants index

| Constant | Value | Section |
|---|---|---|
| Arbiter tick | 10 Hz / 100 ms | §4 |
| Motion step | 50 Hz fixed | §3.2 |
| Tracked dwell | 1.0 s | §5.2 |
| Degrade grace | 250 ms | §5.2 |
| Run invalidation | 3.0 s | §5.2 |
| Pose-jump threshold | 0.35 m | §5.2 |
| Player safety speed | 4.5 m/s sustained 10 s | §5.3 |
| Slope — pets / ranked | 12° / 7° | §5.3 |
| Clearance radius — pets / courses | 0.45 m / 1.5 m | §5.3 |
| Camera start volume — pets / courses | 0.7 m / 1.5 m | §5.3 |
| Course corridor | 0.8 m wide × 1.5 m high | §6.3 |
| Anchor distance limit | 75 m | §3.1 |
| Quaternion tolerance | 1e-4 | §3.1 |
| Reference dog shoulder height | **0.50 m** | §6.3 |
| Species shoulder height range | 0.12–1.10 m | §6.3 |
| GLB transfer limit | 45 MiB | §6.1 |
| AI supplement budget | 2500 ms | §7.3 |
| Brain ring buffer capacity | 4 | §7.3 |
| Brain context / output tokens | 512 / 48 | §7.4 |
| Brain memory — Tier A / B / C | 520 / 230 MiB / disabled | §7.4 |
| Brain GPU duty cap | 20% over 5 s | §7.6 |
| Thermal resume hysteresis | 30 s | §7.6 |
| Backend probe ceiling | 1200 ms | §7.2 |
| Main-thread ABI budget | 50 µs | §7.3 |

## Appendix B — Files this specification requires be changed

| Path | Change |
|---|---|
| `clients/gw-mobile/ProjectSettings/ProjectSettings.asset` | via applier: §1.4 A, D, E, F |
| `clients/gw-mobile/ProjectSettings/GibiBuildSettings.md` | correct the Android graphics API row and the iOS minimum row (§1.4 B, C) |
| `clients/gw-mobile/Assets/XR/XRGeneralSettingsPerBuildTarget.asset` | add the iOS entry (§8.5) |
| `clients/gw-mobile/Assets/Gibi/Editor/ProjectSettingsApplier.cs` | absorb §8.1–§8.3; remove the silent-failure `SerializedObject` path |
| `clients/gw-mobile/Assets/Gibi/Editor/BuildGuard.cs` | **new** (§8.4) |
| `clients/gw-mobile/Assets/Gibi/Editor/NsdkFeatureWaivers.cs` | **new** (G13) |
| `clients/gw-mobile/Assets/Gibi/AI/Runtime/**` | populate (§7.1) |
| `clients/gw-mobile/Assets/Gibi/AI/LocalBrain/**` | **new** assembly (§7) |
| `clients/gw-mobile/Assets/Gibi/Spatial/Runtime/ISemanticProbe.cs` | **new** (§5.4) |
| `clients/gw-mobile/Plugins/Android/libs/arm64-v8a/libgibibrain.so` | **new** (§7.7) |
| `contracts/schemas/model-manifest.schema.json` | **new** (§7.5) |
| `docs/BUILD_STATE.md` | correct the stale NSDK-resolution and Unity-version claims (§1.4-I) |
| `docs/adr/ADR-004-*.md` | decide; §7 is the client half of that decision |
| `docs/TRACEABILITY.md` | add GW-AI-001…006 client bindings and the §10.1 tests |
