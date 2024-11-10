using MessagePack;
using Vox.Genesis;

namespace Vox.Rendering
{
    /**
    * Light node used with BFS (Breadth-First Search) algorithm
    * to determine light levels of block faces
    */

    [MessagePackObject]
    public struct LightNode(short index, Chunk chunk)
    {
        [Key(0)]
        public short Index { get; set; } = index;
        [Key(1)]
        public Chunk Chunk { get; set; } = chunk;
    }
}
