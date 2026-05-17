using Vox.Enums;

namespace Vox.Assets
{
    public interface IAssetLookup
    {
        int GetTextureValueFromFilename(string filename);
        string GetFileFromBlockType(BlockType blockType);
    }
}