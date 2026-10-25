
using Newtonsoft.Json.Linq;
using OpenTK.Mathematics;

namespace Vox.Assets.Models
{
    public class Element
    {
        //model space coordinates
        public int X1 { get; }
        public int Y1 { get; }
        public int Z1 { get; }
        public int X2 { get; }
        public int Y2 { get; }
        public int Z2 { get; }

        //block faces
        public readonly List<string> Faces;

        private Element(int x1, int y1, int z1, int x2, int y2, int z2, List<string> faces)
        {
            this.X1 = x1;
            this.Y1 = y1;
            this.Z1 = z1;
            this.X2 = x2;
            this.Y2 = y2;
            this.Z2 = z2;
            this.Faces = faces;
        }

        public Element(JObject json)
        {

            if (json["from"] != null && json["to"] != null && json["from"]?.ToArray().Length == 3 && json["to"]?.ToArray().Length == 3)
            {
                JToken? from = json["from"];
                JToken? to = json["to"];
                JToken? facesObj = json["faces"]; ;
                
                X1 = from![0]!.Value<int>();
                Y1 = from[1]!.Value<int>();
                Z1 = from[2]!.Value<int>();

                X2 = to![0]!.Value<int>();
                Y2 = to[1]!.Value<int>();
                Z2 = to[2]!.Value<int>();
                

                if (facesObj != null)
                {
                    List<string> list = [];
                    foreach (var face in (JObject)facesObj)
                    {
                        list.Add(face.Key);
                    }
                    Faces = list;
                }
                else
                {
                    Faces = [];
                }
            }
            else
            {
                X1 = 0;
                Y1 = 0;
                Z1 = 0;
                X2 = 0;
                Y2 = 0;
                Z2 = 0;
                Faces = [];
            }
        }

        public bool IsValid()
        {
            return !(X1 == 0 && Y1 == 0 && Z1 == 0 && X2 == 0 && Y2 == 0 && Z2 == 0);
        }

        public Element RotateX(int degrees)
        {

            double rads = MathHelper.DegreesToRadians(degrees);
            int cz = 8, cy = 8;
            int z1 = cz + (int)Math.Round((this.Z1 - cz) * Math.Cos(rads) - (this.Y1 - cy) * Math.Sin(rads));
            int y1 = cy + (int)Math.Round((this.Y1 - cy) * Math.Cos(rads) + (this.Z1 - cz) * Math.Sin(rads));
            int z2 = cz + (int)Math.Round((this.Z2 - cz) * Math.Cos(rads) - (this.Y2 - cy) * Math.Sin(rads));
            int y2 = cy + (int)Math.Round((this.Y2 - cy) * Math.Cos(rads) + (this.Z2 - cz) * Math.Sin(rads));
            return new Element(X1, y1, z1, X2, y2, z2, Faces);
        }

        public Element RotateY(int degrees)
        {
            double rads = MathHelper.DegreesToRadians(degrees);
            int cx = 8, cz = 8;
            int x1 = cx + (int)Math.Round((this.X1 - cx) * Math.Cos(rads) - (this.Z1 - cz) * Math.Sin(rads));
            int z1 = cz + (int)Math.Round((this.Z1 - cz) * Math.Cos(rads) + (this.X1 - cx) * Math.Sin(rads));
            int x2 = cx + (int)Math.Round((this.X2 - cx) * Math.Cos(rads) - (this.Z2 - cz) * Math.Sin(rads));
            int z2 = cz + (int)Math.Round((this.Z2 - cz) * Math.Cos(rads) + (this.X2 - cx) * Math.Sin(rads));
            return new Element(x1, Y1, z1, x2, Y2, z2, Faces);
        }

        public Element RotateZ(int degrees)
        {
            double rads = MathHelper.DegreesToRadians(degrees);
            int cx = 8, cy = 8;
            int x1 = cx + (int)Math.Round((this.X1 - cx) * Math.Cos(rads) - (Z1 - cy) * Math.Sin(rads));
            int y1 = cy + (int)Math.Round((this.Y1 - cy) * Math.Cos(rads) + (this.X1 - cx) * Math.Sin(rads));
            int x2 = cx + (int)Math.Round((this.X2 - cx) * Math.Cos(rads) - (Z2 - cy) * Math.Sin(rads));
            int y2 = cy + (int)Math.Round((this.Y2 - cy) * Math.Cos(rads) + (this.X2 - cx) * Math.Sin(rads));
            return new Element(x1, y1, Z1, x2, y2, Z2, Faces);
        }

        public override string ToString()
        {
            string faceString = "";
            foreach (string f in Faces) {
                faceString += f + " ";
            }
            return $"from=({X1}, {Y1}, {Z1}), to=({X2}, {Y2}, {Z2}), faces={faceString}";
        }
    }
}
