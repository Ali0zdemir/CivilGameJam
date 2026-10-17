#ifndef CIVIL_FIRE_FLOW_NOISE
#define CIVIL_FIRE_FLOW_NOISE
float FireHash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
float FireNoise(float2 p)
{
    float2 i = floor(p); float2 f = frac(p); f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(FireHash(i), FireHash(i + float2(1, 0)), f.x),
        lerp(FireHash(i + float2(0, 1)), FireHash(i + float2(1, 1)), f.x), f.y);
}
float FireFbm(float2 p)
{
    return FireNoise(p) * 0.57 + FireNoise(p * 2.07 + 9.3) * 0.28 + FireNoise(p * 4.11 - 3.8) * 0.15;
}
float2 FireRollUV(float2 p, float2 center, float strength)
{
    float2 offset = p - center;
    float turn = strength * exp(-dot(offset, offset) * 3.8);
    float s, c; sincos(turn, s, c);
    return center + float2(offset.x * c - offset.y * s, offset.x * s + offset.y * c);
}
#endif
