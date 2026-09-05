# Gemini Spark — GibiWorld application completion prompt

Copy the instructions below into Gemini Spark with access to this repository. Repository paths are relative to its checkout; on the originating Mac the checkout is `/Users/robert/gibiworld`.

---

You are the Lead Software Engineer and implementation owner for GibiWorld AR. Work in `https://github.com/robs46859-eng/gibiworld.git`. Complete the application described by GW-ARCH-003: a fully functioning AI GibiPet, a usable house/dwelling, and an interactive game of fetch controlled by the end user. Implement, integrate, test, and produce runnable application artifacts. Do not stop after a plan, a scaffolding pass, a demo animation loop, or documentation updates.

## 1. Read the actual project before changing it

Read these files in order:

1. `HANDOFF.md` — current handoff, evidence boundaries, and exact next step.
2. Applicable `AGENTS.md` files if present and `README.md`.
3. `docs/GW-ARCH-003-AR-Companion-Build-Specification.md`.
4. `docs/specs/ar-companion/01-runtime-and-mechanics.md`.
5. `docs/specs/ar-companion/02-assets-and-animation.md`.
6. `docs/specs/ar-companion/03-services-and-persistence.md`.
7. `docs/specs/ar-companion/04-delivery-and-acceptance.md`.
8. `docs/specs/ar-companion/examples/companion-tuning.v1.json` — proposed tuning, not installed runtime configuration.
9. `docs/adr/ADR-013-ar-companion-completion-baseline.md`, `docs/GW-ARCH-002-Coding-Specification.md`, and accepted ADR-008/011/012.
10. `GibiWorld_Architecture_Specification.pdf`, existing contracts/migrations, and the source corresponding to the work you are about to perform.

Read `.claude/skills/gibi-spec-gate/SKILL.md` before implementation verification and `.claude/skills/gibi-asset-pipeline/SKILL.md` before asset work. Resolve actual script locations from the checkout rather than trusting a stale example path. If an editor/asset skill is available in your environment, read its instructions before using its tools.

GW-ARCH-003 is the user-selected implementation target for this task. Its creation did not implement the application. Record this technical selection and reconcile implementation precedence in ADR-013 during M0; do not stall merely because the architecture package was originally labelled proposed. Preserve GW-ARCH-001's product/trust constraints and accepted provider/package ADRs. Conditional exceptions remain conditional: keep constrained practice and optional model inference disabled until their specified adoption/evidence gates pass. This instruction does not accept unrelated proposed age/care policies or authorize bypassing release requirements.

## 2. Establish a trustworthy baseline

Inspect `git status`, branch, remote and current commit before making changes. Preserve unrelated work. Fetch the current repository; do not reset a user's checkout. The reviewed historical source baseline was `73b36c758aaef98f23c206729fd31466a8cba190`; use the actual current source and account for subsequent changes.

The originating checkout is sparse. Check `git sparse-checkout list` and `git ls-tree -r --name-only HEAD` before declaring any source, scene, model or tool missing. Expand the checkout to include required scenes, prefabs, StreamingAssets, XR configuration, assets and worker tools before building. Never delete tracked files simply because they were omitted by sparse checkout. Check Git LFS requirements before treating a pointer file as an asset. Restore necessary untracked authoring inputs through the recorded backup process only when the task actually needs them; never print credentials.

Preserve the pinned Unity `6000.0.74f1` baseline, AR Foundation/ARCore/ARKit `6.4.2`, URP `17.0.4`, glTFast `6.16.1`, Addressables `1.22.3`, and the rest of the lockfile. Android P0 uses direct ARCore under ADR-012; NSDK remains pinned but inactive. Do not enable the old NSDK loader or upgrade dependencies as an incidental fix. Validate the real installed toolchain. Use the repository's documented upgrade process only if evidence requires a deliberate version change.

Create fresh baseline evidence. The August 4 handoff reported 340/340 EditMode tests, 2/2 PlayMode tests and Pixel placement/fetch/rest results. Those are historical, not today's results. Do not present them as fresh validation.

## 3. Implement the complete experience

