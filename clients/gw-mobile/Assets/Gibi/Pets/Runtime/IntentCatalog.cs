// GW-ARCH-002 section 7, catalog revision 2.
//
// Mirrors contracts/schemas/intent-catalog.json. That file is the source of truth;
// IntentCatalogParityTests fails the build if the two drift apart.
//
// STEP 6 DECISION: architecture B. No language model ships. Twenty-nine intents plus a
// modifier layer, selected by a deterministic seeded policy. The reasoning, recorded here
// so it is not re-litigated by the next agent:
//
//   A language model that picks one of N categories is a lookup table with a battery bill.
//   What actually reads as "alive" is not WHICH action the pet takes but HOW and WHEN --
//   a dog that hesitates four hundred milliseconds before coming feels like it decided
//   something. A dog that picks from twenty-nine categories instead of eight feels
//   identical to the one that picked from eight.
//
//   So the expressive budget goes into BehaviorModifiers, not into model parameters.
//
// This type is pure C#: no UnityEngine dependency, so every behaviour rule is testable in
// EditMode with no device, no scene, and no play mode.
using System;
using System.Collections.Generic;

namespace Gibi.Pets
{
    public enum IntentGroup { Self, Player, Object, Environment, Comfort, Training }

    /// <summary>Whether an intent needs something to act upon.</summary>
    public enum TargetRequirement { None, Optional, Required }

    [Flags]
    public enum TargetKind
    {
        None = 0,
        Toy  = 1 << 0,
        Spo  = 1 << 1,   // spatial object
        Pet  = 1 << 2
    }

    public readonly struct IntentDef
    {
        public readonly string            Id;
        public readonly IntentGroup       Group;
        public readonly TargetRequirement Target;
        public readonly TargetKind        TargetKinds;
        public readonly int               BaseWeight;     // 0..100, pre-context
        public readonly float             MaxIntensity;   // hard cap for this intent
        public readonly int               MaxDurationMs;
        public readonly bool              Interruptible;
        public readonly string            ClipKey;

        public IntentDef(string id, IntentGroup group, TargetRequirement target, TargetKind kinds,
                         int baseWeight, float maxIntensity, int maxDurationMs,
                         bool interruptible, string clipKey)
        {
            Id = id; Group = group; Target = target; TargetKinds = kinds;
            BaseWeight = baseWeight; MaxIntensity = maxIntensity; MaxDurationMs = maxDurationMs;
            Interruptible = interruptible; ClipKey = clipKey;
        }
    }

    public static class IntentCatalog
    {
        /// <summary>Bumped whenever any intent id, weight, cap, or clip binding changes.</summary>
        public const int CatalogRevision = 2;

