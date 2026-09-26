#pragma once
float rand(float2 p)
{
    float h = dot(p, float2(127.1, 311.7));
    float v = sin(h) * 43758.5453123;
    
    return frac(v);
}

float Noise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * f * (f * (f * 6.0f - 15.0f) + 10.0f);

    float upp0 = rand(i + float2(0, 0));
    float upp1 = rand(i + float2(1, 0));
    float low0 = rand(i + float2(0, 1));
    float low1 = rand(i + float2(1, 1));
    
    float upp = lerp(upp0, upp1, u.x);
    float low = lerp(low0, low1, u.x);
    float res = lerp(upp, low, u.y);
    return 1 - 2 * res;
}

float2 hash2(float2 p)
{
    p = float2(dot(p, float2(127.1, 311.7)),
               dot(p, float2(269.5, 183.3)));
    return -1.0 + 2.0 * frac(sin(p) * 43758.5453123);
}

float fade(float t)
{
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
}

float GradientNoise2D(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = float2(fade(f.x), fade(f.y));

    float2 g00 = hash2(i + float2(0, 0));
    float2 g10 = hash2(i + float2(1, 0));
    float2 g01 = hash2(i + float2(0, 1));
    float2 g11 = hash2(i + float2(1, 1));

    float d00 = dot(g00, f - float2(0, 0));
    float d10 = dot(g10, f - float2(1, 0));
    float d01 = dot(g01, f - float2(0, 1));
    float d11 = dot(g11, f - float2(1, 1));

    float nx0 = lerp(d00, d10, u.x);
    float nx1 = lerp(d01, d11, u.x);
    return lerp(nx0, nx1, u.y); // 约 [-0.7, 0.7]
}