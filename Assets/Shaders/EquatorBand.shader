Shader "Custom/EquatorBand"
{
    Properties
    {
        _colorLerpCutoff ("Color Lerp Cutoff", Range(0.001, 1)) = 0.5
        _fullCutoff ("Full Coverage Cutoff", Range(0.001, 1)) = 1
        _fadePower ("Fade Power", Range(1, 15)) = 3
        _solidRatio ("Solid Ratio", Range(0, 1)) = 0.2
        _Alpha ("Alpha", Range(0, 1)) = 1
        _Intensity ("Intensity", Range(0, 1)) = 0.3
        [HDR] _StartColor ("Start Color", Color) = (0, 0, 0, 1)
        [HDR] _EndColor ("End Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "Unlit"
            Tags
            {
                "LightMode"="UniversalForward"
            }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _StartColor;
                half4 _EndColor;
                half _Alpha;
                half _Intensity;
                half _colorLerpCutoff;
                half _fullCutoff;
                half _solidRatio;
                half _fadePower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dirOS : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.dirOS = IN.positionOS.xyz;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 0 = bottom pole, 0.5 = equator, 1 = top pole (linear in height)
                float v = normalize(IN.dirOS).y * 0.5 + 0.5;
                float dist = abs(v - 0.5);

                // Fraction of the object's height the band covers: 0 at _Intensity = 0, 1 at _Intensity = _fullCutoff.
                float coverage = saturate(_Intensity / _fullCutoff);

                // Band spans coverage in total; the middle _solidRatio of it is solid, the rest fades out on both sides.
                float halfTotal = coverage * 0.5;
                float halfSolid = halfTotal * _solidRatio;
                float mask = 1.0 - smoothstep(halfSolid, max(halfTotal, halfSolid + 1e-5), dist);
                mask = pow(mask, _fadePower);
                mask *= step(1e-4, _Intensity);

                // 0 at _Intensity = 0, reaches 1 at _Intensity = _colorLerpCutoff, then holds until 1.
                half t = saturate(_Intensity / _colorLerpCutoff);
                half4 col = lerp(_StartColor, _EndColor, t);

                return half4(col.rgb * mask, col.a * _Alpha * mask);
            }
            ENDHLSL
        }
    }
}