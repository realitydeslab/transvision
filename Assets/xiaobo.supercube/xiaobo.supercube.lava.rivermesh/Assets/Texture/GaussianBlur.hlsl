void GaussianBlur_float(UnityTexture2D tex, UnityTexture2D controlTex, UnitySamplerState samp, float2 UV, float Blur, float lod, out float3 Out_RGB, out float Out_Alpha)
{
    
    float4 col = float4(0.0, 0.0, 0.0, 0.0);
    float kernelSum = 0.0;
 
    int upper = ((Blur - 1) / 2);
    int lower = -upper;
 
    for (int x = lower; x <= upper; ++x)
    {
        for (int y = lower; y <= upper; ++y)
        {
            kernelSum++; 
 
            float2 offset = float2(tex.texelSize.x * x, tex.texelSize.y * y);
            //col += Texture.Sample(Sampler, UV + offset);
            col += SAMPLE_TEXTURE2D_LOD(tex, tex.samplerstate, UV + offset, lod);
        }
    }
 
    col /= kernelSum;
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