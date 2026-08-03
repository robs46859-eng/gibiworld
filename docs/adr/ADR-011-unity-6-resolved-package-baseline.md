# ADR-011: Align direct package pins with the Unity 6 resolved baseline

- **Status:** ACCEPTED
- **Date:** 2026-08-03
- **Supersedes:** ADR-008 only where it pins NSDK transitive package versions

## Decision

The Unity 6000.0.74f1 client manifest will explicitly request the versions that its
Package Manager actually resolves:

| Package | Requested before | Unity 6 resolved baseline |
|---|---:|---:|
| `com.unity.burst` | 1.8.17 | **1.8.29** |
| `com.unity.test-framework` | 1.4.6 | **1.6.0** |
| `com.unity.xr.core-utils` | 2.1.0 | **2.6.0** |
| `com.unity.xr.management` | 4.0.1 | **4.5.4** |

The update SHALL be performed through Unity Package Manager, not by editing
`Packages/manifest.json` or `packages-lock.json` by hand.

## Context

ADR-008 pinned the minimum versions declared by NSDK 4.1.0. Unity 6 and the current
AR Foundation/ARCore dependency graph select newer compatible versions. As a result,
the manifest describes one graph while `packages-lock.json`, the Package Cache, and
Unity Package Manager report another. That violates the B1.4 lock/manifest agreement
gate and makes the requested package configuration a misleading build input.

The resolved versions above were read from Unity Package Manager in the pinned editor,
not inferred from the lock file alone. The full Gibi test suites and scene validator pass
against this graph.

## Options considered

1. **Force the older NSDK minimums.** Rejected. AR Foundation 6.4.2 and Unity 6 select
   newer compatible dependencies, so the lock would continue to contradict the manifest.
2. **Remove the four direct pins and accept transitive resolution.** Rejected. It makes
   future resolution sensitive to unrelated dependency changes and weakens the frozen
   package baseline.
3. **Pin the versions Unity 6 actually resolves.** Chosen. The requested and resolved
   graphs agree while every version remains explicit and reviewable.

## Why this option won

It is the only option that preserves both an explicit package baseline and an honest,
reproducible lock file without attempting to override Unity's supported dependency graph.

## Consequences

- The manifest, lock file, package baseline documentation, and verification gate must
  agree on these four versions before a build is called reproducible.
- The development APK may still be produced while this reconciliation is pending, but
  it must be labelled a non-promotable development snapshot.
- Any regressions found after reconciliation are package-baseline regressions and must
  be fixed or this ADR revisited; silent fallback to the older requested values is not
  permitted.

## Revisit triggers

- Unity editor revision changes.
- NSDK, AR Foundation, ARCore, or ARKit changes.
- Unity Package Manager resolves any of the four packages to a different version.
- The Gibi EditMode, PlayMode, scene-validation, or Android build gates regress.
