
using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using OpenTK.Mathematics;
using Vox.Comparator;
using Vox.Model;
using Vox.Texturing;
using GL = OpenTK.Graphics.OpenGL4.GL;


namespace Vox.Genesis
{
    public class Chunk
    {
        private Vector3 location;
        private bool rerender = false;
        private readonly List<Block> blocks = [];
        private readonly int[,] heightMap = new int[RegionManager.CHUNK_BOUNDS, RegionManager.CHUNK_BOUNDS];
        private bool isInitialized = false;
        private int vbo = 0;
        private int ebo = 0;
        private int vao = 0;
        private static Matrix4 modelMatrix = new();
        private List<float> chunkVerts = [];
        private List<int> chunkEle = [];
        private bool didChange = false;
        private readonly object chunkLock = new();
        private RenderTask renderTask;

        public Chunk()
        {
            //GL work must be done on main trhead
        //    Window.GetMainThreadQueue().Enqueue(() =>
        //    {
                SetEbo(GL.GenBuffer());
                SetVbo(GL.GenBuffer());
                SetVao(GL.GenVertexArray());
        //    });
        }

        /**
         * Initializes a chunk at a given point and populates it's heightmap using Simplex noise
         * @param x coordinate of top left chunk corner
         * @param y coordinate of top left chunk corner
         * @param z coordinate of top left chunk corner
         * @return Returns the chunk
         */
        public Chunk Initialize(float x, float z)
        {
            location = new Vector3(x, 0, z);

            //Generic model matrix applicable to every chunk object
            modelMatrix = Matrix4.CreateTranslation(0, 0, 0);// * Matrix4.CreateScale(-1, 1, -1);// * Matrix4.CreateFromAxisAngle(Vector3.UnitY, MathHelper.DegreesToRadians(45));// * Matrix4.CreateScale(1, 1, 1);

            if (!isInitialized)
            {
                //===================================
                //Generate chunk height map
                //===================================

                int xCount = 0;
                int zCount = 0;

                for (int x1 = (int)x; x1 < x + RegionManager.CHUNK_BOUNDS; x1++)
                {
                    for (int z1 = (int)z; z1 < z + RegionManager.CHUNK_BOUNDS; z1++)
                    {

                        int elevation = GetGlobalHeightMapValue(x1, z1);
                        heightMap[zCount, xCount] = elevation;

                        zCount++;
                        if (zCount > RegionManager.CHUNK_BOUNDS - 1)
                            zCount = 0;
                    }
                    xCount++;
                    if (xCount > RegionManager.CHUNK_BOUNDS - 1)
                        xCount = 0;
                }
                rerender = true;
            }
            return this;
        }

