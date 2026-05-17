using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
