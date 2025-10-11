using MessagePack;
using Vox.Genesis;

namespace Vox.Rendering
{
    /**
    * Light node used with BFS (Breadth-First Search) algorithm
    * to determine light levels of block faces
    */

    public struct LightNode(string index, Chunk chunk)
    {
        public string Index { get; set; } = index;
        public Chunk Chunk { get; set; } = chunk;
    }
}
