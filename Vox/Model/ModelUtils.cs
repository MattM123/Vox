

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
                    return [
                        //Position (X, Y, Z)                                 Texture Index                        Texture Coordinate Index
                        modelEle90.x1, modelEle90.y1, modelEle90.z1,         (float)model90.GetTexture("south"),   3f, //top right                      
                        modelEle.x1, modelEle.y1, modelEle.z1,               (float)model.GetTexture("south"),     2f, //top left
                        modelEle180.x1, modelEle180.y1, modelEle180.z1,      (float)model180.GetTexture("south"),  1f, //bottom right     
                        modelEle270.x1, modelEle270.y1, modelEle270.z1,      (float)model270.GetTexture("south"),  0f, //bottom left
                    ];
                }
                case "north":
                case "back": 
                {
                    return [
                        modelEle270.x2, modelEle270.y2, modelEle270.z2,      (float)model270.GetTexture("north"),  3f,
                        modelEle.x2, modelEle.y2, modelEle.z2,               (float)model.GetTexture("north"),     1f,
                        modelEle180.x2, modelEle180.y2, modelEle180.z2,      (float)model180.GetTexture("north"),  2f,
                        modelEle90.x2, modelEle90.y2, modelEle90.z2,         (float)model90.GetTexture("north"),   0f,
                    ];    
                }
                case "top":
                case "up":
                {
                    return [
                        modelEle180.x1, modelEle180.y1, modelEle180.z1,      (float)model180.GetTexture("up"),     3f,
                        modelEle.x2, modelEle.y2, modelEle.z2,               (float)model.GetTexture("up"),        1f,
                        modelEle270.x1, modelEle270.y1, modelEle270.z1,      (float)model270.GetTexture("up"),     2f,
                        modelEle90.x2, modelEle90.y2, modelEle90.z2,         (float)model90.GetTexture("up"),      0f,

                    ];
                }
                case "bottom":
                case "down":
                {
                    return [                                                 
                        modelEle90.x1, modelEle90.y1, modelEle90.z1,         (float)model90.GetTexture("down"),    3f,
                        modelEle270.x2, modelEle270.y2, modelEle270.z2,      (float)model270.GetTexture("down"),   1f,
                        modelEle.x1, modelEle.y1, modelEle.z1,               (float)model.GetTexture("down"),      2f,
                        modelEle180.x2, modelEle180.y2, modelEle180.z2,      (float)model180.GetTexture("down"),   0f,

                    ];
                }
                case "west":
                case "left":
                {
                    return [
                        modelEle90.x2, modelEle90.y2, modelEle90.z2,         (float)model90.GetTexture("west"),    0f,
                        modelEle270.x1, modelEle270.y1, modelEle270.z1,      (float)model270.GetTexture("west"),   1f,
                        modelEle180.x2, modelEle180.y2, modelEle180.z2,      (float)model180.GetTexture("west"),   2f,
                        modelEle.x1, modelEle.y1, modelEle.z1,               (float)model.GetTexture("west"),      3f,

                    ];
                }
                case "east":
                case "right":
                {
                    return [
                        modelEle90.x1, modelEle90.y1, modelEle90.z1,         (float)model90.GetTexture("east"),    2f,
                        modelEle270.x2, modelEle270.y2, modelEle270.z2,      (float)model270.GetTexture("east"),   3f,
                        modelEle180.x1, modelEle180.y1, modelEle180.z1,      (float)model180.GetTexture("east"),   0f,
                        modelEle.x2, modelEle.y2, modelEle.z2,               (float)model.GetTexture("east"),      1f,

                    ];
                }

                default:
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
                        return [
                        //Position (X, Y, Z)                                                                                    Texture Index   Texture Coordinate Index
                        bLocX + modelEle90.x1  / 16,     bLocY + modelEle90.y1 / 16,     bLocZ + modelEle90.z1 / 16,       (float)model90.GetTexture("south"),   3f, //top right                      
                        bLocX + modelEle.x1    / 16,       bLocY + modelEle.y1 / 16,       bLocZ + modelEle.z1 / 16,         (float)model.GetTexture("south"),     2f, //top left
                        bLocX + modelEle180.x1 / 16,    bLocY + modelEle180.y1 / 16,    bLocZ + modelEle180.z1 / 16,      (float)model180.GetTexture("south"),  1f, //bottom right     
                        bLocX + modelEle270.x1 / 16,    bLocY + modelEle270.y1 / 16,    bLocZ + modelEle270.z1 / 16,      (float)model270.GetTexture("south"),  0f, //bottom left
                    ];
                    }
                case "north":
                case "back":
                    {
                        return [
                        bLocX + modelEle270.x2 / 16,    bLocY + modelEle270.y2 / 16,    bLocZ + modelEle270.z2 / 16,      (float)model270.GetTexture("north"),  3f,
                        bLocX + modelEle.x2    / 16,       bLocY + modelEle.y2 / 16,       bLocZ + modelEle.z2 / 16,         (float)model.GetTexture("north"),     1f,
                        bLocX + modelEle180.x2 / 16,    bLocY + modelEle180.y2 / 16,    bLocZ + modelEle180.z2 / 16,      (float)model180.GetTexture("north"),  2f,
                        bLocX + modelEle90.x2  / 16,     bLocY + modelEle90.y2 / 16,     bLocZ + modelEle90.z2 / 16,       (float)model90.GetTexture("north"),   0f,
                    ];
                    }
                case "top":
                case "up":
                    {
                        return [
                        bLocX + modelEle180.x1 / 16,    bLocY + modelEle180.y1 / 16,    bLocZ + modelEle180.z1 / 16,      (float)model180.GetTexture("up"),     3f,
                        bLocX + modelEle.x2    / 16,       bLocY + modelEle.y2 / 16,       bLocZ + modelEle.z2 / 16,         (float)model.GetTexture("up"),        1f,
                        bLocX + modelEle270.x1 / 16,    bLocY + modelEle270.y1 / 16,    bLocZ + modelEle270.z1 / 16,      (float)model270.GetTexture("up"),     2f,
                        bLocX + modelEle90.x2  / 16,     bLocY + modelEle90.y2 / 16,     bLocZ + modelEle90.z2 / 16,       (float)model90.GetTexture("up"),      0f,
                    ];
                    }
                case "bottom":
                case "down":
                    {
                        return [
                        bLocX + modelEle90.x1  / 16,     bLocY + modelEle90.y1 / 16,     bLocZ + modelEle90.z1 / 16,       (float)model90.GetTexture("down"),    3f,
                        bLocX + modelEle270.x2 / 16,    bLocY + modelEle270.y2 / 16,    bLocZ + modelEle270.z2 / 16,      (float)model270.GetTexture("down"),   1f,
                        bLocX + modelEle.x1    / 16,       bLocY + modelEle.y1 / 16,       bLocZ + modelEle.z1 / 16,         (float)model.GetTexture("down"),      2f,
                        bLocX + modelEle180.x2 / 16,    bLocY + modelEle180.y2 / 16,    bLocZ + modelEle180.z2 / 16,      (float)model180.GetTexture("down"),   0f,

                    ];
                    }
                case "west":
                case "left":
                    {
                        return [
                        bLocX + modelEle90.x2  / 16,     bLocY + modelEle90.y2 / 16,     bLocZ + modelEle90.z2 / 16,       (float)model90.GetTexture("west"),    0f,
                        bLocX + modelEle270.x1 / 16,    bLocY + modelEle270.y1 / 16,    bLocZ + modelEle270.z1 / 16,      (float)model270.GetTexture("west"),   1f,
                        bLocX + modelEle180.x2 / 16,    bLocY + modelEle180.y2 / 16,    bLocZ + modelEle180.z2 / 16,      (float)model180.GetTexture("west"),   2f,
                        bLocX + modelEle.x1    / 16,       bLocY + modelEle.y1 / 16,       bLocZ + modelEle.z1 / 16,         (float)model.GetTexture("west"),      3f,
                    ];
                    }
                case "east":
                case "right":
                    {
                        return [
                        bLocX + modelEle90.x1  / 16,     bLocY + modelEle90.y1 / 16,     bLocZ + modelEle90.z1 / 16,       (float)model90.GetTexture("east"),    2f,
                        bLocX + modelEle270.x2 / 16,    bLocY + modelEle270.y2 / 16,    bLocZ + modelEle270.z2 / 16,      (float)model270.GetTexture("east"),   3f,
                        bLocX + modelEle180.x1 / 16,    bLocY + modelEle180.y1 / 16,    bLocZ + modelEle180.z1 / 16,      (float)model180.GetTexture("east"),   0f,
                        bLocX + modelEle.x2    / 16,       bLocY + modelEle.y2 / 16,       bLocZ + modelEle.z2 / 16,         (float)model.GetTexture("east"),      1f,

                    ];
                    }

                default:
                    return [1f, 1f, 1f, 1f, 1f];

            }
        }
    }
}
