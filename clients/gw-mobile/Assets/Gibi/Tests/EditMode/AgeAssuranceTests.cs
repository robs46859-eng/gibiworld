// Section 0 age gate. Written mostly as negative assertions — the failure mode is the
// system ALLOWING something it must not, not refusing something it should permit.
using System;
using NUnit.Framework;
using Gibi.Core;

namespace Gibi.Tests.EditMode
{
    public class GW_AgeGate
    {
        private static readonly DateTime Now = new(2026, 8, 1);

        [Test]
        public void Bands_are_computed_from_date_not_a_yes_no_question()
        {
            Assert.AreEqual(BirthBand.Under13,      AgeAssurance.BandFor(2020, 1, 1, Now));
            Assert.AreEqual(BirthBand.Teen13To17,   AgeAssurance.BandFor(2011, 1, 1, Now));
            Assert.AreEqual(BirthBand.Adult18Plus,  AgeAssurance.BandFor(2000, 1, 1, Now));
        }

        [Test]
        public void Birthday_not_yet_reached_this_year_counts_as_the_younger_age()
        {
            // Turns 13 later in 2026 — must still be Under13 today.
            Assert.AreEqual(BirthBand.Under13, AgeAssurance.BandFor(2013, 12, 31, Now));
            // Turned 13 earlier in 2026.
            Assert.AreEqual(BirthBand.Teen13To17, AgeAssurance.BandFor(2013, 1, 1, Now));
        }

        [Test]
        public void Under_13_cannot_activate_without_granted_consent()
        {
            foreach (var c in new[] { ConsentStatus.None, ConsentStatus.Pending,
                                      ConsentStatus.Withdrawn, ConsentStatus.Expired })
                Assert.IsFalse(AgeAssurance.MayActivate(BirthBand.Under13, c),
                    $"Section 0 violated: under-13 activated with consent={c}");

            Assert.IsTrue(AgeAssurance.MayActivate(BirthBand.Under13, ConsentStatus.Granted));
        }

        [Test]
        public void Unknown_or_invalid_age_fails_closed()
        {
            Assert.AreEqual(BirthBand.Unknown, AgeAssurance.BandFor(2026, 13, 45, Now));
            Assert.AreEqual(BirthBand.Unknown, AgeAssurance.BandFor(2030, 1, 1, Now), "future DOB");
            Assert.IsFalse(AgeAssurance.MayActivate(BirthBand.Unknown, ConsentStatus.Granted));
        }

        [Test]
        public void Care_context_needs_consent_for_teens_too_not_just_under_13()
        {
            Assert.IsFalse(AgeAssurance.MayEnableCareContext(BirthBand.Teen13To17, ConsentStatus.None),
                "A guardian observing a 16-year-old's play rhythm still requires consent.");
            Assert.IsTrue(AgeAssurance.MayEnableCareContext(BirthBand.Teen13To17, ConsentStatus.Granted));
            Assert.IsTrue(AgeAssurance.MayEnableCareContext(BirthBand.Adult18Plus, ConsentStatus.None));
        }

        [Test]
        public void Consent_expires_so_forgotten_consent_is_not_consent()
        {
            var granted = new DateTime(2024, 1, 1);
            Assert.IsFalse(AgeAssurance.ConsentIsCurrent(ConsentStatus.Granted, granted, Now),
                "Consent older than 24 months must not count as current.");
            Assert.IsTrue(AgeAssurance.ConsentIsCurrent(ConsentStatus.Granted,
                new DateTime(2025, 6, 1), Now));
        }
    }
}