        /**
         * Given a 2D chunk heightmap, interpolates between
         * height-mapped blocks to fill in vertical gaps in terrain generation.
         * Populates this chunk with blocks by making comparisons between the
         * 4 cardinal blocks of each block.

         * All blocks added to the chunk are pre-transformed using the chunks
         * model matrix

         *       |-----|              |-----|
         *       |  d  |              |  a  |
         * |-----|-----|-----|        |-----|
         * |  c  |base |  a  |        |  ?  | <- unknown
         * |-----|-----|-----|  |-----|-----|
         *       |  b  |        | base|
         *       |-----|        |-----|
         *       
         *  Returns a Vector3f list containg all points. These vertices
         *  will be assigned block types and texture infromation in GetRenderTask()
         *  
         */
        private List<Vector3> InterpolateChunk(List<Vector3> inVert)
        {
            //TODO: Determine BlockType based on noise

            List<Vector3> output = [];

            for (int row = 0; row < heightMap.GetLength(0); row++)
            {
                for (int col = 0; col < heightMap.GetLength(1); col++)
                {
                    int base1 = heightMap[row, col];

                    //horizontal comparisons
                    int comparison1; //-1
                    int comparison2; //+1

                    //vertical comparisons
                    int comparison3; //-1
                    int comparison4; //+1

                    //-1 horizontal comparison
                    if (col > 0)
                        comparison1 = heightMap[row, col - 1];
                    else
                        comparison1 = GetGlobalHeightMapValue((int)(col + GetLocation().X - 1), (int)(row + GetLocation().Z));

                    //+1 horizontal comparison
                    if (col < RegionManager.CHUNK_BOUNDS - 1)
                        comparison2 = heightMap[row, col + 1];
                    else
                        comparison2 = GetGlobalHeightMapValue((int)(col + GetLocation().X + 1), (int)(row + GetLocation().Z));

                    //-1 2d vertical comparison
                    if (row > 0)
                        comparison3 = heightMap[row - 1, col];
                    else
                        comparison3 = GetGlobalHeightMapValue((int)(col + GetLocation().X), (int)(row + GetLocation().Z - 1));

                    //+1 2d vertical comparison
                    if (row < RegionManager.CHUNK_BOUNDS - 1)
                        comparison4 = heightMap[row + 1, col];
                    else
                        comparison4 = GetGlobalHeightMapValue((int)(col + GetLocation().X), (int)(row + GetLocation().Z + 1));

                    //Adds base by default since that will always be visible and rendered
                    if (!inVert.Contains(new Vector3(col + GetLocation().X, base1, row + GetLocation().Z)))
                    {
                        // inVert.Add(new Block(modelMatrix.transformPosition(new Vector3(col + GetLocation().X, base1, row + GetLocation().Z)), BlockType.DIRT_BLOCK));
                        //inVert.Add(new Block(col + GetLocation().X, base1, row + GetLocation().Z, BlockType.DIRT_BLOCK));
                        output.Add(new Vector3(col + GetLocation().X, base1, row + GetLocation().Z));
                    }


                    //Populates chunk vertex list. Base needs to be larger than at least one
                    //comparison for any vertical blocks to be added
                    if (base1 > comparison1)
                    {
                        if (base1 - comparison1 > 1)
                        {
                            int numOfBlocks = base1 - comparison1;

                            for (int i = 0; i < numOfBlocks; i++)
                            {
                                if (!inVert.Contains(new Vector3(col + GetLocation().X, base1 - i, row + GetLocation().Z)))
                                    //  inVert.Add(new Block(modelMatrix.transformPosition(new Vector3(col + GetLocation().X, base1 - i, row + GetLocation().Z)), BlockType.DIRT_BLOCK));
                                    //inVert.Add(new Block(col + GetLocation().X, base1 - i, row + GetLocation().Z, BlockType.DIRT_BLOCK));
                                    output.Add(new Vector3(col + GetLocation().X, base1 - i, row + GetLocation().Z));
                            }
                        }
                    }

                    if (base1 > comparison2)
                    {
                        if (base1 - comparison2 > 1)
                        {
                            int numOfBlocks = base1 - comparison2;

                            for (int i = 0; i < numOfBlocks; i++)
                            {
                                if (!inVert.Contains(new Vector3(col + GetLocation().X, base1 - i, row + GetLocation().Z)))
                                    //   inVert.Add(new Block(modelMatrix.transformPosition(new Vector3(col + GetLocation().X, base1 - i, row + GetLocation().Z)), BlockType.DIRT_BLOCK));
                                    //inVert.Add(new Block(col + GetLocation().X, base1 - i, row + GetLocation().Z, BlockType.DIRT_BLOCK));
                                    output.Add(new Vector3(col + GetLocation().X, base1 - i, row + GetLocation().Z));
                            }
                        }
                    }

                    if (base1 > comparison3)
                    {
                        if (base1 - comparison3 > 1)
                        {
                            int numOfBlocks = base1 - comparison3;

                            for (int i = 0; i < numOfBlocks; i++)
                            {
                                if (!inVert.Contains(new Vector3(col + GetLocation().X, base1 - i, row + GetLocation().Z)))
                                    //  inVert.Add(new Block(modelMatrix.transformPosition(new Vector3(col + GetLocation().X, base1 - i, row + GetLocation().Z)), BlockType.DIRT_BLOCK));
                                    //inVert.Add(new Block(col + GetLocation().X, base1 - i, row + GetLocation().Z, BlockType.DIRT_BLOCK));
                                    output.Add(new Vector3(col + GetLocation().X, base1 - i, row + GetLocation().Z));
                            }
                        }
                    }

                    if (base1 > comparison4)
                    {
                        if (base1 - comparison4 > 1)
                        {
                            int numOfBlocks = base1 - comparison4;

                            for (int i = 0; i < numOfBlocks; i++)
                            {
                                if (!inVert.Contains(new Vector3(col + GetLocation().X, base1 - i, row + GetLocation().Z)))
                                    // inVert.Add(new Block(modelMatrix.transformPosition(new Vector3(col + GetLocation().X, base1 - i, row + GetLocation().Z)), BlockType.DIRT_BLOCK));
                                    //inVert.Add(new Block(col + GetLocation().X, base1 - i, row + GetLocation().Z, BlockType.DIRT_BLOCK));
                                    output.Add(new Vector3(col + GetLocation().X, base1 - i, row + GetLocation().Z));
                            }
                        }
                    }
                }
            }
            output.AddRange(inVert);
            return output;
        }

