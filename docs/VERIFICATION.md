# GibiWorld — Verification Playbook

How to prove each gate and checklist item is actually satisfied, rather than believed to be.

Every entry gives a **command**, a **pass criterion**, and an **exists?** flag. `EXISTS`
means you can run it today. `WRITE` means the check is specified here but not yet built —
those are the real work items.

Companion to `BUILD_GATES.md`, `CHECKLIST.md`, and `ASSET_PRODUCTION.md`.
**Last verified:** 2026-08-02

---

## 0. Command reference

Set these once per shell:

```sh
export GW=/Users/robert/gibiworld
export UNITY="/Applications/Unity/Hub/Editor/6000.0.74f1/Unity.app/Contents/MacOS/Unity"
```

| Purpose | Command |
|---|---|
| EditMode tests | `$UNITY -batchmode -runTests -projectPath $GW/clients/gw-mobile -testPlatform EditMode -testResults /tmp/edit.xml -logFile -` |
| PlayMode tests | same, `-testPlatform PlayMode -testResults /tmp/play.xml` |
| Assembly graph | `$UNITY -batchmode -quit -projectPath $GW/clients/gw-mobile -executeMethod Gibi.Editor.AssemblyGraphCheck.CheckForCI -logFile -` |
| Scene validation | `$UNITY -batchmode -quit -projectPath $GW/clients/gw-mobile -executeMethod Gibi.Editor.SceneValidator.ValidateAllForCI -logFile -` |
| Rebuild scenes | `$UNITY -batchmode -quit -projectPath $GW/clients/gw-mobile -executeMethod Gibi.Editor.SceneBuilder.BuildAllForCI -logFile -` |
| Assembly refs (no Unity) | `python3 $GW/tools/check_assembly_refs.py` |
| GLB structural validator | `python3 $GW/tools/gw-asset-worker/validator/glb_inspect.py <file.glb>` |
| Migrations | see B6 below |

Exit code 0 = pass for all of the above. Unity batch mode returns non-zero on test failure,
but **always check the log too** — a licence failure also exits non-zero and is not a test
result.

---

# Part A — Build gates

## B1 — Toolchain and reproducibility

| # | Check | Command | Pass criterion | Exists? |
|---|---|---|---|---|
| B1.1 | Pinned editor installed | `ls "/Applications/Unity/Hub/Editor/6000.0.74f1"` | Directory exists | `EXISTS` |
| B1.2 | Project opens without upgrade prompt | `grep m_EditorVersion $GW/clients/gw-mobile/ProjectSettings/ProjectVersion.txt` | Reads `6000.0.74f1`, matches installed editor | `EXISTS` |
| B1.3 | NSDK resolves | `$UNITY -batchmode -quit -projectPath $GW/clients/gw-mobile -logFile -` then `grep -i "com.nianticspatial.nsdk" $GW/clients/gw-mobile/Packages/packages-lock.json` | Package present in lock with the pinned tag; no resolver error in log | `EXISTS` (blocked on credentials) |
| B1.4 | Lock matches manifest | `python3 -c "import json;m=json.load(open('$GW/clients/gw-mobile/Packages/manifest.json'))['dependencies'];l=json.load(open('$GW/clients/gw-mobile/Packages/packages-lock.json'))['dependencies'];print([k for k,v in m.items() if k in l and l[k].get('version')!=v])"` | Prints `[]` — no package resolved to a version other than the pin | `WRITE` (one-liner above; wire into CI) |
| B1.5 | iOS XR provider assigned | `grep -A3 'Keys:' $GW/clients/gw-mobile/Assets/XR/XRGeneralSettingsPerBuildTarget.asset` | Keys include `04000000` (iOS) with a non-empty loader list | `EXISTS` — **currently fails** |
| B1.6 | Scenes regenerate byte-identical | rebuild scenes, then `git diff --stat $GW/clients/gw-mobile/Assets/Scenes` | Empty diff | `EXISTS` |
| B1.7 | Clean checkout builds | `git clone $GW /tmp/gw-clean && cd /tmp/gw-clean && <run B2 suite>` | All CI jobs green from a fresh clone | `WRITE` |

