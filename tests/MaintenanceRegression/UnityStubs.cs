// Minimal doubles for behavior tests. These do not simulate rendering or a Unity lifecycle.
using System;
using System.Collections;
namespace UnityEngine
{
    public class Object
    {
        public string name = "";
        public static implicit operator bool(Object value) => value != null;
    }
    public class GameObject : Object
    {
        public T GetComponent<T>() where T : new() => new T();
        public T AddComponent<T>() where T : new() => new T();
    }
    public class MonoBehaviour : Object
    {
        public GameObject gameObject = new GameObject();
        public static void Destroy(Object o) {}
        public T GetComponent<T>() where T : new() => gameObject.GetComponent<T>();
        public void StopAllCoroutines() {}
        public void StopCoroutine(IEnumerator e) {}
        public void StartCoroutine(IEnumerator e) {}
    }
    public class Material : Object { }
    public class Texture2D : Object { }
    public static class Application { public static string streamingAssetsPath = "/test"; }
    public class AssetBundle : Object
    {
        public static Object[] Assets = Array.Empty<Object>();
        public static AssetBundle LoadFromFile(string path) => new AssetBundle();
        public Object[] LoadAllAssets() => Assets;
        public void Unload(bool all) {}
    }
    public struct Color
    {
        public float r,g,b,a;
        public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}
        public static Color red => new Color(1,0,0);
        public static Color Lerp(Color x,Color y,float t) => y;
    }
    public static class Time { public static float fixedDeltaTime; }
    public static class Mathf { public static float Max(float x,float y)=>Math.Max(x,y); public static float Abs(float x)=>Math.Abs(x); public static float Sign(float x)=>Math.Sign(x); }
    public class WaitForSeconds { public WaitForSeconds(float seconds) {} }
}
namespace UnityEngine.UI { public class Slider { public float minValue,maxValue,value; } }
namespace Valve.VR { public static class ShaderLoader { public static bool Initialize(string path)=>true; } }
namespace ValheimVRMod.Utilities
{
    public static class LogUtils { public static void LogDebug(string s){} public static void LogError(string s){} public static void LogWarning(string s){} }
}
namespace ValheimVRMod.Scripts
{
    public class Outline : UnityEngine.Object
    {
        public enum Mode { OutlineHidden,OutlineVisible }
        public Mode OutlineMode; public float OutlineWidth; public UnityEngine.Color OutlineColor; public bool enabled;
    }
}
namespace ValheimVRMod.VRCore.UI
{
    public class VRControls { public static bool mainControlsActive; public static VRControls instance=new VRControls(); public float GetJoyRightStickX()=>0; }
}
