// GW-ARCH-001 section 4 — "Assemblies SHALL reference inward through public interfaces.
// No assembly may call a provider SDK directly except its named adapter.
// CYCLIC ASSEMBLY REFERENCES ARE A BUILD FAILURE."
//
// Unity tolerates some layering mistakes silently; this turns the normative rule into
// an actual gate.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Gibi.Editor
{
    public static class AssemblyGraphCheck
    {
        /// <summary>
        /// Allowed dependencies per the section 4 table. An assembly may reference only
        /// what is listed here — anything else is a layering violation even if it compiles.
        /// </summary>
        private static readonly Dictionary<string, string[]> Allowed = new()
        {
            ["Gibi.Core"]         = new string[0],
            ["Gibi.AssetRuntime"] = new[] { "Gibi.Core", "GLTFast" },
            ["Gibi.Spatial"]      = new[] { "Gibi.Core", "Unity.XR.ARFoundation", "Unity.XR.CoreUtils" },
            ["Gibi.Pets"]         = new[] { "Gibi.Core", "Gibi.AssetRuntime", "Unity.Animation.Rigging" },
            ["Gibi.Gameplay"]     = new[] { "Gibi.Core", "Gibi.Spatial", "Gibi.Pets" },
            ["Gibi.Networking"]   = new[] { "Gibi.Core" },
            ["Gibi.AI"]           = new[] { "Gibi.Core", "Gibi.Pets", "Gibi.Networking" },
            ["Gibi.Telemetry"]    = new[] { "Gibi.Core" },
        };

        [MenuItem("GibiWorld/Check Assembly Graph")]
        public static void CheckMenu()
        {
            var errors = Check();
            if (errors.Count == 0) Debug.Log("[GibiWorld] Assembly graph OK: acyclic, layering respected.");
            else Debug.LogError("[GibiWorld] Assembly graph FAILED:\n" + string.Join("\n", errors));
        }

        public static void CheckForCI()
        {
            var errors = Check();
            if (errors.Count > 0)
            {
                Debug.LogError("[GibiWorld] Assembly graph FAILED:\n" + string.Join("\n", errors));
                EditorApplication.Exit(1);
            }
            EditorApplication.Exit(0);
        }

        public static List<string> Check()
        {
            var errors = new List<string>();
            var graph = new Dictionary<string, List<string>>();

            foreach (var path in AssetDatabase.FindAssets("t:asmdef")
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Where(p => p.StartsWith("Assets/Gibi/")))
            {
                var json = File.ReadAllText(path);
                var def = JsonUtility.FromJson<AsmDef>(json);
                if (def == null || string.IsNullOrEmpty(def.name)) continue;

                var refs = (def.references ?? new string[0])
                    .Select(r => r.StartsWith("GUID:")
                        ? SafeName(AssetDatabase.GUIDToAssetPath(r.Substring(5)))
                        : r)
                    .Where(r => !string.IsNullOrEmpty(r))
                    .ToList();

                graph[def.name] = refs;

                if (Allowed.TryGetValue(def.name, out var allowed))
                {
                    foreach (var r in refs.Where(r => r.StartsWith("Gibi.") && !allowed.Contains(r)))
                        errors.Add($"Section 4: {def.name} may not reference {r}. " +
                                   $"Allowed: [{string.Join(", ", allowed)}]");
                }
            }

            if (graph.TryGetValue("Gibi.Core", out var coreRefs) && coreRefs.Count > 0)
                errors.Add($"Section 4: Gibi.Core must have NO dependencies, found: " +
                           string.Join(", ", coreRefs));

            foreach (var cycle in FindCycles(graph))
                errors.Add("Section 4: cyclic assembly reference (build failure): " +
                           string.Join(" -> ", cycle));

            return errors;
        }

        private static string SafeName(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath)) return null;
            var def = JsonUtility.FromJson<AsmDef>(File.ReadAllText(assetPath));
            return def?.name;
        }

        /// <summary>Iterative DFS with an explicit stack so a deep graph cannot blow up.</summary>
        private static List<List<string>> FindCycles(Dictionary<string, List<string>> graph)
        {
            var cycles = new List<List<string>>();
            var state = new Dictionary<string, int>();   // 0 unvisited, 1 in-progress, 2 done
            var stack = new List<string>();

            void Visit(string node)
            {
                if (!graph.ContainsKey(node)) return;
                state.TryGetValue(node, out int s);
                if (s == 2) return;
                if (s == 1)
                {
                    int idx = stack.IndexOf(node);
                    var cyc = stack.Skip(idx).ToList();
                    cyc.Add(node);
                    cycles.Add(cyc);
                    return;
                }
                state[node] = 1;
                stack.Add(node);
                foreach (var dep in graph[node]) Visit(dep);
                stack.RemoveAt(stack.Count - 1);
                state[node] = 2;
            }

            foreach (var n in graph.Keys.ToList()) Visit(n);
            return cycles;
        }

        [System.Serializable]
        private class AsmDef
        {
            public string name;
            public string[] references;
        }
    }
}
