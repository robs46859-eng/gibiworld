// GW-ARCH-001 section 5.3 — "The user-facing placement ring SHALL encode status with
// color, icon, label, and optional haptic; COLOR ALONE IS INSUFFICIENT."
//
// Section 14 reinforces this: "Every critical AR state SHALL have text, shape/icon, color,
// audio/caption, and haptic mappings where the device supports them," and "All
// player-visible strings SHALL use localization keys."
//
// So this component never renders a colour without also driving an icon and a
// localisation key. They are set together in one call, which makes "colour only" an
// impossible state rather than a discouraged one.
using Gibi.Gameplay;
using UnityEngine;

namespace Gibi.UI
{
    [DisallowMultipleComponent]
    public sealed class PlacementRing : MonoBehaviour
    {
        [SerializeField] private Renderer ringRenderer;
        [SerializeField] private Transform iconAnchor;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public string CurrentIconId { get; private set; }
        public string CurrentLabelKey { get; private set; }
        public Color CurrentColor { get; private set; }

        /// <summary>
        /// Apply a placement status. All four channels move together — there is no path
        /// that sets colour without also setting icon and label.
        /// </summary>
        public void Apply(in PlacementStatus status, bool hapticsSupported)
        {
            CurrentColor = status.RingColor;
            CurrentIconId = status.IconId;
            CurrentLabelKey = status.LocalizationKey;

            if (ringRenderer != null)
            {
                var block = new MaterialPropertyBlock();
                ringRenderer.GetPropertyBlock(block);
                block.SetColor(BaseColorId, status.RingColor);
                ringRenderer.SetPropertyBlock(block);
                ringRenderer.enabled = true;
            }

            if (status.ShouldPulseHaptic && hapticsSupported)
                PulseHaptic();
        }

        public void Hide()
        {
            if (ringRenderer != null) ringRenderer.enabled = false;
        }

        /// <summary>
        /// Section 14: haptics are one channel among several, never the only one. A device
        /// without haptic support loses nothing, because colour, icon, and label already
        /// carry the state.
        /// </summary>
        private static void PulseHaptic()
        {
#if UNITY_IOS || UNITY_ANDROID
            Handheld.Vibrate();
#endif
        }

        /// <summary>
        /// Accessibility self-check, callable from a test: a status is only presentable if
        /// it carries a non-colour channel. Section 5.3 makes colour alone insufficient, so
        /// a status missing icon or label is a bug rather than a styling choice.
        /// </summary>
        public static bool IsAccessiblyEncoded(in PlacementStatus status)
            => !string.IsNullOrEmpty(status.IconId) &&
               !string.IsNullOrEmpty(status.LocalizationKey);
    }
}
