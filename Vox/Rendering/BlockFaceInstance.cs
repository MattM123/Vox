using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using Vox.Model;

namespace Vox.Rendering
{
    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    public struct BlockFaceInstance(Vector3 facePosition, Face faceDirection, int textureLayer)
    {
        Vector3 facePosition = facePosition;
        Face faceDirection = faceDirection;
        int textureLayer = textureLayer;
        Vector2 _padding = new(1,1);      // pad to multiple of 16 (can be used for another face property later on)
    }
}
