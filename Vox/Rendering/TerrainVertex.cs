
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
        public int sunlight;
        [Key(6)]
        public float normalX;
        [Key(7)]
        public float normalY;
        [Key(8)]
        public float normalZ;
        [Key(9)]
        public float blocktype;
        [Key(10)]
        public Face face;


        [SerializationConstructor]
        public TerrainVertex(float x, float y, float z,
            Texture texLayer, int texCoord, int sunlight, float normalX, float normalY, float normalZ, float blocktype, Face face)
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
            this.blocktype = blocktype;
        }

        public Vector3 GetVector()
        {
            return new(x, y, z);
        }

        public void SetVector(Vector3 v)
        {
            x = v.X; y = v.Y; z = v.Z;
        }

        public override string ToString()
        {
            return $"{GetVector()}, TexLayer: {texLayer}, TexCoordIdx: {texCoord}, Normal: {new Vector3(normalX, normalY, normalZ)}, BlockFace: {face}, BlockType: {blocktype}";
        }
    }
}
