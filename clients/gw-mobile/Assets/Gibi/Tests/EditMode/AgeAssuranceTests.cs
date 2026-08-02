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
        public void Teens_need_no_guardian_consent_for_base_play()
        {
            // COPPA reaches under-13 only. A 13-17 account plays without any guardian.
            Assert.IsTrue(AgeAssurance.MayActivate(BirthBand.Teen13To17, ConsentStatus.None));
        }

        [Test]
        public void Teen_care_context_gates_on_the_teen_knowing_not_on_guardian_consent()
        {
            // The concern is transparency to the minor, not ceremony from the adult:
            // requiring a guardian to consent to their own settings is circular.
            Assert.IsFalse(AgeAssurance.MayEnableCareContext(
                BirthBand.Teen13To17, ConsentStatus.None, guardianLinkAcknowledgedByTeen: false),
                "A teen must know an adult can see their play rhythm.");

            Assert.IsTrue(AgeAssurance.MayEnableCareContext(
                BirthBand.Teen13To17, ConsentStatus.None, guardianLinkAcknowledgedByTeen: true),
                "Acknowledged link is sufficient — no consent artifact required.");
        }

        [Test]
        public void Under_13_care_context_still_requires_verifiable_consent()
        {
            Assert.IsFalse(AgeAssurance.MayEnableCareContext(
                BirthBand.Under13, ConsentStatus.None, guardianLinkAcknowledgedByTeen: true));
            Assert.IsTrue(AgeAssurance.MayEnableCareContext(
                BirthBand.Under13, ConsentStatus.Granted, guardianLinkAcknowledgedByTeen: true));
        }

        [Test]
        public void Adults_need_nothing()
        {
            Assert.IsTrue(AgeAssurance.MayEnableCareContext(
                BirthBand.Adult18Plus, ConsentStatus.None, guardianLinkAcknowledgedByTeen: false));
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
