// Box blur over a Blur x Blur texel window of the full-resolution texture, read from mip `lod`.
// The window is sampled on a grid of at most TRANSVISION_BLUR_TAPS x TRANSVISION_BLUR_TAPS points
// spread over the same extent, not at every texel: at LOD 4 the window covers only about two mip
// texels, so the dense loop (39 x 39 = 1521 reads per pixel for Blur 40) kept averaging the same
// few texels. With the full tap count the sample positions are exactly the dense loop's; 7 x 7
// (49 reads) differs from it only by a sub-pixel shift of the mask's soft edge.
#ifndef TRANSVISION_BLUR_TAPS
#define TRANSVISION_BLUR_TAPS 7
#endif

void GaussianBlur_float(UnityTexture2D tex, UnityTexture2D controlTex, UnitySamplerState samp, float2 UV, float Blur, float lod, out float3 Out_RGB, out float Out_Alpha)
{
    float4 col = float4(0.0, 0.0, 0.0, 0.0);

    int upper = ((Blur - 1) / 2);
    int taps = min(2 * upper + 1, TRANSVISION_BLUR_TAPS);
    float spacing = taps > 1 ? (2.0 * upper) / (taps - 1) : 0.0;

    for (int x = 0; x < taps; ++x)
    {
        for (int y = 0; y < taps; ++y)
        {
            float2 offset = tex.texelSize.xy * (float2(x, y) * spacing - upper);
            col += SAMPLE_TEXTURE2D_LOD(tex, tex.samplerstate, UV + offset, lod);
        }
    }

    col /= taps * taps;
    Out_RGB = float3(col.r, col.g, col.b);
    Out_Alpha = col.a;

    /*
    float2 R = float2(10240, 10240);
    float2 x = UV;
    float Width = Blur;
    
    lod = log2(max(R.x, R.y));
    float4 map = SAMPLE_TEXTURE2D_LOD(controlTex, controlTex.samplerstate, x, 0);
    map = max(map.x, max(map.y, map.z)) * map.a;
    
    map = 1;
    lod = map.x * (Width) * log2(max(R.x, R.y));
    float4 c = 0;
    float2 off = .5 / R * pow(2, lod) * saturate(lod);
    c += SAMPLE_TEXTURE2D_LOD(tex, samp, x + float2(0, 0) * off, lod);
    c += SAMPLE_TEXTURE2D_LOD(tex, samp, x + float2(1, 1) * off, lod);
    c += SAMPLE_TEXTURE2D_LOD(tex, samp, x + float2(1, -1) * off, lod);
    c += SAMPLE_TEXTURE2D_LOD(tex, samp, x + float2(-1, -1) * off, lod);
    c += SAMPLE_TEXTURE2D_LOD(tex, samp, x + float2(-1, 1) * off, lod);
    off *= 1.86;
    c += SAMPLE_TEXTURE2D_LOD(tex, samp, x + float2(0, 1) * off, lod);
    c += SAMPLE_TEXTURE2D_LOD(tex, samp, x + float2(0, -1) * off, lod);
    c += SAMPLE_TEXTURE2D_LOD(tex, samp, x + float2(-1, 0) * off, lod);
    c += SAMPLE_TEXTURE2D_LOD(tex, samp, x + float2(1, 0) * off, lod);
    c /= 9;
    
    Out_RGB = float3(c.r, c.g, c.b);
    Out_Alpha = c.a;*/
}