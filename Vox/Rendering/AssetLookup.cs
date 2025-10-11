
using Vox.Enums;
using Vox.Model;

namespace Vox.Rendering
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

        public static int GetTextureValue(string filename)
        {
            if (FilenameToTexture.TryGetValue(filename, out Texture texture))
                return (int)texture;
            return (int)Texture.AIR;
        }

    }
}