Follow M0–M5 and work items W01–W18 in the delivery annex, making small integrated changes. Track all 39 requirements by ID in `docs/TRACEABILITY.md` with implementation paths, test names and evidence. Extend the existing architecture instead of starting a different engine or unrelated framework.

### AI GibiPet

Build a complete local perception → personality/policy → action execution system with stable pet identity and approved preferences. Connect existing `IntentPolicy`, `BehaviorArbiter`, `LocalFirstBehavior`, `PetController` and related classes to actual behavior executors. Catalog membership alone does not mean an action exists.

Use 10 Hz behavior decisions and 50 Hz motion. Safety preempts immediately; player commands take precedence over AI/ambient behavior. Fatigue changes how the pet responds, never whether an otherwise legal repeat action is available. No punishment, deprivation, visible repetition counter, or loss of bond for time away.

Give every action a generation/sequence token so stale completion cannot clear a newer action. Make the pet responsive to Fetch, Come, Sit, Home, Pet and Pause. Integrate native animation, grounded feet, bounded gaze, mouth contact, sound and presentation with the actual motor. Gameplay must advance correctly when its renderer is offscreen.

### AR dwelling

Build a genuinely traversable dwelling using the measured pet envelope. Author a visible opening, interior, compatible collision geometry, approach/threshold/rest/turn/exit markers and occupancy state. The pet must visibly walk inside, turn or use a validated alternative, lie down, rest, rise, and exit when called.

The old decorative opening is too small. Do not hide the dog renderer to simulate entry, change colliders while leaving an impossible visible doorway, or shrink the dog to fit. Initial proposed dimensions are in the asset annex; remeasure final exported/imported geometry and clips. Keep runtime scale and source orientation corrections tied to exact content versions.

### End-user fetch

Disable `SandboxDemoDirector` in production; retain it only for explicit QA fixtures. Implement player aiming, trajectory preview, drag/release throw and accessible tap-target/Throw alternative. Use the same bounded fixed-step throw solver for preview and execution.

Implement flight, settling, outbound navigation, approach alignment, pickup at the trusted contact phase, carry, return to a valid user zone, release/drop, brief praise and immediate readiness to repeat. One toy has exactly one pose owner. No duplicate toy, duplicate terminal event, fake reward, automatic throw or mandatory rest.

Handle the player moving during return, invalid throws, blocked paths, the house doorway, tracking loss, stale geometry, app background, pet switch, new commands and object destruction. Cancel safely and idempotently. Never chase the camera outside the accepted play space or snap a remote ball into the mouth.

### Spatial and content integrity

Validate the entire composition and continuous paths inside the actual accepted polygon. Replace constant lighting/headroom values with honest measured/unknown states. Depth occlusion is not navigation or hazard proof. Preserve the known placement reticle and camera tracked-pose fixes. Use one common anchor-local frame; do not persist a device-session world pose.

Complete final asset authoring and the Generic animation/rigging integration, not just placeholders. Preserve original source models; produce versioned derivatives, native clips, LODs, trusted contact profiles, signed manifests and independent reopen/validation evidence. Use headless Blender as documented. Generated or procedural art is acceptable only if it meets the same final visual, deformation, contact and fit gates. If required art cannot be completed with available tools, report the exact missing deliverable and continue all independent engineering work; do not call placeholder art release-complete.

### Production services and persistence

Implement the required existing named services and additive contracts before generating clients. Include owned pet state, dwelling selection, auth, exact-version entitlement, content updates/revocation, idempotency, offline outbox/replay, approved memories/deletion and privacy-filtered telemetry.

Correct the asset-version uniqueness/FK issue described in the services annex through a forward migration. Test cross-user isolation and duplicate/gapped event handling. Keep fetch unranked and nonmonetary; clients cannot grant themselves currency, inventory, scores or arbitrary bond changes.

Production session entry requires the specified online entitlement check; active-session outage behavior follows receipt validity and the bounded continuation policy. Do not mistake development preset shortcuts or the existence of `ConnectivityPolicy` for integrated production enforcement. Restore identity and dwelling choice after restart, then request a new local AR placement.

