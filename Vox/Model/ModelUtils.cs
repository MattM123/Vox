

using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Vox.Model
{
    static class ModelUtils
    {

        //Gets face vertices ready to send to the shaders for rendering
        public static float[] GetCuboidFace(BlockModel model, string face)
        {
            BlockModel model90 = model.RotateX(90);
            BlockModel model180 = model.RotateX(180);
            BlockModel model270 = model.RotateX(270);

            Element modelEle = model.GetElements().ToList().ElementAt(0);
            Element modelEle90 = model90.GetElements().ToList().ElementAt(0);
            Element modelEle180 = model180.GetElements().ToList().ElementAt(0);
            Element modelEle270 = model270.GetElements().ToList().ElementAt(0);

            switch (face)
            {
                case "south":
                case "front":
                {
                    Vector3 normal = CalculateNormal(new Vector3(modelEle90.x1, modelEle90.y1, modelEle90.z1), new Vector3(modelEle.x1, modelEle.y1, modelEle.z1) , 
                        new Vector3(modelEle180.x1, modelEle180.y1, modelEle180.z1));

                    return [
                        //Position (X, Y, Z)                                 Texture Layer                  Tex Coord Idx | Normal X, Y , Z
                        modelEle90.x1, modelEle90.y1, modelEle90.z1,         (float)model90.GetTexture("south"),   3f,      normal.X, normal.Y, normal.Z,    //top right                      
                        modelEle.x1, modelEle.y1, modelEle.z1,               (float)model.GetTexture("south"),     2f,      normal.X, normal.Y, normal.Z,    //top left
                        modelEle180.x1, modelEle180.y1, modelEle180.z1,      (float)model180.GetTexture("south"),  1f,      normal.X, normal.Y, normal.Z,    //bottom right     
                        modelEle270.x1, modelEle270.y1, modelEle270.z1,      (float)model270.GetTexture("south"),  0f,      normal.X, normal.Y, normal.Z,    //bottom left
                    ];                                                                                                      
                }                                                                                                           
                case "north":                                                                                               
                case "back":                                                                                                
                {
                    Vector3 normal = CalculateNormal(new Vector3(modelEle270.x2, modelEle270.y2, modelEle270.z2), new Vector3(modelEle.x2, modelEle.y2, modelEle.z2),
                        new Vector3(modelEle180.x2, modelEle180.y2, modelEle180.z2));

                    return [                                                                                                
                        modelEle270.x2, modelEle270.y2, modelEle270.z2,      (float)model270.GetTexture("north"),  3f,      normal.X, normal.Y, normal.Z,
                        modelEle.x2, modelEle.y2, modelEle.z2,               (float)model.GetTexture("north"),     1f,      normal.X, normal.Y, normal.Z,
                        modelEle180.x2, modelEle180.y2, modelEle180.z2,      (float)model180.GetTexture("north"),  2f,      normal.X, normal.Y, normal.Z,
                        modelEle90.x2, modelEle90.y2, modelEle90.z2,         (float)model90.GetTexture("north"),   0f,      normal.X, normal.Y, normal.Z,
                    ];                                                                                                      
                }                                                                                                           
                case "top":                                                                                                 
                case "up":                                                                                                  
                {
                    Vector3 normal = CalculateNormal(new Vector3(modelEle180.x1, modelEle180.y1, modelEle180.z1), new Vector3(modelEle.x2, modelEle.y2, modelEle.z2),
                        new Vector3(modelEle270.x1, modelEle270.y1, modelEle270.z1));

                    return [                                                                                                
                        modelEle180.x1, modelEle180.y1, modelEle180.z1,      (float)model180.GetTexture("up"),     3f,      normal.X, normal.Y, normal.Z,
                        modelEle.x2, modelEle.y2, modelEle.z2,               (float)model.GetTexture("up"),        1f,      normal.X, normal.Y, normal.Z,
                        modelEle270.x1, modelEle270.y1, modelEle270.z1,      (float)model270.GetTexture("up"),     2f,      normal.X, normal.Y, normal.Z,
                        modelEle90.x2, modelEle90.y2, modelEle90.z2,         (float)model90.GetTexture("up"),      0f,      normal.X, normal.Y, normal.Z,
                                                                                                                            
                    ];                                                                                                      
                }                                                                                                           
                case "bottom":                                                                                              
                case "down":                                                                                                
                {
                    Vector3 normal = CalculateNormal(new Vector3(modelEle90.x1, modelEle90.y1, modelEle90.z1), new Vector3(modelEle270.x2, modelEle270.y2, modelEle270.z2),
                        new Vector3(modelEle.x1, modelEle.y1, modelEle.z1));

                    return [                                                                                                
                        modelEle90.x1, modelEle90.y1, modelEle90.z1,         (float)model90.GetTexture("down"),    3f,      normal.X, normal.Y, normal.Z,
                        modelEle270.x2, modelEle270.y2, modelEle270.z2,      (float)model270.GetTexture("down"),   1f,      normal.X, normal.Y, normal.Z,
                        modelEle.x1, modelEle.y1, modelEle.z1,               (float)model.GetTexture("down"),      2f,      normal.X, normal.Y, normal.Z,
                        modelEle180.x2, modelEle180.y2, modelEle180.z2,      (float)model180.GetTexture("down"),   0f,      normal.X, normal.Y, normal.Z,
                                                                                                                            
                    ];                                                                                                      
                }                                                                                                           
                case "west":                                                                                                
                case "left":                                                                                                
                {
                    Vector3 normal = CalculateNormal(new Vector3(modelEle90.x2, modelEle90.y2, modelEle90.z2), new Vector3(modelEle270.x1, modelEle270.y1, modelEle270.z1),
                        new Vector3(modelEle180.x2, modelEle180.y2, modelEle180.z2));

                    return [                                                                                                
                        modelEle90.x2, modelEle90.y2, modelEle90.z2,         (float)model90.GetTexture("west"),    0f,      normal.X, normal.Y, normal.Z,
                        modelEle270.x1, modelEle270.y1, modelEle270.z1,      (float)model270.GetTexture("west"),   1f,      normal.X, normal.Y, normal.Z,
                        modelEle180.x2, modelEle180.y2, modelEle180.z2,      (float)model180.GetTexture("west"),   2f,      normal.X, normal.Y, normal.Z,
                        modelEle.x1, modelEle.y1, modelEle.z1,               (float)model.GetTexture("west"),      3f,      normal.X, normal.Y, normal.Z,
                                                                                                                            
                    ];                                                                                                      
                }                                                                                                           
                case "east":                                                                                                
                case "right":                                                                                               
                {
                    Vector3 normal = CalculateNormal(new Vector3(modelEle90.x1, modelEle90.y1, modelEle90.z1), new Vector3(modelEle270.x2, modelEle270.y2, modelEle270.z2),
                        new Vector3(modelEle180.x1, modelEle180.y1, modelEle180.z1));

                    return [                                                                                                
                        modelEle90.x1, modelEle90.y1, modelEle90.z1,         (float)model90.GetTexture("east"),    2f,      normal.X, normal.Y, normal.Z,
                        modelEle270.x2, modelEle270.y2, modelEle270.z2,      (float)model270.GetTexture("east"),   3f,      normal.X, normal.Y, normal.Z,
                        modelEle180.x1, modelEle180.y1, modelEle180.z1,      (float)model180.GetTexture("east"),   0f,      normal.X, normal.Y, normal.Z,
                        modelEle.x2, modelEle.y2, modelEle.z2,               (float)model.GetTexture("east"),      1f,      normal.X, normal.Y, normal.Z,

                    ];
                }

                default:
                    Logger.Warn("Error calculating vertices");
                    return [0, 0, 0, 0, 0];
                    
            }
        }

        //Gets offset face vertices ready to send to the shaders for rendering
        public static float[] GetCuboidFace(BlockModel model, string face, Vector3 blockLoc)
        {
            float bLocX = blockLoc.X;
            float bLocY = blockLoc.Y;
            float bLocZ = blockLoc.Z;

            BlockModel model90 = model.RotateX(90);
            BlockModel model180 = model.RotateX(180);
            BlockModel model270 = model.RotateX(270);

            Element modelEle = model.GetElements().ToList().ElementAt(0);
            Element modelEle90 = model90.GetElements().ToList().ElementAt(0);
            Element modelEle180 = model180.GetElements().ToList().ElementAt(0);
            Element modelEle270 = model270.GetElements().ToList().ElementAt(0);

            switch (face)
            {
                case "south":
                case "front":
                    {
                        Vector3 normal = CalculateNormal(new(bLocX + modelEle90.x1 / 16, bLocY + modelEle90.y1 / 16, bLocZ + modelEle90.z1 / 16),
                            new(bLocX + modelEle.x1 / 16, bLocY + modelEle.y1 / 16, bLocZ + modelEle.z1 / 16),
                            new(bLocX + modelEle180.x1 / 16, bLocY + modelEle180.y1 / 16, bLocZ + modelEle180.z1 / 16));

                        return [
                        //Position (X, Y, Z)                                                                              Texture Layer             Tex Coord Idx | Normal X, Y , Z
                        bLocX + modelEle90.x1  / 16,     bLocY + modelEle90.y1 / 16,     bLocZ + modelEle90.z1 / 16,      (float)model90.GetTexture("south"),   3f, normal.X, normal.Y, normal.Z,    //top right                      
                        bLocX + modelEle.x1    / 16,       bLocY + modelEle.y1 / 16,       bLocZ + modelEle.z1 / 16,      (float)model.GetTexture("south"),     2f, normal.X, normal.Y, normal.Z,    //top left
                        bLocX + modelEle180.x1 / 16,    bLocY + modelEle180.y1 / 16,    bLocZ + modelEle180.z1 / 16,      (float)model180.GetTexture("south"),  1f, normal.X, normal.Y, normal.Z,    //bottom right     
                        bLocX + modelEle270.x1 / 16,    bLocY + modelEle270.y1 / 16,    bLocZ + modelEle270.z1 / 16,      (float)model270.GetTexture("south"),  0f, normal.X, normal.Y, normal.Z,    //bottom left
                    ];
                    }
                case "north":
                case "back":
                    {
                        Vector3 normal = CalculateNormal(new(bLocX + modelEle270.x2 / 16, bLocY + modelEle270.y2 / 16, bLocZ + modelEle270.z2 / 16),
                            new(bLocX + modelEle.x2 / 16, bLocY + modelEle.y2 / 16, bLocZ + modelEle.z2 / 16),
                            new(bLocX + modelEle180.x2 / 16, bLocY + modelEle180.y2 / 16, bLocZ + modelEle180.z2 / 16));

                        return [
                        bLocX + modelEle270.x2 / 16,    bLocY + modelEle270.y2 / 16,    bLocZ + modelEle270.z2 / 16,      (float)model270.GetTexture("north"),  3f, normal.X, normal.Y, normal.Z, 
                        bLocX + modelEle.x2    / 16,       bLocY + modelEle.y2 / 16,       bLocZ + modelEle.z2 / 16,         (float)model.GetTexture("north"),  1f, normal.X, normal.Y, normal.Z, 
                        bLocX + modelEle180.x2 / 16,    bLocY + modelEle180.y2 / 16,    bLocZ + modelEle180.z2 / 16,      (float)model180.GetTexture("north"),  2f, normal.X, normal.Y, normal.Z, 
                        bLocX + modelEle90.x2  / 16,     bLocY + modelEle90.y2 / 16,     bLocZ + modelEle90.z2 / 16,       (float)model90.GetTexture("north"),  0f, normal.X, normal.Y, normal.Z,
                    ];
                    }
                case "top":
                case "up":
                    {
                        Vector3 normal = CalculateNormal(new(bLocX + modelEle180.x1 / 16, bLocY + modelEle180.y1 / 16, bLocZ + modelEle180.z1 / 16),
                            new(bLocX + modelEle.x2 / 16, bLocY + modelEle.y2 / 16, bLocZ + modelEle.z2 / 16),
                            new(bLocX + modelEle270.x1 / 16, bLocY + modelEle270.y1 / 16, bLocZ + modelEle270.z1 / 16));

                        return [
                        bLocX + modelEle180.x1 / 16,    bLocY + modelEle180.y1 / 16,    bLocZ + modelEle180.z1 / 16,      (float)model180.GetTexture("up"),     3f, normal.X, normal.Y, normal.Z, 
                        bLocX + modelEle.x2    / 16,       bLocY + modelEle.y2 / 16,       bLocZ + modelEle.z2 / 16,         (float)model.GetTexture("up"),     1f, normal.X, normal.Y, normal.Z, 
                        bLocX + modelEle270.x1 / 16,    bLocY + modelEle270.y1 / 16,    bLocZ + modelEle270.z1 / 16,      (float)model270.GetTexture("up"),     2f, normal.X, normal.Y, normal.Z, 
                        bLocX + modelEle90.x2  / 16,     bLocY + modelEle90.y2 / 16,     bLocZ + modelEle90.z2 / 16,       (float)model90.GetTexture("up"),     0f, normal.X, normal.Y, normal.Z,
                    ];
                    }
                case "bottom":
                case "down":
                    {
                        Vector3 normal = CalculateNormal(new(bLocX + modelEle90.x1 / 16, bLocY + modelEle90.y1 / 16, bLocZ + modelEle90.z1 / 16),
                            new(bLocX + modelEle270.x2 / 16, bLocY + modelEle270.y2 / 16, bLocZ + modelEle270.z2 / 16),
                            new(bLocX + modelEle.x1 / 16, bLocY + modelEle.y1 / 16, bLocZ + modelEle.z1 / 16));

                        return [
                        bLocX + modelEle90.x1  / 16,     bLocY + modelEle90.y1 / 16,     bLocZ + modelEle90.z1 / 16,       (float)model90.GetTexture("down"),   3f, normal.X, normal.Y, normal.Z, 
                        bLocX + modelEle270.x2 / 16,    bLocY + modelEle270.y2 / 16,    bLocZ + modelEle270.z2 / 16,      (float)model270.GetTexture("down"),   1f, normal.X, normal.Y, normal.Z, 
                        bLocX + modelEle.x1    / 16,       bLocY + modelEle.y1 / 16,       bLocZ + modelEle.z1 / 16,         (float)model.GetTexture("down"),   2f, normal.X, normal.Y, normal.Z, 
                        bLocX + modelEle180.x2 / 16,    bLocY + modelEle180.y2 / 16,    bLocZ + modelEle180.z2 / 16,      (float)model180.GetTexture("down"),   0f, normal.X, normal.Y, normal.Z,

                    ];
                    }
                case "west":
                case "left":
                    {
                        Vector3 normal = CalculateNormal(new(bLocX + modelEle90.x2 / 16, bLocY + modelEle90.y2 / 16, bLocZ + modelEle90.z2 / 16),
                            new(bLocX + modelEle270.x1 / 16, bLocY + modelEle270.y1 / 16, bLocZ + modelEle270.z1 / 16),
                            new(bLocX + modelEle180.x2 / 16, bLocY + modelEle180.y2 / 16, bLocZ + modelEle180.z2 / 16));

                        return [
                        bLocX + modelEle90.x2  / 16,     bLocY + modelEle90.y2 / 16,     bLocZ + modelEle90.z2 / 16,       (float)model90.GetTexture("west"),   0f, normal.X, normal.Y, normal.Z, 
                        bLocX + modelEle270.x1 / 16,    bLocY + modelEle270.y1 / 16,    bLocZ + modelEle270.z1 / 16,      (float)model270.GetTexture("west"),   1f, normal.X, normal.Y, normal.Z, 
                        bLocX + modelEle180.x2 / 16,    bLocY + modelEle180.y2 / 16,    bLocZ + modelEle180.z2 / 16,      (float)model180.GetTexture("west"),   2f, normal.X, normal.Y, normal.Z, 
                        bLocX + modelEle.x1    / 16,       bLocY + modelEle.y1 / 16,       bLocZ + modelEle.z1 / 16,         (float)model.GetTexture("west"),   3f, normal.X, normal.Y, normal.Z,
                    ];
                    }
                case "east":
                case "right":
                    {
                        Vector3 normal = CalculateNormal(new(bLocX + modelEle90.x1 / 16, bLocY + modelEle90.y1 / 16, bLocZ + modelEle90.z1 / 16),
                            new(bLocX + modelEle270.x2 / 16, bLocY + modelEle270.y2 / 16, bLocZ + modelEle270.z2 / 16),
                            new(bLocX + modelEle180.x1 / 16, bLocY + modelEle180.y1 / 16, bLocZ + modelEle180.z1 / 16));

                        return [
                        bLocX + modelEle90.x1  / 16,     bLocY + modelEle90.y1 / 16,     bLocZ + modelEle90.z1 / 16,       (float)model90.GetTexture("east"),   2f, normal.X, normal.Y, normal.Z, 
                        bLocX + modelEle270.x2 / 16,    bLocY + modelEle270.y2 / 16,    bLocZ + modelEle270.z2 / 16,      (float)model270.GetTexture("east"),   3f, normal.X, normal.Y, normal.Z, 
                        bLocX + modelEle180.x1 / 16,    bLocY + modelEle180.y1 / 16,    bLocZ + modelEle180.z1 / 16,      (float)model180.GetTexture("east"),   0f, normal.X, normal.Y, normal.Z, 
                        bLocX + modelEle.x2    / 16,       bLocY + modelEle.y2 / 16,       bLocZ + modelEle.z2 / 16,         (float)model.GetTexture("east"),   1f, normal.X, normal.Y, normal.Z,

                    ];
                    }

                default:
                    return [1f, 1f, 1f, 1f, 1f];

            }
        }


        // Method to calculate normal for a face
        public static Vector3 CalculateNormal(Vector3 v0, Vector3 v1, Vector3 v2)
        {
            // Compute the edges
            Vector3 edge1 = v1 - v0;
            Vector3 edge2 = v2 - v0;

            // Cross product of the two edges
            Vector3 normal = Vector3.Cross(edge1, edge2);

            // Normalize the result to get a unit vector
            normal = Vector3.Normalize(normal);

            return normal;
        }
    }
}
