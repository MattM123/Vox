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
        public ConcurrentDictionary<Vector4, BlockFaceInstance> SSBOdata = [];

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
         * Uploads a single block face to the SSBO for rendering.
         * If the index is already present, updates the face data.
         */

        public void AddOrUpdateFaceInMemory(BlockFaceInstance face)
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

        public override bool Equals(object obj)
        {
            Vector3 chunk1 = new Vector3(xLoc, yLoc, zLoc);

            if (obj is Chunk other)
            {
                Vector3 chunk2 = new(other.xLoc, other.yLoc, other.zLoc);
                return chunk1.Equals(chunk2);
            }
            return false;
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