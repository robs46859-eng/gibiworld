// GW-ARCH-001 section 16 — "Client builds SHALL be reproducible from a signed Git commit,
// Unity editor revision, packages-lock.json hash, Addressables/content catalog hash, and
// build configuration manifest."
//
// Batch-mode build so the same command produces the same APK from the same commit, and so
// a build can be reproduced in CI without a human clicking through Build Profiles.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Gibi.Editor
{
    public static class AndroidBuild
    {
        [MenuItem("GibiWorld/Build Android (development)")]
        public static void BuildDevMenu() => Build(development: true);

        public static void BuildDevForCI()
        {
            var report = Build(development: true);
            EditorApplication.Exit(report != null &&
                                   report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        private static BuildReport Build(bool development)
        {
            // Section 16 records what produced this artefact.
            string commit = TryGit("rev-parse --short HEAD") ?? "unknown";
            string lockHash = HashOf("Packages/packages-lock.json");

            Debug.Log($"[GibiWorld] Build config -- editor {Application.unityVersion}, " +
                      $"commit {commit}, packages-lock {lockHash}");

            var scenes = new[]
            {
                SceneValidator.BootstrapScene,
                SceneValidator.ARWorldScene,
                SceneValidator.PetSandboxScene,
            }.Where(File.Exists).ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[GibiWorld] No scenes found. Run GibiWorld > Build P0 Scenes first.");
                return null;
            }

            // GW-AR-001 gates the build. Shipping a scene with two ARSessions is a
            // release-gate failure (section 19), so it fails here rather than on device.
            var sceneErrors = SceneValidator.ValidateAll();
            if (sceneErrors.Count > 0)
            {
                Debug.LogError("[GibiWorld] Scene validation failed; refusing to build:\n" +
                               string.Join("\n", sceneErrors));
                return null;
            }

            Directory.CreateDirectory("Builds");
            string apk = Path.Combine("Builds", $"GibiWorld-{commit}.apk");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = apk,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = development
                    ? BuildOptions.Development | BuildOptions.AllowDebugging
                    : BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;

            Debug.Log($"[GibiWorld] Build {s.result} -- {s.totalSize / (1024 * 1024)} MB, " +
                      $"{s.totalTime.TotalSeconds:F0}s, output {apk}");

            if (s.result != BuildResult.Succeeded)
                foreach (var step in report.steps)
                    foreach (var msg in step.messages)
                        if (msg.type is LogType.Error or LogType.Exception)
                            Debug.LogError($"  {step.name}: {msg.content}");

            return report;
        }

        private static string HashOf(string path)
        {
            if (!File.Exists(path)) return "missing";
            using var sha = System.Security.Cryptography.SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)))
                               .Replace("-", "").Substring(0, 12).ToLowerInvariant();
        }

        private static string TryGit(string args)
        {
            try
            {
                var p = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo("git", args)
                    {
                        RedirectStandardOutput = true, UseShellExecute = false,
                        CreateNoWindow = true, WorkingDirectory = Directory.GetCurrentDirectory(),
                    }
                };
                p.Start();
                string outp = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit(4000);
                return string.IsNullOrEmpty(outp) ? null : outp;
            }
            catch { return null; }
        }
    }
}
