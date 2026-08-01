# Getting Started

## One-time setup

### 1. VS Code extensions
Open the repo in VS Code — it will prompt to install the recommended extensions.
The two that matter:

- **C# Dev Kit** (`ms-dotnettools.csdevkit`) — IntelliSense and navigation
- **Unity** (`visualstudiotoolsforunity.vstuc`) — debugger attach, Unity-aware analysis

### 2. .NET SDK
Not currently installed on this machine. The Unity extension expects it. If C#
IntelliSense fails to start after opening a `.cs` file, install the SDK:

```
brew install --cask dotnet-sdk
```

You do **not** need it to build the game — Unity compiles with its own toolchain. It is
only for editor tooling.

### 3. Open the Unity project once
Unity generates the `.csproj`/`.sln` files that VS Code reads. Until you open
`clients/gw-mobile` in Unity at least once, VS Code shows the `Gibi.*` code as plain
text with no IntelliSense.

---

## First run — in order

Open `clients/gw-mobile` in Unity **6000.0.74f1**, then from the menu bar:

1. **GibiWorld → Apply Required Project Settings** — IL2CPP, linear colour space,
   Metal/Vulkan, permission strings (§3.1, §7, §13.2)
2. **GibiWorld → Build P0 Scenes** — generates Bootstrap, ARWorld, PetSandbox (§4.1)
3. **GibiWorld → Validate Scenes** — GW-AR-001: exactly one ARSession, one XROrigin
4. **GibiWorld → Check Assembly Graph** — §4 layering and cycle check

All four should pass. If step 3 or 4 fails, the message names the requirement.

---

## Running the gates without opening Unity

`Cmd+Shift+B` runs **GATE: full local check**, which mirrors CI. Individual tasks are in
the Tasks menu:

| Task | What it proves |
|---|---|
| Spec: validate all contracts | JSON Schemas are valid draft 2020-12 |
| Assets: inspect source models | Triangle, bone, clip, and scale-curve audit |
| Assets: remediate randy11 | Full LOD chain, spec-compliant |
| Unity: validate scenes | GW-AR-001 |
| Unity: check assembly graph | §4 — no cycles, no provider-SDK leaks |
| Unity: run EditMode tests | The 27 GW-* named tests |

---

## Why the workspace excludes so much

`clients/gw-mobile/Library/` holds hundreds of thousands of generated files. Without the
exclusions in `.vscode/settings.json`, VS Code's file watcher and search grind to a halt
on a Unity project. If search feels slow, check those exclusions survived.

---

## Layout worth knowing

```
contracts/          OpenAPI 3.1 + JSON Schemas — the source of truth (§11)
clients/gw-mobile/Assets/Gibi/
    Core/           deterministic math, IDs, clocks. NO dependencies (§4)
    Spatial/        the ONLY assembly that may touch AR Foundation (§4)
    Pets/           behaviour arbiter, 50 Hz motion
    Gameplay/       placement, training, scoring
    Editor/         scene builder, validators — not shipped
tools/gw-asset-worker/   Blender validators and remediation
db/migrations/      forward-only PostgreSQL + PostGIS
docs/               ADRs, traceability, asset inventory
```

Start with `CHECKLIST.md` for status and `docs/TRACEABILITY.md` for the 40 GW-*
requirements.
