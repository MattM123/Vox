#version 430 core

struct BlockFaceInstance
{
    vec3 facePosition;  
    int faceDirection;   
    int textureLayer;       
    int index; 
    int lighting;
    int _pad1;
};

layout(std430, binding = 1) buffer BlockFaceData
{
    BlockFaceInstance blockFaces[];
};

uniform mat4 viewMatrix;
uniform mat4 modelMatrix;
uniform mat4 projectionMatrix;
uniform mat4 chunkModelMatrix;


uniform mat4 lightProjMatrix;
uniform mat4 lightModel;
uniform mat4 lightViewMatrix;


uniform vec3 playerPos;

uniform vec3 forwardDir;

uniform vec3 playerMin;
uniform vec3 playerMax;

out vec3 fPlayerMin;
out vec3 fPlayerMax;

out vec4 fragPosLightSpace;
out vec4 fColor;
out vec2 ftexCoords;

out vec3 fragPos;
out vec3 fnormal;
flat out float fTexLayer;
flat out int fLighting;

// Voxel utilities
#line 1 "VertexUtilityShader.glsl"
#include "VertexUtilityShader.glsl"

#line 1 "VertexInventoryShader.glsl"
void main() {

    BlockFaceInstance instance = blockFaces[gl_InstanceID];

    vec3 offset = GetCornerOffset(gl_VertexID, instance.faceDirection);
    vec3 worldPos = instance.facePosition + offset;


    gl_Position = vec4(worldPos, 1.0) * modelMatrix * viewMatrix * projectionMatrix;
    fragPos = vec3(vec4(worldPos, 1.0) * modelMatrix);


    //Render partial mesh if chunk is on the edge of render distance by
    //calculating the distance between the current vertex position and the player's position
    float dist = distance(worldPos.xz, playerPos.xz);

        //passthrough
        fTexLayer = instance.textureLayer;
        fLighting = instance.lighting;
        fPlayerMin = playerMin;
        fPlayerMax = playerMax;

        vec2 texCoords[4] = vec2[4](
            vec2(0.0f, 0.0f),
            vec2(1.0f, 0.0f),
            vec2(0.0f, 1.0f),
            vec2(1.0f, 1.0f)
        );

        fragPosLightSpace = vec4(fragPos, 1.0) * lightModel * lightViewMatrix * lightProjMatrix;

        int texCoordMapped = texCoordMap[instance.faceDirection][gl_VertexID];
        ftexCoords = texCoords[texCoordMapped];

        fnormal = GetNormal(instance.faceDirection);
} 