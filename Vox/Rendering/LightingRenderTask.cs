﻿using MessagePack;
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
    public class LightingRenderTask
    {

        [Key(0)]
        public List<LightingVertex> vertexData;

        [Key(1)]
        public int vbo;

        [Key(2)]
        public int ebo;

        [Key(3)]
        public int vao;

        private Matrix4 modelMatrix;

        [SerializationConstructor]
        public LightingRenderTask(List<LightingVertex> vertexData, int vbo, int ebo, int vao)
        {
            this.vertexData = vertexData;
            this.vbo = vbo;
            this.ebo = ebo;
            this.vao = vao;
            modelMatrix = Chunk.GetModelMatrix();

        }
        public LightingVertex[] GetVertexData()
        {
            return [.. vertexData];
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

        public void SetVertexData(List<LightingVertex> vertexData)
        {
            this.vertexData = vertexData;
        }
        public override string ToString()
        {
            return $"    VBO: {vbo},\n " +
                   $"   EBO: {ebo},\n " +
                   $"   VAO:{vao},\n " +
                   $"   Vertex Length: {vertexData.Count}";
        }
    }

}