**Fastest signal:** B1.6. If regenerated scenes differ from committed ones, something was
hand-edited and reproducibility is already broken.

## B2 — CI pipeline completeness

| # | Stage | Command | Pass criterion | Exists? |
|---|---|---|---|---|
| B2.1 | Secret scan | `grep -rEn "(sk_[A-Za-z0-9]{24,}\|AKIA[0-9A-Z]{16}\|-----BEGIN [A-Z ]*PRIVATE KEY-----)" --include="*.cs" --include="*.ts" --include="*.json" --include="*.yaml" --include="*.py" --include="*.md" $GW` | No output | `EXISTS` |
| B2.2 | JSON Schemas valid | `python3 -c "import json,glob;from jsonschema import Draft202012Validator as V;[V.check_schema(json.load(open(f))) for f in glob.glob('$GW/contracts/schemas/*.json')]"` | No exception | `EXISTS` |
| B2.3 | OpenAPI parses, ≥13 paths | see `.github/workflows/ci.yml` `contracts` job | 3.1.x, ≥13 paths | `EXISTS` |
| B2.4 | **OpenAPI backward compat** | `npx @redocly/openapi-cli diff <previous>.yaml contracts/openapi/gibiworld.v1.yaml` | No breaking change without a version bump | `WRITE` — current job only counts paths |
| B2.5 | Formatting | `dotnet format --verify-no-changes` (client) + `npx prettier --check "services/**/*.ts"` | No diffs | `WRITE` |
| B2.6 | Static analysis | Roslyn analyzers via `-warnaserror`; `npx eslint services` | No errors | `WRITE` |
| B2.7 | Dependency audit | `npm audit --audit-level=high` per service; `pip-audit` for tools | No high/critical | `WRITE` |
| B2.8 | Mobile smoke build | `$UNITY -batchmode -quit -projectPath $GW/clients/gw-mobile -executeMethod Gibi.Editor.AndroidBuild.BuildForCI -logFile -` | APK produced, non-zero size | `WRITE` — `AndroidBuild.cs` exists; no CI job |
| B2.9 | CI stage coverage | count jobs against the §16 list of 11 | 11/11 | `WRITE` — currently 5/11 |

## B3 — Test coverage

| # | Check | Command | Pass criterion | Exists? |
|---|---|---|---|---|
| B3.1 | EditMode suite | see §0 | **85/85 pass** (9 Age, 14 Care, 12 Connectivity, 3 Manifest, 9 Offline, 3 Accessibility, 7 Signature, 28 SpecCompliance) | `EXISTS` |
| B3.2 | Assembly graph acyclic | `AssemblyGraphCheck.CheckForCI` | Exit 0 | `EXISTS` |
| B3.3 | Scene contract (GW-AR-001) | `SceneValidator.ValidateAllForCI` | Exit 0; exactly one `ARSession`, one `XROrigin` | `EXISTS` |
| B3.4 | PlayMode suite | `-testPlatform PlayMode` | Suite runs and passes | `WRITE` — no tests authored |
| B3.5 | Recorded AR playback | XR Simulation environment + recorded session, assert `AnchorEligibility` transitions | State sequence matches expected within timing tolerance | `WRITE` |
| B3.6 | Malicious GLB matrix | `glb_inspect.py` against fixtures: external URI, oversized (>45 MiB), tampered digest, zero-length, deep nesting | Every fixture **rejected** with the correct code | `WRITE` — `contracts/fixtures/` is empty |
| B3.7 | Idempotency concurrency | Fire N identical POSTs with the same `Idempotency-Key` | One effect, N identical responses | `WRITE` |
| B3.8 | Ledger under load | Concurrent debits against a low balance | Never negative; no lost update | `WRITE` |

**Note:** `CHECKLIST.md` says "27 EditMode tests." The actual count is **85**. Fix the
checklist, and add B3.9 below so it cannot drift again.

