# Trainable Pet Brain, Positive Redirection, and Guardian Care Context

**Status:** DESIGN PROPOSAL — requires ADR-009 and a §8 amendment before implementation
**Author:** drafted 2026-08-01
**Scope:** GW-ARCH-001 §8 (pet simulation and AI behavior), §9.1 (training), §13 (privacy)

---

## 0. A stated limitation

This document is written by a software architect, not a clinician. It describes
**engineering guardrails that reduce foreseeable harm**. It does **not** describe a
system that detects, diagnoses, monitors, or treats any mental health condition, and it
must never be presented as one.

Before this ships to anyone under 18, it needs review by a child psychologist and by the
child-privacy review §0 already requires. Several decisions below are deliberately
conservative because the failure modes are asymmetric: a pet that is slightly too calm
costs nothing; a pet that reinforces isolation costs a great deal.

---

## 1. The problem

Three distinct risks, often conflated:

1. **Trigger exposure** — a pet behaviour that is neutral for most children is
   distressing for one. Sudden movement, loud reactions, a pet that "disappears,"
   simulated distress.
2. **Unhealthy comfort-seeking** — the avatar becomes a substitute for human contact
   rather than a bridge to it. Long sessions, late-night use, repetitive soothing loops.
3. **Misplaced trust in the AI** — the child concludes the pet is alive, understands
   them, or is a confidant with judgement.

A guardian knows things the system cannot infer. The system must be able to *act* on
that knowledge without *storing* or *transmitting* it.

---

## 2. The central architectural decision

> **The care context never reaches the AI provider.**

§8.2 already states the AI provider "SHALL NOT receive camera frames, precise
coordinates, contacts, advertising ID, or raw voice," and that memory proposals with
"sensitive or inferred attributes SHALL be rejected." A child's mental-health status is
the most sensitive attribute in the system.

So the design does **not** work by telling the model about the child. It works by:

1. **Narrowing the intent allowlist** the server publishes for that session
2. **Reweighting the deterministic arbiter** (§8.1) so gentler intents win more often
3. **Changing pacing** — transition speeds, session rhythm, rest cadence
4. **Adding a redirection layer** that proposes alternatives at a priority AI cannot override

The model receives the same minimal context it always did. It simply has a smaller menu,
and the client interprets its choices more gently. **The AI never learns why, because it
never needs to.**

This is the same pattern as §4's adapter rule: put the sensitive thing behind a boundary
and make crossing it structurally impossible rather than a matter of discipline.

---

## 3. Care profiles are accommodations, not diagnoses

A guardian never enters a condition, diagnosis, or clinical note. There is no free-text
field. They choose from **behavioural accommodations** — descriptions of what helps, not
statements about what is wrong:

| Profile | What the guardian sees | What it changes |
|---|---|---|
| `GENTLE_SENSORY` | "Calmer movement and quieter reactions" | Caps gait at `Walk`, halves particle budget, mutes startle reactions, lengthens all animation blends |
| `PREDICTABLE` | "Fewer surprises — my child likes routine" | Removes `CURIOUS_SNIFF` / `SEEK_SHADE`; pet greets and settles the same way each session |
| `GENTLE_PACING` | "Encourage breaks" | Rest cues arrive sooner; the pet naps rather than the app nagging |
| `EXTRA_ENCOURAGEMENT` | "More praise, less correction" | Training never shows failure; `success` clip weight raised |
| `COMPANION_BALANCE` | "Encourage time with people too" | Redirection favours outward suggestions; long-session cues strengthen |

**Why this framing matters.** "My child likes routine" is not health data in any
meaningful sense — it is a preference, like a difficulty setting. It is storable,
non-stigmatising, and a child who discovers it feels accommodated rather than labelled.
"Child has autism" would be health data, would require a different legal regime under
§13.2, and would tell the system nothing more useful.

Multiple profiles may be active. They compose by taking the **most conservative** value
for every parameter.

