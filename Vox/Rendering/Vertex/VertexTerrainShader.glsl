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

layout(std430, binding = 0) buffer BlockFaceData
{
    BlockFaceInstance blockFaces[];
};


uniform mat4 viewMatrix;
uniform mat4 modelMatrix;
uniform mat4 projectionMatrix;
uniform mat4 chunkModelMatrix;
uniform int isMenuRendered;

uniform mat4 lightProjMatrix;
uniform mat4 lightModel;
uniform mat4 lightViewMatrix;

uniform int renderDistance;
uniform vec3 playerPos;
uniform int chunkSize;
uniform vec3 targetVertex;
uniform vec3 forwardDir;

uniform vec3 playerMin;
uniform vec3 playerMax;

out vec3 fPlayerMin;
out vec3 fPlayerMax;

out vec4 fragPosLightSpace;
out vec4 fColor;
out vec2 ftexCoords;
out vec3 fforwardDir;
out vec3 fragPos;
out vec3 fnormal;
out vec4 fTargetVertex;
flat out float fTexLayer;
flat out int fLighting;

uniform vec3 blockCenter;
uniform vec3 curPos;
uniform vec3 localHit;

// Voxel utilities
#line 1 "VertexUtilityShader.glsl"
#include "VertexUtilityShader.glsl"

#line 1 "VertexTerrainShader.glsl"
void main() {

    BlockFaceInstance instance = blockFaces[gl_InstanceID];

    vec3 offset = GetCornerOffset(gl_VertexID, instance.faceDirection);
    vec3 worldPos = instance.facePosition + offset;


    if (isMenuRendered == 1) {
        gl_Position = vec4(worldPos, 1.0) * modelMatrix * viewMatrix * projectionMatrix;
        fragPos = vec3(vec4(worldPos, 1.0) * modelMatrix);
    }
    else {
        gl_Position =   vec4(worldPos, 1.0) * chunkModelMatrix * viewMatrix * projectionMatrix;
        fTargetVertex = vec4(targetVertex, 1.0) * chunkModelMatrix;
        fforwardDir =   normalize(mat3(chunkModelMatrix) * forwardDir);
        fragPos =       (chunkModelMatrix * vec4(worldPos, 1.0)).xyz;

    }


    //Render partial mesh if chunk is on the edge of render distance by
    //calculating the distance between the current vertex position and the player's position
    float dist = distance(worldPos.xz, playerPos.xz);

    // Cull the vertex if the distance is greater than (chunkSize * renderDistance)
    if ((dist > (chunkSize * renderDistance) && isMenuRendered == 0) || instance.textureLayer == 0) {
        gl_Position = vec4(2.0, 2.0, 2.0, 1.0); // Cull by moving out of view
    } else {

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
} 
