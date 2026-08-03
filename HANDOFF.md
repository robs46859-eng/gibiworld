# GibiWorld P0 AR Pixel handoff

**Date:** 2026-08-03
**Unity:** 6000.0.74f1
**Source baseline before this checkpoint:** `c09b9a47a951109175e54be44d06cf83ef62296f`
**Device:** Google Pixel 9a (`5A061JEBF20220`), Android API 37

## Outcome

P0 device acceptance passed on the connected Pixel. The app cold-launched with the
standard Unity ARCore loader, reached `SessionTracking`, detected horizontal floor
planes, placed one anchored sandbox, rendered the signed Randy11 dog with its real
brown/white texture, and ran the looping toy-fetch and dog-house-rest sequence.

The dog-house opening is physically smaller than this dog. The accepted P0 behavior is
therefore a visible upright rest across the measured doorway threshold, not mesh
clipping through the solid decorative shell.

## Acceptance evidence

- ARCore tracking: PASS
- Horizontal planes: PASS (2 initially, then up to 5)
- Anchored placement: PASS
- Textured signed dog: PASS
- Toy fetch/carry/drop: PASS
- Upright dog-house threshold rest: PASS
- Cyan plane coaching mesh hidden after placement: PASS
- `OPENGL NATIVE PLUG-IN ERROR`: 0 on the final run
- invalid near-zero projection errors: 0 on the final run
- Unity runtime exceptions: 0 on the final run
- EditMode: 337/337 passed
- PlayMode vertical slice: 2/2 passed

The final log contains two complete sequences of:

`FETCH_STARTED -> FETCH_COMPLETED -> REST_STARTED -> REST_ENGAGED`

Evidence is under `docs/device-evidence/`, especially:

- `pixel9a-final-composed-demo.mp4`
- `pixel9a-final-composed-contact.png`
- `pixel9a-final-current.png`
- `pixel9a-final-logcat.txt`
- `pixel9a-final-scan.mp4`

## Android artifact

- APK: `clients/gw-mobile/Builds/GibiWorld-c09b9a4.apk`
- Package: `com.gibiworld.mobile`
- Bytes: `185969947`
- SHA-256: `df3bdb9e496d5bf42c57c2608ed972cbe8ccf3445a4ebaff9aa9daaef6718357`
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
- Relevant Unity skills are installed locally, including the Unity-Technologies
  `unity-cli` and `unity-package-management` skills.

Architecture decisions are recorded in `docs/adr/ADR-011-unity-6-resolved-package-baseline.md`
and `docs/adr/ADR-012-arcore-provider-for-p0-android.md`.

## Next safest work

1. Author a larger doorway or a dog-specific house so rest can move fully inside.
2. Replace Randy11 clip substitutions with native down/rise/pickup/carry/drop/rest clips.
3. Orient the placed composition toward the camera for more repeatable first-shot
   framing; the current anchor is correct, but the provider's plane yaw can put the
   house near a screen edge.
4. Add the production build manifest/BuildGuard before promoting beyond development.

## Git handoff

This file is part of the all-work checkpoint requested on 2026-08-03. Use `git log -1`
for the checkpoint SHA. The private GitHub remote is
`https://github.com/robs46859-eng/gibiworld.git` (`origin`).
