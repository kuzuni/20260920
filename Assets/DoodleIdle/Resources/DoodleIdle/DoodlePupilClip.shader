Shader "DoodleIdle/Pupil Clip"
{
    Properties
    {
        [PerRendererData] _MainTex ("Pupil", 2D) = "white" {}
        _MaskTex ("Light mask", 2D) = "white" {}
        _NormalMap ("Normal map", 2D) = "bump" {}
        _EyeMaskTex ("Authored eye mask", 2D) = "white" {}
        _EyeMaskRect ("Mask texture rect", Vector) = (0,0,1,1)
        _EyeMaskShape ("Mask scale and pivot", Vector) = (1,1,0.5,0.5)
        _EyeMaskCutoff ("Mask alpha cutoff", Range(0,1)) = 0.5
        _PortraitUnlit ("Unlit UI preview", Float) = 0
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "DisableBatching"="True" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            TEXTURE2D(_EyeMaskTex);
            SAMPLER(sampler_EyeMaskTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _EyeMaskRect;
                float4 _EyeMaskShape;
                float4x4 _PupilToMask;
                float _EyeMaskCutoff;
                half _PortraitUnlit;
            CBUFFER_END
            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; };
            struct Varyings { COMMON_2D_LIT_OUTPUTS half4 color : COLOR; float2 maskUV : TEXCOORD4; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"
            Varyings Vert(Attributes input)
            {
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonLitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                float2 maskPosition = mul(_PupilToMask, float4(input.positionOS,1)).xy;
                output.maskUV = maskPosition * _EyeMaskShape.xy + _EyeMaskShape.zw;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                clip(input.maskUV);
                clip(1 - input.maskUV);
                float2 uv = _EyeMaskRect.xy + input.maskUV * _EyeMaskRect.zw;
                clip(SAMPLE_TEXTURE2D(_EyeMaskTex, sampler_EyeMaskTex, uv).a - _EyeMaskCutoff);
                if (_PortraitUnlit > .5h) return input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                return CommonLitFragment(input, input.color);
            }
            ENDHLSL
        }
    }
}
