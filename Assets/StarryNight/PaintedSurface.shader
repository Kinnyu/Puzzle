Shader "Starry Night/Metre Scale Oil"
{
    Properties
    {
        _Paint ("Bristle and pigment tile", 2D) = "white" {}
        [MainColor] _Color ("Pigment tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "OilPigment.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END
            struct Input { float4 position : POSITION; float3 normal : NORMAL; float2 metres : TEXCOORD0; float4 base : COLOR; float3 accent : TEXCOORD1; float3 edge : TEXCOORD2; };
            struct Output { float4 clip : SV_POSITION; float2 metres : TEXCOORD0; float3 pos : TEXCOORD1; float4 base : COLOR; float3 accent : TEXCOORD2; float3 edge : TEXCOORD3; float3 normal : TEXCOORD4; };
            Output Vert(Input v)
            {
                Output o;
                o.pos = TransformObjectToWorld(v.position.xyz);
                o.clip = TransformWorldToHClip(o.pos);
                o.metres = v.metres;
                o.base = v.base;
                o.accent = v.accent;
                o.edge = v.edge;
                o.normal = TransformObjectToWorldNormal(v.normal);
                return o;
            }
            half4 Frag(Output i) : SV_Target
            {
                float family = i.base.a*255.;
                float eyeDistance = distance(_WorldSpaceCameraPos, i.pos);
                float closeWidth = family < .5 ? NearWallWidth(eyeDistance) : 1.;
                float3 imp = SurfacePaint(i.metres, family > 2.5 && family < 3.5, closeWidth);
                float3 pigment = OilColour(i.metres, i.pos, i.base.rgb, i.accent, i.base.a*255., imp, imp.b, eyeDistance);
                // Only authored contour edges carry a finite metric distance. Quad diagonals stay clear.
                float3 pixel = max(fwidth(i.edge), .0001);
                float width = (family < .5 ? .056 : .090) * (.84 + .32*imp.r);
                float3 lineRadius = max(width, pixel*1.28);
                float3 lines = 1. - smoothstep(lineRadius-pixel*.48, lineRadius+pixel*.48, i.edge);
                float inkMask = max(lines.x,max(lines.y,lines.z));
                if (family > 3.5 || (family > .5 && family < 1.5))
                {
                    float facing = abs(dot(normalize(i.normal), GetWorldSpaceNormalizeViewDir(i.pos)));
                    inkMask = max(inkMask,1.-smoothstep(.045,family>3.5 ? .27 : .15,facing));
                }
                float warm = saturate((i.base.r-i.base.b)*3.+.22);
                float3 ink = lerp(float3(.076,.065,.155),float3(.18,.088,.142),warm) * (.79+.38*imp.r);
                pigment = lerp(pigment,ink,inkMask*.92);
                #ifndef UNITY_COLORSPACE_GAMMA
                    pigment = SRGBToLinear(saturate(pigment));
                #endif
                return half4(pigment * _Color.rgb,1);
            }
            ENDHLSL
        }
    }
}

