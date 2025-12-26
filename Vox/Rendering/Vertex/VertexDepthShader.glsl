#version 410 core

layout(location = 0) in vec3 position;
layout(location = 1) in int aTexLayer;
layout(location = 2) in int aTexCoord;
layout(location = 3) in int aLight;
layout(location = 4) in vec3 aNormal;
layout(location = 5) in int aBlocktype;
layout(location = 6) in int aFace;

uniform mat4 lightProjMatrix;
uniform mat4 lightModel;
uniform mat4 lightViewMatrix;

void main()
{
    gl_Position = vec4(position, 1.0) * lightModel * lightViewMatrix * lightProjMatrix;
   
}  