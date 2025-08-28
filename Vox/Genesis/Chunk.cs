
using System.Runtime.InteropServices;
using MessagePack;
using OpenTK.Mathematics;
using Vox.Model;
using Vox.Rendering;


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
        public readonly short[,] heightMap = new short[RegionManager.CHUNK_BOUNDS, RegionManager.CHUNK_BOUNDS];
 
        [Key(4)]
        public bool IsInitialized = false;
 
        [Key(5)]
        public short[,,] lightmap = new short[RegionManager.CHUNK_BOUNDS, RegionManager.CHUNK_BOUNDS, RegionManager.CHUNK_BOUNDS];

        //TODO: Make this global in region manager
        //[Key(6)]
        // public Queue<LightNode> BFSEmissivePropagationQueue = new((int)Math.Pow(RegionManager.CHUNK_BOUNDS, 3));

        [Key(6)]
        public short [,,] blockData = new short[RegionManager.CHUNK_BOUNDS, RegionManager.CHUNK_BOUNDS, RegionManager.CHUNK_BOUNDS];

        //O(1) Lookup on all elements by Vector3 location gives max 6 elements.
        //Vector4 (x, y, z, faceDir)
        [IgnoreMember]
        public Dictionary<Vector4, BlockFaceInstance> SSBOdata = [];

        [IgnoreMember]
        public bool IsEmpty = true;
 
        [IgnoreMember]
        private static readonly object chunkLock = new();
 
        [IgnoreMember]
        private Vector3 location;

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
         * @return the chunk
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
                        short elevation = RegionManager.GetGlobalHeightMapValue(x1, z1);


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
                                blockData[x1, y1, z1] = (short)RegionManager.GetGlobalBlockType((int)(x1 + xLoc), (int)(y1 + yLoc), (int)(z1 + zLoc));
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

            for (int x = 0; x < bounds; x++)
            {
                for (int y = 0; y < bounds; y++)
                {
                    for (int z = 0; z < bounds; z++)
                    {
                        Vector3 facePos = new(x + xLoc, y + yLoc, z + zLoc);
                        if (blockData[x, y, z] != 0)
                        {
                            BlockType type = (BlockType)blockData[x, y, z];
                            
                            //Positive Y (UP)
                            if (y + 1 >= bounds || blockData[x, y + 1, z] == 0)
                            {
                                int texLayer = (int)ModelLoader.GetModel(type).GetTexture(Face.UP);
                                Face faceDir = Face.UP;
                                AddBlockFace(facePos, texLayer, faceDir);
                            }
                            // Positive X (EAST)
                            if (x + 1 >= bounds || blockData[x + 1, y, z] == 0)
                            {
                                int texLayer = (int)ModelLoader.GetModel(type).GetTexture(Face.EAST);
                                Face faceDir = Face.EAST;
                                AddBlockFace(facePos, texLayer, faceDir);
                            }

                            
                            //Negative X (WEST)
                            if (x - 1 < 0 || blockData[x - 1, y, z] == 0)
                            {
                                int texLayer = (int)ModelLoader.GetModel(type).GetTexture(Face.WEST);
                                Face faceDir = Face.WEST;
                                AddBlockFace(facePos, texLayer, faceDir);
                            }

                            //Negative Y (DOWN)
                            if ((y - 1 < 0 || blockData[x, y - 1, z] == 0)
                                //If player is below the blocks Y level, render the bottom face
                                 && Window.GetPlayer().GetPosition().Y < y)
                            {
                                int texLayer = (int)ModelLoader.GetModel(type).GetTexture(Face.DOWN);
                                Face faceDir = Face.DOWN;
                                AddBlockFace(facePos, texLayer, faceDir);
                            }

                            //Positive Z (NORTH)
                            if (z + 1 >= bounds || blockData[x, y, z + 1] == 0)
                            {
                                int texLayer = (int)ModelLoader.GetModel(type).GetTexture(Face.NORTH);
                                Face faceDir = Face.NORTH;
                                AddBlockFace(facePos, texLayer, faceDir);
                            }

                            //Negative Z (SOUTH)
                            if (z - 1 < 0 || blockData[x, y, z - 1] == 0)
                            {
                                int texLayer = (int)ModelLoader.GetModel(type).GetTexture(Face.SOUTH);
                                Face faceDir = Face.SOUTH;
                                AddBlockFace(facePos, texLayer, faceDir);
                            }
                        }
                    }
                }
            }
            isGenerated = true;
        }

        /**
         * Adds a blockface to the chunk in memory to cache for any necessary updating
         * and also uploads it to the SSBO for rendering.
         */
        public void AddBlockFace(Vector3 facePos, int texLayer, Face faceDir) 
        {

            //Look for existing face to update
            if (SSBOdata.ContainsKey(new(facePos, (float)faceDir)))
            {
                BlockFaceInstance existingFace = SSBOdata[new(facePos, (float)faceDir)];

                //Update face texture
                existingFace.textureLayer = texLayer;

                //Update entire instance
                SSBOdata[new(facePos, (float)faceDir)] = existingFace;
            }
            else
                //Add instance if it doesnt exist
                SSBOdata.Add(new(facePos, (float)faceDir), new(facePos, faceDir, texLayer, Window.GetAndIncrementNextFaceIndex()));

            UploadFaceToMemory(SSBOdata[new(facePos, (float)faceDir)]);
            blockFacesInChunk++;
        }


        public void IncrementFaceCount()
        {
            blockFacesInChunk++;
        }
        /**
         * Uploads a single block face to the SSBO for rendering.
         * If the index is already present, updates the face data.
         */

        private void UploadFaceToMemory(BlockFaceInstance face)
        {

            //Write face directly to SSBO
            unsafe
            {
                int offset = face.index * Marshal.SizeOf<BlockFaceInstance>();
                byte* basePtr = (byte*)Window.GetSSBOPtr().ToPointer();

                BlockFaceInstance* instancePtr = (BlockFaceInstance*)(basePtr + offset);

                int instanceSize = Marshal.SizeOf<BlockFaceInstance>();

                if (offset + instanceSize > Window.SSBOSize)
                    throw new InvalidOperationException("SSBO overflow");

                *instancePtr = face;
            }
        }

       // public void ClearSSBOMemory()
       // {
       //     //Clear SSBO memory for this chunk's faces
       //     unsafe
       //     {
       //         foreach (KeyValuePair<Vector4, BlockFaceInstance> kv in SSBOdata)
       //         {
       //             int offset = kv.Value.index * Marshal.SizeOf<BlockFaceInstance>();
       //             byte* basePtr = (byte*)Window.GetSSBOPtr().ToPointer();
       //             BlockFaceInstance* instancePtr = (BlockFaceInstance*)(basePtr + offset);
       //             Marshal.FreeHGlobal((IntPtr)instancePtr);
       //         }
       //     }
       // }


        /**
         * Returns the raw block data for this chunk
         * @return 3D array of block types in this chunk
         */
        public short[,,] GetBlockData()
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

        public static Matrix4 GetModelMatrix()
        {
            return modelMatrix;
        }


        public short[,] GetHeightmap()
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

        //private void PropagateTorchLight(int x, int y, int z, int val)
        //{
        //    int bounds = RegionManager.CHUNK_BOUNDS;
        //    SetBlockLight(x, y, z, val);
        //
        //    short index = (short) (y * bounds * bounds + z * bounds + x);
        //    BFSEmissivePropagationQueue.Enqueue(new(index, this));
        //
        //    while (BFSEmissivePropagationQueue.Count > 0)
        //    {
        //
        //        // Get a reference to the front node.
        //        LightNode node = BFSEmissivePropagationQueue.Dequeue();
        //
        //        int idx = node.Index;
        //
        //        Chunk chunk = node.Chunk;
        //
        //
        //        // Extract x, y, and z from our chunk.
        //        // Depending on how you access data in your chunk, this may be optional
        //
        //        int x1 = index % bounds;
        //
        //        int y1 = index / (bounds * bounds);
        //
        //        int z1 = (index % (bounds * bounds)) / bounds;
        //
        //        // Grab the light level of the current node       
        //
        //        int lightLevel = chunk.GetTorchLight(x, y, z);
        //
        //        //Look at all neighbouring voxels to that node.
        //        //if their light level is 2 or more levels less than
        //        //the current node, then set their light level to
        //        //the current nodes light level - 1, and then add
        //        //them to the queue.
        //
        //        //  if (chunk->getBlock(x - 1, y, z).opaque == false
        //        if (chunk.GetTorchLight(x - 1, y, z) + 2 <= lightLevel)
        //        {
        //
        //            // Set its light level
        //            chunk.SetBlockLight(x - 1, y, z, lightLevel - 1);
        //
        //            // Emplace new node to queue. (could use push as well)
        //            BFSEmissivePropagationQueue.Enqueue(new(index, chunk));
        //
        //        }
        //
        //        // Check other five neighbors
        //    }
        //}
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


        /**
         * Flags chunk to regenerate next frame.
         */
        public void Reset()
        {
            blockFacesInChunk = 0;
            isGenerated = false;
            SSBOdata.Clear();
            //ClearSSBOMemory();
        }
    }
}