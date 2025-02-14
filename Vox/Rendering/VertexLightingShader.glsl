#version 410 core

layout(location = 0) in vec3 position;
layout(location = 1) in int alight;

uniform mat4 viewMatrix;
uniform mat4 modelMatrix;
uniform mat4 projectionMatrix;
uniform mat4 chunkModelMatrix;
uniform int isMenuRendered;

uniform int renderDistance;
uniform vec3 playerPos;
uniform int chunkSize;

out vec3 fragPos;
flat out int flight;

void main() {


    if (isMenuRendered == 1) {
        gl_Position = vec4(position, 1.0) * modelMatrix * viewMatrix * projectionMatrix;
        fragPos = vec3(vec4(position, 1.0) * modelMatrix);
    }
    else {
        gl_Position =   vec4(position, 1.0) * chunkModelMatrix * viewMatrix * projectionMatrix;
        fragPos =       (chunkModelMatrix * vec4(position, 1.0)).xyz;
    }

    //Render partial mesh if chunk is on the edge of render distance by
    //calculating the distance between the current vertex position and the player's position
    float dist = distance(position.xz, playerPos.xz);

    if (dist > (chunkSize * renderDistance - 5) && isMenuRendered == 0) {
        gl_Position = vec4(2.0, 2.0, 2.0, 1.0); // Cull by moving out of view
    } else {
        flight = alight;
    }
}