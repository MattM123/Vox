using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using MessagePack;
using OpenTK.Mathematics;
using Vox.Assets;
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
        private IssboManager? _ssboManager;

        [IgnoreMember]
        private IRegionManager? _regionManager;

        [IgnoreMember]
        private IAssetLookup? _assetLookup;

        //Vector4 (x, y, z, faceDir)
        [IgnoreMember]
        public ConcurrentDictionary<Vector4, BlockFaceInstance> SsbOdata = [];

        [IgnoreMember]
        public bool IsEmpty = true;

        [IgnoreMember]
        private static readonly object ChunkLock = new();

        [IgnoreMember]
        private Vector3 _location;

        [IgnoreMember]
        private static Matrix4 _modelMatrix = new();

        [IgnoreMember]
        bool _isGenerated = false;

        [IgnoreMember]
        public int BlockFacesInChunk = 0;

        [IgnoreMember]
        public int ChunkBounds;

        [IgnoreMember]
        public bool IsInitialized = false;

        [IgnoreMember]
        public bool IsCached = false;

        [IgnoreMember] public bool SiteProcessed = false;

        [Key(0)]
        public float XLoc;

        [Key(1)]
        public float ZLoc;

        [Key(2)]
        public float YLoc;

        [Key(3)]
        public short[,]? HeightMap;

        [Key(4)]
        public short[,,]? BlockData;

        [Key(5)] 
        public int SiteX;
        
        [Key(6)] 
        public int SiteZ;
        

        [SerializationConstructor]
        public Chunk() { }
        public Chunk(IssboManager ssboManager, IRegionManager regionManager, IAssetLookup assetLookup)
        {
            _assetLookup = assetLookup;
            _ssboManager = ssboManager;
            _regionManager = regionManager;
            ChunkBounds = _regionManager.GetChunkBounds();

            HeightMap = new short[_regionManager.GetChunkBounds(), _regionManager.GetChunkBounds()];
            BlockData = new short[_regionManager.GetChunkBounds(), _regionManager.GetChunkBounds(), _regionManager.GetChunkBounds()];
        }
        
        /**
         * Initializes a chunk at a given point and populates it's heightmap using Simplex noise
         * @param v The location of top left chunk corner
         * @param biome the biome that the block at this location inhabits
         * @return the chunk
         */
        public Chunk PopulateHeightmap(Vector3 v)
        {
            XLoc = v.X;
            YLoc = v.Y;
            ZLoc = v.Z;
            _location = new Vector3(XLoc, YLoc, ZLoc);
            
            //Generic model matrix applicable to every chunk object
            _modelMatrix = Matrix4.CreateTranslation(0, 0, 0);

            if (IsInitialized) return this;
            //===================================
            //Generate surface chunk height map
            //===================================

            int xCount = 0;
            int zCount = 0;

            int bounds = _regionManager!.GetChunkBounds();

            for (int localX = 0; localX < bounds; localX++)
            {
                for (int localZ = 0; localZ < bounds; localZ++)
                {
                    int worldX = (int)XLoc + localX;
                    int worldZ = (int)ZLoc + localZ;
                        
                    //Generate height map
                    HeightMap![localZ, localX] =
                        _regionManager.GetGlobalHeightMapValue(worldX, worldZ);

                }
            }
            for (int x1 = 0; x1 < _regionManager?.GetChunkBounds(); x1++)
            {
                for (int z1 = 0; z1 < _regionManager.GetChunkBounds(); z1++)
                {
                    for (int y1 = 0; y1 < _regionManager.GetChunkBounds(); y1++)
                    {
                        int worldX = (int)(x1 + XLoc);
                        int worldY = (int)(y1 + YLoc);
                        int worldZ = (int)(z1 + ZLoc);

                        int elevation = HeightMap![z1, x1];

                        //Set block data to AIR if it's not visible
                        if (worldY <= elevation)
                        {
                            BlockData![x1, y1, z1] =
                                (short)_regionManager.GetGeneratedBlockType(worldX, worldY, worldZ);
                        }
                        //Populate surface deco
                        else if (worldY == elevation + 1 && worldY == _regionManager.GetGlobalHeightMapValue(worldX, worldZ) + 1)
                        {
                            BlockData![x1, y1, z1] =
                                (short)BiomeExtensions.GetSurfaceDecoration(
                                    worldX, worldZ, 
                                    _regionManager.GetBiomeAtLocation(worldX, worldZ),
                                    _regionManager.GetWorlSeed(),
                                    _assetLookup!
                                );
                        }
                    }
                }
            }
            IsInitialized = true;


            return this;
        }
        

        /// <summary>
        /// Re-initializes the chunk with the given dependencies. 
        /// This is used when deserializing a region from file, as the region manager and SSBO manager
        /// is not serialized with the region.
        /// </summary>
        /// <param name="ssboManager">The SSBO manager for rendering the chunk</param>
        /// <param name="assetLookup">The asset manager used for texture and model lookups</param>
        /// <param name="regionManager">The top level world this chunk inhabits</param>
        /// <param name="x">X coordinate of the chunk</param>
        /// <param name="y">Y coordinate of the chunk</param>
        /// <param name="z">Z coordinate of the chunk</param>
        public void Initialize(IssboManager ssboManager, IRegionManager regionManager, IAssetLookup assetLookup, float x, float y, float z)
        {
            XLoc = x;
            ZLoc = z;
            YLoc = y;
            _location = new Vector3(XLoc, YLoc, ZLoc);

            _assetLookup = assetLookup;
            _ssboManager = ssboManager;
            _regionManager = regionManager;
            ChunkBounds = _regionManager.GetChunkBounds();
            IsCached = false;

        }
        public void GenerateRenderData()
        {
            int bounds = ChunkBounds;

            if (_isGenerated) return;
            
            for (int x = 0; x < bounds; x++)
            {
                for (int y = 0; y < bounds; y++)
                {
                    for (int z = 0; z < bounds; z++)
                    {
                        Vector3 facePos = new(x + XLoc, y + YLoc, z + ZLoc);
                            
                        if (BlockData![x, y, z] > 0)
                        {
                            BlockType type = (BlockType)BlockData[x, y, z];
                            ushort isBillboard = _assetLookup!.IsBillboard(type) ? (ushort) 1 : (ushort) 0;
                                
                            //Positive Y (UP)
                            if (y + 1 >= bounds || BlockData[x, y + 1, z] == 0 || _assetLookup.IsBillboard((BlockType) BlockData[x, y + 1, z]))
                            {
                                int texLayer = _assetLookup!.GetModel(type).GetTextureLayer(BlockFace.Up);
                                BlockFace faceDir = BlockFace.Up;
                                AddOrUpdateBlockFace(facePos, texLayer, faceDir, isBillboard);
                            }

                            //Negative Y (DOWN)
                            if (y - 1 < 0 || BlockData[x, y - 1, z] == 0 || _assetLookup.IsBillboard((BlockType) BlockData[x, y - 1, z]))
                            {
                                int texLayer = _assetLookup!.GetModel(type).GetTextureLayer(BlockFace.Down);
                                BlockFace faceDir = BlockFace.Down;
                                AddOrUpdateBlockFace(facePos, texLayer, faceDir, isBillboard);
                            }
                                
                            
                            // Positive X (EAST)
                            if (x + 1 >= bounds || BlockData[x + 1, y, z] == 0 || _assetLookup.IsBillboard((BlockType) BlockData[x + 1, y, z]))
                            {
                                int texLayer = _assetLookup!.GetModel(type).GetTextureLayer(BlockFace.East);
                                BlockFace faceDir = BlockFace.East;
                                AddOrUpdateBlockFace(facePos, texLayer, faceDir, isBillboard);
                            }

                            //Negative X (WEST)
                            if (x - 1 < 0 || BlockData[x - 1, y, z] == 0 || _assetLookup.IsBillboard((BlockType) BlockData[x - 1, y, z]))
                            {
                                int texLayer = _assetLookup!.GetModel(type).GetTextureLayer(BlockFace.West);
                                BlockFace faceDir = BlockFace.West;
                                AddOrUpdateBlockFace(facePos, texLayer, faceDir, isBillboard);
                            }

                            //Positive Z (NORTH)
                            if (z + 1 >= bounds || BlockData[x, y, z + 1] == 0 || _assetLookup.IsBillboard((BlockType) BlockData[x, y, z + 1]))
                            {
                                int texLayer = _assetLookup!.GetModel(type).GetTextureLayer(BlockFace.North);
                                BlockFace faceDir = BlockFace.North;
                                AddOrUpdateBlockFace(facePos, texLayer, faceDir, isBillboard);
                            }

                            //Negative Z (SOUTH)
                            if (z - 1 < 0 || BlockData[x, y, z - 1] == 0 || _assetLookup.IsBillboard((BlockType) BlockData[x, y, z - 1]))
                            {
                                int texLayer = _assetLookup!.GetModel(type).GetTextureLayer(BlockFace.South);
                                BlockFace faceDir = BlockFace.South;
                                AddOrUpdateBlockFace(facePos, texLayer, faceDir, isBillboard);
                            }
                                
                        }
                    }
                }
            }
            _isGenerated = true;
        }

        public bool IsSiteProcessed()
        {
            return SiteProcessed;
        }
        public void ProcessChunkSite()
        {
            //Site used for Voroni site rejection sampling.
            int chunkSeed = HashCode.Combine(_regionManager!.GetWorlSeed(), XLoc, ZLoc);

            Random rnd = new(chunkSeed);
            SiteX = rnd.Next(0, _regionManager.GetChunkBounds());
            SiteZ = rnd.Next(0, _regionManager.GetChunkBounds());
            

            _regionManager.ProcessChunkSite(this);
            SiteProcessed = true;

        }
        /**
         * Adds a blockface to the chunk in memory to cache for any necessary updating
         * and also uploads it to the SSBO for rendering.
         */
        public void AddOrUpdateBlockFace(Vector3 facePos, int texLayer, BlockFace faceDir, ushort isBillboard)
        {
            StorageBufferObject terrainSsbo = _ssboManager!.GetSsbo(Ssbo.Terrain);
            Vector4 key = new(facePos.X, facePos.Y, facePos.Z, (float)faceDir);
            
            //Look for existing face to update 
            if (SsbOdata.TryGetValue(key, out BlockFaceInstance existingFace))
            {
                //Update Texture
                existingFace.textureLayer = texLayer;

                //isBillboard block
                existingFace.isBillboard = isBillboard;
                
                //Update entire instance
                SsbOdata[key] = existingFace;

                //Update data for GPU
                AddOrUpdateFaceInMemory(existingFace);
            }
            // If no existing face, assign the face an index and add to the SSBO data
            else
            {
                int idx = terrainSsbo.AllocateIndex();

                SsbOdata[key] = new(facePos, faceDir, texLayer, idx, isBillboard, 0);
              //  Console.WriteLine("BLOCKFACE TEXTURE: " + model.GetTexture(faceDir));
              //  Console.WriteLine("PROVIDED LAYER: " + texLayer + " : ACTUAL LAYER: " + _assetLookup.GetTextureLayerFromTexture(model.GetTexture(faceDir)));
              //  Console.WriteLine();
                //Update data for GPU
                AddOrUpdateFaceInMemory(SsbOdata[key]);
            }
        }

        /**
         * Uploads a single block face to the SSBO for rendering.
         * If the index is already present, updates the face data.
         */
        public void AddOrUpdateFaceInMemory(BlockFaceInstance face)
        {
            StorageBufferObject terrainSsbo = _ssboManager!.GetSsbo(Ssbo.Terrain);
            //Write face directly to SSBO
            unsafe
            {
                int offset = face.index * Marshal.SizeOf<BlockFaceInstance>();
                byte* basePtr = (byte*)terrainSsbo.Pointer;

                BlockFaceInstance* instancePtr = (BlockFaceInstance*)(basePtr + offset);

                int instanceSize = Marshal.SizeOf<BlockFaceInstance>();

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
            return _location;
        }

        public static Matrix4 GetModelMatrix()
        {
            return _modelMatrix;
        }


        public short[,] GetHeightmap()
        {
            return HeightMap!;
        }

        public bool IsGenerated()
        {
            return _isGenerated;
        }
        public override string ToString()
        {
            return $"Chunk[{XLoc},{YLoc},{ZLoc}]";
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            Vector3 chunk1 = new(XLoc, YLoc, ZLoc);

            if (obj is Chunk other)
            {
                Vector3 chunk2 = new(other.XLoc, other.YLoc, other.ZLoc);
                return chunk1.Equals(chunk2);
            }
            return false;
        }
        /**
         * Frees memory taken by SSBO indcies to be used with other block faces.
         * Returns the size in bytes of the freed memory.
         */
        public int FreeShaderMemory()
        {
            StorageBufferObject terrainSsbo = _ssboManager!.GetSsbo(Ssbo.Terrain);
            BlockFacesInChunk = 0;
            _isGenerated = false;
            int size = SsbOdata.Count;

            foreach (var face in SsbOdata.Values)
            {
                terrainSsbo.FreeIndex(face.index);
            }

            SsbOdata.Clear();

            return size * Marshal.SizeOf<BlockFaceInstance>();

        }
        public void SetCached(bool cached)
        {
            IsCached = cached;
        }
    }
}