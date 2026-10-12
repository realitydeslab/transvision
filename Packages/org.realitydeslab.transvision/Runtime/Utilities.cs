using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Utilities
{
    public static float Remap(float v, float src_min, float src_max, float dst_min, float dst_max, bool need_clamp = false)
    {
        if (need_clamp)
            v = Mathf.Clamp(v, src_min, src_max);

        if (src_min == src_max)
            v = 0;
        else
            v = (v - src_min) / (src_max - src_min);

        return v * (dst_max - dst_min) + dst_min;
    }

    /// <summary>
    /// RGB to HSV, each component in [0, 1]. Same code as Shader Graph's Colorspace Conversion
    /// node (RGB to HSV), so the value matches what the mask shader computes.
    /// </summary>
    public static Vector3 RgbToHsv(Vector3 In)
    {
        Vector4 K = new Vector4(0.0f, -1.0f / 3.0f, 2.0f / 3.0f, -1.0f);
        Vector4 P = Vector4.Lerp(new Vector4(In.z, In.y, K.w, K.z), new Vector4(In.y, In.z, K.x, K.y), In.y >= In.z ? 1 : 0);
        Vector4 Q = Vector4.Lerp(new Vector4(P.x, P.y, P.w, In.x), new Vector4(In.x, P.y, P.z, P.x), In.x >= P.x ? 1 : 0);
        float D = Q.x - Mathf.Min(Q.w, Q.y);
        float E = Mathf.Epsilon;
        return new Vector3(Mathf.Abs(Q.z + (Q.w - Q.y) / (6.0f * D + E)), D / (Q.x + E), Q.x);
    }

}
