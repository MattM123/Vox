#version 430 core

layout(points) in;
layout(line_strip, max_vertices = 24) out; // 12 lines, 2 vertices each

uniform mat4 viewMatrix;
uniform mat4 projectionMatrix;
uniform vec3 playerMin;
uniform vec3 playerMax;

void EmitEdge(vec3 a, vec3 b)
{
    gl_Position = (viewMatrix * projectionMatrix) * vec4(a, 1.0);
    EmitVertex();
    gl_Position = (viewMatrix * projectionMatrix) * vec4(b, 1.0);
    EmitVertex();
    EndPrimitive();
}

void main()
{
    // 8 corners of the cuboid
    vec3 c[8];
    c[0] = vec3(playerMin.x, playerMin.y, playerMin.z);
    c[1] = vec3(playerMax.x, playerMin.y, playerMin.z);
    c[2] = vec3(playerMax.x, playerMax.y, playerMin.z);
    c[3] = vec3(playerMin.x, playerMax.y, playerMin.z);
    c[4] = vec3(playerMin.x, playerMin.y, playerMax.z);
    c[5] = vec3(playerMax.x, playerMin.y, playerMax.z);
    c[6] = vec3(playerMax.x, playerMax.y, playerMax.z);
    c[7] = vec3(playerMin.x, playerMax.y, playerMax.z);

    // 12 edges of a cube
    EmitEdge(c[0], c[1]);
    EmitEdge(c[1], c[2]);
    EmitEdge(c[2], c[3]);
    EmitEdge(c[3], c[0]);

    EmitEdge(c[4], c[5]);
    EmitEdge(c[5], c[6]);
    EmitEdge(c[6], c[7]);
    EmitEdge(c[7], c[4]);

    EmitEdge(c[0], c[4]);
    EmitEdge(c[1], c[5]);
    EmitEdge(c[2], c[6]);
    EmitEdge(c[3], c[7]);
}