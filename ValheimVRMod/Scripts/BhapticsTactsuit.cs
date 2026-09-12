using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Bhaptics.Tact;
using UnityEngine;

using static ValheimVRMod.Utilities.LogUtils;
using System.Linq;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts
{

    public class BhapticsTactsuit : MonoBehaviour
    {
        public static bool suitDisabled = true;
        public static bool systemInitialized = false;
        private static readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        private static readonly HapticScheduler scheduler = new HapticScheduler(PlaybackHaptics, StopHapticFeedback);
        private static double nextThrottledStart;
        // dictionary of all feedback patterns found in the bHaptics directory
        public static Dictionary<string, FileInfo> FeedbackMap = new Dictionary<string, FileInfo>();

#pragma warning disable CS0618 // remove warning that the C# library is deprecated
        public static HapticPlayer hapticPlayer;
#pragma warning restore CS0618 

        public static RotationOption defaultRotationOption = new RotationOption(0.0f, 0.0f);

        #region Initializers

        /**
         * If modVrEnabled harmony will use bhaptics patches
         * initializing suitdisabled according to config bhapticsEnabled
         * if disabled, not starting HapticPlayer
         * Every bhaptics patches return if suitdisabled true so it will be ok
         */
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
            catch
            {
                LogInfo("Suit initialization failed!");
                return;
            }
            if (!RegisterAllTactFiles())
            {
                suitDisabled = true;
                return;
            }
            LogInfo("Starting HeartBeat thread...");
            PlaybackHaptics("HeartBeat");

        }

        /**
         * Registers all tact files in bHaptics folder
         */
        bool RegisterAllTactFiles()
        {
            if (suitDisabled) { return false; }
            // Get location of the compiled assembly and search through "bHaptics" directory and contained patterns
            string assemblyFile = Assembly.GetExecutingAssembly().Location;
            string myPath = Path.GetDirectoryName(assemblyFile);
            LogInfo("Assembly path: " + myPath);
            string configPath = myPath + "\\bHaptics";
            DirectoryInfo d = new DirectoryInfo(configPath);
            if (!d.Exists)
            {
                LogError("Haptics pattern directory is missing: " + configPath);
                return false;
            }
            FeedbackMap.Clear();
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

                FeedbackMap[prefix] = Files[i];
            }
            systemInitialized = true;
            return true;
        }
        private void Update()
        {
            if (!suitDisabled) scheduler.Tick(clock.Elapsed.TotalSeconds);
        }

        private void OnDestroy()
        {
            StopAllHapticFeedback();
            suitDisabled = true;
            systemInitialized = false;
        }
        #endregion


        #region PlayingHapticsEffects

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

        public static MyHitData getAngleAndShift(Player player, Vector3 hit)
        {
            // bhaptics starts in the front, then rotates to the left. 0° is front, 90° is left, 270° is right.
            // y is "up", z is "forward" in local coordinates
            Vector3 patternOrigin = new Vector3(0f, 0f, 1f);
            Vector3 hitPosition = hit - player.transform.position;
            Quaternion PlayerRotation = player.transform.rotation;
            Vector3 playerDir = PlayerRotation.eulerAngles;
            // get rid of the up/down component to analyze xz-rotation
            Vector3 flattenedHit = new Vector3(hitPosition.x, 0f, hitPosition.z);

            // get angle. .Net < 4.0 does not have a "SignedAngle" function...
            float earlyhitAngle = Vector3.Angle(flattenedHit, patternOrigin);
            // check if cross product points up or down, to make signed angle myself
            Vector3 earlycrossProduct = Vector3.Cross(flattenedHit, patternOrigin);
            if (earlycrossProduct.y > 0f) { earlyhitAngle *= -1f; }
            // relative to player direction
            float myRotation = earlyhitAngle - playerDir.y;
            // switch directions (bhaptics angles are in mathematically negative direction)
            myRotation *= -1f;
            // convert signed angle into [0, 360] rotation
            if (myRotation < 0f) { myRotation = 360f + myRotation; }

            // up/down shift is in y-direction
            float hitShift = hitPosition.y;
            //torso/player range in valheim
            float upperBound = 1.0f;
            float lowerBound = 0.0f;
            if (hitShift > upperBound) { hitShift = 0.5f; }
            else if (hitShift < lowerBound) { hitShift = -0.5f; }
            // ...and then spread/shift it to [-0.5, 0.5]
            else { hitShift = (hitShift - lowerBound) / (upperBound - lowerBound) - 0.5f; }
            // No tuple returns available in .NET < 4.0, so this is the easiest quickfix
            return new MyHitData(myRotation, hitShift);
        }

        public static void PlayBackHit(string key, float xzAngle, float yShift)
        {
            // two parameters can be given to the pattern to move it on the vest:
            // 1. An angle in degrees [0, 360] to turn the pattern to the left
            // 2. A shift [-0.5, 0.5] in y-direction (up and down) to move it up or down
            if (suitDisabled) { return; }
            if (FeedbackMap.ContainsKey(key))
            {
                ScaleOption scaleOption = new ScaleOption(1f, 1f);
                RotationOption rotationOption = new RotationOption(xzAngle, yShift);
                hapticPlayer.SubmitRegisteredVestRotation(key, key, rotationOption, scaleOption);
            }
            else
            {
                LogInfo("Feedback not registered: " + key);
            }
        }

        /**
         * Specific sword recoil effect using vest and arms tactosy
         */
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

        // Keep the public method names used by patches; effects are now scheduled on the Unity thread.
        public static void StartThreadHaptic(string EffectName, float intensity = 1.0f,
            bool timerNeeded = false, int sleep = 1000, float duration = 1.0f, int delayedStart = 0)
        {
            if (suitDisabled) return;
            var now = clock.Elapsed.TotalSeconds;
            if (timerNeeded)
            {
                if (now < nextThrottledStart) return;
                nextThrottledStart = now + 0.2;
                sleep = 200;
            }
            scheduler.Start(EffectName, intensity, duration, sleep / 1000.0, delayedStart / 1000.0, now);
        }

        public static void StopThreadHaptic(string name, string[] callback = null)
        {
            scheduler.Stop(name, callback);
        }

        public static void StopThreadHapticDelayed(string name, int delay)
        {
            scheduler.StopAfter(name, delay / 1000.0, clock.Elapsed.TotalSeconds);
        }

        public static void StopHapticFeedback(string effect)
        {
            if (!suitDisabled && hapticPlayer != null) hapticPlayer.TurnOff(effect);
        }

        public static void StopAllHapticFeedback(string[] exceptions = null)
        {
            scheduler.StopAll(exceptions);
            foreach (var key in FeedbackMap.Keys)
                if (exceptions == null || !exceptions.Contains(key)) StopHapticFeedback(key);
        }
        #endregion
    }

    public class MyHitData
    {
        public float angle;
        public float shift;

        public MyHitData(float hitAngle, float hitShift)
        {
            angle = hitAngle;
            shift = hitShift;
        }
    }
}