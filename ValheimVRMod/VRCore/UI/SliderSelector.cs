using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.VRCore.UI
{
    public class SliderSelector : MonoBehaviour 
    {
        private const float SLIDER_DEADZONE = 0.5f;
        private const float SLIDER_COOLDOWN = 0.4f;
        private Slider _splitSlider;
        private IEnumerator _doSliderMovementDelayed;
        private int _cumulativeDirection = 0;
        
        private void Awake() 
        {
            _splitSlider = GetComponent<Slider>();
        }

        private void OnDisable() 
        {
            StopAllCoroutines();
        }

        private void Update() 
        {
            float axisValue = VRControls.instance.GetJoyRightStickX();
            if(Mathf.Abs(axisValue) > SLIDER_DEADZONE)
            {
                if(_doSliderMovementDelayed == null)
                {
                    float sign = Mathf.Sign(axisValue);
                    float delay = _cumulativeDirection != 0 ? SLIDER_COOLDOWN / Mathf.Abs(_cumulativeDirection): 0;
                    StartCoroutine((_doSliderMovementDelayed = DoSliderMovement(delay, sign)));
                }
            }
            else if(_doSliderMovementDelayed != null)
            {
                _cumulativeDirection = 0;
                StopCoroutine(_doSliderMovementDelayed);
                _doSliderMovementDelayed = null;
            }
            else _cumulativeDirection = 0;
        }

        private IEnumerator DoSliderMovement(float delay, float direction)
        {
            yield return new WaitForSeconds(delay);
            if(Mathf.Sign(direction) != Mathf.Sign(_cumulativeDirection)) _cumulativeDirection = 0;
            var newValue = (_splitSlider.value + direction) % _splitSlider.maxValue;
            newValue = newValue >= _splitSlider.minValue ? newValue : _splitSlider.maxValue;
            _splitSlider.value = newValue;
            _cumulativeDirection += (int)direction;
            _doSliderMovementDelayed = null;
        }
    }
}