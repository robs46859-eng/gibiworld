// GW-ARCH-001 section 5.2 — Anchor eligibility state machine.
// GW-AR-002: ranked content is solid and score-enabled ONLY while the target anchor
//            state is tracked.
// GW-AR-003: tracking loss pauses ranked time within 250 ms.
// NORMATIVE (section 0): "Ranked scoring SHALL pause when the tracked site anchor is
// not in tracked state. Device VPS2 state alone SHALL NOT authorize placement or scoring."
using Gibi.Core;

namespace Gibi.Spatial
{
    public enum AnchorState
    {
        Unavailable,  // no AR session / provider unavailable -> 2D companion card
        Scanning,     // session tracking, no accepted surface -> scan coaching
        LocalReady,   // accepted plane/mesh anchor + clearance -> LOCAL badge
        VpsLimited,   // site anchor limited -> preview ghost only
        VpsTracked,   // target anchor tracked >= 1.0 s -> persistent content solid
        Degraded      // tracked lost > 250 ms or pose jump -> freeze clock, relocalize
    }

    public enum ScoringMode { Disabled, PracticeOnly, Eligible, Paused }

    /// <summary>
    /// Per-anchor eligibility. Deliberately consumes an explicit per-anchor tracking
    /// signal rather than a device-level VPS state, because device VPS2 state alone
    /// SHALL NOT authorize placement or scoring.
    /// </summary>
    public sealed class AnchorEligibility
    {
        public const long TrackedDwellMs   = 1000; // >= 1.0 s before VPS_TRACKED
        public const long DegradeGraceMs   = 250;  // tracked lost > 250 ms -> DEGRADED
        public const long InvalidateAfterMs = 3000; // > 3.0 s -> run becomes UNRANKED
        public const float PoseJumpThresholdM = 0.35f;

        private readonly IMonotonicClock _clock;
        private long _trackedSinceMs = -1;
        private long _lostSinceMs = -1;

        public AnchorState State { get; private set; } = AnchorState.Unavailable;
        public bool RunInvalidated { get; private set; }

        public AnchorEligibility(IMonotonicClock clock) { _clock = clock; }

        /// <param name="sessionTracking">AR session is tracking at all.</param>
        /// <param name="surfaceAccepted">A plane/mesh anchor passed clearance + slope gates.</param>
        /// <param name="usingVpsSite">This anchor belongs to a processed VPS Site.</param>
        /// <param name="anchorTracked">PER-ANCHOR tracking state from the provider.</param>
        /// <param name="poseJumpM">Magnitude of this frame's anchor pose correction.</param>
        public AnchorState Tick(bool sessionTracking, bool surfaceAccepted,
                                bool usingVpsSite, bool anchorTracked, float poseJumpM)
        {
            long now = _clock.ElapsedMilliseconds;

            if (!sessionTracking)
            {
                _trackedSinceMs = -1; _lostSinceMs = -1;
                return State = AnchorState.Unavailable;
            }

            if (usingVpsSite)
            {
                bool jumped = poseJumpM > PoseJumpThresholdM;

                if (anchorTracked && !jumped)
                {
                    _lostSinceMs = -1;
                    if (_trackedSinceMs < 0) _trackedSinceMs = now;

                    // Dwell requirement: content stays ghosted until the anchor has been
                    // continuously tracked for at least one second.
                    if (now - _trackedSinceMs >= TrackedDwellMs)
                    {
                        RunInvalidated = false;
                        return State = AnchorState.VpsTracked;
                    }
                    return State = AnchorState.VpsLimited;
                }

                // Lost or jumped.
                _trackedSinceMs = -1;
                if (_lostSinceMs < 0) _lostSinceMs = now;
                long lostFor = now - _lostSinceMs;

                if (lostFor > InvalidateAfterMs) RunInvalidated = true;
                if (lostFor > DegradeGraceMs)    return State = AnchorState.Degraded;
                return State = AnchorState.VpsLimited;
            }

            _trackedSinceMs = -1; _lostSinceMs = -1;
            return State = surfaceAccepted ? AnchorState.LocalReady : AnchorState.Scanning;
        }

        /// <summary>Scoring authority derived from state. Never derived from device VPS state.</summary>
        public ScoringMode Scoring => State switch
        {
            AnchorState.VpsTracked => ScoringMode.Eligible,
            AnchorState.LocalReady => ScoringMode.PracticeOnly,
            AnchorState.Degraded   => ScoringMode.Paused,
            _                      => ScoringMode.Disabled
        };

        /// <summary>Section 5.3: persistence is refused for anything not on a tracked VPS site.</summary>
        public bool MayPersistPlacement => State == AnchorState.VpsTracked;
    }
}
