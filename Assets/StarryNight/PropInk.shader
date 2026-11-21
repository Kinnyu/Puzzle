Shader "Starry Night/Prop ink"
{
    Properties
    {
        _InkCenter ("Local mesh center and flat canvas flag", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            #include "StoryBrush.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _InkCenter;
            CBUFFER_END
            struct Input { float3 position : POSITION; float2 uv : TEXCOORD0; };
            struct Output { float4 clip : SV_POSITION; float2 uv : TEXCOORD0; float width : TEXCOORD1; float3 world : TEXCOORD2; };

            Output Vert(Input input)
            {
                Output o;
                o.clip = TransformObjectToHClip(input.position);
                float4 center = TransformObjectToHClip(_InkCenter.xyz);
                o.width = clamp(3.5 / max(center.w, .5), 1.25, 3.0) * _ScaledScreenParams.y / 720.;
                if (_InkCenter.w < .5 && min(center.w, o.clip.w) > .001)
                {
                    float2 radial = (o.clip.xy / o.clip.w - center.xy / center.w) * _ScaledScreenParams.xy * .5;
                    // Shared corners move together even when adjoining faces have split normals.
                    // Limit expansion on very small silhouettes such as a brush tip.
                    float2 offset = radial * min(.10, o.width / max(length(radial), .001));
                    o.clip.xy += offset * (2. / _ScaledScreenParams.xy) * o.clip.w;
                    // Some period models are painted on both sides. Put their ink behind
                    // the painted face by a few screen pixels even if the winding is reversed.
                    float3 view = TransformWorldToView(TransformObjectToWorld(input.position));
                    view.z -= 6. * o.width * o.clip.w / (_ScaledScreenParams.y * abs(UNITY_MATRIX_P._m11));
                    float4 behind = mul(UNITY_MATRIX_P, float4(view, 1.));
                    o.clip.z = behind.z / behind.w * o.clip.w;
                }
                o.world = TransformObjectToWorld(input.position);
                o.uv = input.uv;
                return o;
            }

            half4 Frag(Output i) : SV_Target
            {
                StoryCut(i.world);
                if (_InkCenter.w > .5)
                {
                    float2 edge = min(i.uv, 1. - i.uv) / max(fwidth(i.uv), .00001);
                    clip(i.width - min(edge.x, edge.y));
                }
                half3 ink = half3(.065, .052, .092);
                #ifndef UNITY_COLORSPACE_GAMMA
                    ink = SRGBToLinear(ink);
                #endif
                return half4(ink, 1);
            }
            ENDHLSL
        }
    }
}
