

using System.Reflection;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Vox.Genesis;
using Vox.Rendering;

namespace Vox.Model
{
    static class ModelUtils
    {


      

        //Gets offset face vertices ready to send to the shaders for rendering
        public static TerrainVertex[] GetCuboidFace(BlockType blockType, Face face, Vector3 blockLoc, Chunk c)
        {
            float x = blockLoc.X;
            float y = blockLoc.Y;
            float z = blockLoc.Z;

            BlockModel model = ModelLoader.GetModel(blockType);

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
                        c ??= RegionManager.GetAndLoadGlobalChunkFromCoords(0, 0, 0);

                        //Clamps coords into a index usable by the chunks lightmap
                        int lightX = (int)Math.Abs(x) % RegionManager.CHUNK_BOUNDS;
                        int lightY = (int)Math.Abs(y) % RegionManager.CHUNK_BOUNDS;
                        int lightZ = (int)Math.Abs(z) % RegionManager.CHUNK_BOUNDS;

                        return [
                            //Position (X, Y, Z)                                                                                   Texture Layer         Tex Coord Idx | Light level                           Normal X, Y , Z               BlockType          Blockface
                            new TerrainVertex(x + modelEle90.x1  / 16,    y + modelEle90.y1 / 16,     z + modelEle90.z1 / 16,      model90.GetTexture(Face.SOUTH),   3, c.lightmap[lightY, lightZ, lightX],    normal.X, normal.Y, normal.Z, (float) blockType, Face.SOUTH),    //top right                      
                            new TerrainVertex(x + modelEle.x1    / 16,    y + modelEle.y1 / 16,       z + modelEle.z1 / 16,        model.GetTexture(Face.SOUTH),     2, c.lightmap[lightY, lightZ, lightX],    normal.X, normal.Y, normal.Z, (float) blockType, Face.SOUTH),    //top left
                            new TerrainVertex(x + modelEle180.x1 / 16,    y + modelEle180.y1 / 16,    z + modelEle180.z1 / 16,     model180.GetTexture(Face.SOUTH),  1, c.lightmap[lightY, lightZ, lightX],    normal.X, normal.Y, normal.Z, (float) blockType, Face.SOUTH),    //bottom right     
                            new TerrainVertex(x + modelEle270.x1 / 16,    y + modelEle270.y1 / 16,    z + modelEle270.z1 / 16,     model270.GetTexture(Face.SOUTH),  0, c.lightmap[lightY, lightZ, lightX],    normal.X, normal.Y, normal.Z, (float) blockType, Face.SOUTH),    //bottom left
                        ];
                    }
                case Face.NORTH:
                case Face.BACK:
                    {
                        Vector3 normal = CalculateNormal(new(x + modelEle270.x2 / 16, y + modelEle270.y2 / 16, z + modelEle270.z2 / 16),
                            new(x + modelEle.x2 / 16, y + modelEle.y2 / 16, z + modelEle.z2 / 16),
                            new(x + modelEle180.x2 / 16, y + modelEle180.y2 / 16, z + modelEle180.z2 / 16));

                        c ??= RegionManager.GetAndLoadGlobalChunkFromCoords(0, 0, 0);

                        //Clamps coords into a index usable by the chunks lightmap
                        int lightX = (int)Math.Abs(x) % RegionManager.CHUNK_BOUNDS;
                        int lightY = (int)Math.Abs(y) % RegionManager.CHUNK_BOUNDS;
                        int lightZ = (int)Math.Abs(z) % RegionManager.CHUNK_BOUNDS;

                        return [
                            new TerrainVertex(x + modelEle270.x2 / 16,    y + modelEle270.y2 / 16,    z + modelEle270.z2 / 16,      model270.GetTexture(Face.NORTH),  3, c.lightmap[lightY, lightZ, lightX],    normal.X, normal.Y, normal.Z, (float) blockType, Face.NORTH),
                            new TerrainVertex(x + modelEle.x2    / 16,    y + modelEle.y2 / 16,       z + modelEle.z2 / 16,         model.GetTexture(Face.NORTH),     1, c.lightmap[lightY, lightZ, lightX],    normal.X, normal.Y, normal.Z, (float) blockType, Face.NORTH),
                            new TerrainVertex(x + modelEle180.x2 / 16,    y + modelEle180.y2 / 16,    z + modelEle180.z2 / 16,      model180.GetTexture(Face.NORTH),  2, c.lightmap[lightY, lightZ, lightX],    normal.X, normal.Y, normal.Z, (float) blockType, Face.NORTH),
                            new TerrainVertex(x + modelEle90.x2  / 16,    y + modelEle90.y2 / 16,     z + modelEle90.z2 / 16,       model90.GetTexture(Face.NORTH),   0, c.lightmap[lightY, lightZ, lightX],    normal.X, normal.Y, normal.Z, (float) blockType, Face.NORTH),
                        ];
                    }
                case Face.TOP:
                case Face.UP:
                    {
                        Vector3 normal = CalculateNormal(new(x + modelEle180.x1 / 16, y + modelEle180.y1 / 16, z + modelEle180.z1 / 16),
                            new(x + modelEle.x2 / 16, y + modelEle.y2 / 16, z + modelEle.z2 / 16),
                            new(x + modelEle270.x1 / 16, y + modelEle270.y1 / 16, z + modelEle270.z1 / 16));

                        c ??= RegionManager.GetAndLoadGlobalChunkFromCoords(0, 0, 0);

                        //Clamps coords into a index usable by the chunks lightmap
                        int lightX = (int)Math.Abs(x) % RegionManager.CHUNK_BOUNDS;
                        int lightY = (int)Math.Abs(y) % RegionManager.CHUNK_BOUNDS;
                        int lightZ = (int)Math.Abs(z) % RegionManager.CHUNK_BOUNDS;

                        return [
                            new TerrainVertex(x + modelEle180.x1 / 16,    y + modelEle180.y1 / 16,    z + modelEle180.z1 / 16,      model180.GetTexture(Face.UP),     3, c.lightmap[lightY, lightZ, lightX],    normal.X, normal.Y, normal.Z, (float) blockType, Face.UP),
                            new TerrainVertex(x + modelEle.x2    / 16,    y + modelEle.y2 / 16,       z + modelEle.z2 / 16,         model.GetTexture(Face.UP),        1, c.lightmap[lightY, lightZ, lightX],    normal.X, normal.Y, normal.Z, (float) blockType, Face.UP),
                            new TerrainVertex(x + modelEle270.x1 / 16,    y + modelEle270.y1 / 16,    z + modelEle270.z1 / 16,      model270.GetTexture(Face.UP),     2, c.lightmap[lightY, lightZ, lightX],    normal.X, normal.Y, normal.Z, (float) blockType, Face.UP),
                            new TerrainVertex(x + modelEle90.x2  / 16,    y + modelEle90.y2 / 16,     z + modelEle90.z2 / 16,       model90.GetTexture(Face.UP),      0, c.lightmap[lightY, lightZ, lightX],    normal.X, normal.Y, normal.Z, (float) blockType, Face.UP),
                        ];
                    }
                case Face.BOTTOM:
                case Face.DOWN:
                    {
                        Vector3 normal = CalculateNormal(new(x + modelEle90.x1 / 16, y + modelEle90.y1 / 16, z + modelEle90.z1 / 16),
                            new(x + modelEle270.x2 / 16, y + modelEle270.y2 / 16, z + modelEle270.z2 / 16),
                            new(x + modelEle.x1 / 16, y + modelEle.y1 / 16, z + modelEle.z1 / 16));

                        c ??= RegionManager.GetAndLoadGlobalChunkFromCoords(0, 0, 0);

                        //Clamps coords into a index usable by the chunks lightmap
                        int lightX = (int)Math.Abs(x) % RegionManager.CHUNK_BOUNDS;
                        int lightY = (int)Math.Abs(y) % RegionManager.CHUNK_BOUNDS;
                        int lightZ = (int)Math.Abs(z) % RegionManager.CHUNK_BOUNDS;

                        return [
                            new TerrainVertex(x + modelEle90.x1  / 16,    y + modelEle90.y1 / 16,     z + modelEle90.z1 / 16,      model90.GetTexture(Face.DOWN),    3, c.lightmap[lightY, lightZ, lightX],     normal.X, normal.Y, normal.Z, (float) blockType, Face.DOWN),
                            new TerrainVertex(x + modelEle270.x2 / 16,    y + modelEle270.y2 / 16,    z + modelEle270.z2 / 16,     model270.GetTexture(Face.DOWN),   1, c.lightmap[lightY, lightZ, lightX],     normal.X, normal.Y, normal.Z, (float) blockType, Face.DOWN),
                            new TerrainVertex(x + modelEle.x1    / 16,    y + modelEle.y1 / 16,       z + modelEle.z1 / 16,        model.GetTexture(Face.DOWN),      2, c.lightmap[lightY, lightZ, lightX],     normal.X, normal.Y, normal.Z, (float) blockType, Face.DOWN),
                            new TerrainVertex(x + modelEle180.x2 / 16,    y + modelEle180.y2 / 16,    z + modelEle180.z2 / 16,     model180.GetTexture(Face.DOWN),   0, c.lightmap[lightY, lightZ, lightX],     normal.X, normal.Y, normal.Z, (float) blockType, Face.DOWN),

                        ];
                    }
                case Face.WEST:
                case Face.LEFT:
                    {
                        Vector3 normal = CalculateNormal(new(x + modelEle90.x2 / 16, y + modelEle90.y2 / 16, z + modelEle90.z2 / 16),
                            new(x + modelEle270.x1 / 16, y + modelEle270.y1 / 16, z + modelEle270.z1 / 16),
                            new(x + modelEle180.x2 / 16, y + modelEle180.y2 / 16, z + modelEle180.z2 / 16));

                        c ??= RegionManager.GetAndLoadGlobalChunkFromCoords(0, 0, 0);

                        //Clamps coords into a index usable by the chunks lightmap
                        int lightX = (int)Math.Abs(x) % RegionManager.CHUNK_BOUNDS;
                        int lightY = (int)Math.Abs(y) % RegionManager.CHUNK_BOUNDS;
                        int lightZ = (int)Math.Abs(z) % RegionManager.CHUNK_BOUNDS;

                        return [
                            new TerrainVertex(x + modelEle90.x2  / 16,    y + modelEle90.y2 / 16,     z + modelEle90.z2 / 16,       model90.GetTexture(Face.WEST),    0, c.lightmap[lightY, lightZ, lightX],     normal.X, normal.Y, normal.Z, (float) blockType, Face.WEST),
                            new TerrainVertex(x + modelEle270.x1 / 16,    y + modelEle270.y1 / 16,    z + modelEle270.z1 / 16,      model270.GetTexture(Face.WEST),   1, c.lightmap[lightY, lightZ, lightX],     normal.X, normal.Y, normal.Z, (float) blockType, Face.WEST),
                            new TerrainVertex(x + modelEle180.x2 / 16,    y + modelEle180.y2 / 16,    z + modelEle180.z2 / 16,      model180.GetTexture(Face.WEST),   2, c.lightmap[lightY, lightZ, lightX],     normal.X, normal.Y, normal.Z, (float) blockType, Face.WEST),
                            new TerrainVertex(x + modelEle.x1    / 16,    y + modelEle.y1 / 16,       z + modelEle.z1 / 16,         model.GetTexture(Face.WEST),      3, c.lightmap[lightY, lightZ, lightX],     normal.X, normal.Y, normal.Z, (float) blockType, Face.WEST),
                        ];
                    }
                case Face.EAST:
                case Face.RIGHT:
                    {
                        Vector3 normal = CalculateNormal(new(x + modelEle90.x1 / 16, y + modelEle90.y1 / 16, z + modelEle90.z1 / 16),
                            new(x + modelEle270.x2 / 16, y + modelEle270.y2 / 16, z + modelEle270.z2 / 16),
                            new(x + modelEle180.x1 / 16, y + modelEle180.y1 / 16, z + modelEle180.z1 / 16));

                        c ??= RegionManager.GetAndLoadGlobalChunkFromCoords(0, 0, 0);

                        //Clamps coords into a index usable by the chunks lightmap
                        int lightX = (int)Math.Abs(x) % RegionManager.CHUNK_BOUNDS;
                        int lightY = (int)Math.Abs(y) % RegionManager.CHUNK_BOUNDS;
                        int lightZ = (int)Math.Abs(z) % RegionManager.CHUNK_BOUNDS;

                        return [
                            new TerrainVertex(x + modelEle90.x1  / 16,    y + modelEle90.y1 / 16,     z + modelEle90.z1 / 16,       model90.GetTexture(Face.EAST),    2, c.lightmap[lightY, lightZ, lightX],    normal.X, normal.Y, normal.Z, (float) blockType, Face.EAST),
                            new TerrainVertex(x + modelEle270.x2 / 16,    y + modelEle270.y2 / 16,    z + modelEle270.z2 / 16,      model270.GetTexture(Face.EAST),   3, c.lightmap[lightY, lightZ, lightX],    normal.X, normal.Y, normal.Z, (float) blockType, Face.EAST),
                            new TerrainVertex(x + modelEle180.x1 / 16,    y + modelEle180.y1 / 16,    z + modelEle180.z1 / 16,      model180.GetTexture(Face.EAST),   0, c.lightmap[lightY, lightZ, lightX],    normal.X, normal.Y, normal.Z, (float) blockType, Face.EAST),
                            new TerrainVertex(x + modelEle.x2    / 16,    y + modelEle.y2 / 16,       z + modelEle.z2 / 16,         model.GetTexture(Face.EAST),      1, c.lightmap[lightY, lightZ, lightX],    normal.X, normal.Y, normal.Z, (float) blockType, Face.EAST),

                        ];
                    }

                default:
                    return [
                        new TerrainVertex(x + modelEle90.x1  / 16,    y + modelEle90.y1 / 16,     z + modelEle90.z1 / 16,       model90.GetTexture(Face.EAST),    2, 1, 1, 2, 3, (float) blockType, Face.EAST),
                        new TerrainVertex(x + modelEle270.x2 / 16,    y + modelEle270.y2 / 16,    z + modelEle270.z2 / 16,      model270.GetTexture(Face.EAST),   3, 1, 1, 2, 3, (float) blockType, Face.EAST),
                        new TerrainVertex(x + modelEle180.x1 / 16,    y + modelEle180.y1 / 16,    z + modelEle180.z1 / 16,      model180.GetTexture(Face.EAST),   0, 1, 1, 2, 3, (float) blockType, Face.EAST),
                        new TerrainVertex(x + modelEle.x2    / 16,    y + modelEle.y2 / 16,       z + modelEle.z2 / 16,         model.GetTexture(Face.EAST),      1, 1, 1, 2, 3, (float) blockType, Face.EAST),

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
