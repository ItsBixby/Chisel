#include <Common.glsl>

uniform sampler2D LightmapB1;
uniform sampler2D LightmapB2;
uniform sampler2D LightmapB3;
uniform sampler2D RefractionTex;
uniform sampler2D RefractionDepth;
uniform sampler2D ReflectionTex;
uniform sampler2D dudv;

const vec3 WaterDeepColor = vec3(55.0 / 255.0, 70.0 / 255.0, 82.0 / 255.0);

const int AtlasW = 6;
const int AtlasH = 5;
const int AtlasFrames = 30; // AtlasW * AtlasH

const float DUDVFrameRate1 = 20.0;
const float DUDVScale1 = 0.1;

const float DUDVFrameRate2 = 10.0;
const float DUDVScale2 = 0.037;

varying vec4 WorldPos;
varying vec3 Normal;
varying vec2 TextureCoordinate;
varying vec2 LightmapCoordinate;
varying vec3 FaceTangent;
varying vec3 FaceBinormal;
varying vec4 ScreenPos;

float Hash21(vec2 p)
{
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

vec2 Hash22(vec2 p)
{
    float n = Hash21(p);
    return vec2(n, Hash21(p + n + 17.13));
}

vec4 CalcReflection(vec2 vTexCoord, float blurAmount, vec2 jitter)
{
    vec2 offset = (1.0 / screenSize) * blurAmount;
    vec2 jitterOffset = jitter * offset;

    vec4 result = vec4(0.0, 0.0, 0.0, 0.0);
    result += texture2D(ReflectionTex, vTexCoord + jitterOffset + vec2(-offset.x, -offset.y));
    result += texture2D(ReflectionTex, vTexCoord + jitterOffset + vec2(offset.x, -offset.y));
    result += texture2D(ReflectionTex, vTexCoord + jitterOffset + vec2(-offset.x, offset.y));
    result += texture2D(ReflectionTex, vTexCoord + jitterOffset + vec2(offset.x, offset.y));
    result *= 0.25;
    return result;
}

vec2 SampleDUDV(vec2 worldXZ, float scale, float frameRate)
{
    int frame = int(mod(time * frameRate, float(AtlasFrames)));
    vec2 cellSize = vec2(1.0 / float(AtlasW), 1.0 / float(AtlasH));
    vec2 cellOrigin = vec2(float(mod(float(frame), float(AtlasW))), float(frame / AtlasW)) * cellSize;

    vec2 tiledUV = fract(worldXZ * scale);
    vec2 atlasUV = cellOrigin + tiledUV * cellSize;

    return texture2D(dudv, atlasUV).xy;
}

void main()
{
    vec2 screenUV = (ScreenPos.xy / ScreenPos.w) * vec2(0.5, -0.5) + vec2(0.5, 0.5);
    vec2 pixelCoord = screenUV * screenSize;

    vec4 lightmap = texture2D(LightmapB1, LightmapCoordinate) * 0.6 +
                     texture2D(LightmapB2, LightmapCoordinate) * 0.6 +
                     texture2D(LightmapB3, LightmapCoordinate) * 0.6;

    vec2 ditherJitter = Hash22(pixelCoord + time * 60.0) - 0.5;

    vec2 dudv1 = SampleDUDV(WorldPos.xz, DUDVScale1, DUDVFrameRate1);
    vec2 dudv2 = SampleDUDV(WorldPos.xz, DUDVScale2, DUDVFrameRate2);

    vec2 combinedOffset = ((dudv1 * 2.0 - 1.0) + (dudv2 * 2.0 - 1.0));
    vec2 samplePoint = screenUV + combinedOffset * 0.025;

    vec4 reflectionColor = CalcReflection(samplePoint, 2.0, ditherJitter);
    vec4 refractionColor = texture2D(RefractionTex, samplePoint);
    vec4 rawRefractionColor = texture2D(RefractionTex, screenUV);

    float sceneDepth = GetLinearDepth(texture2D(RefractionDepth, screenUV).r);
    float waterdist = ScreenPos.z;

    vec3 normal = Normal;
    vec3 dirToWater = normalize(WorldPos.xyz - cameraPos);
    bool viewingFromBelow = dot(dirToWater, normal) > 0.0;

    float viewDepth = distance(waterdist, sceneDepth);

    float verticalness = max(dot(normal, vec3(0.0, 1.0, 0.0)), 0.05);
    float underwaterDepth = max(viewDepth * verticalness, 0.0);

    float waterDownView = dot(normal, -dirToWater);

    vec3 waterColor = WaterDeepColor * lightmap.xyz;

    if (viewingFromBelow)
    {
        gl_FragColor = WriteSceneOutput(mix(rawRefractionColor, refractionColor, clamp(viewDepth, 0.0, 1.0)));
        return;
    }

    refractionColor.xyz = mix(refractionColor.xyz, waterColor, clamp(underwaterDepth * 0.24 * (1.0 - waterDownView * 0.6), 0.0, 1.0));

    vec3 litColor = mix(reflectionColor.xyz, refractionColor.xyz, waterDownView);
    litColor = mix(rawRefractionColor.xyz, litColor, clamp(viewDepth, 0.0, 1.0));

    gl_FragColor = WriteSceneOutput(vec4(litColor, 1.0));
}
