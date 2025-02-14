
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using MessagePack;
using OpenTK.Mathematics;
using Vox.Model;

namespace Vox.Rendering
{
    [MessagePackObject]
    [StructLayout(LayoutKind.Sequential)]
    public struct LightingVertex
    {
        [Key(0)]
        public float x;          
        [Key(1)]                
        public float y;          
        [Key(2)]                
        public float z;
        [Key(3)]
        public int light;


        public LightingVertex(float x, float y, float z, int light)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.light = light;
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
