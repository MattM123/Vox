using Vox.Enums;

namespace Vox.Assets
{
    internal class AssetLookup : IAssetLookup
    {
        public static readonly Dictionary<string, Texture> FilenameToTexture = new()
        {
            {"", Texture.AIR },
            {"dirt.png", Texture.DIRT },
            {"grass_full.png", Texture.GRASS_FULL },
            {"grass_side.png", Texture.GRASS_SIDE },
            {"lamp.png", Texture.LAMP }, 
            {"stone.png", Texture.STONE },
            {"test.png", Texture.TEST },
            {"target.png", Texture.TARGET }
        };

        public static readonly Dictionary<BlockType, string> BlockTypeToIconFile = new()
        {
            {BlockType.DIRT_BLOCK, "dirt.png" },
            {BlockType.GRASS_BLOCK, "grass_side.png" },
            {BlockType.LAMP_BLOCK, "lamp.png" },
            {BlockType.STONE_BLOCK, "stone.png" },
            {BlockType.TEST_BLOCK, "test.png" },
            {BlockType.TARGET_BLOCK, "target.png" }
        };

        public AssetLookup() { }

        public int GetTextureValueFromFilename(string filename)
        {
            if (FilenameToTexture.TryGetValue(filename, out Texture texture))
                return (int)texture;
            return (int)Texture.AIR;
        }

        public string GetFileFromBlockType(BlockType blockType)
        {
            if (BlockTypeToIconFile.TryGetValue(blockType, out string filename))
                return filename;
            return "";
        }

    }
}
