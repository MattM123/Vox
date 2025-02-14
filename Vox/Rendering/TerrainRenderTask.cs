using MessagePack;
using OpenTK.Mathematics;
using Vox.Genesis;

namespace Vox.Rendering
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

    [MessagePackObject]
    public class TerrainRenderTask
    {

        [Key(0)]
        public List<TerrainVertex> vertexData;

        [Key(1)]
        public List<int> elementData;

        [Key(2)]
        public int vbo;

        [Key(3)]
        public int ebo;

        [Key(4)]
        public int vao;

        private Matrix4 modelMatrix;

        [SerializationConstructor]
        public TerrainRenderTask(List<TerrainVertex> vertexData, List<int> elementData, int vbo, int ebo, int vao)
        {
            this.vertexData = vertexData;
            this.elementData = elementData;
            this.vbo = vbo;
            this.ebo = ebo;
            this.vao = vao;
            modelMatrix = Chunk.GetModelMatrix();

        }
        public TerrainVertex[] GetVertexData()
        {
            return [.. vertexData];
        }
        public int[] GetElementData()
        {
            return [.. elementData];
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

        public void SetVertexData(List<TerrainVertex> vertexData)
        {
            this.vertexData = vertexData;
        }
        public override string ToString()
        {
            return $"    VBO: {vbo},\n " +
                   $"   EBO: {ebo},\n " +
                   $"   VAO:{vao},\n " +
                   $"   Vertex Length: {vertexData.Count},\n" +
                   $"    Elements: {elementData.Count}\n";
        }
    }

}