using System;

namespace ValheimVRMod.Utilities
{
    /// <summary>
    /// Marks a Harmony patch class that must be installed even when the VR assemblies are absent from
    /// valheim_Data/Managed, or a component whose Unity messages run in a flat screen session.
    ///
    /// Everything reachable from such a class must be free of VR types: Mono compiles a whole method body
    /// on the first call and resolves the types it mentions, including the ones in branches that are never
    /// taken, so a runtime check does not protect the method that contains the reference. Cross into VR
    /// code only through a <see cref="VrOnlyAttribute"/> member.
    ///
    /// The set of patch classes carrying this attribute must match HarmonyPatcher.FlatScreenSafePatches.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class FlatScreenSafeAttribute : Attribute { }

    /// <summary>
    /// Marks code that is never reached in a flat screen session. On a method this is a call graph sink:
    /// the caller is responsible for guarding it at runtime, and its body may use VR types freely because
    /// it is never compiled when those assemblies are missing.
    ///
    /// Such a member must be MethodImplOptions.NoInlining, must live on a type with no VR typed fields,
    /// and must have a signature free of VR types, because it is the caller's JIT that resolves the
    /// callee's parameter and return types.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property)]
    public sealed class VrOnlyAttribute : Attribute { }
}
