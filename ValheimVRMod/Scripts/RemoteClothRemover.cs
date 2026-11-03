using System.Collections.Generic;
using MagicaCloth2;
using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts
{
    // Turns off the cloth physics (capes) of a remote player animated by VRIK, as set by RemoveRemoteMagicCloth.
    // The root of a remote player moves in uneven physics steps, which the cloth turns into jitter. Without its
    // cloth the cape is drawn as a plain skinned mesh on the body. Destroying this restores the cloth.
    class RemoteClothRemover : MonoBehaviour
    {
        private readonly List<MagicaCloth> disabledCloths = new List<MagicaCloth>();
        private bool removed = false;
        private bool clothChanged = true;

        // Called when the equipment has set up new cloth, so that it is removed as well.
        public void OnClothSetup()
        {
            clothChanged = true;
        }

        void Update()
        {
            bool remove = VHVRConfig.RemoveRemoteMagicCloth();
            if (remove == removed && !clothChanged)
            {
                return;
            }
            removed = remove;
            clothChanged = false;
            if (remove)
            {
                disableCloths();
            }
            else
            {
                restoreCloths();
            }
        }

        void OnDestroy()
        {
            restoreCloths();
        }

        private void disableCloths()
        {
            // Forget cloth that went with unequipped items.
            disabledCloths.RemoveAll(cloth => cloth == null);
            foreach (MagicaCloth cloth in GetComponentsInChildren<MagicaCloth>(true))
            {
                if (cloth.enabled)
                {
                    // Disabling releases the cloth's simulation and its hold on the renderer.
                    cloth.enabled = false;
                    disabledCloths.Add(cloth);
                }
            }
        }

        private void restoreCloths()
        {
            foreach (MagicaCloth cloth in disabledCloths)
            {
                if (cloth != null)
                {
                    cloth.enabled = true;
                }
            }
            disabledCloths.Clear();
        }
    }
}
