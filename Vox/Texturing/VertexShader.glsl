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

out vec4 fColor;
out vec2 ftexCoords;
out vec3 fnormal;
out vec3 fragPos;
flat out float fTexLayer;


void main() {
    if (isMenuRendered == 1)
        gl_Position = vec4(position, 1.0) * modelMatrix * viewMatrix * projectionMatrix;
    else
        gl_Position = vec4(position, 1.0) * chunkModelMatrix * viewMatrix * projectionMatrix;

    //passthrough
    fTexLayer = aTexLayer;

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
