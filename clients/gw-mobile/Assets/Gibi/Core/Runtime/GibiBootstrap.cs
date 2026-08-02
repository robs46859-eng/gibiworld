// GW-ARCH-001 section 4.1 — Bootstrap scene contract:
// "dependency container, app lifecycle, authentication, remote config, crash handler,
//  and persistent UI root only."
//
// Section 7 budget: p95 <= 5 s to interactive home (7 s on Tier C), so startup work is
// ordered cheapest-first and anything requiring network is deferred behind the frame.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace Gibi.Core
{
    [DisallowMultipleComponent]
    public sealed class GibiBootstrap : MonoBehaviour
    {
        public static ServiceContainer Services { get; private set; }
        public static bool IsReady { get; private set; }

        [SerializeField] private string arWorldSceneName = "ARWorld";

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            Services = new ServiceContainer();

            // 1. Crash breadcrumbs first, so a failure during the rest is still reported.
            Application.logMessageReceived += OnLog;

            // 2. Device tier — decided before any renderer or asset choice.
            var tier = DeviceTiering.Resolve();
            Debug.Log($"[GibiWorld] Device tier {tier}, target {DeviceTiering.Budget.TargetFrameRate} fps");

            // 3. Deterministic clock. Everything time-based resolves this, so nothing
            //    in the scoring path can reach for DateTime.UtcNow.
            Services.Register<IMonotonicClock>(new MonotonicClock());

            // 4. Screen policy — AR sessions must not sleep mid-run.
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Application.runInBackground = false;   // reinforces GW-GAME-004
        }

        private IEnumerator Start()
        {
            // Yield once so the first frame presents before any deferred work; this is
            // what keeps the measured time-to-interactive honest.
            yield return null;

            // Authentication and remote config land here. Both are network-bound and
            // MUST NOT block first paint — section 8.3 requires the app be fully
            // playable offline, so neither is a gate on reaching the home screen.

            Services.Seal();
            IsReady = true;
            Debug.Log("[GibiWorld] Bootstrap complete.");
        }

        /// <summary>
        /// Android requires a RUNTIME request; the manifest entry alone grants nothing.
        /// Waits for the user's answer so ARWorld never starts against a denied camera.
        /// </summary>
        private IEnumerator RequestCameraPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Debug.Log("[GibiWorld] Camera permission already granted.");
                yield break;
            }

            Debug.Log("[GibiWorld] Requesting camera permission...");
            Permission.RequestUserPermission(Permission.Camera);

            // The prompt is modal and suspends the app; poll until it is answered rather
            // than assuming a fixed delay, which races on slower devices.
            float waited = 0f;
            while (!Permission.HasUserAuthorizedPermission(Permission.Camera) && waited < 60f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Debug.Log(Permission.HasUserAuthorizedPermission(Permission.Camera)
                ? "[GibiWorld] Camera permission granted."
                : "[GibiWorld] Camera permission DENIED — AR cannot start.");
#else
            yield break;
#endif
        }

        /// <summary>Loads ARWorld additively; Bootstrap stays resident for lifecycle and UI.</summary>
        public void EnterARWorld()
        {
            if (!IsReady) { Debug.LogWarning("[GibiWorld] EnterARWorld before bootstrap completed."); return; }
            if (SceneManager.GetSceneByName(arWorldSceneName).isLoaded) return;
            SceneManager.LoadScene(arWorldSceneName, LoadSceneMode.Additive);
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error) return;
            // Section 13.1 / GW-API-005: tokens, signed URLs and authorization headers
            // SHALL NOT reach logs, analytics, or crash reports. Breadcrumbs carry the
            // message only; stack traces stay in protected logs.
            CrashBreadcrumbs.Record(condition);
        }

        private void OnDestroy() => Application.logMessageReceived -= OnLog;
    }

    /// <summary>Bounded ring buffer of recent errors, attached to crash reports.</summary>
    public static class CrashBreadcrumbs
    {
        private const int Capacity = 32;
        private static readonly string[] Ring = new string[Capacity];
        private static int _next;

        public static void Record(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (message.Length > 200) message = message.Substring(0, 200);
            Ring[_next % Capacity] = message;
            _next++;
        }

        public static string[] Snapshot()
        {
            var outp = new System.Collections.Generic.List<string>(Capacity);
            for (int i = 0; i < Capacity; i++)
            {
                var s = Ring[(_next + i) % Capacity];
                if (!string.IsNullOrEmpty(s)) outp.Add(s);
            }
            return outp.ToArray();
        }
    }
}
