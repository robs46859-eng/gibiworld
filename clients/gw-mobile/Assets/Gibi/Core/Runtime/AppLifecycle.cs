// GW-ARCH-001 section 17 — GW-GAME-004: "App backgrounding makes active ranked run
// unranked." Acceptance test: lifecycle test.
//
// This is a SECURITY-adjacent rule, not a convenience: without it a player could
// background the app, walk the course, and resume to a favourable clock. The
// invalidation is therefore latched — once set it cannot be cleared by resuming.
using System;
using UnityEngine;

namespace Gibi.Core
{
    public interface IRankedRunGuard
    {
        bool HasActiveRankedRun { get; }
        void InvalidateRankedRun(string reasonCode);
    }

    [DisallowMultipleComponent]
    public sealed class AppLifecycle : MonoBehaviour
    {
        public static AppLifecycle Instance { get; private set; }

        private IRankedRunGuard _runGuard;

        /// <summary>Raised on pause/resume so telemetry and AR can react.</summary>
        public event Action<bool> ApplicationPaused;
        public event Action ApplicationQuitting;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void AttachRunGuard(IRankedRunGuard guard) => _runGuard = guard;

        private void OnApplicationPause(bool paused)
        {
            if (paused) InvalidateIfRanked("BACKGROUNDED");
            ApplicationPaused?.Invoke(paused);
        }

        // On some Android devices a task switch surfaces as focus loss without a pause
        // callback, so both paths must invalidate or the rule is bypassable.
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) InvalidateIfRanked("FOCUS_LOST");
        }

        private void OnApplicationQuit()
        {
            InvalidateIfRanked("APP_QUIT");
            ApplicationQuitting?.Invoke();
        }

        private void InvalidateIfRanked(string reason)
        {
            if (_runGuard == null || !_runGuard.HasActiveRankedRun) return;
            _runGuard.InvalidateRankedRun(reason);
            Debug.Log($"[GibiWorld] Ranked run invalidated: {reason} (GW-GAME-004)");
        }
    }
}
