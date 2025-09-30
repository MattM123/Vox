
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
        public ushort[,,] lightmap = new ushort[RegionManager.CHUNK_BOUNDS, RegionManager.CHUNK_BOUNDS, RegionManager.CHUNK_BOUNDS];

        [Key(6)]
        public short [,,] blockData = new short[RegionManager.CHUNK_BOUNDS, RegionManager.CHUNK_BOUNDS, RegionManager.CHUNK_BOUNDS];

        //O(1) Lookup on all elements by Vector3 location gives max 6 elements.
        //Vector4 (x, y, z, faceDir)
        [IgnoreMember]
        public Dictionary<Vector4, BlockFaceInstance> SSBOdata = [];

        //Track the visibility of each voxel in the chunk, used for rendering and mesh updates
        [IgnoreMember]
        public bool[,,] voxelVisibility = new bool[RegionManager.CHUNK_BOUNDS, RegionManager.CHUNK_BOUNDS, RegionManager.CHUNK_BOUNDS];

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
                                voxelVisibility[x1, y1, z1] = true;
                            }
                            else
                            {
                                blockData[x1, y1, z1] = 0;
                                voxelVisibility[x1, y1, z1] = false;
                            }
                            //SetSunlight(x1, y1, z1, 7);
                            SetBlockLight(new(x1, y1, z1), new ColorVector(0,0,0));
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
                            if (voxelVisibility[x, y, z] == true)
                            {
                                BlockType type = (BlockType)blockData[x, y, z];

                                //Positive Y (UP)
                                if (y + 1 >= bounds || blockData[x, y + 1, z] == 0)
                                {
                                    int texLayer = (int)ModelLoader.GetModel(type).GetTexture(Face.UP);
                                    Face faceDir = Face.UP;
                                    AddUpdateBlockFace(facePos, texLayer, faceDir);
                                }
                                // Positive X (EAST)
                                if (x + 1 >= bounds || blockData[x + 1, y, z] == 0)
                                {
                                    int texLayer = (int)ModelLoader.GetModel(type).GetTexture(Face.EAST);
                                    Face faceDir = Face.EAST;
                                    AddUpdateBlockFace(facePos, texLayer, faceDir);
                                }


                                //Negative X (WEST)
                                if (x - 1 < 0 || blockData[x - 1, y, z] == 0)
                                {
                                    int texLayer = (int)ModelLoader.GetModel(type).GetTexture(Face.WEST);
                                    Face faceDir = Face.WEST;
                                    AddUpdateBlockFace(facePos, texLayer, faceDir);
                                }

                                //Negative Y (DOWN)
                                if ((y - 1 < 0 || blockData[x, y - 1, z] == 0)
                                     //If player is below the blocks Y level, render the bottom face
                                     && Window.GetPlayer().GetPosition().Y < y)
                                {
                                    int texLayer = (int)ModelLoader.GetModel(type).GetTexture(Face.DOWN);
                                    Face faceDir = Face.DOWN;
                                    AddUpdateBlockFace(facePos, texLayer, faceDir);
                                }

                                //Positive Z (NORTH)
                                if (z + 1 >= bounds || blockData[x, y, z + 1] == 0)
                                {
                                    int texLayer = (int)ModelLoader.GetModel(type).GetTexture(Face.NORTH);
                                    Face faceDir = Face.NORTH;
                                    AddUpdateBlockFace(facePos, texLayer, faceDir);
                                }

                                //Negative Z (SOUTH)
                                if (z - 1 < 0 || blockData[x, y, z - 1] == 0)
                                {
                                    int texLayer = (int)ModelLoader.GetModel(type).GetTexture(Face.SOUTH);
                                    Face faceDir = Face.SOUTH;
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
        public void AddUpdateBlockFace(Vector3 facePos, int texLayer, Face faceDir) 
        {
            //The block data index within that chunk to modify
            Vector3i blockDataIndex = RegionManager.GetChunkRelativeCoordinates(facePos);

            //Look for existing face to update
            if (SSBOdata.ContainsKey(new(facePos, (float)faceDir)))
            {
                BlockFaceInstance existingFace = SSBOdata[new(facePos, (float)faceDir)];

                //Exit early if nothing is changing
                if (existingFace.lighting == lightmap[blockDataIndex.X, blockDataIndex.Y, blockDataIndex.Z] &&
                    existingFace.textureLayer == (int) ModelLoader.GetModel(blockData[blockDataIndex.X, blockDataIndex.Y, blockDataIndex.Z]).GetTexture(faceDir))
                    return;

                //Update fields
                existingFace.textureLayer = texLayer;
                //Update lighting
                existingFace.lighting = lightmap[blockDataIndex.X, blockDataIndex.Y, blockDataIndex.Z];

                //Update entire instance
                SSBOdata[new(facePos, (float)faceDir)] = existingFace;

            }
            else
                //Add instance if it doesnt exist
                SSBOdata.Add(new(facePos, (float)faceDir), new(facePos, faceDir, texLayer,
                    Window.GetAndIncrementNextFaceIndex(), lightmap[blockDataIndex.X, blockDataIndex.Y, blockDataIndex.Z]));

            UploadFaceToMemory(SSBOdata[new(facePos, (float)faceDir)]);
            blockFacesInChunk++;
        }

        /**
        * Update the lighting value in the correct BlockFaceInstance which is 
        * then passsed to the shaders for rendering
        */
        public void UpdateEmissiveLighting(Vector3 facePos, Face faceDir, Chunk chunk)
        {
            //The block data index within that chunk to modify
            Vector3i blockDataIndex = RegionManager.GetChunkRelativeCoordinates(facePos);
           // Vector3 facePos = new(blockDataIndex.X + chunk.xLoc, blockDataIndex.Y + chunk.yLoc, blockDataIndex.Z + chunk.zLoc);

            Console.WriteLine("Updating light at location " + facePos + " SSBO Index: " + facePos + " " + faceDir.ToString());
            //Look for existing face to update
            if (chunk.SSBOdata.ContainsKey(new(facePos, (float)faceDir)))
            {
                BlockFaceInstance existingFace = chunk.SSBOdata[new(facePos, (float)faceDir)];

                //Exit early if nothing is changing
                if (existingFace.lighting == chunk.lightmap[blockDataIndex.X, blockDataIndex.Y, blockDataIndex.Z])
                {
                    Console.WriteLine("Exit early");
                    return;
                }

                int t = existingFace.lighting;
                //Update lighting
                existingFace.lighting = chunk.lightmap[blockDataIndex.X, blockDataIndex.Y, blockDataIndex.Z];

                //Update entire instance
                chunk.SSBOdata[new(facePos, (float)faceDir)] = existingFace;

                //Update data for GPU
                Console.WriteLine("Uploading light value: Before " + t + " After " + existingFace.lighting);
                UploadFaceToMemory(chunk.SSBOdata[new(facePos, (float)faceDir)]);
            } else
            {
                Console.WriteLine("Face not found af " + facePos);
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

        private void UploadFaceToMemory(BlockFaceInstance face)
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
        public ColorVector GetBlockLight(Vector3i v)
        {
            return new(GetRedLight(v), GetGreenLight(v), GetBlueLight(v));
        }
        // Set emissive RGB values
        public void SetBlockLight(Vector3i v, ColorVector color)
        {
            SetRedLight(v, color.Red);
            SetGreenLight(v, color.Green);
            SetBlueLight(v, color.Blue);
        }

        // Get the bits XXXX0000
        public int GetSunlight(int x, int y, int z)
        {
            return (lightmap[x,y,z] >> 4) & 0xF;
        }
        public int GetSunlight(Vector3i v)
        {
            return (lightmap[v.X, v.Y, v.Z] >> 4) & 0xF;
        }

        // Set the bits XXXX0000
        public void SetSunlight(int x, int y, int z, int val)
        {
            lightmap[x, y, z] = (byte) ((lightmap[x, y, z] & 0xF000) | (val << 4));
        }
        // ================ Blue component (bits 0-3) =================
        public int GetBlueLight(Vector3i v)
        {
            // Convert the short to an unsigned integer for correct masking
            uint temp = lightmap[v.X, v.Y, v.Z];
            return (int)(temp & 0x0F);
        }
        public void SetBlueLight(Vector3i v, int val)
        {
            if (val < 0)
                val = 0;
            else if (val > 15)
                val = 15;

            // Apply mask to new value to ensure only relevant bits are set
            int newValue = val & 0x000F;

            // Combine existing and new blue values without affecting higher-order bits
            lightmap[v.X, v.Y, v.Z] = (ushort)(lightmap[v.X, v.Y, v.Z] | newValue);
        }
        // ================ Green component (bits 4-7) ================
        public int GetGreenLight(Vector3i v)
        {
            return ((char)(lightmap[v.X, v.Y, v.Z] >> 4)) & 0x0F;
        }
        public void SetGreenLight(Vector3i v, int val)
        {

            if (val < 0)
                val = 0;
            else if (val > 15)
                val = 15;

            // Apply mask to new value to ensure only relevant bits are set
            int newValue = (val << 4) & 0x00F0;

            // Combine existing and new green values without affecting higher-order bits
            lightmap[v.X, v.Y, v.Z] = (ushort) (lightmap[v.X, v.Y, v.Z] | newValue);

        }
        // ===========================================================

        // ================ Red component (bits 8-11) ================
        public int GetRedLight(Vector3i v)
        {
            return ((char)(lightmap[v.X, v.Y, v.Z] >> 8)) & 0x0F;
        }
        public void SetRedLight(Vector3i v, int val)
        {
            if (val < 0)
                val = 0;
            else if (val > 15)
                val = 15;

            // Apply mask to new value to ensure only relevant bits are set
            int newValue = (val << 8) & 0x0F00;

            // Combine existing and new green values without affecting higher-order bits
            lightmap[v.X, v.Y, v.Z] = (ushort)(lightmap[v.X, v.Y, v.Z] | newValue);

        }
        // ============================================================


        //FIX: Propagates the opposite direction or doesnt propagate at all when X coordinates are negative
        //FIX: Only propagates when Z is positive
        //FIX: Properly propagate across chunk boundaries and region boundaries
        public void PropagateBlockLight(Vector3 v)
        {
            int x = (int) v.X;
            int y = (int) v.Y;
            int z = (int) v.Z;

            Vector3i blockDataIndex = RegionManager.GetChunkRelativeCoordinates(v);

            // Check the light level of the current node before propagating          
            ColorVector originLightLevel = GetBlockLight(blockDataIndex);

            if (originLightLevel.Red > 0 || originLightLevel.Green > 0 || originLightLevel.Blue > 0)
            {
                int i = 1;
                //blockDataIndex = RegionManager.GetChunkRelativeCoordinates(v);
                int bounds = RegionManager.CHUNK_BOUNDS;

                short index = (short)(x * bounds * bounds + y * bounds + z);

                RegionManager.EnqueueEmissiveLightNode(new(index, this));

                while (RegionManager.GetEmissiveQueueCount() > 0)
                {


                    // Get a reference to the front node.
                    LightNode node = RegionManager.DequeueEmissiveLightNode();
                    Chunk chunk = node.Chunk;


                    //Look at all neighbouring voxels to that node.
                    //if their light level is 2 or more levels less than
                    //the current node, then set their light level to
                    //the current nodes light level - 1, and then add
                    //them to the queue.
                    Vector3i negXIndex = new(blockDataIndex.X - i, blockDataIndex.Y - 1, blockDataIndex.Z);

                    if (negXIndex.X <= 0)
                    {
                        //Wrap to neighboring chunk if out of bounds
                        negXIndex.X = bounds - 1 - Math.Abs(negXIndex.X);
                        chunk = RegionManager.GetAndLoadGlobalChunkFromCoords(new(v.X - i, v.Y, v.Z));                   
                    }
                    if (negXIndex.Y <= 0)
                    {
                        //Wrap to neighboring chunk if out of bounds
                        negXIndex.Y = bounds - 1 - Math.Abs(negXIndex.Y);
                        chunk = RegionManager.GetAndLoadGlobalChunkFromCoords(new(v.X - i, v.Y - 1, v.Z));
                    }

                    // try
                    // {


                    if (chunk.GetBlockLight(negXIndex).Red <= originLightLevel.Red && originLightLevel.Red - i > 0)
                    {
                        // Set its light level
                        chunk.SetRedLight(negXIndex, originLightLevel.Red - i);

                        //Check Red light in -X direction
                     //   Console.WriteLine("Red light: " + chunk.GetBlockLight(negXIndex).Red + " at " + negXIndex);
                      //  Console.WriteLine("i: " + i);
                        //chunk.SetRedLight(blockDataIndex.X - 1, blockDataIndex.Y, blockDataIndex.Z, lightLevel - 1);
                        //chunk.SetBlueLight(blockDataIndex.X - 1, blockDataIndex.Y, blockDataIndex.Z, lightLevel - 1);
                        //chunk.SetGreenLight(blockDataIndex.X - 1, blockDataIndex.Y, blockDataIndex.Z, lightLevel - 1);

                        // Emplace new node to queue. (could use push as well)
                        index = (short)((x - i) * bounds * bounds + y * bounds + z);
                        RegionManager.EnqueueEmissiveLightNode(new(index, chunk));

                        i++;

                    }
                    //  } catch (IndexOutOfRangeException) {
                    //     Console.Write("Index out of range in light propagation\n");
                    // }
                    // Console.WriteLine("PROP RED: " + chunk.GetBlockLight(new(blockDataIndex.X - 2, blockDataIndex.Y, blockDataIndex.Z)).X);
                    // Check other five neighbors

                    //Graphically update light level before uploading ot GPU
                    Vector3 facePos = new(negXIndex.X + xLoc, negXIndex.Y + yLoc, negXIndex.Z + zLoc);
                    chunk.UpdateEmissiveLighting(facePos, Face.UP, chunk);
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