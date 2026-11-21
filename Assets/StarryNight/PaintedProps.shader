Shader "Starry Night/Painted town props"
{
    Properties
    {
        [MainColor] _BaseColor ("Pigment", Color) = (1,1,1,1)
        [MainTexture] _BaseMap ("Artwork", 2D) = "white" {}
        _Paint ("Bristle and pigment tile", 2D) = "gray" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "OilPigment.hlsl"
            #include "StoryBrush.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
            CBUFFER_END

            struct Input { float4 position : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct Output { float4 clip : SV_POSITION; float3 pos : TEXCOORD0; float3 normal : TEXCOORD1; float2 uv : TEXCOORD2; float3 localMetres : TEXCOORD3; };
            Output Vert(Input v)
            {
                Output o;
                o.pos = TransformObjectToWorld(v.position.xyz);
                o.clip = TransformWorldToHClip(o.pos);
                o.normal = TransformObjectToWorldNormal(v.normal);
                o.localMetres = v.position.xyz * float3(length(TransformObjectToWorldDir(float3(1,0,0), false)),
                    length(TransformObjectToWorldDir(float3(0,1,0), false)), length(TransformObjectToWorldDir(float3(0,0,1), false)));
                o.uv = v.uv;
                return o;
            }

            half4 Frag(Output i) : SV_Target
            {
                StoryCut(i.pos);
                float3 normal = normalize(i.normal);
                float3 axis = abs(TransformWorldToObjectNormal(normal));
                float2 metres = axis.y > max(axis.x,axis.z) ? i.localMetres.xz :
                    (axis.z > axis.x ? i.localMetres.xy : i.localMetres.zy);
                float3 paint = SurfacePaint(metres, false, NearWallWidth(distance(i.pos, _WorldSpaceCameraPos)));
                float light = .85 + .20 * dot(normal, normalize(float3(-.45,.8,-.38)));
                float3 pigment = _BaseColor.rgb * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                pigment *= light * (.46 + .94 * paint.r + .26 * paint.b);
                float facing = abs(dot(normal, GetWorldSpaceNormalizeViewDir(i.pos)));
                float rim = 1. - smoothstep(.06, .21, facing);
                float2 edgePixels = min(i.uv, 1. - i.uv) / max(fwidth(i.uv), .00001);
                float contour = (1. - smoothstep(.8, 1.9, min(edgePixels.x, edgePixels.y)))
                    * step(.999, max(axis.x, max(axis.y, axis.z)));
                float3 ink = pigment * .032 + float3(.005, .004, .008);
                return half4(lerp(pigment, ink, max(rim, contour)), 1);
            }
            ENDHLSL
        }
    }
}
