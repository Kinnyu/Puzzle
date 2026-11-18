Shader "Starry Night/Painted ridge" {
    Properties {
        _Landscape ("Hillside pigment", 2D) = "white" {}
        _Paint ("Bristles in metres", 2D) = "gray" {}
    }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass {
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "OilPigment.hlsl"
            struct Input { float4 position : POSITION; float2 pigment : TEXCOORD0; float2 metres : TEXCOORD1; float4 colour : COLOR; };
            struct Varyings { float4 clip : SV_POSITION; float2 pigment : TEXCOORD0; float2 metres : TEXCOORD1; float toe : TEXCOORD2; };
            TEXTURE2D(_Landscape); SAMPLER(sampler_Landscape);
            Varyings Vert(Input v) {
                Varyings o;
                o.clip = TransformObjectToHClip(v.position.xyz);
                o.pigment = v.pigment;
                o.metres = v.metres;
                o.toe = v.colour.r;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target {
                half3 stroke = SurfacePaint(i.metres, false);
                half3 pigment = SAMPLE_TEXTURE2D(_Landscape,sampler_Landscape,i.pigment).rgb;
                half furrow = .5+.5*sin(i.metres.y*2.7+.68*sin(i.metres.x*.32)+stroke.g);
                half3 field = lerp(half3(.016,.026,.036),half3(.080,.098,.067),furrow);
                #ifdef UNITY_COLORSPACE_GAMMA
                    field = pow(field, .455);
                #endif
                pigment = lerp(pigment,field,smoothstep(0.,.29,i.toe));
                // Each hillside occupies half the pigment atlas, beginning at its crest.
                float crest = i.pigment.y - (i.pigment.y >= .5 ? .5 : 0.) - .5/256.;
                half rim = 1.-smoothstep(.65,2.,crest/max(fwidth(i.pigment.y),.00001));
                pigment *= 1.-rim*(.35+.13*stroke.r);
                return half4(pigment*(.77+.44*stroke.r+.10*stroke.b),1);
            }
            ENDHLSL
        }
    }
}