| B3.9 | Test count matches docs | `for f in $GW/clients/gw-mobile/Assets/Gibi/Tests/EditMode/*.cs; do grep -c '\[Test\]' $f; done \| paste -sd+ - \| bc` | Matches the number in `CHECKLIST.md` | `WRITE` |

## B4 — AR runtime completeness

These need a device. Each has a proxy that runs without one.

| # | Check | How | Pass criterion | Exists? |
|---|---|---|---|---|
| B4.1 | Hazard rejection logic | EditMode `Hazard_tags_reject_placement` | Passes | `EXISTS` |
| B4.2 | **Hazard tags actually produced** | Assert `ARSurfaceProbe.ClassifyPlane` can return each hazard tag | Currently **impossible** — no path emits Sky/Person/Vehicle/Water/Road/Rail | `WRITE` — this is the gap, not the test |
| B4.3 | Person/vehicle intersection (§13.3) | Device: walk a person through the reticle | Placement rejects within one frame | `WRITE` — needs segmentation first |
| B4.4 | Lighting gate fires | Unit: feed `SurfaceProbeResult` with `lightingConfidence = 0.2` | Returns `LOW_LIGHT` | `WRITE` — probe hardcodes `1.0f`, so device testing is meaningless until fixed |
| B4.5 | Clearance height gate fires | Unit: sample with `clearanceHeightM = 1.0` | Returns `CLEARANCE_HEIGHT` | `WRITE` — probe returns the constant |
| B4.6 | Slope thresholds | EditMode `Ranked_gates_use_the_stricter_seven_degree_slope` | Passes | `EXISTS` |
| B4.7 | Purpose-dependent clearance | EditMode `Clearance_requirement_depends_on_purpose` | Passes | `EXISTS` |
| B4.8 | Tracking dwell ≥1.0 s | EditMode `Ranked_scoring_requires_one_full_second_of_tracking` | Passes | `EXISTS` |
| B4.9 | Degrade within 250 ms | EditMode `Tracking_loss_degrades_and_pauses_within_250ms` | Passes | `EXISTS` |
| B4.10 | Run invalidation >3 s | EditMode `Tracking_loss_beyond_three_seconds_invalidates_the_run` | Passes | `EXISTS` |
| B4.11 | Local never persists | EditMode `Local_anchor_never_authorises_persistence` | Passes | `EXISTS` |
| B4.12 | **VPS states on device** | Assign `TargetSiteAnchor`, observe `VpsLimited → VpsTracked → Degraded` | All six states reached | `WRITE` — blocked on NSDK |
| B4.13 | Accessible rejection | EditMode `Every_rejection_carries_an_icon_and_a_label_not_just_a_colour` | Passes | `EXISTS` |
| B4.14 | Anchor pose bounds | EditMode `Object_beyond_75m_from_anchor_is_rejected`, `Zero_quaternion_is_rejected`, `NaN_quaternion_is_rejected`, `Denormalised_beyond_1e4_is_rejected` | All pass | `EXISTS` |
| B4.15 | Depth occlusion active | Device: read `ARSessionDriver.DepthOcclusionActive` on a depth-capable phone | `true` | `WRITE` (manual) |
| B4.16 | Frame budget | Unity Profiler on target device, 5-minute session | Meets §15 frame budget; telemetry never blocks the frame | `WRITE` |

## B5 — Asset pipeline tooling

