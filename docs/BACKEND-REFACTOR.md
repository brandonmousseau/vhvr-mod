# SteamVR backend extraction: stage 1

Review branch: `codex/steamvr-backend-refactor`.

Baseline: upstream master `0604e05a3284ba373f3630d2e4d473370a254663`
(October 9, 2026), containing release `v0.11.1`. This branch is a refactor
candidate for review and SteamVR regression testing. Runtime parity has not
yet been established on physical hardware.

## Scope

Gameplay and UI access runtime services through `IVRBackend`, `IVRInputBackend`,
`IVRRigBackend`, `IVRKeyboardBackend` and `IVROverlayBackend` in
`ValheimVRMod/VRCore/Backends`. The only provider is SteamVR. There is no backend
selector, OpenXR implementation, companion dependency, configuration migration
or additional gameplay feature.

- `SteamVRRuntime.cs` contains the existing initialization, retry, mirror and
  recenter implementation. `VRManager` preserves its entry points and delegates.
  The existing process probe still implements automatic desktop/VR selection.
- `SteamVRInputBackend.cs` forwards individual action reads, action sets, native
  callbacks, binding UI and haptics. The action facade preserves the exact
  upstream paths and capitalization. No input polling loop or edge synthesis
  is added.
- `SteamVRRigBackend.cs` wraps the authored player, hands, pointers and trackers.
  The wrappers are ordinary managed views, not new Unity behaviours. The original
  components publish poses, calculate velocities, update pointers and render
  fades. Per-hand haptics retain the action actually assigned to that hand.
- Keyboard and overlay adapters contain native calls. Text editing, chat,
  caret/selection handling, GUI blits and recenter policy stay with their existing
  gameplay/UI owners. The keyboard flags and native payload bytes are preserved.
- The application manifest helper, tracker provider and three SDK-targeted
  Harmony patches move under the backend directory. Their fully qualified type
  names remain intact; existing Harmony discovery and component identity remain.
- Unity assets, prefabs, shaders, generated SteamVR catalogs, binding JSON,
  configuration keys/defaults, plugin version and production project are unchanged.

The public hand/pointer/action helper types become backend-neutral. This is a
source API change for code explicitly typed against those helpers; this branch
does not claim binary compatibility for such external consumers.

## Review order

1. Read the contracts and the runtime delegation. The source gate compares every
   extracted runtime method body with the pinned upstream implementation.
2. Review the native adapters, especially hand velocity forwarding, Unity object
   equality, per-hand haptic identity and event subscription/removal.
3. Review the gameplay/UI substitutions. The changes should only replace provider
   types/calls or move SDK operations behind the contracts.
4. Review the tests and complete the hardware comparison below before treating
   stage 1 as behaviorally equivalent.

The old OpenXR development branch is separate. Its replacement rig components,
binding editor, optional physics/finger features, rendering changes and unrelated
cleanup are not part of this branch.

## Reproduce the automated checks

Requirements: .NET 8 SDK or newer, Python 3, the tracked SteamVR and generated
action sources, and a legally owned Valheim installation containing BepInEx and
the normal VHVR reference libraries. A sparse checkout needs
`Unity/ValheimVR/Assets/SteamVR` and `Unity/ValheimVR/Assets/SteamVR_Input`.

```powershell
./scripts/Test-BackendRefactor.ps1 -ValheimDir 'D:\SteamLibrary\steamapps\common\Valheim'
```

The helper compiles the production gameplay sources and original managed SteamVR
sources against local game references, then loads those DLLs in the check runner.
Its diagnostic build targets .NET Framework 4.7.2 with the owned Mono/Unity
references; the production project's .NET 4.6/Unity editor build is unchanged.
No game files are deployed, packaging command is run, or VR runtime is started.
Generated files stay in ignored `bin`/`obj` directories. No owned assemblies are
committed or distributed.

Checked October 10, 2026 with Valheim 1.0.16 / Unity 6000.0.75f1 references:

| Check | Result |
| --- | --- |
| Gameplay and managed SteamVR source build | Pass; zero errors, existing SDK/Unity compatibility warnings |
| Native SDK differential checks | Pass; 44,173 assertions over published input snapshots, binding state, callbacks, helper contracts and haptic identity |
| Catalog coverage | 32 boolean, four axis, four pose and two haptic actions; two action sets; all 16 input sources |
| Source boundary | Pass; no executable Valve/OpenVR SDK references outside the provider directory |
| Runtime extraction | Pass; all 14 original runtime method bodies preserved, allowing only the neutral mirror-enum conversion |
| SDK/assets/catalog/project/version diff | Empty |
| Physical SteamVR regression comparison | Pending |
| Normal Unity editor / .NET 4.6 release build | Not validated in this environment |

The check runner seeds the real SDK's managed input storage, compares direct SDK
reads with the adapters, and drives managed callbacks. Native input polling is
disabled in that process because Unity's native frame clock is unavailable.
These assertions do not establish tracking timing, rendering, hardware haptics,
controller comfort or frame pacing. Haptic tests check action identity, not
actuator output.

The original unmodified source built successfully with the same owned-reference
diagnostic harness before extraction. An attempt to use the normal production
project locally was blocked by the available Unity/SDK reference layout and
targeting packs; it is not counted as a passing release build.

Validated gameplay DLL SHA-256:
`B2C5C7F93524AFFA71D91AE4177450D42301F619B44A33D51A0823BCA51A681A`.
This identifies the local diagnostic build, not a released package.

## Feature-freeze comparison

Compare the candidate with **the exact source baseline above**, built using the
same compiler, reference assemblies, configuration and complete upstream assets.
Comparing only with the older `v0.11.1` archive would also include subsequent
upstream changes. If upstream finishes more loose ends before the freeze, update
the pinned baseline and repeat both builds and all checks.

Use the same saves, settings, bindings, headset/runtime versions and controller
layout for both runs. Record source/DLL hashes, hardware, steps and differences.

| Area | Required checks on baseline and candidate |
| --- | --- |
| Startup and lifecycle | Explicit desktop/VR and automatic mode; SteamVR already running/absent; initialization failure; title to world, logout and exit |
| Input and gameplay | Custom/default bindings, handedness, movement, action-set transitions, building, combat, bows/crossbows, grabbing, steering/rowing and keyboard/mouse VR |
| Rig and tracking | Head/hand poses, velocities, tracking loss/reconnection, recentering, height/pelvis calibration and weapon attachment while moving |
| Presentation | Both eyes, original fades, each mirror mode, held/world GUI panels, pointer alignment/clicks and startup/scene transitions |
| Keyboard and overlays | Chat, signs/portals, UTF-8 input, caret/selection, submit/cancel, focus return and the configured overlay GUI path |
| Full-body tracking | Role-based and explicit-index trackers, disconnected assignments, tracker table, calibration and reconnection |
| Performance and compatibility | Frame timing/allocation comparison in matched scenes, repeated UI/equipment cycles, multiplayer and any external code using the changed helper types |

Keep observed bug fixes separate from this refactor. Stage 1 is ready for merging
only after the agreed SteamVR comparison passes and maintainers accept the
contract surface.

## Stage 2

Implement OpenXR on top of the accepted contracts in separate changes: runtime
lifecycle/rendering, rig/tracking, input/haptics, then remaining services. Preserve
SteamVR as the existing default unless a later change explicitly changes policy.
Optional gameplay features remain independent of either backend implementation.
