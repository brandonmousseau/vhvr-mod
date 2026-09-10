using ValheimVRMod.Utilities;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
var repo = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var parsed = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(repo, "ValheimVRMod/Patches/ControlPatches.cs")));
var patch = parsed.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.Text == "PlayerController_LateUpdate_Patch");
var source = """
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
public class PlayerController { public void LateUpdate() {} }
public static class ZInput {
 public static bool Mouse = true, Gamepad = false;
 public static bool IsMouseActive() => Mouse;
 public static bool IsGamepadActive() => Gamepad;
}
public static class VHVRConfig {
 public static bool Flat = false, Controls = true;
 public static bool NonVrPlayer() => Flat;
 public static bool UseVrControls() => Controls;
}
public static class LogUtils { public static int Errors; public static void LogError(string text) { Errors++; } }
""" + patch.ToFullString();
var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!.Split(Path.PathSeparator).Append(typeof(CodeInstruction).Assembly.Location).Distinct().Select(p => MetadataReference.CreateFromFile(p));
var compilation = CSharpCompilation.Create("MergedPatch", new[] { CSharpSyntaxTree.ParseText(source) }, refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
using var stream = new MemoryStream();
var result = compilation.Emit(stream);
if (!result.Success) throw new Exception(string.Join("\n",result.Diagnostics));
var a = Assembly.Load(stream.ToArray());
var t = a.GetType("PlayerController_LateUpdate_Patch")!;
var transpiler = t.GetMethod("Transpiler",BindingFlags.Static|BindingFlags.NonPublic)!;
var mouse = a.GetType("ZInput")!.GetMethod("IsMouseActive")!;
var pad = a.GetType("ZInput")!.GetMethod("IsGamepadActive")!;
var pm = t.GetMethod("IsMouseActivePatched",BindingFlags.Static|BindingFlags.NonPublic)!;
var pp = t.GetMethod("IsGamepadActivePatched",BindingFlags.Static|BindingFlags.NonPublic)!;
CodeInstruction[] Apply(params CodeInstruction[] input) => ((IEnumerable<CodeInstruction>)transpiler.Invoke(null,new object[]{input})!).ToArray();
void Check(bool b,string message) { if(!b)throw new Exception(message); }
var dm = new DynamicMethod("BranchTarget",typeof(bool),Type.EmptyTypes);
var il = dm.GetILGenerator(); var label = il.DefineLabel();
var call = new CodeInstruction(OpCodes.Call,pad); call.labels.Add(label);
var code = Apply(new(OpCodes.Call,pad),new(OpCodes.Pop),new(OpCodes.Call,mouse),new(OpCodes.Pop),new(OpCodes.Br_S,label),call,new(OpCodes.Ret));
Check(code[0].Calls(pad),"Early delay guard changed");
Check(code[2].Calls(pm)&&code[5].Calls(pp),"Look gates not rewritten");
Check(code[5].labels.Contains(label),"Branch label lost");
foreach(var i in code) { foreach(var l in i.labels)il.MarkLabel(l); if(i.operand is Label l2)il.Emit(i.opcode,l2); else if(i.operand is MethodInfo m)il.Emit(i.opcode,m); else il.Emit(i.opcode); }
Check(((Func<bool>)dm.CreateDelegate(typeof(Func<bool>)))(),"VR branch failed");
var protectedCall = new CodeInstruction(OpCodes.Call,pad); var block = new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock); protectedCall.blocks.Add(block);
Check(Apply(new(OpCodes.Call,mouse),protectedCall)[1].blocks.Contains(block),"Exception metadata lost");
var config=a.GetType("VHVRConfig")!;
foreach(var flat in new[]{false,true})foreach(var controls in new[]{false,true})foreach(var m in new[]{false,true})foreach(var g in new[]{false,true}) {
 config.GetField("Flat")!.SetValue(null,flat);config.GetField("Controls")!.SetValue(null,controls);
 a.GetType("ZInput")!.GetField("Mouse")!.SetValue(null,m);a.GetType("ZInput")!.GetField("Gamepad")!.SetValue(null,g);
 var vr=!flat&&controls;
 Check((bool)pm.Invoke(null,null)! ==(!vr&&m),"Mouse routing incorrect");
 Check((bool)pp.Invoke(null,null)! ==(vr||g),"Gamepad routing incorrect");
}
var logs=a.GetType("LogUtils")!.GetField("Errors")!;var before=(int)logs.GetValue(null)!;
Check(Apply().Length==0,"Empty input changed");
Check((int)logs.GetValue(null)! ==before+1,"Missing-gate warning absent");
Console.WriteLine("PASS: early guard, VR/flat input gates, emitted branch execution, labels, exception metadata, missing-gate diagnostic");

