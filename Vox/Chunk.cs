using System.Collections;
using System.Diagnostics;
using System.Drawing;
using Newtonsoft.Json;
using OpenTK.Graphics.ES20;
using OpenTK.Mathematics;
using Vox.Comparator;
using Vox.Model;
using Vox.World;


namespace Vox
{
    public class Chunk
    {
        private Vector3 location;
        private bool rerender = false;
        private readonly List<Block> blocks = [];
        private readonly int[,] heightMap = new int[RegionManager.CHUNK_BOUNDS, RegionManager.CHUNK_BOUNDS];
        private readonly List<Block> chunkBlocks = [];
        private bool isInitialized = false;
        private int vbo = 0;
        private int ebo = 0;
        private static Matrix4 modelMatrix = new();
        private static List<float> chunkVerts = [];
        private static List<int> chunkEle = [];

        static Chunk()
        {

            //Chunk events executed by player
            //determine when a chunk should be re-rendered

            //Option 1: Use polygonmesh view with built-in listeners (might not even work
            //since object is the only reference to javafx in the whole project)
            //Option 2: Don't extend Chunk and implement custom listeners

            /*
            setOnMouseClicked(mouseEvent -> {
                rerender = true;
                mouseEvent.GetPickResult().

                if (Player.Getblock players looking at is in heightmap) {
                  remove from heightmap
                else {
                  add to heightmap
                });

            */
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
            location = new Vector3(x, y, z);

            //Generic model matrix applicable to every chunk object
            modelMatrix = Matrix4.CreateTranslation(location) * Matrix4.CreateScale(1/16);

            //===================================
            //Generate chunk height map
            //===================================

            int xCount = 0;
            int zCount = 0;
            for (int x1 = (int)x; x1 < x + RegionManager.CHUNK_BOUNDS; x1++)
            {
                for (int z1 = (int)z; z1 < z + RegionManager.CHUNK_BOUNDS; z1++)
                {

                    //Converts the raw noise value in the range of -1 to 1, to the range of 0 to 320 to match Z coordinate.
                    int elevation = GetGlobalHeightMapValue(x1, z1);

                    heightMap[xCount, zCount] = elevation;
                    //Block c = new Block(x1, elevation, z1, BlockType.GRASS);
                    //  chunkBlocks.Add(c);
                
                    zCount++;
                    if (zCount > RegionManager.CHUNK_BOUNDS - 1)
                        zCount = 0;
                }
                xCount++;
                if (xCount > RegionManager.CHUNK_BOUNDS - 1)
                    xCount = 0;
            }


            //How far down caves should start generating
            int caveStart = 50;

            //checks chunks for blocks to render based on noise value and heightmap
            for (int x1 = (int)x; x1 < x + RegionManager.CHUNK_BOUNDS; x1++)
            {
                for (int z1 = (int)z; z1 < z + RegionManager.CHUNK_BOUNDS; z1++)
                {
                    for (int y1 = (int)y; y1 <= heightMap[xCount, zCount]; y1++)
                    {

                        Block c = new Block(x1, y1, z1, BlockType.DIRT_BLOCK);
                        //    c.f = OpenSimplex.noise3_ImproveXZ(RegionManager.WORLD_SEED, x1 * 0.05, y1 * 0.05, z1 * 0.05);
                        //   if (c.f > 0.00)
                        blocks.Add(c);
                        //      if (c.f <= 0.00 && y1 >= heightMap[xCount][zCount] - caveStart)
                        //           blocks.Add(c);

                        zCount++;
                        if (zCount > RegionManager.CHUNK_BOUNDS - 1)
                            zCount = 0;
                    }
                    xCount++;
                    if (xCount > RegionManager.CHUNK_BOUNDS - 1)
                        xCount = 0;
                }
            }
           // InterpolateChunk();
            SortBlocks();
            isInitialized = true;
            rerender = true;
            // updateMesh();

            return this;
        }

        /**
         * Regenerates this chunks heightmap if the chunk is marked for
         * re-rendering.

         * This might not even be used
         */
        //  public void updateMesh()
        //      {

        //   if (blocks.Count > 0 && rerender)
        //   {

        //Every vertex contained inside the chunk mesh in no particular order
        //   float[] points = new float[0];

        //Check if interpolations are already present in heightmap
        // List<Block> cList = getInterpolatedBlocks();
        // for (Block c : cList) {
        //     if (c != null) {
        //        if (!chunkBlocks.Contains(c))
        //            chunkBlocks.Add(c);
        //     }
        // }

