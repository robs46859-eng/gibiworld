# Assets and animation — GW-ARCH-003 v1.0.0

Parent: [architecture](../../GW-ARCH-003-AR-Companion-Build-Specification.md). This is the production brief for technical art, animation, asset tooling, and Unity integration. Existing measurements are source-recorded; final deliverables must be remeasured after export/import.

## 1. Art direction and deliverables

Use the existing Gibi dog as the recognizable starting character, with readable eyes, calm body language and clear silhouettes on a camera background. Keep realistic physical proportions with a friendly presentation. The dwelling should look inviting and have an obvious entrance; the ball must contrast with common floors. Do not depend on color alone for target validity.

**ASSET-01.** Deliver one production-quality dog, one compatible dwelling, one fetch ball, the complete interaction animation set, UI indicators, sound cues, effects, and reproducible source/publishing records. The reference dog and one second eligible Pawsome3D dog must pass the same trust and controller integration tests; the second model tests asset variability rather than introducing a second active pet.

| Deliverable | Starting source | Required work | Release artifact |
|---|---|---|---|
| Hero dog | `randy11` and its remediated LODs | Smooth skinning; real shoulder/hock/spine articulation; validate bounds, face, sockets and clips | Four immutable GLB LODs, signed manifest and rig/animation profile |
| Second dog fixture | Eligible Pawsome3D dog | Same profile validation, independent shape/stride/mouth check | Signed compatibility fixture, never arbitrary upload |
| Functional home | `luxurydoghouse.glb` as visual reference | Remodel actual opening, interior, floor and wall thickness; author collision and markers | Catalog GLB, primitive collision profile, dwelling definition, thumbnail |
| Ball | `toyball-930.glb` | Preserve 0.067 m diameter; simplify mesh/material; visible grip point | Catalog GLB/prefab, toy definition, LODs |
| Pet presentation | Existing `PetAnimationProfile` | Replace P0 reverse/substitute mappings with native release mappings; migrate Legacy | Versioned profile, generated Generic graph/controller, rig constraints |
| AR indicators | Existing placement ring | Full-composition outline, path/throw preview, valid/invalid target, return zone | Client-authored pooled prefabs and localized labels |
| Effects | New small pooled library | Contact puff, landing ring, soft praise sparkle, recovery dissolve | Client-authored URP materials and bounded effects |
| Audio | New/cleared library | Greeting, footsteps, pickup, drop, soft praise, rest breath | Licensed source WAV plus compressed platform imports |
| Production records | Existing worker provenance | Source license, hashes, validator build, screenshots, contact test output | Reproducible build report keyed to every published version |

Do not edit `assets/source-models/` originals. Put editable production work in a new versioned art workspace; publish derived outputs under new content versions. Screenshots or attractive previews are not proof of rig, collision or gameplay compatibility.

## 2. Physical dimensions and fit

**ASSET-02.** Shoulder height is 0.50 m for the hero dog, as recorded by the corrected inventory. The source profile references a larger total head/ear envelope; shoulder height must not be mistaken for maximum entry height. Measure full evaluated mesh bounds across all release clips.

### Dwelling fit formulas

Let W be maximum lateral body/limb width during entry, H maximum entry height including ears, L nose-to-rump occupied length, Rturn the maximum horizontal swept radius of the chosen interior turn, and M=0.05 m initial physical clearance margin.

- Door clear width ≥W+2M.
- Door clear height ≥H+M; no part of the animated pet may exceed it during entry.
- Interior rest width ≥rest-pose width+2M; interior depth ≥rest-pose length+2M.
- A turning interior requires a free disk of diameter ≥2(Rturn+M); if impossible, author and validate a straight/reverse entry/exit sequence instead.
- The entrance path must include the full swept body envelope, not just the root or doorway center.
- Roof/wall geometry, primitive colliders and navigation exclusion outlines must agree within 0.02 m at gameplay surfaces.

Initial hero home target, pending envelope validation: clear doorway **0.70 m wide ×0.90 m high**; clear interior **1.30 m wide ×1.50 m deep ×1.00 m high**; exterior maximum approximately **1.50 ×1.70 ×1.30 m** (width/depth/height). These are an authoring starting point, not a claim that every dog can turn inside. The source-recorded 0.54 ×0.74 m reference envelope supports the doorway target; fresh animation measurements must validate the interior.

