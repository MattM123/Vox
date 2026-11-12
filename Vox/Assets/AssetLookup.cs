using System.Numerics;
using Newtonsoft.Json.Linq;
using Vox.Assets.Models;
using Vox.Enums;

namespace Vox.Assets
{
    public class AssetLookup : IAssetLookup
    {
        private readonly Dictionary<Texture, int> _textureToTextureLayer = new()
        {   
            { Texture.AIR, 0 },
            { Texture.TEST, 1 },
            { Texture.STONE, 2 },
            { Texture.DIRT, 3 },
            { Texture.GRASS_FULL, 4 },          
            { Texture.LAMP, 5 },
            { Texture.GRASS_SIDE, 6 },
            { Texture.TARGET, 7 },
            { Texture.TALL_GRASS, 8 },
            { Texture.DEAD_BUSH, 9 },
        };

        private readonly Dictionary<BlockType, Tuple<Vector2, Vector2>?> _blockTypeToIconUv = new()
        {
            { BlockType.DIRT_BLOCK,       new(new(0f, 0f),       new(0.03125f, 0.03125f)) },
            { BlockType.GRASS_BLOCK,      new(new(0.03125f, 0f), new(0.0625f,  0.03125f)) },
            { BlockType.STONE_BLOCK,      new(new(0.0625f, 0f),  new(0.09375f, 0.03125f)) },
            { BlockType.LAMP_BLOCK,       new(new(0.09375f, 0f), new(0.12500f, 0.03125f)) },
            { BlockType.TEST_BLOCK,       new(new(0.12500f, 0f), new(0.15625f, 0.03125f)) },
            { BlockType.TARGET_BLOCK,     new(new(0.15625f, 0f), new(0.18750f, 0.03125f)) },
            { BlockType.TALL_GRASS_BLOCK, new(new(0.18750f, 0f), new(0.21875f, 0.03125f)) },
            { BlockType.DEAD_BUSH,        new(new(0.21875f, 0f), new(0.25f,    0.03125f)) },
        };

        private static readonly Dictionary<BlockType, BlockFlags> BlockFlagMapping = new()
        {
            { BlockType.GRASS_BLOCK, BlockFlags.NATURAL },
            { BlockType.DIRT_BLOCK, BlockFlags.NATURAL },
            { BlockType.STONE_BLOCK, BlockFlags.NATURAL },
            { BlockType.TALL_GRASS_BLOCK, BlockFlags.NATURAL | BlockFlags.BILLBOARD | BlockFlags.SURFACE_DECO | BlockFlags.PASSTHROUGH },
            { BlockType.DEAD_BUSH, BlockFlags.NATURAL | BlockFlags.BILLBOARD | BlockFlags.PASSTHROUGH | BlockFlags.SURFACE_DECO},

            { BlockType.LAMP_BLOCK, BlockFlags.NON_NATURAL },
            { BlockType.TEST_BLOCK, BlockFlags.NON_NATURAL },
            { BlockType.TARGET_BLOCK, BlockFlags.NON_NATURAL },
            { BlockType.AIR, BlockFlags.SURFACE_DECO | BlockFlags.PASSTHROUGH }
        };

        //Indicates what biome a certain Blocktype can spawn in
        public static readonly Dictionary<BlockType, Biome> BiomeMapping = new()
        {
            { BlockType.DIRT_BLOCK,       Biome.GRASSLANDS },
            { BlockType.GRASS_BLOCK,      Biome.GRASSLANDS },
            { BlockType.STONE_BLOCK,      Biome.GRASSLANDS },
            { BlockType.TALL_GRASS_BLOCK, Biome.GRASSLANDS },
            { BlockType.DEAD_BUSH,        Biome.DESERT },
            { BlockType.LAMP_BLOCK,       Biome.NONE },
            { BlockType.TEST_BLOCK,       Biome.NONE },
            { BlockType.TARGET_BLOCK,     Biome.NONE }
        };

        private readonly Dictionary<Biome, List<BlockType>> _biomeSurfaceDeco = new();
        
        
        //Indicates the texture will have a generated texture layer within the texture array
        private static readonly Dictionary<Texture, TextureFlags> TextureFlagMapping = new()
        {
            { Texture.AIR, TextureFlags.NONE },
            { Texture.STONE, TextureFlags.HasLayer },
            { Texture.LAMP, TextureFlags.HasLayer },
            { Texture.DIRT, TextureFlags.HasLayer  },
            { Texture.GRASS_FULL, TextureFlags.HasLayer },
            { Texture.GRASS_SIDE, TextureFlags.HasLayer },
            { Texture.TEST, TextureFlags.HasLayer },
            { Texture.TARGET, TextureFlags.HasLayer },
            { Texture.TALL_GRASS, TextureFlags.HasLayer},
            { Texture.DEAD_BUSH, TextureFlags.HasLayer}
        };
        
        private readonly Dictionary<Biome, float> _biomeWeights = new()
        {
            { Biome.NONE,          0f    },
            { Biome.GRASSLANDS,    0.5f },
            { Biome.DESERT,        0.5f },
        };

        private readonly List<BlockType> _naturalBlocks;

        private readonly List<Texture> _texturesWithLayers;

