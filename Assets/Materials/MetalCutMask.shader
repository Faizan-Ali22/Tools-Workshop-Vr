// Written for URP (the default pipeline for new Unity 6 projects).
// If you're on the Built-in Render Pipeline instead, tell me and I'll rewrite this
// as a CG-based Surface Shader — the two pipelines aren't compatible.
Shader "Custom/MetalCutMask"
{
    Properties
    {
        _BaseMap ("Base Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (0.6, 0.6, 0.65, 1)
        _CutMask ("Cut Mask (set at runtime by MetalSheet.cs)", 2D) = "white" {}
        _EdgeColor ("Edge Scorch Color", Color) = (0.05, 0.05, 0.05, 1)
        _EdgeWidth ("Edge Width (in texel-widths, not 0-1 mask value)", Range(0.0, 4.0)) = 1.25
        _CutoffThreshold ("Cutoff Threshold", Range(0.0, 1.0)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_CutMask); SAMPLER(sampler_CutMask);
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _EdgeColor;
            half _EdgeWidth;
            half _CutoffThreshold;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half mask = SAMPLE_TEXTURE2D(_CutMask, sampler_CutMask, IN.uv).r;

                // Discards the pixel entirely where the blade has cut through -> visible slit.
                clip(mask - _CutoffThreshold + 0.001);

                half4 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                // Edge band measured in screen-space texel derivatives rather than a fixed
                // slice of the 0-1 mask value -> stays a crisp 1-2 pixel line at any mask
                // resolution or brush size, instead of a wide smudgy gradient.
                half aa = max(fwidth(mask), 0.0001);
                half edgeFactor = 1.0 - smoothstep(_CutoffThreshold, _CutoffThreshold + aa * _EdgeWidth, mask);
                half3 finalColor = lerp(baseCol.rgb, _EdgeColor.rgb, edgeFactor);

                Light mainLight = GetMainLight();
                half ndotl = saturate(dot(normalize(IN.normalWS), mainLight.direction));
                finalColor *= (ndotl * 0.5 + 0.5) * mainLight.color;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}