// Duplicate starts must update one loop, and old delayed stops must not cancel a restarted effect.
var played = new List<string>();
var stopped = new List<string>();
var scheduler = new HapticScheduler((name, intensity, duration) => played.Add(name), stopped.Add);
scheduler.Start("heartbeat", 1, 1, 1, 0, 0);
scheduler.Start("heartbeat", 0.5f, 1, 1, 0, 0);
scheduler.Tick(0);
Check(played.Count == 1, "Duplicate haptics loop");
scheduler.Tick(0.5);
Check(played.Count == 1, "Haptics interval ignored");
scheduler.StopAfter("heartbeat", 0.5, 0.5);
scheduler.Start("heartbeat", 1, 1, 1, 0, 0.75);
scheduler.Tick(1);
Check(played.Count == 2 && stopped.Count == 0, "Stale delayed stop cancelled restarted effect");
scheduler.Start("portal", 1, 1, 1, 5, 1);
scheduler.Stop("portal", new[]{"exit"});
scheduler.Tick(10);
Check(!played.Contains("portal") && played.Count(x => x == "exit") == 1, "Cancelled delayed effect played");
scheduler.StopAll(new[]{"heartbeat"});
scheduler.Tick(11);
Check(played.Last() == "heartbeat", "StopAll ignored exception");
scheduler.StopAfter("heartbeat", 0, 11);
scheduler.Tick(11);
var finalCount = played.Count;
scheduler.Tick(1000);
Check(played.Count == finalCount, "Stopped effect played again");
Console.WriteLine("PASS: haptics start/update, delays, cancellation, callbacks, exceptions, no backlog");

foreach (var length in new[]{40,804,832,888,900}) Check(VRPosePacket.HasValidLength(length), "Valid pose layout rejected");
for(var length=0;length<888;length++)
 Check(VRPosePacket.HasValidLength(length) == (length==40||length==804||length==832), "Truncated pose layout accepted");
Console.WriteLine("PASS: legacy/full/extended pose layouts and every truncated length");

Check(BuildAngleSnapParser.TryParse("22.5, 15, 0.5", out var angles) && angles.SequenceEqual(new[]{22.5f,15f,0.5f}), "Valid snap angles rejected");
foreach(var invalid in new[]{"", " ", "0", "-1", "NaN", "Infinity", "1e40", "361", "15,", "bad"})
 Check(!BuildAngleSnapParser.TryParse(invalid,out _), "Invalid snap angle accepted: " + invalid);
var culture = System.Globalization.CultureInfo.CurrentCulture;
System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("de-DE");
Check(BuildAngleSnapParser.TryParse("22.5,15",out _), "Angle parsing depends on locale");
System.Globalization.CultureInfo.CurrentCulture = culture;
Console.WriteLine("PASS: snap angles reject invalid/nonfinite/zero values and ignore OS locale");

UnityEngine.AssetBundle.Assets = new UnityEngine.Object[]{new UnityEngine.Material { name="material" }};
Check(VRAssetManager.Initialize(), "Test assets failed to load");
Check(VRAssetManager.GetAsset<UnityEngine.Object>("material") is UnityEngine.Material, "Asset base type rejected");
Check(VRAssetManager.GetAsset<UnityEngine.Material>("missing") == null, "Missing asset threw or returned a value");
Check(VRAssetManager.GetAsset<UnityEngine.Texture2D>("material") == null, "Unrelated asset type accepted");
Console.WriteLine("PASS: asset lookup missing names, valid base types and type mismatches");

var cooldown = new ValheimVRMod.Scripts.MeshCooldown();
var fixedUpdate = typeof(ValheimVRMod.Scripts.MeshCooldown).GetMethod("FixedUpdate",BindingFlags.Instance|BindingFlags.NonPublic)!;
Check(cooldown.tryTrigger(1), "Initial attack rejected");
UnityEngine.Time.fixedDeltaTime = 0.3f;
fixedUpdate.Invoke(cooldown,null);
Check(cooldown.tryTrigger(1,0.25f), "Early attack rejected after minimum interval");
Check(!cooldown.tryTrigger(1,0.25f), "Early attack failed to restart cooldown");
Console.WriteLine("PASS: accepted early melee hits restart the minimum attack interval");

var selector = new ValheimVRMod.VRCore.UI.SliderSelector();
var slider = new UnityEngine.UI.Slider {minValue=0,maxValue=10,value=9};
var selectorType=selector.GetType();
selectorType.GetField("_splitSlider",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(selector,slider);
void MoveSlider(float direction) {
 var move=(System.Collections.IEnumerator)selectorType.GetMethod("DoSliderMovement",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(selector,new object[]{0f,direction})!;
 while(move.MoveNext()) {}
}
MoveSlider(1); Check(slider.value==10,"Slider cannot reach maximum");
MoveSlider(1); Check(slider.value==0,"Slider did not wrap forward");
MoveSlider(-1); Check(slider.value==10,"Slider did not wrap backward");
selectorType.GetField("_doSliderMovementDelayed",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(selector,new[]{1}.GetEnumerator());
selectorType.GetMethod("OnDisable",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(selector,null);
Check(selectorType.GetField("_doSliderMovementDelayed",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(selector)==null,"Closed slider retains cancelled coroutine");
Console.WriteLine("PASS: slider endpoints/wrapping and close/reopen reset");

PatchRewriteTests.Run(repo);

AngularVelocityTests.Run(repo);

EnemyHudTests.Run(repo);