| # | Check | Command | Pass criterion | Exists? |
|---|---|---|---|---|
| B5.1 | Validator runs | `python3 $GW/tools/gw-asset-worker/validator/glb_inspect.py $GW/build/scaled/randy11_LOD0.glb` | JSON out, `violations: 0` | `EXISTS` |
| B5.2 | Signature verify (real preset) | EditMode `Real_preset_signature_verifies_against_the_pinned_key` | Passes | `EXISTS` |
| B5.3 | Tamper detection | EditMode `A_single_flipped_manifest_byte_invalidates_the_signature`, `A_tampered_signature_is_rejected` | Pass | `EXISTS` |
| B5.4 | Key pinning | EditMode `An_unpinned_key_id_is_rejected_even_with_a_valid_signature`, `Wrong_key_cannot_validate_another_keys_signature` | Pass | `EXISTS` |
| B5.5 | Revocation | EditMode `Revoking_a_key_takes_effect_immediately` | Passes | `EXISTS` |
| B5.6 | Canonicalisation | EditMode `Canonical_form_contains_no_whitespace_and_omits_the_signature` | Passes | `EXISTS` — this is the bug that caused `SIGNATURE_INVALID` on device |
| B5.7 | Transfer limit | EditMode `Transfer_limit_is_45_mebibytes` | Passes | `EXISTS` |
| B5.8 | Scale curves stripped | `python3 glb_inspect.py <LOD>.glb \| grep -i scale` | Zero scale channels in every LOD | `EXISTS` |
| B5.9 | Root motion stripped | same, check for root translation curves | Zero | `EXISTS` |
| B5.10 | **Signing service round-trip** | Sign a fixture via the service, verify in-client | Verifies; manual signing path removed | `WRITE` — service doesn't exist |
| B5.11 | Provenance completeness | Assert every output GLB has a provenance record with source SHA-256 | 100% coverage | `WRITE` |

## B6 — Backend services

| # | Check | Command | Pass criterion | Exists? |
|---|---|---|---|---|
| B6.1 | Migrations apply | `for f in $GW/db/migrations/*.sql; do psql -h localhost -U postgres -d gibiworld -v ON_ERROR_STOP=1 -f "$f"; done` | All three apply clean (`0001`, `0002`, `0003`) | `EXISTS` |
| B6.2 | Under-13 cannot activate | Insert an `UNDER_13` + `ACTIVE` user | Insert **refused** | `EXISTS` |
| B6.3 | Consent withdrawal disables | Grant, activate, withdraw | Status becomes `DISABLED` | `EXISTS` |
| B6.4 | Course immutability | `UPDATE course_versions SET version = version + 1;` | **Rejected** | `EXISTS` |
| B6.5 | Ledger cannot go negative | Debit beyond balance | Constraint trigger rejects | `WRITE` (constraint exists; no test) |
| B6.6 | Audit log append-only | `UPDATE audit_log ...` and `DELETE FROM audit_log ...` | Both rejected by rewrite rules | `WRITE` |
| B6.7 | Migrations are forward-only | Assert no migration file is ever modified after commit | `git log --follow` shows one commit per migration | `WRITE` |
| B6.8 | Service compile | `npx tsc --noEmit` per service | Exit 0 | `WRITE` — 9 services scaffolded, none written |
| B6.9 | Endpoint contract conformance | Run the OpenAPI spec against the live service (Dredd / Schemathesis) | All 13 endpoints conform | `WRITE` |
| B6.10 | Prefixed ULID boundary | EditMode `Sequential_database_ids_are_recognised_and_rejectable`, `Prefixed_ulid_pattern_is_enforced_per_entity` | Pass | `EXISTS` |

## B7 — Security controls in code

| # | Check | How | Pass criterion | Exists? |
|---|---|---|---|---|
| B7.1 | Webhook replay rejected | Replay a signed webhook outside the 5-minute window | Rejected | `WRITE` |
| B7.2 | Webhook HMAC | Tamper the body, keep the signature | Rejected | `WRITE` |
| B7.3 | Token redaction | Log/crash canary containing a token pattern | Token absent from emitted payload | `WRITE` |
| B7.4 | Telemetry PII filter | Feed a payload with lat/long + credentials | Both stripped before egress | `WRITE` |
| B7.5 | AI egress allowlist | Assert the outbound request body excludes location, camera, tokens, contacts, voice | Field-level assertion passes | `WRITE` |
| B7.6 | Retry policy bounds | Simulate failures; measure backoff | 0.5/1/2/4 s, ≤250 ms jitter, max 4 attempts | `WRITE` |
| B7.7 | Refresh rotation | Reuse a consumed refresh token | Whole family revoked | `WRITE` |
| B7.8 | Secrets not committed | B2.1 | No output | `EXISTS` |

## B8 — Requirement traceability

