
const int POS_Z = 0;
const int NEG_Z = 1;
const int POS_X = 2;
const int NEG_X = 3;
const int POS_Y = 4;
const int NEG_Y = 5;

//Vertex winding order
const int texCoordMap[6][4] = int[6][4](
    int[4](2, 3, 0, 1), // +Z = North = 0 
    int[4](2, 3, 0, 1), // -Z = South = 1 
    int[4](3, 1, 2, 0), // +X = East = 2 
    int[4](3, 1, 2, 0), // -X = West = 3 
    int[4](3, 1, 2, 0), // +Y = Up = 4
    int[4](3, 1, 2, 0)  // -Y = Down = 5
);

//Texture coord map
vec3 GetCornerOffset(int vertexID, int direction) {
    vec2 offsets[4] = vec2[](
        vec2(0.0, 0.0), // bottom-left
        vec2(1.0, 0.0), // bottom-right
        vec2(0.0, 1.0), // top-left
        vec2(1.0, 1.0)  // top-right
    );

    vec2 offset = offsets[vertexID];

    // Directions:
    // 0: +Z (South), 1: -Z (North), 2: +X (East), 3: -X (West), 4: +Y (Top), 5: -Y (Bottom)
    if (direction == 0)       return vec3(offset.x, offset.y, 1.0);             // +Z
    else if (direction == 1)  return vec3(1.0 - offset.x, offset.y, 0.0);       // -Z
    else if (direction == 2)  return vec3(1.0, offset.x, offset.y);             // +X
    else if (direction == 3)  return vec3(0.0, offset.x, 1.0 - offset.y);       // -X
    else if (direction == 4)  return vec3(offset.x, 1.0, 1.0 - offset.y);       // +Y (top)
    else if (direction == 5)  return vec3(offset.x, 0.0, offset.y);             // -Y (bottom)
}

vec3 GetNormal(int faceDirection)
{
    if (faceDirection == 0) return vec3(0.0, 0.0, 1.0);  // +Z
    if (faceDirection == 1) return vec3(0.0, 0.0, -1.0); // -Z
    if (faceDirection == 2) return vec3(1.0, 0.0, 0.0);  // +X
    if (faceDirection == 3) return vec3(-1.0, 0.0, 0.0); // -X
    if (faceDirection == 4) return vec3(0.0, 1.0, 0.0);  // +Y
    if (faceDirection == 5) return vec3(0.0, -1.0, 0.0); // -Y
}