using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gibi.Pets
{
    /// <summary>
    /// One candidate animation for a canonical action. A negative speed multiplier plays
    /// the clip backwards, which lets the P0 dog rise using its authored sleep transition
    /// until native down/rise clips are delivered.
    /// </summary>
    [Serializable]
    public struct PetClipOption
    {
        [SerializeField] private string clipName;
        [SerializeField] private float speedMultiplier;

        public string ClipName => clipName;
        public float SpeedMultiplier => Mathf.Approximately(speedMultiplier, 0f)
            ? 1f
            : speedMultiplier;

        public PetClipOption(string clipName, float speedMultiplier = 1f)
        {
            this.clipName = clipName;
            this.speedMultiplier = speedMultiplier;
        }
    }

    [Serializable]
    public struct PetClipBinding
    {
        [SerializeField] private string canonicalKey;
        [SerializeField] private PetClipOption[] options;

        public string CanonicalKey => canonicalKey;
        public IReadOnlyList<PetClipOption> Options => options;

        public PetClipBinding(string canonicalKey, params PetClipOption[] options)
        {
            this.canonicalKey = canonicalKey;
            this.options = options;
        }
    }

    public readonly struct PetClipResolution
    {
        public readonly string RequestedKey;
        public readonly string ClipName;
        public readonly float SpeedMultiplier;

        public bool IsValid => !string.IsNullOrEmpty(ClipName);
        public bool IsSubstituted => !string.Equals(
            RequestedKey, ClipName, StringComparison.Ordinal);

        public PetClipResolution(string requestedKey, string clipName,
                                 float speedMultiplier = 1f)
        {
            RequestedKey = requestedKey;
            ClipName = clipName;
            SpeedMultiplier = Mathf.Approximately(speedMultiplier, 0f)
                ? 1f
                : speedMultiplier;
        }
    }

    /// <summary>
    /// Per-asset presentation data. The signed GLB remains immutable verified content;
    /// this profile records the client-side orientation/grounding correction, socket
    /// contract, and honest P0 substitutions for clips the asset does not natively carry.
    /// </summary>
    [CreateAssetMenu(fileName = "PetAnimationProfile",
        menuName = "GibiWorld/Pets/Animation Profile")]
    public sealed class PetAnimationProfile : ScriptableObject
    {
        public const string Randy11PresetId = "asset_01J8ZQK5T7VN2MXR4WD6GHYAB3";

        [Header("Asset")]
        [SerializeField] private string petAssetId;
        [Tooltip("Local correction applied to the verified GLB holder, in metres.")]
        [SerializeField] private Vector3 assetLocalPosition;
        [Tooltip("The source dog faces +X; Unity gameplay movement owns +Z.")]
        [SerializeField] private Vector3 assetLocalEuler;

        [Header("Mouth Socket")]
        [SerializeField] private string mouthBoneName = "jaw";
        [SerializeField] private Vector3 mouthSocketLocalPosition;
        [SerializeField] private Vector3 mouthSocketLocalEuler;

        [Header("Canonical Clip Bindings")]
        [SerializeField] private PetClipBinding[] clipBindings = Array.Empty<PetClipBinding>();

        public string PetAssetId => petAssetId;
        public Vector3 AssetLocalPosition => assetLocalPosition;
        public Quaternion AssetLocalRotation => Quaternion.Euler(assetLocalEuler);
        public string MouthBoneName => mouthBoneName;
        public Vector3 MouthSocketLocalPosition => mouthSocketLocalPosition;
        public Quaternion MouthSocketLocalRotation => Quaternion.Euler(mouthSocketLocalEuler);
        public IReadOnlyList<PetClipBinding> ClipBindings => clipBindings;

        public bool Supports(string assetId)
            => string.Equals(petAssetId, assetId, StringComparison.Ordinal);

        public bool TryResolve(string requested, IReadOnlyCollection<string> available,
                               out PetClipResolution resolution)
        {
            if (!string.IsNullOrEmpty(requested) && available != null)
            {
                for (int i = 0; i < clipBindings.Length; i++)
                {
                    var binding = clipBindings[i];
                    if (!string.Equals(binding.CanonicalKey, requested, StringComparison.Ordinal))
                        continue;

                    var options = binding.Options;
                    if (options == null) break;

                    for (int j = 0; j < options.Count; j++)
                    {
                        var option = options[j];
                        if (!Contains(available, option.ClipName)) continue;

                        resolution = new PetClipResolution(
                            requested, option.ClipName, option.SpeedMultiplier);
                        return true;
                    }
                    break;
                }
            }

            resolution = default;
            return false;
        }

        /// <summary>
        /// Used both by SceneBuilder when authoring the checked-in profile asset and as a
        /// fail-safe for old generated scenes that predate the serialized reference.
        /// </summary>
        public void ApplyRandy11P0Defaults()
        {
            petAssetId = Randy11PresetId;

            // Blender inspection of the corrected 0.50 m-shoulder export measured the
            // lowest evaluated vertex at -0.514626 m. The source faces +X, so -90 yaw
            // maps its forward axis onto Unity +Z without giving motion ownership to art.
            assetLocalPosition = new Vector3(0f, 0.514626f, 0f);
            assetLocalEuler = new Vector3(0f, -90f, 0f);

            mouthBoneName = "jaw";
            mouthSocketLocalPosition = Vector3.zero;
            mouthSocketLocalEuler = Vector3.zero;

            clipBindings = new[]
            {
                new PetClipBinding("down",
                    new PetClipOption("down"),
                    new PetClipOption("sit"),
                    new PetClipOption("idle_a")),
                new PetClipBinding("rise",
                    new PetClipOption("rise"),
                    new PetClipOption("stand"),
                    new PetClipOption("sit", -1f),
                    new PetClipOption("idle_a")),
                // Randy11's authored sleep clip rotates the skinned root onto its back
                // and translates it above the floor when imported as Legacy animation.
                // Use its stable sit loop for visible P0 shelter rest until corrected
                // source animation is signed and shipped.
                new PetClipBinding("sleep",
                    new PetClipOption("sit"),
                    new PetClipOption("idle_a")),
                new PetClipBinding("pickup",
                    new PetClipOption("pickup"),
                    new PetClipOption("pet_react"),
                    new PetClipOption("sit"),
                    new PetClipOption("idle_a")),
                new PetClipBinding("carry",
                    new PetClipOption("carry"),
                    new PetClipOption("walk"),
                    new PetClipOption("run"),
                    new PetClipOption("idle_a")),
                new PetClipBinding("drop",
                    new PetClipOption("drop"),
                    new PetClipOption("pet_react"),
                    new PetClipOption("sit"),
                    new PetClipOption("idle_a")),
            };
        }

        public static PetAnimationProfile CreateRandy11P0Runtime()
        {
            var profile = CreateInstance<PetAnimationProfile>();
            profile.name = "Randy11P0RuntimeProfile";
            profile.hideFlags = HideFlags.HideAndDontSave;
            profile.ApplyRandy11P0Defaults();
            return profile;
        }

        private static bool Contains(IReadOnlyCollection<string> available, string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            foreach (var clip in available)
                if (string.Equals(clip, value, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