        /**
         * Since the location of each chunk is unique this is used as an
         * identifier by the ChunkManager to retrieve, insert, and
         * effectively sort chunks.
         * @return The corner vertex of this chunk.
         */
        public Vector3 GetLocation()
        {
            return location;
        }

        public float[] GetVertices()
        {
            return [.. chunkVerts];
        }
        public int[] GetElements()
        {
            return [.. chunkEle];
        }
        /**
         * If this region is currently visible to the player (in-memory), this region will
         * be returned populated with chunk data. This region will otherwise be an empty object
         * which would need to be populated with data by the RegionManager when the player
         * enters the region.
         *
         * @return Returns the region that the chunk belongs to.
         */
        public Region GetRegion()
        {
            Region returnRegion = null;

            int x = (int)GetLocation().X;
            int xLowerLimit = x / RegionManager.REGION_BOUNDS * RegionManager.REGION_BOUNDS;
            int xUpperLimit;
            if (x < 0)
                xUpperLimit = xLowerLimit - RegionManager.REGION_BOUNDS;
            else
                xUpperLimit = xLowerLimit + RegionManager.REGION_BOUNDS;


            int z = (int)GetLocation().Z;
            int zLowerLimit = z / RegionManager.REGION_BOUNDS * RegionManager.REGION_BOUNDS;
            int zUpperLimit;
            if (z < 0)
                zUpperLimit = zLowerLimit - RegionManager.REGION_BOUNDS;
            else
                zUpperLimit = zLowerLimit + RegionManager.REGION_BOUNDS;


            //Calculates region coordinates chunk inhabits
            int regionXCoord = xUpperLimit;
            int regionZCoord = zUpperLimit;

            foreach (Region region in RegionManager.VisibleRegions)
            {
                Rectangle regionBounds = region.GetBounds();
                if (regionXCoord == regionBounds.X && regionZCoord == regionBounds.Y)
                {
                    returnRegion = region;
                }
            }

            if (returnRegion == null)
            {
                returnRegion = new Region(regionXCoord, regionZCoord);
                RegionManager.VisibleRegions.Add(returnRegion);
            }

            return returnRegion;
        }

