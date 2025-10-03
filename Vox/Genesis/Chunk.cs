using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using MessagePack;
using OpenTK.Mathematics;
using Vox.Enums;
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
        public short [,,] blockData = new short[RegionManager.CHUNK_BOUNDS, RegionManager.CHUNK_BOUNDS, RegionManager.CHUNK_BOUNDS];


        //Vector4 (x, y, z, faceDir)
        [IgnoreMember]
        public Dictionary<Vector4, BlockFaceInstance> SSBOdata = [];

        //Track the visibility of each voxel in the chunk, used for rendering and mesh updates
       // [IgnoreMember]
       // public bool[,,] voxelVisibility = new bool[RegionManager.CHUNK_BOUNDS, RegionManager.CHUNK_BOUNDS, RegionManager.CHUNK_BOUNDS];

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

                            //Set block data to AIR and visibility to false if its not visible
                            if (y + y1 <= elevation && elevation >= GetLocation().Y)
                            {
                                blockData[x1, y1, z1] = (short)RegionManager.GetGlobalBlockType((int)(x1 + xLoc), (int)(y1 + yLoc), (int)(z1 + zLoc));
                              //  voxelVisibility[x1, y1, z1] = true;
                            }
                            else
                            {
                                blockData[x1, y1, z1] = 0;
                              //  voxelVisibility[x1, y1, z1] = false;
                            }
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

            if (!isGenerated)
            {
                for (int x = 0; x < bounds; x++)
                {
                    for (int y = 0; y < bounds; y++)
                    {
                        for (int z = 0; z < bounds; z++)
                        {
                            Vector3 facePos = new(x + xLoc, y + yLoc, z + zLoc);
                            if (blockData[x, y, z] > 0)
                            {
                                BlockType type = (BlockType)blockData[x, y, z];

                                //Positive Y (UP)
                                if (y + 1 >= bounds || blockData[x, y + 1, z] == 0)
                                {
                                    int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.UP);
                                    BlockFace faceDir = BlockFace.UP;
                                    AddUpdateBlockFace(facePos, texLayer, faceDir);
                                }
                                // Positive X (EAST)
                                if (x + 1 >= bounds || blockData[x + 1, y, z] == 0)
                                {
                                    int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.EAST);
                                    BlockFace faceDir = BlockFace.EAST;
                                    AddUpdateBlockFace(facePos, texLayer, faceDir);
                                }


                                //Negative X (WEST)
                                if (x - 1 < 0 || blockData[x - 1, y, z] == 0)
                                {
                                    int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.WEST);
                                    BlockFace faceDir = BlockFace.WEST;
                                    AddUpdateBlockFace(facePos, texLayer, faceDir);
                                }

                                //Negative Y (DOWN)
                                if ((y - 1 < 0 || blockData[x, y - 1, z] == 0)
                                     //If player is below the blocks Y level, render the bottom face
                                     && Window.GetPlayer().GetPosition().Y < y)
                                {
                                    int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.DOWN);
                                    BlockFace faceDir = BlockFace.DOWN;
                                    AddUpdateBlockFace(facePos, texLayer, faceDir);
                                }

                                //Positive Z (NORTH)
                                if (z + 1 >= bounds || blockData[x, y, z + 1] == 0)
                                {
                                    int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.NORTH);
                                    BlockFace faceDir = BlockFace.NORTH;
                                    AddUpdateBlockFace(facePos, texLayer, faceDir);
                                }

                                //Negative Z (SOUTH)
                                if (z - 1 < 0 || blockData[x, y, z - 1] == 0)
                                {
                                    int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.SOUTH);
                                    BlockFace faceDir = BlockFace.SOUTH;
                                    AddUpdateBlockFace(facePos, texLayer, faceDir);
                                }
                            }
                        }
                    }
                }
                isGenerated = true;
            }
        }

        /**
         * Adds a blockface to the chunk in memory to cache for any necessary updating
         * and also uploads it to the SSBO for rendering.
         */
        public void AddUpdateBlockFace(Vector3 facePos, int texLayer, BlockFace faceDir)
        {

            Vector4 key = new(facePos.X, facePos.Y, facePos.Z, (float)faceDir);
            
            //Look for existing face to update
            if (SSBOdata.TryGetValue(key, out BlockFaceInstance existingFace))
            {
                //Update Texture
                existingFace.textureLayer = texLayer;
   
                //Update entire instance
                SSBOdata[key] = existingFace;
           
            }
            else
                //Add instance if it doesnt exist
                SSBOdata.TryAdd(key, new(facePos, faceDir, texLayer, Window.GetAndIncrementNextFaceIndex(), 0));
            
            //Update data for GPU
            AddOrUpdateFaceInMemory(SSBOdata[key]);
        }

        /**
        * Update the lighting value in the correct BlockFaceInstance which is 
        * then passsed to the shaders for rendering
        */
        public void UpdateEmissiveLighting(Vector3 facePos, BlockFace faceDir, int lighting)
        {
            Vector4 key = new(facePos.X, facePos.Y, facePos.Z, (float)faceDir);

            if (SSBOdata.TryGetValue(key, out BlockFaceInstance existingFace))
            {
                //Update lighting
                existingFace.lighting = lighting;
           
                //Update entire instance
                SSBOdata[key] = existingFace;
           
                //Update data for GPU
                AddOrUpdateFaceInMemory(SSBOdata[key]);
            }

        }

        public void IncrementFaceCount()
        {
            blockFacesInChunk++;
        }
        /**
         * Uploads a single block face to the SSBO for rendering.
         * If the index is already present, updates the face data.
         */

        private void AddOrUpdateFaceInMemory(BlockFaceInstance face)
        {

            //Write face directly to SSBO
            unsafe
            {
                int offset = face.index * Marshal.SizeOf<BlockFaceInstance>();
                byte* basePtr = (byte*)Window.GetSSBOPtr().ToPointer();

                BlockFaceInstance* instancePtr = (BlockFaceInstance*)(basePtr + offset);

                int instanceSize = Marshal.SizeOf<BlockFaceInstance>();

                if (offset + instanceSize > Window.GetSSBOSize())
                    throw new InvalidOperationException("SSBO overflow");

                *instancePtr = face;
            }
        }


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

        //Given the block data index of a chunk, returns the light level for red green and blue channels
        public ColorVector GetBlockLightVector(Vector3 location)
        {
            return new(GetRedLight(location), GetGreenLight(location), GetBlueLight(location));

        }
        public ushort GetBlockLight(Vector3 location, BlockFace faceDir)
        {
            try
            {
                return (ushort) SSBOdata[new(location.X, location.Y, location.Z, (int)faceDir)].lighting;
            }
            catch (KeyNotFoundException)
            {
                return 0;
            }

        }
        // Set emissive RGB values
        public void SetBlockFaceLight(Vector3 location, BlockFace faceDir, ColorVector color)
        {
            SetRedLight(location, faceDir, color.Red);
            SetGreenLight(location, color.Green);
            SetBlueLight(location, color.Blue);
        }

        // Get the bits XXXX0000
        public int GetSunlight(int x, int y, int z)
        {
            return 0;// (lightmap[GetIndex(new(x + (int)xLoc, y + (int)yLoc, z + (int)zLoc))] >> 4) & 0xF;
        }
        public int GetSunlight(Vector3i v)
        {
            return 0;// (lightmap[GetIndex(new(v.X + (int)xLoc, v.Y + (int)yLoc, v.Z + (int)zLoc))] >> 4) & 0xF;
        }

        // Set the bits XXXX0000
        public void SetSunlight(int x, int y, int z, int val)
        {
           // lightmap[GetIndex(new(x + (int)xLoc, y + (int)yLoc, z + (int)zLoc))] = (byte) ((lightmap[GetIndex(new(x + (int)xLoc, y + (int)yLoc, z + (int)zLoc))] & 0xF000) | (val << 4));
        }
        // ================ Blue component (bits 0-3) =================
        public int GetBlueLight(Vector3 location)
        {
            // Convert the short to an unsigned integer for correct masking
            //  uint temp = lightmap[GetIndex(new((int) location.X, (int) location.Y, (int) location.Z))];
            return 0;// (int)(temp & 0x0F);
        }
        public void SetBlueLight(Vector3 location, int val)
        {
            if (val < 0)
                val = 0;
            else if (val > 15)
                val = 15;

            // Apply mask to new value to ensure only relevant bits are set
            int newValue = val & 0x000F;

            // Combine existing and new blue values without affecting higher-order bits
            //lightmap[GetIndex(new(v.X + (int)xLoc, v.Y + (int)yLoc, v.Z + (int)zLoc))] = (ushort)(lightmap[GetIndex(new(v.X + (int)xLoc, v.Y + (int)yLoc, v.Z + (int)zLoc))] | newValue);
        }
        // ================ Green component (bits 4-7) ================
        public int GetGreenLight(Vector3 location)
        {
            return 0;// ((char)(lightmap[GetIndex(new((int) location.X, (int) location.Y, (int) location.Z))] >> 4)) & 0x0F;
        }
        public void SetGreenLight(Vector3 location, int val)
        {

            if (val < 0)
                val = 0;
            else if (val > 15)
                val = 15;

            // Apply mask to new value to ensure only relevant bits are set
            int newValue = (val << 4) & 0x00F0;

            // Combine existing and new green values without affecting higher-order bits
           // lightmap[GetIndex(new(location.X + (int)xLoc, location.Y + (int)yLoc, location.Z + (int)zLoc))] = (ushort) (lightmap[GetIndex(new(location.X + (int)xLoc, location.Y + (int)yLoc, location.Z + (int)zLoc))] | newValue);

        }
        // ===========================================================

        // ================ Red component (bits 8-11) ================
        public int GetRedLight(Vector3 location)
        {
            if (SSBOdata.ContainsKey(new(location.X, location.Y, location.Z, (int)BlockFace.UP)))
            {
                int red = SSBOdata[new(location.X, location.Y, location.Z, (int)BlockFace.UP)].lighting;
                return ((char)red >> 8) & 0x0F;
            }
            else
                return 0;

        }
        public void SetRedLight(Vector3 location, BlockFace faceDir, int val)
        {
            if (val < 0)
                val = 0;
            else if (val > 15)
                val = 15;

            // Apply mask to new value to ensure only relevant bits are set
            int newValue = (val << 8) & 0x0F00;

            Vector3 facePos = new(location.X, location.Y, location.Z);
            Vector3i index = RegionManager.GetChunkRelativeCoordinates(location);
            BlockType type = (BlockType)blockData[index.X, index.Y, index.Z];

            if (faceDir == BlockFace.ALL)
            {

                if (SSBOdata.ContainsKey(new(facePos, (int)faceDir))) {
                    UpdateEmissiveLighting(facePos, BlockFace.UP, newValue | (ushort)SSBOdata[new(facePos, (int)BlockFace.UP)].lighting);
                    UpdateEmissiveLighting(facePos, BlockFace.DOWN, newValue | (ushort)SSBOdata[new(facePos, (int)BlockFace.DOWN)].lighting);
                    UpdateEmissiveLighting(facePos, BlockFace.EAST, newValue | (ushort)SSBOdata[new(facePos, (int)BlockFace.EAST)].lighting);
                    UpdateEmissiveLighting(facePos, BlockFace.WEST, newValue | (ushort)SSBOdata[new(facePos, (int)BlockFace.WEST)].lighting);
                    UpdateEmissiveLighting(facePos, BlockFace.NORTH, newValue | (ushort)SSBOdata[new(facePos, (int)BlockFace.NORTH)].lighting);
                    UpdateEmissiveLighting(facePos, BlockFace.SOUTH, newValue | (ushort)SSBOdata[new(facePos, (int)BlockFace.SOUTH)].lighting);
                }
            }
            else
            {
                if (SSBOdata.ContainsKey(new(facePos, (int)faceDir)))
                    UpdateEmissiveLighting(facePos, faceDir, newValue | (ushort)SSBOdata[new(facePos, (int)faceDir)].lighting);
            }
        }
        // ============================================================


        private short GetIndex(Vector3i v)
        {
            int bounds = RegionManager.CHUNK_BOUNDS;
            return (short)(v.X * bounds * bounds + v.Y * bounds + v.Z);
        }

        //FIX: Propagates the opposite direction or doesnt propagate at all when X coordinates are negative
        //FIX: Only propagates when Z is positive
        //FIX: Properly propagate across chunk boundaries and region boundaries
        //Idea: If I just update blockfaces directly in this function then i wouldnt ned to store them in memory
        public void PropagateBlockLight(Vector3 location)
        {
            int x = (int) location.X;
            int y = (int) location.Y;
            int z = (int) location.Z;

            Vector3i blockDataIndex = RegionManager.GetChunkRelativeCoordinates(location);

            // Check the light level of the current node before propagating          
            ColorVector originLightLevel = GetBlockLightVector(location);

            if (originLightLevel.Red > 0 || originLightLevel.Green > 0 || originLightLevel.Blue > 0)
            {
                int i = 1;
                int bounds = RegionManager.CHUNK_BOUNDS;

                short index = (short)(x * bounds * bounds + y * bounds + z);

                RegionManager.EnqueueEmissiveLightNode(new(index, RegionManager.GetAndLoadGlobalChunkFromCoords(location)));

                while (RegionManager.GetEmissiveQueueCount() > 0)
                {


                    // Get a reference to the front node.
                    LightNode node = RegionManager.DequeueEmissiveLightNode();
                    Chunk chunk = node.Chunk;
                    int currentIdx = node.Index;
                    int lightLocationX = currentIdx % bounds;
                    int lightLocationY = currentIdx / (bounds * bounds);
                    int lightLocationZ = (currentIdx % (bounds * bounds)) / bounds;
                   
                    //Look at all neigZbouring voxels to that node.
                    //if their light level is 2 or more levels less than
                    //the current node, then set their light level to
                    //the current nodes light level - 1, and then add
                    //them to the queue.
                    Vector3i negXIndex = new(blockDataIndex.X - i, blockDataIndex.Y - 1, blockDataIndex.Z);

                    chunk = RegionManager.GetAndLoadGlobalChunkFromCoords(new(location.X - i, location.Y - 1, location.Z));                   


                    if ((originLightLevel.Red - i > 0 ||
                        originLightLevel.Green - i > 0 ||
                        originLightLevel.Blue - i > 0))
                    {

                        Vector3 setLightHere = new(location.X - i, location.Y - 1, location.Z);
                        if (chunk.GetBlockLightVector(setLightHere).Red <= originLightLevel.Red && originLightLevel.Red - i > 0)
                            // Add the propagated red light level to the blocks current level
                            chunk.SetRedLight(setLightHere, BlockFace.UP, originLightLevel.Red - i + chunk.GetRedLight(setLightHere));

                       // if (chunk.GetBlockLight(negXIndex).Green <= originLightLevel.Green && originLightLevel.Green - i > 0)
                       //     // Add the propagated green light level to the blocks current level
                       //     chunk.SetGreenLight(negXIndex, originLightLevel.Green - i + chunk.GetGreenLight(negXIndex));
                       //
                       // if (chunk.GetBlockLight(negXIndex).Blue <= originLightLevel.Blue && originLightLevel.Blue - i > 0)
                       //     // Add the propagated blue light level to the blocks current level
                       //     chunk.SetBlueLight(negXIndex, originLightLevel.Blue - i + chunk.GetBlueLight(negXIndex));

                        // Emplace new node to queue. (could use push as well)
                        index = (short)((x - i) * bounds * bounds + y * bounds + z);
                        RegionManager.EnqueueEmissiveLightNode(new(index, chunk));
                    }

                    i++;

                }
            }
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


        /**
         * Flags chunk to regenerate next frame.
         */
        public void Reset()
        {
            blockFacesInChunk = 0;
            isGenerated = false;
            SSBOdata.Clear();
        }
    }
}