# Required Project Settings — GW-ARCH-001

Unity writes most of `ProjectSettings/*.asset` itself and rewrites them on open, so
hand-authoring the YAML invites silent drift. These are the settings the spec makes
normative; `Gibi.Editor.ProjectSettingsApplier` applies them from code so the result is
reproducible per §16 and reviewable in a diff.

| Setting | Required value | Source |
|---|---|---|
| Scripting backend | **IL2CPP** | §3.1 "gw-mobile: Unity IL2CPP app" |
| API compatibility | .NET Standard 2.1 | build size |
| Color space | **Linear** | §7 URP requirement |
| Graphics API (iOS) | Metal only | URP mobile |
| Graphics API (Android) | Vulkan, OpenGLES3 | URP mobile |
| Target frame rate | 60 (Tier A/B), 30 (Tier C) | §7 |
| Managed stripping | Low | preserve reflection in glTFast |
| Accelerometer frequency | Disabled | §13.2 minimise sensor capture |
| Internet access | Required | §11 |
| iOS camera usage string | Required, must name AR | store review |
| iOS location usage | **When-in-use only** | §13.2 no raw history |
| Android min SDK | 29 | ARCore + Vulkan |
| iOS min version | 13.0 | ARKit 4 depth |
| Mobile MTRendering | Enabled | frame budget |
| Static/Dynamic batching | Dynamic on, static off | §7 draw-call budget |
