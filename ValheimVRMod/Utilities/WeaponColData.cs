using UnityEngine;

namespace ValheimVRMod.Utilities
{

    public readonly struct WeaponColData
    {

        public WeaponColData(Vector3 pos, Vector3 euler, Vector3 scale)
        {
            this.pos = pos;
            this.euler = euler;
            this.scale = scale;
        }
        
        public Vector3 pos { get; }
        public Vector3 euler { get; }
        public Vector3 scale { get; }
        
        public static WeaponColData create (float f_pos_x = 0, float f_pos_y = 0, float f_pos_z = 0,
                                             float f_euler_x = 0, float f_euler_y = 0, float f_euler_z = 0,
                                             float f_scale_x = 0, float f_scale_y = 0, float f_scale_z = 0)
        {
            return new WeaponColData(
                new Vector3(f_pos_x,f_pos_y,f_pos_z),
                new Vector3(f_euler_x,f_euler_y,f_euler_z),
                new Vector3(f_scale_x,f_scale_y,f_scale_z)
            );
        }
    }
}
