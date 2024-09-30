#version 410 core

layout(location = 0) in vec3 position;
layout(location = 1) in float aTexLayer;
layout(location = 2) in float aTexCoord;

uniform mat4 viewMatrix;
uniform mat4 modelMatrix;
uniform mat4 projectionMatrix;

out vec4 fColor;
out vec2 ftexCoords;
flat out float fTexLayer;

void main() {
    gl_Position = vec4(position, 1.0) *  modelMatrix * viewMatrix * projectionMatrix;

    //passthrough
    fTexLayer = aTexLayer;

    vec2 texCoords[4] = vec2[4](
        vec2(0.0f, 0.0f),
        vec2(1.0f, 0.0f),
        vec2(0.0f, 1.0f),
        vec2(1.0f, 1.0f)
    );

    ftexCoords = texCoords[int(aTexCoord)];


}
