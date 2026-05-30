using UnityEngine;

/// <summary>
/// Applies a random per-renderer color using MaterialPropertyBlock.
/// This avoids creating material copies and can preserve GPU Instancing
/// if the _InstanceColor property is marked as Instanced / Hybrid Per Instance
/// in Shader Graph.
/// </summary>
[ExecuteAlways]
public class MushroomRandomColorController : MonoBehaviour
{
    [Header("Target renderers")]
    [SerializeField] private Renderer[] targetRenderers;

    [Header("Random color ranges")]
    [Range(0.0f, 1.0f)] [SerializeField] private float minHue = 0.0f;
    [Range(0.0f, 1.0f)] [SerializeField] private float maxHue = 1.0f;
    [Range(0.0f, 1.0f)] [SerializeField] private float minSaturation = 0.45f;
    [Range(0.0f, 1.0f)] [SerializeField] private float maxSaturation = 1.0f;
    [Range(0.0f, 2.0f)] [SerializeField] private float minValue = 0.75f;
    [Range(0.0f, 2.0f)] [SerializeField] private float maxValue = 1.15f;

    [Header("Seed")]
    [SerializeField] private int seedOffset = 0;

    private static readonly int InstanceColor_ID = Shader.PropertyToID("_InstanceColor");
    private MaterialPropertyBlock propertyBlock;

    private void Reset()
    {
        targetRenderers = GetComponentsInChildren<Renderer>();
        ApplyColor();
    }

    private void OnEnable()
    {
        EnsureReferences();
        ApplyColor();
    }

    private void OnValidate()
    {
        EnsureReferences();
        ApplyColor();
    }

    [ContextMenu("Randomize Seed")]
    private void RandomizeSeed()
    {
        seedOffset = Random.Range(int.MinValue, int.MaxValue);
        ApplyColor();
    }

    private void EnsureReferences()
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            targetRenderers = GetComponentsInChildren<Renderer>();
        }

        propertyBlock ??= new MaterialPropertyBlock();
    }

    private void ApplyColor()
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
            return;

        propertyBlock ??= new MaterialPropertyBlock();

        int seed = GetInstanceID() + seedOffset;
        float h = Mathf.Lerp(minHue, maxHue, Hash01(seed * 17 + 1));
        float s = Mathf.Lerp(minSaturation, maxSaturation, Hash01(seed * 31 + 2));
        float v = Mathf.Lerp(minValue, maxValue, Hash01(seed * 47 + 3));

        Color instanceColor = Color.HSVToRGB(h, s, Mathf.Clamp01(v));
        instanceColor.a = 1.0f;

        foreach (Renderer targetRenderer in targetRenderers)
        {
            if (targetRenderer == null)
                continue;

            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(InstanceColor_ID, instanceColor);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    private static float Hash01(int value)
    {
        unchecked
        {
            uint x = (uint)value;
            x ^= x >> 16;
            x *= 0x7feb352d;
            x ^= x >> 15;
            x *= 0x846ca68b;
            x ^= x >> 16;
            return (x & 0x00FFFFFF) / 16777216.0f;
        }
    }
}
