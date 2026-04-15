using UnityEngine;
using UnityEngine.Rendering.Universal;

public class FroxelVolumetricLightingRendererFeature : ScriptableRendererFeature
{
    public FroxelVolumetricLightingSettings froxelVolumetricLightingSettings;

    private FroxelVolumetricLightingRenderPass froxelVolumetricLightingRenderPass;

    public override void Create()
    {
        froxelVolumetricLightingRenderPass?.Dispose();
        froxelVolumetricLightingRenderPass = null;

        if (froxelVolumetricLightingSettings == null || froxelVolumetricLightingSettings.computeShader == null)
        {
            return;
        }

        froxelVolumetricLightingRenderPass = new FroxelVolumetricLightingRenderPass(froxelVolumetricLightingSettings);
        froxelVolumetricLightingRenderPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (froxelVolumetricLightingRenderPass == null)
        {
            return;
        }

        renderer.EnqueuePass(froxelVolumetricLightingRenderPass);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        froxelVolumetricLightingRenderPass?.Dispose();
        froxelVolumetricLightingRenderPass = null;
    }
}
