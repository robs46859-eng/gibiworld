// GW-ARCH-001 section 8 (proposed amendment; see docs/design/pet-brain-and-care-context.md
// and ADR-009). Guardian-set behavioural accommodations.
//
// NORMATIVE INTENT: this type MUST NOT reach the AI provider. Section 8.2 already bars
// sensitive or inferred attributes from the provider context, and a child's regulation
// needs are the most sensitive attribute in the system.
//
// Enforcement is structural, not conventional: CareProfile carries no serializer, no
// ToString override, and no public enumeration of active flags. Gibi.AI cannot include
// what it cannot read, and Gibi.Pets does not reference Gibi.AI (section 4 layering).
using System;

namespace Gibi.Pets
{
    /// <summary>
    /// Behavioural accommodations, NOT diagnoses. A guardian selects what helps; they
    /// never enter a condition, and there is no free-text field anywhere in this path.
    /// "Likes routine" is a preference. "Has autism" would be health data under a
    /// different legal regime and would tell the system nothing more useful.
    /// </summary>
    [Flags]
    public enum CareProfile
    {
        None                = 0,
        GentleSensory       = 1 << 0, // calmer movement, quieter reactions
        Predictable         = 1 << 1, // fewer surprises, consistent greeting and settle
        GentlePacing        = 1 << 2, // rest cues arrive sooner
        ExtraEncouragement  = 1 << 3, // training never surfaces failure
        CompanionBalance    = 1 << 4, // redirection favours outward suggestions
    }

    /// <summary>
    /// Resolved behaviour parameters. Multiple profiles compose by taking the MOST
    /// CONSERVATIVE value for every parameter, so adding a profile can only ever calm
    /// the pet further — never make it more intense.
    /// </summary>
    public readonly struct CareParameters
    {
        public readonly Gait MaxGait;
        public readonly float BlendMultiplier;      // >1 lengthens animation blends
        public readonly float ParticleBudgetScale;  // <1 reduces visual intensity
        public readonly bool  SuppressStartle;
        public readonly bool  SuppressFailureFeedback;
        public readonly long  RestCueAfterMs;
        public readonly bool  FavourOutwardRedirection;

        public CareParameters(Gait maxGait, float blendMultiplier, float particleScale,
                              bool suppressStartle, bool suppressFailure,
                              long restCueAfterMs, bool favourOutward)
        {
            MaxGait = maxGait; BlendMultiplier = blendMultiplier;
            ParticleBudgetScale = particleScale; SuppressStartle = suppressStartle;
            SuppressFailureFeedback = suppressFailure; RestCueAfterMs = restCueAfterMs;
            FavourOutwardRedirection = favourOutward;
        }

        /// <summary>Defaults are already gentle. Wellbeing pacing is not opt-in.</summary>
        public static CareParameters Default => new(
            maxGait: Gait.Run, blendMultiplier: 1.0f, particleScale: 1.0f,
            suppressStartle: false, suppressFailure: false,
            restCueAfterMs: 45 * 60 * 1000, favourOutward: false);

        public static CareParameters Resolve(CareProfile profile)
        {
            var p = Default;

            Gait gait = p.MaxGait;
            float blend = p.BlendMultiplier;
            float particles = p.ParticleBudgetScale;
            bool startle = p.SuppressStartle;
            bool failure = p.SuppressFailureFeedback;
            long rest = p.RestCueAfterMs;
            bool outward = p.FavourOutwardRedirection;

            if (profile.HasFlag(CareProfile.GentleSensory))
            {
                gait = Gait.Walk;
                blend = Math.Max(blend, 2.0f);
                particles = Math.Min(particles, 0.5f);
                startle = true;
            }
            if (profile.HasFlag(CareProfile.Predictable))
            {
                blend = Math.Max(blend, 1.5f);
                startle = true;
            }
            if (profile.HasFlag(CareProfile.GentlePacing))
                rest = Math.Min(rest, 20 * 60 * 1000);
            if (profile.HasFlag(CareProfile.ExtraEncouragement))
                failure = true;
            if (profile.HasFlag(CareProfile.CompanionBalance))
                outward = true;

            return new CareParameters(gait, blend, particles, startle, failure, rest, outward);
        }
    }
}
