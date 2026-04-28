using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public sealed class OutlineGlowPass : ScriptableRenderPass
{
    private static readonly int s_ColorId = Shader.PropertyToID("_OutlineGlowColor");
    private static readonly int s_IntensityId = Shader.PropertyToID("_OutlineGlowIntensity");
    private static readonly int s_BlurDirectionId = Shader.PropertyToID("_OutlineGlowBlurDirection");
    private static readonly int s_BlurRadiusId = Shader.PropertyToID("_OutlineGlowBlurRadius");
    private static readonly int s_OriginalMaskId = Shader.PropertyToID("_OutlineGlowOriginalMask");
    private static readonly int s_StrengthId = Shader.PropertyToID("_OutlineGlowStrength");

    private static readonly Vector4 s_FullScaleBias = new Vector4(1f, 1f, 0f, 0f);

    private readonly OutlineGlowFeature.Settings _settings;
    private readonly Material _maskMat;
    private readonly Material _blurMat;
    private readonly Material _compositeMat;

    private readonly List<OutlineGlowTarget> _targetsBuffer = new();

    public OutlineGlowPass(
        OutlineGlowFeature.Settings settings,
        Material maskMat,
        Material blurMat,
        Material compositeMat)
    {
        _settings = settings;
        _maskMat = maskMat;
        _blurMat = blurMat;
        _compositeMat = compositeMat;
    }

    private class MaskPassData
    {
        public Material maskMat;
        public List<OutlineGlowTarget> targets;
    }

    private class BlurPassData
    {
        public Material blurMat;
        public TextureHandle source;
        public Vector4 direction;
        public float radius;
    }

    private class CompositePassData
    {
        public Material compositeMat;
        public TextureHandle blurredMask;
        public float strength;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (OutlineGlowTarget.Active.Count == 0) return;
        if (_maskMat == null || _blurMat == null || _compositeMat == null) return;

        var resourceData = frameData.Get<UniversalResourceData>();
        var cameraData = frameData.Get<UniversalCameraData>();

        TextureHandle cameraColor = resourceData.activeColorTexture;
        if (!cameraColor.IsValid()) return;

        int downsample = Mathf.Max(1, _settings.downsample);
        int width = Mathf.Max(1, cameraData.cameraTargetDescriptor.width / downsample);
        int height = Mathf.Max(1, cameraData.cameraTargetDescriptor.height / downsample);

        var maskDesc = new TextureDesc(width, height)
        {
            colorFormat = GraphicsFormat.R16G16B16A16_SFloat,
            depthBufferBits = DepthBits.None,
            msaaSamples = MSAASamples.None,
            clearBuffer = true,
            clearColor = Color.clear,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "OutlineGlow_Mask"
        };

        var blurADesc = maskDesc;
        blurADesc.name = "OutlineGlow_BlurA";
        blurADesc.clearBuffer = false;

        var blurBDesc = maskDesc;
        blurBDesc.name = "OutlineGlow_BlurB";
        blurBDesc.clearBuffer = false;

        TextureHandle maskHandle = renderGraph.CreateTexture(maskDesc);
        TextureHandle blurAHandle = renderGraph.CreateTexture(blurADesc);
        TextureHandle blurBHandle = renderGraph.CreateTexture(blurBDesc);

        // --- Mask pass: draw each tagged renderer with its color into the mask RT.
        _targetsBuffer.Clear();
        for (int i = 0; i < OutlineGlowTarget.Active.Count; i++)
            _targetsBuffer.Add(OutlineGlowTarget.Active[i]);

        using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>("OutlineGlow_Mask", out var passData))
        {
            passData.maskMat = _maskMat;
            passData.targets = _targetsBuffer;

            builder.SetRenderAttachment(maskHandle, 0, AccessFlags.Write);
            builder.SetGlobalTextureAfterPass(maskHandle, s_OriginalMaskId);
            builder.AllowPassCulling(false);
            builder.AllowGlobalStateModification(true);

            builder.SetRenderFunc((MaskPassData data, RasterGraphContext ctx) =>
            {
                int drawCount = 0;
                for (int i = 0; i < data.targets.Count; i++)
                {
                    var target = data.targets[i];
                    if (target == null) continue;

                    ctx.cmd.SetGlobalColor(s_ColorId, target.Color);
                    ctx.cmd.SetGlobalFloat(s_IntensityId, target.Intensity);

                    var renderers = target.Renderers;
                    if (renderers == null) continue;

                    for (int r = 0; r < renderers.Length; r++)
                    {
                        var rend = renderers[r];
                        if (rend == null || !rend.enabled || !rend.gameObject.activeInHierarchy) continue;

                        int submeshCount = Mathf.Max(1, rend.sharedMaterials != null ? rend.sharedMaterials.Length : 1);
                        for (int s = 0; s < submeshCount; s++)
                        {
                            ctx.cmd.DrawRenderer(rend, data.maskMat, s, 0);
                            drawCount++;
                        }
                    }
                }
                if (drawCount == 0)
                    Debug.LogWarning($"[OutlineGlow] Mask pass had 0 draw calls. Active targets={data.targets.Count}");
            });
        }

        // --- Separable Gaussian blur. Iterate to widen the kernel.
        // Ping-pong: H always writes to A, V always writes to B.
        // Next iteration's H reads from B (last V output) and writes back to A.
        TextureHandle source = maskHandle;

        int iterations = Mathf.Max(1, _settings.blurIterations);
        float radius = Mathf.Max(0.01f, _settings.blurRadius);

        for (int it = 0; it < iterations; it++)
        {
            BuildBlurPass(
                renderGraph,
                $"OutlineGlow_BlurH_{it}",
                source,
                blurAHandle,
                new Vector4(1f / width, 0f, 0f, 0f),
                radius);

            BuildBlurPass(
                renderGraph,
                $"OutlineGlow_BlurV_{it}",
                blurAHandle,
                blurBHandle,
                new Vector4(0f, 1f / height, 0f, 0f),
                radius);

            source = blurBHandle;
        }

        TextureHandle finalBlur = source;

        // --- Composite onto camera color. Reads _OutlineGlowOriginalMask globally.
        using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>("OutlineGlow_Composite", out var passData))
        {
            passData.compositeMat = _compositeMat;
            passData.blurredMask = finalBlur;
            passData.strength = Mathf.Max(0f, _settings.compositeStrength);

            builder.UseTexture(finalBlur, AccessFlags.Read);
            builder.UseTexture(maskHandle, AccessFlags.Read);
            // ReadWrite preserves the existing camera color so our additive blend has something to add onto.
            builder.SetRenderAttachment(cameraColor, 0, AccessFlags.ReadWrite);
            builder.AllowPassCulling(false);
            builder.AllowGlobalStateModification(true);

            builder.SetRenderFunc((CompositePassData data, RasterGraphContext ctx) =>
            {
                ctx.cmd.SetGlobalFloat(s_StrengthId, data.strength);
                Blitter.BlitTexture(ctx.cmd, data.blurredMask, s_FullScaleBias, data.compositeMat, 0);
            });
        }
    }

    private void BuildBlurPass(
        RenderGraph renderGraph,
        string name,
        TextureHandle source,
        TextureHandle destination,
        Vector4 direction,
        float radius)
    {
        using (var builder = renderGraph.AddRasterRenderPass<BlurPassData>(name, out var passData))
        {
            passData.blurMat = _blurMat;
            passData.source = source;
            passData.direction = direction;
            passData.radius = radius;

            builder.UseTexture(source, AccessFlags.Read);
            builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
            builder.AllowPassCulling(false);
            builder.AllowGlobalStateModification(true);

            builder.SetRenderFunc((BlurPassData data, RasterGraphContext ctx) =>
            {
                ctx.cmd.SetGlobalVector(s_BlurDirectionId, data.direction);
                ctx.cmd.SetGlobalFloat(s_BlurRadiusId, data.radius);
                Blitter.BlitTexture(ctx.cmd, data.source, s_FullScaleBias, data.blurMat, 0);
            });
        }
    }
}
