# Age Assurance and Guardian Consent

**Status:** DESIGN — see ADR-010
**Scope:** GW-ARCH-001 §0 (age gate), §13.2 (data classification), §19 (age-gate policy,
app-store privacy declarations)

> **Not legal advice.** This document specifies engineering and product behaviour. The
> consent form in `docs/legal/guardian-consent.md` is a **drafting input for counsel**,
> not a reviewed instrument. Nothing ships without a lawyer.

---

## 1. Position

Initial launch excludes the UK and EU. The design still implements age assurance and
guardian consent **everywhere, including the US**, for three reasons:

1. **It is the right default.** A child using an emotionally responsive AI companion
   should have an adult who has read what it does. That is true regardless of jurisdiction.
2. **Retrofitting consent is far harder than building it in.** Adding verifiable consent
   after a userbase exists means re-consenting everyone, and any account you cannot
   re-consent must be disabled.
3. **App stores enforce independently of law.** Both stores require accurate age ratings
   and data declarations, and a "kids" categorisation triggers review criteria stricter
   than COPPA in several respects.

§0 already disables under-13 accounts pending "verifiable parental consent, guardian
controls, child privacy review, and store declarations." This design is that work.

---

## 2. Three tiers

| Tier | Who | Requirement |
|---|---|---|
| **A — Adult** | 18+ | Self-declared date of birth. Full features. |
| **B — Teen** | 13–17 | Self-declared DOB. **No guardian consent at all.** Care-context features require an acknowledged guardian link — see below. |
| **C — Child** | Under 13 | **Verifiable** guardian consent before any account exists. No account, no play. |

### Teens do not need guardian consent

COPPA reaches **under-13 only**; the federal threshold remains 13. The US state laws that
require parental consent for minors (UT, TX, LA, AR, FL) target **social media accounts**,
which GibiWorld is not. So a 13–17 account plays with no guardian involved.

**Care context is gated on the teen knowing, not on the guardian consenting.**

An earlier draft required guardian consent here. That was circular: it asked a guardian to
consent to their own configuration of their own teen's settings, which is ceremony rather
than protection. The substantive concern is different — a 15-year-old should not have an
adult watching their play rhythm **without being aware of it**.

So for a teen, care context requires an **acknowledged guardian link**: a guardian is
linked to the account, and the teen has been shown, in plain language, exactly what that
guardian can see. Transparency to the minor replaces consent from the adult. If no
guardian is linked — the common case for a teen who set the account up themselves — the
features simply do not appear.

Under-13 is unchanged: verifiable consent, because the account itself cannot exist without it.

---

## 3. The gate itself

### First run

Neutral date-of-birth entry. Not "are you over 13?" — a yes/no question teaches a child
the answer that unlocks the app. A DOB field with no visible threshold does not.

Store the **birth band** (`UNDER_13` / `13_17` / `18_PLUS`), never the date. §13.2's
minimisation principle, and the band is all any downstream check needs. The `users` table
already has `birth_band` and a CHECK constraint refusing an ACTIVE under-13 row.

### Neutral-age-screen rules

- No leading copy, no indication of which answer unlocks anything
- Not re-askable by retry — one answer per device install; changing it requires the
  guardian flow
- No inference of age from behaviour, name, or voice. If the system suspects an
  understated age it may re-verify, but it may not silently reclassify.

### Under-13 path

The account does not exist yet. Nothing is stored beyond an opaque pending-verification
token until consent completes. Abandoned flows expire in 30 days and delete.

---

## 4. Verifiable parental consent — method choice

COPPA-recognised methods, with real tradeoffs. **This is a product decision with cost,
friction, and privacy consequences, and it is Robert's to make.**

| Method | Friction | Privacy cost | Notes |
|---|---|---|---|
| **Card micro-transaction** ($0.50, refunded/donated) | Low | Card handled by processor; GibiWorld never sees a PAN | Widely used, well-understood |
| **Signed form return** (upload/scan) | High | Stores a signature image | The "print-and-send" method; robust, unpopular |
| **Government ID check** | High | Highest — identity document | Strong, but heavy for a pet app |
| **Facial age estimation** | Low | Biometric processing | Fast, but processes a face; conflicts with the spirit of §13.2 |
| **Video call with trained staff** | Very high | Live video | Not operationally viable at consumer scale |

**Recommendation: card micro-transaction as primary, signed-form upload as fallback.**
Lowest friction at acceptable privacy cost, no biometrics, and the payment processor
absorbs the sensitive data — GibiWorld stores only a boolean and a timestamp.

Whatever is chosen, the system stores: **consent granted (bool), method, UTC timestamp,
consent document version, guardian contact.** Never the evidence itself.

---

## 5. Re-consent triggers

Consent is not perpetual. It is re-sought when:

- The **consent document version** changes materially
- A **new category** of data or feature is introduced
- The child **crosses 13**, at which point the account converts to Tier B and the guardian
  is notified
- **24 months** elapse — a stale consent from a guardian who has forgotten the product is
  not meaningful consent

## 6. Guardian controls that must exist before launch

Consent without control is theatre. Each of these is a shipping requirement:

- **See** every memory the pet holds, and delete any of them individually (§8.2 already
  requires the deletion path)
- **Set** care profiles (ADR-009 §3)
- **View** the coarse rhythm summary (ADR-009 §5)
- **Export** all data about the child in a portable format
- **Delete** the account and everything in it, with a stated completion window
- **Revoke** consent, which disables the account rather than silently degrading it

## 7. What is NOT built

Stated explicitly so it does not drift in later:

- No behavioural age inference
- No biometric age estimation in v1
- No dark patterns in the age screen — no pre-filled adult DOB, no "skip"
- No account for an under-13 without completed verifiable consent, ever, including "trial"
- No retention of consent evidence beyond the boolean record
