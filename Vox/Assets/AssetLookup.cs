using System.Reflection;
using System.Reflection.Emit;
using Newtonsoft.Json.Linq;
using OpenTK.Graphics.OpenGL;
using Vox.Assets.Models;
using Vox.Enums;

namespace Vox.Assets
{
    public class AssetLookup : IAssetLookup
    {
        private readonly Dictionary<Texture, int> TextureToTextureLayer = new()
        {   
            { Texture.AIR, 0 },
            { Texture.TEST, 1 },
            { Texture.STONE, 2 },
            { Texture.DIRT, 3 },
            { Texture.GRASS_FULL, 4 },          
            { Texture.LAMP, 5 },
            { Texture.GRASS_SIDE, 6 },
            { Texture.TARGET, 7 }


        };

        private readonly Dictionary<BlockType, string> BlockTypeToIconFile = new()
        {
            {BlockType.DIRT_BLOCK, "dirt.png" },
            {BlockType.GRASS_BLOCK, "grass_side.png" },
            {BlockType.LAMP_BLOCK, "lamp.png" },
            {BlockType.STONE_BLOCK, "stone.png" },
            {BlockType.TEST_BLOCK, "test.png" },
            {BlockType.TARGET_BLOCK, "target.png" }
        };

        public static readonly Dictionary<BlockType, BlockFlags> BlockFlagMapping = new()
        {
            { BlockType.GRASS_BLOCK, BlockFlags.NATURAL },
            { BlockType.DIRT_BLOCK, BlockFlags.NATURAL },
            { BlockType.STONE_BLOCK, BlockFlags.NATURAL },

            { BlockType.LAMP_BLOCK, BlockFlags.NON_NATURAL },
            { BlockType.TEST_BLOCK, BlockFlags.NON_NATURAL },
            { BlockType.TARGET_BLOCK, BlockFlags.NON_NATURAL },
            { BlockType.AIR, BlockFlags.NONE }
        };

        public static readonly Dictionary<Texture, TextureFlags> TextureFlagMapping = new()
        {
            { Texture.AIR, TextureFlags.NONE },
            { Texture.STONE, TextureFlags.HasLayer },
            { Texture.LAMP, TextureFlags.HasLayer },
            { Texture.DIRT, TextureFlags.HasLayer  },
            { Texture.GRASS_FULL, TextureFlags.HasLayer },
            { Texture.GRASS_SIDE, TextureFlags.HasLayer },
            { Texture.TEST, TextureFlags.HasLayer },
            { Texture.TARGET, TextureFlags.HasLayer }
        };

        private readonly List<BlockType> _naturalBlocks;

        private readonly List<Texture> _texturesWithLayers;
        private readonly Dictionary<BlockType, BlockModel> _models = [];




        public AssetLookup() {
            _naturalBlocks = [.. BlockFlagMapping
                .Where(kvp => kvp.Value.HasFlag(BlockFlags.NATURAL))
                .Select(kvp => kvp.Key)];

            _texturesWithLayers = [.. TextureFlagMapping
                .Where(kvp => kvp.Value.HasFlag(TextureFlags.HasLayer))
                .Select(kvp => kvp.Key)];

            LoadModels();
        }

        private void LoadModels()
        {
            string _assets = Window.GetAssetPath();
            Directory.CreateDirectory(_assets + "BlockModels");

            //Copies block _models into foler if not present
            string[] fileModels = Directory.EnumerateFiles(Path.Combine(_assets, "BlockModels")).ToArray();
            string[] projmodels = Directory.EnumerateFiles("..\\..\\..\\Assets\\Models\\BlockModels").ToArray();
            if (fileModels.Length != projmodels.Length)
            {
                Logger.Info("Reloading block _models");
                for (int i = 0; i < projmodels.Length; i++)
                {
                    File.Copy(projmodels[i], _assets + "BlockModels\\" + Path.GetFileName(projmodels[i]), true);
                    Logger.Debug($"Loaded model {Path.GetFileName(projmodels[i])}");
                }
            }

            //Populate _models to store in memory
            _models.Add(BlockType.GRASS_BLOCK, new(JObject.Parse(File.ReadAllText(_assets + "BlockModels\\grass.json")), BlockType.GRASS_BLOCK, this));
            _models.Add(BlockType.DIRT_BLOCK, new(JObject.Parse(File.ReadAllText(_assets + "BlockModels\\dirt.json")), BlockType.DIRT_BLOCK, this));
            _models.Add(BlockType.STONE_BLOCK, new(JObject.Parse(File.ReadAllText(_assets + "BlockModels\\stone.json")), BlockType.STONE_BLOCK, this));
            _models.Add(BlockType.TEST_BLOCK, new(JObject.Parse(File.ReadAllText(_assets + "BlockModels\\testblock.json")), BlockType.TEST_BLOCK, this));
            _models.Add(BlockType.TARGET_BLOCK, new(JObject.Parse(File.ReadAllText(_assets + "BlockModels\\target.json")), BlockType.TARGET_BLOCK, this));
            _models.Add(BlockType.LAMP_BLOCK, new(JObject.Parse(File.ReadAllText(_assets + "BlockModels\\lamp.json")), BlockType.LAMP_BLOCK, this));
        }
        public bool IsNatural(BlockType type)
            => BlockFlagMapping.TryGetValue(type, out var f) && f.HasFlag(BlockFlags.NATURAL);


        public int GetTextureLayerFromTexture(Texture texture)
        {
            //Subtract by 1 to account for AIR texture
            if (TextureToTextureLayer.TryGetValue(texture, out int layer))
                return layer - 1;
            return 0;
        }
        public string GetFileFromBlockType(BlockType blockType)
        {
            if (BlockTypeToIconFile.TryGetValue(blockType, out string filename))
                return filename;
            return "";
        }
        
        public List<BlockType> GetNaturalBlockTypes()
        {
            return _naturalBlocks;
        }
        public List<Texture> GetTexturesWithLayers() { 
            return _texturesWithLayers;
        }

        public BlockModel GetModel(BlockType type)
        {
            if (type == BlockType.AIR)
                return new(this);

            if (_models.TryGetValue(type, out BlockModel? value))
                return value;

            return new(this);
        }
        public BlockModel GetModel(int type)
        {
            if ((BlockType)type == BlockType.AIR)
                return new(this);

            if (_models.TryGetValue((BlockType)type, out BlockModel? value))
                return value;

            return new(this);
        }
    }
}
