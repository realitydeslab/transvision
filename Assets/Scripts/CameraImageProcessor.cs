using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using Xiaobo.UnityToolkit.Helper;

public class CameraImageProcessor : MonoBehaviour
{
    [Header("Step0")]
    [SerializeField]
    ARCameraBackground m_ARCameraBackground;

    [SerializeField]
    RenderTexture m_RenderTexture;

    [SerializeField]
    Texture m_TestTexture;

    [Header("Step1")]
    [SerializeField]
    Material m_CalculateMaskMaterial;

    [SerializeField]
    RenderTexture m_MaskTexture;

    [SerializeField]
    Vector3 RGB;

    [SerializeField]
    Vector3 HSL;

    [Header("Step2")]
    [SerializeField]
    Material m_BlurMaterial;
    [SerializeField]
    RenderTexture m_BlurredMaskTexture;

    [Header("Step3")]
    [SerializeField]
    Material m_GenerateOffsetMaterial;
    [SerializeField]
    RenderTexture m_OffsetTexture;

    [Header("Step10")]
    [SerializeField]
    Material m_DistortionMaterial;

    [SerializeField]
    RenderTexture m_DistortionTexture;

    

    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        HelperModule.Instance.SetSlider("Hue Threshold", (float v) => { m_CalculateMaskMaterial.SetFloat("_HueThreshold", v); }, m_CalculateMaskMaterial.GetFloat("_HueThreshold"), 0, 1);
        HelperModule.Instance.SetSlider("Saturate Threshold", (float v) => { m_CalculateMaskMaterial.SetFloat("_SaturateThreshold", v); }, m_CalculateMaskMaterial.GetFloat("_SaturateThreshold"), 0, 1);

        HelperModule.Instance.SetSlider("Blur", (float v) => { m_BlurMaterial.SetFloat("_Blur", v); }, m_BlurMaterial.GetFloat("_Blur"), 0, 40);
        HelperModule.Instance.SetSlider("LOD", (float v)=> { m_BlurMaterial.SetFloat("_LOD", v); }, m_BlurMaterial.GetFloat("_LOD"), 0, 20);

        HelperModule.Instance.SetSlider("Normal Strength", (float v) => { m_GenerateOffsetMaterial.SetFloat("_NormalStrength", v); }, m_GenerateOffsetMaterial.GetFloat("_NormalStrength"), 0, 40);
        HelperModule.Instance.SetSlider("Constant Flow Speed", (float v) => { m_GenerateOffsetMaterial.SetFloat("_ConstantFlowMapSpeed", v); }, m_GenerateOffsetMaterial.GetFloat("_ConstantFlowMapSpeed"), 0, 1);
        HelperModule.Instance.SetSlider("Constant Flow Strength", (float v) => { m_GenerateOffsetMaterial.SetFloat("_ConstantFlowMapStrength", v); }, m_GenerateOffsetMaterial.GetFloat("_ConstantFlowMapStrength"), -1, 1);

