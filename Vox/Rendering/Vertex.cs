
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using MessagePack;
using OpenTK.Mathematics;
using Vox.Model;
using Vox.Texturing;

namespace Vox.Rendering
{
    [MessagePackObject]
    [StructLayout(LayoutKind.Sequential)]
    public struct Vertex
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
        public int sunlight;   
        [Key(6)]
        public float normalX;
        [Key(7)]
        public float normalY;
        [Key(8)]
        public float normalZ;
        [Key(9)]
        public Face face;

        public Vertex(float x, float y, float z,
            Texture texLayer, int texCoord, int sunlight, float normalX, float normalY, float normalZ, Face face)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.texLayer = texLayer;
            this.texCoord = texCoord;
            this.sunlight = sunlight;
            this.normalX = normalX;
            this.normalY = normalY;
            this.normalZ = normalZ;
            this.face = face;
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
