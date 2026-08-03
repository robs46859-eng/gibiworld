using System.Collections.Generic;
using Gibi.Pets;
using NUnit.Framework;
using UnityEngine;

namespace Gibi.Tests
{
    public sealed class PetAnimationProfileTests
    {
        [Test]
        public void Randy_profile_grounds_the_scaled_dog_and_faces_Unity_forward()
        {
            var profile = PetAnimationProfile.CreateRandy11P0Runtime();
            try
            {
                Assert.That(profile.AssetLocalPosition.y, Is.EqualTo(0.514626f).Within(0.000001f));

                Vector3 correctedForward = profile.AssetLocalRotation * Vector3.right;
                Assert.That(correctedForward.x, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(correctedForward.z, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(profile.MouthBoneName, Is.EqualTo("jaw"));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Randy_profile_resolves_P0_actions_without_the_unstable_sleep_clip()
        {
            var profile = PetAnimationProfile.CreateRandy11P0Runtime();
            var available = new HashSet<string>
            {
                "greet", "idle_a", "pet_react", "run",
                "sit", "sleep", "success", "walk"
            };

            try
            {
                AssertResolution(profile, available, "down", "sit", 1f);
                AssertResolution(profile, available, "rise", "sit", -1f);
                AssertResolution(profile, available, "sleep", "sit", 1f);
                AssertResolution(profile, available, "pickup", "pet_react", 1f);
                AssertResolution(profile, available, "carry", "walk", 1f);
                AssertResolution(profile, available, "drop", "pet_react", 1f);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        private static void AssertResolution(
            PetAnimationProfile profile,
            IReadOnlyCollection<string> available,
            string requested,
            string expectedClip,
            float expectedSpeed)
        {
            Assert.IsTrue(profile.TryResolve(requested, available, out var resolution));
            Assert.That(resolution.ClipName, Is.EqualTo(expectedClip));
            Assert.That(resolution.SpeedMultiplier, Is.EqualTo(expectedSpeed));
            Assert.IsTrue(resolution.IsSubstituted);
        }
    }
}
