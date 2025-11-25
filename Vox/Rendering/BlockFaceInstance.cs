using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using Vox.Enums;

namespace Vox.Rendering
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct BlockFaceInstance(Vector3 facePosition, BlockFace faceDirection, int textureLayer, int index, ushort lighting)
    {
        [FieldOffset(0)] public Vector3 facePosition = facePosition;        // 12 byte vector 3 for position
        [FieldOffset(12)] public int faceDirection = (int) faceDirection;   // 4 bytes for face direction
        [FieldOffset(16)] public int textureLayer = textureLayer;           // 4 bytes for the texture layer
        [FieldOffset(20)] public int index = index;                         // 4 bytes to store index within the SSBO                                                                
        [FieldOffset(24)] public ushort lighting = lighting;                // 4 bytes to store bitpacked light color value for emissive blocks
        [FieldOffset(28)] public int _pad2 = 1;                             // 4 bytes for padding

    }
}
