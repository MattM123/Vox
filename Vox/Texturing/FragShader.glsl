#version 410 core

uniform sampler2DArray texture_sampler;

flat in float fTexLayer;
in vec2 ftexCoords;
out vec4 color;

void main()
{
   color = vec4(texture(texture_sampler, vec3(ftexCoords.xy, fTexLayer)));
}