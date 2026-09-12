using System;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace ValheimVRMod.Utilities
{
    // Helpers for rewriting IL without losing the metadata attached to the instructions being replaced.
    //
    // Creating a fresh CodeInstruction (e. g. via CodeInstruction.Call()) and putting it where an existing
    // instruction used to be silently drops that instruction's branch labels and exception block markers.
    // If anything branched to the replaced instruction, Harmony then fails to emit the method with
    // "ArgumentException: Label #N is not marked", which aborts patching and takes the whole mod down with it.
    // Valheim 1.0 recompiled the game assemblies, so calls we rewrite are now branch targets in methods where
    // they previously were not, which is why this has to be handled everywhere rather than case by case.
    public static class TranspilerUtils
    {
        // Copies the labels and exception blocks of source onto target and returns target, so that a replacement
        // instruction stays a valid branch target and stays inside the same try/catch/finally region.
        // Use this when the replacement instruction is built by hand.
        public static CodeInstruction KeepMetadataOf(this CodeInstruction target, CodeInstruction source)
        {
            target.labels.AddRange(source.labels);
            target.blocks.AddRange(source.blocks);
            return target;
        }

        // Drop-in replacement for CodeInstruction.Call() for the case where the emitted instruction takes the
        // place of an existing one: emits the same "call <replacement>" that CodeInstruction.Call() would, but
        // keeps the labels and exception blocks of the instruction being replaced.
        public static CodeInstruction ReplaceCallWith(this CodeInstruction instruction, MethodInfo replacement)
        {
            if (replacement == null)
            {
                throw new ArgumentNullException(nameof(replacement));
            }

            // OpCodes.Call unconditionally, matching CodeInstruction.Call(), so that swapping a call out never
            // changes how it is dispatched. Only the labels and blocks are carried over from the original.
            return new CodeInstruction(OpCodes.Call, replacement).KeepMetadataOf(instruction);
        }

        // Convenience overload resolving the replacement by declaring type and name.
        public static CodeInstruction ReplaceCallWith(this CodeInstruction instruction, Type type, string name, Type[] parameters = null)
        {
            var replacement = AccessTools.Method(type, name, parameters);
            if (replacement == null)
            {
                throw new MissingMethodException(type.FullName, name);
            }

            return instruction.ReplaceCallWith(replacement);
        }

        // Moves the labels and exception blocks off instruction and onto the first instruction of a replacement
        // sequence, for the cases where one instruction is expanded into several. The original instruction is
        // left without metadata so that it can be discarded without duplicating labels.
        public static void MoveMetadataTo(this CodeInstruction instruction, CodeInstruction target)
        {
            target.labels.AddRange(instruction.labels);
            target.blocks.AddRange(instruction.blocks);
            instruction.labels.Clear();
            instruction.blocks.Clear();
        }
    }
}
