#version 410 core

layout(location = 0) in vec3 position;
layout(location = 1) in float aTexLayer;
layout(location = 2) in float aTexCoord;
layout(location = 3) in vec3 normal;

uniform mat4 viewMatrix;
uniform mat4 modelMatrix;
uniform mat4 projectionMatrix;
uniform mat4 chunkModelMatrix;
uniform int isMenuRendered;

uniform int renderDistance;
uniform vec3 playerPos;
uniform int chunkSize;

out vec4 fColor;
out vec2 ftexCoords;
out vec3 fnormal;
out vec3 fragPos;
out vec3 vertexPos;
flat out float fTexLayer;
    

void main() {
    if (isMenuRendered == 1)
        gl_Position = vec4(position, 1.0) * modelMatrix * viewMatrix * projectionMatrix;
    else
        gl_Position = vec4(position, 1.0) * chunkModelMatrix * viewMatrix * projectionMatrix;

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

        fragPos = vec3(vec4(position, 1.0) * modelMatrix);
        ftexCoords = texCoords[int(aTexCoord)];
        fnormal = normal * mat3(transpose(inverse(modelMatrix)));
    }
} 
