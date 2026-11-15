Shader "Aurora/Ocean"
{
    Properties
    {
        _NoiseTex("Water detail",2D)="gray"{}
        _RecoveryTime("Animation clock",Float)=0
        _SplashAge("Seconds after touchdown",Float)=-1
        _CapsulePosition("Capsule XZ / contact",Vector)=(0,0,0,0)
        _SplashPosition("Impact XZ",Vector)=(0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial)
                float _RecoveryTime, _SplashAge;
                float4 _CapsulePosition, _SplashPosition;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float2 slope:TEXCOORD1; };
            float Wave(float2 p,float t)
            {
                return sin(dot(p,float2(.72,.34))+t*1.25)*.055
                    +sin(dot(p,float2(-1.1,.91))-t*1.65)*.028
                    +sin(dot(p,float2(2.1,1.7))+t*2.1)*.012;
            }
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                o.positionWS.y+=Wave(o.positionWS.xz,_RecoveryTime);
                float2 p=o.positionWS.xz; float t=_RecoveryTime;
                o.slope=cos(dot(p,float2(.72,.34))+t*1.25)*.055*float2(.72,.34)
                    +cos(dot(p,float2(-1.1,.91))-t*1.65)*.028*float2(-1.1,.91)
                    +cos(dot(p,float2(2.1,1.7))+t*2.1)*.012*float2(2.1,1.7);
                o.positionCS=TransformWorldToHClip(o.positionWS); return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 p=i.positionWS.xz;
                float t=_RecoveryTime;
                float2 uv=p*.055+float2(t*.008,-t*.005);
                float4 detail=SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,uv);
                float4 detail2=SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,p*.13+float2(-t*.011,t*.007));
                float4 fine=SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,p*.47+float2(t*.013,t*.009));
                float2 slope=i.slope;
                float footprint=max(length(ddx(p)),length(ddy(p)));
                slope*=1-smoothstep(.25,2,footprint);
                slope+=(detail.rg+detail2.gb-1)*.18+(fine.rg-.5)*.095;
                float3 normal=normalize(float3(-slope.x,1,-slope.y));
                float3 view=GetWorldSpaceNormalizeViewDir(i.positionWS);
                float fresnel=pow(1-saturate(dot(normal,view)),4);
                float3 deep=float3(.012,.115,.17), shallow=float3(.035,.31,.36);
                float3 color=lerp(deep,shallow,saturate(detail.r*.55+detail2.g*.35));
                color=lerp(color,float3(.30,.48,.57),fresnel*.8);
                float3 sun=normalize(float3(-.5,.32,.72));
                float spec=pow(saturate(dot(normal,normalize(view+sun))),150);
                color+=float3(1,.80,.51)*spec*.65*(.3+detail2.b*.7);
                float distanceToCamera=distance(i.positionWS,_WorldSpaceCameraPos);
                float shimmer=pow(saturate(detail.b*detail2.b*2.6),5)*.12;
                color+=float3(.33,.65,.7)*shimmer*saturate(1-distanceToCamera/180);
                float crest=smoothstep(.68,.79,detail.g*.6+detail2.r*.4);
                float foam=crest*.35;
                if(_CapsulePosition.z>0)
                {
                    float radius=distance(p,_CapsulePosition.xy);
                    float contact=exp(-pow((radius-.34)/.065,2))*_CapsulePosition.z;
                    foam+=contact*(.2+fine.b*.7);
                }
                if(_SplashAge>=0 && _SplashAge<5)
                {
                    float r=distance(p,_SplashPosition.xy);
                    float ring=exp(-pow((r-(.42+_SplashAge*1.15))/.14,2));
                    float ring2=exp(-pow((r-(.32+_SplashAge*.8))/.1,2));
                    foam+=(ring+ring2*.55)*saturate(1-_SplashAge/5);
                }
                color=lerp(color,float3(.66,.87,.9),saturate(foam));
                color=lerp(color,float3(.31,.48,.56),1-exp(-distanceToCamera*.0018));
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
