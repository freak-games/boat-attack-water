using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WaterSystem.Rendering
{
    public class WaterFxPass : ScriptableRenderPass
    {
        private static int m_BufferATexture = Shader.PropertyToID("_WaterBufferA");
        private static int m_BufferBTexture = Shader.PropertyToID("_WaterBufferB");

        private static int
            m_MockDepthTexture = Shader.PropertyToID("_DepthBufferMock"); // TODO remove once bug is fixed

        private RenderTargetIdentifier m_BufferTargetA = new RenderTargetIdentifier(m_BufferATexture);
        private RenderTargetIdentifier m_BufferTargetB = new RenderTargetIdentifier(m_BufferBTexture);


        private const string k_RenderWaterFXTag = "Render Water FX";
        private ProfilingSampler m_WaterFX_Profile = new ProfilingSampler(k_RenderWaterFXTag);
        private readonly ShaderTagId m_WaterFXShaderTag = new ShaderTagId("WaterFX");

        private readonly Color
            m_ClearColor =
                new Color(0.0f, 0.5f, 0.5f, 0.5f); //r = foam mask, g = normal.x, b = normal.z, a = displacement

        private FilteringSettings m_FilteringSettings;
        private RenderTargetHandle m_WaterFX = RenderTargetHandle.CameraTarget;

        public WaterFxPass()
        {
            m_WaterFX.Init("_WaterFXMap");
            m_FilteringSettings = new FilteringSettings(RenderQueueRange.transparent);
            renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            RenderTextureDescriptor rtd = new RenderTextureDescriptor
            {
                depthBufferBits = 0,
                dimension = TextureDimension.Tex2D,
                width = cameraTextureDescriptor.width / 100,
                height = cameraTextureDescriptor.height / 100,
                colorFormat = RenderTextureFormat.Default,
                msaaSamples = 1,
                useMipMap = false,
            };

            cmd.GetTemporaryRT(m_BufferATexture, rtd, FilterMode.Bilinear);
            cmd.GetTemporaryRT(m_BufferBTexture, rtd, FilterMode.Bilinear);
            cmd.GetTemporaryRT(m_MockDepthTexture, rtd, FilterMode.Point);

            RenderTargetIdentifier[] multiTargets = { m_BufferTargetA, m_BufferTargetB };
            ConfigureTarget(multiTargets, m_MockDepthTexture);
            ConfigureClear(ClearFlag.Color, m_ClearColor);

#if UNITY_2021_1_OR_NEWER
            ConfigureDepthStoreAction(RenderBufferStoreAction.DontCare);
#endif
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cam = renderingData.cameraData.camera;
            if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView) return;

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_WaterFX_Profile)) // makes sure we have profiling ability
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // here we choose renderers based off the "WaterFX" shader pass and also sort back to front
                var drawSettings = CreateDrawingSettings(m_WaterFXShaderTag, ref renderingData,
                    SortingCriteria.CommonTransparent);

                // draw all the renderers matching the rules we setup
                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref m_FilteringSettings);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // since the texture is used within the single cameras use we need to cleanup the RT afterwards
            cmd.ReleaseTemporaryRT(m_BufferATexture);
            cmd.ReleaseTemporaryRT(m_BufferBTexture);
            cmd.ReleaseTemporaryRT(m_MockDepthTexture);
        }
    }
}