// TODO
sampler s0;

struct ShaderInput
{
    float4 _ : SV_Position;
    float4 drawColor : COLOR0;
    float2 drawLocation : TEXCOORD0;
};

float4 PixelFunction(ShaderInput input) : SV_Target
{
    float2 delta = float2(0.5f, 0.5f) - input.drawLocation;
    float distance = delta.x * delta.x + delta.y * delta.y;
    float distanceFromCenter = 0.25f - distance;
    return (distanceFromCenter > 0) ?
            float4(1, 1, 1, 1) * input.drawColor :
            float4(0, 0, 0, 0);
}

technique Technique1
{
    pass Pass1
    {
        PixelShader = compile ps_4_0 PixelFunction();
    }
}