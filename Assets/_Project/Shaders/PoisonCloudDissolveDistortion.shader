Shader "DevouringBeast/VFX/PoisonCloudDissolveDistortion"
{
    Properties
    {
        [MainTexture] _BaseMap ("Poison Cloud", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (0.12, 0.42, 0.1, 0.85)
        _NoiseScale ("Noise Scale", Float) = 5
        _NoiseSpeed ("Noise Speed", Float) = 0.25
        _DissolveSoftness ("Dissolve Softness", Range(0.01, 0.4)) = 0.14
        _DistortionStrength ("Distortion Strength", Range(0, 0.05)) = 0.012
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Name "PoisonCloud"
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _NoiseScale;
                float _NoiseSpeed;
                float _DissolveSoftness;
                float _DistortionStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float SmoothNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                local = local * local * (3.0 - 2.0 * local);
                float a = Hash21(cell);
                float b = Hash21(cell + float2(1, 0));
                float c = Hash21(cell + float2(0, 1));
                float d = Hash21(cell + float2(1, 1));
                return lerp(lerp(a, b, local.x), lerp(c, d, local.x), local.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 cloud = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float2 animatedUv = input.uv * _NoiseScale;
                animatedUv += float2(_Time.y * _NoiseSpeed, -_Time.y * _NoiseSpeed * 0.73);
                float noiseA = SmoothNoise(animatedUv);
                float noiseB = SmoothNoise(animatedUv * 1.91 + 7.3);
                float noise = saturate(noiseA * 0.65 + noiseB * 0.35);

                float lifetime = saturate(input.color.a);
                float dissolveThreshold = lerp(0.08, 1.08, 1.0 - lifetime);
                float density = cloud.a * 0.68 + noise * 0.5;
                float dissolveMask = smoothstep(
                    dissolveThreshold - _DissolveSoftness,
                    dissolveThreshold + _DissolveSoftness,
                    density);
                clip(dissolveMask - 0.015);

                float2 screenUv = input.screenPos.xy / max(input.screenPos.w, 0.0001);
                float2 distortion = (float2(noiseA, noiseB) - 0.5) *
                    _DistortionStrength * dissolveMask;
                half3 refractedBackground = SampleSceneColor(screenUv + distortion);

                half3 poisonTint = lerp(_BaseColor.rgb, input.color.rgb, 0.72h);
                half3 poisonColor = cloud.rgb * poisonTint;
                half opacity = saturate(dissolveMask * lifetime * _BaseColor.a);
                half3 finalColor = lerp(refractedBackground, poisonColor, 0.78h);
                return half4(finalColor, opacity);
            }
            ENDHLSL
        }
    }
}
