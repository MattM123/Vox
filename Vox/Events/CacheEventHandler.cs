using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vox.Genesis;

namespace Vox.Events
{
    public class CacheEventHandler
    {
        public event EventHandler<ChunkCacheEventArgs>? ChunkCached;
        public event EventHandler<ChunkCacheEventArgs>? ChunkRemoved;

        public void CacheChunk(Chunk chunk)
        {
            chunk.SetCached(true);
            ChunkCached?.Invoke(this, new ChunkCacheEventArgs(chunk));
        }
        public void RemoveFromCache(Chunk chunk)
        {
            chunk.SetCached(false);
            chunk.FreeShaderMemory();
            ChunkRemoved?.Invoke(this, new ChunkCacheEventArgs(chunk));
        }
    }
}
