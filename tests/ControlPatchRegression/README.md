# Control Patch Regression

Requires .NET 8 SDK. From the repository root:

```sh
dotnet run --project tests/ControlPatchRegression -- ValheimVRMod/Patches/ControlPatches.cs
```

The test uses Roslyn from the SDK to compile the actual LateUpdate patch in isolation, with minimal game/config stubs and HarmonyX 2.16.1 (compatible with the test's .NET 8 runtime). It verifies replacement calls retain branch labels and exception blocks, then emits and executes a branch-target example. No proprietary game code or binaries are included.

This checks the transpiler, not complete VR gameplay compatibility.

The observed failure on Valheim 1.0.7 (Windows Steam build 25185596) was `Label #8 is not marked` while Harmony patched `PlayerController.LateUpdate`. Inspection of that method found a conditional branch at IL offset 0x0069 targeting the `ZInput.IsGamepadActive` call at 0x00aa. Replacing the call with a fresh instruction drops that destination label. The copy constructor retains metadata while allowing the method operand to change.

The regression runner fails with `Branch label was dropped` on upstream commit `0dc9e015b80fe1cfef70f9f7cbe2567c34db321d` and passes after this change. Actual headset rendering and the full mod's other patches still require runtime validation.
