# ADR-008: AR Foundation 6.4.2 instead of the 6.4.1 named in §0

- **Status:** ACCEPTED
- **Date:** 2026-08-01
- **Supersedes:** the AR Foundation version named in GW-ARCH-001 §0 and Appendix A
- **Requires:** a new specification version per the §0 change-control clause

## Context

GW-ARCH-001 §0 states:

> The shipping client SHALL pin Unity 6000.0.74f1, Niantic Spatial SDK 4.1.0, and
> AR Foundation 6.4.1.

These three cannot hold simultaneously. NSDK 4.1.0's own package manifest, read from the
published tag, declares:

```json
"dependencies": {
  "com.unity.xr.arfoundation": "6.4.2",
  "com.unity.xr.arcore": "6.4.2",
  "com.unity.xr.arkit": "6.4.2",
  ...
}
```

Source: `https://raw.githubusercontent.com/nianticspatial/nsdk-library-upm/4.1.0-26051913/package.json`

Unity's package resolver treats a package dependency as a minimum and will upgrade
AR Foundation to 6.4.2 to satisfy NSDK regardless of what the project manifest requests.
Pinning 6.4.1 alongside NSDK 4.1.0 therefore does not produce a 6.4.1 build — it produces
a 6.4.2 build with a manifest that misreports it, which is worse than an honest 6.4.2 pin
because §16 requires builds be reproducible from a recorded configuration.

Two related corrections were made at the same time:

- The package **name** in the project manifest was `com.nianticspatial.sdk`; the published
  package is `com.nianticspatial.nsdk`.
- The project manifest carried a scoped registry pointing at `registry.npmjs.org`. NSDK is
  not published to npm — `https://registry.npmjs.org/com.nianticspatial.sdk` returns 404.
  The official install path per Niantic's setup documentation is a git URL.

Both were authoring errors in this repository, not spec deviations.

## Decision

Pin AR Foundation, ARCore, and ARKit to **6.4.2**, and install NSDK from the
build-stamped git tag:

```
"com.nianticspatial.nsdk": "https://github.com/nianticspatial/nsdk-library-upm.git#4.1.0-26051913"
```

The tag is used rather than a branch so the dependency is immutable, satisfying §16's
reproducibility requirement more strongly than a floating reference would.

NSDK's transitive dependencies (`com.unity.xr.core-utils` 2.1.0,
`com.unity.xr.management` 4.0.1, `com.unity.editorcoroutines` 1.0.0,
`com.unity.burst` 1.8.17) are pinned explicitly so `packages-lock.json` records intended
versions rather than whatever the resolver reaches first.

## Consequences

- §0 and Appendix A must be amended to read 6.4.2. Until GW-ARCH-001 is reissued, this
  ADR is the authority for that one value.
- The delta is a patch release within the same minor line. No AR Foundation API used by
  `Gibi.Spatial` changes between 6.4.1 and 6.4.2.
- **Unity 6000.0.74f1 is unaffected** and remains correct — Niantic's own documentation
  states NSDK "only supports Unity LTS and Unity 6000.0.74f1," independently confirming
  the §0 editor pin.
- Any future NSDK upgrade must re-check this constraint: the AR Foundation floor is set by
  NSDK, not by GibiWorld.

## Alternatives considered

**Pin an older NSDK that depends on AR Foundation 6.4.1.** Rejected: §0 also pins NSDK
4.1.0, so this trades one deviation for another while giving up VPS2 fixes.

**Drop NSDK for P0 and defer the conflict.** Technically viable — §18 makes P0 local
placement only, and no code in `Gibi.Spatial` calls NSDK. Rejected because the conflict is
structural and would resurface unchanged at P1, when VPS2 sites arrive.

**Force 6.4.1 and override the resolver.** Rejected: unsupported by Unity, and it would
produce a manifest that does not describe the resulting build.