| # | Check | Command | Pass criterion | Exists? |
|---|---|---|---|---|
| B8.1 | Requirement count | `grep -cE '^\| GW-' $GW/docs/TRACEABILITY.md` | `42` | `EXISTS` |
| B8.2 | Status distribution | `grep -oE '\| (IMPL\|PART\|TODO) \|$' $GW/docs/TRACEABILITY.md \| sort \| uniq -c` | `15 IMPL · 18 PART · 9 TODO` | `EXISTS` |
| B8.3 | Docs match reality | Compare B8.1/B8.2 output against the numbers written in `CHECKLIST.md` | Identical | `WRITE` — **currently fails** (checklist says 40 / 16-19-10) |
| B8.4 | Every GW-* has a test | Cross-reference the Test column against actual test method names | No row cites a test that doesn't exist | `WRITE` |
| B8.5 | Every test cites a GW-* | Assert each EditMode test maps to a traceability row | No orphan tests | `WRITE` |

---

# Part B — CHECKLIST phases

## Phase 0 — Foundation

| Item | Verify with | Exists? |
|---|---|---|
| Monorepo tree per §3.1/§19 | Assert every required top-level unit exists: `contracts`, `db`, `services`, `clients`, `infra`, `tools`, `docs` | `WRITE` (trivial script) |
| `.gitignore` secrets denylist | B2.1 + `git check-ignore -v <sample.pem>` | `EXISTS` |
| Source models migrated | `ls $GW/assets/source-models/*.glb \| wc -l` → 3, originals untouched | `EXISTS` |
| OpenAPI 13 endpoints + 12 error codes | B2.3, plus assert 12 distinct codes in the error envelope | `EXISTS` / `WRITE` for the code count |
| 4 JSON Schemas valid | B2.2 | `EXISTS` |
| `LOCAL_SESSION` barred from persistence | Schema test: a `LOCAL_SESSION` spatial object must fail validation for a persisted payload | `WRITE` |
| `ai-intent` rejects unknown fields | Validate a payload with an extra property → must fail | `WRITE` |
| Package pins exact | B1.4 | `WRITE` |
| 11 asmdefs, inward-only, acyclic | B3.2 + `python3 tools/check_assembly_refs.py` | `EXISTS` |

## Phase 1 — Deterministic core

All discharged by EditMode tests that exist today. Run B3.1 and check these names:

| Item | Test |
|---|---|
| `GeoPosition` float64 | *(no direct test — `WRITE`: assert it cannot be assigned to `Vector3` and haversine matches a known fixture)* |
| `AnchorLocalPose` validation | `Zero_quaternion_is_rejected`, `NaN_quaternion_is_rejected`, `Denormalised_beyond_1e4_is_rejected`, `Object_beyond_75m_from_anchor_is_rejected` |
| `MonotonicClock` off the scoring path | *(`WRITE`: assert no `DateTime.Now`/`Time.time` reference in the ranked path — a source-level grep test)* |
| `GibiId` | `Sequential_database_ids_are_recognised_and_rejectable`, `Prefixed_ulid_pattern_is_enforced_per_entity` |
| `AnchorEligibility` | B4.8–B4.11 |
| `SurfaceAcceptance` fails closed | `Hazard_tags_reject_placement` + `WRITE`: feed an unrecognised enum value, assert hazard |
| `AssetVerifier` | B5.2–B5.7 |
| `AssetLimits` | `Transfer_limit_is_45_mebibytes`, `Digest_comparison_rejects_mismatch` |

## Phase 2 — Simulation and gameplay