The existing decorative 0.294 ×0.446 m opening is not a valid hero home. Enlarging only a collider is insufficient; visible geometry must change. Runtime cannot nonuniformly scale a house or shrink the dog to make it fit. If space is insufficient, offer a smaller **compatible authored** dwelling or ask to re-place. Do not silently trade physical size for apparent availability.

### Local frames and markers

Dwelling origin: ground center of exterior bounds, +Y up, +Z pointing from front door toward interior. Pet gameplay faces +Z; a signed versioned correction may account for source orientation.

| Marker | Required meaning |
|---|---|
| ExteriorApproach | Grounded waiting/alignment point outside the doorway with full pet clearance |
| DoorThreshold | Center of navigable aperture at sill height; local forward points inward |
| InteriorTurn | Validated turn or reverse waypoint with associated swept envelope |
| InteriorRest | PetRoot pose for down/sleep, distinct from a visual cushion origin |
| ExitClear | Point outside the house where the entire pet clears the doorway |
| ToyReset | Optional safe ground point outside the home; must not lie in entry corridor |

Markers and primitive collisions are trusted client/catalog data bound to exact asset digest and version. A user-supplied GLB node cannot change the collision profile. Reject NaN, nonunit rotations and markers outside expected bounds.

## 3. Pet model and rig specification

**ASSET-03.** Preserve GW-ARCH-001 `GIBI_QUADRUPED_V1`, Unity Generic animation, in-place movement, no scale curves, root identity within 1e-4, and maximum 96 deform bones with ≤4 influences per vertex. Four is a ceiling, not a requirement to add meaningless influences. Skinning must visibly deform smoothly at shoulders, elbows, hips, hocks, jaw and spine.

Required named chain:

```text
root -> pelvis -> spine_01 -> spine_02 -> chest -> neck -> head
front L/R: clavicle_l/r -> upper_front_l/r -> lower_front_l/r -> paw_front_l/r
rear L/R: upper_rear_l/r -> lower_rear_l/r -> hock_l/r -> paw_rear_l/r
face: jaw, eye_l, eye_r where anatomically present
tail: optional contiguous tail_01..tail_06
ears: optional ear_l_01..03 and ear_r_01..03
```

Joint-name coverage is not evidence of functional anatomy. Reject placeholder coincident bones that do not participate in meaningful deformation. Validate skin weights and pose extremes after reopening the GLB, not only in Blender.

The root remains a grounded gameplay origin. The current Randy11 profile applies `(0,0.514626,0)` and -90° yaw to the imported holder. Preserve that verified P0 correction only for the corresponding old asset. A corrected new export receives a new measured profile; do not apply both a baked correction and the old holder offset.

### Sockets and contact metadata

| Socket/profile | Contract |
|---|---|
| MouthSocket | Attached to validated jaw/head chain; authored grip offset and rotation for the 0.067 m ball |
| LookOrigin | Measured head/eye center used for bounded gaze |
| Paw contact curves | Front/rear left/right support phase and local contact positions per gait |
| Interaction volumes | Authored head/back/body primitives; distinct from movement capsule |
| Entry envelope | Full animated body bounds for approach, entry, turn, down and rise |

**ASSET-04.** A trusted `PetActionProfile` defines action duration in ticks, contact/release normalized phase, optional sampled socket curve, stride reference distance and blend windows. It is bound to pet digest, clip hash and profile revision. Importing a different clip invalidates its contact metadata.

Contact events are enum data consumed by the client-owned timeline; they are not arbitrary Unity AnimationEvents, method names or scripts. Pickup contact should initially occur near 55% of the authored clip and release near 50% of drop, but the animator must choose the exact phases from the final art. Runtime and offline validators verify actual socket-to-ball error at those phases.

## 4. Animation library

**ASSET-05.** Publish every canonical clip required by GW-ARCH-001 for the chosen profile, even where a clip is unused by this first fetch UI. All sampled at ≤30 fps after reduction; ≤48 clips and ≤300,000 total keyframes per published pet. Root translation/yaw comes from the motor, never the animation.

