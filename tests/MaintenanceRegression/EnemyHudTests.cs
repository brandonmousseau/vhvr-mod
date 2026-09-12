using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class EnemyHudTests
{
    public static void Run(string repo)
    {
        var tree=CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(repo,"ValheimVRMod/Patches/UIPatches.cs")));
        var method=tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.Text=="MaybeAddSetActiveInstructions");
        var source="""
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
public class Gui { public void SetActive(bool active){} }
public class Entry { public Gui gui; }
public class EnemyHud_UpdateHuds_Patch {
 private static bool patchedSetActiveTrue,patchedSetActiveFalse;
 private static FieldInfo guiField=typeof(Entry).GetField("gui");
 private static MethodInfo setActiveMethod=typeof(Gui).GetMethod("SetActive");
 private static void LoadCharacterField(ref List<CodeInstruction> code){code.Add(new CodeInstruction(OpCodes.Ldnull));}
 public static bool UpdateActive(bool active,object character)=>active;
"""+method.ToFullString()+"}";
        var refs=((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!.Split(Path.PathSeparator).Append(typeof(CodeInstruction).Assembly.Location).Distinct().Select(p=>MetadataReference.CreateFromFile(p));
        var compilation=CSharpCompilation.Create("HudTests",new[]{CSharpSyntaxTree.ParseText(source)},refs,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream=new MemoryStream();var result=compilation.Emit(stream);
        if(!result.Success)throw new Exception(string.Join("\n",result.Diagnostics));
        var assembly=Assembly.Load(stream.ToArray());var type=assembly.GetType("EnemyHud_UpdateHuds_Patch")!;
        var original=new List<CodeInstruction> {new(OpCodes.Ldfld,assembly.GetType("Entry")!.GetField("gui")),new(OpCodes.Ldc_I4_1),new(OpCodes.Callvirt,assembly.GetType("Gui")!.GetMethod("SetActive"))};
        List<CodeInstruction> Apply(){
            var output=new List<CodeInstruction>();
            type.GetMethod("MaybeAddSetActiveInstructions",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object[]{original,output,1});
            return output;
        }
        var first=Apply();var second=Apply();
        if(!first.Any(i=>i.opcode==OpCodes.Call)||second.Count!=2||second[0].opcode!=OpCodes.Pop||second[1].opcode!=OpCodes.Ldc_I4_0)
            throw new Exception("The second enemy HUD visibility branch is unreachable or no longer hides the original HUD");
        Console.WriteLine("PASS: first enemy HUD activation is mirrored and subsequent vanilla activation is suppressed");
    }
}
