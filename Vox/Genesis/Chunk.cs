
using System.Drawing;
using MessagePack;
using OpenTK.Mathematics;
using Vox.Model;
using Vox.Rendering;
using Vox.Texturing;
using GL = OpenTK.Graphics.OpenGL4.GL;


namespace Vox.Genesis
{

    [MessagePackObject]
    public class Chunk
    {
        [Key(0)]
        public float xLoc;

        [Key(1)]
        public float zLoc;

        [Key(2)]
        public readonly int[,] heightMap = new int[RegionManager.CHUNK_BOUNDS, RegionManager.CHUNK_BOUNDS];

        [Key(3)]
        public bool isInitialized = false;

        [IgnoreMember]
        private Vector3 location;

        [IgnoreMember]
        private int vbo = 0;

        [IgnoreMember]
        private int ebo = 0;

        [IgnoreMember]
        private int vao = 0;

        [IgnoreMember]
        private static Matrix4 modelMatrix = new();

        [Key(4)]
        public bool didChange = false;

        [Key(5)]
        public RenderTask renderTask;
        
        [Key(6)]
        public short[,,] lightmap = new short[RegionManager.CHUNK_BOUNDS, RegionManager.CHUNK_HEIGHT, RegionManager.CHUNK_BOUNDS];

        [IgnoreMember]
        private readonly object chunkLock = new();

        [Key(8)]
        public Queue<LightNode> BFSPropagationQueue = new();

        [Key(9)]
        public Queue<LightNode> sunlightBFSPropagationQueue = new();
        
