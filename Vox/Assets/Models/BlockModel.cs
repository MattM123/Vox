
using Newtonsoft.Json.Linq;
using Vox.Enums;

namespace Vox.Assets.Models
{
    public class BlockModel
    {

        private readonly Dictionary<BlockFace, string> _textures = [];
        private readonly List<Element> _elements = [];
        private readonly BlockType _type;
        private readonly IAssetLookup _assetLookup;

        private readonly Dictionary<string, BlockFace> _placeholders = new()
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
         
            BlockModel parentModel;
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

            _textures = new Dictionary<BlockFace, string>(parentModel._textures);
            _elements = new List<Element>(parentModel._elements);
            bool transparent = jsonObject.Value<bool>("transparent");
            
            //TODO: transparent block detection
            if(transparent) { }

            if (jsonTextures != null)
            {
                foreach (var texture in jsonTextures)
                {
                    Enum.TryParse(texture.Key, true, out BlockFace face);
                    _textures[face] = texture.Value!.ToString();
                }
            }

            if (jsonElements != null)
            {
                _elements.Clear();
                foreach (var element in jsonElements)
                {
                    if (element is JObject elementObj)
                    {
                        Element el = new(elementObj);
                        if (el.IsValid())
                        {
                            _elements.Add(el);
                        }
                    }
                    
                }
            }


            //Map parent texture references to child model textures
            foreach (BlockFace key in _textures.Keys)
            {
                string value = _textures[key];

                // If the value starts with '#', it's a placeholder
                if (value.StartsWith("#") && _placeholders.TryGetValue(value, out BlockFace sourceFace))
                {
                    // If the source face actually exists in the texture map, resolve it
                    if (_textures.TryGetValue(sourceFace, out string? resolvedValue))
                    {
                        _textures[key] = resolvedValue;
                    }
                }
            }
        }


        /**
         * Retrieves a texture from a block model for rendering.
         * If the model has no texture assigned it assumes its AIR
         */
        public int GetTextureLayer(BlockFace side)
        {
            //Subtract by 1 to account for AIR texture
            if (_textures.TryGetValue(side, out string? value))
            {
                if (Enum.TryParse(value, true, out Texture texture))
                    return _assetLookup.GetTextureLayerFromTexture(texture);
            }
            return 0;
        }

        public Dictionary<BlockFace, string> GetTextures()
        {
            return _textures;
        }

        public IEnumerable<Element> GetElements()
        {
            return _elements;
        }

        public BlockModel RotateX(int degrees)
        {
            BlockModel model = new(_assetLookup);

            //Adds current instance textures to a new model, then rotates them
            for (int i = 0; i < _textures.Count; i++)
            {
                model._textures.Add(_textures.Keys.ElementAt(i), _textures.Values.ElementAt(i));
            }

            foreach (Element element in _elements)
            {
                model._elements.Add(element.RotateX(degrees));
            }
          
            return model;
        }

        public BlockModel RotateY(int degrees)
        {
            BlockModel model = new(_assetLookup);

            //Adds current instance textures to a new model, then rotates them
            for (int i = 0; i < _textures.Count; i++)
                model._textures.Add(_textures.Keys.ElementAt(i), _textures.Values.ElementAt(i));

            foreach (Element element in _elements)
            {
                model._elements.Add(element.RotateY(degrees));
            }
            return model;
        }

        public BlockModel RotateZ(int degrees)
        {
            BlockModel model = new(_assetLookup);

            //Adds current instance textures to a new model, then rotates them
            for (int i = 0; i < _textures.Count; i++)
                model._textures.Add(_textures.Keys.ElementAt(i), _textures.Values.ElementAt(i));

            foreach (Element element in _elements)
            {
                model._elements.Add(element.RotateZ(degrees));
            }
            return model;
        }

        public BlockType GetBlockType() { return _type; }
        public override string ToString()
        {
            string textures = _textures.Aggregate("", (current, t) => current + $"[{t.Key}, {t.Value}]\n");
            string elements = _elements.Aggregate("", (current, t) => current + $"from=[{t.X1}, {t.Y1}, {t.Z1}], to=[{t.X2}, {t.Y2}, {t.Z2}]\n");
            string faces = _elements.SelectMany(t => t.Faces).Aggregate("", (current, t1) => current + $"[{t1}]\n");
            
            return _type + "\ntextures=\n" + textures + "elements=\n" + elements + "faces=\n" + faces;
        }
    }
}

