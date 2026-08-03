// Maps a catalog clip key onto a clip the LOADED ASSET ACTUALLY SHIPS.
//
// Why this exists rather than trimming the catalog:
//
//   The shipped preset (randy11) contains exactly 8 clips -- greet, idle_a, pet_react,
//   run, sit, sleep, success, walk. GW-ARCH-001 section 6.3 names 23. So even clips that
//   are fully in-spec (idle_b, down, rise, pickup, carry, drop, stand, sit_idle,
//   turn_l_90) are absent from this particular asset.
//
//   Different pets will ship different subsets forever -- that is the nature of an open
//   asset pipeline where a Pawsome3D dog and a preset dog are authored by different
//   people at different times. Hard-coding the catalog down to one asset's clip list
//   would break the moment a second asset arrives.
//
//   So resolution is per-asset and explicit: every catalog clip declares an ordered
//   preference chain, and the resolver walks it against the manifest's real clip list.
//   A missing clip degrades to something sensible; it is never an error and never
//   prevents the intent from running. An intent the pet cannot animate still HAPPENS --
//   it just looks calmer.
//
// Pure C#. No UnityEngine reference.
using System;
using System.Collections.Generic;

namespace Gibi.Pets
{
    public static class ClipResolver
    {
        // Ordered preference per catalog clip key. First entry present on the asset wins.
        // Every chain ends at "idle_a" because section 6.3 makes it the one clip every
        // playable pet is required to carry.
        private static readonly Dictionary<string, string[]> Chains =
            new(StringComparer.Ordinal)
        {
            ["idle_a"]      = new[] { "idle_a" },
            ["idle_b"]      = new[] { "idle_b", "idle_a" },
            ["walk"]        = new[] { "walk", "trot", "idle_a" },
            ["trot"]        = new[] { "trot", "walk", "idle_a" },
            ["run"]         = new[] { "run", "trot", "walk", "idle_a" },
            ["turn_l_90"]   = new[] { "turn_l_90", "walk", "idle_a" },
            ["turn_r_90"]   = new[] { "turn_r_90", "walk", "idle_a" },

            ["sit"]         = new[] { "sit", "idle_a" },
            ["sit_idle"]    = new[] { "sit_idle", "sit", "idle_a" },
            ["stand"]       = new[] { "stand", "rise", "idle_a" },
            ["rise"]        = new[] { "rise", "stand", "idle_a" },
            ["down"]        = new[] { "down", "sleep", "sit", "idle_a" },
            ["down_idle"]   = new[] { "down_idle", "sleep", "sit", "idle_a" },
            ["sleep"]       = new[] { "sleep", "down_idle", "down", "idle_a" },

            ["greet"]       = new[] { "greet", "success", "idle_a" },
            ["pet_react"]   = new[] { "pet_react", "greet", "idle_a" },
            ["success"]     = new[] { "success", "greet", "idle_a" },

            // Clips the intent catalog wants but section 6.3 never defined. They resolve
            // to the nearest authored equivalent rather than blocking the intent.
            ["play_bow"]    = new[] { "play_bow", "greet", "idle_a" },
            ["sniff"]       = new[] { "sniff", "idle_b", "idle_a" },
            ["nudge"]       = new[] { "nudge", "pet_react", "idle_a" },
            ["shake"]       = new[] { "shake", "pet_react", "idle_a" },
            ["yawn"]        = new[] { "yawn", "idle_b", "idle_a" },

            ["pickup"]      = new[] { "pickup", "down", "idle_a" },
            ["carry"]       = new[] { "carry", "walk", "idle_a" },
            ["drop"]        = new[] { "drop", "down", "idle_a" },
        };

        public const string UniversalFallback = "idle_a";

        /// <summary>
        /// Resolve <paramref name="requested"/> against the clips this asset carries.
        /// Returns null only when the asset ships nothing at all, which the caller must
        /// treat as "play nothing" rather than as a failure.
        /// </summary>
        public static string Resolve(string requested, IReadOnlyCollection<string> available)
        {
            if (available == null || available.Count == 0) return null;

            if (requested != null && Chains.TryGetValue(requested, out var chain))
            {
                for (int i = 0; i < chain.Length; i++)
                    if (Contains(available, chain[i])) return chain[i];
            }
            else if (Contains(available, requested))
            {
                return requested;   // unknown key, but the asset happens to have it
            }

            if (Contains(available, UniversalFallback)) return UniversalFallback;

            foreach (var any in available) return any;   // last resort: anything at all
            return null;
        }

        /// <summary>True when the requested clip resolved to something other than itself.</summary>
        public static bool IsSubstituted(string requested, string resolved)
            => resolved != null && !string.Equals(requested, resolved, StringComparison.Ordinal);

        private static bool Contains(IReadOnlyCollection<string> set, string value)
        {
            if (value == null) return false;
            foreach (var s in set)
                if (string.Equals(s, value, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>Diagnostic: which catalog clips this asset cannot serve natively.</summary>
        public static List<string> MissingNatively(IReadOnlyCollection<string> available)
        {
            var missing = new List<string>();
            foreach (var key in Chains.Keys)
                if (!Contains(available, key)) missing.Add(key);
            missing.Sort(StringComparer.Ordinal);
            return missing;
        }
    }
}