        private readonly List<BlockType> _surfaceDecoBlocks;
        
        private readonly Dictionary<BlockType, BlockModel> _models = [];


        public AssetLookup() {

            foreach (Biome biome in Enum.GetValues<Biome>())
            {
                _biomeSurfaceDeco[biome] =
                [
                    .. BiomeMapping
                        .Where(a => a.Value == biome &&
                                    BlockFlagMapping[a.Key].HasFlag(BlockFlags.SURFACE_DECO))
                        .Select(a => a.Key)
                ];
            }
            
            _surfaceDecoBlocks = [.. BlockFlagMapping
                    .Where(f =>
                    f.Value.HasFlag(BlockFlags.SURFACE_DECO))
                    .Select(f => f.Key)];
           
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
            string assets = Window.GetAssetPath();
            Directory.CreateDirectory(Path.Combine(assets + Path.AltDirectorySeparatorChar, "BlockModels"));

            string blockModelsPath = Path.Combine(assets, "BlockModels");
            string projectModelsPath = Path.Combine("..", "..", "..", "Assets", "Models", "BlockModels");
            
            //Copies block _models into folder if not present
            string[] fileModels = Directory.GetFiles(blockModelsPath);
            string[] projModels = Directory.GetFiles(projectModelsPath);

            if (fileModels.Length != projModels.Length)
            {


                foreach (string sourceFile in projModels)
                {
                    string destinationFile = Path.Combine(
                        blockModelsPath,
                        Path.GetFileName(sourceFile)
                    );

                    File.Copy(sourceFile, destinationFile, true);

                    Logger.Debug($"Loaded model {Path.GetFileName(sourceFile)}");
                }

            }

            //Populate _models to store in memory
            _models.Add(BlockType.GRASS_BLOCK,       new(JObject.Parse(File.ReadAllText(Path.Combine(assets, "BlockModels", "grass.json"))),      BlockType.GRASS_BLOCK,      this));
            _models.Add(BlockType.DIRT_BLOCK,        new(JObject.Parse(File.ReadAllText(Path.Combine(assets, "BlockModels", "dirt.json"))),       BlockType.DIRT_BLOCK,       this));
            _models.Add(BlockType.STONE_BLOCK,       new(JObject.Parse(File.ReadAllText(Path.Combine(assets, "BlockModels", "stone.json"))),      BlockType.STONE_BLOCK,      this));
            _models.Add(BlockType.TEST_BLOCK,        new(JObject.Parse(File.ReadAllText(Path.Combine(assets, "BlockModels", "testblock.json"))),  BlockType.TEST_BLOCK,       this));
            _models.Add(BlockType.TARGET_BLOCK,      new(JObject.Parse(File.ReadAllText(Path.Combine(assets, "BlockModels", "target.json"))),     BlockType.TARGET_BLOCK,     this));
            _models.Add(BlockType.LAMP_BLOCK,        new(JObject.Parse(File.ReadAllText(Path.Combine(assets, "BlockModels", "lamp.json"))),       BlockType.LAMP_BLOCK,       this));
            _models.Add(BlockType.TALL_GRASS_BLOCK,  new(JObject.Parse(File.ReadAllText(Path.Combine(assets, "BlockModels", "tall_grass.json"))), BlockType.TALL_GRASS_BLOCK, this));
            _models.Add(BlockType.DEAD_BUSH,         new(JObject.Parse(File.ReadAllText(Path.Combine(assets, "BlockModels", "dead_bush.json"))),  BlockType.DEAD_BUSH,        this));
        }
        public bool IsNatural(BlockType type)
            => BlockFlagMapping.TryGetValue(type, out var f) && f.HasFlag(BlockFlags.NATURAL);

        public bool IsBillboard(BlockType type)
            => BlockFlagMapping.TryGetValue(type, out var f) && f.HasFlag(BlockFlags.BILLBOARD);
        
        public bool IsPassthrough(BlockType type)
        => BlockFlagMapping.TryGetValue(type, out var f) && f.HasFlag(BlockFlags.PASSTHROUGH);

        public Dictionary<Biome, float> GetBiomeWeights()
        {
            return _biomeWeights;
        }
        
        public List<BlockType> GetTerrainBlocks()
            =>
            [
                .. BlockFlagMapping.Where(f =>
                        f.Value.HasFlag(BlockFlags.NATURAL)
                        && !f.Value.HasFlag(BlockFlags.SURFACE_DECO))
                    .Select(f => f.Key)
            ];

        public List<BlockType> GetSurfaceDeco(Biome biome)
        {
            return _biomeSurfaceDeco[biome];
        }
        
        public Tuple<Vector2, Vector2> GetUVFromBlockType(BlockType blockType)
        {
            if (_blockTypeToIconUv.TryGetValue(blockType, out Tuple<Vector2, Vector2>? uvs) && uvs != null)
                return uvs;
            
            return new Tuple<Vector2, Vector2>(Vector2.Zero, Vector2.Zero);
        }

        public int GetTextureLayerFromTexture(Texture texture)
        {
            //Subtract by 1 to account for AIR texture
            if (_textureToTextureLayer.TryGetValue(texture, out int layer))
                return layer - 1;
            return 0;
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
