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
        BlockType GetGlobalBlockType(int x, int y, int z);
        int PollChunkMemory();
        int GetRenderDistance();
        public Chunk GetAndLoadGlobalChunkFromCoords(Vector3 v);
        Chunk GetAndLoadGlobalChunkFromCoords(int x, int y, int z);
        Dictionary<string, Region> GetVisibleRegions();
        Region EnterRegion(string rIndex);
        void EnterRegion(Region region);
        Region TryGetRegionFromFile(string rIndex);
        int GetWorldHeight();
        void SetRegionDir(string path);
        string GetRegionIndexFromChunkCoords(int chunkX, int chunkZ);
        void LeaveRegion(string rIndex);
        void RemoveBlockFromChunk(Vector3 blockSpace);
        void PropagateBlockLight(Vector3 location, BlockFace faceDir, bool depropagate, bool colorOverride);
        Vector3i GetChunkRelativeCoordinates(Vector3 v);
        int GetRegionBounds();
        void ClearVisibleRegions();
    }
}
