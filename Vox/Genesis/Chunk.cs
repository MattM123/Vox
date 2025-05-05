
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
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
        public int [,,] blockData = new int[RegionManager.CHUNK_BOUNDS, RegionManager.CHUNK_BOUNDS, RegionManager.CHUNK_BOUNDS];

        [IgnoreMember]
        public BlockFaceInstance[] SSBOdata;

        [IgnoreMember]
        public bool IsEmpty = true;
 
        [IgnoreMember]
        private static readonly object chunkLock = new();
 
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

                //===================================
                //Generate surface chunk height map
                //===================================

                int xCount = 0;
                int zCount = 0;

                for (int x1 = (int)x; x1 < x + RegionManager.CHUNK_BOUNDS; x1++)
                {
                    for (int z1 = (int)z; z1 < z + RegionManager.CHUNK_BOUNDS; z1++)
                    {
                        int elevation = RegionManager.GetGlobalHeightMapValue(x1, z1);


                        heightMap[zCount, xCount] = elevation;

                        zCount++;
                        if (zCount > RegionManager.CHUNK_BOUNDS - 1)
                            zCount = 0;

                    }
                    xCount++;
                    if (xCount > RegionManager.CHUNK_BOUNDS - 1)
                        xCount = 0;
                }

                for (int x1 = 0; x1 < RegionManager.CHUNK_BOUNDS; x1++)
                {
                    for (int z1 = 0; z1 < RegionManager.CHUNK_BOUNDS; z1++)
                    {
                        for (int y1 = 0; y1 < RegionManager.CHUNK_BOUNDS; y1++)
                        {
                            int elevation = heightMap[z1, x1];

                            //Set block data to AIR

                            if (y + y1 <= elevation)
                                blockData[x1, y1, z1] = (int)RegionManager.GetGlobalBlockType((int)(x + xLoc), (int)(y + yLoc), (int)(z + zLoc));

                            else
                                blockData[x1, y1, z1] = 0;
                        }
                    }
                }
                IsInitialized = true;
            
            }
            return this;
        }

        public TerrainRenderTask GenerateRenderData()
        {
            List<Vector3> nonInterpolated = [];
            List<TerrainVertex> vertices = [];
            List<int> elements = [];
            int elementCounter = 0;

            if (didChange || Window.GetNextFaceIndex() == 0)
            {

                for (int x = 0; x < RegionManager.CHUNK_BOUNDS; x++)
                {
                    for (int z = 0; z < RegionManager.CHUNK_BOUNDS; z++)
                    {
                        for (int y = 0; y < RegionManager.CHUNK_BOUNDS; y++)
                        {
                            if (blockData[x, y, z] != 0)
                            {

                                BlockType type = (BlockType)blockData[x, y, z];

                                //Positive X (EAST)
                                if (x + 1 < RegionManager.CHUNK_BOUNDS)
                                {
                                    if (blockData[x + 1, y, z] == 0)
                                    {
                                        //Texture enum value corresponds to texture array layer 
                                        BlockFaceInstance face = new(new(x + xLoc, y + yLoc, z + zLoc), Face.EAST,
                                            (int)ModelLoader.GetModel(type).GetTexture(Face.EAST));

                                        //Write face directly to SSBO
                                        unsafe
                                        {
                                            int offset = Window.GetNextFaceIndex() * Marshal.SizeOf<BlockFaceInstance>();
                                            byte* basePtr = (byte*)Window.GetSSBOPtr().ToPointer();
                                            BlockFaceInstance* instancePtr = (BlockFaceInstance*)(basePtr + offset);
                                            *instancePtr = face;
                                            Window.GetAndIncrementNextFaceIndex();
                                        }
                                    }
                                }

                                //Negative X (WEST)
                                if (x - 1 >= 0)
                                {
                                    if (blockData[x - 1, y, z] == 0)
                                    {
                                        //Texture enum value corresponds to texture array layer 
                                        BlockFaceInstance face = new(new(x + xLoc, y + yLoc, z + zLoc), Face.WEST,
                                            (int)ModelLoader.GetModel(type).GetTexture(Face.WEST));
                                
                                        //Write face directly to SSBO
                                        unsafe
                                        {
                                            int offset = Window.GetNextFaceIndex() * Marshal.SizeOf<BlockFaceInstance>();
                                            byte* basePtr = (byte*)Window.GetSSBOPtr().ToPointer();
                                            BlockFaceInstance* instancePtr = (BlockFaceInstance*)(basePtr + offset);
                                            *instancePtr = face;
                                            Window.GetAndIncrementNextFaceIndex();
                                        }
                                    }
                                }

                                //Positive Y (UP)
                                if (y + 1 < RegionManager.CHUNK_BOUNDS)
                                {
                                    if (blockData[x, y + 1, z] == 0)
                                    {
                                        //Texture enum value corresponds to texture array layer 
                                        BlockFaceInstance face = new(new(x + xLoc, y + yLoc, z + zLoc), Face.UP,
                                            (int)ModelLoader.GetModel(type).GetTexture(Face.UP));

                                        //Write face directly to SSBO
                                        unsafe
                                        {
                                            int offset = Window.GetNextFaceIndex() * Marshal.SizeOf<BlockFaceInstance>();
                                            byte* basePtr = (byte*)Window.GetSSBOPtr().ToPointer();
                                            BlockFaceInstance* instancePtr = (BlockFaceInstance*)(basePtr + offset);
                                            *instancePtr = face;
                                            Window.GetAndIncrementNextFaceIndex();
                                        }
                                    }
                                }

                                //Negative Y (DOWN)
                                if (y - 1 >= 0)
                                {
                                    if (blockData[x, y - 1, z] == 0)
                                    {
                                        //Texture enum value corresponds to texture array layer 
                                        BlockFaceInstance face = new(new(x + xLoc, y + yLoc, z + zLoc), Face.DOWN,
                                            (int)ModelLoader.GetModel(type).GetTexture(Face.DOWN));
                            
                                        //Write face directly to SSBO
                                        unsafe
                                        {
                                            int offset = Window.GetNextFaceIndex() * Marshal.SizeOf<BlockFaceInstance>();
                                            byte* basePtr = (byte*)Window.GetSSBOPtr().ToPointer();
                                            BlockFaceInstance* instancePtr = (BlockFaceInstance*)(basePtr + offset);
                                            *instancePtr = face;
                                            Window.GetAndIncrementNextFaceIndex();
                                        }
                                    }
                                }



                                //Positive Z (NORTH)
                                if (z + 1 < RegionManager.CHUNK_BOUNDS)
                                {
                                    if (blockData[x, y, z + 1] == 0)
                                    {
                                        //Texture enum value corresponds to texture array layer 
                                        BlockFaceInstance face = new(new(x + xLoc, y + yLoc, z + zLoc), Face.NORTH,
                                            (int)ModelLoader.GetModel(type).GetTexture(Face.NORTH));
                            
                                        //Write face directly to SSBO
                                        unsafe
                                        {
                                            int offset = Window.GetNextFaceIndex() * Marshal.SizeOf<BlockFaceInstance>();
                                            byte* basePtr = (byte*)Window.GetSSBOPtr().ToPointer();
                                            BlockFaceInstance* instancePtr = (BlockFaceInstance*)(basePtr + offset);
                                            *instancePtr = face;
                                            Window.GetAndIncrementNextFaceIndex();
                                        }
                                    }
                                }

                                //Negative Z (SOUTH)
                                if (z - 1 >= 0)
                                {
                                    if (blockData[x, y, z - 1] == 0)
                                    {
                                        //Texture enum value corresponds to texture array layer 
                                        BlockFaceInstance face = new(new(x + xLoc, y + yLoc, z + zLoc), Face.SOUTH,
                                            (int)ModelLoader.GetModel(type).GetTexture(Face.SOUTH));
                            
                                        //Write face directly to SSBO
                                        unsafe
                                        {
                                            int offset = Window.GetNextFaceIndex() * Marshal.SizeOf<BlockFaceInstance>();
                                            byte* basePtr = (byte*)Window.GetSSBOPtr().ToPointer();
                                            BlockFaceInstance* instancePtr = (BlockFaceInstance*)(basePtr + offset);
                                            *instancePtr = face;
                                            Window.GetAndIncrementNextFaceIndex();
                                        }
                                    }
                                }


                            }
                        }
                    }
                }

                            //for (int x = 0; x < RegionManager.CHUNK_BOUNDS; x++)
                            //{
                            //    for (int z = 0; z < RegionManager.CHUNK_BOUNDS; z++)
                            //    {
                            //
                            //        foreach (Vector3 v in interpolatedChunk)
                            //        {
                            //   
                            //            
                            //        }
                            //    }
                            //}

                            //Updates chunk data
                            lock (chunkLock)
                {
                    //O(n) because of ToArray
                 //   renderTask = new TerrainRenderTask([.. vertices], [.. elements], GetVbo("Terrain"), GetEbo("Terrain"), GetVao("Terrain"));
                    didChange = false;
                }
            }
            return renderTask;
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
        public List<Vector3> InterpolateChunk(List<Vector3> inVert)
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
                        comparison1 = RegionManager.GetGlobalHeightMapValue((int)(col + GetLocation().X - 1), (int)(row + GetLocation().Z));

                    //+1 horizontal comparison
                    if (col < RegionManager.CHUNK_BOUNDS - 1)
                        comparison2 = heightMap[row, col + 1];
                    else
                        comparison2 = RegionManager.GetGlobalHeightMapValue((int)(col + GetLocation().X + 1), (int)(row + GetLocation().Z));

                    //-1 2d vertical comparison
                    if (row > 0)
                        comparison3 = heightMap[row - 1, col];
                    else
                        comparison3 = RegionManager.GetGlobalHeightMapValue((int)(col + GetLocation().X), (int)(row + GetLocation().Z - 1));

                    //+1 2d vertical comparison
                    if (row < RegionManager.CHUNK_BOUNDS - 1)
                        comparison4 = heightMap[row + 1, col];
                    else
                        comparison4 = RegionManager.GetGlobalHeightMapValue((int)(col + GetLocation().X), (int)(row + GetLocation().Z + 1));

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
         * Since the location of each chunk is unique this is used as a
         * key to retrieve from spatial hashing storage.
         * @return The corner vertex of this chunk.
         */
        public Vector3 GetLocation()
        {
            return location;
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
                    return (BlockType) v.blocktype;
            }
            return BlockType.TEST_BLOCK;
        }
        public TerrainVertex[] GetVertices()
        {
            return renderTask.GetVertexData();
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

        public static Matrix4 GetModelMatrix()
        {
            return modelMatrix;
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
               // Console.WriteLine(BFSSunlightPropagationQueue.Count);
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
                    Console.WriteLine("test1");
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
                    Console.WriteLine("test2");
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
                    Console.WriteLine("test3");
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
                    Console.WriteLine("test4");
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
                    Console.WriteLine("test5");
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
                    Console.WriteLine("test6");
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
           // GetTerrainRenderTask();
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

                if ((adjBlock.Y < RegionManager.GetGlobalHeightMapValue((int)adjBlock.X, (int)adjBlock.Z) 
                    || adjBlock.Y < RegionManager.GetGlobalHeightMapValue((int)v.X, (int)v.Z) 
                    || v.Y < RegionManager.GetGlobalHeightMapValue((int)v.X, (int)v.Z)
                    || v.Y < RegionManager.GetGlobalHeightMapValue((int)adjBlock.X, (int)adjBlock.Z))
                    && !containsRange)

                {
                    Console.WriteLine("Filling in terrain hole at " + adjBlock);
                    AddBlockToChunk(adjBlock);
                }
             
            }
            
            DidChange(true);
            //GetTerrainRenderTask();

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