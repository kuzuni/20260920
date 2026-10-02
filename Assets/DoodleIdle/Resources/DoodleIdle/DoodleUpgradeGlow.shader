Shader "DoodleIdle/Upgrade Glow"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass {
            Tags { "LightMode"="Universal2D" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            V Vert(A a) { V o; o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.color=a.color;o.uv=a.uv;return o; }
            half4 Frag(V i):SV_Target { float r=length(i.uv*2-1); i.color.a*=1-smoothstep(.1,1,r);return i.color; }
            ENDHLSL
        }
    }
}
