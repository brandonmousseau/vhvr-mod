using System.Collections.Generic;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts
{
    // Manages staves that are cast by pulling the trigger of the hand holding them and that play their
    // attack animation in full (Northern Vengeance, staff of protection). Unlike the other staves these are
    // not necessarily main hand weapons, so everything here is relative to the hand actually holding the item.
    public class Protector : LocalWeaponWield
    {
        public static readonly HashSet<string> ITEM_NAMES =
            new HashSet<string>(new string[] { "$item_staff_frostorbs", "$item_staffshield" });

        public static Protector instance;

        protected override void Awake()
        {
            base.Awake();
            instance = this;
        }

        protected override void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
            base.OnDestroy();
        }

        private bool IsInRightHand { get { return isDominantHandWeapon == VRPlayer.isRightHandMainWeaponHand; } }

        // A level read rather than an edge read, like the dead raiser: vanilla won't restart an attack that is
        // already playing, and the attack hold has to stay raised for as long as the player keeps casting.
        public bool AttemptingAttack
        {
            get { return (IsInRightHand ? SteamVR_Actions.valheim_Use : SteamVR_Actions.valheim_UseLeft).state; }
        }
    }
}
