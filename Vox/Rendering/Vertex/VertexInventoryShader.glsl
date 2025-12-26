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
void main()
{
    // Just a dummy output position (replace with your actual code)
    gl_Position = vec4(0.0);
}