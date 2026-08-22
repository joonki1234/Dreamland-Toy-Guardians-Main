Shader "DreamGuardians/Police Lightning Beam"
{
    Properties
    {
        _LightningTex ("Vefects Lightning Texture", 2D) = "white" {}
        _LUT ("Vefects Blue LUT", 2D) = "white" {}
        _Tint ("Tint", Color) = (0.45, 0.9, 1, 1)
        _Intensity ("Emission Intensity", Range(0, 10)) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "PoliceLightningBeam"
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_LightningTex);
            SAMPLER(sampler_LightningTex);
            TEXTURE2D(_LUT);
            SAMPLER(sampler_LUT);

            CBUFFER_START(UnityPerMaterial)
                float4 _LightningTex_ST;
                float4 _Tint;
                float _Intensity;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _LightningTex);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = saturate(input.uv);
                half maskCenter = SAMPLE_TEXTURE2D(
                    _LightningTex,
                    sampler_LightningTex,
                    uv
                ).g;
                half maskForward = SAMPLE_TEXTURE2D(
                    _LightningTex,
                    sampler_LightningTex,
                    float2(saturate(uv.x + 0.035), uv.y)
                ).g;
                half maskBackward = SAMPLE_TEXTURE2D(
                    _LightningTex,
                    sampler_LightningTex,
                    float2(saturate(uv.x - 0.035), uv.y)
                ).g;
                half mask = saturate(max(maskCenter, max(maskForward, maskBackward)));

                half edge = saturate(1.0 - abs(uv.y * 2.0 - 1.0));
                half baseBeam = pow(edge, 6.0) * 0.24;
                half electricDetail = mask * pow(edge, 1.5);
                half emission = baseBeam + electricDetail * 1.35;

                half3 lutColor = SAMPLE_TEXTURE2D(
                    _LUT,
                    sampler_LUT,
                    float2(mask, mask)
                ).rgb;
                half3 visibleBlue = max(lutColor, half3(0.2, 0.65, 1.0));
                half3 color = visibleBlue * _Tint.rgb * (_Intensity * emission);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
