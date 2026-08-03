# P0 AR dog sandbox checkpoint

**Date:** 2026-08-03  
**Source baseline:** `c09b9a47a951109175e54be44d06cf83ef62296f` (`main`)  
**Checkpoint state:** final Pixel acceptance passed; see `/HANDOFF.md`

> Final update: the Pixel 9a run passed ARCore tracking, horizontal floor detection,
> anchored placement, textured dog rendering, fetch/carry/drop, and upright visible
> dog-house threshold rest. The final runtime recorded zero OpenGL native plug-in errors,
> zero invalid near-zero projection errors, and zero Unity exceptions. The current APK is
> 185969947 bytes with SHA-256
> `df3bdb9e496d5bf42c57c2608ed972cbe8ccf3445a4ebaff9aa9daaef6718357`.

## Outcome target

On a Pixel, place one anchored scene containing the verified dog avatar, the real toy
ball, and the real dog-house prop. The dog automatically fetches, carries, returns, and
drops the toy, then rests visibly across the dog-house threshold. Reset must remove the
whole placed scene and its local anchor.

## Current gates

| Gate | Status | Evidence |
|---|---|---|
| Signed preset dog verifies and renders | **PASS** | Runtime PlayMode spawn uses the pinned signing key and signed StreamingAssets preset. |
| Correct scale, floor contact, and +Z presentation | **PASS (profiled)** | Scaled GLB is used by the signed preset; `PetAnimationProfile` applies deterministic presentation corrections. |
| Native contract rig and animation set | **PART** | The source dog still has 8 native clips and lacks six contract bones plus five requested clips. Profiled clip substitution and a jaw-based `MouthSocket` keep P0 functional. |
| Real toy and dog-house composition | **PASS** | Generated `PetSandbox` and `ARWorld` share the same measured GLB composition. |
| Deterministic fetch/rest loop | **PASS** | Fetch state ordering, attachment/drop, boundary clamp, safety ownership, and the signed vertical slice are tested. |
| Scene contract | **PASS** | `GibiWorld/Validate Scenes`: `GW-AR-001` passed after regeneration. |
| EditMode suite | **PASS** | 337/337 tests passed headlessly (the 118 Gibi tests plus the installed UnitySkills package tests), 16.02 s. |
| PlayMode vertical slice | **PASS** | 2/2 tests passed headlessly, 4.20 s. The signed dog completes fetch/drop/rest even with no render camera after removing visibility-dependent animation culling. |
| Real AR local anchor | **PASS in code; device pending** | `ARWorldAnchorHost` creates one AR Foundation anchor and parents `PlacedWorldRoot` beneath it. Failure keeps the entire composition hidden; Reset reparents and removes the anchor. |
| Floor detection and visualization | **PASS in config; device pending** | Plane detection is explicitly horizontal. The URP Unlit plane material is transparent cyan at 0.45 alpha and double-sided (`_Cull = 0`), separating provider detection from visual visibility in device diagnostics. |
| Android input | **PASS in config** | Active Input Handling is Input System only (`activeInputHandler = 1`); the unsupported Android `Both` setting was removed before this build. |
| Application identity | **PASS** | Explicit `GibiWorld` identity and `com.gibiworld.mobile`; no `com.DefaultCompany.*` placeholder remains. |
| Package reproducibility | **PASS** | Manifest and lock agree on Burst 1.8.29, Test Framework 1.6.0, XR Core Utils 2.6.0, and XR Management 4.5.4 after reconciliation through Unity Package Manager. |
| Current Android APK | **PASS (development)** | Unity 6000.0.74f1 ARM64 build succeeded in 155 s. Exact artifact evidence is below. |
| Pixel acceptance | **BLOCKED ON CONNECTION** | The user reports the Pixel is connected, but both `adb devices -l` and the Mac USB inventory currently show no Android/Pixel device. No install or AR claim has been made. |

## Selected asset evidence

- Signed/scaled preset GLB SHA-256:
  `0cfe82430485b6e99ffa6882c074872c99b642cb0126033fddfd635b1455af8a`
- Dog-house source SHA-256:
  `6c5f258f626c92be73e70db939142a8625a4522ebc381d5dce01fbb895725ea5`
- Toy source SHA-256:
  `b0582c2eecbea2d8df32a848fc83c7fe3891fdf0adff31d625a6c8a9a7393de1`
- Dog-house measured bounds: approximately 1.147 m wide, 1.084 m deep,
  0.900 m high.
- Toy measured diameter: approximately 0.067 m.

## Current Android artifact

- Path: `clients/gw-mobile/Builds/GibiWorld-c09b9a4.apk`
- Timestamp: `2026-08-03T12:14:33-0600`
- APK bytes: `119357335`
- APK SHA-256:
  `88e3f2a7c803231dd61703b5fc326b1667ac09f2e45c00b1782b258779280f51`
- Package/application: `com.gibiworld.mobile` / `GibiWorld`
- Version: code `1`, name `1.0`
- SDK: minimum 29, target/compile 36
- ABI: ARM64 only (`lib/arm64-v8a`)
- Unity: `6000.0.74f1`
- Source baseline: `c09b9a47a951109175e54be44d06cf83ef62296f`, dirty local
  checkpoint (60 modified/untracked entries at build time)
- `packages-lock.json` SHA-256:
  `ed24bca3bc21d02875b632c7f4e47c57549a7248d23eaec46b09ffbe4375c73e`

## Deliberate P0 compromise

The decorative dog-house opening is about 0.294 × 0.446 m, smaller than the reference
dog. Walking the mesh literally through the wall would be false acceptance. P0 therefore
paths to the measured threshold and places the resting pose 0.12 m across the sill, with
the dog still visible. A literal interior traversal requires a larger authored doorway or
a smaller compatible dog profile.

## Pixel handoff checklist

1. On the unlocked Pixel, enable Developer options and USB debugging; set the USB use to
   **File transfer / Android Auto** and accept the Mac's RSA authorization prompt.
2. Confirm the Pixel appears in both the USB inventory and `adb devices -l` as `device`.
3. Install `com.gibiworld.mobile` without uninstalling the old
   `com.DefaultCompany.gwmobile` build; this preserves the old app and its data.
4. Launch; grant camera permission; scan until a horizontal plane is accepted; tap once.
5. Observe two complete fetch → drop → rest loops. Confirm later taps do not relocate the
   scene and Reset removes the dog, toy, house, and anchor together.
6. Relaunch and confirm no stale local placement survives. Capture device logs and a
   short recording; measure frame time on the Pixel before calling device acceptance PASS.

## Known issues / follow-up

- Replace clip substitutions with native `down`, `rise`, `pickup`, `carry`, and `drop`.
- Author the six missing contract bones and validate deformation against production art.
- Repeat the now-passing fetch/rest vertical slice on the Pixel and soak it for transient
  navigation failures.
- Add a complete §8.6 build manifest and BuildGuard before promoting beyond development.
