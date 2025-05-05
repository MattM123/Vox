#version 430 core

struct BlockFaceInstance
{
    vec3 facePosition;  
    float padding;       
    int faceDirection;   
    int textureLayer;       
    
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

out vec4 fragPosLightSpace;
out vec4 fColor;
out vec2 ftexCoords;
out vec3 fforwardDir;
out vec3 fragPos;
out vec3 fnormal;
out vec4 fTargetVertex;
flat out float fTexLayer;

uniform vec3 blockCenter;
uniform vec3 curPos;
uniform vec3 localHit;


const int POS_Z = 0;
const int NEG_Z = 1;
const int POS_X = 2;
const int NEG_X = 3;
const int POS_Y = 4;
const int NEG_Y = 5;

const int texCoordMap[6][4] = int[6][4](
    int[4](3, 1, 2, 0), // +Z = North = 0
    int[4](3, 2, 1, 0), // -Z = South = 1
    int[4](2, 3, 0, 1), // +X = East = 2
    int[4](0, 1, 2, 3), // -X = West = 3
    int[4](3, 1, 2, 0), // +Y = Up = 4
    int[4](3, 1, 2, 0)  // -Y = Down = 5
);

vec3 GetCornerOffset(int vertexID, int direction) {
    // Define offsets for the 4 corners of the face
    vec2 offsets[4] = vec2[](
       vec2(0.0, 0.0), // bottom-left
       vec2(1.0, 0.0), // bottom-right
       vec2(0.0, 1.0), // top-left
       vec2(1.0, 1.0)  // top-right
   );
        
    vec2 offset2D = offsets[vertexID];

    // Convert 2D offsets to 3D face orientation
    if (direction == 0)       return vec3(offset2D.x, offset2D.y, 0); // +Z 
    else if (direction == 1)  return vec3(-offset2D.x, offset2D.y, 0); // -Z 
    else if (direction == 2)  return vec3(0, offset2D.x, offset2D.y); // +X 
    else if (direction == 3)  return vec3(0, offset2D.x, -offset2D.y); // -X 
    else if (direction == 4)  return vec3(offset2D.x, 0, offset2D.y); // +Y
    else                      return vec3(offset2D.x, 0, -offset2D.y); // -Y              
}

vec3 GetNormal(int faceDirection)
{
    if (faceDirection == 0) return vec3(0.0, 0.0, 1.0);  // +Z
    if (faceDirection == 1) return vec3(0.0, 0.0, -1.0); // -Z
    if (faceDirection == 2) return vec3(1.0, 0.0, 0.0);  // +X
    if (faceDirection == 3) return vec3(-1.0, 0.0, 0.0); // -X
    if (faceDirection == 4) return vec3(0.0, 1.0, 0.0);  // +Y
    return vec3(0.0, -1.0, 0.0);                         // -Y
}

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
    if (dist > (chunkSize * renderDistance - 5) && isMenuRendered == 0) {
        gl_Position = vec4(2.0, 2.0, 2.0, 1.0); // Cull by moving out of view
    } else {

        //passthrough
        fTexLayer = instance.textureLayer;

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
