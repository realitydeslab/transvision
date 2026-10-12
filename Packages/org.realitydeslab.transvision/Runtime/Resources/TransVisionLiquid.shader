// TransVision's liquid effect: red regions bulge (or shrink) and keep moving like liquid (a flowing
// warp, ripples and bubbles); the rest of the picture only has a small, fixed warp. Every texture
// except the input and the output is small: a 240x135 red field and a 320x180 wave height field.
//   Bulge:   UVs pushed along the gradient of the blurred red field.
//   Wobble:  domain warping (Quilez, f(p + h(p))) of a baked noise texture: flowing inside the red,
//            frozen (no time) outside it.
//   Ripples: Hugo Elias' 2D wave equation, next = (left + right + up + down) / 2 - previous, damped,
//            with drops falling inside the red.
//   Bubbles: two layers of baked domes, cross-faded like a two-phase flow map, with a rim and a highlight.
// Textures between passes are globals (_TV*) set by the command buffer in order.
Shader "Hidden/TransVision/Liquid"
{
    Properties { _MainTex ("Input", 2D) = "black" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        sampler2D _TVField; float4 _TVField_TexelSize;   // blurred red field
        sampler2D _TVSrc;   float4 _TVSrc_TexelSize;     // blur source
        sampler2D _TVCur;   float4 _TVCur_TexelSize;     // wave height, this step
        sampler2D _TVPrev;                               // wave height, previous step
        sampler2D _TVNoise;                              // baked RG value noise, tiling
        sampler2D _TVBubble;                             // baked bubble height (r) and derivatives (gb)
        float _TVTime, _TVDir, _TVAspect;
        float _Push, _Ripple, _Bubbles, _Global, _Inside, _Damp, _Inject, _Spec, _Rim;
        struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
        v2f vert(appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }
        float2 Noise(float2 uv) { return tex2D(_TVNoise, uv).rg * 2 - 1; }

        // Redness from YCbCr chroma: hue within about 20 degrees of pure red, with enough chroma.
        float Redness(float3 linearRgb)
        {
            float3 c = LinearToGammaSpace(saturate(linearRgb));
            float y = dot(c, float3(0.299, 0.587, 0.114));
            float2 v = float2((c.b - y) * 0.564, (c.r - y) * 0.713);   // (Cb, Cr) - 0.5
            float chroma = length(v);
            float cosA = dot(v, float2(-0.320, 0.947)) / max(chroma, 1e-4);
            return smoothstep(0.88, 0.97, cosA) * smoothstep(0.10, 0.20, chroma);
        }
        ENDCG

        // 0: red field (render target texel = 4x4 input taps)
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float frag(v2f i) : SV_Target
            {
                float2 cell = abs(ddx(i.uv)) + abs(ddy(i.uv));
                float r = 0;
                for (int x = 0; x < 4; x++) for (int y = 0; y < 4; y++)
                    r += Redness(tex2D(_MainTex, i.uv + (float2(x, y) - 1.5) * cell / 4).rgb);
                return r / 16;
            }
            ENDCG }

        // 1: separable 9-tap blur of _TVSrc along _TVDir (0 = x, 1 = y)
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float frag(v2f i) : SV_Target
            {
                float2 d = (_TVDir < 0.5 ? float2(1, 0) : float2(0, 1)) * _TVSrc_TexelSize.xy * 1.5;
                const float w[5] = { 0.227, 0.195, 0.122, 0.054, 0.016 };
                float s = tex2D(_TVSrc, i.uv).r * w[0];
                for (int k = 1; k < 5; k++) s += (tex2D(_TVSrc, i.uv + d * k).r + tex2D(_TVSrc, i.uv - d * k).r) * w[k];
                return s;
            }
            ENDCG }

        // 2: one wave step, with sparse drops inside the red re-seeded six times a second
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float frag(v2f i) : SV_Target
            {
                float2 t = _TVCur_TexelSize.xy;
                float next = (tex2D(_TVCur, i.uv + float2(t.x, 0)).r + tex2D(_TVCur, i.uv - float2(t.x, 0)).r
                            + tex2D(_TVCur, i.uv + float2(0, t.y)).r + tex2D(_TVCur, i.uv - float2(0, t.y)).r) * 0.5
                            - tex2D(_TVPrev, i.uv).r;
                float n = tex2D(_TVNoise, i.uv * float2(_TVAspect, 1) * 2.5 + floor(_TVTime * 6) * float2(0.37, 0.61)).r;
                float drop = smoothstep(0.86, 0.96, n) * tex2D(_TVField, i.uv).r;
                return next * _Damp + drop * _Inject;
            }
            ENDCG }

        // 3: composite
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag(v2f i) : SV_Target
            {
                float2 asp = float2(_TVAspect, 1);
                float2 tf = _TVField_TexelSize.xy, tw = _TVCur_TexelSize.xy * 2;
                float m = tex2D(_TVField, i.uv).r;
                float2 gm = float2(tex2D(_TVField, i.uv + float2(tf.x, 0)).r - tex2D(_TVField, i.uv - float2(tf.x, 0)).r,
                                   tex2D(_TVField, i.uv + float2(0, tf.y)).r - tex2D(_TVField, i.uv - float2(0, tf.y)).r);
                float2 gh = float2(tex2D(_TVCur, i.uv + float2(tw.x, 0)).r - tex2D(_TVCur, i.uv - float2(tw.x, 0)).r,
                                   tex2D(_TVCur, i.uv + float2(0, tw.y)).r - tex2D(_TVCur, i.uv - float2(0, tw.y)).r) * 0.5;
                float inside = smoothstep(0.05, 0.45, m);
                // Outside the red: a small fixed warp, no time in it, so the picture stays still.
                float2 still = Noise(i.uv * 2.3 + Noise(i.uv * 1.2) * 0.9);
                // Inside the red: a flowing warp, ripples and bubbles, all faded out at the red's edge.
                float2 q = Noise(i.uv * 1.2 + float2(_TVTime * 0.06, -_TVTime * 0.045));
                float2 w = Noise(i.uv * 2.3 + q * 0.9 + float2(-_TVTime * 0.09, _TVTime * 0.075));
                float ph = _TVTime * 0.35;
                float wA = 1 - abs(1 - 2 * frac(ph)), wB = 1 - wA;
                float4 bA = tex2D(_TVBubble, i.uv * asp * 1.6 + floor(ph) * float2(0.23, 0.41) + float2(0, -_TVTime * 0.05));
                float4 bB = tex2D(_TVBubble, i.uv * asp * 1.6 + floor(ph + 0.5) * float2(0.57, 0.19) + float2(0.5, 0.5 - _TVTime * 0.04));
                float2 gb = ((bA.gb * 2 - 1) * wA + (bB.gb * 2 - 1) * wB) * inside;
                gh *= inside;
                float2 offset = gm * _Push + still * _Global + (w * _Inside + gh * _Ripple + gb * _Bubbles) * inside;
                float4 c = tex2D(_MainTex, i.uv + offset / asp);
                c.rgb *= 1 - saturate(length(gb)) * _Rim;
                float3 n = normalize(float3(-(gh * _Ripple * 40 + gb * _Bubbles * 25), 1));
                float spec = pow(saturate(dot(n, normalize(float3(0.35, 0.45, 1)))), 60) * _Spec;
                return float4(c.rgb + spec * inside, 1);
            }
            ENDCG }
    }
}
