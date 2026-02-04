Shader "Custom/RetroCRT_U6_Final_Fixed"
{
    Properties
    {
        _PixelSize ("Pixel Size", Range(1, 500)) = 150
        _Scanline ("Scanline Intensity", Range(0, 1)) = 0.5
        _Distortion ("Distortion", Range(0, 1)) = 0.1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            HLSLPROGRAM
            // 핵심: 유니티 공식 내장 함수 'Vert'를 사용함 (대문자 V 주의)
            #pragma vertex Vert
            #pragma fragment Frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // Inversion 샘플이 사용하는 공식 라이브러리
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _PixelSize;
            float _Scanline;
            float _Distortion;

            // 프래그먼트 함수 이름도 공식 규격에 맞게 Frag로 변경
            half4 Frag (Varyings input) : SV_Target
            {
                // Blit.hlsl에서 제공하는 공식 UV 좌표
                float2 uv = input.texcoord;

                // 1. 볼록렌즈 왜곡
                float2 centeredUV = uv * 2.0 - 1.0;
                float dist = length(centeredUV);
                uv += centeredUV * dist * dist * _Distortion;

                // 2. 픽셀레이션
                uv = floor(uv * _PixelSize) / _PixelSize;

                // 화면 밖 영역 검정색 처리
                if(uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return half4(0, 0, 0, 1);

                // 3. 화면 샘플링 (Inversion과 동일한 공식 매크로)
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                // 4. CRT 가로줄
                float scanline = sin(uv.y * _PixelSize * 3.14) * _Scanline;
                col.rgb -= scanline * 0.1;

                return col;
            }
            ENDHLSL
        }
    }
}