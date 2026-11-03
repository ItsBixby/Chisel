#include <Common.glsl>
#include <PosFixup.glsl>

uniform mat4 World;
uniform mat4 View;
uniform mat4 Projection;

uniform vec3 SkyboxViewTranslation;
uniform bool Skybox3DView;

attribute vec4 in_Position0;
attribute vec4 in_Normal0;
attribute vec3 in_Tangent0;
attribute vec3 in_Binormal0;
attribute vec2 in_TextureCoordinate0;
attribute vec2 in_TextureCoordinate1; // lightmap UV

varying vec4 WorldPos;
varying vec3 Normal;
varying vec2 TextureCoordinate;
varying vec2 LightmapCoordinate;
varying vec3 FaceTangent;
varying vec3 FaceBinormal;
varying vec4 ScreenPos;

void main()
{
    vec4 worldPosition = in_Position0 * World;
    vec4 viewPosition = worldPosition * View;

    gl_Position = viewPosition * Projection;
    WorldPos = worldPosition;
    WorldPos.w = gl_Position.w;

    if (Skybox3DView)
    {
        WorldPos = (worldPosition - vec4(SkyboxViewTranslation, 0.0)) * 16.0;
        vec4 fakeViewPosition = WorldPos * View;
        WorldPos.w = (fakeViewPosition * Projection).w;
    }

    Normal = in_Normal0.xyz;
    TextureCoordinate = in_TextureCoordinate0;
    LightmapCoordinate = in_TextureCoordinate1;
    FaceTangent = in_Tangent0;
    FaceBinormal = in_Binormal0;
    ScreenPos = gl_Position;

    ApplyPosFixup();
}
