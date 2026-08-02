# ADR-010: Age assurance and guardian consent

- **Status:** PROPOSED — requires legal review before launch
- **Date:** 2026-08-01
- **Design:** `docs/design/age-assurance-and-consent.md`
- **Consent draft:** `docs/legal/guardian-consent.md`

## Context

§0 disables under-13 accounts "until verifiable parental consent, guardian controls, child
privacy review, and store declarations are implemented." ADR-009 adds care-context
features involving an adult observing a minor's play.

Initial launch excludes the UK and EU. Age assurance and guardian consent are built
**everywhere anyway**, including the US.

## Decision

**1. Build it everywhere, not only where required.** Retrofitting consent means
re-consenting an existing userbase and disabling every account that cannot be reached.
App stores also enforce data declarations independently of law.

**2. Three tiers.** Adult 18+ (self-declared DOB); Teen 13–17 (DOB, plus guardian consent
for care-context features); Child under-13 (verifiable consent before an account exists).

**3. Care-context consent is required for ALL minors**, not just under-13. A guardian
observing a 16-year-old's play rhythm warrants consent as much as at 11.

**4. Neutral age screen.** A date field, never "are you over 13?" A binary question with a
visible consequence teaches a child which answer unlocks the app. Store the **band**, never
the date (§13.2 minimisation).

**5. The database is the enforcement point.** A constraint trigger refuses an ACTIVE
under-13 row unless a current GRANTED consent exists, and withdrawal disables the account
rather than silently degrading it. No service bug, admin action, or later migration can
produce an unconsented active under-13 account.

**6. Store the fact of verification, never the evidence.** A boolean, a method, a
timestamp, a document version, a guardian contact. Never a card number, signature image,
or ID document.

**7. Consent expires at 24 months.** Consent a guardian has forgotten giving is not
meaningful consent.

**8. The consent form is written on the service-animal model** — what the companion is
trained to do, what it is not, what stays the guardian's responsibility, and how to stop.
Those documents exist because a supportive animal is genuinely helpful *and* genuinely
misunderstood, and the gap between assumption and reality is where harm happens. The same
gap exists here, wider.

The single most important passage is §2, *What Ollie is NOT* — specifically that the app
**cannot recognise a crisis and will not alert anyone.** A guardian who believes otherwise
has been actively misled by our silence.

## Consequences

- New migration `0003` with `guardian_consents` and `pet_care_profiles`, replacing
  migration 0001's CHECK-based age gate with a cross-table trigger.
- `Gibi.Core.AgeAssurance` mirrors the rule client-side so the UI never offers a path the
  server refuses. Unknown age **fails closed**.
- Six new EditMode tests, mostly negative assertions — the failure mode is the system
  *allowing* something it must not.
- Two new CI gates asserting the database refuses an unconsented under-13 activation and
  disables on withdrawal.
- Guardian controls (view/delete memories, set profiles, export, delete account, revoke)
  become shipping requirements. Consent without control is theatre.

## Open items for counsel

1. **VPC method.** Recommendation: card micro-transaction primary, signed-form fallback —
   lowest friction at acceptable privacy cost, no biometrics, processor absorbs the
   sensitive data. Robert's decision.
2. **UK/EU.** Deferred but designed for. GDPR-K digital-consent age varies 13–16 by member
   state; the UK Children's Code adds design duties beyond consent.
3. **Care profiles as health data** under COPPA, GDPR Art. 9, and US state law. The design
   argues accommodations are not conditions and no diagnosis is ever collected.
4. **Adequacy of the §2 crisis disclaimer** — the sentence most likely to be tested.
5. **Reading level.** Target grade 8; a consent form a guardian cannot read is not
   informed consent.

## Note on clinical authority

ADR-009's clinical-review question is **resolved**: Robert is trained in child psychology
and owns the guardrails. Every numeric threshold in the ADR-009 design document is an
engineering placeholder pending his values.
