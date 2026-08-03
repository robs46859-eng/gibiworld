using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Gibi.Pets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gibi.Tests
{
    public sealed class SandboxVerticalSliceTests
    {
        [UnityTest]
        public IEnumerator Verified_dog_fetches_real_toy_contract_then_rests_at_house_threshold()
        {
            var world = new GameObject("PlayModeWorld");
            var boundary = world.AddComponent<SandboxBoundary>();
            boundary.Configure(new Vector2(2.1f, 2.1f));
            var profile = PetAnimationProfile.CreateRandy11P0Runtime();
            var cts = new CancellationTokenSource();

            PetController pet = null;
            GameObject toyGo = null;
            GameObject houseGo = null;
            try
            {
                var spawner = new PetSpawner(
                    Shader.Find("Universal Render Pipeline/Lit"), profile);

                Task<int> keys = spawner.LoadTrustedKeysAsync(cts.Token);
                yield return WaitFor(keys, 5f, "trusted key load");
                Assert.AreEqual(1, keys.Result);

                Task<PetSpawnResult> spawn = spawner.SpawnAsync(
                    PetAnimationProfile.Randy11PresetId,
                    new Pose(Vector3.zero, Quaternion.identity),
                    world.transform, cts.Token);
                yield return WaitFor(spawn, 12f, "verified pet spawn");

                Assert.IsTrue(spawn.Result.Success, spawn.Result.FailureCode);
                pet = spawn.Result.Pet;
                pet.ConfigureBoundary(boundary);
                Assert.NotNull(pet.MouthSocket, "jaw-based MouthSocket must exist");

                bool hasApprovedTexturedMaterial = false;
                foreach (var renderer in pet.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material == null ||
                            material.shader.name != "Universal Render Pipeline/Lit" ||
                            !material.HasProperty("_BaseMap") ||
                            material.GetTexture("_BaseMap") == null) continue;
                        hasApprovedTexturedMaterial = true;
                    }
                }
                Assert.IsTrue(hasApprovedTexturedMaterial,
                    "Verified dog must retain its base texture on the approved URP shader");

                toyGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                toyGo.name = "FetchToy";
                toyGo.transform.SetParent(world.transform, false);
                toyGo.transform.localScale = Vector3.one * 0.067f;
                toyGo.transform.position = new Vector3(0.85f, 0.0335f, 0.30f);
                var toy = toyGo.AddComponent<FetchToy>();
                toy.Configure(0.0335f);

                Assert.IsTrue(pet.CueFetch(toy, Vector3.zero));
                float fetchDeadline = Time.realtimeSinceStartup + 8f;
                while (pet.CompletedFetches != 1 && Time.realtimeSinceStartup < fetchDeadline)
                    yield return null;
                Assert.AreEqual(1, pet.CompletedFetches,
                    $"Timed out waiting for fetch completion. " +
                    $"stage={pet.CurrentFetchStage}, position={pet.transform.position:F3}, " +
                    $"target={pet.NavigationTarget:F3}, hasTarget={pet.HasNavigationTarget}, " +
                    $"heading={pet.NavigationHeadingDeg:F1}, gait={pet.CurrentGait}, " +
                    $"action={pet.CurrentAction}, toyHeld={toy.IsHeld}.");
                Assert.AreEqual(FetchStage.Idle, pet.CurrentFetchStage);
                Assert.IsFalse(toy.IsHeld);
                Assert.Greater(pet.DistanceTravelledM, 0.5d);

                houseGo = new GameObject("DogHouse");
                houseGo.transform.SetParent(world.transform, false);
                houseGo.transform.localPosition = new Vector3(0f, 0f, 1.55f);
                var rest = houseGo.AddComponent<RestAffordance>();
                rest.ConfigureVisibleThresholdRest(0.0287f, 1.084f);

                Assert.IsTrue(pet.CueRest(rest));
                float restDeadline = Time.realtimeSinceStartup + 8f;
                while (!pet.IsEngaged && Time.realtimeSinceStartup < restDeadline)
                    yield return null;
                Assert.IsTrue(pet.IsEngaged,
                    $"Timed out waiting for rest engagement. " +
                    $"position={pet.transform.position:F3}, " +
                    $"approach={rest.ApproachPointWorld:F3}, " +
                    $"distance={Vector3.Distance(pet.transform.position, rest.ApproachPointWorld):F3}, " +
                    $"target={pet.NavigationTarget:F3}, hasTarget={pet.HasNavigationTarget}, " +
                    $"heading={pet.NavigationHeadingDeg:F1}, gait={pet.CurrentGait}, " +
                    $"action={pet.CurrentAction}.");
                Assert.That(Vector3.Distance(
                    pet.transform.position, rest.EngagedAnchorWorld), Is.LessThan(0.01f));
                Assert.That(pet.GetComponent<PetAnimator>().CurrentClip, Is.EqualTo("sit"),
                    "Randy11 uses its stable upright sit loop for visible shelter rest");

                foreach (var renderer in pet.GetComponentsInChildren<Renderer>(true))
                    Assert.IsTrue(renderer.enabled,
                        "P0 dog must remain visible while resting across the threshold");
            }
            finally
            {
                cts.Cancel();
                cts.Dispose();
                Object.Destroy(profile);
                if (world != null) Object.Destroy(world);
                if (toyGo != null && toyGo.transform.parent == null) Object.Destroy(toyGo);
                if (houseGo != null && houseGo.transform.parent == null) Object.Destroy(houseGo);
            }

            yield return null;
        }

        private static IEnumerator WaitFor(Task task, float timeoutS, string label)
        {
            float deadline = Time.realtimeSinceStartup + timeoutS;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsTrue(task.IsCompleted, $"Timed out waiting for {label}.");
            if (task.IsFaulted) throw task.Exception;
            Assert.IsFalse(task.IsCanceled, $"{label} was cancelled.");
        }

    }
}