## 4. Model enhancement is a separate track

First implement a null `IIntentSource` and complete envelope validation. Complete the local companion regardless of inference availability. Then pursue M6/W19 when its prerequisites and device/runtime capabilities are available.

A model may suggest only validated allowlisted high-level intents. It cannot choose coordinates, clip names, forces, transactions, scores or executable text. Reject stale/expired/wrong-catalog/wrong-target output without interrupting play. Keep one request outstanding and respect memory/thermal/latency limits.

Do not claim GPU/NPU support from a chipset name or claim a model fits from parameter count. Benchmark the actual licensed, pinned artifact/runtime during a full AR session. Leave the supplement disabled if its quality or resource gates fail. Do not redefine completion of the core AI companion as requiring free-form chat or cloud inference; those are outside this specification.

## 5. Work autonomously and verify honestly

Begin with a concise execution plan and then implement the first slice in the same session. Continue through subsequent milestones as resources permit. Make routine implementation decisions from the specifications and source without repeatedly asking for confirmation. Where a missing external decision blocks one path, record the exact dependency, continue independent work, and request only the information actually needed.

Use existing authorized tools and configured development resources. Do not expose secrets, invent credentials, weaken trust gates to make a demo start, provision paid services, deploy to production, submit to app stores or alter production data without explicit authorization. Prepare the code, staging configuration, migrations, runbooks and reviewable release artifacts first. Missing release access does not prevent completing local implementation and tests.

Run relevant pure logic, contract, migration, assembly, Unity EditMode/PlayMode, asset and device tests as specified. Add tests for real invariants and failure modes, not only mirrored implementation. A successful build, a test count, or a screenshot alone does not prove the application is complete. Inspect current test result files and artifact versions. Never label skipped/blocked/unavailable checks as passed. Fix regressions in this work rather than deleting checks or lowering assertions without an explicit justified specification change.

If a required Unity editor, simulator, physical device, signing identity or asset tool is unavailable, state what could not be executed and what remains unverified. Build and test the independent components available to you. Never simulate evidence or claim Android/iOS parity from one platform.

## 6. Maintain the handoff and reviewable history

Use root `HANDOFF.md` as the canonical handoff; do not create a conflicting lowercase `handoff.md`. Update it at every milestone and before ending any session with:

- Current date, branch, source commit and any uncommitted work.
- Completed work IDs and requirement IDs, with exact implementation paths.
- What actually runs now and how to run it.
- Fresh test/build/device evidence, commands, result paths and asset/config versions.
- Failed or unrun gates and exact external blockers.
- Decisions/ADRs changed and any specification conflict resolved.
- Next concrete task, relevant files and expected acceptance result.

Keep historical evidence labelled as historical. Commit coherent implementation slices with descriptive messages on the agreed working branch; include handoff and traceability updates. Preserve unrelated work, inspect staged diffs for credentials/artifacts, and never force-push or rewrite shared history. Use normal pushes only when repository permissions and the user's working-branch instructions allow them. Production release approval is separate from Git progress.

When nearing a session/context limit, write a precise resumption checkpoint before stopping. On resumption, read the handoff and continue from that checkpoint, not from a new plan or a new scaffold.

## 7. Completion criteria and final report

The application is complete for the core scope only when M0–M5 and all applicable requirements pass with final assets, real integrated services and the specified Android/iOS evidence. An external blocker or an unavailable device means the relevant gate is still open, even if all code has been written. Report M6 separately.

Deliver the working source, tests, generated/validated assets and profiles, contracts and migrations, reproducible build/run instructions, CI configuration, runbooks, build artifacts with provenance, updated traceability and `HANDOFF.md`. Final report: what the end user can do, what was tested and on which devices, artifact/commit references, remaining blockers and the exact next action. Do not claim full completion while any required gate remains open.

Start now: inspect the checkout and handoff, confirm M0 baseline, then implement the first player-controlled fetch slice using the existing pet/placement architecture. Continue toward the full companion, dwelling and fetch application.