        // Ordered exactly as the contract file. Order is load-bearing: the deterministic
        // selector indexes into this array, so reordering changes every pet's behaviour.
        // Append only. Never reorder, never delete a published id.
        private static readonly IntentDef[] Defs =
        {
            // ---- SELF -----------------------------------------------------------------
            new IntentDef("CALM_IDLE",         IntentGroup.Self,        TargetRequirement.None,     TargetKind.None,               40, 0.40f, 6000, true,  "idle_a"),
            new IntentDef("SETTLE",            IntentGroup.Self,        TargetRequirement.None,     TargetKind.None,               25, 0.30f, 8000, true,  "down"),
            new IntentDef("REST",              IntentGroup.Self,        TargetRequirement.None,     TargetKind.None,               15, 0.20f, 8000, true,  "down_idle"),
            new IntentDef("STRETCH",           IntentGroup.Self,        TargetRequirement.None,     TargetKind.None,               10, 0.70f, 2500, false, "rise"),
            new IntentDef("SHAKE_OFF",         IntentGroup.Self,        TargetRequirement.None,     TargetKind.None,                8, 1.00f, 1200, false, "shake"),
            new IntentDef("SCAN_AROUND",       IntentGroup.Self,        TargetRequirement.None,     TargetKind.None,               18, 0.60f, 3000, true,  "idle_b"),
            new IntentDef("YAWN",              IntentGroup.Self,        TargetRequirement.None,     TargetKind.None,                6, 0.50f, 1800, false, "yawn"),

            // ---- PLAYER ---------------------------------------------------------------
            new IntentDef("GREET",             IntentGroup.Player,      TargetRequirement.None,     TargetKind.None,               30, 1.00f, 3000, true,  "greet"),
            new IntentDef("SEEK_PROXIMITY",    IntentGroup.Player,      TargetRequirement.None,     TargetKind.None,               22, 0.80f, 5000, true,  "walk"),
            new IntentDef("INVITE_PLAY",       IntentGroup.Player,      TargetRequirement.Optional, TargetKind.Toy,                26, 1.00f, 4000, true,  "play_bow"),
            new IntentDef("FOLLOW_GAZE",       IntentGroup.Player,      TargetRequirement.Optional, TargetKind.Toy | TargetKind.Spo,20, 0.50f, 2500, true,  "idle_a"),
            new IntentDef("LEAN_IN",           IntentGroup.Player,      TargetRequirement.None,     TargetKind.None,               14, 0.40f, 4000, true,  "pet_react"),
            new IntentDef("CHECK_IN",          IntentGroup.Player,      TargetRequirement.None,     TargetKind.None,               16, 0.40f, 1500, true,  "idle_b"),
            new IntentDef("PRESENT_ITEM",      IntentGroup.Player,      TargetRequirement.Required, TargetKind.Toy,                18, 0.80f, 4000, true,  "carry"),

            // ---- OBJECT ---------------------------------------------------------------
            new IntentDef("INSPECT_OBJECT",    IntentGroup.Object,      TargetRequirement.Required, TargetKind.Toy | TargetKind.Spo,20, 0.60f, 4000, true,  "idle_b"),
            new IntentDef("CURIOUS_SNIFF",     IntentGroup.Object,      TargetRequirement.Optional, TargetKind.Toy | TargetKind.Spo,22, 0.50f, 3500, true,  "sniff"),
            new IntentDef("NUDGE_OBJECT",      IntentGroup.Object,      TargetRequirement.Required, TargetKind.Toy,                14, 0.90f, 2000, true,  "nudge"),
            new IntentDef("RETRIEVE",          IntentGroup.Object,      TargetRequirement.Required, TargetKind.Toy,                24, 1.00f, 8000, true,  "pickup"),
            // GUARD_ITEM is soft and playful only. Section 1.2 excludes possessive or
            // punishing mechanics, so this can never escalate and never denies the player.
            new IntentDef("GUARD_ITEM",        IntentGroup.Object,      TargetRequirement.Required, TargetKind.Toy,                 8, 0.50f, 5000, true,  "down_idle"),
            new IntentDef("ABANDON_ITEM",      IntentGroup.Object,      TargetRequirement.Optional, TargetKind.Toy,                10, 0.40f, 1500, true,  "drop"),

            // ---- ENVIRONMENT ----------------------------------------------------------
            new IntentDef("SEEK_SHADE",        IntentGroup.Environment, TargetRequirement.Optional, TargetKind.Spo,                10, 0.60f, 6000, true,  "walk"),
            new IntentDef("AVOID_SURFACE",     IntentGroup.Environment, TargetRequirement.None,     TargetKind.None,               12, 0.70f, 2500, true,  "turn_l_90"),
            new IntentDef("ORIENT_TO_SOUND",   IntentGroup.Environment, TargetRequirement.None,     TargetKind.None,               14, 0.50f, 2000, true,  "idle_b"),

            // ---- COMFORT --------------------------------------------------------------
            new IntentDef("SOFTEN",            IntentGroup.Comfort,     TargetRequirement.None,     TargetKind.None,               20, 0.25f, 6000, true,  "idle_a"),
            new IntentDef("LIE_NEAR",          IntentGroup.Comfort,     TargetRequirement.None,     TargetKind.None,               22, 0.20f, 8000, true,  "down"),
            new IntentDef("REST_HEAD",         IntentGroup.Comfort,     TargetRequirement.None,     TargetKind.None,               16, 0.15f, 8000, true,  "sleep"),

            // ---- TRAINING -------------------------------------------------------------
            new IntentDef("ANTICIPATE_CUE",    IntentGroup.Training,    TargetRequirement.None,     TargetKind.None,               18, 0.70f, 3000, true,  "sit_idle"),
            new IntentDef("OFFER_KNOWN_TRICK", IntentGroup.Training,    TargetRequirement.None,     TargetKind.None,               14, 0.80f, 4000, true,  "sit"),
            new IntentDef("RESET_POSTURE",     IntentGroup.Training,    TargetRequirement.None,     TargetKind.None,               12, 0.50f, 2000, true,  "stand"),
        };

        public static int Count => Defs.Length;
        public static IntentDef At(int index) => Defs[index];

        private static readonly Dictionary<string, int> IndexById = BuildIndex();

        private static Dictionary<string, int> BuildIndex()
        {
            var map = new Dictionary<string, int>(Defs.Length, StringComparer.Ordinal);
            for (int i = 0; i < Defs.Length; i++) map.Add(Defs[i].Id, i);
            return map;
        }

        public static bool TryGet(string intentId, out IntentDef def)
        {
            if (intentId != null && IndexById.TryGetValue(intentId, out int i))
            {
                def = Defs[i];
                return true;
            }
            def = default;
            return false;
        }

        public static bool IsKnown(string intentId)
            => intentId != null && IndexById.ContainsKey(intentId);

        /// <summary>
        /// Groups that a care profile may never remove. The catalog is guaranteed non-empty
        /// after filtering, so the pet can always act -- an empty catalog would be a pet
        /// that freezes, which reads to a child as the pet being broken or upset.
        /// </summary>
        public static bool IsUnremovable(IntentGroup g)
            => g == IntentGroup.Self || g == IntentGroup.Comfort;

        /// <summary>All ids, in catalog order. Allocates; test and tooling use only.</summary>
        public static string[] AllIds()
        {
            var ids = new string[Defs.Length];
            for (int i = 0; i < Defs.Length; i++) ids[i] = Defs[i].Id;
            return ids;
        }
    }
}
