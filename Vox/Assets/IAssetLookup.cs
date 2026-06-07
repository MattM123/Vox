using System.Numerics;
using Vox.Assets.Models;
using Vox.Enums;

namespace Vox.Assets
{
    public interface IAssetLookup
    {
        int GetTextureLayerFromTexture(Texture texture);
        bool IsNatural(BlockType type);
        List<BlockType> GetNaturalBlockTypes();
        List<Texture> GetTexturesWithLayers();
        BlockModel GetModel(int type);
        BlockModel GetModel(BlockType type);
        Tuple<Vector2, Vector2> GetUVFromBlockType(BlockType blockType);

    }
}