---

## 4. Positive redirection

The pet never says no. It offers something else.

Redirection sits at **priority 3.5** in the §8.1 arbiter — above the needs scheduler,
below player cues. It cannot interrupt a child mid-action, and it can never override
safety. But it outranks AI intent, so a redirection always beats a model suggestion.

```
0  Safety override      (unchanged)
1  Session rule         (unchanged)
2  Direct player cue    (unchanged)
3  AI high-level intent (unchanged)
3.5 REDIRECTION         ← new
4  Needs scheduler      (unchanged)
5  Ambient animation    (unchanged)
```

### Triggers and responses

Every response is **in-fiction** — the pet does something. No system message, no modal,
no "you have been playing for a while." The child experiences a pet that got sleepy, not
an app that judged them.

| Signal | The pet does | Never |
|---|---|---|
| Session > profile threshold | Yawns, lies down, settles | Show a timer or a warning |
| Same interaction repeated many times | Brings a different toy, looks toward the door | Refuse to repeat it |
| Late hour (device clock, local) | Curls up, breathes slowly, dims | Lock the child out |
| Long stillness with pet held close | Rests head, stays; softens gradually | Withdraw abruptly |
| Repeated failed training attempts | Succeeds at something easier | Show failure feedback |

**The stillness row is the delicate one.** A child seeking comfort from the pet is not
doing something wrong, and abrupt withdrawal at exactly that moment is the cruelest
possible response. The pet stays. It becomes calmer over minutes, and only much later
offers something outward. Comfort is not interrupted; it is gently widened.

### The three hard rules

1. **Always toward, never away.** Redirection proposes an activity. It never removes one.
2. **Never shame.** No copy implies the child did something wrong. No streaks, no
   "you've been here too long." §1.2 already forbids coercive daily streaks; this extends
   the same principle to wellbeing nudges.
3. **Never a substitute for a person.** The pet may not claim to understand feelings,
   may not offer advice, and may not position itself as someone to talk to instead of a
   human. When a child expresses distress, the pet is warm and present — and the system
   does not attempt to counsel.

---

## 5. What the guardian sees

Subtle, per the product direction. In the child's UI: **nothing**. No badge, no mode
indicator, no setting they can find and feel singled out by.

In the guardian's area:

- A short **rhythm summary** — roughly how often and how long, in plain language
  ("most days, usually after school"). Not a dashboard. Not minute counts.
- The **memory list** — every fact the pet has learned, each individually deletable.
  This already exists per §8.2; it simply becomes visible to the guardian too.
- The **profile switches** above.

Deliberately absent: transcripts, session replays, emotional inference, anything
resembling surveillance. A guardian should be able to answer "is this healthy?" without
being able to answer "what did my child do at 4:15pm."

**The child is told, once, in plain language, that a grown-up helps set up their pet and
can see what it remembers.** Covert monitoring of a child by a system they believe is
private is not acceptable, regardless of intent.

---
---

## 6. Inference — continuous, ephemeral, never confirmed

The system **should** infer. A pet that waits to be told how a child is doing is useless,
and a pet that *asks* is worse than useless.

The distinction that matters is not whether to infer. It is **what the inference becomes**:

| Infer in order to... | Verdict |
|---|---|
| Soften movement, slow a transition, settle instead of bounce | **Yes — do this continuously** |
| Choose which intent to favour in the next 100 ms | **Yes** |
| Ask the child "are you okay?" | **Never** |
| Announce that something was noticed | **Never** |
| Persist a conclusion about the person | **Never** |
| Report a state to anyone | **Never** |

### Never ask for confirmation

The pet does not ask the child to confirm a state, ever. Not "are you feeling sad?", not
"do you want to take a break?", not a mood picker, not an emoji check-in.

Three reasons, in order of weight:

1. **Asking makes a child self-conscious.** A child who is quietly comforted by an animal
   and gets asked to name why is no longer being comforted.
