#!/usr/bin/env python3
"""
Static assembly-reference check for the Gibi.* assemblies — GW-ARCH-001 section 4.

Catches, without opening Unity:
  * provider-SDK leaks outside the named adapter
  * layering violations against the section 4 dependency table
  * asmdef references that do not cover what the source actually imports

The third check previously collapsed UnityEngine.XR.ARFoundation and
UnityEngine.XR.ARSubsystems into one requirement. They are SEPARATE assemblies
(Unity.XR.ARFoundation, Unity.XR.ARSubsystems), so a file importing only ARSubsystems
passed the check and then failed to compile. Each namespace now maps to its own assembly.
"""
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1] / "clients/gw-mobile/Assets/Gibi"

# namespace -> (assembly that provides it, assembly allowed to import it)
NAMESPACE_ASSEMBLY = {
    "UnityEngine.XR.ARFoundation": ("Unity.XR.ARFoundation", "Gibi.Spatial"),
    "UnityEngine.XR.ARSubsystems": ("Unity.XR.ARSubsystems", "Gibi.Spatial"),
    "Unity.XR.CoreUtils":          ("Unity.XR.CoreUtils",    "Gibi.Spatial"),
    "GLTFast":                     ("glTFast",               "Gibi.AssetRuntime"),
    "UnityEngine.Animations.Rigging": ("Unity.Animation.Rigging", "Gibi.Pets"),
    "UnityEngine.InputSystem":     ("Unity.InputSystem",     "Gibi.UI"),
}

ALLOWED_GIBI = {
    "Gibi.Core": set(),
    "Gibi.AssetRuntime": {"Gibi.Core"},
    "Gibi.Spatial": {"Gibi.Core"},
    "Gibi.Pets": {"Gibi.Core", "Gibi.AssetRuntime"},
    "Gibi.Gameplay": {"Gibi.Core", "Gibi.Spatial", "Gibi.Pets"},
    "Gibi.Networking": {"Gibi.Core"},
    "Gibi.AI": {"Gibi.Core", "Gibi.Pets", "Gibi.Networking"},
    "Gibi.Telemetry": {"Gibi.Core"},
}

FOLDERS = ["Core", "Spatial", "Pets", "AssetRuntime", "Gameplay",
           "Networking", "AI", "Telemetry", "UI", "Editor", "Tests"]


def assembly_of(path: pathlib.Path):
    parts = path.parts
    for f in FOLDERS:
        if f in parts:
            return "Gibi.Tests" if f == "Tests" else f"Gibi.{f}"
    return None


def main():
    problems = []
    asmdefs = {}
    for a in ROOT.rglob("*.asmdef"):
        d = json.loads(a.read_text())
        asmdefs[d["name"]] = {"refs": set(d.get("references", [])), "dir": a.parent}

    for cs in ROOT.rglob("*.cs"):
        asm = assembly_of(cs)
        if asm is None:
            problems.append(f"UNCLASSIFIED: {cs} — not under a known assembly folder")
            continue
        src = cs.read_text()

        for ns, (provider_asm, owner) in NAMESPACE_ASSEMBLY.items():
            if not re.search(r"^\s*using\s+" + re.escape(ns) + r"\s*;", src, re.M):
                continue
            # provider leak
            if asm not in (owner, "Gibi.Editor", "Gibi.Tests"):
                problems.append(
                    f"PROVIDER LEAK: {cs.relative_to(ROOT)} ({asm}) imports {ns}; "
                    f"section 4 permits only {owner}")
            # asmdef must actually grant the providing assembly
            entry = asmdefs.get(asm)
            if entry and provider_asm not in entry["refs"]:
                problems.append(
                    f"MISSING REF: {asm}.asmdef imports {ns} but does not reference "
                    f"{provider_asm} — WILL NOT COMPILE")

        if asm in ALLOWED_GIBI:
            for m in re.finditer(r"^\s*using\s+(Gibi\.[A-Za-z]+)\s*;", src, re.M):
                dep = m.group(1)
                if dep != asm and dep not in ALLOWED_GIBI[asm]:
                    problems.append(
                        f"LAYERING: {cs.relative_to(ROOT)} ({asm}) uses {dep}; "
                        f"allowed: {sorted(ALLOWED_GIBI[asm]) or 'none'}")

    if asmdefs.get("Gibi.Core", {}).get("refs"):
        problems.append("Gibi.Core must have NO references (section 4)")

    print(f"checked {len(list(ROOT.rglob('*.cs')))} files across {len(asmdefs)} assemblies")
    if problems:
        for p in sorted(set(problems)):
            print("  " + p)
        return 1
    print("  clean")
    return 0


if __name__ == "__main__":
    sys.exit(main())
