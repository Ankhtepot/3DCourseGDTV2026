Shader "Custom/EquatorBand"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 1
        _Intensity ("Intensity", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Alpha;
                half _Intensity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
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

                // Solid strip is _Intensity tall in total, fade adds _Intensity on each side.
                float halfSolid = _Intensity * 0.5;
                float fade = max(_Intensity, 1e-5);
                float mask = 1.0 - smoothstep(halfSolid, halfSolid + fade, dist);
                mask *= step(1e-4, _Intensity);

                return half4(_Color.rgb, _Color.a * _Alpha * mask);
            }
            ENDHLSL
        }
    }
}
