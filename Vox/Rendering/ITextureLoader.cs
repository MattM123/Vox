namespace Vox.Rendering
{
    public interface ITextureLoader
    {
        int LoadTextures();
        int LoadSingleTexture(string fileBlockName);
        int UpdateExistingTexture(int texId, int layer);
        int GenerateIconAtlas();
    }
}