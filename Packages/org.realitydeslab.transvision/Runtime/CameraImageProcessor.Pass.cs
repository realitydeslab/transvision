using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public partial class CameraImageProcessor
{
    /// <summary>
    /// The effect as one URP Render Graph pass. Step0 turns the camera frame (or the still) into
    /// <see cref="m_RenderTexture"/>; then either the liquid effect (Hidden/TransVision/Liquid: red
    /// field, blur, one wave step, composite) or the classic four Shader Graph Blits write
    /// <see cref="m_DistortionTexture"/>, which the result plane's material samples outside the graph.
    /// </summary>
    sealed class EffectPass : ScriptableRenderPass
    {
        static readonly int s_CameraImage = Shader.PropertyToID("_CameraImage");
        static readonly int s_TextureIn = Shader.PropertyToID("_TextureIn");
        static readonly int s_HeightMap = Shader.PropertyToID("_HeightMap");
        static readonly int s_CameraTexture = Shader.PropertyToID("_CameraTexture");
        static readonly int s_MaskTexture = Shader.PropertyToID("_MaskTexture");
        static readonly int s_TVSrc = Shader.PropertyToID("_TVSrc");
        static readonly int s_TVSrcTexelSize = Shader.PropertyToID("_TVSrc_TexelSize");
        static readonly int s_TVField = Shader.PropertyToID("_TVField");
        static readonly int s_TVFieldTexelSize = Shader.PropertyToID("_TVField_TexelSize");
        static readonly int s_TVCur = Shader.PropertyToID("_TVCur");
        static readonly int s_TVCurTexelSize = Shader.PropertyToID("_TVCur_TexelSize");
        static readonly int s_TVPrev = Shader.PropertyToID("_TVPrev");
        static readonly int s_TVDir = Shader.PropertyToID("_TVDir");
        static readonly int s_TVTime = Shader.PropertyToID("_TVTime");
        static readonly int s_TVAspect = Shader.PropertyToID("_TVAspect");

        static readonly Vector4 s_FieldTexelSize = TexelSize(LiquidState.FieldWidth, LiquidState.FieldHeight);
        static readonly Vector4 s_WaveTexelSize = TexelSize(LiquidState.WaveWidth, LiquidState.WaveHeight);

        readonly CameraImageProcessor m_Owner;
        readonly RTHandle[] m_Handles = new RTHandle[11];

        class PassData
        {
            public Texture Source;
            public Material Background, Mask, Blur, Offset, Distortion;
            public TextureHandle CameraImage, MaskTexture, BlurredMask, OffsetTexture, Result;
            public bool Liquid;
            public Material LiquidMaterial;
            public TextureHandle Field, Temp, Blurred, WavePrevious, WaveCurrent, WaveNext;
            public float Time, Aspect;
        }

        public EffectPass(CameraImageProcessor owner)
        {
            m_Owner = owner;
            renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
            profilingSampler = new ProfilingSampler("TransVision Camera Image Effect");
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var o = m_Owner;
            using var builder = renderGraph.AddUnsafePass<PassData>(passName, out var data, profilingSampler);

            data.CameraImage = Import(renderGraph, 0, o.m_RenderTexture);
            data.Result = Import(renderGraph, 4, o.m_DistortionTexture);
            builder.UseTexture(data.CameraImage, AccessFlags.ReadWrite);
            builder.UseTexture(data.Result, AccessFlags.Write);

            // Step0's source: the latest ARKit camera frame, or the still image before the first one.
            data.Background = o.m_HasCameraFrame ? o.m_CameraImageMaterial : null;
            data.Source = o.m_HasCameraFrame ? null : o.m_TestTexture;

            data.Liquid = o.m_Effect == Effect.Liquid;
            if (data.Liquid)
                RecordLiquid(renderGraph, builder, data);
            else
                RecordClassic(renderGraph, builder, data);

            // The only consumer is the plane's material, which the graph cannot see.
            builder.AllowPassCulling(false);
            builder.SetRenderFunc(static (PassData d, UnsafeGraphContext context) =>
                Execute(d, CommandBufferHelpers.GetNativeCommandBuffer(context.cmd)));
        }

        void RecordClassic(RenderGraph renderGraph, IUnsafeRenderGraphBuilder builder, PassData data)
        {
            var o = m_Owner;
            data.MaskTexture = Import(renderGraph, 1, o.m_MaskTexture);
            data.BlurredMask = Import(renderGraph, 2, o.m_BlurredMaskTexture);
            data.OffsetTexture = Import(renderGraph, 3, o.m_OffsetTexture);
            builder.UseTexture(data.MaskTexture, AccessFlags.ReadWrite);
            builder.UseTexture(data.BlurredMask, AccessFlags.ReadWrite);
            builder.UseTexture(data.OffsetTexture, AccessFlags.ReadWrite);

            // Material inputs, bound to the same render textures as before.
            data.Mask = o.m_CalculateMaskMaterial;
            data.Blur = o.m_BlurMaterial;
            data.Offset = o.m_GenerateOffsetMaterial;
            data.Distortion = o.m_DistortionMaterial;
            data.Mask.SetTexture(s_CameraImage, o.m_RenderTexture);
            data.Blur.SetTexture(s_TextureIn, o.m_MaskTexture);
            data.Offset.SetTexture(s_HeightMap, o.m_BlurredMaskTexture);
            data.Distortion.SetTexture(s_CameraTexture, o.m_RenderTexture);
            data.Distortion.SetTexture(s_MaskTexture, o.m_BlurredMaskTexture);
        }

        void RecordLiquid(RenderGraph renderGraph, IUnsafeRenderGraphBuilder builder, PassData data)
        {
            var o = m_Owner;
            o.m_Liquid ??= new LiquidState();
            var liquid = o.m_Liquid;
            o.ApplyLiquidSettings(liquid.Material);
            data.LiquidMaterial = liquid.Material;
            data.Field = Import(renderGraph, 5, liquid.Field);
            data.Temp = Import(renderGraph, 6, liquid.Temp);
            data.Blurred = Import(renderGraph, 7, liquid.Blurred);
            // The three wave textures keep their slots; which is previous, current and next rotates.
            var waves = new[] { Import(renderGraph, 8, liquid.Waves[0]), Import(renderGraph, 9, liquid.Waves[1]), Import(renderGraph, 10, liquid.Waves[2]) };
            data.WavePrevious = waves[liquid.Previous];
            data.WaveCurrent = waves[liquid.Current];
            data.WaveNext = waves[liquid.Next];
            builder.UseTexture(data.Field, AccessFlags.ReadWrite);
            builder.UseTexture(data.Temp, AccessFlags.ReadWrite);
            builder.UseTexture(data.Blurred, AccessFlags.ReadWrite);
            foreach (var wave in waves)
                builder.UseTexture(wave, AccessFlags.ReadWrite);
            data.Time = Time.time;
            data.Aspect = o.m_DistortionTexture.width / (float)o.m_DistortionTexture.height;
            liquid.Advance(); // this frame's next step is the next frame's current one
        }

        static void Execute(PassData d, CommandBuffer cmd)
        {
            // Step0 - camera image (ARKit's Y and CbCr planes to RGB)
            if (d.Background != null)
                cmd.Blit(d.Source, d.CameraImage, d.Background);
            else
                cmd.Blit(d.Source, d.CameraImage);

            if (d.Liquid)
            {
                ExecuteLiquid(d, cmd);
                return;
            }

            // Step1 - red mask, Step2 - blurred mask, Step3 - offset map, Step10 - distortion
            cmd.Blit(d.CameraImage, d.MaskTexture, d.Mask, 0);
            cmd.Blit(d.MaskTexture, d.BlurredMask, d.Blur, 0);
            cmd.Blit(d.BlurredMask, d.OffsetTexture, d.Offset, 0);
            cmd.Blit(d.CameraImage, d.Result, d.Distortion, 0);
        }

        // The liquid passes read each other's output through globals set in order (one material).
        static void ExecuteLiquid(PassData d, CommandBuffer cmd)
        {
            var m = d.LiquidMaterial;
            cmd.SetGlobalFloat(s_TVTime, d.Time);
            cmd.SetGlobalFloat(s_TVAspect, d.Aspect);

            cmd.Blit(d.CameraImage, d.Field, m, 0);
            cmd.SetGlobalVector(s_TVSrcTexelSize, s_FieldTexelSize);
            cmd.SetGlobalTexture(s_TVSrc, d.Field);
            cmd.SetGlobalFloat(s_TVDir, 0);
            cmd.Blit(d.CameraImage, d.Temp, m, 1);
            cmd.SetGlobalTexture(s_TVSrc, d.Temp);
            cmd.SetGlobalFloat(s_TVDir, 1);
            cmd.Blit(d.CameraImage, d.Blurred, m, 1);
            cmd.SetGlobalTexture(s_TVField, d.Blurred);
            cmd.SetGlobalVector(s_TVFieldTexelSize, s_FieldTexelSize);

            cmd.SetGlobalVector(s_TVCurTexelSize, s_WaveTexelSize);
            cmd.SetGlobalTexture(s_TVCur, d.WaveCurrent);
            cmd.SetGlobalTexture(s_TVPrev, d.WavePrevious);
            cmd.Blit(d.CameraImage, d.WaveNext, m, 2);

            cmd.SetGlobalTexture(s_TVCur, d.WaveNext);
            cmd.Blit(d.CameraImage, d.Result, m, 3);
        }

        static Vector4 TexelSize(int width, int height) => new(1f / width, 1f / height, width, height);

        TextureHandle Import(RenderGraph renderGraph, int slot, RenderTexture texture)
        {
            if (m_Handles[slot] == null || m_Handles[slot].rt != texture)
            {
                m_Handles[slot]?.Release();
                m_Handles[slot] = RTHandles.Alloc(texture); // wraps the texture; Release never destroys it
            }
            return renderGraph.ImportTexture(m_Handles[slot]);
        }

        public void Dispose()
        {
            foreach (var handle in m_Handles)
                handle?.Release();
        }
    }
}
