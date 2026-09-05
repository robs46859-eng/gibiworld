// GW-ARCH-003 PET-02 — ActionTokenTests.
// Asserts that tokens prevent stale same-named completion from clearing a newer action.
using Gibi.Core;
using Gibi.Pets;
using NUnit.Framework;

namespace Gibi.Tests
{
    public sealed class ActionTokenTests
    {
        [Test]
        public void Action_tokens_distinguish_distinct_generation_and_sequence()
        {
            var tokenA = new ActionToken(1, 10, "pet_01");
            var tokenB = new ActionToken(1, 11, "pet_01");
            var tokenC = new ActionToken(2, 10, "pet_01");
            var tokenCopy = new ActionToken(1, 10, "pet_01");

            Assert.IsTrue(tokenA.IsValid);
            Assert.IsTrue(tokenA.Matches(tokenCopy));
            Assert.IsFalse(tokenA.Matches(tokenB));
            Assert.IsFalse(tokenA.Matches(tokenC));
        }

        [Test]
        public void Stale_action_token_cannot_clear_newer_same_named_action()
        {
            var arbiter = new BehaviorArbiter(new FakeClock());
            var token1 = new ActionToken(1, 1, "pet_randy");
            var token2 = new ActionToken(1, 2, "pet_randy");

            // First action
            Assert.IsTrue(arbiter.ProposeWithToken(
                BehaviorLayer.PlayerCue, "FETCH", token1, 30000, interruptible: false));
            Assert.AreEqual("FETCH", arbiter.CurrentActionKey);
            Assert.IsTrue(arbiter.CurrentToken.Matches(token1));

            // Stale callback for an older token (e.g. token0) cannot clear it
            var staleToken = new ActionToken(1, 0, "pet_randy");
            Assert.IsFalse(arbiter.CompleteIfCurrent(staleToken));
            Assert.AreEqual("FETCH", arbiter.CurrentActionKey);

            // Valid completion clears it
            Assert.IsTrue(arbiter.CompleteIfCurrent(token1));
            Assert.AreEqual("CALM_IDLE", arbiter.CurrentActionKey);

            // Now second action starts
            Assert.IsTrue(arbiter.ProposeWithToken(
                BehaviorLayer.PlayerCue, "FETCH", token2, 30000, interruptible: false));
            Assert.IsTrue(arbiter.CurrentToken.Matches(token2));

            // Delayed completion from token1 arrives: must NOT clear token2
            Assert.IsFalse(arbiter.CompleteIfCurrent(token1));
            Assert.AreEqual("FETCH", arbiter.CurrentActionKey);
            Assert.IsTrue(arbiter.CurrentToken.Matches(token2));
        }
    }
}
