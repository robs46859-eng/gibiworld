# ADR-009: Guardian care context and positive redirection

- **Status:** PROPOSED — requires a §8 amendment and legal confirmation
- **Clinical authority:** Robert Smith (trained in child psychology); thresholds are his to set
- **Date:** 2026-08-01
- **Design:** `docs/design/pet-brain-and-care-context.md`

## Context

A guardian knows things about a child that the system cannot and should not infer. Three
risks motivate this: distressing pet behaviour that is neutral for most children,
comfort-seeking that substitutes for human contact, and misplaced trust in the AI.

The obvious implementation — telling the model about the child — is the wrong one. §8.2
already bars sensitive or inferred attributes from the provider context, and a child's
regulation needs are the most sensitive attribute in the system.

## Decision

**1. The care context never reaches the AI provider.** Steering happens by narrowing the
server-published intent allowlist, reweighting the deterministic §8.1 arbiter, and
changing pacing. The model receives the same minimal context it always did; it simply has
a smaller menu. It never learns why, because it never needs to.

**2. Guardians select accommodations, not diagnoses.** No condition, no clinical note, no
free-text field. "My child likes routine" is a preference; "my child has autism" would be
health data under a different legal regime and would tell the system nothing more useful.

**3. Inference is continuous and ephemeral.** The estimator runs constantly, reads only
touch tempo and dwell time, and is never persisted, transmitted, or sent to the provider.
Continuous inference is safe *because* nothing survives the session — a system that cannot
retain a conclusion cannot build a profile, leak one, or be asked for one.

**4. The pet never asks the child to confirm a state.** No mood picker, no "are you
okay?", no check-in. Asking makes a child self-conscious, constitutes a safety-assessment
question an automated system is unqualified to pose, and degrades to noise within a week
while the intrusion remains.

**5. Repetition is never extinguished.** Repetitive play is frequently self-regulation.
The system cannot distinguish joyful, regulating, and distressed repetition through a
touchscreen, so the response is identical for all three and safe for the case where
interruption would cause harm. Redirection adds texture and offers alongside; it never
caps, refuses, comments, or replaces. Fatigue changes *how* the pet responds, never
*whether*.

**6. Nothing is counted where the child can see.** No achievement for repetition. §1.2
already forbids coercive streaks; rewarding repetition would manufacture compulsion.

## Consequences

- §8.1 gains priority tier 3.5 for redirection, above the needs scheduler and below player
  cues, so a redirection always beats an AI suggestion but never interrupts the child.
- §8.2's intent enum becomes per-session and profile-dependent, published by the server.
- §13.2 gains three rows: care profile flags (Confidential, never egressed), redirection
  counters (Internal, 30-day aggregate), guardian rhythm summary (derived, never stored).
- `pet_state.energy` may modulate animation quality but MUST NOT gate action availability.
  This is normative, not a tuning choice.
- 14 new EditMode tests, several written as negative assertions because the failure mode
  is the system starting to do something harmful rather than ceasing something useful.

## Alternatives considered

**Send the care context to the AI provider as prompt context.** Rejected: it is exactly
the sensitive-attribute egress §8.2 forbids, and it buys nothing the allowlist cannot
achieve deterministically.

**Ask the child how they are feeling.** Rejected on three grounds in decision 4.

**Cap or cool down repeated actions.** Rejected: a cap is a refusal, and it risks
interrupting self-regulation. This is the decision the whole design turns on.

**Real-time guardian alerts on distress signals.** Rejected: an automated system inferring
distress in a child will be wrong often, the cost of a false positive lands on someone
with no ability to contest it, and a real-time alert converts a moment of comfort into an
incident.

## Open questions blocking ACCEPTED status

1. ~~**Clinical review by a child psychologist.**~~ **RESOLVED 2026-08-01.** Robert is
   trained in child psychology and owns the guardrails. The thresholds in the design
   document are engineering placeholders and are his to set; treat every numeric
   threshold in section 4, 6, and 6a as provisional until he replaces it.
2. **Whether care profiles constitute health data** under COPPA / GDPR-K. The design
   argues not, because they encode accommodations rather than conditions. That needs legal
   confirmation, not an architect's opinion.
3. **Sequencing against the §0 age gate.** This feature matters most for the under-13
   cohort that §0 keeps disabled pending verifiable parental consent, so it likely ships
   with that work rather than before it.
