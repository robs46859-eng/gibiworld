// GW-ARCH-001 sections 6.4, 10.2, 13.1 — connectivity is REQUIRED, even though a session
// in progress degrades gracefully.
//
// These are two different questions and they must not be conflated:
//
//   "Does the pet keep playing if the network drops mid-session?"   -> YES (section 8.3)
//   "May a pet run indefinitely with no server contact?"            -> NO
//
// Section 6.4 step 3 requires a CURRENT entitlement before instantiation, and section 10.2
// requires that "security revocation SHALL take effect immediately through status checks
// and cache invalidation." A pet that never phones home is a pet whose revocation never
// lands -- a compromised or withdrawn asset would keep rendering forever.
//
// So: connectivity gates ENTRY and periodic REVALIDATION. It does not gate the moment to
// moment experience, which is what section 8.3's offline library is for.
using System;
using Gibi.Core;

namespace Gibi.AssetRuntime
{
    public enum EntitlementCheckResult { Valid, Revoked, Expired, Unreachable }

    public sealed class ConnectivityPolicy
    {
        /// <summary>A session may not START without a successful check. Section 6.4 step 3.</summary>
        public const bool RequiresOnlineSessionStart = true;

        /// <summary>
        /// How long play may continue on a cached entitlement before the server must be
        /// reached again. Long enough that a weekend without signal is not a punishment,
        /// short enough that a revocation cannot be outrun.
        /// </summary>
        public const long RevalidationWindowMs = 72L * 60 * 60 * 1000;   // 72 hours

        /// <summary>
        /// Grace after the window lapses, during which the app retries quietly while play
        /// continues. Only after this does the pet actually become unavailable.
        /// </summary>
        public const long GracePeriodMs = 24L * 60 * 60 * 1000;          // 24 hours

        // --- Update heartbeat -------------------------------------------------
        // Section 16 requires every feature flag to have a KILL SWITCH, and section 10
        // makes live configuration server-owned. A kill switch that takes 72 hours to
        // arrive is not a kill switch, so update polling runs on a much faster cadence
        // than entitlement revalidation. They are separate concerns with separate clocks.
        //
        // The heartbeat carries: revocation list, feature flags and kill switches,
        // safety revision (section 9.2), and content catalog version.
        public const long UpdateCheckIntervalMs = 15L * 60 * 1000;        // 15 minutes while active
        public const long UpdateCheckOnForegroundMs = 60L * 1000;         // and within a minute of resume

        private readonly IMonotonicClock _clock;
        private long _lastSuccessfulCheckMs = -1;
        private long _lastUpdateCheckMs = -1;
        private bool _revoked;
        private bool _killSwitchEngaged;
        private int _safetyRevision;

        public ConnectivityPolicy(IMonotonicClock clock) { _clock = clock; }

        public void RecordCheck(EntitlementCheckResult result)
        {
            switch (result)
            {
                case EntitlementCheckResult.Valid:
                    _lastSuccessfulCheckMs = _clock.ElapsedMilliseconds;
                    _revoked = false;
                    break;

                case EntitlementCheckResult.Revoked:
                case EntitlementCheckResult.Expired:
                    // Section 10.2: takes effect IMMEDIATELY. Not at the next window,
                    // not after grace — now. Grace exists for unreachable servers, never
                    // for a definitive negative answer.
                    _revoked = true;
                    break;

                case EntitlementCheckResult.Unreachable:
                    // Says nothing about entitlement. Do not treat as either answer.
                    break;
            }
        }

        /// <summary>Section 6.4: a pet may not be instantiated without a current entitlement.</summary>
        public bool MayStartSession()
            => !_killSwitchEngaged && !_revoked && _lastSuccessfulCheckMs >= 0 &&
               _clock.ElapsedMilliseconds - _lastSuccessfulCheckMs <= RevalidationWindowMs;

        /// <summary>
        /// May play CONTINUE? More permissive than starting: a drop mid-session must not
        /// end the session (section 8.3), but it cannot extend forever either.
        /// </summary>
        public bool MayContinuePlaying()
        {
            if (_killSwitchEngaged) return false;       // section 16: immediate
            if (_revoked) return false;                 // immediate, no grace
            if (_lastSuccessfulCheckMs < 0) return false;
            long since = _clock.ElapsedMilliseconds - _lastSuccessfulCheckMs;
            return since <= RevalidationWindowMs + GracePeriodMs;
        }

        /// <summary>True once inside grace — the app retries quietly, the player sees nothing yet.</summary>
        public bool IsInGracePeriod()
        {
            if (_revoked || _lastSuccessfulCheckMs < 0) return false;
            long since = _clock.ElapsedMilliseconds - _lastSuccessfulCheckMs;
            return since > RevalidationWindowMs && since <= RevalidationWindowMs + GracePeriodMs;
        }

        // --- Update polling ---------------------------------------------------

        /// <summary>
        /// Is an update check due? Called from the frame loop; cheap, and does not block.
        /// Section 4.2 step 8: nothing here may stall the frame.
        /// </summary>
        public bool ShouldCheckForUpdates(bool justForegrounded)
        {
            long now = _clock.ElapsedMilliseconds;
            if (_lastUpdateCheckMs < 0) return true;
            long since = now - _lastUpdateCheckMs;
            return justForegrounded
                ? since >= UpdateCheckOnForegroundMs
                : since >= UpdateCheckIntervalMs;
        }

        /// <summary>
        /// Record the outcome of an update poll. A poll that reaches the server also
        /// counts as proof of connectivity, so a device that is playing normally never
        /// drifts toward the revalidation window.
        /// </summary>
        public void RecordUpdateCheck(bool reachedServer, bool killSwitchEngaged,
                                      int safetyRevision, EntitlementCheckResult entitlement)
        {
            _lastUpdateCheckMs = _clock.ElapsedMilliseconds;
            if (!reachedServer) return;

            _killSwitchEngaged = killSwitchEngaged;
            _safetyRevision = safetyRevision;
            RecordCheck(entitlement);
        }

        /// <summary>
        /// Section 16 kill switch. Takes effect at the next poll, not at the next
        /// revalidation window — which is the whole point of the faster cadence.
        /// </summary>
        public bool KillSwitchEngaged => _killSwitchEngaged;

        /// <summary>
        /// Section 9.2: a safety revision bump invalidates cached course eligibility.
        /// Returned so gameplay can drop ranked eligibility without waiting for a restart.
        /// </summary>
        public int SafetyRevision => _safetyRevision;

        public long MsUntilNextUpdateCheck()
            => _lastUpdateCheckMs < 0
                ? 0
                : Math.Max(0, UpdateCheckIntervalMs - (_clock.ElapsedMilliseconds - _lastUpdateCheckMs));

        public long MsUntilRevalidationRequired()
            => _lastSuccessfulCheckMs < 0
                ? 0
                : Math.Max(0, RevalidationWindowMs -
                             (_clock.ElapsedMilliseconds - _lastSuccessfulCheckMs));
    }
}