| Item | Test | Exists? |
|---|---|---|
| `BehaviorArbiter` priority | `Safety_interrupts_every_lower_priority_layer_immediately`, `Lower_priority_cannot_preempt_a_locked_action` | `EXISTS` |
| 10 Hz cadence | `Arbiter_evaluates_at_ten_hertz` | `EXISTS` |
| Frame-rate independence | `Locomotion_distance_is_identical_at_30_60_and_120_fps`, `Fixed_timestep_is_exactly_fifty_hertz` | `EXISTS` |
| `GateCrossing` swept volume | `Fast_pass_that_skips_the_plane_between_frames_is_still_detected`, `Passing_beside_the_gate_post_does_not_count`, `Out_of_order_crossing_is_flagged` | `EXISTS` |
| `PlayerSafetyGate` | `Sustained_speed_above_threshold_enters_passenger_safe_mode` | `EXISTS` |
| `RigLimits` IK/look-at | *(no test found)* — `WRITE`: assert foot IK ≤0.18 m/25°, head yaw ≤50°, pitch ≤30°, and that look-at cannot snap | `WRITE` |
| `TrainingStateMachine` has no failure state | `WRITE`: enumerate states, assert none is a failure; assert all timeouts are kind | `WRITE` |
| PlayMode + AR fixtures | B3.4, B3.5 | `WRITE` |

## Phase 3 — Asset pipeline

See B5. Additionally:

| Item | Verify with | Exists? |
|---|---|---|
| 26/26 joint coverage | `python3 blender_inspect.py` against the profile; assert every `GIBI_QUADRUPED_V1` joint is mapped | `EXISTS` |
| LOD budgets | `glb_inspect.py` per LOD vs. 35k/18k/7.5k/2k | `EXISTS` |
| Remediation is idempotent | Run `blender_remediate.py` twice; second run produces an identical digest | `WRITE` |
| Fixture matrix | B3.6 | `WRITE` |

## Phase 4 — Backend and data

See B6.

## Phase 5 — Release gates

Only the engineering half applies here: B2 (CI completeness) and B8 (traceability). ADRs,
threat model, and privacy artefacts moved out of this tracker — see `BUILD_GATES.md`
"Deliberately excluded."

---

# Part C — Asset production

Art work can't be unit-tested, but it can be **gated**. These are the accept criteria a
delivered asset must meet before it enters the pipeline.

| Item | Check | Pass criterion | Exists? |
|---|---|---|---|
| Re-skin to 4 influences | `glb_inspect.py` reports max influences per vertex | ≤4 and **>1** — the current value of exactly 1 must change | `WRITE` (inspector reports it; no assertion) |
| `spine` subdivided | Joint list contains `spine_01`, `spine_02` | Present | `WRITE` |
| Clavicles present | Joint list contains `clavicle_l`, `clavicle_r` | Present | `WRITE` |
| Hocks present | Joint list contains `hock_l`, `hock_r` | Present | `WRITE` |
| 15 clips delivered | Clip name set matches the profile's required list | No missing clip | `WRITE` |
| Clips in-place | Zero root-motion curves | 0 | `EXISTS` |
| Prop scale sane | Bounding box vs. declared real-world size, ±10% | Within tolerance; `startgate` ≥1.5 m | `WRITE` |

**Highest-value one to build:** the influence-count assertion. Single-bone binding passes
every gate you have today and only shows up as creased joints in play.

---

# What to build first

Ranked by how much certainty each buys per hour spent:

1. **B8.3 + B3.9 — doc/reality drift checks.** Two greps in CI. Your checklist is currently
   wrong about both the requirement count and the test count; this class of error hides
   real regressions.
2. **B4.4 and B4.5 — make the dead gates fire.** Unblock by removing the hardcoded
   `lightingConfidence` and `clearanceHeightM`, then the unit tests are trivial. Right now
   two documented safety gates are decorative.
3. **B3.6 — malicious GLB fixtures.** `AssetVerifier` is thoroughly tested against *valid*
   input and never tested against hostile input. That's the wrong way round for a
   signature verifier.
4. **B1.4 — lock/manifest agreement.** One line. Directly protects §16 reproducibility, and
   catches exactly the class of drift ADR-008 was written about.
5. **B6.6 — audit log append-only.** The rewrite rules are claimed but never exercised. An
   append-only log that isn't is worse than no log, because it is trusted.
6. **B4.2 — decide the hazard-tag story.** Either wire semantic segmentation or write down
   that §13.3's person/vehicle clause is unmet in P0. Do not leave a declared safety gate
   silently unreachable.
