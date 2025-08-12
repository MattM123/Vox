
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
        public short[,,] lightmap = new short[RegionManager.CHUNK_BOUNDS, RegionManager.CHUNK_BOUNDS, RegionManager.CHUNK_BOUNDS];
 
        [Key(7)]
        public Queue<LightNode> BFSEmissivePropagationQueue = new((int)Math.Pow(RegionManager.CHUNK_BOUNDS, 3));
 
        [Key(8)]
        public Queue<LightNode> BFSSunlightPropagationQueue = new((int) Math.Pow(RegionManager.CHUNK_BOUNDS, 3));
 
        [Key(9)]
        public List<float> blocksToAdd = [];
 
        [Key(10)]
        public List<float> blocksToExclude = [];

        [Key(11)]
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

        [IgnoreMember] 
        bool isGenerated = false;

        [IgnoreMember]
       public int blockFacesInChunk = 0;

        public Chunk() { }

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
                    for (int y1 = 0; y1 < RegionManager.CHUNK_BOUNDS; y1++)
                    {
                        for (int z1 = 0; z1 < RegionManager.CHUNK_BOUNDS; z1++)
                        {
                            int elevation = heightMap[z1, x1];

                            //Me thinks the problem is here
                            //Set block data to AIR if its not visible
                            if (y + y1 <= elevation && elevation >= GetLocation().Y)
                                blockData[x1, y1, z1] = (int)RegionManager.GetGlobalBlockType((int)(x1 + xLoc), (int)(y1 + yLoc), (int)(z1 + zLoc));
                            else
                                blockData[x1, y1, z1] = 0;
                        }
                    }
                }
                IsInitialized = true;
            
            }


            return this;
        }

        public void GenerateRenderData()
        {
            int bounds = RegionManager.CHUNK_BOUNDS;
                                                            //If the chunk above this is generated, we don't need to generate this chunk   
            if ((!isGenerated) && !RegionManager.GetAndLoadGlobalChunkFromCoords((int)xLoc, (int)(yLoc + bounds), (int)zLoc).isGenerated)
            {
                for (int x = 0; x < bounds; x++)
                {
                    for (int y = 0; y < bounds; y++)
                    {
                        for (int z = 0; z < bounds; z++)
                        {
                            if (blockData[x, y, z] != 0)
                            {
                                BlockType type = (BlockType)blockData[x, y, z];
                               
                                
                                //Positive Y (UP)
                                if ((y + 1 >= bounds || blockData[x, y + 1, z] == 0))// &&
                                //yLoc + y >= CalculateMeshDepth(
                                 //    RegionManager.GetGlobalHeightMapValue(x, z), x + 1, z + 1, x - 1, z - 1))
                                {
                                    BlockFaceInstance face = new(new(x + xLoc, y + yLoc, z + zLoc), Face.UP,
                                        (int)ModelLoader.GetModel(type).GetTexture(Face.UP));
                                    UploadFace(face);
                                    blockFacesInChunk++;
                                }
                                // Positive X (EAST)
                                if ((x + 1 >= bounds || blockData[x + 1, y, z] == 0))// &&
                                //         yLoc + y >= CalculateMeshDepth(
                                //             RegionManager.GetGlobalHeightMapValue(x, z), x + 1, z + 1, x - 1, z - 1))
                                {
                                    BlockFaceInstance face = new(new(x + xLoc, y + yLoc, z + zLoc), Face.EAST,
                                             (int)ModelLoader.GetModel(type).GetTexture(Face.EAST));
                                    UploadFace(face);
                                    blockFacesInChunk++;
                                }

                                
                                //Negative X (WEST)
                                if ((x - 1 < 0 || blockData[x - 1, y, z] == 0))// &&
                                //        yLoc + y >= CalculateMeshDepth(
                                //             RegionManager.GetGlobalHeightMapValue(x, z), x + 1, z + 1, x - 1, z - 1))
                                {
                                    //Texture enum value corresponds to texture array layer 
                                    BlockFaceInstance face = new(new(x + xLoc , y + yLoc, z + zLoc), Face.WEST,
                                      (int)ModelLoader.GetModel(type).GetTexture(Face.WEST));

                                    UploadFace(face);
                                    blockFacesInChunk++;
                                }

                                //Negative Y (DOWN)
                                if ((y - 1 < 0 || blockData[x, y - 1, z] == 0)
                                    //If player is below the blocks Y level, render the bottom face
                                     && Window.GetPlayer().GetPosition().Y < y)
                                {
                                    //Texture enum value corresponds to texture array layer 
                                    BlockFaceInstance face = new(new(x + xLoc, y + yLoc, z + zLoc), Face.DOWN,
                                      (int)ModelLoader.GetModel(type).GetTexture(Face.DOWN));

                                    UploadFace(face);
                                    blockFacesInChunk++;
                                }

                                //Positive Z (NORTH)
                                if ((z + 1 >= bounds || blockData[x, y, z + 1] == 0))// &&
                                 //    yLoc + y >= CalculateMeshDepth(
                                 //        RegionManager.GetGlobalHeightMapValue(x, z), x + 1, z + 1, x - 1, z - 1))
                                {
                                    //Texture enum value corresponds to texture array layer 
                                    BlockFaceInstance face = new(new(x + xLoc, y + yLoc, z + zLoc), Face.NORTH,
                                      (int)ModelLoader.GetModel(type).GetTexture(Face.NORTH));

                                    UploadFace(face);
                                    blockFacesInChunk++;
                                }

                                //Negative Z (SOUTH)
                                if ((z - 1 < 0 || blockData[x, y, z - 1] == 0))// &&
                                //    yLoc + y >= CalculateMeshDepth(
                                //         RegionManager.GetGlobalHeightMapValue(x, z), x + 1, z + 1, x - 1, z - 1))
                                {
                                    //Texture enum value corresponds to texture array layer 
                                    BlockFaceInstance face = new(new(x + xLoc, y + yLoc, z + zLoc), Face.SOUTH,
                                      (int)ModelLoader.GetModel(type).GetTexture(Face.SOUTH));

                                    UploadFace(face);
                                    blockFacesInChunk++;
                                }
                            }
                        }
                    }
                }
                isGenerated = true;
            }
        }

        private void UploadFace(BlockFaceInstance face)
        {

            //Write face directly to SSBO
            unsafe
            {
                int offset = Window.GetNextFaceIndex() * Marshal.SizeOf<BlockFaceInstance>();
                byte* basePtr = (byte*)Window.GetSSBOPtr().ToPointer();
                BlockFaceInstance* instancePtr = (BlockFaceInstance*)(basePtr + offset);

                int instanceSize = Marshal.SizeOf<BlockFaceInstance>();
                if (offset + instanceSize > Window.SSBOSize)
                    throw new InvalidOperationException("SSBO overflow");

                *instancePtr = face;
                Window.GetAndIncrementNextFaceIndex();
            }
        }

        public int[,,] GetBlockData()
        {
            return blockData;
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

        public bool IsGenerated()
        {
            return isGenerated;
        }
        public override string ToString()
        {
            return $"Chunk[{xLoc},{yLoc},{zLoc}]";
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        //Resets this chunk. It will be regenerated next time it is cached.
        public void Reset()
        {
            isGenerated = false;
        }
    }
}