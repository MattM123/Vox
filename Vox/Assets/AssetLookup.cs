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
            { Texture.Stone, 0 },
            { Texture.Lamp, 1 },
            { Texture.Dirt, 2 },
            { Texture.GrassFull, 3 },          
            { Texture.GrassSide, 4 },
            { Texture.Test, 5 },
            { Texture.Target, 6 },
            { Texture.TallGrass, 7 },
            { Texture.DeadBush, 8 },
            { Texture.Sand, 9}
        };

        private const float Interval = 0.03125f;
        private readonly Dictionary<BlockType, Tuple<Vector2, Vector2>?> _blockTypeToIconUv = new()
        {
            { BlockType.Dirt,       new(new(0f,           0f),  new(Interval,     Interval)) },
            { BlockType.Grass,      new(new(Interval,     0f),  new(Interval * 2, Interval)) },
            { BlockType.Stone,      new(new(Interval * 2, 0f),  new(Interval * 3, Interval)) },
            { BlockType.Lamp,       new(new(Interval * 3, 0f),  new(Interval * 4, Interval)) },
            { BlockType.Test,       new(new(Interval * 4, 0f),  new(Interval * 5, Interval)) },
            { BlockType.Target,     new(new(Interval * 5, 0f),  new(Interval * 6, Interval)) },
            { BlockType.TallGrass,  new(new(Interval * 6, 0f),  new(Interval * 7, Interval)) },
            { BlockType.DeadBush,   new(new(Interval * 7, 0f),  new(Interval * 8, Interval)) },
            { BlockType.Sand,       new(new(Interval * 8, 0f),  new(Interval * 9, Interval)) },                  
        };

        private static readonly Dictionary<BlockType, BlockFlags> BlockFlagMapping = new()
        {
            { BlockType.Grass, BlockFlags.Natural },
            { BlockType.Dirt, BlockFlags.Natural },
            { BlockType.Stone, BlockFlags.Natural },
            { BlockType.Sand, BlockFlags.Natural },
            { BlockType.TallGrass, BlockFlags.Natural | BlockFlags.Billboard | BlockFlags.SurfaceDeco | BlockFlags.Passthrough },
            { BlockType.DeadBush, BlockFlags.Natural | BlockFlags.Billboard | BlockFlags.Passthrough | BlockFlags.SurfaceDeco},

            { BlockType.Lamp, BlockFlags.NonNatural },
            { BlockType.Test, BlockFlags.NonNatural },
            { BlockType.Target, BlockFlags.NonNatural },
            { BlockType.Air, BlockFlags.SurfaceDeco | BlockFlags.Passthrough }
        };

        //Indicates what biome a certain Blocktype can generate in
        private static readonly Dictionary<BlockType, Biome> BiomeMapping = new()
        {
            { BlockType.Dirt,       Biome.Grasslands },
            { BlockType.Grass,      Biome.Grasslands },
            { BlockType.TallGrass,  Biome.Grasslands },
            { BlockType.Stone,      Biome.Grasslands | Biome.Desert},
            { BlockType.Sand,       Biome.Desert },
            { BlockType.DeadBush,   Biome.Desert },
            { BlockType.Lamp,       Biome.None },
            { BlockType.Test,       Biome.None },
            { BlockType.Target,     Biome.None }
        };

        private readonly Dictionary<Biome, List<BlockType>> _biomeSurfaceDeco = new();
        
        
        //Indicates the texture will have a generated texture layer within the texture array
        private static readonly Dictionary<Texture, TextureFlags> TextureFlagMapping = new()
        {
            { Texture.Stone,     TextureFlags.HasLayer },
            { Texture.Lamp,      TextureFlags.HasLayer },
            { Texture.Dirt,      TextureFlags.HasLayer },
            { Texture.GrassFull, TextureFlags.HasLayer },
            { Texture.GrassSide, TextureFlags.HasLayer },
            { Texture.Test,      TextureFlags.HasLayer },
            { Texture.Target,    TextureFlags.HasLayer },
            { Texture.TallGrass, TextureFlags.HasLayer },
            { Texture.Sand,      TextureFlags.HasLayer },
            { Texture.DeadBush,  TextureFlags.HasLayer }
        };
        
        private readonly Dictionary<Biome, float> _biomeWeights = new()
        {
            { Biome.None,          0f   },
            { Biome.Grasslands,    0.5f },
            { Biome.Desert,        0.5f },
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
                        .Where(a => (a.Value & biome) != 0 &&
                                    BlockFlagMapping[a.Key].HasFlag(BlockFlags.SurfaceDeco))
                        .Select(a => a.Key)
                ];
            }
            
            _surfaceDecoBlocks = [.. BlockFlagMapping
                    .Where(f =>
                    f.Value.HasFlag(BlockFlags.SurfaceDeco))
                    .Select(f => f.Key)];
           
            _naturalBlocks = [.. BlockFlagMapping
                .Where(kvp => kvp.Value.HasFlag(BlockFlags.Natural))
                .Select(kvp => kvp.Key)];

            _texturesWithLayers = [.. TextureFlagMapping
                .Where(kvp => kvp.Value.HasFlag(TextureFlags.HasLayer))
                .Select(kvp => kvp.Key)
                .OrderBy(GetTextureLayerFromTexture)];

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
            _models.Add(BlockType.Grass,       new(JObject.Parse(File.ReadAllText(Path.Combine(assets, "BlockModels", "grass.json"))),      BlockType.Grass,      this));
            _models.Add(BlockType.Dirt,        new(JObject.Parse(File.ReadAllText(Path.Combine(assets, "BlockModels", "dirt.json"))),       BlockType.Dirt,       this));
            _models.Add(BlockType.Stone,       new(JObject.Parse(File.ReadAllText(Path.Combine(assets, "BlockModels", "stone.json"))),      BlockType.Stone,      this));
            _models.Add(BlockType.Test,        new(JObject.Parse(File.ReadAllText(Path.Combine(assets, "BlockModels", "testblock.json"))),  BlockType.Test,       this));
            _models.Add(BlockType.Target,      new(JObject.Parse(File.ReadAllText(Path.Combine(assets, "BlockModels", "target.json"))),     BlockType.Target,     this));
            _models.Add(BlockType.Lamp,        new(JObject.Parse(File.ReadAllText(Path.Combine(assets, "BlockModels", "lamp.json"))),       BlockType.Lamp,  this));
            _models.Add(BlockType.TallGrass,   new(JObject.Parse(File.ReadAllText(Path.Combine(assets, "BlockModels", "tall_grass.json"))), BlockType.TallGrass,  this));
            _models.Add(BlockType.DeadBush,    new(JObject.Parse(File.ReadAllText(Path.Combine(assets, "BlockModels", "dead_bush.json"))),  BlockType.DeadBush,   this));
            _models.Add(BlockType.Sand,        new(JObject.Parse(File.ReadAllText(Path.Combine(assets, "BlockModels", "sand.json"))),       BlockType.Sand,   this));
        }
        public bool IsNatural(BlockType type)
            => BlockFlagMapping.TryGetValue(type, out var f) && f.HasFlag(BlockFlags.Natural);

        public bool IsBillboard(BlockType type)
            => BlockFlagMapping.TryGetValue(type, out var f) && f.HasFlag(BlockFlags.Billboard);
        
        public bool IsPassthrough(BlockType type)
        => BlockFlagMapping.TryGetValue(type, out var f) && f.HasFlag(BlockFlags.Passthrough);

        public Dictionary<Biome, float> GetBiomeWeights()
        {
            return _biomeWeights;
        }

        public BlockFlags GetBlockFlags(BlockType type)
        {
            return BlockFlagMapping[type];
        }
        
        public List<BlockType> GetTerrainBlocks(Biome biome)
        {
            List<BlockType> types =
            [
                .. BiomeMapping
                    .Where(b => (b.Value & biome) != 0)
                    .Select(b => b.Key)
                    .Where(type =>
                        !GetBlockFlags(type).HasFlag(BlockFlags.SurfaceDeco) &&
                        GetBlockFlags(type).HasFlag(BlockFlags.Natural))
            ];
            
            return types;
        }

        public List<BlockType> GetSurfaceDeco(Biome biome)
        {
            return _biomeSurfaceDeco[biome];
        }
        
        public Tuple<Vector2, Vector2> GetUvFromBlockType(BlockType blockType)
        {
            if (_blockTypeToIconUv.TryGetValue(blockType, out Tuple<Vector2, Vector2>? uvs) && uvs != null)
                return uvs;
            
            return new Tuple<Vector2, Vector2>(Vector2.Zero, Vector2.Zero);
        }

        public int GetTextureLayerFromTexture(Texture texture)
        {
            //Subtract by 1 to account for AIR texture
            if (_textureToTextureLayer.TryGetValue(texture, out int layer))
                return layer;
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
            return _models.TryGetValue(type, out BlockModel? value) ? value : new(this);
        }
    }
}
