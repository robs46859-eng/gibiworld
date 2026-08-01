// GW-ARCH-001 section 8.1 — Deterministic behavior layers.
// GW-GAME-001: safety override interrupts ALL lower-priority behavior within one 10 Hz tick.
// NORMATIVE (section 0): "Frame-level behavior, physics, navigation, course scoring,
// and safety SHALL be deterministic on the client or authoritative session server.
// AI MAY choose only validated high-level intents from a fixed allowlist."
using System;
using Gibi.Core;

namespace Gibi.Pets
{
    /// <summary>Lower ordinal = higher priority. Priority 0 is the safety override.</summary>
    public enum BehaviorLayer
    {
        SafetyOverride  = 0, // client deterministic: stop, freeze, return to safe zone
        SessionRule     = 1, // authoritative course/session: start, gate order, finish
        PlayerCue       = 2, // client validated input: sit, come, fetch, stay
        AiIntent        = 3, // backend proposal, client validator
        NeedsScheduler  = 4, // client deterministic: rest, idle variety
        AmbientAnimation= 5  // client: blink, ear twitch, breathing
    }

    public readonly struct BehaviorAction
    {
        public readonly BehaviorLayer Layer;
        public readonly string ActionKey;
        public readonly long   Sequence;        // monotonic
        public readonly long   StartMs;
        public readonly long   MaxDurationMs;
        public readonly bool   Interruptible;
        public readonly string FallbackState;

        public BehaviorAction(BehaviorLayer layer, string actionKey, long sequence,
                              long startMs, long maxDurationMs, bool interruptible,
                              string fallbackState)
        { Layer = layer; ActionKey = actionKey; Sequence = sequence; StartMs = startMs;
          MaxDurationMs = maxDurationMs; Interruptible = interruptible; FallbackState = fallbackState; }

        public bool IsExpired(long nowMs) => nowMs - StartMs >= MaxDurationMs;
    }

    /// <summary>
    /// Evaluates at exactly 10 Hz. A lower-priority intent cannot interrupt a LOCKED
    /// safety, course, or player-cue action.
    /// </summary>
    public sealed class BehaviorArbiter
    {
        public const int TickHz = 10;
        public const long TickIntervalMs = 1000 / TickHz; // 100 ms

        private readonly IMonotonicClock _clock;
        private long _nextTickMs;
        private long _sequence;
        private BehaviorAction? _current;

        public BehaviorArbiter(IMonotonicClock clock) { _clock = clock; }

        public BehaviorAction? Current => _current;
        public string CurrentActionKey => _current?.ActionKey ?? "CALM_IDLE";

        /// <summary>Layers that lock out lower-priority interruption while running.</summary>
        private static bool IsLocking(BehaviorLayer l) =>
            l == BehaviorLayer.SafetyOverride ||
            l == BehaviorLayer.SessionRule    ||
            l == BehaviorLayer.PlayerCue;

        /// <summary>
        /// Safety proposals bypass the tick cadence entirely. GW-GAME-001 requires the
        /// interrupt to land within one tick; taking effect immediately satisfies that
        /// bound with margin and removes any dependence on tick phase.
        /// </summary>
        public BehaviorAction ForceSafety(string actionKey, long maxDurationMs)
        {
            long now = _clock.ElapsedMilliseconds;
            var a = new BehaviorAction(BehaviorLayer.SafetyOverride, actionKey,
                                       ++_sequence, now, maxDurationMs,
                                       interruptible: false, fallbackState: "CALM_IDLE");
            _current = a;
            return a;
        }

        /// <summary>
        /// Offer a candidate. Returns true if it was accepted as the active action.
        /// Deterministic: identical inputs and clock produce identical outputs.
        /// </summary>
        public bool Propose(BehaviorLayer layer, string actionKey, long maxDurationMs,
                            bool interruptible = true, string fallbackState = "CALM_IDLE")
        {
            long now = _clock.ElapsedMilliseconds;

            if (_current.HasValue)
            {
                var cur = _current.Value;

                if (cur.IsExpired(now))
                {
                    _current = null; // fall through to accept
                }
                else if (layer > cur.Layer && (IsLocking(cur.Layer) || !cur.Interruptible))
                {
                    return false; // strictly lower priority cannot preempt a locked action
                }
                else if (layer > cur.Layer)
                {
                    return false; // lower priority never preempts a live higher-priority action
                }
                else if (layer == cur.Layer && !cur.Interruptible)
                {
                    return false;
                }
            }

            _current = new BehaviorAction(layer, actionKey, ++_sequence, now,
                                          maxDurationMs, interruptible, fallbackState);
            return true;
        }

        /// <summary>Advance the 10 Hz cadence. Returns true when a tick was consumed.</summary>
        public bool Tick()
        {
            long now = _clock.ElapsedMilliseconds;
            if (now < _nextTickMs) return false;
            _nextTickMs = now + TickIntervalMs;

            if (_current.HasValue && _current.Value.IsExpired(now))
                _current = null;

            return true;
        }
    }
}
