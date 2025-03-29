// TODO
struct ShaderInput
{
    float4 Position : POSITION;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

float4 PixelFunction(ShaderInput input) : SV_Target
{
    return float4(noise(input.TexCoords.x), noise(input.TexCoords.y), noise(input.TexCoords.x * input.TexCoords.y), 1);
}

technique Technique1
{
    pass Pass1
    {
        PixelShader = compile ps_4_0 PixelFunction();
    }
}