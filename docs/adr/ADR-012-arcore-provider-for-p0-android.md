# ADR-012: Use the ARCore provider for the P0 Android runtime

- **Status:** Accepted
- **Date:** 2026-08-03
- **Scope:** `clients/gw-mobile`, Android P0 local placement
- **Supersedes:** ADR-008 only where it selects `NsdkARCoreLoader` as the active P0
  Android provider

## Context

The Pixel 9a device run used Unity 6000.0.74f1, NSDK 4.1.0-26051913, AR Foundation
6.4.2, OpenGLES3, and Android 17/API 37. Real AR tracking succeeded and horizontal planes
were detected, but the development console repeatedly reported:

`OPENGL NATIVE PLUG-IN ERROR: GL_INVALID_OPERATION: Operation illegal in current state`

The adjacent native error was:

`ViewManager::GetProjectionMatrix: near must be greater than zero; near=0`

This was not caused by Android GPU debug layers; device settings reported them disabled.
It was also not caused by the plane material or GibiWorld camera, whose near clip is 0.1 m.

The pinned NSDK source contains the conflicting value in
`Runtime/PlatformAdapterManager/DataSources/SubsystemsDataAcquirer.cs`.
`TryGetCameraTimestampMs` constructs dummy `XRCameraParams` with `zNear = 0` and calls the
camera subsystem every update. The official repository has no tag newer than the pinned
4.1.0 release at the time of this decision.

The P0 runtime consumes AR Foundation planes, raycasts, and local anchors. It does not
consume VPS2, scanning, meshing, scene segmentation, or another NSDK-only runtime service.
Keeping the NSDK loader active therefore adds a native failure without supplying P0 value.

## Decision

1. Android P0 SHALL assign `ARCoreLoader` as its sole XR loader.
2. `NIANTICSPATIAL_NSDK_AR_LOADER_ENABLED` SHALL be absent while that loader is inactive.
3. NSDK remains pinned in Package Manager so future authenticated features can be
   developed against an explicit adapter; it is not initialized by the P0 runtime.
4. `ProjectSettingsApplier` SHALL reassert this provider choice from code.
5. Re-enabling `NsdkARCoreLoader` requires a newer pinned NSDK source where the zero-near
   path is fixed, plus a Pixel device run with zero OpenGL native plug-in errors.

## Consequences

- Local AR placement uses the provider directly exercised by the P0 feature set.
- The current native projection/OpenGL error is removed instead of hidden.
- NSDK-only functionality remains unavailable in P0, matching the actual runtime code.
- ADR-008's package/version compatibility findings remain valid; only active-provider
  selection changes.

## Device acceptance gate

The replacement APK must prove all of the following on the Pixel 9a:

- `SessionTracking` and at least one horizontal-up plane;
- a visible plane overlay and one successful local anchor placement;
- zero `OPENGL NATIVE PLUG-IN ERROR` lines after cold launch;
- the verified dog, toy, and dog house visible in the anchored composition;
- one complete fetch/drop/rest loop.
