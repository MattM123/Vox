
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using MessagePack;
using OpenTK.Mathematics;
using Vox.Model;

namespace Vox.Rendering
{
    [MessagePackObject]
    [StructLayout(LayoutKind.Sequential)]
    public struct TerrainVertex
    {
        [Key(0)]
        public float x;          
        [Key(1)]                
        public float y;          
        [Key(2)]                
        public float z;          
        [Key(3)]
        public Texture texLayer; 
        [Key(4)]
        public int texCoord;     
        [Key(5)]
        public float blockType;
        [Key(6)]
        public Face face;

        public TerrainVertex(float x, float y, float z,
            Texture texLayer, int texCoord, float blockType, Face face)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.texLayer = texLayer;
            this.texCoord = texCoord;
            this.face = face;
            this.blockType = blockType;
        }

        public Vector3 GetVector()
        {
            return new(x, y, z);
        }

        public void SetVector(Vector3 v)
        {
            x = v.X; y = v.Y; z = v.Z;
        }
    }
}
