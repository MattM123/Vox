
using MessagePack;
using OpenTK.Mathematics;
using Vox.Model;
using Vox.Rendering;
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
        public float yLoc;
 
        [Key(3)]
        public readonly int[,] heightMap = new int[RegionManager.CHUNK_BOUNDS, RegionManager.CHUNK_BOUNDS];
 
        [Key(4)]
        public bool IsInitialized = false;
 
        [Key(5)]
        public bool didChange = false;
 
        [Key(6)]
        public TerrainRenderTask renderTask;
 
        [Key(7)]
        public short[,,] lightmap = new short[RegionManager.CHUNK_BOUNDS, RegionManager.CHUNK_BOUNDS, RegionManager.CHUNK_BOUNDS];
 
        [Key(8)]
        public Queue<LightNode> BFSEmissivePropagationQueue = new((int)Math.Pow(RegionManager.CHUNK_BOUNDS, 3));
 
        [Key(9)]
        public Queue<LightNode> BFSSunlightPropagationQueue = new((int) Math.Pow(RegionManager.CHUNK_BOUNDS, 3));
 
        [Key(10)]
        public List<float> blocksToAdd = [];
 
        [Key(11)]
        public List<float> blocksToExclude = [];

        [Key(12)]
        public LightingRenderTask lightingRenderTask;

        [IgnoreMember]
        public bool IsEmpty = true;
 
        [IgnoreMember]
        private readonly object chunkLock = new();
 
        [IgnoreMember]
        private Vector3 location;

        [IgnoreMember]
        private readonly Dictionary<string, int> VBOs = [];

        [IgnoreMember]
        private readonly Dictionary<string, int> EBOs = [];

        [IgnoreMember]
        private readonly Dictionary<string, int> VAOs = [];


        [IgnoreMember]
        private static Matrix4 modelMatrix = new();
      
        public Chunk()
        {

            SetEbo(GL.GenBuffer(),      "Lighting");
            SetVbo(GL.GenBuffer(),      "Lighting");
            SetVao(GL.GenVertexArray(), "Lighting");

            SetEbo(GL.GenBuffer(),      "Terrain");
            SetVbo(GL.GenBuffer(),      "Terrain");
            SetVao(GL.GenVertexArray(), "Terrain");
        }

        /**
         * Initializes a chunk at a given point and populates it's heightmap using Simplex noise
         * @param x coordinate of top left chunk corner
         * @param y coordinate of top left chunk corner
         * @param z coordinate of top left chunk corner
         * @return Returns the chunk
         */
        public Chunk Initialize(float x, float y, float z)
        {
            xLoc = x;
            zLoc = z;
            yLoc = y;
            location = new Vector3(xLoc, yLoc, zLoc);

            //Generic model matrix applicable to every chunk object
            modelMatrix = Matrix4.CreateTranslation(0, 0, 0);

            if (!IsInitialized)
            {
                lightmap = new short[RegionManager.CHUNK_BOUNDS, RegionManager.CHUNK_BOUNDS, RegionManager.CHUNK_BOUNDS];

                //===================================
                //Generate surface chunk height map
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
                IsInitialized = true;
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
         * Generates or regenerates this Chunks RenderTask. The RenderTask is
         * used to graphically render the Chunk. Calling this method will, if
         * needed, automatically update this chunks vertex and element data and
         * return a new RenderTask that can be passed to the GPU when drawing.
         *
         * @return A RenderTask whose regularly updated contents can be
         * used in GL draw calls to render this Chunk graphically
         */

        public TerrainRenderTask GetTerrainRenderTask()
        {
            List<Vector3> nonInterpolated = [];
            List<TerrainVertex> vertices = [];
            List<int> elements = [];
            int elementCounter = 0;

            Array values = Enum.GetValues(typeof(BlockType));

            Random random = new();

            if (didChange || renderTask == null)
            {
                for (int x = 0; x < heightMap.GetLength(0); x++) //rows          
                    for (int z = 0; z < heightMap.GetLength(1); z++) //columns
                        nonInterpolated.Add(new Vector3(location.X + x, heightMap[z, x], location.Z + z));



                //Add any player placed blocks to the mesh before interpolating
                for (int i = 0; i < blocksToAdd.Count; i += 3)
                    nonInterpolated.Add(new(blocksToAdd[i], blocksToAdd[i + 1], blocksToAdd[i + 2]));

                //Interpolate chunk heightmap
                List<Vector3> interpolatedChunk = InterpolateChunk(nonInterpolated);

                //Remove any blocks from the mesh marked for exclusion.
                //(i.e player broke a block)
                for (int i = 0; i < blocksToExclude.Count; i += 3)
                    interpolatedChunk.Remove(new(blocksToExclude[i], blocksToExclude[i + 1], blocksToExclude[i + 2]));


                int randomIndex = random.Next(values.Length);
                BlockType? randomBlock;
                if (randomIndex > 1)
                    randomBlock = (BlockType?)values.GetValue(randomIndex - 2);
                else
                    randomBlock = (BlockType?)values.GetValue(randomIndex);

                BlockType blockType = (BlockType) randomBlock;
                //if (randomBlock != null)
                //    blockType = ModelLoader.GetModel((BlockType)randomBlock);
                //else
                //    blockType = ModelLoader.GetModel(BlockType.TEST_BLOCK);


                for (int x = 0; x < RegionManager.CHUNK_BOUNDS; x++)
                {
                    for (int z = 0; z < RegionManager.CHUNK_BOUNDS; z++)
                    {

                        foreach (Vector3 v in interpolatedChunk)
                        {

                            //Top face check X and Z
                            if (v.X == (int)xLoc + x && v.Z == (int)zLoc + z)
                            {
                                bool up = interpolatedChunk.Contains(new(v.X, v.Y + 1, v.Z));
                                if (!up)
                                {
                                    vertices.AddRange(ModelUtils.GetCuboidFace(blockType, Face.UP, new Vector3(x + xLoc, v.Y, z + zLoc), this));
                                    elements.AddRange([elementCounter, elementCounter + 1, elementCounter + 2, elementCounter + 3, 80000]);
                                    elementCounter += 4;
                                }

                                //East face check z + 1
                                bool east = interpolatedChunk.Contains(new(v.X, v.Y, v.Z + 1));
                                if (!east)
                                {
                                    vertices.AddRange(ModelUtils.GetCuboidFace(blockType, Face.EAST, new Vector3(x + xLoc, v.Y, z + zLoc), this));
                                    elements.AddRange([elementCounter, elementCounter + 1, elementCounter + 2, elementCounter + 3, 80000]);
                                    elementCounter += 4;
                                }

                                //West face check z - 1
                                bool west = interpolatedChunk.Contains(new(v.X, v.Y, v.Z - 1));
                                if (!west)
                                {
                                    vertices.AddRange(ModelUtils.GetCuboidFace(blockType, Face.WEST, new Vector3(x + xLoc, v.Y, z + zLoc), this));
                                    elements.AddRange([elementCounter, elementCounter + 1, elementCounter + 2, elementCounter + 3, 80000]);
                                    elementCounter += 4;
                                }

                                //North face check x + 1
                                bool north = interpolatedChunk.Contains(new(v.X + 1, v.Y, v.Z));
                                if (!north)
                                {
                                    vertices.AddRange(ModelUtils.GetCuboidFace(blockType, Face.NORTH, new Vector3(x + xLoc, v.Y, z + zLoc), this));
                                    elements.AddRange([elementCounter, elementCounter + 1, elementCounter + 2, elementCounter + 3, 80000]);
                                    elementCounter += 4;
                                }

                                //South face check x - 1
                                bool south = interpolatedChunk.Contains(new(v.X - 1, v.Y, v.Z));
                                {
                                    vertices.AddRange(ModelUtils.GetCuboidFace(blockType, Face.SOUTH, new Vector3(x + xLoc, v.Y, z + zLoc), this));
                                    elements.AddRange([elementCounter, elementCounter + 1, elementCounter + 2, elementCounter + 3, 80000]);
                                    elementCounter += 4;
                                }
                                //Bottom face
                                bool bottom = interpolatedChunk.Contains(new(v.X, v.Y - 1, v.Z));
                                if (Window.GetPlayer().GetPosition().Y < v.Y && !bottom)
                                {
                                    vertices.AddRange(ModelUtils.GetCuboidFace(blockType, Face.DOWN, new Vector3(x + xLoc, v.Y, z + zLoc), this));
                                    elements.AddRange([elementCounter, elementCounter + 1, elementCounter + 2, elementCounter + 3, 80000]);
                                    elementCounter += 4;
                                }
                            }
                        }
                    }
                    
                }

                //Updates chunk data
                lock (chunkLock)
                {
                    renderTask = new TerrainRenderTask(vertices, elements, GetVbo("Terrain"), GetEbo("Terrain"), GetVao("Terrain"));
                    didChange = false;
                }

                //Populate sunlight propagation queue
               // PopulateSunlightBFSQueue();

                //Empty queue and propagate sunlight
               // PropagateSunlight();
            }
            return renderTask;
        }

        public LightingRenderTask GetLightingRenderTask()
        {

            if (lightingRenderTask != null)
                return lightingRenderTask;

            List<LightingVertex> vertices = [];

            PopulateSunlightBFSQueue();
            for (int y = 0; y < lightmap.GetLength(0); y++)
            {
                for (int z = 0; z < lightmap.GetLength(1); z++)
                {
                    for (int x = 0; x < lightmap.GetLength(2); x++)
                    {
                        vertices.Add(new(x, y, z, lightmap[y, z, x]));
                    }
                }
            }

            //PropagateSunlight();

            foreach(var v in lightmap)
            {
              //  Console.WriteLine(v);
            }

            lightingRenderTask = new LightingRenderTask(vertices, GetVbo("Lighting"), GetEbo("Lighting"), GetVao("Lighting"));
            return lightingRenderTask;

        }
        /**
         * Since the location of each chunk is unique this is used as a
         * key to retrieve from spatial hashing storage.
         * @return The corner vertex of this chunk.
         */
        public Vector3 GetLocation()
        {
            return location;
        }

        public bool ContainsBlockAt(Vector3 block)
        {
            if (GetVertices().Where(v =>
                v.GetVector().X == block.X
                && v.GetVector().Y == block.Y
                && v.GetVector().Z == block.Z).ToList().Count > 0)
            { 
                return true;
            }

            return false;
        }

        //This returns the first occurance of the vertex with the coordinate vector,
        //which means there are 3 other vetrex objects with that vector forming a blocks face.
        
        //The other 3 faces will naturally share the same blocktype with this face but have
        //other values like light and textures that differ from face to face.
        public BlockType GetBlockTypeAt(int x, int y, int z)
        {

            foreach (TerrainVertex v in GetVertices())
            {
                if (v.GetVector().X == x && v.GetVector().Y == y && v.GetVector().Z == z)
                    return (BlockType) v.blockType;
            }
            return BlockType.TEST_BLOCK;
        }
        public List<TerrainVertex> GetVertices()
        {
            return [.. renderTask.GetVertexData()];
        }
        public int[] GetElements()
        {
            return renderTask.GetElementData();
        }

        /**
         * This flag should only be updated to true when the chunk needs to re-render.
         * Ex. If a block was destroyer or placed or otherwise modified
         * */
        public void DidChange(bool f)
        {
            didChange = f;
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
        public void SetEbo(int i, string key)
        {
            EBOs[key] = i;
        }
        public int GetEbo(string key)
        {
            return EBOs[key];
        }
        public int GetVao(string key)
        {
            return VAOs[key];
        }
        /**
         * Vertex Buffer Object specific to this chunk used in the
         * Chunks RenderTask and for drawing the chunks data
         * @param i VBO ID. Ideally using glGenBuffers() as the parameter
         */
        public void SetVbo(int i, string key)
        {
            VBOs[key] = i;
        }
        public void SetVao(int i, string Key)
        {
            VAOs[Key] = i;
        }
        public int GetVbo(string key)
        {
            return VBOs[key];
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
            lightmap[y,z,x] = (byte) ((lightmap[y,z,x] & 0xF) | (val << 4));

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
        public int GetTorchLight(int x, int y, int z)
        {
            return lightmap[x,y,z] & 0xF;
        }

        public void PopulateSunlightBFSQueue()
        {

            int bounds = RegionManager.CHUNK_BOUNDS;
            Vector3 chunkAbove = new(xLoc, yLoc + RegionManager.CHUNK_BOUNDS, zLoc);

            //check if the chunk above this is loaded
            bool isLoaded = Region.IsChunkLoaded(chunkAbove);

            //If the chunk above is loaded, add light nodes to its propagation queue
            if (isLoaded)
            {
                Chunk top = RegionManager.GetAndLoadGlobalChunkFromCoords((int)xLoc, (int)yLoc + bounds, (int)zLoc);

                //If chunk above is empty, there will be no sunlight to propagate on its surface
                for (int y = 0; y < top.lightmap.GetLength(0); y++)
                { 
                    for (int z = 0; z < top.lightmap.GetLength(1); z++)
                    {
                        for (int x = 0; x < top.lightmap.GetLength(2); x++)
                        {

                            //If TOP is loaded, check the sunlightMap for TOP. For all nonzero sunlight values, 
                            //add a node to the sunlightBfsQueue.
                            int sunlightLevel = top.GetSunlight(x, y, z);
                            if (sunlightLevel > 0)
                                top.BFSSunlightPropagationQueue.Enqueue(
                                    new LightNode()
                                    {
                                        Index = (short)(y * bounds * bounds + z * bounds + x),
                                        Chunk = top
                                    }
                                );
                        }
                        
                    }
                }
            }
            else
            {
                //TOP is not loaded

                //Chunk is underground, will have no sunlight propagation
                if (yLoc > GetGlobalHeightMapValue((int)xLoc, (int)zLoc))
                {
                    Console.WriteLine("underground: " + this);
                    return;
                }

                //If chunk is unloaded and above ground, this chunk will have sunlight
                for (int y = 0; y < lightmap.GetLength(0); y++)
                {
                    for (int z = 0; z < lightmap.GetLength(1); z++)
                    {
                        for (int x = 0; x < lightmap.GetLength(2); x++)
                        {

                            //If a voxel is transparent to light, such as glass or air, then we set the sunlight value to
                            //the maximum and add a node to sunlightBfsQueue.

                            short idx = (short)(y * bounds * bounds + z * bounds + x);
                            // Console.WriteLine("Q");
                            if (renderTask != null)
                            {
                                 //Y will be between 0 and 31 but the heightmap will be between 0 and CHUNK_HEIGHT
                                 //Use linear mapping to proportionally clamp a 0-31 number to 0-CHUNK_HEIGHT              
                                 int clampedY = (int)(1 + (y / (float)bounds) * (RegionManager.CHUNK_HEIGHT - 1));
                                 if (clampedY >= heightMap[z, x])
                                 {
                                    //Set all air blocks in the chunk to 15
                                    SetSunlight(x, y, z, 15);
                                    BFSSunlightPropagationQueue.Enqueue(new LightNode { Index = idx, Chunk = this });
                                }
                            }
                        }
                    }   
                }
            }
        }
        private void PropagateTorchLight(int x, int y, int z, int val)
        {
            int bounds = RegionManager.CHUNK_BOUNDS;
            SetBlockLight(x, y, z, val);

            short index = (short) (y * bounds * bounds + z * bounds + x);
            BFSEmissivePropagationQueue.Enqueue(new(index, this));

            while (BFSEmissivePropagationQueue.Count > 0)
            {

                // Get a reference to the front node.
                LightNode node = BFSEmissivePropagationQueue.Dequeue();

                int idx = node.Index;

                Chunk chunk = node.Chunk;


                // Extract x, y, and z from our chunk.
                // Depending on how you access data in your chunk, this may be optional

                int x1 = index % bounds;

                int y1 = index / (bounds * bounds);

                int z1 = (index % (bounds * bounds)) / bounds;

                // Grab the light level of the current node       

                int lightLevel = chunk.GetTorchLight(x, y, z);

                //Look at all neighbouring voxels to that node.
                //if their light level is 2 or more levels less than
                //the current node, then set their light level to
                //the current nodes light level - 1, and then add
                //them to the queue.

                //  if (chunk->getBlock(x - 1, y, z).opaque == false
                if (chunk.GetTorchLight(x - 1, y, z) + 2 <= lightLevel)
                {

                    // Set its light level
                    chunk.SetBlockLight(x - 1, y, z, lightLevel - 1);

                    // Emplace new node to queue. (could use push as well)
                    BFSEmissivePropagationQueue.Enqueue(new(index, chunk));

                }

                // Check other five neighbors
            }
        }

        public void PropagateSunlight()
        {
            int bounds = RegionManager.CHUNK_BOUNDS;
            while (BFSSunlightPropagationQueue.Count > 0)
            {
                Console.WriteLine(BFSSunlightPropagationQueue.Count);
                // Get a reference to the front node and pop it off the queue. We no longer need the node reference
                LightNode front = BFSSunlightPropagationQueue.Dequeue();

                int index = front.Index;
                Chunk chunk = front.Chunk;

                // Extract x, y, and z from our chunk.
                int x = index % bounds;

                int y = index / (bounds * bounds);

                int z = (index % (bounds * bounds)) / bounds;

                // Grab the light level of the current node       
                int lightLevel = chunk.GetSunlight(x, y, z);


                //Look at all neighbouring voxels to that node.
                //if their light level is 2 or more levels less than
                //the current node, then set their light level to
                //the current nodes light level - 1, and then add
                //them to the queue.

                // NOTE: You will need to do bounds checking!
                // If you are on the edge of a chunk, then x - 1 will be -1. Instead
                // you need to look at your left neighboring chunk and check the
                // adjacent block there. When you do that, be sure to use the
                // neighbor chunk when emplacing the new node to lightBfsQueue;

                // Check negative X neighbor
                // Make sure you don't propagate light into opaque blocks like stone!

                if (ModelLoader.GetModel(chunk.GetBlockTypeAt(x - 1, y, z)).IsTransparent()
                    && chunk.GetSunlight(x - 1, y, z) + 2 <= lightLevel)
                {
                    // Set its light level
                    chunk.SetSunlight(x - 1, y, z, lightLevel - 1);

                    // Construct index
                    short idx = (short) (y * bounds * bounds + z * bounds + (x - 1));

                    // Emplace new node to queue. (could use push as well)
                    BFSSunlightPropagationQueue.Enqueue(new LightNode { Index = idx, Chunk = chunk });

                }
                if (ModelLoader.GetModel(chunk.GetBlockTypeAt(x, y, z - 1)).IsTransparent()
                    && chunk.GetSunlight(x, y, z - 1) + 2 <= lightLevel)
                {
                    // Set its light level
                    chunk.SetSunlight(x, y, z - 1, lightLevel - 1);

                    // Construct index
                    short idx = (short)(y * bounds * bounds + (z - 1) * bounds + x);

                    // Emplace new node to queue. (could use push as well)
                    BFSSunlightPropagationQueue.Enqueue(new LightNode { Index = idx, Chunk = chunk });

                }
                if (ModelLoader.GetModel(chunk.GetBlockTypeAt(x, y - 1, z)).IsTransparent()
                    && chunk.GetSunlight(x, y - 1, z) + 2 <= lightLevel)
                {
                    // Set its light level
                    chunk.SetSunlight(x, y - 1, z, lightLevel - 1);

                    // Construct index
                    short idx = (short)((y - 1) * bounds * bounds + z * bounds + x);

                    // Emplace new node to queue. (could use push as well)
                    BFSSunlightPropagationQueue.Enqueue(new LightNode { Index = idx, Chunk = chunk });

                }
                if (ModelLoader.GetModel(chunk.GetBlockTypeAt(x + 1, y, z)).IsTransparent()
                    && chunk.GetSunlight(x + 1, y, z) + 2 <= lightLevel)
                {
                    // Set its light level
                    chunk.SetSunlight(x + 1, y, z, lightLevel - 1);

                    // Construct index
                    short idx = (short)(y * bounds * bounds + z * bounds + (x + 1));

                    // Emplace new node to queue. (could use push as well)
                    BFSSunlightPropagationQueue.Enqueue(new LightNode { Index = idx, Chunk = chunk });

                }
                if (ModelLoader.GetModel(chunk.GetBlockTypeAt(x, y, z + 1)).IsTransparent()
                    && chunk.GetSunlight(x, y, z + 1) + 2 <= lightLevel)
                {
                    // Set its light level
                    chunk.SetSunlight(x, y, z + 1, lightLevel - 1);

                    // Construct index
                    short idx = (short)(y * bounds * bounds + (z + 1) * bounds + x);

                    // Emplace new node to queue. (could use push as well)
                    BFSSunlightPropagationQueue.Enqueue(new LightNode { Index = idx, Chunk = chunk });

                }
                if (ModelLoader.GetModel(chunk.GetBlockTypeAt(x, y + 1, z)).IsTransparent()
                    && chunk.GetSunlight(x, y + 1, z) + 2 <= lightLevel)
                {
                    // Set its light level
                    chunk.SetSunlight(x, y + 1, z, lightLevel - 1);

                    // Construct index
                    short idx = (short)((y + 1) * bounds * bounds + z * bounds + x);

                    // Emplace new node to queue. (could use push as well)
                    BFSSunlightPropagationQueue.Enqueue(new LightNode { Index = idx, Chunk = chunk });

                }
            }  //End while loop
        }
        public void AddBlockToChunk(Vector3 v)
        {
            for (int i = 0; i < blocksToExclude.Count; i += 3)
            {
                if (blocksToExclude[i] == v.X && blocksToExclude[i + 1] == v.Y && blocksToExclude[i + 2] == v.Z)
                    blocksToExclude.RemoveRange(i, 3);
            }
            

            blocksToAdd.AddRange([v.X, v.Y, v.Z]);
            DidChange(true);
            GetTerrainRenderTask();
        }
 
        public void RemoveBlockFromChunk(Vector3 v)
        {
            for (int i = 0; i < blocksToAdd.Count; i += 3)
            {
                if (blocksToAdd[i] == v.X && blocksToAdd[i + 1] == v.Y && blocksToAdd[i + 2] == v.Z)
                    blocksToAdd.RemoveRange(i, 3);
            }
            blocksToExclude.AddRange([v.X,v.Y, v.Z]);

            //Check for terrain holes after removing a block
            BlockDetail detail = new(
                ModelUtils.GetCuboidFace(BlockType.TEST_BLOCK, Face.NORTH, v, this),
                ModelUtils.GetCuboidFace(BlockType.TEST_BLOCK, Face.SOUTH, v, this),
                ModelUtils.GetCuboidFace(BlockType.TEST_BLOCK, Face.UP,    v, this),
                ModelUtils.GetCuboidFace(BlockType.TEST_BLOCK, Face.DOWN,  v, this),
                ModelUtils.GetCuboidFace(BlockType.TEST_BLOCK, Face.EAST,  v, this),
                ModelUtils.GetCuboidFace(BlockType.TEST_BLOCK, Face.WEST,  v, this)
            );
        
            //Fill in holes resulting from a removed block
            //Only works when underground
            foreach (Vector3 adjBlock in detail.GetFaceAdjacentBlocks())
            {
                bool containsRange = false;
                for (int i = 0; i < blocksToExclude.Count; i += 3)
                {
                    if (blocksToExclude[i] == adjBlock.X && blocksToExclude[i + 1] == adjBlock.Y && blocksToExclude[i + 2] == adjBlock.Z)
                        containsRange = true;
                }

                if ((adjBlock.Y < GetGlobalHeightMapValue((int)adjBlock.X, (int)adjBlock.Z) 
                    || adjBlock.Y < GetGlobalHeightMapValue((int)v.X, (int)v.Z) 
                    || v.Y < GetGlobalHeightMapValue((int)v.X, (int)v.Z)
                    || v.Y < GetGlobalHeightMapValue((int)adjBlock.X, (int)adjBlock.Z))
                    && !containsRange)

                {
                    Console.WriteLine("Filling in terrain hole at " + adjBlock);
                    AddBlockToChunk(adjBlock);
                }
             
            }
            
            DidChange(true);
            GetTerrainRenderTask();

        }
        public override string ToString()
        {
            return $"Chunk[{xLoc},{yLoc},{zLoc}]";
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}