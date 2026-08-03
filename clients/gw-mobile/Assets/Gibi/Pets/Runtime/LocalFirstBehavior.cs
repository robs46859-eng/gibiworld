// GW-ARCH-001 section 8.3 — "The app SHALL contain a complete local behavior library
// sufficient for placement, idle, direct cues, training, fetch, and unranked courses.
// AI outage is a feature degradation, not a gameplay outage."
//
// This goes further than a fallback, deliberately.
//
// A conventional design calls the model, waits, and substitutes something simpler on
// failure. That makes the AI LOAD-BEARING: when it is slow or absent the pet is visibly
// worse, so the child learns the pet is better when connected, and attachment attaches to
// the connected state.
//
// Here the local library is the BASELINE and AI is a SUPPLEMENT. Local always produces a
// valid intent, immediately, without waiting. An AI response is accepted only if it
// arrives inside the budget and validates -- otherwise nothing happens, because nothing
// was pending. There is no fallback path because there is no failure state.
//
// Consequences:
//   * section 15's "AI intent p95 <= 2.5 s ... do not block play" is satisfied by
//     construction: play was never blocked, because play never waited.
//   * offline and online produce the same CAPABILITY. Only flavour differs.
//   * a break, a timeout, an outage, or a deliberate offline session are all identical
//     from the child's side -- which is the healthy outcome and is why it is built this way.
using System;
using Gibi.Core;

namespace Gibi.Pets
{
    /// <summary>
    /// The complete offline behaviour set. Section 8.3 requires this to cover placement,
    /// idle, direct cues, training, fetch, and unranked courses with no network at all.
    /// </summary>
    public static class LocalBehaviorLibrary
    {
        /// <summary>Every intent the pet can ever express. The AI selects FROM this set — never beyond it.</summary>
        // Catalog revision 2 (GW-ARCH-002 step 6). This was eight hardcoded strings and was
        // a SECOND source of truth for what the pet could do -- exactly the drift pattern
        // that produced every finding in GW-ARCH-002 section 1.4. It now delegates, so the
        // catalog cannot grow without this agreeing.
        public static string[] AllIntents => IntentCatalog.AllIds();

        /// <summary>
        /// Compatibility shim over <see cref="IntentPolicy.Select"/>. Prefer the policy
        /// directly: it returns the modifiers too, and the modifiers are where the pet's
        /// sense of life actually lives. This overload throws that away and keeps only the
        /// intent id, which is why the old eight-intent version felt mechanical.
        /// </summary>
        public static string Choose(long personalitySeed, int bond, int energy,
                                    float settling, long tickIndex)
        {
            var engagement = new EngagementEstimate(
                arousal: 0.5f, perseveration: 0f, settling: settling, fatigue: 0f);

            var ctx = new PolicyContext(
                personalitySeed: personalitySeed,
                bond: bond,
                energy: energy,
                engagement: engagement,
                care: CareProfile.None,
                targets: new AvailableTargets(toys: 1, spatialObjects: 1, pets: 0),
                localHourOfDay: 14,
                tickIndex: tickIndex,
                lastIntentIndex: -1,
                repeatRunLength: 0);

            return IntentPolicy.Select(in ctx).IntentId;
        }

        public static bool IsKnownIntent(string intent) => IntentCatalog.IsKnown(intent);
    }

    /// <summary>
    /// Accepts an AI intent ONLY as a supplement to an already-chosen local intent.
    /// Never awaited, never required, never surfaced as an error when absent.
    /// </summary>
    public sealed class AiSupplementPolicy
    {
        /// <summary>Section 15: AI intent p95 <= 2.5 s. Anything later is simply ignored.</summary>
        public const long SupplementBudgetMs = 2500;

        private readonly IMonotonicClock _clock;
        public AiSupplementPolicy(IMonotonicClock clock) { _clock = clock; }

        /// <summary>Count of AI intents that arrived too late. Diagnostic only — never player-visible.</summary>
        public int LateArrivals { get; private set; }
        public int Accepted { get; private set; }

        /// <summary>
        /// The pet has ALREADY acted on <paramref name="localIntent"/>. This decides only
        /// whether a later-arriving AI suggestion may colour the next beat.
        /// </summary>
        public string Resolve(string localIntent, string aiIntent, long aiRequestedAtMs,
                              int expectedContextRevision, int aiContextRevision)
        {
            // No response, empty response, or an outage: keep local. Nothing was pending,
            // so there is nothing to recover from.
            if (string.IsNullOrEmpty(aiIntent)) return localIntent;

            // Section 8.2: unknown intents are rejected. The AI may only choose from the
            // same set the local library already covers, so acceptance can never expand
            // what the pet is capable of.
            if (!LocalBehaviorLibrary.IsKnownIntent(aiIntent)) return localIntent;

            // Section 8.2: wrong contextRevision is rejected.
            if (aiContextRevision != expectedContextRevision) return localIntent;

            // Late is treated exactly like absent.
            if (_clock.ElapsedMilliseconds - aiRequestedAtMs > SupplementBudgetMs)
            {
                LateArrivals++;
                return localIntent;
            }

            Accepted++;
            return aiIntent;
        }

        /// <summary>
        /// Section 8.3: "The UI SHALL not display an error unless the player explicitly
        /// opens online memory or dialogue features." Absence of AI is not an event.
        /// </summary>
        public static bool ShouldSurfaceToPlayer(bool playerOpenedOnlineFeature)
            => playerOpenedOnlineFeature;
    }
}
