namespace Vox.Rendering
{
    public interface ITextureLoader
    {
        int LoadTextures(int slot);
        int LoadSingleTexture(string fileBlockName);
       // void Dispose(int texId);
    }
}