        //Populating array that holds the surface points of the chunk.
        //    for (Block block : chunkBlocks) {
        //        float[] coordArr = {block.GetLocation().X, block.GetLocation().Y, block.GetLocation().Z};
        //         points = ArrayUtils.AddAll(points, coordArr);
        //      }

        //Updates data caches when the chunk mesh has changed
        //Future<RenderTask> temp;
        //     Main.executor.submit(this::getRenderTask);
        //  try {
        //  vertexCache = temp.Get().GetVertexData();
        //   elementCache = temp.Get().GetElementData();
        //   } catch (Exception e) {
        //      logger.warning(e.GetMessage());
        //  }
        //  for (int[] ints : heightMap) {
        //      System.out.println(Arrays.toString(ints));
        //   }

        //}

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

            float f = (1 * OpenSimplex.noise2(RegionManager.WORLD_SEED, (x * var2), (z * var2)) / (var1 + 2)) //Noise Octave 1
                    + (float)(0.5 * OpenSimplex.noise2(RegionManager.WORLD_SEED, (x * (var2 * 2)), (z * (var2 * 2))) / (var1 + 4)) //Noise Octave 2
                    + (float)(0.25 * OpenSimplex.noise2(RegionManager.WORLD_SEED, (x * (var2 * 2)), (z * (var2 * 2))) / (var1 + 6)); //Noise Octave 3

            return (int)Math.Floor(((f + 1) / 2) * (RegionManager.CHUNK_HEIGHT - 1));

        }

        public int[,] GetHeightmap()
        {
            return heightMap;
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
            //loop through heightmap
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
            int xLowerLimit = ((x / RegionManager.REGION_BOUNDS) * RegionManager.REGION_BOUNDS);
            int xUpperLimit;
            if (x < 0)
                xUpperLimit = xLowerLimit - RegionManager.REGION_BOUNDS;
            else
                xUpperLimit = xLowerLimit + RegionManager.REGION_BOUNDS;


            int z = (int)GetLocation().Z;
            int zLowerLimit = ((z / RegionManager.REGION_BOUNDS) * RegionManager.REGION_BOUNDS);
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
                returnRegion = new Region(regionXCoord, regionZCoord);

            return returnRegion;
        }

        /**
         * Sorts the chunks blocks to make accessing them more efficient.
         */
        private void SortBlocks()
        {
            chunkBlocks.Sort(new BlockComparator());
        }

        /**
         * Generates or regenerates this Chunks RenderTask. The RenderTask is
         * used to graphically render the Chunk. Calling this method will
         * automatically update this chunks vertex and element data and
         * return a new RenderTask that can be passed to the GPU when drawing.
         *
         * @return A RenderTask whose regularly updated contents can be
         * used in GL draw calls to render this Chunk graphically
         */

