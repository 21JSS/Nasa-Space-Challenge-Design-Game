Shader "Aurora/RecoverySky"
{
    Properties
    {
        _NoiseTex("Cloud texture",2D)="gray"{}
        _RecoveryTime("Animation clock",Float)=0
        _AtmosphereBlend("Atmosphere visibility",Range(0,1))=1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-100" }
        Pass
        {
            Cull Front
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial)
                float _RecoveryTime;
                float _AtmosphereBlend;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 direction:TEXCOORD0; };
            Varyings Vert(Attributes i)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz);
                o.direction=i.positionOS.xyz; return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float3 ray=normalize(i.direction);
                float h=saturate(ray.y);
                float3 color=lerp(float3(.43,.62,.69),float3(.065,.24,.43),pow(h,.42));
                float3 sun=normalize(float3(-.5,.32,.72));
                float glow=pow(saturate(dot(ray,sun)),12);
                color+=float3(.4,.25,.10)*glow;
                color+=float3(1,.83,.54)*smoothstep(.9992,.9998,dot(ray,sun));
                float2 uv=ray.xz/(max(ray.y,0)+.22)*.22+float2(_RecoveryTime*.0009,_RecoveryTime*.0003);
                float n=SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,uv).r*.60;
                n+=SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,uv*2.07).g*.26;
                n+=SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,uv*4.11).b*.14;
                float clouds=smoothstep(.46,.66,n)*smoothstep(-.02,.10,ray.y);
                float3 cloudColor=lerp(float3(.38,.51,.59),float3(.92,.91,.82),smoothstep(.45,.73,n));
                color=lerp(color,cloudColor,clouds*.9);
                float haze=exp(-abs(ray.y)*22);
                color=lerp(color,float3(.43,.60,.66),haze*.65);
                // A dark upper atmosphere gradually gives way to the blue recovery sky.
                color=lerp(color*float3(.20,.35,.65),color,_AtmosphereBlend);
                return half4(color,_AtmosphereBlend);
            }
            ENDHLSL
        }
    }
}