        /**
         * Generates or regenerates this Chunks RenderTask. The RenderTask is
         * used to graphically render the Chunk. Calling this method will, if
         * needed, automatically update this chunks vertex and element data and
         * return a new RenderTask that can be passed to the GPU when drawing.
         *
         * @return A RenderTask whose regularly updated contents can be
         * used in GL draw calls to render this Chunk graphically
         */
        [MethodImpl(MethodImplOptions.Synchronized)]
        public RenderTask GetRenderTask()
        {
            List<Vector3> nonInterpolated = [];
            List<float> vertices = [];
            List<int> elements = [];
            int elementCounter = 0;

            BlockModel model = ModelLoader.GetModel(BlockType.DIRT_BLOCK);
            Array values = Enum.GetValues(typeof(BlockType));
            Random random = new();

            if (didChange || renderTask == null)
            {
                for (int x = 0; x < heightMap.GetLength(0); x++) // GetLength(0) gives the number of rows
                {
                    for (int z = 0; z < heightMap.GetLength(1); z++) // GetLength(1) gives the number of columns
                    {
                        nonInterpolated.Add(new Vector3(location.X + x, heightMap[z, x], location.Z + z));
                    }
                }

                List<Vector3> interpolatedChunk = InterpolateChunk(nonInterpolated);
                for (int i = 0; i < interpolatedChunk.Count; i++)
                {
                    int randomIndex = random.Next(values.Length);
                    BlockType? randomBlock = (BlockType?) values.GetValue(randomIndex);
                    if (randomBlock != null)
                        model = ModelLoader.GetModel((BlockType) randomBlock);
                    else
                        model = ModelLoader.GetModel(BlockType.GRASS_BLOCK);

                    vertices.AddRange(ModelUtils.GetCuboidFace(model, "south", new Vector3(interpolatedChunk[i].X, interpolatedChunk[i].Y, interpolatedChunk[i].Z)));
                    vertices.AddRange(ModelUtils.GetCuboidFace(model, "north", new Vector3(interpolatedChunk[i].X, interpolatedChunk[i].Y, interpolatedChunk[i].Z)));
                    vertices.AddRange(ModelUtils.GetCuboidFace(model, "up", new Vector3(interpolatedChunk[i].X, interpolatedChunk[i].Y, interpolatedChunk[i].Z)));
                    vertices.AddRange(ModelUtils.GetCuboidFace(model, "down", new Vector3(interpolatedChunk[i].X, interpolatedChunk[i].Y, interpolatedChunk[i].Z)));
                    vertices.AddRange(ModelUtils.GetCuboidFace(model, "west", new Vector3(interpolatedChunk[i].X, interpolatedChunk[i].Y, interpolatedChunk[i].Z)));
                    vertices.AddRange(ModelUtils.GetCuboidFace(model, "east", new Vector3(interpolatedChunk[i].X, interpolatedChunk[i].Y, interpolatedChunk[i].Z)));

                    elements.AddRange([
                        elementCounter,      elementCounter + 1,  elementCounter + 2,   elementCounter + 3, 80000,
                    elementCounter + 4,  elementCounter + 5,  elementCounter + 6,   elementCounter + 7, 80000,
                    elementCounter + 8,  elementCounter + 9,  elementCounter + 10,  elementCounter + 11, 80000,
                    elementCounter + 12, elementCounter + 13, elementCounter + 14,  elementCounter + 15, 80000,
                    elementCounter + 16, elementCounter + 17, elementCounter + 18,  elementCounter + 19, 80000,
                    elementCounter + 20, elementCounter + 21, elementCounter + 22,  elementCounter + 23, 80000,
                ]);

                    elementCounter += 24;
                }

              //  //Updates chunk data
                lock (chunkLock)
                {
                    renderTask = new RenderTask(this, vertices, elements, GetVbo(), GetEbo(), GetVao(), modelMatrix);
                    didChange = false;
                    chunkVerts = vertices;
                    chunkEle = elements;
                  //  Logger.Info($"{GetRegion()} updating {this} with RenderTask:\n {renderTask}");
                }
            }
            return renderTask;
        }

        /**
         * This flag should only be updated to true when the chunk needs to re-render.
         * Ex. If a block was destroyer or placed or otherwise modified
         * */
        public void DidChange(bool b)
        {
            didChange = b;
        }

