using UnityEngine;

/// <summary>
/// The liquid effect's two baked textures, made once at runtime: tiling value noise (wobble) and
/// bubble domes over a jittered Voronoi (height and its derivatives, so one fetch gives a slope).
/// </summary>
public static class LiquidTextures
{
    public const int Size = 256;

    static Texture2D s_Noise, s_Bubbles;

    public static Texture2D Noise => s_Noise != null ? s_Noise : s_Noise = MakeNoise();
    public static Texture2D Bubbles => s_Bubbles != null ? s_Bubbles : s_Bubbles = MakeBubbles();

    // Two channels of two-octave value noise on a 16x16 lattice that wraps, so the texture tiles.
    static Texture2D MakeNoise()
    {
        const int lattice = 16;
        var rng = new System.Random(7);
        var values = new float[2, lattice, lattice];
        for (int c = 0; c < 2; c++)
            for (int x = 0; x < lattice; x++)
                for (int y = 0; y < lattice; y++)
                    values[c, x, y] = (float)rng.NextDouble();

        float Sample(int c, float u, float v, int octave)
        {
            int period = lattice / octave;
            float fx = u * period, fy = v * period;
            int x0 = (int)fx, y0 = (int)fy;
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3 - 2 * tx);
            ty = ty * ty * (3 - 2 * ty);
            int x1 = (x0 + 1) % period, y1 = (y0 + 1) % period;
            float a = values[c, x0 * octave % lattice, y0 * octave % lattice], b = values[c, x1 * octave % lattice, y0 * octave % lattice];
            float d = values[c, x0 * octave % lattice, y1 * octave % lattice], e = values[c, x1 * octave % lattice, y1 * octave % lattice];
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(d, e, tx), ty);
        }

        var pixels = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float u = x / (float)Size, v = y / (float)Size;
                pixels[y * Size + x] = new Color(Sample(0, u, v, 1) * 0.65f + Sample(0, u, v, 2) * 0.35f,
                                                 Sample(1, u, v, 1) * 0.65f + Sample(1, u, v, 2) * 0.35f, 0, 1);
            }
        return Make("TransVision Liquid Noise", TextureFormat.RGBA32, pixels);
    }

    // Smooth domes of radius 15 texels around 8x8 jittered points on a torus.
    static Texture2D MakeBubbles()
    {
        const int grid = 8;
        const float radius = 15f;
        var rng = new System.Random(11);
        var points = new Vector2[grid * grid];
        for (int i = 0; i < grid; i++)
            for (int j = 0; j < grid; j++)
                points[i * grid + j] = new Vector2((i + 0.2f + 0.6f * (float)rng.NextDouble()) * Size / grid,
                                                   (j + 0.2f + 0.6f * (float)rng.NextDouble()) * Size / grid);
        var height = new float[Size, Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float best = float.MaxValue;
                foreach (var p in points)
                {
                    float dx = Mathf.Abs(x - p.x), dy = Mathf.Abs(y - p.y);
                    dx = Mathf.Min(dx, Size - dx);
                    dy = Mathf.Min(dy, Size - dy);
                    best = Mathf.Min(best, dx * dx + dy * dy);
                }
                float t = Mathf.Clamp01(1 - best / (radius * radius));
                height[x, y] = t * t;
            }

        var dxs = new float[Size, Size];
        var dys = new float[Size, Size];
        float largest = 1e-4f;
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                dxs[x, y] = (height[(x + 1) % Size, y] - height[(x + Size - 1) % Size, y]) * 0.5f;
                dys[x, y] = (height[x, (y + 1) % Size] - height[x, (y + Size - 1) % Size]) * 0.5f;
                largest = Mathf.Max(largest, Mathf.Abs(dxs[x, y]), Mathf.Abs(dys[x, y]));
            }
        var pixels = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                pixels[y * Size + x] = new Color(height[x, y], dxs[x, y] / largest * 0.5f + 0.5f, dys[x, y] / largest * 0.5f + 0.5f, 1);
        return Make("TransVision Liquid Bubbles", TextureFormat.RGBAHalf, pixels);
    }

    static Texture2D Make(string name, TextureFormat format, Color[] pixels)
    {
        var texture = new Texture2D(Size, Size, format, false, linear: true)
        {
            name = name,
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave,
        };
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }
}
