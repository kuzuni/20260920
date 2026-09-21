Shader "DoodleIdle/Terrain"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite geometry texture", 2D) = "white" {}
        _AtlasTex ("Theme atlas", 2D) = "white" {}
        _UvRect ("Theme bounds", Vector) = (0,0,1,1)
        _TileSize ("Pattern size in world units", Float) = 3
        _GroundColor ("Opaque ground", Color) = (0.7,0.7,0.5,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            Blend Off
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_AtlasTex); SAMPLER(sampler_AtlasTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _UvRect;
                half4 _GroundColor;
                float _TileSize;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 world : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(world);
                output.world=world.xy;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // A continuous triangle wave samples identical texels at every mirrored boundary.
                float2 uv=1-abs(frac(input.world/(_TileSize*2))*2-1);
                half4 texel=SAMPLE_TEXTURE2D(_AtlasTex,sampler_AtlasTex,_UvRect.xy+uv*_UvRect.zw);
                return half4(lerp(_GroundColor.rgb,texel.rgb,texel.a),1);
            }
            ENDHLSL
        }
    }
}
