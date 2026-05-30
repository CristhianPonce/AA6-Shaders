using UnityEngine;

/// <summary>
/// Sends global values to the mushroom Shader Graph:
/// player position, affect radius, movement intensity and optional contrast.
/// Put this script on an empty GameObject in the scene.
/// </summary>
[ExecuteAlways]
public class MushroomGlobalShaderController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;

    [Header("Mushroom movement")]
    [Min(0.01f)]
    [SerializeField] private float affectRadius = 2.0f;

    [SerializeField] private float affectIntensity = 0.5f;

    [Tooltip("0 = linear falloff. Higher values make the movement stronger only when the player is very close.")]
    [Min(0.0f)]
    [SerializeField] private float affectContrast = 0.0f;

    private static readonly int PlayerPositionWS_ID = Shader.PropertyToID("_MushroomPlayerPositionWS");
    private static readonly int AffectRadius_ID = Shader.PropertyToID("_MushroomAffectRadius");
    private static readonly int AffectIntensity_ID = Shader.PropertyToID("_MushroomAffectIntensity");
    private static readonly int AffectContrast_ID = Shader.PropertyToID("_MushroomAffectContrast");

    private void OnEnable()
    {
        ApplyValues();
    }

    private void Update()
    {
        ApplyValues();
    }

    private void OnValidate()
    {
        ApplyValues();
    }

    private void ApplyValues()
    {
        Vector3 position = player != null ? player.position : transform.position;

        Shader.SetGlobalVector(PlayerPositionWS_ID, new Vector4(position.x, position.y, position.z, 1.0f));
        Shader.SetGlobalFloat(AffectRadius_ID, Mathf.Max(0.01f, affectRadius));
        Shader.SetGlobalFloat(AffectIntensity_ID, affectIntensity);
        Shader.SetGlobalFloat(AffectContrast_ID, Mathf.Max(0.0f, affectContrast));
    }
}
