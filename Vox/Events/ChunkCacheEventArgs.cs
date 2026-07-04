using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vox.Genesis;

namespace Vox.Events
{
    public class ChunkCacheEventArgs : EventArgs
    {
        public Chunk Chunk { get; }

        public ChunkCacheEventArgs(Chunk chunk)
        {
            Chunk = chunk;
        }
    }
}
