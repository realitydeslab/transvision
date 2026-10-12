using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

// This is the script template for creating a ScriptableRendererFeature meant for a post-processing effect
//
// To see how this feature is made to work with on a custom VolumeComponent observe the "AddRenderPasses" and "ExecuteMainPass" methods
//
// For a general guide on how to create custom ScriptableRendererFeatures see the following URP documentation page:
// https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/renderer-features/create-custom-renderer-feature.html
//
// Unity 6.3 (URP 17.3) compiles the Compatibility Mode API (Execute, OnCameraSetup) only with URP_COMPATIBILITY_MODE,
// so this feature implements the Render Graph path alone.
public sealed class NewPostProcessEffectRendererFeature : ScriptableRendererFeature
{
    #region FEATURE_FIELDS

    // * The material used to render the post-processing effect
    // * The 'SerializeField' attribute makes sure that the private material reference we assign in the "Create" method
    //   while in the editor will be serialised and the referenced material will be included in the player build
    // * To not clutter the renderer feature UI we're keeping this field hidden, but if you'd like to be able to change
    //   the material in the editor UI you could just make this field public and remove the current attributes
    [SerializeField]
    [HideInInspector]
    private Material m_Material;

    // The user defined ScriptableRenderPass that is responsible for the actual rendering of the effect
    private CustomPostRenderPass m_FullScreenPass;

    #endregion

    #region FEATURE_METHODS

    public override void Create()
    {
#if UNITY_EDITOR
        // * This assigns a material asset reference while in the editor and the "[SerializeField]" attribute on the
        //   private `m_Material` field will make sure that the referenced material will be included in player builds
        // * Alternatively, you could create a material from the shader at runtime e.g.:
        //     'm_Material = new Material(m_Shader);'
        //   In this case for the shader referenced by 'm_Shader' to be included in builds you will have to either:
        //     1) Assign 'm_Shader = Shader.Find("Shader Graphs/FullscreenInvertColors")' behind UNITY_EDITOR only and make sure 'm_Shader' is a "[SerializedField]"
        //     2) Or add "Shader Graphs/FullscreenInvertColors" to the "Always Included Shaders List" under "ProjectSettings"-> "Graphics" -> "Shader Settings"
        //        and call 'm_Shader = Shader.Find("Shader Graphs/FullscreenInvertColors")' outside of the UNITY_EDITOR section
        if (m_Material == null)
        {
            //m_Material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/FullscreenInvertColors.mat");
            m_Material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Packages/org.realitydeslab.transvision/Debug/FullScreenShaderTest.mat");
            
        }
            
#endif

        if(m_Material)
            m_FullScreenPass = new CustomPostRenderPass(name, m_Material);
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // Skip rendering if m_Material or the pass instance are null for whatever reason
        if (m_Material == null || m_FullScreenPass == null)
            return;

        // This check makes sure to not render the effect to reflection probes or preview cameras as post-processing is typically not desired there
        if (renderingData.cameraData.cameraType == CameraType.Preview || renderingData.cameraData.cameraType == CameraType.Reflection)
            return;

        // You can control the rendering of your feature using custom post-processing VolumeComponents
        //
        // E.g. when controlling rendering with a VolumeComponent you will typically want to skip rendering as an optimization when the component
        // has settings which would make it imperceptible (e.g. the implementation of IsActive() might return false when some "intensity" value is 0).
        //
        // N.B. if your volume component type is actually defined in C# it is unlikely that VolumeManager would return a "null" instance of it as
        // GlobalSettings should always contain an instance of all VolumeComponents in the project even if if they're not overriden in the scene
        NewPostProcessEffectVolumeComponent myVolume = VolumeManager.instance.stack?.GetComponent<NewPostProcessEffectVolumeComponent>();
        if (myVolume == null || !myVolume.IsActive())
            return;

        // Here you specify at which part of the frame the effect will execute
        //
        // When creating post-processing effects you will almost always want to use on of the following injection points:
        // BeforeRenderingTransparents - in cases you want your effect to be visible behind transparent objects
        // BeforeRenderingPostProcessing - in cases where your effect is supposed to run before the URP post-processing stack
        // AfterRenderingPostProcessing - in cases where your effect is supposed to run after the URP post-processing stack, but before FXAA, upscaling or color grading
        m_FullScreenPass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

        // You can specify if your effect needs scene depth, normals, motion vectors or a downscaled opaque color as input
        //
        // You specify them as a mask e.g. ScriptableRenderPassInput.Normals | ScriptableRenderPassInput.Motion and URP
        // will either reuse these if they've been generated earlier in the frame or will add passes to generate them.
        //
        // The inputs will get bound as global shader texture properties and can be sampled in the shader using using the following:
        // * Depth  - use "SampleSceneDepth" after including "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
        // * Normal - use "SampleSceneNormals" after including "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
        // * Opaque Scene Color - use "SampleSceneColor" after including "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl".
        //   Note that "OpaqueSceneColor" is a texture containing a possibly downscaled copy of the framebuffer from before rendering transparent objects which
        //   should not be your first choice when wanting to do a post-processing effect, for that this template will copy the active texture for sampling which is more expensive
        // * Motion Vectors - you currently need to declare and sample the texture as follows:
        //     TEXTURE2D_X(_MotionVectorTexture);
        //     ...
        //     LOAD_TEXTURE2D_X_LOD(_MotionVectorTexture, pixelCoords, 0).xy
        //
        // N.B. when using the FullScreenPass Shader Graph target you should simply use the "URP Sample Buffer" node which will handle the above for you
        m_FullScreenPass.ConfigureInput(ScriptableRenderPassInput.None);

        renderer.EnqueuePass(m_FullScreenPass);
    }

