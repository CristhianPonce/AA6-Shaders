using UnityEngine;

public class SnowEffectController : MonoBehaviour
{
    private static readonly int SnowAmountID = Shader.PropertyToID("_SnowAmount");

    [Header("Material")]
    [SerializeField] private Material snowMaterial;

    [Header("Animation")]
    [SerializeField] private float cycleDuration = 5f;
    [SerializeField] private AnimationCurve snowCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [SerializeField] private bool pingPong = true;

    private void Update()
    {
        if (snowMaterial == null || cycleDuration <= 0f)
        {
            return;
        }

        float t;

        if (pingPong)
        {
            t = Mathf.PingPong(Time.time / cycleDuration, 1f);
        }
        else
        {
            t = Mathf.Repeat(Time.time / cycleDuration, 1f);
        }

        float amount = Mathf.Clamp01(snowCurve.Evaluate(t));
        snowMaterial.SetFloat(SnowAmountID, amount);
    }
}