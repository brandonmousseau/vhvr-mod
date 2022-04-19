using System;
using UnityEngine;
using Valve.VR;
using HarmonyLib;
using ValheimVRMod.VRCore.UI;

namespace ValheimVRMod.Scripts
{
    /// <summary>
    /// Scripts that fades the world to black when certain events are happening (Player Death, Teleport, Player sleep, Cutscenes)
    /// Based on SteamVR_Fade with proper material
    /// </summary>
    public class FadeToBlackManager : MonoBehaviour
    {
        private bool bAllow;
        public static bool bFadeToBlack;
        public static bool bClear = false;
        public static bool bLogout = false;

        public event Action OnFadeToBlack;
        public event Action OnFadeToWorld;

        public bool IsFadingToBlack => bFadeToBlack;
        private bool ShouldFadeToBlack => (Player.m_localPlayer != null
                                        && (Player.m_localPlayer.InBed()
                                        || Player.m_localPlayer.IsDead()
                                        || Player.m_localPlayer.IsSleeping()
                                        || Player.m_localPlayer.IsTeleporting()))
                                        
        void Update()
        {
            //When First Starting A Game From Main Menu
            if (!bLogout && FejdStartup.instance.m_startingWorld)
            {
                bLogout = true;
                SteamVRFade(true);
                VRCore.UI.SoftwareCursor.instance.SetActive(false);
            }
        
            //When Loading Screens Are Visible(Fixes an issue with loading screens not always fading
            //For some reason there is a problem with the death loading screen not fading when using
            //m_startingWorld and combining this check into ShouldFadeToBlack...
            else if (Hud.instance?.m_loadingScreen && Hud.instance.m_loadingScreen.isActiveAndEnabled)
            {
                bAllow = true;
                SteamVRFade(true);
            }
            else if (bAllow)
            {
                bAllow = false;
                SteamVRFade(false);
            }
            else if (!bClear && ShouldFadeToBlack)
            {
                if (Player.m_localPlayer.InBed() || Player.m_localPlayer.IsSleeping())
                    MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "You Are Sleeping...", 1);

                MessageHud.instance.m_unlockMsgPrefab.transform.position = new Vector2(2500, 0);
                MessageHud.instance.m_messageText.transform.position = new Vector2(2500, 0);
                Hud.instance.gameObject.SetActive(false);

                bClear = true;
                SteamVRFade(true);
            }
            else if (bClear && !ShouldFadeToBlack)
            {
                MessageHud.instance.m_unlockMsgPrefab.transform.position = new Vector2(600, -200); // This returns to centered position
                MessageHud.instance.m_messageText.transform.position = new Vector2(Screen.width / 2 + 250, Screen.height / 2);

                bClear = false;
                SteamVRFade(false);
                Hud.instance.gameObject.SetActive(true);
            }
        }

        public void SteamVRFade(bool fade)
        {
            if (fade)
            {
                bFadeToBlack = true;
                SteamVR_Fade.Start(Color.black, 0.01f);
                OnFadeToBlack?.Invoke();
            }
            else
            {
                bFadeToBlack = false;
                SteamVR_Fade.Start(Color.clear, 0.2f);
                OnFadeToWorld?.Invoke();
            }
        }
    }

    // Fix m_startingWorld - Used to reset bLogout bool on logout
    // so that m_startingWorld black screen triggers again
    [HarmonyPatch(typeof(Menu), "OnLogoutYes")]
    class Menu_OnLogoutYes_Patch
    {
        static void Prefix()
        {
            FadeToBlackManager.bLogout = false;
        }
    }
}
