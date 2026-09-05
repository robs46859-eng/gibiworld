// GW-ARCH-003 AI-01, PET-03 — IntentEnvelopeTests.
// Asserts envelope schema constraints, intent allowlist, and NullIntentSource fallback.
using System;
using System.Threading;
using System.Threading.Tasks;
using Gibi.AI;
using NUnit.Framework;

namespace Gibi.Tests
{
    public sealed class IntentEnvelopeTests
    {
        [Test]
        public async Task Null_intent_source_returns_clean_inactive_fallback()
        {
            var source = new NullIntentSource();
            var context = new AiIntentContext("req_01J8ZQK5T7VN2MXR4WD6GHYAB3", "pet_01J8ZQK5T7VN2MXR4WD6GHYAB3", 1, 2, new[] { "toy_01" });

            var result = await source.RequestIntentAsync(context, CancellationToken.None);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NULL_INTENT_SOURCE_INACTIVE", result.Error);
        }

        [Test]
        public void Valid_envelope_passes_validation()
        {
            var context = new AiIntentContext("req_01J8ZQK5T7VN2MXR4WD6GHYAB3", "pet_01J8ZQK5T7VN2MXR4WD6GHYAB3", 1, 2, new[] { "toy_01" });
            var envelope = new AiIntentEnvelope(
                schemaVersion: 2,
                requestId: "req_01J8ZQK5T7VN2MXR4WD6GHYAB3",
                petId: "pet_01J8ZQK5T7VN2MXR4WD6GHYAB3",
                contextRevision: 1,
                catalogRevision: 2,
                intent: "GREET",
                targetId: "toy_01",
                expiresAt: DateTime.UtcNow.AddSeconds(5)
            );

            var validation = IntentEnvelopeValidator.Validate(envelope, context, DateTime.UtcNow);

            Assert.IsTrue(validation.Success);
            Assert.AreEqual("GREET", validation.Value.Intent);
        }

        [Test]
        public void Expired_or_unallowlisted_intents_reject()
        {
            var context = new AiIntentContext("req_01J8ZQK5T7VN2MXR4WD6GHYAB3", "pet_01J8ZQK5T7VN2MXR4WD6GHYAB3", 1, 2, new[] { "toy_01" });

            // Expired intent
            var expiredEnvelope = new AiIntentEnvelope(
                2, "req_01J8ZQK5T7VN2MXR4WD6GHYAB3", "pet_01J8ZQK5T7VN2MXR4WD6GHYAB3",
                1, 2, "CALM_IDLE", "toy_01", DateTime.UtcNow.AddSeconds(-10));
            var res1 = IntentEnvelopeValidator.Validate(expiredEnvelope, context, DateTime.UtcNow);
            Assert.IsFalse(res1.Success);
            Assert.AreEqual("INTENT_EXPIRED", res1.Error);

            // Unallowlisted intent
            var invalidEnvelope = new AiIntentEnvelope(
                2, "req_01J8ZQK5T7VN2MXR4WD6GHYAB3", "pet_01J8ZQK5T7VN2MXR4WD6GHYAB3",
                1, 2, "FLY_AWAY_INTO_SPACE", "toy_01", DateTime.UtcNow.AddSeconds(10));
            var res2 = IntentEnvelopeValidator.Validate(invalidEnvelope, context, DateTime.UtcNow);
            Assert.IsFalse(res2.Success);
            Assert.AreEqual("UNALLOWLISTED_INTENT_FLY_AWAY_INTO_SPACE", res2.Error);
        }
    }
}
