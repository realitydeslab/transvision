using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// The red-expansion effect: camera image, red mask, blurred mask, offset map, distortion, written
/// to <see cref="m_DistortionTexture"/> for the result plane. The steps run inside URP as one
/// Render Graph pass (<see cref="EffectPass"/>), once per frame.
/// </summary>
public partial class CameraImageProcessor : MonoBehaviour
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

    static readonly int s_UnityDisplayTransform = Shader.PropertyToID("_UnityDisplayTransform");
    static readonly int s_DisplayTransform = Shader.PropertyToID("_TransVisionDisplayTransform");

    EffectPass m_Pass;
    DisplayPass m_DisplayPass;
    int m_LastRenderedFrame = -1;
    ARCameraManager m_CameraManager;
    Material m_CameraImageMaterial;
    Matrix4x4? m_FrameDisplayMatrix;
    bool m_HasCameraFrame;

    void OnEnable()
    {
        m_Pass ??= new EffectPass(this);
        m_DisplayPass ??= new DisplayPass(this);
        if (m_CameraImageMaterial == null)
            m_CameraImageMaterial = new Material(Resources.Load<Shader>("TransVisionCameraImage"));
        // Frames arrive in Stereo too, where the HoloKit SDK turns the AR camera background off.
        m_CameraManager = m_ARCameraBackground != null ? m_ARCameraBackground.GetComponent<ARCameraManager>() : null;
        if (m_CameraManager != null)
            m_CameraManager.frameReceived += OnCameraFrameReceived;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    void OnDisable()
    {
        if (m_CameraManager != null)
            m_CameraManager.frameReceived -= OnCameraFrameReceived;
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    void OnDestroy()
    {
        m_Pass?.Dispose();
        m_DisplayPass?.Dispose();
        m_Liquid?.Dispose();
        m_Liquid = null;
        if (m_CameraImageMaterial != null)
            CoreUtils.Destroy(m_CameraImageMaterial);
    }

    void OnCameraFrameReceived(ARCameraFrameEventArgs args)
    {
        for (int i = 0; i < args.textures.Count; i++)
            m_CameraImageMaterial.SetTexture(args.propertyNameIds[i], args.textures[i]);
        m_FrameDisplayMatrix = args.displayMatrix;
        m_HasCameraFrame = args.textures.Count > 0;
    }

    /// <summary>
    /// Takes the display transform from the AR background material when it has one, just before
    /// rendering. On iOS 27 the HoloKit SDK replaces that material and corrects its transform every
    /// frame after AR Foundation wrote the frame's own (wrong) one into it, so it must be read after
    /// the correction, not when the frame arrives. Without one, the frame's transform.
    /// </summary>
    void UpdateDisplayTransform()
    {
        var background = m_ARCameraBackground != null ? m_ARCameraBackground.material : null;
        // HasMatrix, not HasProperty: the matrix is not in the shader's Properties block. Reading one the
        // material lacks logs an error, so check first.
        var matrix = background != null && background.HasMatrix(s_UnityDisplayTransform)
            ? background.GetMatrix(s_UnityDisplayTransform) : Matrix4x4.zero;
        if (matrix == Matrix4x4.zero || matrix == Matrix4x4.identity)
            matrix = m_FrameDisplayMatrix ?? Matrix4x4.identity;
        if (WholeCameraImage)
        {
            matrix = WholeImageTransform(matrix, out var crop);
            ScreenCrop = crop;
        }
        else
            ScreenCrop = Vector2.one;
        m_CameraImageMaterial.SetMatrix(s_DisplayTransform, matrix);
    }

    /// <summary>
    /// Stereo: the picture is the camera's whole image, not the part the screen shows. The screen
    /// keeps the sensor's full width but only 61.5% of its height (2.17:1 of a 4:3 sensor, 34.5 of
    /// 53.6 degrees), less than HoloKit X's eyes see (41 degrees).
    /// </summary>
    public bool WholeCameraImage { get; set; }

    /// <summary>
    /// The part of the picture the screen's view covers, per screen axis: (1, 1) when the picture is
    /// the screen's view; with <see cref="WholeCameraImage"/>, (1, 0.615) on iPhone 15 Pro. The
    /// screen's view is picture uv = screen uv * ScreenCrop + (1 - ScreenCrop) / 2.
    /// </summary>
    public Vector2 ScreenCrop { get; private set; } = Vector2.one;

    /// <summary>
    /// The display transform for the camera's whole image: <paramref name="screen"/> (screen uv to
    /// camera image uv, row vector as ARKit's shader) without its crop, same orientation and centre.
    /// <paramref name="crop"/> is the length of each screen axis in the image, the crop removed.
    /// </summary>
    public static Matrix4x4 WholeImageTransform(Matrix4x4 screen, out Vector2 crop)
    {
        crop = new Vector2(new Vector2(screen.m00, screen.m01).magnitude, new Vector2(screen.m10, screen.m11).magnitude);
        var whole = screen;
        whole.m00 /= crop.x;
        whole.m01 /= crop.x;
        whole.m10 /= crop.y;
        whole.m11 /= crop.y;
        Vector2 centre = ImageUv(screen, new Vector2(0.5f, 0.5f)) - ImageUv(whole, new Vector2(0.5f, 0.5f));
        whole.m20 += centre.x;
        whole.m21 += centre.y;
        return whole;
    }

    /// Camera image uv for screen uv <paramref name="uv"/>, as the shaders compute it.
    public static Vector2 ImageUv(Matrix4x4 display, Vector2 uv) =>
        new(uv.x * display.m00 + uv.y * display.m10 + display.m20 + display.m30,
            uv.x * display.m01 + uv.y * display.m11 + display.m21 + display.m31);

    void Update()
    {
        HSL = Utilities.RgbToHsv(RGB / 255.0f);
    }

    // The first game camera of each frame runs the effect, so Stereo's two eyes do not compute it twice.
    // The Mono (AR) camera then draws the result full screen; Stereo's eyes show it on the result plane.
    void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera.cameraType != CameraType.Game)
            return;
        if (!camera.TryGetComponent<UniversalAdditionalCameraData>(out var cameraData) || cameraData.scriptableRenderer == null)
            return;
        if (Time.frameCount != m_LastRenderedFrame)
        {
            m_LastRenderedFrame = Time.frameCount;
            UpdateDisplayTransform();
            cameraData.scriptableRenderer.EnqueuePass(m_Pass);
        }
        if (m_ARCameraBackground != null && camera.gameObject == m_ARCameraBackground.gameObject)
            cameraData.scriptableRenderer.EnqueuePass(m_DisplayPass);
    }
}
