using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class RuntimeWaterFlowController : MonoBehaviour
{
    [Header("Scene References")]
    [Tooltip("Transform que realmente se desplaza por la escena.")]
    [SerializeField] private Transform player;

    [Tooltip("Mesh Renderer del plano que muestra el agua.")]
    [SerializeField] private Renderer waterRenderer;

    [Tooltip("Material creado a partir de SG_WaterSimulation.")]
    [SerializeField] private Material simulationMaterialAsset;

    [Tooltip("Opcional: Raw Image para visualizar el flowmap durante las pruebas.")]
    [SerializeField] private RawImage debugPreview;

    [Header("Flowmap Texture")]
    [Tooltip("Resolución de la textura dinámica. 512 es un buen valor para la práctica.")]
    [Range(64, 1024)]
    [SerializeField] private int resolution = 512;

    [Header("Player Interaction")]
    [Tooltip("Radio mundial de influencia del player sobre el agua.")]
    [Min(0.01f)]
    [SerializeField] private float playerRadius = 1.2f;

    [Tooltip("Dureza del borde del pincel. Más alto = borde más definido.")]
    [Range(0f, 0.95f)]
    [SerializeField] private float playerHardness = 0.55f;

    [Tooltip("Velocidad a la que desaparece el flujo anterior.")]
    [Range(0f, 10f)]
    [SerializeField] private float fadeSpeed = 1.5f;

    private Material simulationMaterial;
    private MaterialPropertyBlock waterPropertyBlock;

    private RenderTexture previousFrame;
    private RenderTexture nextFrame;

    private Vector3 previousPlayerPosition;

    private static readonly int PreviousFrameId =
        Shader.PropertyToID("_PreviousFrame");

    private static readonly int VelocityId =
        Shader.PropertyToID("_Velocity");

    private static readonly int PositionId =
        Shader.PropertyToID("_Position");

    private static readonly int SimulationCenterId =
        Shader.PropertyToID("_SimulationCenter");

    private static readonly int SimulationSizeId =
        Shader.PropertyToID("_SimulationSize");

    private static readonly int PlayerRadiusId =
        Shader.PropertyToID("_PlayerRadius");

    private static readonly int PlayerHardnessId =
        Shader.PropertyToID("_PlayerHardness");

    private static readonly int FadeSpeedId =
        Shader.PropertyToID("_FadeSpeed");

    private static readonly int SimulationDeltaTimeId =
        Shader.PropertyToID("_SimulationDeltaTime");

    private static readonly int FlowmapId =
        Shader.PropertyToID("_Flowmap");

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (!ValidateReferences())
        {
            enabled = false;
            return;
        }

        simulationMaterial = new Material(simulationMaterialAsset)
        {
            name = $"{simulationMaterialAsset.name} (Runtime Instance)",
            hideFlags = HideFlags.HideAndDontSave
        };

        waterPropertyBlock = new MaterialPropertyBlock();

        CreateRenderTextures();

        previousPlayerPosition = player.position;

        PublishFlowmap();
    }

    private void LateUpdate()
    {
        if (simulationMaterial == null ||
            previousFrame == null ||
            nextFrame == null)
        {
            return;
        }

        float deltaTime = Mathf.Max(Time.deltaTime, 0.00001f);

        Vector3 currentPlayerPosition = player.position;
        Vector3 playerVelocity =
            (currentPlayerPosition - previousPlayerPosition) / deltaTime;

        Bounds waterBounds = waterRenderer.bounds;

        Vector3 simulationCenter = waterBounds.center;

        Vector3 simulationSize = new Vector3(
            Mathf.Max(waterBounds.size.x, 0.001f),
            1f,
            Mathf.Max(waterBounds.size.z, 0.001f)
        );

        simulationMaterial.SetTexture(PreviousFrameId, previousFrame);
        simulationMaterial.SetVector(VelocityId, playerVelocity);
        simulationMaterial.SetVector(PositionId, currentPlayerPosition);
        simulationMaterial.SetVector(SimulationCenterId, simulationCenter);
        simulationMaterial.SetVector(SimulationSizeId, simulationSize);
        simulationMaterial.SetFloat(PlayerRadiusId, playerRadius);
        simulationMaterial.SetFloat(PlayerHardnessId, playerHardness);
        simulationMaterial.SetFloat(FadeSpeedId, fadeSpeed);
        simulationMaterial.SetFloat(SimulationDeltaTimeId, deltaTime);


        Graphics.Blit(previousFrame, nextFrame, simulationMaterial, 0);

        SwapRenderTextures();

        PublishFlowmap();

        previousPlayerPosition = currentPlayerPosition;
    }

    private void CreateRenderTextures()
    {
        previousFrame = CreateFlowTexture("Runtime Flowmap A");
        nextFrame = CreateFlowTexture("Runtime Flowmap B");

        ClearToZeroFlow(previousFrame);
        ClearToZeroFlow(nextFrame);
    }

    private RenderTexture CreateFlowTexture(string textureName)
    {
        RenderTexture texture = new RenderTexture(
            resolution,
            resolution,
            0,
            RenderTextureFormat.ARGBHalf,
            RenderTextureReadWrite.Linear
        )
        {
            name = textureName,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false,
            antiAliasing = 1
        };

        texture.Create();

        return texture;
    }

    private static void ClearToZeroFlow(RenderTexture texture)
    {
        RenderTexture previouslyActive = RenderTexture.active;

        RenderTexture.active = texture;

        GL.Clear(false, true, new Color(0f, 0f, 0f, 1f));

        RenderTexture.active = previouslyActive;
    }

    private void SwapRenderTextures()
    {
        RenderTexture temporary = previousFrame;
        previousFrame = nextFrame;
        nextFrame = temporary;
    }

    private void PublishFlowmap()
    {
        waterRenderer.GetPropertyBlock(waterPropertyBlock);

        waterPropertyBlock.SetTexture(FlowmapId, previousFrame);

        waterRenderer.SetPropertyBlock(waterPropertyBlock);

        if (debugPreview != null)
        {
            debugPreview.texture = previousFrame;
        }
    }

    private bool ValidateReferences()
    {
        if (player == null)
        {
            Debug.LogError(
                "RuntimeWaterFlowController: falta asignar Player.",
                this
            );

            return false;
        }

        if (waterRenderer == null)
        {
            Debug.LogError(
                "RuntimeWaterFlowController: falta asignar Water Renderer.",
                this
            );

            return false;
        }

        if (simulationMaterialAsset == null)
        {
            Debug.LogError(
                "RuntimeWaterFlowController: falta asignar Simulation Material Asset.",
                this
            );

            return false;
        }

        if (waterRenderer.sharedMaterial == null ||
            !waterRenderer.sharedMaterial.HasProperty(FlowmapId))
        {
            Debug.LogError(
                "RuntimeWaterFlowController: el material del agua no contiene la propiedad _Flowmap.",
                this
            );

            return false;
        }

        if (!simulationMaterialAsset.HasProperty(PreviousFrameId) ||
            !simulationMaterialAsset.HasProperty(VelocityId) ||
            !simulationMaterialAsset.HasProperty(PositionId) ||
            !simulationMaterialAsset.HasProperty(SimulationCenterId) ||
            !simulationMaterialAsset.HasProperty(SimulationSizeId))
        {
            Debug.LogError(
                "RuntimeWaterFlowController: las referencias de propiedades de MAT_WaterSimulation no coinciden con el script.",
                this
            );

            return false;
        }

        return true;
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (waterRenderer != null)
        {
            waterRenderer.SetPropertyBlock(null);
        }

        if (debugPreview != null)
        {
            debugPreview.texture = null;
        }

        ReleaseRenderTexture(ref previousFrame);
        ReleaseRenderTexture(ref nextFrame);

        if (simulationMaterial != null)
        {
            Destroy(simulationMaterial);
            simulationMaterial = null;
        }
    }

    private static void ReleaseRenderTexture(ref RenderTexture texture)
    {
        if (texture == null)
        {
            return;
        }

        texture.Release();
        Destroy(texture);
        texture = null;
    }
}