| Canonical clip(s) | Loop | Required presentation / initial duration |
|---|---|---|
| idle_a | Yes | Relaxed neutral, 2–6 s |
| idle_b | Yes | Distinct quiet variation, 2–8 s |
| walk | Yes | In-place, 0.6–1.5 s; reference 1.0 m/s |
| trot | Yes | In-place, 0.5–1.2 s; reference 2.0 m/s |
| run | Yes | In-place, 0.4–1.0 s; reference 3.8 m/s; unused fast speed need not be enabled indoors |
| turn_l_90 / turn_r_90 | No | Footwork with root rotation stripped, 0.25–0.8 s |
| sit / sit_idle / stand | Transition / loop / transition | Correct posture changes, 0.4–4.0 s |
| down / down_idle / rise | Transition / loop / transition | Native floor and home rest; 0.4–4.0 s |
| jump_takeoff / jump_air / jump_land | Phase appropriate | Required profile coverage, 0.15–1.0 s; not enabled in fetch v1 |
| pickup | No | Reach jaw to grounded ball, contact phase, lift; 0.2–2.0 s |
| carry | Yes | Mouth stable, no penetrative jaw opening; 0.2–2.0 s |
| drop | No | Lower, release and withdraw, 0.2–2.0 s |
| success / greet / pet_react | No | Friendly, grounded, interruptible where safe, 0.5–8.0 s |
| sleep | Yes | Stable grounded body and breath, 0.5–8.0 s |

Catalog extras such as sniff, play_bow, stretch, yawn, shake and nudge need native clips or explicitly validated compositions before enabling those behaviors. Extras must stay within the clip/keyframe budgets. A catalog clipKey is trusted mapping metadata; it does not allow model output to select clip names.

For the release hero, P0 `pickup→pet_react`, `carry→walk`, `sleep→sit` and reversed sit/rise substitutions fail the native-animation acceptance gate. A temporary graybox test may use them only with explicit development labeling.

### Animation graph and locomotion

**ASSET-06.** Migrate `PetAssetLoader`/`PetAnimator` together from Legacy to a Generic Animator-compatible import and playback route supported by the pinned glTFast package. Prototype that route with one fixture before changing all assets. Validate runtime-bound clips through an Animator/PlayableGraph and rigging; merely changing an import enum does not constitute the migration.

Use a locomotion blend layer, exclusive action layer, and bounded additive gaze/breath/tail layer. Generated controller/graph references canonical actions, then the profile binds actual clips for each verified pet. `applyRootMotion=false`. No native third-party script or graph is imported from content.

- Motor speed drives gait and stride phase. Clamp ordinary playback adjustment to 0.75–1.25× authored speed; transition gait or author another clip when that would visibly slide.
- Carry can be a locomotion-compatible blend with a stable jaw overlay; a frozen full-body carry pose moving across the floor fails.
- Blend posture/IK weights for ≥100 ms; initial normal transition 150 ms. Safety stops motor immediately even if animation blends visually.
- Foot IK correction ≤0.18 m and paw rotation ≤25°. Ground samples must come from accepted support, not arbitrary depth noise.
- Head gaze ≤50° yaw /30° pitch; optional eyes add ≤15°. Damped target changes; no camera-snap behavior.
- Test culling and low-frame-rate playback: state/timeline advances correctly offscreen, while visible socket motion matches the authoritative contact phase.

## 5. Geometry, material, texture and audio budgets

| Item | Normative inherited limit or new initial allocation |
|---|---|
| Pet LOD0/1/2/3 | ≤35,000 /18,000 /7,500 /2,000 triangles; LOD0 ≤45,000 vertices |
| Pet mesh/material | ≤2 skinned meshes, ≤3 materials |
| Pet dimensions | Shoulder 0.12–1.10 m; full published bounds ≤2 m per axis |
| Pet textures | Base/normal ≤2048; other maps ≤1024; total decoded ≤48 MiB |
| Pet morphs | ≤12 targets, ≤4 active simultaneously |
| Pet payload | GLB compressed transfer ≤45 MiB; no external URI, camera/light/script |
| Dwelling LOD0/1/2 | Initial ≤12,000 /6,000 /2,000 triangles, ≤3 materials; decoded textures ≤24 MiB |
| Ball LOD0/1/2 | Initial ≤2,500 /800 /200 triangles, one material; texture ≤512 |
| Visible optional effects | Initial ≤500 particles for this loop; total app still within tier limits |
| Audio voices | Initial ≤8 simultaneous with ≤2 pet vocalizations; deterministic priority/culling |

The inherited pet LOD screen transitions are 0.42, 0.18 and 0.06. Validate no silhouette, socket, or rig jump at transition. Prefer one compatible skeleton/socket profile across pet LODs. Changing LOD must not detach the toy or reset the action.

