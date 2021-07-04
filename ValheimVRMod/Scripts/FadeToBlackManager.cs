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
        private Material _fadeMaterial = null;
        private int _fadeMaterialColorID;
        private Color _currentColor = Color.clear;
        private Color _fadeToColor = Color.clear;
        private Color _fadeColorStep = Color.clear;

        public bool ShouldFadeToBlack => (Player.m_localPlayer != null && (
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
                    StartFade(Color.black, 0.2f);
                    OnFadeToBlack?.Invoke();
                }
                else
                {
                    StartFade(Color.clear, 0.15f);
                    OnFadeToWorld?.Invoke();
                }
                _lastShouldFade = shouldFade;
            }
        }

        public void StartFade(Color targetColor, float fadeDuration)
        {
            if (fadeDuration > 0f)
            {
                _currentColor = new Color(0,0,0,0);
                _fadeToColor = targetColor;
                _fadeColorStep = (_fadeToColor - _currentColor) / fadeDuration;
            }
            else _currentColor = _fadeToColor;
        }

        private void OnEnable()
        {
            if(_fadeMaterial == null)
            {
		        Shader shader = ShaderLoader.GetShader("Custom/SteamVR_Fade");
                if (shader == null)
                {
                    Debug.Log("Fade shade is null.");
                }
                _fadeMaterial = new Material(shader);
                _fadeMaterial.renderQueue = (int)RenderQueue.Overlay - 1;
                _fadeMaterialColorID = Shader.PropertyToID("fadeColor");
            }
        }

        private void OnPostRender()
        {
            if(_currentColor != _fadeToColor)
            {
                //This only fades based on alpha
                if (Mathf.Abs(_currentColor.a - _currentColor.a) < Mathf.Abs(_currentColor.a) * Time.deltaTime)
                {
                    _currentColor = _fadeToColor;
                    _fadeColorStep = new Color(0f, 0f, 0f, 0f);
                }
                else
                {
                    _currentColor += _fadeColorStep * Time.deltaTime;
                }
            }

            if (_currentColor.a > 0f && _fadeMaterial)
            {
                _fadeMaterial.SetColor(_fadeMaterialColorID, _currentColor);
                _fadeMaterial.SetPass(0);
                GL.Begin(GL.QUADS);
                GL.Vertex3(-1f, -1f, 0f);
                GL.Vertex3(1f, -1f, 0f);
                GL.Vertex3(1f, 1f, 0f);
                GL.Vertex3(-1f, 1f, 0f);
                GL.End();
            }
        }
    }
}