        public RenderTask GetRenderTask()
        {
            //   long t1 = System.currentTimeMillis();
            //TODO: in progress
            //idea: have bits/bools at block level for which faces are rendered.
            //use to determine what other faces to render since +y face
            //renders properly
            List<Vector3> nonInterpolated = [];
            List<float> vertices = [];
            List<int> elements = [];
            int elementCounter = 0;

            BlockModel model = ModelLoader.GetModel(BlockType.DIRT_BLOCK);
            Array values = Enum.GetValues(typeof(BlockType));
            Random random = new();

            if (rerender && (chunkVerts.Count == 0 || chunkEle.Count == 0))
            {
                Logger.Debug("Calculating chunk vertex and element data");
                for (int x = 0; x < heightMap.GetLength(0); x++) // GetLength(0) gives the number of rows
                {
                    for (int z = 0; z < heightMap.GetLength(1); z++) // GetLength(1) gives the number of columns
                    {
                        nonInterpolated.Add(new Vector3(location.X + x, heightMap[z,x], location.Z + z));
                    }
                }

                List<Vector3> interpolatedChunk = InterpolateChunk(nonInterpolated);
                for (int i = 0; i < interpolatedChunk.Count; i++)
                {
                    int randomIndex = random.Next(values.Length);
                    model = ModelLoader.GetModel((BlockType)values.GetValue(randomIndex));

                    vertices.AddRange(ModelUtils.GetCuboidFace(model, "south", new Vector3(interpolatedChunk[i].X, interpolatedChunk[i].Y, interpolatedChunk[i].Z)));
                    vertices.AddRange(ModelUtils.GetCuboidFace(model, "north", new Vector3(interpolatedChunk[i].X, interpolatedChunk[i].Y, interpolatedChunk[i].Z)));
                    vertices.AddRange(ModelUtils.GetCuboidFace(model, "up",    new Vector3(interpolatedChunk[i].X, interpolatedChunk[i].Y, interpolatedChunk[i].Z)));
                    vertices.AddRange(ModelUtils.GetCuboidFace(model, "down",  new Vector3(interpolatedChunk[i].X, interpolatedChunk[i].Y, interpolatedChunk[i].Z)));
                    vertices.AddRange(ModelUtils.GetCuboidFace(model, "west",  new Vector3(interpolatedChunk[i].X, interpolatedChunk[i].Y, interpolatedChunk[i].Z)));
                    vertices.AddRange(ModelUtils.GetCuboidFace(model, "east",  new Vector3(interpolatedChunk[i].X, interpolatedChunk[i].Y, interpolatedChunk[i].Z)));

                    elements.AddRange([
                        elementCounter,      elementCounter + 1,  elementCounter + 2,   elementCounter + 3, 10000,
                        elementCounter + 4,  elementCounter + 5,  elementCounter + 6,   elementCounter + 7, 10000,
                        elementCounter + 8,  elementCounter + 9,  elementCounter + 10,  elementCounter + 11, 10000,
                        elementCounter + 12, elementCounter + 13, elementCounter + 14,  elementCounter + 15, 10000,
                        elementCounter + 16, elementCounter + 17, elementCounter + 18,  elementCounter + 19, 10000,
                        elementCounter + 20, elementCounter + 21, elementCounter + 22,  elementCounter + 23, 10000,
                    ]);

                    elementCounter += 24;
                }

                //Updates chunk data
                chunkVerts = vertices;
                chunkEle = elements;
                
                /*
                //Block face render conditions
                for (int i = 0; i < chunkBlocks.Count; i++)
                {
                    Block? block = chunkBlocks[i];

                    Block? c1 = BinarySearchBlockWithLocation(0, chunkBlocks.Count - 1, new Block(block.GetLocation().X + 1, block.GetLocation().Y - 1, block.GetLocation().Z));
                    Block? c2 = BinarySearchBlockWithLocation(0, chunkBlocks.Count - 1, new Block(block.GetLocation().X, block.GetLocation().Y + 1, block.GetLocation().Z));

                    Block? c3 = BinarySearchBlockWithLocation(0, chunkBlocks.Count - 1, new Block(block.GetLocation().X, block.GetLocation().Y, block.GetLocation().Z + 1));
                    Block? c4 = BinarySearchBlockWithLocation(0, chunkBlocks.Count - 1, new Block(block.GetLocation().X - 1, block.GetLocation().Y, block.GetLocation().Z));
                    Block? c5 = BinarySearchBlockWithLocation(0, chunkBlocks.Count - 1, new Block(block.GetLocation().X, block.GetLocation().Y - 1, block.GetLocation().Z));
                    Block? c6 = BinarySearchBlockWithLocation(0, chunkBlocks.Count - 1, new Block(block.GetLocation().X, block.GetLocation().Y, block.GetLocation().Z - 1));

                    //TODO: Add texture layer index in vertex attributes

                    //If c1 is null, positive X face should be rendered
                    if (c1 != null)
                    {
                        float[] origin = { block.GetLocation().X, block.GetLocation().Y, block.GetLocation().Z };
                        List<float> posXFace = [
                            //Position (X, Y, Z)                            Color (R, G, B, A)          Texture (U, V)
                            origin[0] + 1, origin[1], origin[2],            1.0f, 0.0f, 0.0f, 1.0f,     0, 0,
                            origin[0] + 1, origin[1] + 1, origin[2],        1.0f, 0.0f, 0.0f, 1.0f,     1, 0,
                            origin[0] + 1, origin[1] + 1, origin[2] + 1,    1.0f, 0.0f, 0.0f, 1.0f,     1, 1,
                            origin[0] + 1, origin[1], origin[2] + 1,        1.0f, 0.0f, 0.0f, 1.0f,     0, 1,
                        ];
                        List<int> posXElements = [
                            elementCounter, elementCounter + 1, elementCounter + 2, elementCounter + 3, 80000
                        ];
                        elementCounter += 4;

                       vertices.AddRange(posXFace);
                       elements.AddRange(posXElements);
                    }


                    //If c2 is null, positive Y face should be rendered
                    if (c2 == null)
                    {
                        float[] origin = [block.GetLocation().X, block.GetLocation().Y, block.GetLocation().Z];

                        List<float> posYFace = [
                            //Position (X, Y, Z)                            Color (R, G, B, A)          Texture (U, V)
                            origin[0], origin[1] + 1, origin[2],            0.0f, 1.0f, 0.0f, 1.0f,     0, 0,
                            origin[0], origin[1] + 1, origin[2] + 1,        0.0f, 1.0f, 0.0f, 1.0f,     1, 0,
                            origin[0] + 1, origin[1] + 1, origin[2] + 1,    0.0f, 1.0f, 0.0f, 1.0f,     1, 1,
                            origin[0] + 1, origin[1] + 1, origin[2],        0.0f, 1.0f, 0.0f, 1.0f,     0, 1,
                        ];
                        List<int> posYElements = [
                            elementCounter, elementCounter + 1, elementCounter + 2, elementCounter + 3, 80000
                        ];
                        elementCounter += 4;

                        vertices.AddRange(posYFace);
                        elements.AddRange(posYElements);
                    }

                
                }
                */
            }
            return new RenderTask(vertices, elements, vbo, ebo, modelMatrix);
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
                if (comparator.Compare(v, ((Block)chunkBlocks[0]).GetLocation()) < 0)
                {
                    chunkBlocks.Insert(0, b);
                    return b;
                }
                //Appends to end of list
                if (comparator.Compare(v, ((Block)chunkBlocks[0]).GetLocation()) > 0)
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
                if (comparator.Compare(v, ((Block) chunkBlocks[0]).GetLocation()) < 0)
                {
                    chunkBlocks.Insert(0, b);
                    return b;
                }
                //If element is more than last element insert at end of list
                if (comparator.Compare(v, ((Block) chunkBlocks[chunkBlocks.Count - 1]).GetLocation()) > 0)
                {
                    chunkBlocks.Add(b);
                    return b;
                }

                //If the index is near the middle
                if (comparator.Compare(v, ((Block) chunkBlocks[mid - 1]).GetLocation()) > 0
                        && comparator.Compare(b.GetLocation(), ((Block) chunkBlocks[mid]).GetLocation()) < 0)
                {
                    chunkBlocks.Insert(mid, b);
                    return b;
                }
                if (comparator.Compare(v, ((Block) chunkBlocks[mid + 1]).GetLocation()) < 0
                        && comparator.Compare(v, ((Block) chunkBlocks[mid]).GetLocation()) > 0)
                {
                    chunkBlocks.Insert(mid + 1, b);
                    return b;
                }

                // If element is smaller than mid, then
                // it can only be present in left subarray
                if (comparator.Compare(v, ((Block) chunkBlocks[mid]).GetLocation()) < 0)
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

        /**
         * Searches for a Block in O(log n) time complexity and returns it.
         *
         * @param l The farthest left index of the list
         * @param r The farthest right index of the list
         * @param v The coordinate to search for.
         * @return Returns the Block if found. Else null.
         */
        private Block? BinarySearchBlockWithLocation(int l, int r, Block v)
        {
            BlockComparator comparator = new();

            if (r >= l)
            {
                int mid = l + (r - l) / 2;

                // If the element is present at the middle
                if (comparator.Compare(v, (Block?) chunkBlocks[mid]) == 0)
                {
                    // System.out.println("Found Equal: " + v + "   " + chunkBlocks[mid]);
                    return (Block?) chunkBlocks[mid];
                }

                // If element is smaller than mid, then
                // it can only be present in left subarray
                if (comparator.Compare(v, (Block?) chunkBlocks[mid]) < 0)
                {
                    return BinarySearchBlockWithLocation(l, mid - 1, v);
                }

                // Else the element can only be present
                // in right subarray
                if (comparator.Compare(v, (Block?) chunkBlocks[mid]) > 0)
                {
                    return BinarySearchBlockWithLocation(mid + 1, r, v);
                }
            }
            return null;

        }

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
        /**
         * Vertex Buffer Object specific to this chunk used in the
         * Chunks RenderTask and for drawing the chunks data
         * @param i VBO ID. Ideally using glGenBuffers() as the parameter
         */
        public void SetVbo(int i)
        {
            vbo = i;
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
        public override bool Equals(object? obj)
        {
            if (obj?.GetType() == typeof(Chunk))
            {
                if (chunkBlocks.Equals(((Chunk)obj).chunkBlocks) && GetLocation() == ((Chunk)obj).GetLocation())
                    return true;
            }
            else return false;
            return true;
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