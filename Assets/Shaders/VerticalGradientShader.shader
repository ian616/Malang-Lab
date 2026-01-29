Shader "MalangLab/SoftLightBeam"
{
    Properties
    {
        [HDR] _Color ("Beam Color", Color) = (1, 1, 0, 1)
        _Intensity ("Intensity", Range(0, 20)) = 5.0
        _VPower ("Vertical Fade (Up/Down)", Range(0.1, 10)) = 2.0
        _SPower ("Side Fade (Edges)", Range(0.1, 10)) = 3.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        Blend SrcAlpha One  // 빛이 겹치면 더 밝아지는 효과
        ZWrite Off 
        Cull Off 

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            float4 _Color;
            float _Intensity;
            float _VPower;
            float _SPower;

            Varyings vert (Attributes input) {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag (Varyings input) : SV_Target {
                // 1. 위로 갈수록 흐려지는 그라데이션
                float vFade = pow(1.0 - input.uv.y, _VPower);

                // 2. [추가] 옆면 가장자리를 깎아서 '상자' 느낌 제거
                // UV.x가 0이나 1에 가까워지면 투명하게 만듦
                float sFade = sin(input.uv.x * 3.14159);
                sFade = pow(sFade, _SPower);

                half4 col = _Color * _Intensity;
                col.a = vFade * sFade; // 두 효과를 곱함

                return col;
            }
            ENDHLSL
        }
    }
}