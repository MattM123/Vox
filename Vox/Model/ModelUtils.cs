

using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Vox.Genesis;
using Vox.Rendering;

namespace Vox.Model
{
    static class ModelUtils
    {

        //Gets face vertices ready to send to the shaders for rendering
        /*
        public static Vertex[] GetCuboidFace(BlockModel model, string face)
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
                        //Position (X, Y, Z)                                        Texture Layer                  Tex Coord Idx | Normal X, Y , Z
                        new Vertex(modelEle90.x1, modelEle90.y1, modelEle90.z1,     model90.GetTexture(Face.SOUTH),   3,              normal.X, normal.Y, normal.Z),    //top right                      
                        new Vertex(modelEle.x1, modelEle.y1, modelEle.z1,           model.GetTexture(Face.SOUTH),     2,              normal.X, normal.Y, normal.Z),    //top left
                        new Vertex(modelEle180.x1, modelEle180.y1, modelEle180.z1,  model180.GetTexture(Face.SOUTH),  1,              normal.X, normal.Y, normal.Z),    //bottom right     
                        new Vertex(modelEle270.x1, modelEle270.y1, modelEle270.z1,  model270.GetTexture(Face.SOUTH),  0,              normal.X, normal.Y, normal.Z),    //bottom left
                    ];                                                                                                      
                }                                                                                                           
                case "north":                                                                                               
                case "back":                                                                                                
                {
                    Vector3 normal = CalculateNormal(new Vector3(modelEle270.x2, modelEle270.y2, modelEle270.z2), new Vector3(modelEle.x2, modelEle.y2, modelEle.z2),
                        new Vector3(modelEle180.x2, modelEle180.y2, modelEle180.z2));

                    return [                                                                                                
                       new Vertex(modelEle270.x2, modelEle270.y2, modelEle270.z2,   model270.GetTexture(Face.NORTH),  3,      normal.X, normal.Y, normal.Z),
                       new Vertex(modelEle.x2, modelEle.y2, modelEle.z2,            model.GetTexture(Face.NORTH),     1,      normal.X, normal.Y, normal.Z),
                       new Vertex(modelEle180.x2, modelEle180.y2, modelEle180.z2,   model180.GetTexture(Face.NORTH),  2,      normal.X, normal.Y, normal.Z),
                       new Vertex(modelEle90.x2, modelEle90.y2, modelEle90.z2,      model90.GetTexture(Face.NORTH),   0,      normal.X, normal.Y, normal.Z),
                    ];                                                                                                      
                }                                                                                                           
                case "top":                                                                                                 
                case "up":                                                                                                  
                {
                    Vector3 normal = CalculateNormal(new Vector3(modelEle180.x1, modelEle180.y1, modelEle180.z1), new Vector3(modelEle.x2, modelEle.y2, modelEle.z2),
                        new Vector3(modelEle270.x1, modelEle270.y1, modelEle270.z1));

                    return [                                                                                                
                        new Vertex(modelEle180.x1, modelEle180.y1, modelEle180.z1,  model180.GetTexture(Face.UP),     3,      normal.X, normal.Y, normal.Z),
                        new Vertex(modelEle.x2, modelEle.y2, modelEle.z2,           model.GetTexture(Face.UP),        1,      normal.X, normal.Y, normal.Z),
                        new Vertex(modelEle270.x1, modelEle270.y1, modelEle270.z1,  model270.GetTexture(Face.UP),     2,      normal.X, normal.Y, normal.Z),
                        new Vertex(modelEle90.x2, modelEle90.y2, modelEle90.z2,     model90.GetTexture(Face.UP),      0,      normal.X, normal.Y, normal.Z),
                                                                                                                            
                    ];                                                                                                      
                }                                                                                                           
                case "bottom":                                                                                              
                case "down":                                                                                                
                {
                    Vector3 normal = CalculateNormal(new Vector3(modelEle90.x1, modelEle90.y1, modelEle90.z1), new Vector3(modelEle270.x2, modelEle270.y2, modelEle270.z2),
                        new Vector3(modelEle.x1, modelEle.y1, modelEle.z1));
                         
                    return [                                                                                                
                        new Vertex(modelEle90.x1, modelEle90.y1, modelEle90.z1,     model90.GetTexture(Face.DOWN),    3,      normal.X, normal.Y, normal.Z),
                        new Vertex(modelEle270.x2, modelEle270.y2, modelEle270.z2,  model270.GetTexture(Face.DOWN),   1,      normal.X, normal.Y, normal.Z),
                        new Vertex(modelEle.x1, modelEle.y1, modelEle.z1,           model.GetTexture(Face.DOWN),      2,      normal.X, normal.Y, normal.Z),
                        new Vertex(modelEle180.x2, modelEle180.y2, modelEle180.z2,  model180.GetTexture(Face.DOWN),   0,      normal.X, normal.Y, normal.Z),
                                                                                                                            
                    ];                                                                                                      
                }                                                                                                           
                case "west":                                                                                                
                case "left":                                                                                                
                {
                    Vector3 normal = CalculateNormal(new Vector3(modelEle90.x2, modelEle90.y2, modelEle90.z2), new Vector3(modelEle270.x1, modelEle270.y1, modelEle270.z1),
                        new Vector3(modelEle180.x2, modelEle180.y2, modelEle180.z2));

                    return [                                                                                                
                        new Vertex(modelEle90.x2, modelEle90.y2, modelEle90.z2,     model90.GetTexture(Face.WEST),    0,      normal.X, normal.Y, normal.Z),
                        new Vertex(modelEle270.x1, modelEle270.y1, modelEle270.z1,  model270.GetTexture(Face.WEST),   1,      normal.X, normal.Y, normal.Z),
                        new Vertex(modelEle180.x2, modelEle180.y2, modelEle180.z2,  model180.GetTexture(Face.WEST),   2,      normal.X, normal.Y, normal.Z),
                        new Vertex(modelEle.x1, modelEle.y1, modelEle.z1,           model.GetTexture(Face.WEST),      3,      normal.X, normal.Y, normal.Z),
                                                                                                                            
                    ];                                                                                                      
                }                                                                                                           
                case "east":                                                                                                
                case "right":                                                                                               
                {
                    Vector3 normal = CalculateNormal(new Vector3(modelEle90.x1, modelEle90.y1, modelEle90.z1), new Vector3(modelEle270.x2, modelEle270.y2, modelEle270.z2),
                        new Vector3(modelEle180.x1, modelEle180.y1, modelEle180.z1));

                    return [                                                                                     
                        new Vertex(modelEle90.x1, modelEle90.y1, modelEle90.z1,     model90.GetTexture(Face.EAST),    2,      normal.X, normal.Y, normal.Z),
                        new Vertex(modelEle270.x2, modelEle270.y2, modelEle270.z2,  model270.GetTexture(Face.EAST),   3,      normal.X, normal.Y, normal.Z),
                        new Vertex(modelEle180.x1, modelEle180.y1, modelEle180.z1,  model180.GetTexture(Face.EAST),   0,      normal.X, normal.Y, normal.Z),
                        new Vertex(modelEle.x2, modelEle.y2, modelEle.z2,           model.GetTexture(Face.EAST),      1,      normal.X, normal.Y, normal.Z),

                    ];
                }

                default:
                    Logger.Warn("Error calculating vertices");
                    return [
                        new Vertex(modelEle90.x1, modelEle90.y1, modelEle90.z1,     model90.GetTexture(Face.EAST),    2,      1,2,3),
                        new Vertex(modelEle270.x2, modelEle270.y2, modelEle270.z2,  model270.GetTexture(Face.EAST),   3,      1,2,3),
                        new Vertex(modelEle180.x1, modelEle180.y1, modelEle180.z1,  model180.GetTexture(Face.EAST),   0,      1,2,3),
                        new Vertex(modelEle.x2, modelEle.y2, modelEle.z2,           model.GetTexture(Face.EAST),      1,      1,2,3),

                    ];

            }
        }
        */
        //Gets offset face vertices ready to send to the shaders for rendering
        public static Vertex[] GetCuboidFace(BlockModel model, Face face, Vector3 blockLoc, Chunk c)
        {
            float x = blockLoc.X;
            float y = blockLoc.Y;
            float z = blockLoc.Z;

            int lightX = (int)Math.Abs(x) % RegionManager.CHUNK_BOUNDS;
            int lightY = (int)Math.Abs(y) % RegionManager.CHUNK_BOUNDS;
            int lightZ = (int)Math.Abs(z) % RegionManager.CHUNK_BOUNDS;

            BlockModel model90 = model.RotateX(90);
            BlockModel model180 = model.RotateX(180);
            BlockModel model270 = model.RotateX(270);

            Element modelEle = model.GetElements().ToList().ElementAt(0);
            Element modelEle90 = model90.GetElements().ToList().ElementAt(0);
            Element modelEle180 = model180.GetElements().ToList().ElementAt(0);
            Element modelEle270 = model270.GetElements().ToList().ElementAt(0);

            switch (face)
            {
                case Face.SOUTH:
                case Face.FRONT:
                    {
                        Vector3 normal = CalculateNormal(new(x + modelEle90.x1 / 16, y + modelEle90.y1 / 16, z + modelEle90.z1 / 16),
                            new(x + modelEle.x1 / 16, y + modelEle.y1 / 16, z + modelEle.z1 / 16),
                            new(x + modelEle180.x1 / 16, y + modelEle180.y1 / 16, z + modelEle180.z1 / 16));
                        if (c == null)
                            c = new Chunk().Initialize(0, 0);
                        return [
                        //Position (X, Y, Z)                                                                            Texture Layer          ex Coord Idx | Sunlight level                           Normal X, Y , Z
                        new Vertex(x + modelEle90.x1  / 16,    y + modelEle90.y1 / 16,     z + modelEle90.z1 / 16,      model90.GetTexture(Face.SOUTH),   3, c.GetSunlight(lightX, lightY, lightZ),    normal.X, normal.Y, normal.Z, Face.SOUTH),    //top right                      
                        new Vertex(x + modelEle.x1    / 16,    y + modelEle.y1 / 16,       z + modelEle.z1 / 16,        model.GetTexture(Face.SOUTH),     2, c.GetSunlight(lightX, lightY, lightZ),    normal.X, normal.Y, normal.Z, Face.SOUTH),    //top left
                        new Vertex(x + modelEle180.x1 / 16,    y + modelEle180.y1 / 16,    z + modelEle180.z1 / 16,     model180.GetTexture(Face.SOUTH),  1, c.GetSunlight(lightX, lightY, lightZ),    normal.X, normal.Y, normal.Z, Face.SOUTH),    //bottom right     
                        new Vertex(x + modelEle270.x1 / 16,    y + modelEle270.y1 / 16,    z + modelEle270.z1 / 16,     model270.GetTexture(Face.SOUTH),  0, c.GetSunlight(lightX, lightY, lightZ),    normal.X, normal.Y, normal.Z, Face.SOUTH),    //bottom left
                    ];
                    }
                case Face.NORTH:
                case Face.BACK:
                    {
                        Vector3 normal = CalculateNormal(new(x + modelEle270.x2 / 16, y + modelEle270.y2 / 16, z + modelEle270.z2 / 16),
                            new(x + modelEle.x2 / 16, y + modelEle.y2 / 16, z + modelEle.z2 / 16),
                            new(x + modelEle180.x2 / 16, y + modelEle180.y2 / 16, z + modelEle180.z2 / 16));
                        
                        if (c == null)
                            c = new Chunk().Initialize(0, 0);

                        return [
                        new Vertex(x + modelEle270.x2 / 16,    y + modelEle270.y2 / 16,    z + modelEle270.z2 / 16,      model270.GetTexture(Face.NORTH),  3, c.GetSunlight(lightX, lightY, lightZ),    normal.X, normal.Y, normal.Z, Face.NORTH), 
                        new Vertex(x + modelEle.x2    / 16,    y + modelEle.y2 / 16,       z + modelEle.z2 / 16,         model.GetTexture(Face.NORTH),     1, c.GetSunlight(lightX, lightY, lightZ),    normal.X, normal.Y, normal.Z, Face.NORTH), 
                        new Vertex(x + modelEle180.x2 / 16,    y + modelEle180.y2 / 16,    z + modelEle180.z2 / 16,      model180.GetTexture(Face.NORTH),  2, c.GetSunlight(lightX, lightY, lightZ),    normal.X, normal.Y, normal.Z, Face.NORTH),
                        new Vertex(x + modelEle90.x2  / 16,    y + modelEle90.y2 / 16,     z + modelEle90.z2 / 16,       model90.GetTexture(Face.NORTH),   0, c.GetSunlight(lightX, lightY, lightZ),    normal.X, normal.Y, normal.Z, Face.NORTH),
                    ];
                    }
                case Face.TOP:
                case Face.UP:
                    {
                        Vector3 normal = CalculateNormal(new(x + modelEle180.x1 / 16, y + modelEle180.y1 / 16, z + modelEle180.z1 / 16),
                            new(x + modelEle.x2 / 16, y + modelEle.y2 / 16, z + modelEle.z2 / 16),
                            new(x + modelEle270.x1 / 16, y + modelEle270.y1 / 16, z + modelEle270.z1 / 16));

                        if (c == null)
                            c = new Chunk().Initialize(0, 0);

                        return [
                        new Vertex(x + modelEle180.x1 / 16,    y + modelEle180.y1 / 16,    z + modelEle180.z1 / 16,      model180.GetTexture(Face.UP),     3, c.GetSunlight(lightX, lightY, lightZ),    normal.X, normal.Y, normal.Z, Face.UP), 
                        new Vertex(x + modelEle.x2    / 16,    y + modelEle.y2 / 16,       z + modelEle.z2 / 16,         model.GetTexture(Face.UP),        1, c.GetSunlight(lightX, lightY, lightZ),    normal.X, normal.Y, normal.Z, Face.UP), 
                        new Vertex(x + modelEle270.x1 / 16,    y + modelEle270.y1 / 16,    z + modelEle270.z1 / 16,      model270.GetTexture(Face.UP),     2, c.GetSunlight(lightX, lightY, lightZ),    normal.X, normal.Y, normal.Z, Face.UP), 
                        new Vertex(x + modelEle90.x2  / 16,    y + modelEle90.y2 / 16,     z + modelEle90.z2 / 16,       model90.GetTexture(Face.UP),      0, c.GetSunlight(lightX, lightY, lightZ),    normal.X, normal.Y, normal.Z, Face.UP),
                    ];
                    }
                case Face.BOTTOM:
                case Face.DOWN:
                    {
                        Vector3 normal = CalculateNormal(new(x + modelEle90.x1 / 16, y + modelEle90.y1 / 16, z + modelEle90.z1 / 16),
                            new(x + modelEle270.x2 / 16, y + modelEle270.y2 / 16, z + modelEle270.z2 / 16),
                            new(x + modelEle.x1 / 16, y + modelEle.y1 / 16, z + modelEle.z1 / 16));

                        if (c == null)
                            c = new Chunk().Initialize(0, 0);

                        return [
                        new Vertex(x + modelEle90.x1  / 16,    y + modelEle90.y1 / 16,     z + modelEle90.z1 / 16,      model90.GetTexture(Face.DOWN),    3, c.GetSunlight(lightX, lightY, lightZ),     normal.X, normal.Y, normal.Z, Face.DOWN), 
                        new Vertex(x + modelEle270.x2 / 16,    y + modelEle270.y2 / 16,    z + modelEle270.z2 / 16,     model270.GetTexture(Face.DOWN),   1, c.GetSunlight(lightX, lightY, lightZ),     normal.X, normal.Y, normal.Z, Face.DOWN), 
                        new Vertex(x + modelEle.x1    / 16,    y + modelEle.y1 / 16,       z + modelEle.z1 / 16,        model.GetTexture(Face.DOWN),      2, c.GetSunlight(lightX, lightY, lightZ),     normal.X, normal.Y, normal.Z, Face.DOWN), 
                        new Vertex(x + modelEle180.x2 / 16,    y + modelEle180.y2 / 16,    z + modelEle180.z2 / 16,     model180.GetTexture(Face.DOWN),   0, c.GetSunlight(lightX, lightY, lightZ),     normal.X, normal.Y, normal.Z, Face.DOWN),

                    ];
                    }
                case Face.WEST:
                case Face.LEFT:
                    {
                        Vector3 normal = CalculateNormal(new(x + modelEle90.x2 / 16, y + modelEle90.y2 / 16, z + modelEle90.z2 / 16),
                            new(x + modelEle270.x1 / 16, y + modelEle270.y1 / 16, z + modelEle270.z1 / 16),
                            new(x + modelEle180.x2 / 16, y + modelEle180.y2 / 16, z + modelEle180.z2 / 16));

                        if (c == null)
                            c = new Chunk().Initialize(0, 0);

                        return [
                        new Vertex(x + modelEle90.x2  / 16,    y + modelEle90.y2 / 16,     z + modelEle90.z2 / 16,       model90.GetTexture(Face.WEST),    0, c.GetSunlight(lightX, lightY, lightZ),     normal.X, normal.Y, normal.Z, Face.WEST), 
                        new Vertex(x + modelEle270.x1 / 16,    y + modelEle270.y1 / 16,    z + modelEle270.z1 / 16,      model270.GetTexture(Face.WEST),   1, c.GetSunlight(lightX, lightY, lightZ),     normal.X, normal.Y, normal.Z, Face.WEST), 
                        new Vertex(x + modelEle180.x2 / 16,    y + modelEle180.y2 / 16,    z + modelEle180.z2 / 16,      model180.GetTexture(Face.WEST),   2, c.GetSunlight(lightX, lightY, lightZ),     normal.X, normal.Y, normal.Z, Face.WEST), 
                        new Vertex(x + modelEle.x1    / 16,    y + modelEle.y1 / 16,       z + modelEle.z1 / 16,         model.GetTexture(Face.WEST),      3, c.GetSunlight(lightX, lightY, lightZ),     normal.X, normal.Y, normal.Z, Face.WEST),
                    ]; 
                    }
                case Face.EAST:
                case Face.RIGHT:
                    {
                        Vector3 normal = CalculateNormal(new(x + modelEle90.x1 / 16, y + modelEle90.y1 / 16, z + modelEle90.z1 / 16),
                            new(x + modelEle270.x2 / 16, y + modelEle270.y2 / 16, z + modelEle270.z2 / 16),
                            new(x + modelEle180.x1 / 16, y + modelEle180.y1 / 16, z + modelEle180.z1 / 16));

                        if (c == null)
                            c = new Chunk().Initialize(0, 0);

                        return [
                        new Vertex(x + modelEle90.x1  / 16,    y + modelEle90.y1 / 16,     z + modelEle90.z1 / 16,       model90.GetTexture(Face.EAST),    2, c.GetSunlight(lightX, lightY, lightZ),    normal.X, normal.Y, normal.Z, Face.EAST), 
                        new Vertex(x + modelEle270.x2 / 16,    y + modelEle270.y2 / 16,    z + modelEle270.z2 / 16,      model270.GetTexture(Face.EAST),   3, c.GetSunlight(lightX, lightY, lightZ),    normal.X, normal.Y, normal.Z, Face.EAST), 
                        new Vertex(x + modelEle180.x1 / 16,    y + modelEle180.y1 / 16,    z + modelEle180.z1 / 16,      model180.GetTexture(Face.EAST),   0, c.GetSunlight(lightX, lightY, lightZ),    normal.X, normal.Y, normal.Z, Face.EAST), 
                        new Vertex(x + modelEle.x2    / 16,    y + modelEle.y2 / 16,       z + modelEle.z2 / 16,         model.GetTexture(Face.EAST),      1, c.GetSunlight(lightX, lightY, lightZ),    normal.X, normal.Y, normal.Z, Face.EAST),

                    ];
                    }

                default:
                    return [
                        new Vertex(x + modelEle90.x1  / 16,    y + modelEle90.y1 / 16,     z + modelEle90.z1 / 16,       model90.GetTexture(Face.EAST),    2, 1, 1, 2, 3, Face.EAST),
                        new Vertex(x + modelEle270.x2 / 16,    y + modelEle270.y2 / 16,    z + modelEle270.z2 / 16,      model270.GetTexture(Face.EAST),   3, 1, 1, 2, 3, Face.EAST),
                        new Vertex(x + modelEle180.x1 / 16,    y + modelEle180.y1 / 16,    z + modelEle180.z1 / 16,      model180.GetTexture(Face.EAST),   0, 1, 1, 2, 3, Face.EAST),
                        new Vertex(x + modelEle.x2    / 16,    y + modelEle.y2 / 16,       z + modelEle.z2 / 16,         model.GetTexture(Face.EAST),      1, 1, 1, 2, 3, Face.EAST),

                    ];

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
