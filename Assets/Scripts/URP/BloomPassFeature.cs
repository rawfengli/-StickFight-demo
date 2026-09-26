using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable]
public class BloomRenderPassSetting
{
    public RenderPassEvent renderPassEvent;
    public Shader bloomShader;
    public Shader blitShader;
    [Range(0, 2)]
    public float threshold;
    public float softKnee;
    [Range(1, BloomRenderPass.MAX_MIP_COUNT)]
    public int iterations;
    [Range(0.05f, 0.95f)]
    public float scatter;
    public Color bloomColor;
    public float intensity;
}
public class BloomPassFeature : ScriptableRendererFeature
{
    public BloomRenderPassSetting setting = new();
    private BloomRenderPass pass;
    public override void Create()
    {
        if (pass == null)
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
public class BloomRenderPass : ScriptableRenderPass
{
    public const int MAX_MIP_COUNT = 16;//2^10 = 1024

    public BloomRenderPassSetting Setting;
    private RenderTextureDescriptor descriptor;

    private Material bloomMaterial;
    private Material blitMaterial;

    private RTHandle source;
    private RTHandle tempTexture;
    private RTHandle[] BloomMipDown;
    private RTHandle[] BloomMipUp;
    //Shader Property ID
    private static readonly int ThresholdID = Shader.PropertyToID("_Threshold");
    private static readonly int SoftKneeID = Shader.PropertyToID("_SoftKnee");
    private static readonly int LowTextureID = Shader.PropertyToID("_LowTexture");
    private static readonly int ScatterID = Shader.PropertyToID("_Scatter");
    private static readonly int BloomTextureID = Shader.PropertyToID("_BloomTexture");
    
    private static readonly int BloomColorID = Shader.PropertyToID("_BloomColor");
    private static readonly int IntensityID = Shader.PropertyToID("_Intensity");

    public void Set(BloomRenderPassSetting setting)
    {
        Setting = setting;

        BloomMipDown = new RTHandle[MAX_MIP_COUNT];
        BloomMipUp = new RTHandle[MAX_MIP_COUNT];
    }
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        if (bloomMaterial == null && Setting.bloomShader != null)
        {
            bloomMaterial = CoreUtils.CreateEngineMaterial(Setting.bloomShader);
        }

        if (blitMaterial == null && Setting.blitShader != null)
        {
            blitMaterial = CoreUtils.CreateEngineMaterial(Setting.blitShader);
        }

        source = renderingData.cameraData.renderer.cameraColorTargetHandle;
        descriptor = renderingData.cameraData.cameraTargetDescriptor;
    }
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (bloomMaterial == null || blitMaterial == null)
            return;
        CommandBuffer cmd = CommandBufferPool.Get("Bloom Cmd");

        var desc = descriptor;
        desc.height = descriptor.height;
        desc.width = descriptor.width;
        desc.depthBufferBits = (int)DepthBits.None;
        desc.msaaSamples = 1;
        desc.graphicsFormat = GraphicsFormat.B10G11R11_UFloatPack32;

        int maxSize = Mathf.Max(descriptor.width, descriptor.height);
        int iterations = Mathf.FloorToInt(Mathf.Log(maxSize, 2f));

        int mipCount = Mathf.Clamp(iterations, 1, Setting.iterations);
        if(mipCount <= 0)
        {
            cmd.Clear();
            CommandBufferPool.Release(cmd);
            return;
        }

        for (int i = 0; i < mipCount; i++)
        {
            RenderingUtils.ReAllocateIfNeeded(
                ref BloomMipUp[i],
                desc,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: "BloomMipUp_" + i.ToString());

            RenderingUtils.ReAllocateIfNeeded(
                ref BloomMipDown[i],
                desc,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: "BloomMipDown_" + i.ToString());

            desc.width = Mathf.Max(1, desc.width >> 1);
            desc.height = Mathf.Max(1, desc.height >> 1);
        }

        cmd.SetGlobalFloat(ThresholdID, Setting.threshold);
        cmd.SetGlobalFloat(SoftKneeID, Setting.softKnee);

        Blitter.BlitCameraTexture(
            cmd,
            source,
            BloomMipDown[0],
            RenderBufferLoadAction.DontCare,
            RenderBufferStoreAction.Store,
            bloomMaterial, 0);

        var lastDownRT = BloomMipDown[0];

        for (int i = 1; i < mipCount; i++)
        {
            Blitter.BlitCameraTexture(
                cmd,
                lastDownRT, 
                BloomMipUp[i], 
                RenderBufferLoadAction.DontCare,
                RenderBufferStoreAction.Store, 
                bloomMaterial, 1);

            Blitter.BlitCameraTexture(
                cmd, 
                BloomMipUp[i], 
                BloomMipDown[i], 
                RenderBufferLoadAction.DontCare,
                RenderBufferStoreAction.Store, 
                bloomMaterial, 2);

            lastDownRT = BloomMipDown[i];
        }

        for (int i = mipCount - 2; i >= 0; i--)
        {
            RTHandle lowMip;
            //上采样，先用下采样的最后一层的结果作为低层
            if (i == mipCount - 2)
                lowMip = BloomMipDown[i + 1];
            //然后再用上一轮上采样的结果作为低层
            else
                lowMip = BloomMipUp[i + 1];

            RTHandle highMip = BloomMipDown[i];
            RTHandle target = BloomMipUp[i];

            cmd.SetGlobalTexture(LowTextureID, lowMip);
            cmd.SetGlobalFloat(ScatterID, Setting.scatter);

            Blitter.BlitCameraTexture(
                cmd, 
                highMip,
                target, 
                RenderBufferLoadAction.DontCare,
                RenderBufferStoreAction.Store, 
                bloomMaterial, 3);
        }

        cmd.SetGlobalTexture(BloomTextureID, BloomMipUp[0]);
        cmd.SetGlobalColor(BloomColorID, Setting.bloomColor);
        cmd.SetGlobalFloat(IntensityID, Setting.intensity);


        var tempDesc = descriptor;
        tempDesc.height = descriptor.height;
        tempDesc.width = descriptor.width;
        tempDesc.depthBufferBits = (int)DepthBits.None;
        tempDesc.msaaSamples = 1;

        RenderingUtils.ReAllocateIfNeeded(
            ref tempTexture, 
            tempDesc, 
            FilterMode.Bilinear,
            TextureWrapMode.Clamp,
            name: "Temp Camera Texture");

        Blitter.BlitCameraTexture(
            cmd,
            source,
            tempTexture,
            RenderBufferLoadAction.DontCare,
            RenderBufferStoreAction.Store,
            blitMaterial, 0);

        Blitter.BlitCameraTexture(
            cmd,
            tempTexture, 
            source, 
            RenderBufferLoadAction.DontCare,
            RenderBufferStoreAction.Store, 
            bloomMaterial, 4);

        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        CommandBufferPool.Release(cmd);

    }
    public void Dispose()
    {
        foreach (var handle in BloomMipDown)
            handle?.Release();
        foreach (var handle in BloomMipUp)
            handle?.Release();

        tempTexture?.Release();
        source?.Release();

        CoreUtils.Destroy(bloomMaterial);
        CoreUtils.Destroy(blitMaterial);
    }

}
