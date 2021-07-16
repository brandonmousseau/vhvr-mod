using System;
using UnityEngine;
using Valve.VR;
using UnityEngine.PostProcessing;
using UnityEngine.Rendering;

namespace ValheimVRMod.Scripts 
{
    /// <summary>
    /// Scripts that fades the world to black when certain events are happening (Player Death, Teleport, Player sleep, Cutscenes)
    /// Based on SteamVR_Fade with proper material
    /// </summary>
    public class FadeToBlackManager : MonoBehaviour
    {
        public event Action OnFadeToBlack;
        public event Action OnFadeToWorld;

        private bool _lastShouldFade = false;
        public bool IsFadingToBlack => _lastShouldFade;

        private bool ShouldFadeToBlack => (Player.m_localPlayer != null && (
                                        Player.m_localPlayer.IsSleeping() 
                                        || Player.m_localPlayer.IsDead() 
                                        || Player.m_localPlayer.InBed() 
                                        || Player.m_localPlayer.IsTeleporting())) ||
                                        (Hud.instance?.m_loadingScreen && Hud.instance.m_loadingScreen.isActiveAndEnabled);


        private void FixedUpdate()
        {
            bool shouldFade = ShouldFadeToBlack;
            if(_lastShouldFade != shouldFade)
            {
                if(shouldFade)
                {
                    SteamVR_Fade.Start(Color.black, 0.2f);
                    OnFadeToBlack?.Invoke();
                }
                else
                {
                    SteamVR_Fade.Start(Color.clear, 0.15f);
                    OnFadeToWorld?.Invoke();
                }
                _lastShouldFade = shouldFade;
            }
        }
    }
}
