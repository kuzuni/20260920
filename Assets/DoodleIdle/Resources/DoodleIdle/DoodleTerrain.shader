Shader "DoodleIdle/Terrain"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite geometry texture", 2D) = "white" {}
        _AtlasTex ("Theme atlas", 2D) = "white" {}
        _UvRect ("Theme bounds", Vector) = (0,0,1,1)
        _TileSize ("Pattern size in world units", Float) = 3
        _GroundColor ("Opaque ground", Color) = (0.7,0.7,0.5,1)
        _PatternStrength ("Ground detail contrast", Range(0,1)) = 0.55
        _PatternSaturation ("Ground detail saturation", Range(0,1)) = 0.85
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
                half _PatternStrength;
                half _PatternSaturation;
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
            half4 GroundSample(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_AtlasTex,sampler_AtlasTex,_UvRect.xy+uv*_UvRect.zw);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Repeat in the source orientation: grass roots must stay below the blades.
                float2 uv=frac(input.world/_TileSize);
                float2 shifted=frac(uv+0.5);
                float2 interior=smoothstep(0.0,0.08,min(uv,1-uv));
                // Cross-fade only near wrap edges into a translated copy of the same art.
                // At the edge both sides sample the center; no mirroring, rotation or gaps.
                half4 lower=lerp(GroundSample(shifted),GroundSample(float2(uv.x,shifted.y)),interior.x);
                half4 upper=lerp(GroundSample(float2(shifted.x,uv.y)),GroundSample(uv),interior.x);
                half4 texel=lerp(lower,upper,interior.y);
                half luminance=dot(texel.rgb,half3(0.2126,0.7152,0.0722));
                half3 detail=lerp(luminance.xxx,texel.rgb,_PatternSaturation);
                // Keep inked motifs behind actors while retaining each theme's surface texture.
                return half4(lerp(_GroundColor.rgb,detail,texel.a*_PatternStrength),1);
            }
            ENDHLSL
        }
    }
}