        /**
         * Searches for an index to insert a new Block at in O(log n) time complexity.
         * Ensures the list is sorted by the Blocks location as new Blocks are inserted into it.
         *
         * @param l The farthest left index of the list
         * @param r The farthest right index of the list
         * @param v The coordinate to search for.
         * @return Returns the Block that was just inserted into the list.
         */
        /*
        private Block BinaryInsertBlockWithLocation(int l, int r, Vector3 v)
        {
            ChunkComparator comparator = new();
            Block b = new(v.X, v.Y, v.Z);

            if (chunkBlocks.Count == 0)
            {
                chunkBlocks.Add(b);
            }
            if (chunkBlocks.Count == 1)
            {
                //Inserts element as first in list
                if (comparator.Compare(v, chunkBlocks[0].GetLocation()) < 0)
                {
                    chunkBlocks.Insert(0, b);
                    return b;
                }
                //Appends to end of list
                if (comparator.Compare(v, chunkBlocks[0].GetLocation()) > 0)
                {
                    chunkBlocks.Add(b);
                    return b;
                }
            }

            if (r >= l && chunkBlocks.Count > 1)
            {
                int mid = l + (r - l) / 2;
                //When an index has been found, right and left will be very close to each other
                //Insertion of the right index will shift the right element
                //and all subsequent ones to the right.
                if (Math.Abs(r - l) == 1)
                {
                    chunkBlocks.Insert(r, b);
                    return b;
                }

                //If element is less than first element insert at front of list
                if (comparator.Compare(v, chunkBlocks[0].GetLocation()) < 0)
                {
                    chunkBlocks.Insert(0, b);
                    return b;
                }
                //If element is more than last element insert at end of list
                if (comparator.Compare(v, chunkBlocks[chunkBlocks.Count - 1].GetLocation()) > 0)
                {
                    chunkBlocks.Add(b);
                    return b;
                }

                //If the index is near the middle
                if (comparator.Compare(v, chunkBlocks[mid - 1].GetLocation()) > 0
                        && comparator.Compare(b.GetLocation(), chunkBlocks[mid].GetLocation()) < 0)
                {
                    chunkBlocks.Insert(mid, b);
                    return b;
                }
                if (comparator.Compare(v, chunkBlocks[mid + 1].GetLocation()) < 0
                        && comparator.Compare(v, chunkBlocks[mid].GetLocation()) > 0)
                {
                    chunkBlocks.Insert(mid + 1, b);
                    return b;
                }

                // If element is smaller than mid, then
                // it can only be present in left subarray
                if (comparator.Compare(v, chunkBlocks[mid].GetLocation()) < 0)
                {
                    return BinaryInsertBlockWithLocation(l, mid - 1, v);
                }

                // Else the element can only be present
                // in right subarray
                return BinaryInsertBlockWithLocation(mid + 1, r, v);

            }
            else
            {
                return null;
            }
        }
        */

        /**
         * Searches for a Block in O(log n) time complexity and returns it.
         *
         * @param l The farthest left index of the list
         * @param r The farthest right index of the list
         * @param v The coordinate to search for.
         * @return Returns the Block if found. Else null.
         */
        /*
        private Block? BinarySearchBlockWithLocation(int l, int r, Block v)
        {
            BlockComparator comparator = new();

            if (r >= l)
            {
                int mid = l + (r - l) / 2;

                // If the element is present at the middle
                if (comparator.Compare(v, (Block?)chunkBlocks[mid]) == 0)
                {
                    // System.out.println("Found Equal: " + v + "   " + chunkBlocks[mid]);
                    return (Block?)chunkBlocks[mid];
                }

                // If element is smaller than mid, then
                // it can only be present in left subarray
                if (comparator.Compare(v, (Block?)chunkBlocks[mid]) < 0)
                {
                    return BinarySearchBlockWithLocation(l, mid - 1, v);
                }

                // Else the element can only be present
                // in right subarray
                if (comparator.Compare(v, (Block?)chunkBlocks[mid]) > 0)
                {
                    return BinarySearchBlockWithLocation(mid + 1, r, v);
                }
            }
            return null;

        }
        */

