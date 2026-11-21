Shader "Starry Night/World brush guide"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../../StarryNight/StoryBrush.hlsl"
            float _BrushHighlight;
            struct Vertex { float4 clip : SV_POSITION; float3 world : TEXCOORD0; };
            Vertex Vert(float4 position : POSITION)
            {
                Vertex o;
                o.world = TransformObjectToWorld(position.xyz);
                o.clip = TransformWorldToHClip(o.world);
                return o;
            }
            half4 Frag(Vertex i) : SV_Target
            {
                half4 pixel = StorySample(i.world);
                if (_StoryErase > .5) clip(.995 - pixel.a);
                pixel.a = saturate(pixel.a * _BrushHighlight);
                return pixel;
            }
            ENDHLSL
        }
    }
}
