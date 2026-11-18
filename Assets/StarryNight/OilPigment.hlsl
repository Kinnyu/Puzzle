#ifndef STARRY_OIL_PIGMENT
#define STARRY_OIL_PIGMENT
TEXTURE2D(_Paint); SAMPLER(sampler_Paint);

float3 SamplePaintLevel(float2 uv, float2 dx, float2 dy, float2 scale)
{
    return SAMPLE_TEXTURE2D_GRAD(_Paint, sampler_Paint, uv*scale, dx*scale, dy*scale).rgb;
}

float3 SurfacePaint(float2 metres, bool lane)
{
    float2 uv = metres + .12*sin(metres.yx*.72 + .42*sin(metres*.91));
    float2 dx = ddx(uv), dy = ddy(uv);
    // Measure each axis separately so a receding road can still show individual dabs.
    // The tile's brush marks run along U. Broaden the cross-stroke axis more than
    // the long axis to give each dab a fuller body against the painted sky.
    float across = length(float2(dx.y, dy.y));
    // Along a stroke the second coordinate is constant. Project out its gradient
    // so a wall seen almost edge-on does not stretch the long axis into streaks.
    float along = abs(dx.x*dy.y - dx.y*dy.x) / max(across, .000001);
    float2 metresPerPixel = float2(along, across);
    // The street's long, receding marks need a wider footprint at walking height.
    float2 tileSpanInScreenHeights = lane ? float2(.85, 1.32) : float2(.76, 1.08);
    float2 lod = log2(max(metresPerPixel * GetScaledScreenParams().y * tileSpanInScreenHeights, .00001));
    float2 fine = exp2(-floor(lod));
    float2 blend = frac(lod);
    // Interpolate the enclosing triangle in the two-axis scale grid. Each level stays
    // anchored to the mesh, and shared boundary samples agree as the camera moves.
    float2 middle = blend.x > blend.y ? float2(.5, 1.) : float2(1., .5);
    float3 a = SamplePaintLevel(uv, dx, dy, fine);
    float3 b = SamplePaintLevel(uv, dx, dy, fine*middle);
    float3 c = SamplePaintLevel(uv, dx, dy, fine*.5);
    float3 paint = a*(1.-max(blend.x,blend.y)) + b*abs(blend.x-blend.y) + c*min(blend.x,blend.y);
    // Strengthen the deposited pigment around the tile's channel means. The
    // brighter ridges read as thicker paint while the underlying palette stays stable.
    const float3 meanPaint = float3(.531, .215, .555);
    return saturate(meanPaint + (paint-meanPaint)*1.38);
}

float3 OilColour(float2 q, float3 pos, float3 base, float3 accent, float family, float3 imp, float bristles, float distanceMetres)
{
    float wash = .5 + .24*sin(q.y*2.65 + .64*sin(q.x*.84)) + .14*sin(q.x*.62+q.y*.46);
    float deposit = imp.g*.68 + .22*wash;
    float shade = .50 + .84*imp.r + .24*imp.b + .23*(bristles-.52);
    if (family > 4.5)
    {
        // Broken silver-green strokes turn with the curved coordinates of each crown.
        float arc = q.x*3.35 + q.y*.92 + 1.16*sin(q.y*1.71+.33*q.x);
        float ridge = .5+.5*cos(arc+.24*sin(q.y*3.13));
        float light = smoothstep(.69,.96,ridge+.13*(imp.r-.5));
        float broken = .68+.32*sin(q.y*2.1+.81*sin(q.x)+imp.g);
        deposit += .34*light;
        accent += float3(.17,.20,.23)*light*broken;
        shade *= .83+.32*light;
    }
    else if (family > .5 && family < 1.5)
    {
        // Irregular blue strata trace the curved hillside in successive bands of broken colour.
        float contour = pos.y*.93 + pos.x*.085 + .85*sin(pos.x*.17 + pos.z*.08);
        float band = .5 + .5*sin(contour + .71*sin(contour*2.32 + pos.x*.13));
        deposit += .33*band;
        shade *= .78 + .30*band;
    }
    else if (family > 1.5 && family < 3.5)
    {
        float furrow = sin(q.y*4.25 + 1.1*sin(q.x*.37) + .6*sin(q.x*.94+q.y));
        float ribbon = smoothstep(-.57,.89,furrow + .49*(imp.r-.5));
        float cluster = .52 + .28*sin(q.x*.64+q.y*.71) + .17*sin(q.x*1.44-q.y*.49);
        deposit += ribbon*cluster*(family < 2.5 ? .26 : .18);
        shade *= .92+.13*ribbon;
    }
    else if (family > 3.5)
    {
        float branch = pos.x*4.9+pos.z*3.6 + 1.15*sin(pos.y*.91 + pos.x*1.43) + .44*sin(pos.y*2.14);
        float run = .5+.5*sin(branch + .68*sin(branch*1.86));
        deposit += .38*run;
        shade *= .57 + .51*run;
    }
    else
    {
        // A cooler underpainting remains visible between the denser passages of pigment.
        float broken = .5+.5*sin(q.y*2.7 + sin(q.x*.77) + .8*sin(q.y*.75 + q.x*.36));
        deposit += .17*broken;
        shade *= .88+.18*broken;
    }
    float3 pigment = lerp(base, accent, saturate(deposit))*shade;
    float air = smoothstep(40.,145.,distanceMetres)*.23;
    return lerp(pigment,float3(.19,.26,.43),air);
}
#endif

