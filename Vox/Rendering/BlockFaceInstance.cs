using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using Vox.Model;

namespace Vox.Rendering
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct BlockFaceInstance(Vector3 facePosition, Face faceDirection, int textureLayer)
    {
        [FieldOffset(0)] public Vector3 facePosition = facePosition;        // vec3 padded to 16 bytes
        [FieldOffset(12)] public int faceDirection = (int) faceDirection;   // 4 bytes
        [FieldOffset(16)] public int textureLayer = textureLayer;           // 4 bytes
        [FieldOffset(20)] public int _pad0 = 1;          
        [FieldOffset(24)] public int _pad1 = 1;           
        [FieldOffset(28)] public int _pad2 = 1;          

    }
}
