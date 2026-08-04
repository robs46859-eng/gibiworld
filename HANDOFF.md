# GibiWorld P0 AR Pixel handoff

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
its cold-start log no longer contains the missing-driver warning.

The dog-house opening is physically smaller than this dog. The accepted P0 behavior is
therefore a visible upright rest across the measured doorway threshold, not mesh
clipping through the solid decorative shell.

## Acceptance evidence and current boundary

- ARCore tracking: PASS
- Horizontal plane-only probing: PASS
- Depth-hit `HAZARD_UNKNOWN` / `clearanceR=0.00`: eliminated in the fresh run
- Readiness/final-placement clearance parity: PASS
- Lower reticle live distance: PASS (1.70-2.08 m versus former 3.54 m)
- Correct-size signed dog and house rendered: PASS for the placement frame
- Cyan plane coaching mesh hidden after placement: PASS
- Camera pose-driver scene contract: PASS locally; missing-driver warning absent on Pixel
- EditMode: 340/340 passed
- PlayMode vertical slice: 2/2 passed
- Final persistence-after-camera-movement check on the newly installed pose-driver APK:
  **PENDING PHYSICAL RESCAN AND TAP**. Do not call the overall device goal complete until
  the dog/house remain in the rug view after moving the Pixel.

Evidence is under `docs/device-evidence/`, especially:

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
4. After the tracked-camera device pass, tune the serialized 0.35 reticle only if a
   second representative room requires it; do not change verified asset scale to mask
   an aiming or tracking defect.

## Git handoff

This file was updated for the August 4 Pixel repair. Use `git log -1` for the pushed
checkpoint SHA. The private GitHub remote is
`https://github.com/robs46859-eng/gibiworld.git` (`origin`).