        [Key(10)]
        public List<float> blocksToAdd = [];
        public Chunk()
        {

            SetEbo(GL.GenBuffer());
            SetVbo(GL.GenBuffer());
            SetVao(GL.GenVertexArray());
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
            xLoc = x;
            zLoc = z;
            location = new Vector3(xLoc, 0, zLoc);

            //Generic model matrix applicable to every chunk object
            modelMatrix = Matrix4.CreateTranslation(0, 0, 0);

            if (!isInitialized)
            {
                lightmap = new short[RegionManager.CHUNK_BOUNDS, RegionManager.CHUNK_HEIGHT, RegionManager.CHUNK_BOUNDS];

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
         *  will be assigned block types and texture infromation in the render task
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

        //Contains face at?
        //face plane determined from forward direction
        public bool ContainsBlockAt(Vector3 block, out Vertex? v)
        {
            Vertex[] vertex = GetVertices();
            v = null;
            for (int i = 0; i < vertex.Length; i++)
            {
                if (vertex[i].x == block.X &&
                    vertex[i].y == block.Y &&
                    vertex[i].z == block.Z) 
                {
                    //fix: all vertices in the cube are based on one vertex
                    //so this will only return SOUTH or NORTH face vertex since
                    //those are defined first when the render task is being created.
                    //Need it so it returns a certain FACE. Have face as parameter? How do i determine 
                    //how which face to input?
                    v = vertex[i];
                    return true;
                }
            }
            return false;
        }

        public Vertex[] GetVertices()
        {
            return GetRenderTask().GetVertexData();
        }
        public int[] GetElements()
        {
            return renderTask.GetElementData();
        }
        public void DidChange(bool f)
        {
            didChange = f;
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
        public RenderTask GetRenderTask()
        {
            List<Vector3> nonInterpolated = [];
            List<Vertex> vertices = [];
            List<int> elements = [];
            int elementCounter = 0;

            Array values = Enum.GetValues(typeof(BlockType));

            Random random = new();

            if (didChange || renderTask == null)
            {
                for (int x = 0; x < heightMap.GetLength(0); x++) //rows          
                    for (int z = 0; z < heightMap.GetLength(1); z++) //columns       
                        nonInterpolated.Add(new Vector3(location.X + x, heightMap[z, x], location.Z + z));

                //Populate sunlight map before interpolating
              //  foreach (Vector3 v in nonInterpolated)
              //  {
                    //Does not need propagation algorithm, sunlight level always at 16 for top most blocks
                    //SetSunlight((int) v.X, (int)v.Y, (int)v.Z, 15);
               //     PropagateEmissiveBlock((int)v.X, (int)v.Y, (int)v.Z, 15);
               // }
;
                //Add any player placed blocks to the mesh before interpolation.
                List<Vector3> toVert = [];
                for (int i = 0; i < blocksToAdd.Count; i += 3)
                    toVert.Add(new(blocksToAdd[i], blocksToAdd[i + 1], blocksToAdd[i + 2]));

                nonInterpolated.AddRange(toVert);
                List<Vector3> interpolatedChunk = InterpolateChunk(nonInterpolated);

                for (int i = 0; i < interpolatedChunk.Count; i++)
                {
                    int randomIndex = random.Next(values.Length);
                    BlockType? randomBlock;
                    if (randomIndex > 1)
                        randomBlock = (BlockType?) values.GetValue(randomIndex - 2);
                    else
                        randomBlock = (BlockType?)values.GetValue(randomIndex);

                    BlockModel model;
                    if (randomBlock != null)
                        model = ModelLoader.GetModel((BlockType)randomBlock);
                    else
                        model = ModelLoader.GetModel(BlockType.TEST_BLOCK);

                    //TODO: Implement binary meshing here
                    vertices.AddRange(ModelUtils.GetCuboidFace(model, Face.SOUTH, new Vector3(interpolatedChunk[i].X, interpolatedChunk[i].Y, interpolatedChunk[i].Z), this));
                    vertices.AddRange(ModelUtils.GetCuboidFace(model, Face.NORTH, new Vector3(interpolatedChunk[i].X, interpolatedChunk[i].Y, interpolatedChunk[i].Z), this));
                    vertices.AddRange(ModelUtils.GetCuboidFace(model, Face.UP,    new Vector3(interpolatedChunk[i].X, interpolatedChunk[i].Y, interpolatedChunk[i].Z), this));
                    vertices.AddRange(ModelUtils.GetCuboidFace(model, Face.DOWN,  new Vector3(interpolatedChunk[i].X, interpolatedChunk[i].Y, interpolatedChunk[i].Z), this));
                    vertices.AddRange(ModelUtils.GetCuboidFace(model, Face.WEST,  new Vector3(interpolatedChunk[i].X, interpolatedChunk[i].Y, interpolatedChunk[i].Z),  this));
                    vertices.AddRange(ModelUtils.GetCuboidFace(model, Face.EAST,  new Vector3(interpolatedChunk[i].X, interpolatedChunk[i].Y, interpolatedChunk[i].Z),  this));

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

                //Updates chunk data
                lock (chunkLock)
                {
                    renderTask = new RenderTask(vertices , elements, GetVbo(), GetEbo(), GetVao());
                    didChange = false;
                }
            }
            return renderTask;
        }

        /**
         * This flag should only be updated to true when the chunk needs to re-render.
         * Ex. If a block was destroyer or placed or otherwise modified
         * */
        public void SetChange(bool b)
        {
            didChange = b;
        }
        public bool DidChange() { return didChange; }

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

        /**
       * Retrieves the Y value for any given x,z column in any chunk
       * @param x coordinate of column
       * @param z coordinate of column
       * @return Returns the noise value which is scaled between 0 and CHUNK_HEIGHT
       */
        public static int GetGlobalHeightMapValue(int x, int z)
        {
            long seed;
            if (Window.IsMenuRendered())
                seed = Window.GetMenuSeed();
            else
                seed = RegionManager.WORLD_SEED;
            
            //Affects height of terrain. A higher value will result in lower, smoother terrain while a lower value will result in
            // a rougher, raised terrain
            float var1 = 12;

            //Affects coalescence of terrain. A higher value will result in more condensed, sharp peaks and a lower value will result in
            //more smooth, spread out hills.
            double var2 = 0.01;

            float f = 1 * OpenSimplex2.Noise2(seed, x * var2, z * var2) / (var1 + 2) //Noise Octave 1
                    + (float)(0.5 * OpenSimplex2.Noise2(seed, x * (var2 * 2), z * (var2 * 2)) / (var1 + 4)) //Noise Octave 2
                    + (float)(0.25 * OpenSimplex2.Noise2(seed, x * (var2 * 2), z * (var2 * 2)) / (var1 + 6)); //Noise Octave 3

            int min = 0;
            return (int)Math.Floor((f + 1) / 2 * RegionManager.CHUNK_HEIGHT - 1);

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
        public int[,] GetHeightmap()
        {
            return heightMap;
        }


        //========================
        //Light helper functions
        //========================

        public void SetBlockLight(int x, int y, int z, int val)
        {

            lightmap[x,y,z] = (short) ((lightmap[x,y,z] & 0xF0) | val);

        }

        // Get the bits XXXX0000
        public int GetSunlight(int x, int y, int z)
        {
            return (lightmap[x,y,z] >> 4) & 0xF;
        }

        // Set the bits XXXX0000
        public void SetSunlight(int x, int y, int z, int val)
        {
            x = Math.Abs(x) % RegionManager.CHUNK_BOUNDS;
            y = Math.Abs(y) % RegionManager.CHUNK_BOUNDS;
            z = Math.Abs(z) % RegionManager.CHUNK_BOUNDS;
            lightmap[x,y,z] = (byte) ((lightmap[x,y,z] & 0xF) | (val << 4));

        }

        public int GetRedLight(int x, int y, int z)
        {
            return (lightmap[y,z,x] >> 8) & 0xF;
        }

        public void SetRedLight(int x, int y, int z, int val)
        {
            lightmap[y,z,x] = (short) ((lightmap[x,y,z] & 0xF0FF) | (val << 8));
        }

        public int GetGreenLight(int x, int y, int z)
        {

            return(lightmap[y,z,x] >> 4) & 0xF;

        }

        public void SetGreenLight(int x, int y, int z, int val)
        {
            lightmap[y,z,x] = (short) ((lightmap[x,y,z] & 0xFF0F) | (val << 4));
        }

        public int GetBlueLight(int x, int y, int z)
        {

            return lightmap[y,z,x] & 0xF;

        }

        public void SetBlueLight(int x, int y, int z, int val)
        {

            lightmap[y,z,x] = (short) ((lightmap[x,y,z] & 0xFFF0) | (val));
        }

        // Get the bits 0000XXXX
        public int GetBlockLight(int x, int y, int z)
        {
            return lightmap[x,y,z] & 0xF;
        }

        private void PropagateEmissiveBlock(int x, int y, int z, int val)
        {
            int bounds = RegionManager.CHUNK_BOUNDS;
            int height = RegionManager.CHUNK_HEIGHT;
            SetBlockLight(x, y, z, val);

            short index = (short) (y * bounds * bounds + z * bounds + x);
            BFSPropagationQueue.Enqueue(new(index, this));

            while (BFSPropagationQueue.Count > 0)
            {

                // Get a reference to the front node.
                LightNode node = BFSPropagationQueue.Dequeue();

                int idx = node.Index;

                Chunk chunk = node.Chunk;


                // Extract x, y, and z from our chunk.
                // Depending on how you access data in your chunk, this may be optional

                int x1 = index % bounds;

                int y1 = index / (bounds * bounds);

                int z1 = (index % (bounds * bounds)) / bounds;

                // Grab the light level of the current node       

                int lightLevel = chunk.GetBlockLight(x, y, z);

                //Look at all neighbouring voxels to that node.
                //if their light level is 2 or more levels less than
                //the current node, then set their light level to
                //the current nodes light level - 1, and then add
                //them to the queue.

                //  if (chunk->getBlock(x - 1, y, z).opaque == false
                if (chunk.GetBlockLight(x - 1, y, z) + 2 <= lightLevel)
                {

                    // Set its light level

                    chunk.SetBlockLight(x - 1, y, z, lightLevel - 1);

                    // Emplace new node to queue. (could use push as well)
                    BFSPropagationQueue.Enqueue(new(index, chunk));

                }

                // Check other five neighbors
            }
        }

        public void AddBlockToChunk(Vector3 v)
        {
            blocksToAdd.AddRange([v.X, v.Y,v.Z]);
            DidChange(true);
            GetRenderTask();
        }
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