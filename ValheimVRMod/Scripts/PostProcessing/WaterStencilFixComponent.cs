//This patch lets the game render the water over the skybox
//this solves a new bug in the base game which introduces a wrong stencil check
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.PostProcessing;
using UnityEngine.Rendering;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts.PostProcessing
{
    public class WaterPostProcessingModel : PostProcessingModel
    {
        public override void Reset()
        {
            
        }
    }

    public class WaterStencilFixComponent : PostProcessingComponentCommandBuffer<WaterPostProcessingModel>
    {
        public static readonly ConditionalWeakTable<PostProcessingBehaviour, WaterStencilFixComponent> PostProcessingExtension =
            new ConditionalWeakTable<PostProcessingBehaviour, WaterStencilFixComponent>();

        private Mesh _fullScreenQuad;
        private Material _stencilFixMaterial;
        
        public override bool active => true;

        public override CameraEvent GetCameraEvent() => CameraEvent.AfterGBuffer;

        public override string GetName() => "SeaFix";

        public static WaterPostProcessingModel fakeModel = new WaterPostProcessingModel();

        public override void OnEnable()
        {
            base.OnEnable();
            _fullScreenQuad = PostProcessingUtils.BuildQuad(1,1);
            _stencilFixMaterial = VRAssetManager.GetAsset<Material>("SeaStencilFix");
        }

        public override void PopulateCommandBuffer(CommandBuffer cb)
        {
            cb.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
            cb.DrawMesh(_fullScreenQuad, Matrix4x4.identity, _stencilFixMaterial);
        }
    }
}