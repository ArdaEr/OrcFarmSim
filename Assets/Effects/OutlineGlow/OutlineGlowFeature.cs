using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class OutlineGlowFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        [Header("Shaders")]
        public Shader maskShader;
        public Shader blurShader;
        public Shader compositeShader;

        [Header("Tuning")]
        [Range(0.5f, 16f)] public float blurRadius = 4f;
        [Range(1, 4)] public int blurIterations = 2;
        [Range(1, 4)] public int downsample = 2;
        [Range(0f, 4f)] public float compositeStrength = 1.5f;
    }

    public Settings settings = new Settings();

    private OutlineGlowPass _pass;
    private Material _maskMat;
    private Material _blurMat;
    private Material _compositeMat;

    public override void Create()
    {
        DisposeMaterials();

        if (settings.maskShader != null)
            _maskMat = CoreUtils.CreateEngineMaterial(settings.maskShader);
        if (settings.blurShader != null)
            _blurMat = CoreUtils.CreateEngineMaterial(settings.blurShader);
        if (settings.compositeShader != null)
            _compositeMat = CoreUtils.CreateEngineMaterial(settings.compositeShader);

        _pass = new OutlineGlowPass(settings, _maskMat, _blurMat, _compositeMat)
        {
            renderPassEvent = settings.renderPassEvent
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_pass == null) return;
        if (_maskMat == null || _blurMat == null || _compositeMat == null) return;
        if (OutlineGlowTarget.Active.Count == 0) return;

        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        DisposeMaterials();
    }

    private void DisposeMaterials()
    {
        CoreUtils.Destroy(_maskMat); _maskMat = null;
        CoreUtils.Destroy(_blurMat); _blurMat = null;
        CoreUtils.Destroy(_compositeMat); _compositeMat = null;
    }
}
