namespace Vox.Rendering
{
    public interface ITextureLoader
    {
        int LoadTextures();
        int LoadSingleTexture(string fileBlockName);
        void CleanupTextures();
        void CleanupTexture(int texid);
    }
}