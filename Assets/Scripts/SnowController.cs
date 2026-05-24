// ============================================================
// SnowController.cs
// Animates the snow effect, cycling between no-snow and full-snow.
//
// SETUP:
//   1. Attach to any GameObject in the scene.
//   2. Assign "Snow Material" (the material using SnowFullscreenEffect.shader).
//   3. Press Play — the effect will cycle automatically.
//
// The controller also exposes public methods (TriggerSnowOn / TriggerSnowOff)
// so you can trigger transitions from timeline, UI buttons, etc.
//
// MANUAL MODE:
//   Enable "Use Manual Control" and drag the "Manual Snow Amount" slider
//   to preview the effect in Play mode without waiting for the cycle.
// ============================================================

using System.Collections;
using UnityEngine;

[AddComponentMenu("Snow Effect/Snow Controller")]
public class SnowController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────
    // Inspector
    // ─────────────────────────────────────────────────────────────────────
    [Header("Target Material")]
    [Tooltip("The material created from SnowFullscreenEffect.shader")]
    [SerializeField] private Material snowMaterial;

    [Header("Automatic Cycle")]
    [Tooltip("Duration of the fade-in and fade-out transitions (seconds)")]
    [SerializeField] private float transitionDuration = 8f;

    [Tooltip("How long to hold at each extreme before transitioning (seconds)")]
    [SerializeField] private float holdDuration = 4f;

    [Tooltip("Easing curve: X = normalised time, Y = snow amount (0→1)")]
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Manual Debug Control")]
    [Tooltip("When enabled, the cycle stops and the slider below controls snow")]
    [SerializeField] private bool useManualControl = false;

    [SerializeField, Range(0f, 1f)]
    private float manualSnowAmount = 0f;

    // ─────────────────────────────────────────────────────────────────────
    // Internals
    // ─────────────────────────────────────────────────────────────────────
    private static readonly int SnowAmountID = Shader.PropertyToID("_SnowAmount");

    private Coroutine _activeCoroutine;
    private float     _currentAmount;

    // ─────────────────────────────────────────────────────────────────────
    // Unity callbacks
    // ─────────────────────────────────────────────────────────────────────
    private void Start()
    {
        SetSnowAmount(0f);

        if (!useManualControl)
            _activeCoroutine = StartCoroutine(CycleRoutine());
    }

    private void Update()
    {
        if (useManualControl)
            SetSnowAmount(manualSnowAmount);
    }

    private void OnDestroy()
    {
        // Always reset when this component is removed / scene unloads.
        SetSnowAmount(0f);
    }

    // Called in editor when inspector values change
    private void OnValidate()
    {
        if (Application.isPlaying && useManualControl)
            SetSnowAmount(manualSnowAmount);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Public API — call from Timeline, UI, Animation Events, etc.
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Fade snow in from current level to 1.</summary>
    public void TriggerSnowOn()
    {
        StopActive();
        _activeCoroutine = StartCoroutine(
            TransitionRoutine(_currentAmount, 1f, transitionDuration));
    }

    /// <summary>Fade snow out from current level to 0.</summary>
    public void TriggerSnowOff()
    {
        StopActive();
        _activeCoroutine = StartCoroutine(
            TransitionRoutine(_currentAmount, 0f, transitionDuration));
    }

    /// <summary>Immediately set snow amount without transition.</summary>
    public void SetSnowAmountImmediate(float amount)
    {
        StopActive();
        SetSnowAmount(amount);
    }

    /// <summary>Resume the automatic cycle from the current amount.</summary>
    public void StartCycle()
    {
        StopActive();
        useManualControl = false;
        _activeCoroutine = StartCoroutine(CycleRoutine());
    }

    // ─────────────────────────────────────────────────────────────────────
    // Coroutines
    // ─────────────────────────────────────────────────────────────────────

    /// Endlessly cycles:  wait → fade in → wait → fade out → repeat
    private IEnumerator CycleRoutine()
    {
        while (true)
        {
            // Hold at no-snow
            yield return new WaitForSeconds(holdDuration);

            // Fade IN: 0 → 1
            yield return TransitionRoutine(0f, 1f, transitionDuration);

            // Hold at full snow
            yield return new WaitForSeconds(holdDuration);

            // Fade OUT: 1 → 0
            yield return TransitionRoutine(1f, 0f, transitionDuration);
        }
    }

    /// Smoothly interpolates snow amount between [from] and [to] over [duration].
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
            float t        = Mathf.Clamp01(elapsed / duration);
            float curved   = transitionCurve.Evaluate(t);
            float newAmount = Mathf.LerpUnclamped(from, to, curved);
            SetSnowAmount(newAmount);
            yield return null;
        }

        // Guarantee exact final value
        SetSnowAmount(to);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────
    private void SetSnowAmount(float amount)
    {
        _currentAmount = Mathf.Clamp01(amount);
        if (snowMaterial != null)
            snowMaterial.SetFloat(SnowAmountID, _currentAmount);
    }

    private void StopActive()
    {
        if (_activeCoroutine != null)
        {
            StopCoroutine(_activeCoroutine);
            _activeCoroutine = null;
        }
    }
}
