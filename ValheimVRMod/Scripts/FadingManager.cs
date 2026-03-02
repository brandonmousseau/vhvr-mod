using System;
using System.Collections;
using UnityEngine;
using Valve.VR;

namespace ValheimVRMod.Scripts 
{
    /// <summary>
    /// Scripts that fades the world to black when certain events are happening (Player Death, Teleport, Player sleep, Cutscenes)
    /// and displays red-out to indicate low health
    /// Based on SteamVR_Fade with proper material
    /// </summary>
    public class FadingManager : MonoBehaviour
    {
        public event Action OnFadeToBlack;
        public event Action OnFadeToWorld;

        private bool _lastShouldFadeToBlack = false;
        public bool IsFadingToBlack => _lastShouldFadeToBlack;

        private bool ShouldFadeToBlack => (Player.m_localPlayer != null && (
                                        Player.m_localPlayer.IsSleeping() 
                                        || Player.m_localPlayer.IsDead() 
                                        || Player.m_localPlayer.InBed() 
                                        || Player.m_localPlayer.IsTeleporting())) ||
                                        (Hud.instance?.m_loadingScreen && Hud.instance.m_loadingScreen.isActiveAndEnabled);

        private Coroutine lowHealthPulseCoroutine;
        private bool isLowHealthPulsing;
        private float lowHealthPulseAlpha;
        private float lowHealthPulseInterval;

        private void FixedUpdate()
        {
            if (ShouldFadeToBlack)
            {
                StopLowHealthPulse();
                if (!_lastShouldFadeToBlack)
                {
                    SteamVR_Fade.Start(Color.black, 0.2f);
                    OnFadeToBlack?.Invoke();
                    _lastShouldFadeToBlack = true;
                }
            }
            else
            {
                if (_lastShouldFadeToBlack)
                {
                    SteamVR_Fade.Start(Color.clear, 0.15f);
                    OnFadeToWorld?.Invoke();
                    _lastShouldFadeToBlack = false;
                }
                UpdateLowHealthPulse();
            }
        }

        private void UpdateLowHealthPulse() {
            var player = Player.m_localPlayer;
            if (player == null)
            {
                StopLowHealthPulse();
                return;
            }

            var maxHealth = player.GetMaxHealth();
            var currentHealth = player.GetHealth();
            var warningHealth = Mathf.Min(maxHealth * 0.25f, 32);

            if (currentHealth > warningHealth) {
                StopLowHealthPulse();
                return;
            }

            lowHealthPulseAlpha = Mathf.Lerp(0.25f, 0, currentHealth / warningHealth);
            lowHealthPulseInterval = Mathf.Min(currentHealth, 32) / 64 + 0.25f;
            StartLowHealthPulse();
        }

        private void StartLowHealthPulse()
        {
            if (isLowHealthPulsing)
            {
                return;
            }
            if (lowHealthPulseCoroutine != null)
            {
                StopCoroutine(lowHealthPulseCoroutine);
            }
            lowHealthPulseCoroutine = StartCoroutine(PulseRed());
            isLowHealthPulsing = true;
        }

        private void StopLowHealthPulse()
        {
            if (!isLowHealthPulsing)
            {
                return;
            }
            if (lowHealthPulseCoroutine != null)
            {
                StopCoroutine(lowHealthPulseCoroutine);
            }
            SteamVR_Fade.Start(Color.clear, 0.5f);
            isLowHealthPulsing = false;
        }

        private IEnumerator PulseRed()
        {
            while (true)
            {
                SteamVR_Fade.Start(new Color(1f, 0f, 0f, lowHealthPulseAlpha), lowHealthPulseInterval);
                yield return new WaitForSeconds(lowHealthPulseInterval);
                SteamVR_Fade.Start(new Color(1f, 0f, 0f, 0f), lowHealthPulseInterval);
                yield return new WaitForSeconds(lowHealthPulseInterval);
            }
        }
    }
}