    #endregion

    private class CustomPostRenderPass : ScriptableRenderPass
    {
        #region PASS_FIELDS

        // The material used to render the post-processing effect
        private Material m_Material;

        // The property block used to set additional properties for the material
        private static MaterialPropertyBlock s_SharedPropertyBlock = new MaterialPropertyBlock();

        // This constant is meant to showcase how to create a copy color pass that is needed for most post-processing effects
        private static readonly bool kSampleActiveColor = true;

        // This constant is meant to showcase how you can add dept-stencil support to your main pass
        private static readonly bool kBindDepthStencilAttachment = false;

        // Creating some shader properties in advance as this is slightly more efficient than referencing them by string
        private static readonly int kBlitTexturePropertyId = Shader.PropertyToID("_BlitTexture");
        private static readonly int kBlitScaleBiasPropertyId = Shader.PropertyToID("_BlitScaleBias");

        #endregion

        public CustomPostRenderPass(string passName, Material material)
        {
            profilingSampler = new ProfilingSampler(passName);
            m_Material = material;

            // * The 'requiresIntermediateTexture' field needs to be set to 'true' when a ScriptableRenderPass intends to sample
            //   the active color buffer
            // * This will make sure that URP will not apply the optimization of rendering the entire frame to the write-only backbuffer,
            //   but will instead render to intermediate textures that can be sampled, which is typically needed for post-processing
            requiresIntermediateTexture = kSampleActiveColor;
        }

        #region PASS_SHARED_RENDERING_CODE

        // This method contains the rendering logic for the main post-processing pass
        private static void ExecuteMainPass(RasterCommandBuffer cmd, RTHandle sourceTexture, Material material)
        {
            s_SharedPropertyBlock.Clear();
            if(sourceTexture != null)
                s_SharedPropertyBlock.SetTexture(kBlitTexturePropertyId, sourceTexture);

            // This uniform needs to be set for user materials with shaders relying on core Blit.hlsl to work as expected
            s_SharedPropertyBlock.SetVector(kBlitScaleBiasPropertyId, new Vector4(1, 1, 0, 0));

            // USING A CUSTOM VOLUME COMPONENT:
            //
            // To control the rendering of your effect using a custom VolumeComponent you can set the material's properties
            // based on the blended values of your VolumeComponent by querying them with the core VolumeManager API e.g.:
            NewPostProcessEffectVolumeComponent myVolume = VolumeManager.instance.stack?.GetComponent<NewPostProcessEffectVolumeComponent>();
            if (myVolume != null)
                s_SharedPropertyBlock.SetFloat("_Intensity", myVolume.intensity.value);

            cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1, s_SharedPropertyBlock);
        }

        #endregion

        #region PASS_RENDER_GRAPH_PATH

        // The custom main pass data that will be passed at render graph execution to the lambda we set with "SetRenderFunc" during render graph setup
        private class MainPassData
        {
            public Material material;
            public TextureHandle inputTexture;
        }

        private static void ExecuteMainPass(MainPassData data, RasterGraphContext context)
        {
            ExecuteMainPass(context.cmd, data.inputTexture.IsValid() ? data.inputTexture : null, data.material);
        }

        // Here you can implement the rendering logic for the render graph path
        // No copy is needed to sample the active color: cameraColor is swapped to a new texture below. However, this means that every pixel needs to be written in your material, a partial write (eg using stencil) will not be visually correct and requires a copy first.
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourcesData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            // Below is an example of a typical post-processing effect which samples from the current color 
            // Feel free modify/rename/add additional or remove the existing passes based on the needs of your custom post-processing effect

            using (var builder = renderGraph.AddRasterRenderPass<MainPassData>(passName, out var passData, profilingSampler))
            {
                passData.material = m_Material;

                TextureHandle destination;

                // GPU graphics pipelines don't allow to sample the texture bound as the active color target, ie the cameraColor cannot both be an input and the render target.
                // Before, this required us to first copy the cameraColor to then blit back to it while sampling from the copy. Now that we have the ContextContainer, we can swap the cameraColor to 
                // another (temp) resource so that the next pass uses the temp resource. We don't need the copy anymore. However, this only works if you are writing to every 
                // pixel of the frame, a partial write will need the copy first to add to the existing content. See FullScreenPassRendererFeature.cs for an example. 
                if (kSampleActiveColor)
                {
                    var cameraColorDesc = renderGraph.GetTextureDesc(resourcesData.cameraColor);
                    cameraColorDesc.name = "_CameraColorCustomPostProcessing";
                    cameraColorDesc.clearBuffer = false;

                    destination = renderGraph.CreateTexture(cameraColorDesc);
                    passData.inputTexture = resourcesData.cameraColor;

                    //If you use framebuffer fetch in your material then you need to use builder.SetInputAttachment. If the pass can be merged then this will reduce GPU bandwidth usage / power consumption and improve GPU performance. 
                    builder.UseTexture(passData.inputTexture, AccessFlags.Read);
                }
                else
                {
                    destination = resourcesData.cameraColor;
                    passData.inputTexture = TextureHandle.nullHandle;
                }

                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                // This branch is currently not taken, but if your pass needed the depth and/or stencil buffer to be bound this is how you would do it
                if (kBindDepthStencilAttachment)
                    builder.SetRenderAttachmentDepth(resourcesData.activeDepthTexture, AccessFlags.Write);

                builder.SetRenderFunc((MainPassData data, RasterGraphContext context) => ExecuteMainPass(data, context));

                //Swap cameraColor to the new temp resource (destination) for the next pass
                if (kSampleActiveColor)
                {
                    resourcesData.cameraColor = destination;
                }
            }
        }

        #endregion
    }
}
