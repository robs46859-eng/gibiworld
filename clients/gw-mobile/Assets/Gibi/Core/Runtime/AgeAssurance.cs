// GW-ARCH-001 section 0 — "Launch accounts SHALL be age 13 or older. Under-13 accounts
// SHALL remain disabled until verifiable parental consent, guardian controls, child
// privacy review, and store declarations are implemented."
//
// See docs/design/age-assurance-and-consent.md and ADR-010.
using System;

namespace Gibi.Core
{
    /// <summary>Only the band is retained — never a date of birth (section 13.2 minimisation).</summary>
    public enum BirthBand { Unknown = 0, Under13, Teen13To17, Adult18Plus }

    public enum ConsentStatus { None, Pending, Granted, Withdrawn, Expired }

    public static class AgeAssurance
    {
        /// <summary>
        /// Neutral age screen: the user enters a date, and only the BAND is kept.
        ///
        /// Deliberately NOT a yes/no "are you over 13?" — a binary question with a visible
        /// consequence teaches a child which answer unlocks the app. A date field with no
        /// stated threshold does not.
        /// </summary>
        public static BirthBand BandFor(int birthYear, int birthMonth, int birthDay,
                                        DateTime utcNow)
        {
            if (birthYear < 1900 || birthMonth < 1 || birthMonth > 12 ||
                birthDay < 1 || birthDay > 31)
                return BirthBand.Unknown;

            DateTime dob;
            try { dob = new DateTime(birthYear, birthMonth, birthDay); }
            catch (ArgumentOutOfRangeException) { return BirthBand.Unknown; }

            if (dob > utcNow) return BirthBand.Unknown;

            int age = utcNow.Year - dob.Year;
            if (dob.Date > utcNow.Date.AddYears(-age)) age--;

            if (age < 13) return BirthBand.Under13;
            if (age < 18) return BirthBand.Teen13To17;
            return BirthBand.Adult18Plus;
        }

        /// <summary>
        /// May an account become active? Section 0 permits under-13 ONLY with current
        /// verifiable consent. The database enforces this independently (migration 0003);
        /// this is the client-side half so the UI never offers a path the server refuses.
        /// </summary>
        public static bool MayActivate(BirthBand band, ConsentStatus consent)
            => band switch
            {
                BirthBand.Adult18Plus => true,
                BirthBand.Teen13To17  => true,
                BirthBand.Under13     => consent == ConsentStatus.Granted,
                _                     => false   // Unknown fails CLOSED
            };

        /// <summary>
        /// Care-context features (ADR-009) require guardian consent for ANY minor, not
        /// just under-13: they involve an adult observing a minor's play rhythm, which
        /// warrants consent at 16 as much as at 11.
        /// </summary>
        public static bool MayEnableCareContext(BirthBand band, ConsentStatus consent)
            => band == BirthBand.Adult18Plus || consent == ConsentStatus.Granted;

        /// <summary>Consent is re-sought when stale; forgotten consent is not consent.</summary>
        public const int ConsentValidityMonths = 24;

        public static bool ConsentIsCurrent(ConsentStatus status, DateTime grantedUtc,
                                            DateTime utcNow)
            => status == ConsentStatus.Granted &&
               grantedUtc.AddMonths(ConsentValidityMonths) > utcNow;
    }
}
