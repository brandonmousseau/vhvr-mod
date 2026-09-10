using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class PatchRewriteTests
{
    public static void Run(string repo)
    {
        string utilities = File.ReadAllText(Path.Combine(repo,"ValheimVRMod/Utilities/TranspilerUtils.cs"));
        var bowTree = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(repo,"ValheimVRMod/Patches/BowAndFishingPatches.cs")));
        var bow = bowTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c=>c.Identifier.Text=="PatchPlayerAttackInput");
        var taaTree = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(repo,"ValheimVRMod/Patches/PostProcessingPatches.cs")));
        var taa = taaTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c=>c.Identifier.Text=="PostProcessingPatches");
        var taaMembers = taa.Members.Where(m=>m is FieldDeclarationSyntax || m is MethodDeclarationSyntax method && (method.Identifier.Text=="TranspileTaaComponentAway" || method.Identifier.Text=="GetVrTaaComponent"));
        // The extracted methods retain their implementation; patch attributes require unrelated runtime targets.
        var taaSource = string.Join("\n",taaMembers.Select(m=>m is MethodDeclarationSyntax method ? method.WithAttributeLists(default).ToFullString() : m.ToFullString()));
        string source = """
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using ValheimVRMod.Utilities;
public static class VHVRConfig { public static bool Vr=true; public static bool UseVrControls()=>Vr; public static bool NonVrPlayer()=>!Vr; }
public class Player {}
public static class Shader { public static int PropertyToID(string name)=>0; }
public class ZSyncAnimation { public bool Value=true; public void SetBool(string name,bool value) { Value=value; } }
public static class Debug { public static void Log(string text) {} }
public struct Vector2 {}
public struct Matrix4x4 {}
public class RenderTexture {}
public class TaaComponent {
 public Vector2 jitterVector=>new Vector2();
 public void SetProjectionMatrix(Func<Vector2,Matrix4x4> p){}
 public void Render(RenderTexture a,RenderTexture b){}
 public void ResetHistory(){}
}
public class PostProcessingBehaviour { public TaaComponent m_Taa; }
public class VRTaaComponent {
 public static ConditionalWeakTable<PostProcessingBehaviour,VRTaaComponent> PostProcessingExtension=new ConditionalWeakTable<PostProcessingBehaviour,VRTaaComponent>();
 public Vector2 jitterVector=>new Vector2();
 public void ConfigureStereoMonoProjectionMatrices(Func<Vector2,Matrix4x4> p){}
 public void Render(RenderTexture a,RenderTexture b){}
 public void ResetHistory(){}
}
""" + bow.ToFullString() + "\npublic class PostProcessingPatches {"+taaSource+"}";
        var refs=((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!.Split(Path.PathSeparator).Append(typeof(CodeInstruction).Assembly.Location).Distinct().Select(p=>MetadataReference.CreateFromFile(p));
        var compilation=CSharpCompilation.Create("RewriteTests",new[]{CSharpSyntaxTree.ParseText(source),CSharpSyntaxTree.ParseText(utilities)},refs,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream=new MemoryStream();var emitted=compilation.Emit(stream);
        Check(emitted.Success,string.Join("\n",emitted.Diagnostics));
        var assembly=Assembly.Load(stream.ToArray());
        var animationType=assembly.GetType("ZSyncAnimation")!;
        var boolMethod=animationType.GetMethod("SetBool")!;
        var bowType=assembly.GetType("PatchPlayerAttackInput")!;
        var valueProducer=new CodeInstruction(OpCodes.Ldarg_2);
        var call=new CodeInstruction(OpCodes.Callvirt,boolMethod);
        var label=new DynamicMethod("Labels",typeof(void),Type.EmptyTypes).GetILGenerator().DefineLabel();
        call.labels.Add(label);call.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
        var output=Apply(bowType,"Transpiler",valueProducer,call);
        Check(valueProducer.opcode==OpCodes.Ldarg_2,"Bow patch corrupted argument producer");
        Check(output[1].labels.Contains(label)&&output[1].blocks.Count==1,"Bow patch lost metadata");
        var animation=Activator.CreateInstance(animationType);
        ((MethodInfo)output[1].operand).Invoke(null,new[]{animation,"draw",(object)true});
        Check(!(bool)animationType.GetField("Value")!.GetValue(animation)!,"Bow patch did not suppress animation");
        Check(Apply(bowType,"Transpiler",call).Length==1,"Bow call at beginning rejected");
        assembly.GetType("VHVRConfig")!.GetField("Vr")!.SetValue(null,false);
        Check(ReferenceEquals(Apply(bowType,"Transpiler",call)[0],call),"Flat bow behavior changed");
        assembly.GetType("VHVRConfig")!.GetField("Vr")!.SetValue(null,true);
        var behaviour=assembly.GetType("PostProcessingBehaviour")!;
        var field=behaviour.GetField("m_Taa")!;
        var taaType=assembly.GetType("PostProcessingPatches")!;
        // Branch to a field load whose receiver is already on the stack, not preceded by ldarg.0.
        var dm=new DynamicMethod("TaaFieldReceiver",typeof(object),new[]{typeof(object)});
        var il=dm.GetILGenerator();var fieldLabel=il.DefineLabel();
        var load=new CodeInstruction(OpCodes.Ldfld,field);load.labels.Add(fieldLabel);
        var rewritten=Apply(taaType,"TranspileTaaComponentAway",new(OpCodes.Ldarg_0),new(OpCodes.Castclass,behaviour),new(OpCodes.Br_S,fieldLabel),load,new(OpCodes.Ret));
        foreach(var instruction in rewritten) {
            foreach(var target in instruction.labels)il.MarkLabel(target);
            if(instruction.operand is Label l)il.Emit(instruction.opcode,l);
            else if(instruction.operand is MethodInfo m)il.Emit(instruction.opcode,m);
            else if(instruction.operand is Type t)il.Emit(instruction.opcode,t);
            else il.Emit(instruction.opcode);
        }
        var run=(Func<object,object>)dm.CreateDelegate(typeof(Func<object,object>));
        var first=Activator.CreateInstance(behaviour)!;var second=Activator.CreateInstance(behaviour)!;
        Check(ReferenceEquals(run(first),run(first)),"TAA history not retained per camera");
        Check(!ReferenceEquals(run(first),run(second)),"TAA history shared across cameras");
        Check(load.opcode==OpCodes.Ldfld&&load.labels.Contains(fieldLabel),"TAA input mutated");
        Check(Apply(taaType,"TranspileTaaComponentAway",load).Length==1,"TAA field at beginning rejected");
        Console.WriteLine("PASS: bow argument/metadata preservation and emitted TAA branches with independent camera history");
    }

    private static CodeInstruction[] Apply(Type type,string method,params CodeInstruction[] instructions) =>
        ((IEnumerable<CodeInstruction>)type.GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{instructions})!).ToArray();
    private static void Check(bool ok,string message) { if(!ok)throw new Exception(message); }
}