2. **Asking is a safety-assessment question**, and an automated system is not qualified to
   ask one or to handle the answer.
3. **A child will learn the "right" answer.** Any confirmation prompt becomes a thing to
   dismiss within a week, which means the signal degrades to noise while the intrusion
   remains.

The pet responds the way an actual animal does: it notices something, and its behaviour
changes. Nobody says anything about it.

### What is estimated

A deterministic, on-device estimator reads interaction signals and outputs **continuous
parameters**, never categories or labels:

| Signal (all local, no new permissions) | Contributes to |
|---|---|
| Tap tempo and rhythm regularity | `arousal` |
| Repetition of one interaction | `perseveration` |
| Stillness while the pet is near | `settling` |
| Session length and time-of-day bucket | `fatigue` |
| Cue-to-response latency drift | `engagement` |

These bias the §8.1 arbiter — weighting gentler intents, lengthening blends, lowering
gait caps, favouring settle over play. The effect is felt, never displayed.

**Deliberately excluded:** camera frames, microphone, facial expression, heart rate, and
any inference about emotion as such. §8.2 already bars camera and raw voice from leaving
the device; this design does not read them locally either. The estimator uses only how the
child touches the screen and how long they stay.

### Ephemeral by construction

The estimate lives in memory for the session and is **never written to disk, never
transmitted, never included in telemetry, and never sent to the AI provider.** It is
recomputed from scratch on every launch.

This is what makes continuous inference safe. A system that infers constantly but
remembers nothing cannot build a profile, cannot leak a conclusion, and cannot be
subpoenaed for one. The estimator is a thermostat, not a diary.

The only thing that survives a session is a set of **anonymous counters** — how many
times redirection triggered, bucketed coarsely — which is what §5's rhythm summary is
built from. Those counters describe the *pet's behaviour*, not the child's state.

### Escalation stays narrow

Persistent redirection over many days adds one observational sentence to the guardian's
summary:

> "Ollie has been getting a lot of visits lately, including some late ones."

The system does not name a concern, suggest a cause, use clinical language, alert anyone
but the guardian, or notify in real time — a real-time alert would turn a moment of
comfort into an incident.

**The line:** inference may steer the pet continuously and invisibly. It may not produce a
statement about a child that outlives the session. An automated system inferring distress
in a child will be wrong often, and the cost of being wrong lands on someone with no
ability to contest it.

---
---

## 6a. Repetition — the case that decides the design

**Worked example:** a child asks the pet to jump in place a hundred times a day. Or to
run in circles for an hour.

This is the hardest case, and getting it right constrains everything else.

### Why the obvious responses are wrong

**"Cap it."** A repetition limit is a refusal. It teaches a child that wanting something
a lot makes it stop being available.

**"Comment on it."** *"Wow, you really like jumping!"* names the behaviour and makes the
child self-conscious about it. Same failure as asking a confirmation question, §6.

**"Redirect away from it."** This is the one that looks responsible and is the most
harmful. Repetitive, predictable play is a **regulation strategy** — for many children,
especially autistic children, it is how they self-soothe. A system that interrupts it is
interrupting the thing that is working.

**And the system cannot tell the difference.** Joyful repetition, self-regulating
repetition, and distressed compulsion look identical through a touchscreen. Any design
that responds differently to them is guessing about a child's inner state and acting on
the guess. Given three indistinguishable causes and one asymmetric harm, the response
must be the same for all three — and it must be safe for the one where interruption
would hurt.

### The rule

> **Never extinguish the repetition. Add texture to it.**

The hundredth jump happens. It is just not quite identical to the first.

| The pet does | The pet never |
|---|---|
| Complies every time, without hesitation | Refuses, caps, or cools down |
| Varies micro-timing and landing so it stays alive | Repeats a byte-identical loop |
| Around a threshold, adds a flourish — a happy shake, a tail flick | Announces or counts anything |
| Later, brings a ball **while still willing to jump** | Swaps the activity for another |
| Eventually flops down happily, then gets up and jumps again | Becomes unavailable |

