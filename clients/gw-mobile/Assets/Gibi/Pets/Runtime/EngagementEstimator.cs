// GW-ARCH-001 section 8 (proposed amendment). Continuous, on-device, EPHEMERAL estimation.
//
// This estimator infers constantly and remembers nothing. It is a thermostat, not a diary.
//
// NORMATIVE CONSTRAINTS:
//   * NEVER persisted to disk, transmitted, sent to the AI provider, or included in
//     telemetry. Recomputed from scratch every launch.
//   * NEVER produces a label, category, or statement about the person -- only continuous
//     parameters that bias behaviour.
//   * NEVER triggers a question to the child. The pet does not ask how anyone is feeling.
//   * Reads ONLY touch tempo and dwell time. No camera, no microphone, no facial
//     expression, no biometrics -- not even locally.
//
// Continuous inference is safe precisely BECAUSE nothing survives the session: a system
// that cannot retain a conclusion cannot build a profile, leak one, or be asked for one.
using Gibi.Core;

namespace Gibi.Pets
{
    /// <summary>Continuous parameters. Deliberately not an enum — a category would be a label.</summary>
    public readonly struct EngagementEstimate
    {
        public readonly float Arousal;        // 0 calm .. 1 highly activated
        public readonly float Perseveration;  // 0 varied .. 1 one action repeated
        public readonly float Settling;       // 0 active .. 1 still, pet held close
        public readonly float Fatigue;        // 0 fresh .. 1 long session / late hour

        public EngagementEstimate(float arousal, float perseveration, float settling, float fatigue)
        { Arousal = arousal; Perseveration = perseveration; Settling = settling; Fatigue = fatigue; }
    }

    public sealed class EngagementEstimator
    {
        private const int WindowSize = 24;
        private readonly string[] _recentActions = new string[WindowSize];
        private readonly long[] _recentTimes = new long[WindowSize];
        private int _count;

        private readonly IMonotonicClock _clock;
        private readonly long _sessionStartMs;
        private long _lastInteractionMs;

        public EngagementEstimator(IMonotonicClock clock)
        {
            _clock = clock;
            _sessionStartMs = clock.ElapsedMilliseconds;
            _lastInteractionMs = _sessionStartMs;
        }

        /// <summary>Record a player-initiated action. No identifiers, no content — a key only.</summary>
        public void RecordInteraction(string actionKey)
        {
            long now = _clock.ElapsedMilliseconds;
            _recentActions[_count % WindowSize] = actionKey;
            _recentTimes[_count % WindowSize] = now;
            _count++;
            _lastInteractionMs = now;
        }

        public EngagementEstimate Estimate(int localHourOfDay)
        {
            long now = _clock.ElapsedMilliseconds;
            int n = _count < WindowSize ? _count : WindowSize;

            // --- Arousal: interaction tempo. Rapid input reads as activated. ---
            float arousal = 0f;
            if (n >= 3)
            {
                long span = 0; int gaps = 0;
                for (int i = 1; i < n; i++)
                {
                    long dt = _recentTimes[i % WindowSize] - _recentTimes[(i - 1) % WindowSize];
                    if (dt > 0 && dt < 10_000) { span += dt; gaps++; }
                }
                if (gaps > 0)
                {
                    float meanGapMs = span / (float)gaps;
                    // ~400 ms between taps reads as high arousal; ~4 s reads as calm.
                    arousal = Clamp01(1f - (meanGapMs - 400f) / 3600f);
                }
            }

            // --- Perseveration: fraction of the window that is one repeated action. ---
            float perseveration = 0f;
            if (n >= 4)
            {
                string dominant = null; int best = 0;
                for (int i = 0; i < n; i++)
                {
                    string a = _recentActions[i];
                    if (a == null) continue;
                    int c = 0;
                    for (int j = 0; j < n; j++) if (_recentActions[j] == a) c++;
                    if (c > best) { best = c; dominant = a; }
                }
                if (dominant != null) perseveration = best / (float)n;
            }

            // --- Settling: quiet dwell. Not "sad" — just still. ---
            long idleMs = now - _lastInteractionMs;
            float settling = Clamp01(idleMs / 90_000f);

            // --- Fatigue: session length plus a late-hour term. ---
            float sessionFatigue = Clamp01((now - _sessionStartMs) / (40f * 60f * 1000f));
            float lateHour = (localHourOfDay >= 21 || localHourOfDay <= 5) ? 0.5f : 0f;
            float fatigue = Clamp01(sessionFatigue + lateHour);

            return new EngagementEstimate(arousal, perseveration, settling, fatigue);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
