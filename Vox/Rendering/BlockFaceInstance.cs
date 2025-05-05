using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using Vox.Model;

namespace Vox.Rendering
{
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct BlockFaceInstance(Vector3 facePosition, Face faceDirection, int textureLayer)
    {
        [FieldOffset(0)] public Vector3 facePosition = facePosition;        // vec3 padded to 16 bytes
        [FieldOffset(12)] private float _padding = 1f;                      // force C# to match GLSL 16-byte alignment
        [FieldOffset(16)] public int faceDirection = (int) faceDirection;   // 4 bytes
        [FieldOffset(20)] public int textureLayer = textureLayer;           // 4 bytes
    }
}
