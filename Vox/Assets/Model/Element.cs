using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenTK.Mathematics;

namespace Vox.Assets.Model
{
    public class Element
    {
        //model space coordinates
        public int x1 { get; }
        public int y1 { get; }
        public int z1 { get; }
        public int x2 { get; }
        public int y2 { get; }
        public int z2 { get; }

        //block faces
        public List<string> faces;

        public Element(int x1, int y1, int z1, int x2, int y2, int z2, List<string> faces)
        {
            this.x1 = Math.Min(x1, x2);
            this.y1 = Math.Min(y1, y2);
            this.z1 = Math.Min(z1, z2);
            this.x2 = Math.Max(x1, x2);
            this.y2 = Math.Max(y1, y2);
            this.z2 = Math.Max(z1, z2);
            this.faces = faces;
        }

        public Element(JsonObject json)
        {
            
            if (json["from"] != null && json["to"] != null && json["from"]?.AsArray().Count == 3 && json["to"]?.AsArray().Count == 3)
            {
                JsonNode? from = json["from"];
                JsonNode? to = json["to"];
                JsonNode? facesObj = json["faces"]; ;

                x1 = from[0].GetValue<int>();
                y1 = from[1].GetValue<int>();
                z1 = from[2].GetValue<int>();

                x2 = to[0].GetValue<int>(); 
                y2 = to[1].GetValue<int>(); 
                z2 = to[2].GetValue<int>();

                if (facesObj != null)
                {
                    List<string> list = [];
                    foreach (var face in (JsonObject) facesObj)
                    {
                        list.Add(face.Key);
                    }
                    faces = list;
                }
                else
                {
                    faces = [];
                }
            }
            else
            {
               x1 = 0;
               y1 = 0;
               z1 = 0;
               x2 = 0;
               y2 = 0;
               z2 = 0;
               faces = [];
            }
        }

        public bool IsValid()
        {
            return !(x1 == 0 && y1 == 0 && z1 == 0 && x2 == 0 && y2 == 0 && z2 == 0);
        }

        public Element rotateX(int degrees)
        {
            double rads = MathHelper.RadiansToDegrees(degrees);
            int cz = 8, cy = 8;
            int z1 = cz + (int)Math.Round((this.z1 - cz) * Math.Cos(rads) - (this.y1 - cy) * Math.Sin(rads));
            int y1 = cy + (int)Math.Round((this.y1 - cy) * Math.Cos(rads) + (this.z1 - cz) * Math.Sin(rads));
            int z2 = cz + (int)Math.Round((this.z2 - cz) * Math.Cos(rads) - (this.y2 - cy) * Math.Sin(rads));
            int y2 = cy + (int)Math.Round((this.y2 - cy) * Math.Cos(rads) + (this.z2 - cz) * Math.Sin(rads));
            return new Element(x1, y1, z1, x2, y2, z2, faces);
        }

        public Element rotateY(int degrees)
        {
            double rads = MathHelper.RadiansToDegrees(degrees);
            int cx = 8, cz = 8;
            int x1 = cx + (int)Math.Round((this.x1 - cx) * Math.Cos(rads) - (this.z1 - cz) * Math.Sin(rads));
            int z1 = cz + (int)Math.Round((this.z1 - cz) * Math.Cos(rads) + (this.x1 - cx) * Math.Sin(rads));
            int x2 = cx + (int)Math.Round((this.x2 - cx) * Math.Cos(rads) - (this.z2 - cz) * Math.Sin(rads));
            int z2 = cz + (int)Math.Round((this.z2 - cz) * Math.Cos(rads) + (this.x2 - cx) * Math.Sin(rads));
            return new Element(x1, y1, z1, x2, y2, z2, faces);
        }

        public Element rotateZ(int degrees)
        {
            double rads = MathHelper.RadiansToDegrees(degrees);
            int cx = 8, cy = 8;
            int x1 = cx + (int)Math.Round((this.x1 - cx) * Math.Cos(rads) - (this.z1 - cy) * Math.Sin(rads));
            int y1 = cy + (int)Math.Round((this.y1 - cy) * Math.Cos(rads) + (this.x1 - cx) * Math.Sin(rads));
            int x2 = cx + (int)Math.Round((this.x2 - cx) * Math.Cos(rads) - (this.z2 - cy) * Math.Sin(rads));
            int y2 = cy + (int)Math.Round((this.y2 - cy) * Math.Cos(rads) + (this.x2 - cx) * Math.Sin(rads));
            return new Element(x1, y1, z1, x2, y2, z2, faces);
        }

        public override string ToString()
        {
            return string.Format("from=(%s,%s,%s), to=(%s,%s,%s), faces=%s", x1, y1, z1, x2, y2, z2, faces);
        }
    }
}
