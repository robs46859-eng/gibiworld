// Where the pet looks to find something worth walking to.
//
// A flat list with linear search, deliberately. Placed-object counts in this game are
// single digits -- a doghouse, a bowl, a couple of toys -- so a spatial index would be
// more code, more state to keep coherent with AR anchor updates, and no measurable win.
// If counts ever reach the hundreds this is the one file that changes.
//
// Registration is driven by OnEnable/OnDisable, so an affordance whose anchor loses
// tracking and gets disabled disappears from consideration automatically rather than
// leaving the pet walking toward a position that is no longer valid.
using System.Collections.Generic;
using UnityEngine;

namespace Gibi.Pets
{
    public static class AffordanceRegistry
    {
        private static readonly List<IAffordance> All = new();

        public static int Count => All.Count;

        public static void Register(IAffordance a)
        {
            if (a != null && !All.Contains(a)) All.Add(a);
        }

        public static void Unregister(IAffordance a)
        {
            if (a != null) All.Remove(a);
        }

        /// <summary>Scene teardown. Without this, domain reload leaks stale entries into tests.</summary>
        public static void Clear() => All.Clear();

        /// <summary>
        /// Nearest available affordance of a kind, or null. <paramref name="maxRangeM"/>
        /// keeps the pet from setting off across a room toward something the player placed
        /// and forgot; beyond it the pet simply behaves normally.
        /// </summary>
        public static IAffordance FindNearest(AffordanceKind kind, Vector3 fromWorld,
                                              float maxRangeM = 12f)
        {
            IAffordance best = null;
            float bestSqr = maxRangeM * maxRangeM;

            for (int i = 0; i < All.Count; i++)
            {
                var a = All[i];
                if (a == null || !a.IsAvailable || a.Kind != kind) continue;

                float sqr = (a.ApproachPointWorld - fromWorld).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = a;
                }
            }
            return best;
        }

        public static bool Any(AffordanceKind kind)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i] != null && All[i].IsAvailable && All[i].Kind == kind) return true;
            return false;
        }
    }
}
