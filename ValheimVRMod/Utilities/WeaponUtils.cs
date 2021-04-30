using System;
using System.ComponentModel;
using UnityEngine;

namespace ValheimVRMod.Utilities
{
    public class WeaponUtils
    {

        private static readonly string[] NAMES =
        {
            "AxeBronze",
            "AxeBlackMetal"
            // TODO: more weapons
        };

        private static readonly WeaponCollider[] COLLIDERS =
        {
              // Viking_Axe
            WeaponCollider.create( 
                0.0105f,  0.0f, -0.03929f,
                0,  -9, 0,
                0.01f,  0.001f, 0.021f
            ),// AxeBlackMetal
            WeaponCollider.create( 
                0.058f,  0.714f, -0.003f,
                93.75299f,  90, 90,
                0.1f,  0.01f, 0.21f
            ),
            // TODO: more weapons
        };

        public static WeaponCollider getForName(string name)
        {
            int index = Array.IndexOf(NAMES, name);

            if (index < 0)
            {
                throw new InvalidEnumArgumentException(); 
            }
            
            return COLLIDERS[index];
            
        }

    }
}