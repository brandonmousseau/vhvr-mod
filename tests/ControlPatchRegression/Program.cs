using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Compile the actual patch in isolation: no Unity runtime or game assets are distributed.
var sourcePath = args[0];
var parsed = CSharpSyntaxTree.ParseText(File.ReadAllText(sourcePath));
var patch = parsed.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
    .Single(c => c.Identifier.Text == "PlayerController_LateUpdate_Patch");
var source = """
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
public class PlayerController { public void LateUpdate() {} }
public static class ZInput { public static bool IsGamepadActive() => false; }
public static class VHVRConfig {
    public static bool NonVrPlayer() => false;
    public static bool UseVrControls() => true;
}
""" + patch.ToFullString();
var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
    .Split(Path.PathSeparator).Append(typeof(CodeInstruction).Assembly.Location)
    .Distinct().Select(p => MetadataReference.CreateFromFile(p));
var compilation = CSharpCompilation.Create("PatchUnderTest", new[] { CSharpSyntaxTree.ParseText(source) }, references,
    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
using var stream = new MemoryStream();
var result = compilation.Emit(stream);
if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics));
var assembly = Assembly.Load(stream.ToArray());
var patchType = assembly.GetType("PlayerController_LateUpdate_Patch")!;
var transpiler = patchType.GetMethod("Transpiler", BindingFlags.Static | BindingFlags.NonPublic)!;
var originalCall = assembly.GetType("ZInput")!.GetMethod("IsGamepadActive")!;
var replacementCall = patchType.GetMethod("IsGamepadActivePatched", BindingFlags.Static | BindingFlags.NonPublic)!;
CodeInstruction[] Apply(params CodeInstruction[] input) =>
    ((IEnumerable<CodeInstruction>)transpiler.Invoke(null, new object[] { input })!).ToArray();
void Check(bool value, string message) { if (!value) throw new Exception(message); }

var dynamicMethod = new DynamicMethod("BranchTargetRegression", typeof(bool), Type.EmptyTypes);
var il = dynamicMethod.GetILGenerator();
var label = il.DefineLabel();
var call = new CodeInstruction(OpCodes.Call, originalCall);
call.labels.Add(label);
var input = new[] { new CodeInstruction(OpCodes.Br_S, label), call, new CodeInstruction(OpCodes.Ret) };
var output = Apply(input);
Check(output.Length == input.Length, "Instruction count changed");
Check(output[1].Calls(replacementCall), "Call was not replaced");
Check(output[1].labels.Contains(label), "Branch label was dropped");
Check(call.Calls(originalCall) && call.labels.Contains(label), "Input instruction was mutated");
foreach (var instruction in output) {
    foreach (var target in instruction.labels) il.MarkLabel(target);
    if (instruction.operand is Label branch) il.Emit(instruction.opcode, branch);
    else if (instruction.operand is MethodInfo method) il.Emit(instruction.opcode, method);
    else il.Emit(instruction.opcode);
}
Check(((Func<bool>)dynamicMethod.CreateDelegate(typeof(Func<bool>)))(), "Replacement call did not execute");

var protectedCall = new CodeInstruction(OpCodes.Call, originalCall);
protectedCall.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
var protectedOutput = Apply(protectedCall)[0];
Check(protectedOutput.blocks.SequenceEqual(protectedCall.blocks), "Exception metadata was dropped");
var untouched = new CodeInstruction(OpCodes.Nop);
Check(ReferenceEquals(Apply(untouched)[0], untouched), "Unrelated instruction replaced");
Check(Apply(new CodeInstruction(OpCodes.Call, originalCall), new CodeInstruction(OpCodes.Call, originalCall))
    .All(i => i.Calls(replacementCall)), "Not all matching calls replaced");
Check(Apply().Length == 0, "Empty input changed");
Console.WriteLine("PASS: branch-target execution, labels, exception blocks, unchanged input, unrelated instructions, multiple calls, empty input");
