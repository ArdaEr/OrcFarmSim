using System.Collections.Generic;
using UnityEngine;

public sealed class OutlineGlowTarget : MonoBehaviour
{
    [SerializeField] private Color color = new Color(0.2f, 1f, 0.3f, 1f);
    [SerializeField, Range(0f, 8f)] private float intensity = 2f;
    [SerializeField] private Renderer[] renderers;

    public Color Color => color;
    public float Intensity => intensity;
    public Renderer[] Renderers => renderers;

    private static readonly List<OutlineGlowTarget> s_Active = new();
    public static IReadOnlyList<OutlineGlowTarget> Active => s_Active;

    public void SetColor(Color c) => color = c;
    public void SetIntensity(float i) => intensity = i;
    public void SetRenderers(Renderer[] r) => renderers = r;

    public void RefreshRenderers()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void Reset()
    {
        RefreshRenderers();
    }

    private void OnEnable()
    {
        if (!s_Active.Contains(this)) s_Active.Add(this);
    }

    private void OnDisable()
    {
        s_Active.Remove(this);
    }
}
