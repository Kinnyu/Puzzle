#ifndef STORY_BRUSH_INCLUDED
#define STORY_BRUSH_INCLUDED
TEXTURE2D(_StoryStencil);
SAMPLER(sampler_StoryStencil);
float4 _StoryAxisX, _StoryAxisY;
float _StoryErase;

half4 StorySample(float3 world)
{
    float4 position = float4(world, 1);
    return SAMPLE_TEXTURE2D(_StoryStencil, sampler_StoryStencil,
        saturate(float2(dot(position,_StoryAxisX), dot(position,_StoryAxisY))));
}

void StoryCut(float3 world)
{
    if (_StoryErase > .5) clip(.995 - StorySample(world).a);
}
#endif
