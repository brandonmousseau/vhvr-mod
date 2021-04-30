using System.Runtime.CompilerServices;
using UnityEngine;

namespace ValheimVRMod.Utilities
{

    public struct WeaponCollider
    {

        private WeaponCollider(Vector3 pos, Vector3 euler, Vector3 scale)
        {
            this.pos = pos;
            this.euler = euler;
            this.scale = scale;
        }
        
        public Vector3 pos { get; }
        public Vector3 euler { get; }
        public Vector3 scale { get; }
        
        public static WeaponCollider create (float f_pos_x, float f_pos_y, float f_pos_z,
                                             float f_euler_x, float f_euler_y, float f_euler_z,
                                             float f_scale_x, float f_scale_y, float f_scale_z)
        {
            return new WeaponCollider(
                new Vector3(f_pos_x,f_pos_y,f_pos_z),
                new Vector3(f_euler_x,f_euler_y,f_euler_z),
                new Vector3(f_scale_x,f_scale_y,f_scale_z)
            );
        }
    }
}