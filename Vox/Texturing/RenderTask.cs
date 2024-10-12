
using OpenTK.Mathematics;
using Vox.Genesis;

namespace Vox.Texturing
{
    /**
     * RenderTask objects store GL primitive data relative to each chunk.
     * Each chunk has its own RenderTask that it generates or regenerates
     * when that chunk is flagged for re-rendering. The RenderTask contains a
     * VBO, EBO, vertex array, and element array used when performing draw
     * calls with glDrawElements
     *
     * @param vertexData float array constructed using the chunks heightmap
     * @param elementData int array constructed alongside the vertexData
     * @param vbo Chunk specific VBO used for rendering
     * @param ebo Chunk specific EBO used for rendering
     * @param modelMatrix The chunks model matrix to use for rendering
     */

    public class RenderTask(Chunk chunk, List<float> vertexData, List<int> elementData, int vbo, int ebo, int vao, Matrix4 modelMatrix)
    {
        private readonly float[] vertexData = [.. vertexData];
        private readonly int[] elementData = [.. elementData];
        private readonly Matrix4 modelMatrix = modelMatrix;

        public float[] GetVertexData()
        {
            return vertexData;
        }
        public int[] GetElementData()
        {
            return elementData;
        }
        public int GetVbo()
        {
            return vbo;
        }
        public int GetEbo()
        {
            return ebo;
        }
        public int GetVao()
        {
            return vao;
        }
        public Matrix4 GetModelMatrix()
        {
            return modelMatrix;
        }
        public Chunk GetChunk() { return chunk; }

        public override string ToString()
        {
            return $"   {chunk}, \n" +
                   $"    VBO: {vbo},\n " +
                   $"   EBO: {ebo},\n " +
                   $"   VAO:{vao},\n " +
                   $"   Vertex Length: {vertexData.Length},\n" +
                   $"    Elements: {elementData.Length}\n"; 
        }
    }

}
