using OpenTK.Mathematics;
using Vox.Enums;
using Vox.Model;

namespace Vox.Genesis
{
    public interface IRegionManager
    {

        int GetChunkBounds();
        void AddBlockToChunk(Vector3 blockSpace, BlockType type, ColorVector blockLight);
        void AddBlockToChunk(Vector3 blockSpace, BlockType type, ColorVector blockLight, bool colorOverride);
        short GetGlobalHeightMapValue(int x, int z);
        BlockType GetGeneratedBlockType(int x, int y, int z);
        BlockType GetSurfaceDecoration(int x, int z, Biome biome);
        int PollChunkMemory();
        int GetRenderDistance();
        public Chunk GetAndLoadGlobalChunkFromCoords(Vector3 v);
        BlockType GetBlockTypeAtCoords(float x, float y, float z);
        Chunk GetAndLoadGlobalChunkFromCoords(int x, int y, int z);
        Dictionary<string, Region> GetVisibleRegions();
        bool IsRegionVisible(string rIndex);
        Region EnterRegion(string rIndex);
        void EnterRegion(Region region);
        Region TryGetRegionFromFile(string rIndex);
        int GetWorldHeight();
        void SetRegionDir(string path);
        string GetRegionIndexFromChunkCoords(int chunkX, int chunkZ);
        void LeaveRegion(string rIndex);
        void RemoveBlockFromChunk(Vector3 blockSpace);
        void ProcessChunkSite(Chunk c);
        long GetSeed();
        void PropagateBlockLight(Vector3 location, BlockFace faceDir, bool depropagate, bool colorOverride);
        Vector3i GetChunkRelativeCoordinates(Vector3 v);
        int GetRegionBounds();
        void ClearVisibleRegions();
        Biome GetBiomeAtLocation(int x, int z);
    }
}
