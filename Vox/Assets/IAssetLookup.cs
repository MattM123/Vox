using System.Numerics;
using Vox.Assets.Models;
using Vox.Enums;

namespace Vox.Assets
{
    public interface IAssetLookup
    {
        int GetTextureLayerFromTexture(Texture texture);
        bool IsNatural(BlockType type);
        List<BlockType> GetTerrainBlocks(Biome biome);
        List<BlockType> GetSurfaceDeco(Biome biome);
        bool IsPassthrough(BlockType type);
        bool IsBillboard(BlockType type);
        List<BlockType> GetNaturalBlockTypes();
        Dictionary<Biome, float> GetBiomeWeights();
        BlockFlags GetBlockFlags(BlockType type);
        List<Texture> GetTexturesWithLayers();
        BlockModel GetModel(BlockType type);
        Tuple<Vector2, Vector2> GetUvFromBlockType(BlockType blockType);

    }
}