// ============================================================
// SnowRenderFeature.cs
// URP Scriptable Renderer Feature for the snow post-process.
//
// Contains two render passes:
//   1. StencilWritePass  — renders "no-snow" objects to stencil buffer
//   2. SnowFullscreenPass — applies fullscreen snow effect (skips stencil=1)
//
// Compatible with URP 14+ (Unity 2022.2+)
//
// SETUP IN EDITOR:
//   1. Open your URP Renderer asset (e.g. "UniversalRenderer").
//   2. Add Renderer Feature → "Snow Render Feature".
//   3. Assign:
//      • Snow Material   → Material using SnowFullscreenEffect.shader
//      • Stencil Material→ Material using StencilWrite.shader
//      • Stencil Layer   → The Unity Layer for objects excluded from snow
//
// URP RENDERER SETTINGS required:
//   • Depth Texture:              ON
//   • Opaque Texture:             ON
//   • Depth Normal Prepass:       ON  (adds _CameraNormalsTexture)
// ============================================================

using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable]
public class SnowRenderFeature : ScriptableRendererFeature
{
    // ─────────────────────────────────────────────────────────────────────
    // Inspector-visible settings
    // ─────────────────────────────────────────────────────────────────────
    [Serializable]
    public class Settings
    {
        [Header("Materials")]
        [Tooltip("Material created from SnowFullscreenEffect.shader")]
        public Material snowMaterial;

        [Tooltip("Material created from StencilWrite.shader")]
        public Material stencilMaterial;

        [Header("Stencil Exclusion Layer")]
        [Tooltip("Objects on this layer will NOT receive snow (stencil-masked)")]
        public LayerMask stencilLayer;

        [Header("Timing")]
        [Tooltip("Where in the frame to apply the snow effect")]
        public RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    public Settings settings = new Settings();

    // ─────────────────────────────────────────────────────────────────────
    // Private pass instances
    // ─────────────────────────────────────────────────────────────────────
    private StencilWritePass  _stencilPass;
    private SnowFullscreenPass _snowPass;

    // ─────────────────────────────────────────────────────────────────────
    // ScriptableRendererFeature API
    // ─────────────────────────────────────────────────────────────────────
    public override void Create()
    {
        _stencilPass = new StencilWritePass(settings)
        {
            // Write stencil right after opaque objects are rendered.
            // The depth buffer already has correct depth at this point.
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques
        };

        _snowPass = new SnowFullscreenPass(settings)
        {
            renderPassEvent = settings.passEvent
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.snowMaterial == null) return;

        // Only render for Game and Scene cameras
        var camType = renderingData.cameraData.cameraType;
        if (camType != CameraType.Game && camType != CameraType.SceneView) return;

        if (settings.stencilMaterial != null)
            renderer.EnqueuePass(_stencilPass);

        renderer.EnqueuePass(_snowPass);
    }

    /// Called after AddRenderPasses — safe place to pass RTHandles to passes.
    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        _snowPass.Setup(
            renderer.cameraColorTargetHandle,
            renderer.cameraDepthTargetHandle
        );
    }

    protected override void Dispose(bool disposing)
    {
        _snowPass?.Dispose();
    }

    // =========================================================================
    // PASS 1 — StencilWritePass
    // Renders the "no-snow" layer objects with the stencil-write material.
    // This marks their pixels with stencil=1 so the snow pass can skip them.
    // =========================================================================
    sealed class StencilWritePass : ScriptableRenderPass
    {
        private readonly Settings         _settings;
        private readonly ProfilingSampler _sampler = new ProfilingSampler("Snow :: Stencil Write");

        public StencilWritePass(Settings settings)
        {
            _settings = settings;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_settings.stencilMaterial == null) return;

            var cmd = CommandBufferPool.Get();

