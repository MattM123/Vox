

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Vox.AssetManagement;
using Vox.Enums;

namespace Vox.Model
{
    public class BlockModel
    {

        private Dictionary<BlockFace, string> textures = [];
        private readonly List<Element> elements = [];
        private bool transparent = false;
        BlockModel parentModel;
        BlockType type;

        public BlockModel() { }

        public BlockModel(JObject jsonObject, BlockType type)
        {
            this.type = type;
            JToken? parent = jsonObject["parent"];
            JObject? jsonTextures = jsonObject["textures"] as JObject;
            JArray? jsonElements = jsonObject["elements"] as JArray;
            

            if (parent?.ToString().Length > 0)
            {
                string parentPath = parent.ToString();
                JObject parentObj = AssetManager.GetInstance().GetJson(new AssetPath(Path.Combine(Window.assets, "BlockModels\\" + parent)));
                parentModel = new BlockModel(parentObj, type);
            }
            else
            {
                parentModel = new BlockModel();
            }

            textures = new Dictionary<BlockFace, string>(parentModel.textures);
            elements = new List<Element>(parentModel.elements);
            transparent = jsonObject.Value<bool>("transparent");

            if (jsonTextures != null)
            {
                foreach (var texture in jsonTextures)
                {
                    BlockFace face;
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
            foreach (BlockFace key in textures.Keys)
            {

                if (textures.TryGetValue(BlockFace.ALL, out string? all))
                {
                    if (textures[key] == "#all")
                        textures[key] = all;
                }
                if (textures.TryGetValue(BlockFace.TOP, out string? top))
                {
                    if (textures[key] == "#top")
                        textures[key] = top;
                }
                if (textures.TryGetValue(BlockFace.BOTTOM, out string? bottom))
                {
                    if (textures[key] == "#bottom")
                        textures[key] = bottom;
                }
                if (textures.TryGetValue(BlockFace.SIDE, out string? side))
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

        /**
         * Retrieves a texture from a block model for rendering.
         * If there the model has no texture assigned it assumes its AIR
         */
        public Texture GetTexture(BlockFace side)
        {
            Texture output = 0;
            if (textures.TryGetValue(side, out string? value)) {
                Enum.TryParse(value, true, out output);
                return output;
            }

            return output;
        }

        public Dictionary<BlockFace, string> GetTextures()
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

        public BlockType GetBlockType() { return type; }
        public override string ToString()
        {
            string tex = "";
            foreach (KeyValuePair<BlockFace, string> t in textures)
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

       //public static BlockModel ForPath(AssetPath path)
       //{
       //    JObject obj = AssetManager.GetInstance().GetJson(path);
       //    return new BlockModel(obj);
       //}
    }
}

