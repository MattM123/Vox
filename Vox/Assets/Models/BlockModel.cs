
using Newtonsoft.Json.Linq;
using Vox.Enums;

namespace Vox.Assets.Models
{
    public class BlockModel
    {

        private Dictionary<BlockFace, string> textures = [];
        private readonly List<Element> elements = [];
        private bool transparent = false;
        private BlockModel parentModel;
        private BlockType _type;
        private readonly IAssetLookup _assetLookup;

        Dictionary<string, BlockFace> _placeholders = new()
        {
            { "#all", BlockFace.ALL },
            { "#top", BlockFace.TOP },
            { "#bottom", BlockFace.BOTTOM },
            { "#side", BlockFace.SIDE }
        };

        public BlockModel(IAssetLookup assetLookup) {
            _assetLookup = assetLookup;
        }

        public BlockModel(JObject jsonObject, BlockType type, IAssetLookup assetLookup)
        {
            _assetLookup = assetLookup;
            _type = type;
            JToken? parent = jsonObject["parent"];
            JObject? jsonTextures = jsonObject["textures"] as JObject;
            JArray? jsonElements = jsonObject["elements"] as JArray;
            

            if (parent?.ToString().Length > 0)
            {
                string parentPath = Path.Combine(Window.GetAssetPath(), "BlockModels", parent.ToString());
                string jsonContent = File.ReadAllText(parentPath + ".json");
                JObject parentObj = JObject.Parse(jsonContent);
                parentModel = new BlockModel(parentObj, type, _assetLookup);
            }
            else
            {
                parentModel = new BlockModel(_assetLookup);
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

                    textures[face] = texture.Value!.ToString();
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
                string value = textures[key];

                // If the value starts with '#', it's a placeholder
                if (value.StartsWith("#") && _placeholders.TryGetValue(value, out BlockFace sourceFace))
                {
                    // If the source face actually exists in the texture map, resolve it
                    if (textures.TryGetValue(sourceFace, out string? resolvedValue))
                    {
                        textures[key] = resolvedValue;
                    }
                }
            }
        }


        /**
         * Retrieves a texture from a block model for rendering.
         * If there the model has no texture assigned it assumes its AIR
         */
        public int GetTextureLayer(BlockFace side)
        {
            //Subtract by 1 to account for AIR texture
            if (textures.TryGetValue(side, out string? value))
            {
                if (Enum.TryParse(value, true, out Texture texture))
                    return _assetLookup.GetTextureLayerFromTexture(texture);
            }
            return 0;
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
            BlockModel model = new(_assetLookup);

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
            BlockModel model = new(_assetLookup);

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
            BlockModel model = new(_assetLookup);

            //Adds current instance textures to a new model, then rotates them
            for (int i = 0; i < textures.Count; i++)
                model.textures.Add(textures.Keys.ElementAt(i), textures.Values.ElementAt(i));

            foreach (Element element in elements)
            {
                model.elements.Add(element.RotateZ(degrees));
            }
            return model;
        }

        public BlockType GetBlockType() { return _type; }
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
            return _type + "\ntextures=\n" + tex + "elements=\n" + ele + "faces=\n" + faces;
        }
    }
}

