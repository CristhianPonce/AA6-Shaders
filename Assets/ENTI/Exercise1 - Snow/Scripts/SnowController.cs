using System.Collections;
using UnityEngine;

[AddComponentMenu("Snow Effect/Snow Controller")]
public class SnowController : MonoBehaviour
{
    [Header("Target Material")]
    [Tooltip("The material created from SnowFullscreenEffect.shader")]
    [SerializeField] private Material snowMaterial;

    [Header("Automatic Cycle")]
    [Tooltip("Duration of the fade-in and fade-out transitions (seconds)")]
    [SerializeField] private float transitionDuration = 8f;

    [Tooltip("How long to hold at each extreme before transitioning (seconds)")]
    [SerializeField] private float holdDuration = 4f;

    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Manual Preview")]
    [Tooltip("Check this to override the automatic cycle and control snow amount instantly with the slider below")]
    public bool useManualControl = false;

    [Range(0f, 1f)] public float manualSnowAmount = 0f;

    private float _currentAmount = 0f;
    private Coroutine _activeCoroutine;
    private static readonly int SnowAmountID = Shader.PropertyToID("_SnowAmount");

    private void Start()
    {
        if (snowMaterial == null)
        {
            Debug.LogError("SnowController: No material assigned!", this);
            enabled = false;
            return;
        }

        // Si no está el modo manual activo al arrancar, iniciamos el ciclo automático
        if (!useManualControl)
        {
            _activeCoroutine = StartCoroutine(AutomaticCycleRoutine());
        }
    }

    private void Update()
    {
        if (snowMaterial == null) return;

        if (useManualControl)
        {
            // ── CRÍTICO: Si el usuario activa el modo manual, paramos la corrutina INMEDIATAMENTE ──
            if (_activeCoroutine != null)
            {
                StopCoroutine(_activeCoroutine);
                _activeCoroutine = null;
            }

            // Aplicamos el valor del slider al instante
            SetSnowAmount(manualSnowAmount);
        }
        else
        {
            // Si desactivamos el modo manual y no hay ninguna corrutina corriendo, reiniciamos el ciclo
            if (_activeCoroutine == null)
            {
                _activeCoroutine = StartCoroutine(AutomaticCycleRoutine());
            }
        }
    }

    private IEnumerator AutomaticCycleRoutine()
    {
        // Forzamos empezar desde cero por seguridad
        SetSnowAmount(0f);

        while (true)
        {
            // Espera en Otoño (Sin nieve)
            yield return new WaitForSeconds(holdDuration);

            // Transición a invierno: 0 → 1
            yield return TransitionRoutine(0f, 1f, transitionDuration);

            // Espera en Invierno (Nieve completa)
            yield return new WaitForSeconds(holdDuration);

            // Transición a otoño: 1 → 0
            yield return TransitionRoutine(1f, 0f, transitionDuration);
        }
    }

    private IEnumerator TransitionRoutine(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetSnowAmount(to);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curved = transitionCurve.Evaluate(t);
            float newAmount = Mathf.LerpUnclamped(from, to, curved);

            SetSnowAmount(newAmount);
            yield return null;
        }

        SetSnowAmount(to);
    }

    private void SetSnowAmount(float amount)
    {
        _currentAmount = Mathf.Clamp01(amount);
        snowMaterial.SetFloat(SnowAmountID, _currentAmount);
    }

    private void OnDisable()
    {
        if (_activeCoroutine != null)
        {
            StopCoroutine(_activeCoroutine);
            _activeCoroutine = null;
        }

        // Dejar el escenario limpio al apagar el script o salir de Play
        if (snowMaterial != null)
        {
            snowMaterial.SetFloat(SnowAmountID, 0f);
        }
    }
}