The offer is always **additive**. The ball appears next to the jumping, never instead of
it. If the child ignores the ball and asks for jump 101, they get jump 101.

### Fatigue is expressive, not restrictive

§1.2 excludes pet injury, punishment, and hunger loss. A pet that gets *too tired to
continue* is a soft refusal wearing a costume, so tiredness may change **how** the pet
responds — never **whether**.

After long repetition: the jump gets slightly lower, the landing softer, a contented flop
between reps. It reads as a happy, tired animal. It never reads as "no."

`pet_state.energy` therefore modulates animation quality and never gates action
availability. This is a normative constraint on the implementation, not a tuning choice.

### Nothing is counted where the child can see

No achievement for "jumped 100 times." No streak, no badge, no counter.

§1.2 already forbids coercive streaks. This is the same principle pointed at wellbeing:
rewarding repetition **manufactures** compulsion. The safest system is the one that finds
the hundredth jump exactly as unremarkable as the first.

Internal counters exist only to drive micro-variation and the coarse rhythm summary in
§5. They are never surfaced, never gamified, and never persisted per-session.

### What actually changes

Almost nothing visible — and that is the point. A child who wants an hour of circles gets
an hour of circles, from a pet that stays interested, varies a little, occasionally offers
something else without withdrawing anything, and never once suggests they should be doing
something different.

If a guardian later sees "lots of visits lately," that is the *only* place this surfaces —
as an observation for an adult, never as a correction to the child.

---

## 7. Data classification

Extends the §13.2 table:

| Data | Class | Retention | Rule |
|---|---|---|---|
| Care profile flags | Confidential | Until changed or account closed | **Never sent to the AI provider.** Never leaves the entitlement boundary. Not exported in telemetry. |
| Redirection counters | Internal | 30 days rolling | Aggregate counts only. No timestamps beyond coarse time-of-day bucket. |
| Guardian rhythm summary | Confidential | Derived, not stored | Computed on read from counters; never persisted as a record about the child. |

The care profile is stored against the **pet**, not the child, and expressed as behaviour
parameters. Reading the database reveals a pet configured for calmer movement — not a
statement about a person.

---

## 8. How the training loop teaches AI literacy

The trainable brain and the safety design are the same mechanism viewed from two angles.

- **Memory is visible and deletable.** A child can see every fact and remove one — and
  watch the pet's behaviour change. That is a more honest model of AI memory than most
  adults hold.
- **Training changes preference, never capability.** §8.2 forbids AI from selecting
  animations, coordinates, or forces. A trained pet *wants* different things; it cannot
  *do* new things. The child learns that shaping a system is not the same as extending it.
- **The smart part is optional.** §8.3 requires a complete offline behaviour library. When
  AI is unavailable the pet still plays. A child who notices this has learned something
  many adults have not.
- **Limits are discovered, not lectured.** The pet cannot be trained into unkindness
  because those intents do not exist in the enum. A child who tries finds a wall, not a
  scolding.

---

## 9. What must be decided before building

1. **Clinical review.** Non-negotiable. Section 4's trigger thresholds are engineering
   guesses and should not survive contact with an expert unchanged.
2. **Whether care profiles are health data under COPPA/GDPR-K.** §7 argues not, because
   they are accommodations rather than conditions. That argument needs legal confirmation,
   not an architect's opinion.
3. **Age gate interaction.** §0 keeps under-13 accounts disabled pending verifiable
   parental consent. This feature is most valuable exactly for that cohort, so it likely
   ships *with* the under-13 work rather than before it.
4. **Whether redirection is on by default.** Recommendation: yes, at conservative
   thresholds, for everyone. Wellbeing pacing should not be a setting only attentive
   guardians find.
