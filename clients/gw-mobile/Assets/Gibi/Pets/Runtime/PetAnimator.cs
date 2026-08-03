// The component that finally calls Play().
//
// The shipped preset GLB has carried 8 real animation clips -- 50 channels each over a
// 25-bone skin -- since the asset pipeline first ran, and PetAssetLoader already imports
// them with AnimationMethod.Legacy, which makes glTFast attach a working UnityEngine
// .Animation component to the instantiated pet. Nothing ever asked it to play. The pet
// has been standing perfectly still on device for want of one call.
//
// Legacy Animation is deliberate for P0 rather than an Animator Controller:
//   * no .controller asset to author, so SceneBuilder stays code-generated (section 16)
//   * CrossFade by clip NAME, which is exactly what a manifest gives us
//   * clip set varies per asset; an Animator Controller would bake one asset's clip list
//     into a shared asset and break the moment a second pet ships a different set
// The cost is no Animation Rigging layer, so foot IK and gaze (section 6.3) will need an
// Animator later. That is a P1 swap behind this same interface.
using System.Collections.Generic;
using UnityEngine;

namespace Gibi.Pets
{
    [DisallowMultipleComponent]
    public sealed class PetAnimator : MonoBehaviour
    {
        private Animation _animation;
        private readonly List<string> _available = new();
        private string _currentClip;
        private float _pendingLatencyS;
        private string _pendingClip;
        private float _pendingSpeed = 1f;
        private bool _pendingLoop;

        /// <summary>Clips this asset actually carries. Empty until Bind runs.</summary>
        public IReadOnlyList<string> AvailableClips => _available;
        public string CurrentClip => _currentClip;
        public bool IsBound => _animation != null && _available.Count > 0;

        /// <summary>
        /// Discover the Animation component glTFast attached and enumerate its clips.
        /// Called once, after the verified GLB is instantiated.
        /// </summary>
        public bool Bind(GameObject instantiatedAsset)
        {
            _animation = instantiatedAsset != null
                ? instantiatedAsset.GetComponentInChildren<Animation>(true)
                : null;

            _available.Clear();
            if (_animation == null) return false;

            foreach (AnimationState state in _animation)
            {
                if (state?.clip == null) continue;
                _available.Add(state.name);
                // Section 6.3: locomotion clips are in-place and the deterministic motion
                // controller owns translation. Legacy Animation would happily apply root
                // motion baked into a clip, so wrap mode is set explicitly rather than
                // trusting the asset.
                state.wrapMode = WrapMode.Once;
            }

            _animation.playAutomatically = false;
            _animation.cullingType = AnimationCullingType.BasedOnRenderers;
            return _available.Count > 0;
        }

        /// <summary>
        /// Request a clip. <paramref name="clipKey"/> is a CATALOG key -- resolution to
        /// something this asset owns happens here, so callers never need to know which
        /// clips a given pet shipped with.
        /// </summary>
        public void Play(string clipKey, in BehaviorModifiers mods, bool loop)
        {
            if (!IsBound) return;

            string resolved = ClipResolver.Resolve(clipKey, _available);
            if (resolved == null) return;

            // Intensity scales playback speed within a narrow, readable band. Below ~0.75
            // a quadruped gait reads as slow-motion rather than as calm, and above ~1.25
            // it reads as jittery.
            float speed = Mathf.Lerp(0.75f, 1.25f, Mathf.Clamp01(mods.Intensity));
            speed /= Mathf.Max(0.01f, mods.DurationScale);

            if (mods.LatencyMs > 0)
            {
                // Hesitation before acting. This is the single field that makes the pet
                // read as having decided something rather than having been triggered.
                _pendingClip = resolved;
                _pendingLatencyS = mods.LatencyMs / 1000f;
                _pendingSpeed = speed;
                _pendingLoop = loop;
                return;
            }

            Begin(resolved, speed, loop);
        }

        /// <summary>Immediate, no hesitation. Safety overrides must never wait.</summary>
        public void PlayImmediate(string clipKey, bool loop = false)
        {
            if (!IsBound) return;
            string resolved = ClipResolver.Resolve(clipKey, _available);
            if (resolved == null) return;
            _pendingClip = null;
            Begin(resolved, 1f, loop);
        }

        private void Begin(string clip, float speed, bool loop)
        {
            var state = _animation[clip];
            if (state == null) return;

            state.speed = speed;
            state.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;

            // CrossFade rather than Play: section 6.3 requires blends of at least 100 ms
            // and forbids snapping. 0.15 s sits above that floor with margin.
            _animation.CrossFade(clip, 0.15f);
            _currentClip = clip;
        }

        private void Update()
        {
            if (_pendingClip == null) return;

            _pendingLatencyS -= Time.deltaTime;
            if (_pendingLatencyS > 0f) return;

            Begin(_pendingClip, _pendingSpeed, _pendingLoop);
            _pendingClip = null;
        }

        /// <summary>True once the current non-looping clip has finished.</summary>
        public bool IsIdleOrFinished()
        {
            if (!IsBound || _currentClip == null) return true;
            var state = _animation[_currentClip];
            if (state == null) return true;
            if (state.wrapMode == WrapMode.Loop) return false;
            return !_animation.IsPlaying(_currentClip) || state.normalizedTime >= 1f;
        }

        /// <summary>
        /// Diagnostic only -- never player-visible. Reports which catalog clips this asset
        /// serves by substitution, so an artist can see what is worth authoring next.
        /// </summary>
        public string DescribeCoverage()
        {
            if (!IsBound) return "PetAnimator: not bound";
            var missing = ClipResolver.MissingNatively(_available);
            return $"PetAnimator: {_available.Count} clips present, " +
                   $"{missing.Count} catalog clips served by substitution";
        }
    }
}
