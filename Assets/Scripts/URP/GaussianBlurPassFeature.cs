using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable]
public class GaussianBlurRenderPassSetting
{
    public RenderPassEvent renderPassEvent;
    public Shader shader;
    public int iterations;
}

public class GaussianBlurPassFeature : ScriptableRendererFeature
{
    public GaussianBlurRenderPassSetting setting = new();
    private GaussianBlurRenderPass pass;

    public override void Create()
    {
        if(pass == null)
            pass = new();


        pass.Set(setting);
        pass.renderPassEvent = setting.renderPassEvent;
    }
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType != CameraType.Game)
            return;

        renderer.EnqueuePass(pass);
    }
    protected override void Dispose(bool disposing)
    {
        pass?.Dispose();
    }
}
public class GaussianBlurRenderPass : ScriptableRenderPass
{
    private GaussianBlurRenderPassSetting setting;
    //性能分析器采样器
    private ProfilingSampler sampler = new("Gaussian Blur");
    private RenderTextureDescriptor descriptor;
    private RTHandle source;
    private RTHandle rtBufferHorizontal;
    private RTHandle rtBufferVertical;
    private Material material;
    public void Set(GaussianBlurRenderPassSetting setting)
    {
        this.setting = setting;
    }
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        if (material == null && setting.shader != null)
        {
            material = CoreUtils.CreateEngineMaterial(setting.shader);
        }

        source = renderingData.cameraData.renderer.cameraColorTargetHandle;
        descriptor = renderingData.cameraData.cameraTargetDescriptor;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (material == null)
            return;
        CommandBuffer cmd = CommandBufferPool.Get("GaussianBlur");

        using (new ProfilingScope(cmd, sampler))
        {
            var desc = descriptor;
            desc.width = descriptor.width;
            desc.height = descriptor.height;
            desc.depthBufferBits = (int)DepthBits.None;
            desc.msaaSamples = 1;

            RenderingUtils.ReAllocateIfNeeded(
                ref rtBufferHorizontal,
                desc,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: "_GaussianBlurBufferHorizontal"
            );

            RenderingUtils.ReAllocateIfNeeded(
                ref rtBufferVertical,
                desc,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: "_GaussianBlurBufferVertical"
            );
            Blitter.BlitCameraTexture(cmd, source, rtBufferHorizontal);

            int iterations = Mathf.Max(0, setting.iterations);
            for (int i = 0; i < iterations; i++)
            {
                Blitter.BlitCameraTexture(
                    cmd, rtBufferHorizontal, rtBufferVertical, 
                    RenderBufferLoadAction.DontCare,
                    RenderBufferStoreAction.Store, 
                    material, 0);

                Blitter.BlitCameraTexture(
                    cmd, rtBufferVertical, rtBufferHorizontal, 
                    RenderBufferLoadAction.DontCare,
                    RenderBufferStoreAction.Store, 
                    material, 1);
            }

            Blitter.BlitCameraTexture(cmd, rtBufferHorizontal, source);
        }

        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        CommandBufferPool.Release(cmd);

    }
    public override void OnCameraCleanup(CommandBuffer cmd) { }
    public void Dispose()
    {
        rtBufferHorizontal?.Release();
        rtBufferVertical?.Release();
    }
}
