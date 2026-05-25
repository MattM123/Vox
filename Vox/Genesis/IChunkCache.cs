
namespace Vox.Genesis
{
    public interface IChunkCache
    {
        void SetRenderDistance(int renderDistance);
        void SetPlayerChunk(Chunk c);
        public void GetRadialChunks();
        void ClearChunkCache();
        Dictionary<string, Chunk> UpdateChunkCache();
        Dictionary<string, Region> GetRegions();
    }
}
