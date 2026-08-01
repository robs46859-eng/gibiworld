// GW-ARCH-001 section 7 — Rendering budgets by device tier, and section 17.1's
// device test matrix (iOS Tier A/B, Android Tier A/B/C).
//
// Tiering is resolved ONCE at bootstrap. Re-evaluating per-frame would let thermal
// throttling silently move a device between tiers mid-run, changing physics-adjacent
// budgets during a ranked course.
using UnityEngine;

namespace Gibi.Core
{
    public enum DeviceTier { A, B, C }

    public readonly struct TierBudget
    {
        public readonly int TargetFrameRate;
        public readonly float FrameBudgetMs;
        public readonly long PeakResidentBytes;
        public readonly int MaxDrawCalls;
        public readonly int MaxVisibleParticles;
        public readonly int MaxWorldMeshTriangles;
        public readonly int ShadowMapSize;       // 0 = blob/contact shadow fallback
        public readonly float MinDynamicResolution;

        public TierBudget(int fps, float frameMs, long memBytes, int draws, int particles,
                          int worldTris, int shadowMap, float minDynRes)
        {
            TargetFrameRate = fps; FrameBudgetMs = frameMs; PeakResidentBytes = memBytes;
            MaxDrawCalls = draws; MaxVisibleParticles = particles;
            MaxWorldMeshTriangles = worldTris; ShadowMapSize = shadowMap;
            MinDynamicResolution = minDynRes;
        }
    }

    public static class DeviceTiering
    {
        private const long GiB = 1024L * 1024L * 1024L;

        // Section 7 budget table.
        public static readonly TierBudget TierAB = new(
            fps: 60, frameMs: 16.7f, memBytes: (long)(1.2 * GiB),
            draws: 160, particles: 20_000, worldTris: 120_000,
            shadowMap: 1024, minDynRes: 0.70f);

        public static readonly TierBudget TierC = new(
            fps: 30, frameMs: 33.3f, memBytes: 900L * 1024L * 1024L,
            draws: 100, particles: 5_000, worldTris: 60_000,
            shadowMap: 0, minDynRes: 0.70f);

        public static DeviceTier Current { get; private set; } = DeviceTier.B;
        public static TierBudget Budget => Current == DeviceTier.C ? TierC : TierAB;

        private static bool _resolved;

        /// <summary>Resolve once during bootstrap. Idempotent.</summary>
        public static DeviceTier Resolve()
        {
            if (_resolved) return Current;
            _resolved = true;

            int memMb = SystemInfo.systemMemorySize;
            int gpuMem = SystemInfo.graphicsMemorySize;
            int cores = SystemInfo.processorCount;

            // Section 17.1 pegs Android Tier B at 6 GB RAM, so anything materially
            // below that is the minimum-supported Tier C path.
            if (memMb < 4096 || cores <= 4) Current = DeviceTier.C;
            else if (memMb < 6144) Current = DeviceTier.B;
            else Current = gpuMem >= 2048 ? DeviceTier.A : DeviceTier.B;

            Application.targetFrameRate = Budget.TargetFrameRate;
            return Current;
        }

        /// <summary>
        /// Section 6.2: Tier C forces LOD1 and simplified fur. Exposed so the asset
        /// runtime can pick a starting LOD before the first frame is presented.
        /// </summary>
        public static int MinimumLodIndex => Current == DeviceTier.C ? 1 : 0;

        public static bool SupportsDepthOcclusion =>
            Current != DeviceTier.C && SystemInfo.supportsAsyncGPUReadback;
    }
}