            using (new ProfilingScope(cmd, _sampler))
            {
                // Flush any queued commands before DrawRenderers
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // ── Draw settings: override every object's shader with StencilWrite ──
                var drawSettings = CreateDrawingSettings(
                    // Tag doesn't matter much — we override the material anyway
                    new ShaderTagId("UniversalForward"),
                    ref renderingData,
                    renderingData.cameraData.defaultOpaqueSortFlags
                );
                drawSettings.overrideMaterial          = _settings.stencilMaterial;
                drawSettings.overrideMaterialPassIndex = 0;

                // ── Filter: only objects on the stencilLayer ──────────────────────
                var filterSettings = new FilteringSettings(
                    RenderQueueRange.all,
                    _settings.stencilLayer
                );

                var renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);

                context.DrawRenderers(
                    renderingData.cullResults,
                    ref drawSettings,
                    ref filterSettings,
                    ref renderStateBlock
                );
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    // =========================================================================
    // PASS 2 — SnowFullscreenPass
    // Copies the camera colour, then draws a fullscreen quad with:
    //   • the snow effect shader
    //   • the camera depth-stencil buffer bound  ← key for hardware stencil test
    //
    // GPU skips any pixel where stencil == 1 (the objects from Pass 1).
    // =========================================================================
    sealed class SnowFullscreenPass : ScriptableRenderPass
    {
        private readonly Settings         _settings;
        private readonly ProfilingSampler _sampler = new ProfilingSampler("Snow :: Fullscreen Effect");

        private RTHandle _colorHandle;
        private RTHandle _depthHandle;
        private RTHandle _tempHandle;   // intermediate copy of the frame colour

        public SnowFullscreenPass(Settings settings)
        {
            _settings = settings;

            // Tell URP to generate the depth and normal textures
            // (_CameraDepthTexture, _CameraNormalsTexture) before this pass runs.
            ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        }

        /// Called by the feature's SetupRenderPasses so we get RTHandles early.
        public void Setup(RTHandle colorHandle, RTHandle depthHandle)
        {
            _colorHandle = colorHandle;
            _depthHandle = depthHandle;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // Allocate temp RT matching camera colour (no depth needed)
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples     = 1;

            RenderingUtils.ReAllocateIfNeeded(
                ref _tempHandle,
                desc,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: "_SnowTempTex"
            );
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_settings.snowMaterial == null) return;
            if (_colorHandle == null || _depthHandle == null) return;

            var cmd = CommandBufferPool.Get();

            using (new ProfilingScope(cmd, _sampler))
            {
                // ── Step 1: Snapshot current frame into temp buffer ───────────────
                // (We can't sample _colorHandle while also writing to it)
                Blitter.BlitCameraTexture(cmd, _colorHandle, _tempHandle);

                // ── Step 2: Tell the snow shader where the frame snapshot is ─────
                cmd.SetGlobalTexture(Shader.PropertyToID("_BlitTexture"), _tempHandle);

                // ── Step 3: Bind colour + depth targets ───────────────────────────
                // CRITICAL: binding _depthHandle makes the GPU honour the stencil
                // test inside the snow material (Stencil { Ref 1 Comp NotEqual }).
                cmd.SetRenderTarget(
                    _colorHandle,
                    RenderBufferLoadAction.Load,    // keep existing colour
                    RenderBufferStoreAction.Store,
                    _depthHandle,
                    RenderBufferLoadAction.Load,    // keep existing depth/stencil
                    RenderBufferStoreAction.DontCare
                );

                // ── Step 4: Draw the fullscreen triangle ──────────────────────────
                // CoreUtils.DrawFullScreen draws a single large triangle that
                // covers the screen, more efficient than a quad.
                CoreUtils.DrawFullScreen(cmd, _settings.snowMaterial, null, shaderPassId: 0);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // Null out the handles — they're owned by the renderer, not this pass.
            _colorHandle = null;
            _depthHandle = null;
        }

        public void Dispose()
        {
            _tempHandle?.Release();
            _tempHandle = null;
        }
    }
}
