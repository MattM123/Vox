namespace Vox.Rendering
{
    public interface ITextureLoader
    {
        int LoadTextures(int slot);
        int LoadSingleTexture(string fileBlockName);
        void CleanupTextures();
        void CleanupTexture(int texid);
    }
}