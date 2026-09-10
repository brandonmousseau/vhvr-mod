# Valheim 1.0 maintenance pass

Date: 2026-09-09. Starting revision: `c850046f` (upstream through `2e3b87bb`).
Branch: `cleanup/valheim-1.0-maintenance`.

This is a repository-wide static sweep followed by detailed review and fixes in
startup, asset loading, Harmony rewrites, cameras/HUD, weapon preparation,
physics, multiplayer pose handling, configuration, haptics and build tooling.
The starting runtime has 111 C# files, approximately 37,000 lines. This does not
claim line-by-line certification of every game interaction. Vendored SteamVR,
FinalIK and other third-party libraries were not broadly rewritten; the duplicate
asset lookup helper in the Unity asset project received the same correctness fix.

## Fixed in this pass

| Area | Problem and resulting behavior | Evidence |
| --- | --- | --- |
| Asset lookup | A missing name logged an error and then threw from the dictionary. The type-assignability check was reversed. Missing/wrong types now return null; requesting a valid base type works. | Regression; old code fails it. |
| XR startup | A missing XR bundle was dereferenced; asset failures and subsystem-start failures could leave VR patches enabled without a working rig. Failures now exit before applying asset-dependent patches or switch to companion behavior as appropriate. | Full build and source review; runtime failure-path check pending. |
| Bundle ownership | Loaded prefab/XR bundle containers were retained after loading. Containers now unload while the loaded assets stay alive. | Source review; Unity lifetime check pending. |
| World-space HUD | The helper camera inherited an offset because parenting retained its original world transform. It now starts at the VR camera's local origin. | Source review; headset check pending. |
| Cinematics | Two Awake patches disabled the same startup intro. One combined patch handles startup/new-world intros and the cinematic camera mask. Conflicting camera-setup commentary was removed. | Full build and source review. |
| TAA rewrite | Rewriting a field load assumed its receiver came from an immediately preceding ldarg.0. A static accessor now consumes the actual receiver already on the stack, preserving labels and exception metadata. | Executed emitted branch with distinct camera histories; old code fails. |
| Bow animation rewrite | The patch changed the previous instruction into a constant, potentially corrupting an argument load or expression. A replacement call preserves the original stack contract. | Argument/metadata and flat-mode regressions; old code fails. |
| Enemy HUD | The condition for handling the first activation made the subsequent-activation branch unreachable. Later matching vanilla activations can now be suppressed as intended. | Regression; old code fails. Visual result pending. |
| Bow preparation | A worker thread changed Unity transforms; partial initialization also made destruction dereference missing objects. Transform work runs on the Unity thread, worker failures are reported there, and the owned hierarchy can be destroyed at any stage. | Full build and source review; rapid equip/unequip test pending. |
| Bow geometry | A near-zero hand distance or missing grip sample could produce nonfinite geometry. The distance/asin domain and missing-sample fallback are handled. | Full build and source review. |
| Angular velocity | Extrapolating a quaternion before measuring its angle wrapped fast movement, and the rotation delta used the wrong reference frame. The shortest measured angle is now divided by elapsed time. | Numerical tests for fast motion, orientation, zero time and identity. |
| Melee cooldown | An accepted early attack did not restart the minimum interval, allowing subsequent hits every update. Accepted hits now restart the cooldown. | Regression; old code fails. Gameplay balance still needs testing. |
| Object cleanup | Several components used Destroy or OnDestory instead of Unity's OnDestroy. Collision helpers, physics debug lines, outline materials, underwater helpers/materials and owned pose targets now clean up. Borrowed headset/controller objects are not destroyed. | Full build and ownership review; long-session profiling pending. |
| VR GUI | Re-enabling created another GUI camera/texture, overlay textures were not released, and laser-pointer handlers were not detached. The camera is reused and owned resources/handlers are released. | Full build and source review; enable/disable check pending. |
| Haptics | Repeating effects shared unsynchronized dictionaries across foreground threads; starts/stops raced and delayed stops could cancel a restarted effect. One main-thread scheduler owns each repeating effect and its delayed stop. | Deterministic scheduler tests; physical suit test pending. |
| Pose packets | Truncated optional sections could throw while decoding; extra/missing finger bones changed the wire layout. Accepted existing packet lengths are checked and finger output is fixed at 20 rotations per hand. Bounds checks protect playback, and packet-remaining checks no longer copy the whole buffer. | Every truncated length below the full format checked; multiplayer test pending. |
| HUD placement settings | Reentrant requests could replace another session's callbacks; destroyed targets could leave automatic config saving disabled. Sessions reject reentry and restore the prior saving policy on destruction. Placement updates run once per frame. | Full build and source review; calibration test pending. |
| Unity font | HUD placement requested the removed Arial built-in font on Unity 6. Modern Unity uses LegacyRuntime.ttf; the old name remains for older runtimes. | [Unity's own Text implementation](https://github.com/Unity-Technologies/uGUI/blob/main/com.unity.ugui/Runtime/UGUI/UI/Core/Text.cs). |
| Sliders | Modulo arithmetic skipped a zero-based slider's maximum; a stopped coroutine remained recorded after closing. Endpoints wrap explicitly and disable clears repeat state. | Regression; old code fails. |
| Building configuration | Bad, zero, nonfinite or empty snap-angle values could throw or enter invalid calculations. Validated values are cached, with defaults and one warning per invalid edit. | Valid/invalid/locale regression tests. |
| Diagnostics | Material failures discarded the exception, and a debug indicator continued after detecting a missing owner. Errors now retain context and the missing-owner path returns. | Full build and source review. |

## Build and verification

- Full Release build against freshly generated installed Valheim 1.0.7 references:
  zero errors, eight warnings (older Unity API use plus a disabled debug path).
- Standalone SteamVR dependency build: zero errors.
- Ten regression groups pass. Six tests were independently run against the old
  source and reproduced failures: asset lookup, melee cooldown, slider endpoints,
  bow instruction rewriting, TAA receiver handling and enemy-HUD visibility.
- A checked-in preparation script creates stripped build-only references with
  BepInEx AssemblyPublicizer 0.4.3. `UseInstalledGameAssemblies=true` is opt-in;
  upstream's normal dependency choices remain the default. See `build/README.md`.
- GitHub Actions runs the portable regression suite without game files. Its
  result should be checked on the pushed revision.

The cleanup build has not been installed into the live game and has not had a
headset test. The earlier successful headset test applies to the earlier
compatibility build, not to this cleanup.

## Remaining acceptance work

1. Check the main menu, new-world transition, enemy/boss HUD, crosshair and repair
   indicator in both eyes. Exercise TAA/AO settings and underwater transitions.
2. Reopen split-stack sliders and HUD placement repeatedly; leave the world during
   placement and verify later settings still save.
3. Rapidly equip/unequip bows; check fast swings, early melee hits, throwing,
   building and ordinary keyboard/mouse play. Cooldown and angular-velocity fixes
   intentionally affect the numbers feeding gameplay.
4. Join with VR and companion clients, test handedness/body tracking, and repeat
   player joins/leaves. Packet extensions retain the existing format rather than
   introducing a protocol version in this pass.
5. With a bHaptics suit, test heartbeat/portal effects, cancellation and shutdown.
   The scheduler is tested, but physical timing has not been measured.
6. Profile a longer session for allocations and surviving objects. Camera/HUD
   discovery still uses older scene-search APIs in several paths, and the enemy
   HUD transpiler still depends on game-local variable layouts. Those remain
   maintenance risks when Valheim updates.

The next cleanup should follow those results. Broad changes to the vendored IK,
weapon anatomy tables or gesture tuning would need a separate set of hardware
and gameplay checks.
