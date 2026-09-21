Shader "DoodleIdle/Particles"
{
    Properties
    {
        _MainTex ("Generated doodle texture", 2D) = "white" {}
        _UvRect ("Sprite bounds", Vector) = (0, 0, 1, 1)
        _Recolor ("Recolor flame", Float) = 0
        _Palette ("Flame color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _UvRect;
                float _Recolor;
                half4 _Palette;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = _UvRect.xy + input.uv * _UvRect.zw;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 pixel=SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half light=saturate(max(pixel.r,max(pixel.g,pixel.b))*1.3);
                pixel.rgb=lerp(pixel.rgb,lerp(half3(.04,.03,.06),_Palette.rgb,light),_Recolor);
                return pixel * input.color;
            }
            ENDHLSL
        }
    }
}
