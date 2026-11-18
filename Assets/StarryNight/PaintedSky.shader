Shader "Starry Night/Panoramic Oil Sky"
{
    Properties
    {
        _Sky ("Painted hemisphere", 2D) = "black" {}
        _VillageHeading ("Sky heading (degrees)", Float) = 0
        _Spread ("Spacing of the stars", Range(0,.4)) = 0
        _LowerSky ("Lower the stars (degrees)", Range(0,10)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_Sky); SAMPLER(sampler_Sky);
            float _VillageHeading, _Spread, _LowerSky;
            struct Varyings { float4 clip : SV_POSITION; float3 direction : TEXCOORD0; };
            Varyings Vert(float4 p : POSITION)
            {
                Varyings v;
                v.clip = TransformObjectToHClip(p.xyz);
                v.direction = p.xyz;
                return v;
            }
            half4 Frag(Varyings v) : SV_Target
            {
                float3 d = normalize(v.direction);
                float lon = atan2(d.x,d.z) - radians(_VillageHeading);
                float lat = asin(clamp(d.y,-1.,1.));
                float2 uv = float2((lon-_Spread*sin(lon))/(2*PI)+.5, saturate((degrees(lat)+_LowerSky*cos(lat)+8.)/98.));
                return half4(SAMPLE_TEXTURE2D(_Sky,sampler_Sky,uv).rgb,1);
            }
            ENDHLSL
        }
    }
}

