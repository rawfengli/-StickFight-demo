using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static Unity.VisualScripting.Member;

[Serializable]
public class EnumButtonRenderPassSetting
{
    public RenderPassEvent renderPassEvent;
    public Shader shader;
    public Color edgeColor;
    public float threshold = 0.1f;
}

public class EnumButtonPassFeature : ScriptableRendererFeature
{
    public EnumButtonRenderPassSetting setting;
    private EnumButtonRenderPass pass;

    public override void Create()
    {
        if(pass == null)
            pass = new();


        pass.Set(setting);
        pass.renderPassEvent = setting.renderPassEvent;
    }
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(pass);
    }
}
public class EnumButtonRenderPass : ScriptableRenderPass
{
    private EnumButtonRenderPassSetting setting;
    private ProfilingSampler sampler = new("Enum Button Render Sampler");
    private RenderTextureDescriptor descriptor;
    private RTHandle source;
    private RTHandle rtBuffer;
    private Material material;
    private Material CreateMaterial(Shader shader)
    => CoreUtils.CreateEngineMaterial(shader);
    public void Set(EnumButtonRenderPassSetting setting)
    {
        this.setting = setting;
        rtBuffer = RTHandles.Alloc(1, name: "_BloomMipDown");
    }
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        material = CreateMaterial(setting.shader);
        source = renderingData.cameraData.renderer.cameraColorTargetHandle;

        descriptor = renderingData.cameraData.cameraTargetDescriptor;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get("Enum Button");

        using (new ProfilingScope(cmd, sampler))
        {
            /*
            foreach (MenuButton button in MenuButtonManager.Instance.buttons)
            {
                RenderTextureDescriptor desc = descriptor;
                desc.depthBufferBits = (int)DepthBits.None;
                cmd.DrawMesh(button.image, Matrix4x4.identity material);
                button
                
                Blitter.BlitCameraTexture(
                    cmd, source, , 
                    RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, 
                    material, 0);
            }
            */
        }

        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        CommandBufferPool.Release(cmd);

    }
    public override void OnCameraCleanup(CommandBuffer cmd)
    {
    }
}

