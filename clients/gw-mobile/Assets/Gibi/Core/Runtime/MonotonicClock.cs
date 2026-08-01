// GW-ARCH-001 section 9.2 — "The authoritative timer SHALL use server-issued
// startEpochMs and monotonic client deltas. Client wall-clock values are never
// trusted for ranking."
// GW-GAME-006: no absence duration decreases bond or inventory; a wall-clock jump
// must therefore never be readable as elapsed gameplay time.
using System.Diagnostics;

namespace Gibi.Core
{
    public interface IMonotonicClock { long ElapsedMilliseconds { get; } }

    /// <summary>
    /// Wraps Stopwatch, which is unaffected by NTP correction, timezone change, or
    /// user clock tampering. DateTime.UtcNow is deliberately NOT exposed anywhere in
    /// the ranked scoring path.
    /// </summary>
    public sealed class MonotonicClock : IMonotonicClock
    {
        private readonly Stopwatch _sw = Stopwatch.StartNew();
        public long ElapsedMilliseconds => _sw.ElapsedMilliseconds;
    }

    /// <summary>Deterministic clock for EditMode tests and recorded AR playback.</summary>
    public sealed class FakeClock : IMonotonicClock
    {
        public long ElapsedMilliseconds { get; private set; }
        public void Advance(long ms) => ElapsedMilliseconds += ms;
    }
}
