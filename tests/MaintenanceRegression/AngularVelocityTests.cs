using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class AngularVelocityTests
{
    public static void Run(string repo)
    {
        var tree=CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(repo,"ValheimVRMod/Utilities/PhysicsEstimator.cs")));
        var method=tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.Text=="EstimateAngularVelocity");
        var source="""
using System;
public struct Vector3 {
 public float x,y,z;
 public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
 public static Vector3 zero=>new Vector3();
 public static Vector3 operator *(Vector3 v,float k)=>new Vector3(v.x*k,v.y*k,v.z*k);
}
public struct Quaternion {
 private System.Numerics.Quaternion value;
 public Quaternion(System.Numerics.Quaternion value){this.value=value;}
 public static Quaternion Inverse(Quaternion q)=>new Quaternion(System.Numerics.Quaternion.Inverse(q.value));
 public static Quaternion operator *(Quaternion a,Quaternion b)=>new Quaternion(a.value*b.value);
 public static Quaternion Rotation(float x,float y,float z,float degrees)=>new Quaternion(System.Numerics.Quaternion.CreateFromAxisAngle(new System.Numerics.Vector3(x,y,z),degrees*MathF.PI/180));
 public void ToAngleAxis(out float angle,out Vector3 axis){
  var n=System.Numerics.Quaternion.Normalize(value);
  angle=2*MathF.Acos(Math.Clamp(n.W,-1,1))*180/MathF.PI;
  var divisor=MathF.Sqrt(Math.Max(0,1-n.W*n.W));
  axis=divisor<0.000001f?new Vector3(1,0,0):new Vector3(n.X/divisor,n.Y/divisor,n.Z/divisor);
 }
}
public static class Mathf { public const float Deg2Rad=MathF.PI/180; public static float Abs(float x)=>Math.Abs(x); }
public class PhysicsTest {
"""+method.ToFullString()+"""
 public static void Run(){
  var identity=Quaternion.Rotation(1,0,0,0);
  var fast=EstimateAngularVelocity(identity,Quaternion.Rotation(1,0,0,90),0.02f);
  Near(fast.x,MathF.PI/2/0.02f);Near(fast.y,0);Near(fast.z,0);
  var previous=Quaternion.Rotation(0,0,1,90);
  var current=Quaternion.Rotation(1,0,0,10)*previous;
  var oriented=EstimateAngularVelocity(previous,current,1);
  Near(oriented.x,10*MathF.PI/180);Near(oriented.y,0);Near(oriented.z,0);
  var shortest=EstimateAngularVelocity(identity,Quaternion.Rotation(0,1,0,350),1);
  Near(shortest.y,-10*MathF.PI/180);
  var stationary=EstimateAngularVelocity(previous,previous,1);
  Near(stationary.x,0);Near(stationary.y,0);Near(stationary.z,0);
  var paused=EstimateAngularVelocity(identity,current,0);Near(paused.x,0);
 }
 private static void Near(float actual,float expected){if(!float.IsFinite(actual)||Math.Abs(actual-expected)>0.001f)throw new Exception($"Angular velocity: {actual} != {expected}");}
}
""";
        var refs=((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!.Split(Path.PathSeparator).Select(p=>MetadataReference.CreateFromFile(p));
        var compilation=CSharpCompilation.Create("PhysicsTests",new[]{CSharpSyntaxTree.ParseText(source)},refs,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream=new MemoryStream();var result=compilation.Emit(stream);
        if(!result.Success)throw new Exception(string.Join("\n",result.Diagnostics));
        Assembly.Load(stream.ToArray()).GetType("PhysicsTest")!.GetMethod("Run")!.Invoke(null,null);
        Console.WriteLine("PASS: angular velocity magnitude, reference frame, shortest rotation and zero-time/identity cases");
    }
}