        public static Matrix4 GetModelMatrix()
        {
            return modelMatrix;
        }
        /*
        @Serial
    private void writeObject(ObjectOutputStream o)
        {
            writeChunk(o, this);
        }

        @Serial
    private void readObject(ObjectInputStream o)
        {
            if (!isInitialized)
            {
                return;
            }
            this.chunkBlocks = readChunk(o).chunkBlocks;
        }

        private void writeChunk(OutputStream stream, Chunk c)
        {
            if (this.equals(c)) return;
            System.out.println("Writing chunk for " + c.GetRegion());
            Main.executor.execute(()-> {
                try
                {
                    FSTObjectOutput out = Main.GetInstance().GetObjectOutput(stream);
                out.writeObject(c, Chunk.class);
                out.flush();
    } catch (Exception e) {
                logger.warning(e.GetMessage());
            }
        });
    }

    private Chunk readChunk(InputStream stream)
{

    AtomicReference<Chunk> c = new AtomicReference<>();
    Main.executor.execute(()-> {
        FSTObjectInput in = Main.GetInstance().GetObjectInput(stream);
        try
        {
            c.set((Chunk) in.readObject(Chunk.class));
stream.close();
            } catch (Exception ignored) {
            }

            try
{ in.close();
}
catch (Exception e) { e.printStackTrace(); }
        });
return c.Get();
    }

    /**
     * @return True if this chunk should be re-rendered on the next frame, false if this chunk
     * has not been modified in any way and therefore should not be re-rendered.
     */
        public bool ShouldRerender()
        {
            return rerender;
        }

        /**
       * Retrieves the Y value for any given x,z column in any chunk
       * @param x coordinate of column
       * @param z coordinate of column
       * @return Returns the noise value which is scaled between 0 and CHUNK_HEIGHT
       */
        public static int GetGlobalHeightMapValue(int x, int z)
        {
            //Affects height of terrain. A higher value will result in lower, smoother terrain while a lower value will result in
            // a rougher, raised terrain
            float var1 = 12;

            //Affects coalescence of terrain. A higher value will result in more condensed, sharp peaks and a lower value will result in
            //more smooth, spread out hills.
            double var2 = 0.01;


            float f = 1 * OpenSimplex2.Noise2(RegionManager.WORLD_SEED, x * var2, z * var2) / (var1 + 2) //Noise Octave 1
                    + (float)(0.5 * OpenSimplex2.Noise2(RegionManager.WORLD_SEED, x * (var2 * 2), z * (var2 * 2)) / (var1 + 4)) //Noise Octave 2
                    + (float)(0.25 * OpenSimplex2.Noise2(RegionManager.WORLD_SEED, x * (var2 * 2), z * (var2 * 2)) / (var1 + 6)); //Noise Octave 3

            int min = 0;
            return (int)Math.Floor((f + 1) / 2 * RegionManager.CHUNK_HEIGHT - 1);

        }

        /**
         * Set weather this Chunk should be marked for re-render or not
         * @param b True if this Chunk should be re-rendered, false otherwise
         */
        public void SetRerender(bool b)
        {
            rerender = b;
        }

        /**
         * Element Buffer Object specific to this chunk used in the
         * Chunks RenderTask and for drawing the chunks data
         * @param i EBO ID. Ideally using glGenBuffers() as the parameter
         */
        public void SetEbo(int i)
        {
            ebo = i;
        }
        public int GetEbo()
        {
            return ebo;
        }
        public int GetVao()
        {
            return vao;
        }
        /**
         * Vertex Buffer Object specific to this chunk used in the
         * Chunks RenderTask and for drawing the chunks data
         * @param i VBO ID. Ideally using glGenBuffers() as the parameter
         */
        public void SetVbo(int i)
        {
            vbo = i;
        }
        public void SetVao(int i)
        {
            vao = i;
        }
        public int GetVbo()
        {
            return vbo;
        }
        /**
         * Each Chunk is identified by its location in three-dimensional space,
         * this value is unique to all chunks and is therefore used to compare
         * Chunk objects to each other in conjunction with the chunks heightmap.
         * Two chunks will be equal if their location and heightmap are the same.
         *
         * @param obj The object to compare
         * @return True if the chunks are equal, false if not
         */
     //   public bool Equals(object? obj)
     //   {
      //      if (obj?.GetType() == typeof(Chunk))
      //      {
      //          if (GetLocation().X.Equals(((Chunk)obj).GetLocation().X)
      //              && GetLocation().Z.Equals(((Chunk)obj).GetLocation().Z))
      //              return true;
      //      }
      //      else return false;
      //      return true;
       // }

        public override string ToString()
        {
            return "Chunk[" + location.X + "," + location.Z + "]";
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}