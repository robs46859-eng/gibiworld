# ADR-013: Complete the AI companion, dwelling, and player-controlled fetch

- Status: PROPOSED implementation decision, authored under the requested architecture task
- Date: 2026-09-05
- Specification: [GW-ARCH-003 v1.0.0](../GW-ARCH-003-AR-Companion-Build-Specification.md)
- Source baseline: `73b36c758aaef98f23c206729fd31466a8cba190`
- Adoption changes specification precedence; this document does not claim an approved production release.

## Context

The repository has a local AR demonstration with automatic fetch/rest sequencing, a signed dog, a decorative dwelling whose aperture is too small, and Legacy animation substitutions. The requested product needs player-controlled throws, a fully functional home, and an AI pet whose actions remain coherent during tracking loss, interruptions, and network outages.

GW-ARCH-002 §7 specifies an on-device language model, while the current intent schema says no model producer ships. Older build-state documents also conflict with subsequent provider and asset fixes. A new implementation baseline must identify which requirements are retained and which need adoption before changing behavior.

## Decision and precedence on adoption

1. Adopt GW-ARCH-003 and its annexes for this companion/fetch completion scope. Retain GW-ARCH-001 product/security requirements and accepted ADR-008/011/012 package/provider decisions.
2. Sequence a complete deterministic companion release before the model enhancement. This changes the mandatory implementation ordering of GW-ARCH-002 §7 and §11; it does not remove the typed intent-source interface or its restrictions. Model runtime/backend/accelerator claims require measured compatibility and quality evidence.
3. Replace the P0 Legacy presentation with Generic Animator/Playables and rigging, and remove demo-only clip substitutes from the release hero profile. This implements GW-ARCH-001 §6.3 and supersedes the P0 presentation exception.
4. Use fixed-step kinematic toy flight and local grid navigation for one small play area. No package upgrade is part of this decision. Engine collision queries can supply observations, but recorded snapshots drive deterministic replay.
5. Preserve the production requirement for measured spatial safety. Introduce an explicitly opt-in constrained local practice mode for devices lacking semantic/headroom evidence only if approved as a scoped exception to GW-ARCH-002 §5.4; its status must say that environmental validation is limited. Until that exception is adopted, unknown required evidence disables AR fetch and falls back to companion view. A preview or user acknowledgement alone never counts as proof of environmental safety.
6. Clarify connectivity: production session entry requires an online entitlement validation; active-session continuation is bounded by the existing 72 h revalidation plus 24 h unreachable-only grace policy and receipt validity. Reboot cannot reset grace. Explicit expiry, revocation, or kill switch overrides grace immediately upon receipt. This resolves the difference between the `RequiresOnlineSessionStart` comment and `MayStartSession`'s cached-window implementation. No infinite offline startup is authorized.
7. Keep local spatial layouts out of persistence endpoints. Persist pet/home selection, preferences, and non-spatial game events; re-place after restart. Persistent VPS placement remains a later separately gated feature.
8. Gate under-13 availability independently of database trigger presence. ADR-009 and ADR-010 are proposed; this architecture does not convert their policy statements or legal assertions into accepted launch decisions.

## Alternatives

- Rewrite in another engine: discards existing verified placement, trust, and behavior work without a demonstrated benefit.
- Add unrestricted model control: violates the existing intent, motion, and trust contracts.
- Keep the demo loop as the final interaction: cannot meet the end-user fetch requirement.
- Hide the dog inside the existing house: does not solve aperture, navigation, or visible entry defects.
- Use dynamic Rigidbody flight as gameplay authority: makes the preview/landing and replay guarantees harder to satisfy for this small interaction.

## Consequences and gates

Art authoring is a critical dependency, especially skinning, native pickup/carry/drop/rest, and a fitted interior. Runtime integration can proceed against validated primitives while artists work, but a primitive demo does not pass the final asset gate.

Acceptance requires the requirement matrix in GW-ARCH-003's delivery annex. Adoption must include the corresponding OpenAPI/schema changes when implemented, updated traceability, recorded AR regressions, asset fixtures, and device results. No existing gate is marked passed by this ADR.
