using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ValheimVRMod.Utilities
{
    public class WeaponUtils
    {
        
        private static readonly Dictionary<string, WeaponCollider> colliders = new Dictionary<string, WeaponCollider>
        {
            {
                "AxeBronze", WeaponCollider.create(
                    0.0105f,  0, -0.03929f,
                    0,  -9, 0,
                    0.01f,  0.001f, 0.021f
                )}, {
                "AxeBlackMetal", WeaponCollider.create(
                    0.058f,  0.714f, 0,
                    0,  0, 0,
                    0.1f,  0.21f, 0.01f
                )}, {
                "AxeFlint", WeaponCollider.create( 
                    -0.1339f,  2.2458f, 0,
                    0,  0, 0,
                    0.9623004f,  0.36668f, 0.2870208f
                )}, {
                "PickaxeIron", WeaponCollider.create( 
                    0,  1.9189f, 0,
                    0,  0, 0,
                    2.865605f,  0.1f, 0.1f
                )}, {
                "PickaxeBronze", WeaponCollider.create( 
                    -0.711f,  2.219f, 0,
                    0,  0, 2.903f,
                    1.192384f,  0.1884746f, 0.1568103f
                )}, {
                "PickaxeAntler", WeaponCollider.create( 
                    -0.722f,  2.256f, 0,
                    0,  0, 0,
                    1.192384f,  0.1884746f, 0.1568103f
                )}, {
                "Hammer", WeaponCollider.create( 
                    0,  0.956f, 0,
                    0,  0, 0,
                    0.9030886f,  0.3803992f, 0.3803992f
                )}
            // TODO: more weapons
        };

        public static WeaponCollider getForName(string name)
        {

            if (colliders.ContainsKey(name)) {
                return colliders[name];
            }
            
            throw new InvalidEnumArgumentException(); 
            
        }

    }
}