using UnityEngine;

public class SnowEffectController : MonoBehaviour
{
    private static readonly int SnowAmountID = Shader.PropertyToID("_SnowAmmount");

    [Header("Material")]
    [SerializeField] private Material snowMaterial;

    [Header("Snow Cycle")]
    [SerializeField, Min(0.1f)] private float cycleDuration = 2.5f;

    [Header("Limits")]
    [SerializeField, Range(0f, 1f)] private float minAmount = 0f;
    [SerializeField, Range(0f, 1f)] private float maxAmount = 1f;

    [Header("Curve")]
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private void Update()
    {
        if (snowMaterial == null)
        {
            return;
        }

        float pingPongValue = Mathf.PingPong(Time.time / cycleDuration, 1f);

        float curvedValue = transitionCurve.Evaluate(pingPongValue);

        float snowAmount = Mathf.Lerp(minAmount, maxAmount, curvedValue);

        snowMaterial.SetFloat(SnowAmountID, snowAmount);
    }
}