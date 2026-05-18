using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using MessagePack;
using OpenTK.Mathematics;
using Vox.Assets.Models;
using Vox.Enums;
using Vox.Model;
using Vox.Rendering;

namespace Vox.Genesis
{

    [MessagePackObject]
    public class Chunk
    {
        [IgnoreMember]
        private ISSBOManager? _ssboManager;

        [IgnoreMember]
        private IRegionManager? _regionManager;

        //Vector4 (x, y, z, faceDir)
        [IgnoreMember]
        public ConcurrentDictionary<Vector4, BlockFaceInstance> SSBOdata = [];

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

        [IgnoreMember]
        public int _chunkBounds;

        [IgnoreMember]
        public bool IsInitialized = false;

        [Key(0)]
        public float xLoc;
 
        [Key(1)]
        public float zLoc;
 
        [Key(2)]
        public float yLoc;

        [Key(3)]
        public short[,]? _heightMap;

        [Key(4)]
        public short[,,]? _blockData;

        [SerializationConstructor]
        public Chunk() { }
        public Chunk(ISSBOManager ssboManager, IRegionManager regionManager)
        {
            _ssboManager = ssboManager ?? throw new Exception(nameof(ssboManager) + " is null in Chunk");
            _regionManager = regionManager ?? throw new Exception(nameof(regionManager) + " is null in Chunk");
            _chunkBounds = _regionManager.GetChunkBounds();

            _heightMap = new short[_regionManager.GetChunkBounds(), _regionManager.GetChunkBounds()];
            _blockData = new short[_regionManager.GetChunkBounds(), _regionManager.GetChunkBounds(), _regionManager.GetChunkBounds()];
        }

        /**
         * Initializes a chunk at a given point and populates it's heightmap using Simplex noise
         * @param x coordinate of top left chunk corner
         * @param y coordinate of top left chunk corner
         * @param z coordinate of top left chunk corner
         * @return the chunk
         */
        public Chunk PopulateHeightmap(float x, float y, float z)
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

                for (int x1 = (int)x; x1 < x + _regionManager?.GetChunkBounds(); x1++)
                {
                    for (int z1 = (int)z; z1 < z + _regionManager?.GetChunkBounds(); z1++)
                    {
                        short elevation = _regionManager!.GetGlobalHeightMapValue(x1, z1);


                        _heightMap![zCount, xCount] = elevation;

                        zCount++;
                        if (zCount > _regionManager.GetChunkBounds() - 1)
                            zCount = 0;

                    }
                    xCount++;
                    if (xCount > _regionManager?.GetChunkBounds() - 1)
                        xCount = 0;
                }
                for (int x1 = 0; x1 < _regionManager?.GetChunkBounds(); x1++)
                {
                    for (int z1 = 0; z1 < _regionManager.GetChunkBounds(); z1++)
                    {
                        for (int y1 = 0; y1 < _regionManager.GetChunkBounds(); y1++)
                        {
                            int elevation = _heightMap![z1, x1];

                            //Set block data to AIR and visibility to false if its not visible
                            if (y1 + yLoc <= elevation && elevation >= yLoc)
                            {
                                _blockData![x1, y1, z1] = (short)_regionManager.GetGlobalBlockType((int)(x1 + xLoc), (int)(y1 + yLoc), (int)(z1 + zLoc));
                            }
                            else
                            {
                                _blockData![x1, y1, z1] = 0;
                            }
                        }
                    }
                }
                IsInitialized = true;
            
            }


            return this;
        }


        /// <summary>
        /// Re-Initializes the chunk with the given SSBO manager and region manager. 
        /// This is used when deserializing a region from file, as the region manager and SSBO manager
        /// is not serialized with the region.
        /// </summary>
        /// <param name="ssboManager"></param>
        /// <exception cref="Exception"></exception>
        public void Initialize(ISSBOManager ssboManager, IRegionManager regionManager, float x, float y, float z)
        {
            xLoc = x;
            zLoc = z;
            yLoc = y;
            location = new Vector3(xLoc, yLoc, zLoc);

            _ssboManager = ssboManager ?? throw new Exception(nameof(ssboManager) + " is null in Chunk");
            _regionManager = regionManager ?? throw new Exception(nameof(regionManager) + " is null in Chunk");
            _chunkBounds = _regionManager.GetChunkBounds();

        }
        public void GenerateRenderData()
        {
            int bounds = _chunkBounds;

            if (!isGenerated)
            {
                for (int x = 0; x < bounds; x++)
                {
                    for (int y = 0; y < bounds; y++)
                    {
                        for (int z = 0; z < bounds; z++)
                        {
                            Vector3 facePos = new(x + xLoc, y + yLoc, z + zLoc);
                            if (_blockData![x, y, z] > 0)
                            {
                                BlockType type = (BlockType)_blockData[x, y, z];

                                //Positive Y (UP)
                                if (y + 1 >= bounds || _blockData[x, y + 1, z] == 0)
                                {
                                    int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.UP);
                                    BlockFace faceDir = BlockFace.UP;
                                    AddOrUpdateBlockFace(facePos, texLayer, faceDir);
                                }
                                // Positive X (EAST)
                                if (x + 1 >= bounds || _blockData[x + 1, y, z] == 0)
                                {
                                    int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.EAST);
                                    BlockFace faceDir = BlockFace.EAST;
                                    AddOrUpdateBlockFace(facePos, texLayer, faceDir);
                                }


                                //Negative X (WEST)
                                if (x - 1 < 0 || _blockData[x - 1, y, z] == 0)
                                {
                                    int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.WEST);
                                    BlockFace faceDir = BlockFace.WEST;
                                    AddOrUpdateBlockFace(facePos, texLayer, faceDir);
                                }

                                //Negative Y (DOWN)
                                if (y - 1 < 0 || _blockData[x, y - 1, z] == 0)
                                {
                                    int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.DOWN);
                                    BlockFace faceDir = BlockFace.DOWN;
                                    AddOrUpdateBlockFace(facePos, texLayer, faceDir);
                                }

                                //Positive Z (NORTH)
                                if (z + 1 >= bounds || _blockData[x, y, z + 1] == 0)
                                {
                                    int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.NORTH);
                                    BlockFace faceDir = BlockFace.NORTH;
                                    AddOrUpdateBlockFace(facePos, texLayer, faceDir);
                                }

                                //Negative Z (SOUTH)
                                if (z - 1 < 0 || _blockData[x, y, z - 1] == 0)
                                {
                                    int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.SOUTH);
                                    BlockFace faceDir = BlockFace.SOUTH;
                                    AddOrUpdateBlockFace(facePos, texLayer, faceDir);
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
        public void AddOrUpdateBlockFace(Vector3 facePos, int texLayer, BlockFace faceDir)
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
                byte* basePtr = (byte*)_ssboManager!.GetSSBO(SSBO.Terrain).Pointer;

                BlockFaceInstance* instancePtr = (BlockFaceInstance*)(basePtr + offset);

                int instanceSize = Marshal.SizeOf<BlockFaceInstance>();

                if (offset + instanceSize > _ssboManager!.GetSSBO(SSBO.Terrain).Size)
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
            return _heightMap!;
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
            Vector3 chunk1 = new(xLoc, yLoc, zLoc);

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