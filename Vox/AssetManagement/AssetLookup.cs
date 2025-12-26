using Vox.Enums;
using Vox.Model;

namespace Vox.AssetManagement
{
    internal static class AssetLookup
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

        public static int GetTextureValue(string filename)
        {
            if (FilenameToTexture.TryGetValue(filename, out Texture texture))
                return (int)texture;
            return (int)Texture.AIR;
        }

    }
}