Use approved URP PBR materials. Copy base color texture/factor correctly as the existing fix does; preserve normal-map semantics. Use sRGB base color and linear normal/metal/roughness data. Platform texture compression must be tested after import with full decoded memory counted. KTX2 is preferred only when the pinned import path handles it correctly. Normal opaque surfaces are the default; alpha blend is limited to approved eyes/fur and tightly budgeted.

Dwelling interior must render properly from exterior and cutaway viewpoints; no missing faces or inverted normals. Use primitive box/capsule collision pieces matching walls/roof/floor and leave the doorway actually open. Pet mesh colliders are forbidden; toy uses a sphere. The decorative mesh must not accidentally enter a navigation bake as a solid bounding box.

Spatial pet audio uses inherited attenuation 0.5–8 m. Critical status uses UI audio with text/icon/caption equivalents. Author several quiet variants with a seeded choice and a minimum spacing to prevent repetitive bark spam. No purchased voice subscription is required for core mechanics.

## 6. Publishing and runtime trust

**ASSET-07.** Use the existing Pawsome3D/preset authority and verifier. Production publishing stages:

1. Receive authorized preset source or timestamped HMAC asset-ready event; verify owner/source version and replay protection.
2. Fetch server-side into quarantine with an allowlisted origin, size/time limits and redirect restrictions. No client-supplied arbitrary URL is accepted.
3. Inspect GLB structure before expensive decode; reject external resources, invalid buffers/accessors, unsupported extensions and decompression/resource bombs.
4. Run isolated Blender remediation only on a derivative. Preserve source hash and build/tool versions. Enforce time, memory and egress limits.
5. Export each LOD; reopen independently; verify physical scale, geometry, skin/rig, root identity, clip bounds and materials. Run contact/doorway fixtures using the corresponding profiles.
6. Create immutable manifest with exact files/digests, rig, clip/profile versions and compatibility. Extend the production schema through reviewed versioning when multi-file/profile trust needs new fields; do not overload an existing scalar digest to imply coverage of unlisted files.
7. Sign canonical manifest with production Ed25519 key held outside the worker. Publish immutable content only after all gates pass. Separate development and production trust roots.
8. Client authenticates entitlement, verifies manifest signature/digests, bounds download/import, validates parsed content and replaces materials, binds trusted controllers/profiles, and promotes cache atomically. Nothing visible before the complete verification/binding succeeds.

**ASSET-08.** Props, profiles and content metadata need integrity too. For v1, ship home/toy/controller profiles as application-build content, protected by the signed app and reproducibility manifest. Remote updates require a signed catalog envelope with per-file digests, size limits, schema version and app compatibility. Do not invent pet entitlements for free props; enforce the correct catalog entitlement if a dwelling becomes owned content later.

Cache identity is digest, not an expiring download URL. Never log tokens, URLs, raw authorization headers or signing material. Keep last known verified compatible content during interrupted updates; an explicit revocation disables it even if cached. Unload glTF importer resources, textures, animation graph and Addressables handles on switch; no resource leaks hidden by a one-pet test.

## 7. Technical-art acceptance

| Fixture | Pass condition |
|---|---|
| Neutral scale | Export and imported pet shoulder 0.50 m ±0.005 m; ball 0.067 m ±0.001 m |
| Deformation | All core clips reviewed front/side/rear; no collapsed joints or torn skin at action extremes |
| Root motion | Root translation/rotation/scale identity within inherited tolerance for every clip sample |
| Pickup/drop contact | Socket/grip distance ≤0.04 m at event; no obvious floor penetration; no large contact teleport |
| Carry | Ball remains in jaw with ≤0.02 m visible grip drift through walk/trot/turn and LOD changes |
| Door traversal | Full animated body envelope passes visible opening and authored collision with ≥0.05 m margin |
| Rest | Body/feet supported; no shell clipping; source sleep flip/lift defect absent |
| Foot contact | On reference flat fixture, stance foot slide ≤0.03 m over a planted phase; penetration ≤0.02 m |
| Materials | Correct appearance after approved-shader replacement; no pink/missing textures; color-space tests |
| Thermal/load | Full composition within app budgets on all release tiers; unload returns memory to stable baseline |
| Round trip | Hash/provenance match; reopened exported files, not the authoring scene, satisfy every gate |

Numerical checks complement visual review; neither substitutes for the other. Asset rejection output must name the failed gate, asset version, offending clip/bone/node, measured value and allowed limit so the artist can repair it directly.
