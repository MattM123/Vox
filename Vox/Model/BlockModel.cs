
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Vox.AssetManagement;
using Vox.Rendering;

namespace Vox.Model
{
    public class BlockModel
    {

        private Dictionary<Face, string> textures = [];
        private readonly List<Element> elements = [];
        private bool transparent = false;
        BlockModel parentModel;

        private BlockModel() { }

        public BlockModel(JObject jsonObject)
        {
            JToken? parent = jsonObject["parent"];
            JObject? jsonTextures = jsonObject["textures"] as JObject;
            JArray? jsonElements = jsonObject["elements"] as JArray;
            

            if (parent?.ToString().Length > 0)
            {
                string parentPath = parent.ToString();
                JObject parentObj = AssetManager.GetInstance().GetJson(new AssetPath(Path.Combine(Window.assets, "BlockModels\\" + parent)));
                parentModel = new BlockModel(parentObj);
            }
            else
            {
                parentModel = new BlockModel();
            }

            textures = new Dictionary<Face, string>(parentModel.textures);
            elements = new List<Element>(parentModel.elements);
            transparent = jsonObject.Value<bool>("transparent");

            if (jsonTextures != null)
            {
                foreach (var texture in jsonTextures)
                {
                    Face face;
                    Enum.TryParse(texture.Key, true, out face);

                    textures[face] = texture.Value.ToString();
                }
            }

            if (jsonElements != null)
            {
                elements.Clear();
                foreach (var element in jsonElements)
                {
                    if (element is JObject elementObj)
                    {
                        Element el = new(elementObj);
                        if (el.IsValid())
                        {
                            elements.Add(el);
                        }
                    }
                }
            }


            //Map parent texture references to child model textures
            foreach (Face key in textures.Keys)
            {

                if (textures.TryGetValue(Face.ALL, out string? all))
                {
                    if (textures[key] == "#all")
                        textures[key] = all;
                }
                if (textures.TryGetValue(Face.TOP, out string? top))
                {
                    if (textures[key] == "#top")
                        textures[key] = top;
                }
                if (textures.TryGetValue(Face.BOTTOM, out string? bottom))
                {
                    if (textures[key] == "#bottom")
                        textures[key] = bottom;
                }
                if (textures.TryGetValue(Face.SIDE, out string? side))
                {
                    if (textures[key] == "#side")
                    {
                        textures[key] = side;
                        textures[key] = side;
                        textures[key] = side;
                        textures[key] = side;
                    }
                }
            }
        }

        public bool IsTransparent() { return transparent; }
        public Texture GetTexture(Face side)
        {
            Enum.TryParse(textures[side], true, out Texture output);
            return output;
        }

        public Dictionary<Face, string> GetTextures()
        {
            return textures;
        }

        public IEnumerable<Element> GetElements()
        {
            return elements;
        }

        public BlockModel RotateX(int degrees)
        {
            BlockModel model = new();

            //Adds current instance textures to a new model, then rotates them
            for (int i = 0; i < textures.Count; i++)
            {
                model.textures.Add(textures.Keys.ElementAt(i), textures.Values.ElementAt(i));
            }

            foreach (Element element in elements)
            {
                model.elements.Add(element.RotateX(degrees));
            }
          
            return model;
        }

        public BlockModel RotateY(int degrees)
        {
            BlockModel model = new();

            //Adds current instance textures to a new model, then rotates them
            for (int i = 0; i < textures.Count; i++)
                model.textures.Add(textures.Keys.ElementAt(i), textures.Values.ElementAt(i));

            foreach (Element element in elements)
            {
                model.elements.Add(element.RotateY(degrees));
            }
            return model;
        }

        public BlockModel RotateZ(int degrees)
        {
            BlockModel model = new();

            //Adds current instance textures to a new model, then rotates them
            for (int i = 0; i < textures.Count; i++)
                model.textures.Add(textures.Keys.ElementAt(i), textures.Values.ElementAt(i));

            foreach (Element element in elements)
            {
                model.elements.Add(element.RotateZ(degrees));
            }
            return model;
        }

        public override string ToString()
        {
            string tex = "";
            foreach (KeyValuePair<Face, string> t in textures)
            {
                tex += $"[{t.Key}, {t.Value}]\n";
            }
            string ele = "";
            foreach (Element t in elements)
            {
                ele += $"from=[{t.x1}, {t.y1}, {t.z1}], to=[{t.x2}, {t.y2}, {t.z2}]\n";
            }
            string faces = "";
            for (int i = 0; i < elements.Count; i++)
            {
                for (int j = 0; j < elements[i].faces.Count; j++)
                {
                    faces += $"[{elements[i].faces[j]}]\n";
                }
            }
            return "\ntextures=\n" + tex + "elements=\n" + ele + "faces=\n" + faces;
        }

        public static BlockModel ForPath(AssetPath path)
        {
            JObject obj = AssetManager.GetInstance().GetJson(path);
            return new BlockModel(obj);
        }
    }
}

