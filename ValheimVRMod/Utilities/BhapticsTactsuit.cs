using System;
using System.Timers;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Bhaptics.Tact;
using UnityEngine;

using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.Utilities
{

    public class TactsuitVR : MonoBehaviour
    {
        public static bool suitDisabled = true;
        public static  bool systemInitialized = false;
        public static bool threadEnabled = false;
        // Event to start and stop the thread
        public static Dictionary<string, ManualResetEvent> ThreadEffectsEvents = new Dictionary<string, ManualResetEvent>();
        // dictionary of all feedback patterns found in the bHaptics directory
        public static Dictionary<string, FileInfo> FeedbackMap = new Dictionary<string, FileInfo>();

#pragma warning disable CS0618 // remove warning that the C# library is deprecated
        public static HapticPlayer hapticPlayer;
#pragma warning restore CS0618 

        public static RotationOption defaultRotationOption = new RotationOption(0.0f, 0.0f);
        private static System.Timers.Timer aTimer;

        public void Awake()
        {

            LogInfo("Initializing suit");
            try
            {
#pragma warning disable CS0618 // remove warning that the C# library is deprecated
                hapticPlayer = new HapticPlayer("Valheim_bhaptics", "Valheim_bhaptics");
#pragma warning restore CS0618
                suitDisabled = false;
            }
            catch { LogInfo("Suit initialization failed!"); }
            RegisterAllTactFiles();
            LogInfo("Starting HeartBeat thread...");
            PlaybackHaptics("HeartBeat");
            SetTimer();
        }


        void RegisterAllTactFiles()
        {
            if (suitDisabled) { return; }
            // Get location of the compiled assembly and search through "bHaptics" directory and contained patterns
            string assemblyFile = Assembly.GetExecutingAssembly().Location;
            string myPath = Path.GetDirectoryName(assemblyFile);
            LogInfo("Assembly path: " + myPath);
            string configPath = myPath + "\\bHaptics";
            DirectoryInfo d = new DirectoryInfo(configPath);
            FileInfo[] Files = d.GetFiles("*.tact", SearchOption.AllDirectories);
            for (int i = 0; i < Files.Length; i++)
            {
                string filename = Files[i].Name;
                string fullName = Files[i].FullName;
                string prefix = Path.GetFileNameWithoutExtension(filename);
                if (filename == "." || filename == "..")
                    continue;
                string tactFileStr = File.ReadAllText(fullName);
                try
                {
                    hapticPlayer.RegisterTactFileStr(prefix, tactFileStr);
                    LogInfo("Pattern registered: " + prefix);
                }
                catch (Exception e) { LogInfo(e.ToString()); }

                FeedbackMap.Add(prefix, Files[i]);
            }
            systemInitialized = true;
        }

        public static void PlaybackHaptics(string key, float intensity = 1.0f, float duration = 1.0f)
        {
            if (suitDisabled) { return; }
            if (FeedbackMap.ContainsKey(key))
            {
                ScaleOption scaleOption = new ScaleOption(intensity, duration);
                hapticPlayer.SubmitRegisteredVestRotation(key, key, defaultRotationOption, scaleOption);
            }
            else
            {
                LogInfo("Feedback not registered: " + key);
            }
        }

        public static void PlayBackHit(string key, float xzAngle, float yShift)
        {
            // two parameters can be given to the pattern to move it on the vest:
            // 1. An angle in degrees [0, 360] to turn the pattern to the left
            // 2. A shift [-0.5, 0.5] in y-direction (up and down) to move it up or down
            if (suitDisabled) { return; }
            ScaleOption scaleOption = new ScaleOption(1f, 1f);
            RotationOption rotationOption = new RotationOption(xzAngle, yShift);
            hapticPlayer.SubmitRegisteredVestRotation(key, key, rotationOption, scaleOption);
        }

        public static void SwordRecoil(bool isRightHand, float intensity = 1.0f)
        {
            // Melee feedback pattern
            if (suitDisabled) { return; }
            float duration = 1.0f;
            var scaleOption = new ScaleOption(intensity, duration);
            var rotationFront = new RotationOption(0f, 0f);
            string postfix = "_L";
            if (isRightHand) { postfix = "_R"; }
            string keyArm = "Sword" + postfix;
            string keyVest = "SwordVest" + postfix;
            hapticPlayer.SubmitRegisteredVestRotation(keyArm, keyArm, rotationFront, scaleOption);
            hapticPlayer.SubmitRegisteredVestRotation(keyVest, keyVest, rotationFront, scaleOption);
        }

        public static void StartThreadHaptic(string EffectName, float intensity = 1.0f, int sleep = 1000)
        {
            try
            {
                //checking if event with name exists
                if (ThreadEffectsEvents.ContainsKey(EffectName))
                {
                    LogInfo("THREAD CONTAINED " + EffectName);
                    /*if (ThreadEffectsEvents[EffectName].WaitOne())
                    {
                        LogInfo("THREAD SET OK" + EffectName);
                        return;
                    }*/
                    LogInfo("THREAD SETTING " + EffectName);
                    ThreadEffectsEvents[EffectName].Set();
                }
                else
                {
                    LogInfo("MANUAL EVENT CREATED " + EffectName);
                    ManualResetEvent ThreadEvent = new ManualResetEvent(false);
                    LogInfo("MANUAL EVENT ADD " + EffectName);
                    ThreadEffectsEvents.Add(EffectName, ThreadEvent);
                    LogInfo("MANUAL EVENT SET " + EffectName);
                    ThreadEvent.Set();
                }
                if (threadEnabled)
                {
                    LogInfo("THREAD ENABLED " + EffectName);
                    Thread EffectThread = new Thread(() => ThreadHapticFunc(EffectName, intensity, sleep));
                    EffectThread.Start();
                    threadEnabled = false;
                }
                else
                {
                    LogInfo("NOT ENABLED");
                }
            } catch (Exception e)
            {
                LogInfo(e.ToString());
            }
        }

        public static void StopThreadHaptic(string name)
        {
            if (ThreadEffectsEvents.ContainsKey(name))
            {
                ThreadEffectsEvents[name].Reset();
            }
        }

        public static void StopHapticFeedback(string effect)
        {
            hapticPlayer.TurnOff(effect);
        }

        public static void StopAllHapticFeedback()
        {
            StopThreads();
            foreach (string key in FeedbackMap.Keys)
            {
                hapticPlayer.TurnOff(key);
            }
        }

        public static void StopThreads()
        {
            foreach (var entry in ThreadEffectsEvents.Values)
            {
                entry.Reset();
            }
        }
        public static void ThreadHapticFunc(string name, float intensity = 1.0f, int sleep = 1000)
        {
            while (true)
            {
                // Check if reset event is active
                ThreadEffectsEvents[name].WaitOne();
                PlaybackHaptics(name, intensity);
                Thread.Sleep(sleep == 0 ? 1000 : sleep);
            }
        }
        private static void SetTimer()
        {
            // Create a timer with a 200ms interval.
            aTimer = new System.Timers.Timer(200);
            // Hook up the Elapsed event for the timer. 
            aTimer.Elapsed += OnTimedEvent;
            aTimer.AutoReset = true;
            aTimer.Enabled = true;
        }
        private static void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            LogInfo("TIMER EVENT");
            threadEnabled = true;
        }
    }
}
