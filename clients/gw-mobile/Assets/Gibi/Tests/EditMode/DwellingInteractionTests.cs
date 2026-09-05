// GW-ARCH-003 HOME-01, HOME-02 — DwellingInteractionTests.
// Validates physical envelope fit gates and traversable occupancy state machines.
using Gibi.Core;
using Gibi.Pets;
using NUnit.Framework;
using UnityEngine;

namespace Gibi.Tests
{
    public sealed class DwellingInteractionTests
    {
        [Test]
        public void Dwelling_envelope_fits_reference_hero_dog()
        {
            var go = new GameObject("TestDwelling");
            try
            {
                var dwelling = go.AddComponent<DwellingDefinition>();
                dwelling.EnsureDefaultMarkers();

                // Reference dog envelope: radius 0.20m (width 0.40m), height 0.50m
                var heroEnvelope = new AgentEnvelope(0.20f, 0.50f, 0.40f);
                Assert.IsTrue(dwelling.CanFitPet(heroEnvelope), "Dwelling must fit the reference hero dog");

                // Check physical doorway targets: >= 0.70m W x 0.90m H
                Assert.That(dwelling.DoorWidthM, Is.GreaterThanOrEqualTo(0.70f));
                Assert.That(dwelling.DoorHeightM, Is.GreaterThanOrEqualTo(0.90f));

                // Check interior envelope targets: >= 1.30m W x 1.50m D x 1.00m H
                Assert.That(dwelling.InteriorWidthM, Is.GreaterThanOrEqualTo(1.30f));
                Assert.That(dwelling.InteriorDepthM, Is.GreaterThanOrEqualTo(1.50f));
                Assert.That(dwelling.InteriorHeightM, Is.GreaterThanOrEqualTo(1.00f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Undersized_dwelling_rejects_dog_envelope()
        {
            var go = new GameObject("UndersizedDwelling");
            try
            {
                var dwelling = go.AddComponent<DwellingDefinition>();
                // Massive pet (width 1.2m, height 1.1m)
                var hugeDog = new AgentEnvelope(0.60f, 1.10f, 0.80f);

                Assert.IsFalse(dwelling.CanFitPet(hugeDog), "Undersized dwelling must reject an oversized pet envelope");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Dwelling_occupancy_lifecycle_and_cancellation_release()
        {
            var go = new GameObject("DwellingFixture");
            try
            {
                var def = go.AddComponent<DwellingDefinition>();
                def.EnsureDefaultMarkers();
                var interaction = go.AddComponent<DwellingInteraction>();

                var token = new ActionToken(1, 5, "pet_01");

                Assert.AreEqual(DwellingOccupancyState.Available, interaction.State);
                Assert.IsTrue(interaction.IsAvailable);

                // Reserve
                Assert.IsTrue(interaction.TryReserve(token));
                Assert.AreEqual(DwellingOccupancyState.Reserved, interaction.State);
                Assert.IsFalse(interaction.IsAvailable);

                // Second pet cannot reserve
                var otherToken = new ActionToken(1, 6, "pet_other");
                Assert.IsFalse(interaction.TryReserve(otherToken));

                // Begin Entry
                Assert.IsTrue(interaction.BeginEntry(token));
                Assert.AreEqual(DwellingOccupancyState.Entering, interaction.State);

                // Commit Rest
                Assert.IsTrue(interaction.CommitRest(token));
                Assert.AreEqual(DwellingOccupancyState.Occupied, interaction.State);
                Assert.IsTrue(interaction.IsOccupied);

                // Begin Exit
                Assert.IsTrue(interaction.BeginExit(token));
                Assert.AreEqual(DwellingOccupancyState.Exiting, interaction.State);

                // Complete Exit
                Assert.IsTrue(interaction.CompleteExit(token));
                Assert.AreEqual(DwellingOccupancyState.Available, interaction.State);
                Assert.IsTrue(interaction.IsAvailable);

                // Cancellation in middle of occupancy safely releases
                Assert.IsTrue(interaction.TryReserve(token));
                Assert.IsTrue(interaction.BeginEntry(token));
                interaction.Release(token);
                Assert.AreEqual(DwellingOccupancyState.Available, interaction.State);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