        HelperModule.Instance.SetSlider("FlowMap Speed", (float v) => { m_DistortionMaterial.SetFloat("_FlowmapSpeed", v); }, m_DistortionMaterial.GetFloat("_FlowmapSpeed"), 0, 1);
        HelperModule.Instance.SetSlider("FlowMap Strength", (float v) => { m_DistortionMaterial.SetFloat("_FlowmapStrength", v); }, m_DistortionMaterial.GetFloat("_FlowmapStrength"), -1, 1);

        
    }

    // Update is called once per frame
    void Update()
    {
        // Step0 - Read CameraImage To RenderTexture
        ReadCameraImageToRenderTexture();

        // Step1 - Calculate Mask
        m_CalculateMaskMaterial.SetTexture("_CameraImage", m_RenderTexture);
        Graphics.Blit(m_RenderTexture, m_MaskTexture, m_CalculateMaskMaterial, 0);
        HSL = Unity_ColorspaceConversion_RGB_RGB_float(RGB / 255.0f);

        // Step2 - Blur Mask
        m_BlurMaterial.SetTexture("_TextureIn", m_MaskTexture);
        Graphics.Blit(m_MaskTexture, m_BlurredMaskTexture, m_BlurMaterial, 0);

        // Step3 - Offset
        m_GenerateOffsetMaterial.SetTexture("_HeightMap", m_BlurredMaskTexture);
        Graphics.Blit(null, m_OffsetTexture, m_GenerateOffsetMaterial, 0);


        // Step10 - Distortion 
        m_DistortionMaterial.SetTexture("_CameraTexture", m_RenderTexture);
        m_DistortionMaterial.SetTexture("_MaskTexture", m_BlurredMaskTexture);
        Graphics.Blit(m_RenderTexture, m_DistortionTexture, m_DistortionMaterial, 0);

        
    }

    void ReadCameraImageToRenderTexture()
    {
        // Create a new command buffer
        var commandBuffer = new CommandBuffer();
        commandBuffer.name = "AR Camera Background Blit Pass";

        //Debug.Log($"m_ARCameraBackground == null ? {m_ARCameraBackground == null}, m_ARCameraBackground.material == null ? { m_ARCameraBackground.material == null}");

        Texture texture;
        if (m_ARCameraBackground == null || m_ARCameraBackground.material == null)
        {
            texture = m_TestTexture;
        }
        else
        {
            // Get a reference to the AR Camera Background's main texture
            // We will copy this texture into our chosen render texture
            texture = !m_ARCameraBackground.material.HasProperty("_MainTex") ?
                null : m_ARCameraBackground.material.GetTexture("_MainTex");
        }

        // Resize Texture if not match with camera resolution
        //ResizeTexture(m_RenderTexture, texture.width, texture.height);

        // Save references to the active render target before we overwrite it
        var colorBuffer = Graphics.activeColorBuffer;
        var depthBuffer = Graphics.activeDepthBuffer;

        // Set Unity's render target to our render texture
        Graphics.SetRenderTarget(m_RenderTexture);

        // Clear the render target before we render new pixels into it
        commandBuffer.ClearRenderTarget(true, false, Color.clear);

        // Blit the AR Camera Background into the render target
        commandBuffer.Blit(
            texture,
            BuiltinRenderTextureType.CurrentActive,
            m_ARCameraBackground.material);

        // Execute the command buffer
        Graphics.ExecuteCommandBuffer(commandBuffer);

        // Set Unity's render target back to its previous value
        Graphics.SetRenderTarget(colorBuffer, depthBuffer);
    }

    void ResizeTexture(RenderTexture src, int width, int height)
    {
        if(src.width != width || src.height != height)
        {
            Debug.Log($"[{this.GetType().Name}]: Resize from ({src.width},{src.height} to ({ width},{height} ))");
            m_RenderTexture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32);
        }
    }

    Vector3 Unity_ColorspaceConversion_RGB_RGB_float(Vector3 In)
    {
        Vector3 Out;
        Vector4 K = new Vector4(0.0f, -1.0f / 3.0f, 2.0f / 3.0f, -1.0f);
        Vector4 P = Vector4.Lerp(new Vector4(In.z, In.y, K.w, K.z), new Vector4(In.y, In.z, K.x, K.y), In.y >= In.z ? 1 : 0);
        Vector4 Q = Vector4.Lerp(new Vector4(P.x, P.y, P.w, In.x), new Vector4(In.x, P.y, P.z, P.x), In.x >= P.x ? 1 : 0);
        float D = Q.x - Mathf.Min(Q.w, Q.y);
        float E = Mathf.Epsilon;
        Out = new Vector3(Mathf.Abs(Q.z + (Q.w - Q.y) / (6.0f * D + E)), D / (Q.x + E), Q.x);
        return Out;
    }
}
