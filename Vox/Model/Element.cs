
using System.Text.Json.Nodes;
using Newtonsoft.Json.Linq;
using OpenTK.Mathematics;

namespace Vox.Model
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
            this.x1 = x1;
            this.y1 = y1;
            this.z1 = z1;
            this.x2 = x2;
            this.y2 = y2;
            this.z2 = z2;
            this.faces = faces;
        }

        public Element(JObject json)
        {

            if (json["from"] != null && json["to"] != null && json["from"]?.ToArray().Length == 3 && json["to"]?.ToArray().Length == 3)
            {
                JToken? from = json["from"];
                JToken? to = json["to"];
                JToken? facesObj = json["faces"]; ;

                x1 = from[0].Value<int>();
                y1 = from[1].Value<int>();
                z1 = from[2].Value<int>();

                x2 = to[0].Value<int>();
                y2 = to[1].Value<int>();
                z2 = to[2].Value<int>();
              
                if (facesObj != null)
                {
                    List<string> list = [];
                    foreach (var face in (JObject)facesObj)
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

        public Element RotateX(int degrees)
        {

            double rads = MathHelper.DegreesToRadians(degrees);
            int cz = 8, cy = 8;
            int z1 = cz + (int)Math.Round((this.z1 - cz) * Math.Cos(rads) - (this.y1 - cy) * Math.Sin(rads));
            int y1 = cy + (int)Math.Round((this.y1 - cy) * Math.Cos(rads) + (this.z1 - cz) * Math.Sin(rads));
            int z2 = cz + (int)Math.Round((this.z2 - cz) * Math.Cos(rads) - (this.y2 - cy) * Math.Sin(rads));
            int y2 = cy + (int)Math.Round((this.y2 - cy) * Math.Cos(rads) + (this.z2 - cz) * Math.Sin(rads));
            return new Element(x1, y1, z1, x2, y2, z2, faces);
        }

        public Element RotateY(int degrees)
        {
            double rads = MathHelper.DegreesToRadians(degrees);
            int cx = 8, cz = 8;
            int x1 = cx + (int)Math.Round((this.x1 - cx) * Math.Cos(rads) - (this.z1 - cz) * Math.Sin(rads));
            int z1 = cz + (int)Math.Round((this.z1 - cz) * Math.Cos(rads) + (this.x1 - cx) * Math.Sin(rads));
            int x2 = cx + (int)Math.Round((this.x2 - cx) * Math.Cos(rads) - (this.z2 - cz) * Math.Sin(rads));
            int z2 = cz + (int)Math.Round((this.z2 - cz) * Math.Cos(rads) + (this.x2 - cx) * Math.Sin(rads));
            return new Element(x1, y1, z1, x2, y2, z2, faces);
        }

        public Element RotateZ(int degrees)
        {
            double rads = MathHelper.DegreesToRadians(degrees);
            int cx = 8, cy = 8;
            int x1 = cx + (int)Math.Round((this.x1 - cx) * Math.Cos(rads) - (z1 - cy) * Math.Sin(rads));
            int y1 = cy + (int)Math.Round((this.y1 - cy) * Math.Cos(rads) + (this.x1 - cx) * Math.Sin(rads));
            int x2 = cx + (int)Math.Round((this.x2 - cx) * Math.Cos(rads) - (z2 - cy) * Math.Sin(rads));
            int y2 = cy + (int)Math.Round((this.y2 - cy) * Math.Cos(rads) + (this.x2 - cx) * Math.Sin(rads));
            return new Element(x1, y1, z1, x2, y2, z2, faces);
        }

        public override string ToString()
        {
            string facestring = "";
            foreach (string f in faces) {
                facestring += f + " ";
            }
            return $"from=({x1}, {y1}, {z1}), to=({x2}, {y2}, {z2}), faces={facestring}";
        }
    }
}
