// The ARKit camera image (Y and CbCr planes) as RGB, for Blit. Same UV remap and colour math as AR
// Foundation's Unlit/ARKitBackground, whose shader draws nothing when used with Blit (AR Foundation
// draws it with its own mesh and matrices).
Shader "Hidden/TransVision/CameraImage"
{
    Properties
    {
        _textureY ("TextureY", 2D) = "black" {}
        _textureCbCr ("TextureCbCr", 2D) = "gray" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _textureY;
            sampler2D _textureCbCr;
            // Image UV from screen UV, multiplied as a row vector (ARKit background convention).
            float4x4 _TransVisionDisplayTransform;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = mul(float4(v.uv, 1.0, 1.0), _TransVisionDisplayTransform).xy;
                return o;
            }

            static const half4x4 s_YCbCrToSRGB = half4x4(
                half4(1.0h,  0.0000h,  1.4020h, -0.7010h),
                half4(1.0h, -0.3441h, -0.7141h,  0.5291h),
                half4(1.0h,  1.7720h,  0.0000h, -0.8860h),
                half4(0.0h,  0.0000h,  0.0000h,  1.0000h));

            half4 frag(v2f i) : SV_Target
            {
                half4 ycbcr = half4(tex2D(_textureY, i.uv).r, tex2D(_textureCbCr, i.uv).rg, 1.0h);
                half3 color = mul(s_YCbCrToSRGB, ycbcr).rgb;
            #if !defined(UNITY_COLORSPACE_GAMMA)
                color = GammaToLinearSpace(color);
            #endif
                return half4(color, 1.0h);
            }
            ENDCG
        }
    }
}
