using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public partial class CameraImageProcessor
{
    /// <summary>
    /// Mono: copies <see cref="m_DistortionTexture"/> straight over the AR camera's picture, full
    /// screen. The result is already laid out for the screen (Step0 used the AR background's display
    /// transform), so it needs no plane in front of the camera, nor that plane's size and placement.
    /// Stereo's eye cameras still show it on the result plane.
    /// </summary>
    sealed class DisplayPass : ScriptableRenderPass
    {
        readonly CameraImageProcessor m_Owner;
        RTHandle m_Result;

        public DisplayPass(CameraImageProcessor owner)
        {
            m_Owner = owner;
            // After the AR camera background and anything 3D; the UI overlay still draws on top.
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var result = m_Owner.m_DistortionTexture;
            if (m_Result == null || m_Result.rt != result)
            {
                m_Result?.Release();
                m_Result = RTHandles.Alloc(result); // wraps the texture; Release never destroys it
            }
            var resourceData = frameData.Get<UniversalResourceData>();
            // In Stereo the picture is the camera's whole image (for the recorder's AR camera render):
            // copy only the part the screen's view covers.
            Vector2 crop = m_Owner.ScreenCrop;
            renderGraph.AddBlitPass(renderGraph.ImportTexture(m_Result), resourceData.activeColorTexture,
                crop, (Vector2.one - crop) * 0.5f, passName: "TransVision Full-Screen Result");
        }

        public void Dispose() => m_Result?.Release();
    }
}
