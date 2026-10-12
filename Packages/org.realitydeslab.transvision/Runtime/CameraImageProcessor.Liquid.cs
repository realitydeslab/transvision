using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraImageProcessor
{
    public enum Effect
    {
        /// <summary>Red flows like liquid, with ripples and bubbles; the rest keeps only a small still warp.</summary>
        Liquid,
        /// <summary>The original four Shader Graph steps.</summary>
        Classic,
    }

    [Header("Effect")]
    [SerializeField]
    Effect m_Effect = Effect.Liquid;

    [Tooltip("Liquid: red shrinks (placebo) instead of spreading (nocebo).")]
    [SerializeField]
    bool m_Shrink;

    [Tooltip("Liquid: how far red spreads (or shrinks).")]
    [SerializeField, Range(0f, 1f)]
    float m_Bulge = 0.35f;

    [Tooltip("Liquid: refraction of the ripples the red throws up.")]
    [SerializeField, Range(0f, 0.1f)]
    float m_Ripples = 0.03f;

    [Tooltip("Liquid: refraction of the bubbles inside the red.")]
    [SerializeField, Range(0f, 0.1f)]
    float m_Bubbles = 0.03f;

    [Tooltip("Liquid: small fixed (still) warp outside the red.")]
    [SerializeField, Range(0f, 0.03f)]
    float m_Wobble = 0.003f;

    [Tooltip("Liquid: flowing (moving) warp inside the red.")]
    [SerializeField, Range(0f, 0.05f)]
    float m_RedWobble = 0.02f;

    public Effect CurrentEffect { get => m_Effect; set => m_Effect = value; }
    public bool Shrink { get => m_Shrink; set => m_Shrink = value; }

    LiquidState m_Liquid;

    /// <summary>The liquid effect's material and its small render textures; the wave field persists across frames.</summary>
    sealed class LiquidState : System.IDisposable
    {
        public const int FieldWidth = 240, FieldHeight = 135, WaveWidth = 320, WaveHeight = 180;

        public readonly Material Material;
        public readonly RenderTexture Field, Temp, Blurred;
        public readonly RenderTexture[] Waves;
        /// Which of <see cref="Waves"/> holds the previous, current and next wave step.
        public int Previous { get; private set; }
        public int Current { get; private set; } = 1;
        public int Next { get; private set; } = 2;

        public LiquidState()
        {
            Material = new Material(Resources.Load<Shader>("TransVisionLiquid")) { hideFlags = HideFlags.DontSave };
            Material.SetTexture("_TVNoise", LiquidTextures.Noise);
            Material.SetTexture("_TVBubble", LiquidTextures.Bubbles);
            Field = Small("TransVision Red Field", FieldWidth, FieldHeight);
            Temp = Small("TransVision Red Field Blur", FieldWidth, FieldHeight);
            Blurred = Small("TransVision Red Field Blurred", FieldWidth, FieldHeight);
            Waves = new[] { Small("TransVision Waves A", WaveWidth, WaveHeight), Small("TransVision Waves B", WaveWidth, WaveHeight),
                            Small("TransVision Waves C", WaveWidth, WaveHeight) };
        }

        /// The step just recorded becomes the current one.
        public void Advance() => (Previous, Current, Next) = (Current, Next, Previous);

        static RenderTexture Small(string name, int width, int height)
        {
            var texture = new RenderTexture(width, height, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            texture.Create();
            return texture;
        }

        public void Dispose()
        {
            foreach (var texture in new[] { Field, Temp, Blurred, Waves[0], Waves[1], Waves[2] })
            {
                texture.Release();
                CoreUtils.Destroy(texture);
            }
            CoreUtils.Destroy(Material);
        }
    }

    void ApplyLiquidSettings(Material material)
    {
        material.SetFloat("_Push", m_Shrink ? -m_Bulge : m_Bulge);
        material.SetFloat("_Ripple", m_Ripples);
        material.SetFloat("_Bubbles", m_Bubbles);
        material.SetFloat("_Global", m_Wobble);
        material.SetFloat("_Inside", m_RedWobble);
        material.SetFloat("_Damp", 0.975f);
        material.SetFloat("_Inject", 0.35f);
        material.SetFloat("_Spec", 0.22f);
        material.SetFloat("_Rim", 0.35f);
    }
}
