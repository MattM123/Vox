#version 430 core

layout(location = 0) in vec3 position;
layout(location = 1) in int aTexLayer;
layout(location = 2) in int aTexCoord;
layout(location = 3) in int aLight;
layout(location = 4) in vec3 aNormal;
layout(location = 5) in int aBlocktype;
layout(location = 6) in int aFace;

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

out vec4 fragPosLightSpace;
out vec4 fColor;
out vec2 ftexCoords;
out vec3 fforwardDir;
out vec3 fragPos;
out vec3 fnormal;
out vec4 fTargetVertex;
flat out float fTexLayer;
out vec3 vertexPos;

uniform vec3 blockCenter;
uniform vec3 curPos;
uniform vec3 localHit;

out vec4 fblockCenter;
out vec4 fcurPos;
out vec4 flocalHit;

void main() {

    if (isMenuRendered == 1) {
        gl_Position = vec4(position, 1.0) * modelMatrix * viewMatrix * projectionMatrix;
        fragPos = vec3(vec4(position, 1.0) * modelMatrix);
    }
    else {
        gl_Position =   vec4(position, 1.0) * chunkModelMatrix * viewMatrix * projectionMatrix;
        fTargetVertex = vec4(targetVertex, 1.0) * chunkModelMatrix;
        fforwardDir =   normalize(mat3(chunkModelMatrix) * forwardDir);
        fragPos =       (chunkModelMatrix * vec4(position, 1.0)).xyz;
        fblockCenter =  vec4(blockCenter, 1.0) * chunkModelMatrix;// * viewMatrix * projectionMatrix;
        fcurPos =       vec4(curPos, 1.0) * chunkModelMatrix;// * viewMatrix * projectionMatrix;
        flocalHit =     vec4(localHit, 1.0) * chunkModelMatrix;// * viewMatrix * projectionMatrix;

    }

    //Render partial mesh if chunk is on the edge of render distance by
    //calculating the distance between the current vertex position and the player's position
    float dist = distance(position.xz, playerPos.xz);

    // Cull the vertex if the distance is greater than (chunkSize * renderDistance)
    if (dist > (chunkSize * renderDistance - 5) && isMenuRendered == 0) {
        gl_Position = vec4(2.0, 2.0, 2.0, 1.0); // Cull by moving out of view
    } else {

        //passthrough
        fTexLayer = aTexLayer;
        vertexPos = position;

        vec2 texCoords[4] = vec2[4](
            vec2(0.0f, 0.0f),
            vec2(1.0f, 0.0f),
            vec2(0.0f, 1.0f),
            vec2(1.0f, 1.0f)
        );

        fragPosLightSpace = vec4(fragPos, 1.0) * lightModel * lightViewMatrix * lightProjMatrix;
        ftexCoords = texCoords[aTexCoord];
        fnormal = aNormal * mat3(transpose(inverse(modelMatrix)));
    }
} 
