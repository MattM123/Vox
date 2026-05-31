using System.Collections.Concurrent;
using System.Drawing;
using System.Security.Cryptography;
using MessagePack;
using OpenTK.Mathematics;
using Vox.Assets;
using Vox.Assets.Models;
using Vox.Enums;
using Vox.Exceptions;
using Vox.Model;
using Vox.Rendering;
namespace Vox.Genesis
{

    public class RegionManager : IRegionManager
    {
        private Dictionary<string, Region> VisibleRegions = [];
        private readonly List<Vector3> visited = [];

        private readonly ISSBOManager _ssboManager;
        private readonly ILightHelper _lightHelper;
        private readonly IAssetLookup _assetLookup;

        private string worldDir = "";
        private readonly int WORLD_HEIGHT = 500;
        private readonly int RENDER_DISTANCE = 3;
        public readonly int REGION_BOUNDS = 512;
        private readonly int CHUNK_BOUNDS = 32;
        private readonly long WORLD_SEED;
        private readonly object chunkLock = new();

        private readonly ConcurrentQueue<LightNode> BFSEmissivePropagationQueue;
        /**
         * The highest level object representation of a world. The RegionManager
         * contains an in-memory list of regions that are currently within
         * the players render distance. This region list is constantly updated each
         * frame and is used for reading regions from file and writing regions to file.
         *
         * @param path The file path of this worlds directory
         */

        /// <summary>
        /// Initializes a new instance of the <see cref="RegionManager"/> class.
        /// </summary>
        /// <param name="path">The file path of this world's directory.</param>
        /// <param name="ssboManager">The SSBO manager.</param>
        /// <param name="lightHelper">The light helper.</param>
        public RegionManager(ISSBOManager ssboManager, ILightHelper lightHelper, IAssetLookup assetLookup)
        {
            _lightHelper = lightHelper;
            _ssboManager = ssboManager;
            _assetLookup = assetLookup;

            BFSEmissivePropagationQueue = new(new Queue<LightNode>((int)Math.Pow(CHUNK_BOUNDS, 3)));

            byte[] buffer = new byte[8];
            RandomNumberGenerator.Fill(buffer); // Fills the buffer with random bytes
            WORLD_SEED = BitConverter.ToInt64(buffer, 0);
        }

        /// <summary>
        /// Sets the directory path used to store region data for the world.
        /// </summary>
        /// <remarks>If the specified directory does not exist, it is created along with a 'regions'
        /// subdirectory. Any errors encountered during directory creation are logged.</remarks>
        /// <param name="path">The file system path to the directory where region data will be stored. Cannot be null or empty.</param>
        public void SetRegionDir(string path)
        {
            try
            {
                worldDir = path;
                Directory.CreateDirectory(Path.Combine(worldDir, "regions"));
            } catch (Exception ex)
            {
                Logger.Error(ex, "RegionManager");
            }
            
        }

        /// <summary>
        /// Removes all regions from the collection of visible regions.
        /// </summary>
        public void ClearVisibleRegions()
        {
            VisibleRegions.Clear();
        }

        /// <summary>
        /// Gets the fixed height of the world in blocks.
        /// </summary>
        /// <returns>The height of the world as an integer value.</returns>
        public int GetWorldHeight() { return WORLD_HEIGHT; }

        /// <summary>
        /// Gets the size of one side of the cubic chunk. 
        /// This value defines the length, width, and height of a chunk in blocks.
        /// </summary>
        /// <returns>An integer representing the size of the chunk.</returns>
        public int GetChunkBounds() { return CHUNK_BOUNDS; }
        private void EnqueueEmissiveLightNode(LightNode node)
        {
            BFSEmissivePropagationQueue.Enqueue(node);
        }

        /// <summary>
        /// Removes and returns the next emissive light node from the propagation queue.
        /// </summary>
        /// <remarks>If the queue is empty, the returned light node will have default values. Callers
        /// should check the returned node to determine if a valid node was dequeued.</remarks>
        /// <returns>The next available emissive light node if the queue is not empty; otherwise, a default-initialized light
        /// node.</returns>
        private LightNode DequeueEmissiveLightNode()
        {
            if (BFSEmissivePropagationQueue.TryDequeue(out LightNode node))
                return node;
            else
                return new LightNode("", null);
        }

        /// <summary>
        /// Calculates the global height map value at the specified coordinates using procedural noise generation.
        /// </summary>
        /// <remarks>The returned height is determined by combining multiple octaves of noise, resulting
        /// in varied terrain features.</remarks>
        /// <param name="x">The X-coordinate to get the height map value for.</param>
        /// <param name="z">The Z-coordinate to get the height map value for</param>
        /// <returns>A <see cref="short"/> representing the normalized terrain height at the specified coordinates. The value ranges
        /// from 0 to the maximum world height.</returns>
        public short GetGlobalHeightMapValue(int x, int z)
        {
            long seed;
            if (Window.IsMainMenuScreenDisplayed())
                seed = Window.GetMenuSeed();
            else
                seed = WORLD_SEED;

            //Affects height of terrain. A higher value will result in lower, smoother terrain while a lower value will result in
            // a rougher, raised terrain
            float var1 = 100;

            //Affects coalescence of terrain. A higher value will result in more condensed, sharp peaks and a lower value will result in
            //more smooth, spread out hills.
            double var2 = 0.021;

            float f = 1 * OpenSimplex2.Noise2(seed, x * var2, z * var2) / (var1 + 2) //Noise Octave 1
                    + (float)(0.5 * OpenSimplex2.Noise2(seed, x * (var2 * 2), z * (var2 * 2)) / (var1 + 4)) //Noise Octave 2
                    + (float)(0.25 * OpenSimplex2.Noise2(seed, x * (var2 * 2), z * (var2 * 2)) / (var1 + 6)); //Noise Octave 3

            //Normalized the noise value and scale between 0 and WORLD_HEIGHT
            float noise = (f * 0.5f) + 0.5f;
            return (short)(noise * WORLD_HEIGHT);

        }

        /// <summary>
        /// Given a 3D point in world space, convert to chunk relative coordinates within
        /// the range of the chunks bounds.
        /// </summary>
        /// <remarks>Use this method to determine the local position of a point within a chunk, given its
        /// world coordinates.</remarks>
        /// <param name="v">The world-space coordinates to convert to chunk-relative coordinates.</param>
        /// <returns>A <see cref="Vector3i"/> representing the position of the point relative to the origin of its chunk. Each component is in
        /// the range [0, CHUNK_BOUNDS].</returns>
        public Vector3i GetChunkRelativeCoordinates(Vector3 v)
        {
            return new Vector3i(
                (int)Math.Abs(v.X % CHUNK_BOUNDS),
                (int)Math.Abs(v.Y % CHUNK_BOUNDS),
                (int)Math.Abs(v.Z % CHUNK_BOUNDS)
            );
        }

        /// <summary>
        /// Determines the global block _type at the specified world coordinates using Simplex noise.
        /// </summary>
        /// <remarks>The block _type is selected based on a noise function seeded by either the current
        /// menu seed or the world seed, depending on the application state. The result is clamped to exclude non-block
        /// types. This method will return consistent results
        /// for the same coordinates and seed.</remarks>
        /// <param name="x">The X coordinate in world space</param>
        /// <param name="y">The Y coordinate in world space</param>
        /// <param name="z">The Z coordinate in world space</param>
        /// <returns>A <see cref="BlockType"/> enumeration representing the block _type at the given coordinates.</returns>
        public BlockType GetGlobalBlockType(int x, int y, int z)
        {
            long seed;
            if (Window.IsMainMenuScreenDisplayed())
                seed = Window.GetMenuSeed();
            else
                seed = WORLD_SEED;

            //Generate noise and normalize to 0-1 from -1-1
            float noise = (OpenSimplex2.Noise3_ImproveXZ(seed, x, y, z) * 0.5f) + 0.5f;

            //how many different types of blocks are there?

            //this needs to be fixed. It has range 0-3 which doesnt map correctly to the texture layers
            List<BlockType> naturalBlockTypes = _assetLookup.GetNaturalBlockTypes();

            // Multiply normalized noise to get an index
            int index = (int)(noise * naturalBlockTypes.Count);
            index = Math.Clamp(index, 0, naturalBlockTypes.Count - 1);

            //Manually define ranges for certain block types like this:
            //if (noise < 0.3f)
            //    return BlockType.GRASS_BLOCK;
            //else if (noise < 0.6f)
            //    return BlockType.DIRT_BLOCK;
            //else
            //    return BlockType.STONE_BLOCK;

            // Convert index to BlockType
            return naturalBlockTypes[index];

        }



        /// <summary>
        /// Calculates the total number of chunks currently present in all visible regions.
        /// </summary>
        /// <returns>The total count of chunks across all visible regions. Returns 0 if no regions are visible or if all regions
        /// contain no chunks.</returns>
        public int PollChunkMemory()
        {
            int count = 0;
            foreach (KeyValuePair<string, Region> pair in VisibleRegions)
                count += pair.Value.chunks.Count;
            return count;
        }


        /// <summary>
        /// Gets the current render distance setting. Represents how many chunks are 
        /// rendered in each cardinal direction from the player's current position.
        /// </summary>
        /// <returns>The render distance value.</returns>
        public int GetRenderDistance() { return RENDER_DISTANCE; }


        /// <summary>
        /// Removes the specified region from the collection of visible regions and updates the display accordingly.
        /// </summary>
        /// <remarks>If the specified region does not exist in the collection, no action is taken. This
        /// method also logs the region removal and updates the display to reflect the change.</remarks>
        /// <param name="rIndex">The key of the region to remove from the visible regions collection. Cannot be null.</param>
        public void LeaveRegion(string rIndex)
        {
            Logger.Info($"Leaving {VisibleRegions[rIndex]}", ConsoleColor.Blue);
            WriteRegion(rIndex);
            VisibleRegions.Remove(rIndex);
        }


        /// <summary>
        /// Retrieves a region by its identifier, attempting to load it from the file system if it is not already visible.
        /// </summary>
        /// <param name="rIndex">The unique identifier of the region to retrieve. Cannot be null.</param>
        /// <returns>The region associated with the specified identifier. If the region is not already visible, it is loaded and
        /// added to the visible regions.</returns>
        public Region EnterRegion(string rIndex)
        {
            //If region is already visible
            if (VisibleRegions.TryGetValue(rIndex, out Region? value))
                return value;

            //Gets region from filesystem if it's not visible
            Region region = TryGetRegionFromFile(rIndex);

            if (!VisibleRegions.TryGetValue(rIndex, out _))
                VisibleRegions.Add(rIndex, region);

            return region;

        }



        /// <summary>
        /// Adds the specified region to the collection of visible regions if it is not already present and returns the
        /// region.
        /// </summary>
        /// <remarks>If the specified region is not already tracked as visible, it is added to the
        /// collection. The region is identified by its bounds' X and Y coordinates.</remarks>
        /// <param name="region">The region to enter. Cannot be null.</param>
        /// <returns>The region that was entered. If the region is already present, returns the existing region; otherwise,
        /// returns the newly added region.</returns>
        public void EnterRegion(Region region)
        {
            string regionIdx = $"{region.GetBounds().X}|{region.GetBounds().Y}";

            //Null and ungenerated regions are initlialized if necessary 
            if (region == null)
                EnterRegion(regionIdx);

            else
            {
                if (!VisibleRegions.TryGetValue(regionIdx, out _))
                    VisibleRegions.Add(regionIdx, region);
            }
        }


        /// <summary>
        /// Serializes the specified visible region and writes it to a file in the region data directory.
        /// </summary>
        /// <remarks>The region is serialized using MessagePack and saved to a file named according to the
        /// region's bounds. If a file with the same name already exists, it will be overwritten.</remarks>
        /// <param name="rIndex">The key identifying the region to write. Must correspond to an existing entry in the visible regions
        /// collection.</param>
        private void WriteRegion(string rIndex)
        {
            Region r = VisibleRegions[rIndex];

            string path = Path.Combine(worldDir, "regions", $"{r.GetBounds().X}.{r.GetBounds().Y}.dat");

            byte[] serializedRegion = MessagePackSerializer.Serialize(r);

            File.WriteAllBytes(path, serializedRegion);
        }

        /// <summary>
        /// Gets the constant value representing the region bounds. 
        /// This value defines the length and width of the 2D region in blocks.
        /// </summary>
        /// <returns>An integer value that specifies the region bounds.</returns>
        public int GetRegionBounds()
        {
            return REGION_BOUNDS;
        }

        /// <summary>
        /// Reads and deserializes a region from the specified file path.
        /// </summary>
        /// <remarks>After deserialization, the region and its chunks are initialized in preparation for loading and generating.
        /// The caller is responsible for ensuring the file at the specified path exists and contains
        /// valid region data.</remarks>
        /// <param name="path">The path to the file containing the serialized region data. Cannot be null or empty.</param>
        /// <returns>A <see cref="Region"/> object representing the deserialized region, or <see langword="null"/> if the
        /// operation fails.</returns>
        private Region? ReadRegion(string path)
        {
            byte[] serializedData = File.ReadAllBytes(path);

            // Deserialize the byte array back into an object
            Region region = MessagePackSerializer.Deserialize<Region>(serializedData);

            //Initialize region and chunk dependancies after deserialization
            region.Initialize(this);

            foreach (Chunk chunk in region.chunks.Values)
                chunk.Initialize(_ssboManager, this, _assetLookup, chunk.xLoc, chunk.yLoc, chunk.zLoc);

            return region;

        }



        /// <summary>
        /// Retrieves an existing region from memory or disk, or creates a new region if none exists for the specified
        /// index.
        /// </summary>
        /// <param name="rIndex">The regions index</param>
        /// <returns>A Region instance corresponding to the supplied index. Returns a new Region if no existing region is found
        /// in memory or on disk.</returns>
        public Region TryGetRegionFromFile(string rIndex)
        {
            int[] index = [.. rIndex.Split('|').Select(int.Parse)];
            string path = Path.Combine(worldDir, "regions", index[0] + "." + index[1] + ".dat");

            if (!VisibleRegions.TryGetValue(rIndex, out Region? value) && !File.Exists(path))
            {
                Logger.Info($"No file found for region {rIndex}. Creating new region.", ConsoleColor.Blue);
                return new Region(this, index[0], index[1]);
            }
            else if (VisibleRegions.TryGetValue(rIndex, out Region? val) && !File.Exists(path))
            {
                return val;
            }
            else if (!VisibleRegions.TryGetValue(rIndex, out Region? v) && File.Exists(path))
            {
                Logger.Info($"File found for region {rIndex}. Loading region from file.", ConsoleColor.Blue);
                return ReadRegion(path)!;
            }
            else // Region is visible and exists as a file
            {
                if (VisibleRegions.TryGetValue(rIndex, out Region? region))
                    if (region == null || region.chunks.Count == 0)
                        return new Region(this, index[0], index[1]);

                return VisibleRegions[rIndex];
            }
        }

        /// <summary>
        /// Gets the chunk at the specified global coordinates, loading or generating it if necessary.
        /// </summary>
        /// <remarks>If the chunk at the specified coordinates has not been generated, this method creates
        /// and loads it before returning. The method is thread-safe when adding new chunks to a region.</remarks>
        /// <param name="x">The global X coordinate of the chunk to retrieve.</param>
        /// <param name="y">The global Y coordinate of the chunk to retrieve.</param>
        /// <param name="z">The global Z coordinate of the chunk to retrieve.</param>
        /// <returns>The <see cref="Chunk"/> located at the specified global coordinates.</returns>
        public Chunk GetAndLoadGlobalChunkFromCoords(int x, int y, int z)
        {

            string playerChunkIdx =
                $"{Math.Floor((float)x / CHUNK_BOUNDS) * CHUNK_BOUNDS}|" +
                $"{Math.Floor((float)y / CHUNK_BOUNDS) * CHUNK_BOUNDS}|" +
                $"{Math.Floor((float)z / CHUNK_BOUNDS) * CHUNK_BOUNDS}";

            int[] chunkIdxArray = [.. playerChunkIdx.Split('|').Select(int.Parse)];
            string playerRegionIdx = GetRegionIndexFromChunkCoords(chunkIdxArray[0], chunkIdxArray[2]);
            Region r = EnterRegion(playerRegionIdx);

            //If chunk has not yet been generated, generate it
            if (!r.chunks.TryGetValue(playerChunkIdx, out Chunk? value))
            {
                value = new Chunk(_ssboManager, this, _assetLookup).PopulateHeightmap(chunkIdxArray[0], chunkIdxArray[1], chunkIdxArray[2]);
                lock (chunkLock)
                {
                    r.chunks.Add(playerChunkIdx, value);
                }
                return value;
            }
            else
            {
                return r.chunks[$"{chunkIdxArray[0]}|{chunkIdxArray[1]}|{chunkIdxArray[2]}"];
            }

        }
        public Chunk GetAndLoadGlobalChunkFromCoords(Vector3 v)
        {
            int x = (int)v.X;
            int y = (int)v.Y;
            int z = (int)v.Z;

            string chunkIdx =
                $"{Math.Floor((float)x / CHUNK_BOUNDS) * CHUNK_BOUNDS}|" +
                $"{Math.Floor((float)y / CHUNK_BOUNDS) * CHUNK_BOUNDS}|" +
                $"{Math.Floor((float)z / CHUNK_BOUNDS) * CHUNK_BOUNDS}";

            int[] chunkIdxArray = chunkIdx.Split('|').Select(int.Parse).ToArray();

            string regionIdx = GetRegionIndexFromChunkCoords(chunkIdxArray[0], chunkIdxArray[2]);
            Region r = EnterRegion(regionIdx);

            if (!r.chunks.TryGetValue(chunkIdx, out Chunk? value))
            {
                value = new Chunk(_ssboManager, this, _assetLookup).PopulateHeightmap(chunkIdxArray[0], chunkIdxArray[1], chunkIdxArray[2]);
                r.chunks.Add(chunkIdx, value);
            }
            return value;
        }

        /// <summary>
        /// Calculates the region index string corresponding to the specified chunk coordinates.
        /// </summary>
        /// <remarks>The region index is determined by grouping chunks into regions of a fixed size
        /// defined by REGION_BOUNDS. Negative chunk coordinates are handled such that regions extend correctly into
        /// negative space.</remarks>
        /// <param name="chunkX">The X coordinate of the chunk.</param>
        /// <param name="chunkZ">The Z coordinate of the chunk.</param>
        /// <returns>A <see cref="string"/> representing the region index in the format "x|z", where x and z are the world coordinates of the region
        /// containing the provided chunk.</returns>
        public string GetRegionIndexFromChunkCoords(int chunkX, int chunkZ)
        {
            int xLowerLimit = ((chunkX / REGION_BOUNDS) * REGION_BOUNDS);
            int xUpperLimit;
            if (chunkX < 0)
                xUpperLimit = xLowerLimit - REGION_BOUNDS;
            else
                xUpperLimit = xLowerLimit + REGION_BOUNDS;


            int zLowerLimit = ((chunkZ / REGION_BOUNDS) * REGION_BOUNDS);
            int zUpperLimit;
            if (chunkZ < 0)
                zUpperLimit = zLowerLimit - REGION_BOUNDS;
            else
                zUpperLimit = zLowerLimit + REGION_BOUNDS;

            return $"{xUpperLimit}|{zUpperLimit}";
        }


        /// <summary>
        /// Adds a block of the specified _type to the chunk at the given world-space coordinates and updates the corresponding chunk's
        /// rendering and lighting data as needed.
        /// </summary>
        /// <remarks>If the block _type is emissive, lighting data is updated and propagated
        /// asynchronously. Only the visible faces of the block are added to the chunk's rendering data, based on the
        /// presence of neighboring blocks. This method may trigger updates to lighting and rendering in adjacent chunks
        /// if the block is placed at a chunk boundary.</remarks>
        /// <param name="blockSpace">The world-space coordinates where the block will be added. Specifies the position within the global block
        /// grid.</param>
        /// <param name="type">The _type of block to add at the specified coordinates. Determines the block's appearance and behavior.</param>
        /// <param name="blockLight">The light color and intensity to assign to the block if it is emissive. Used for blocks that emit light,
        /// such as lamps.</param>
        public void AddBlockToChunk(Vector3 blockSpace, BlockType type, ColorVector blockLight)
        {

            //The chunk that is added to
            Chunk actionChunk = GetAndLoadGlobalChunkFromCoords(blockSpace);

            //The block data index within that chunk to modify
            Vector3i blockDataIndex = GetChunkRelativeCoordinates(blockSpace);

            //Update block data in chunk 
            actionChunk._blockData[(short)blockDataIndex.X, (short)blockDataIndex.Y, (short)blockDataIndex.Z] = (short)type;

            /*=============================================
             * Add a single block to the SSBO for rendering
             *=============================================*/

            int bounds = CHUNK_BOUNDS;

            int x = blockDataIndex.X;
            int y = blockDataIndex.Y;
            int z = blockDataIndex.Z;

            //Positive Y (UP)
            if (y + 1 >= bounds || actionChunk._blockData[(short)x, (short)(y + 1), (short)z] == 0)
            {
                int texLayer = (int)_assetLookup.GetModel(type).GetTextureLayer(BlockFace.UP);
                BlockFace faceDir = BlockFace.UP;
                actionChunk.AddOrUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing UP Face");
            }
            // Positive X (EAST)
            if (x + 1 >= bounds || actionChunk._blockData[(short)(x + 1), (short)y, (short)z] == 0)
            {
                int texLayer = (int)_assetLookup.GetModel(type).GetTextureLayer(BlockFace.EAST);
                BlockFace faceDir = BlockFace.EAST;
                actionChunk.AddOrUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing EAST Face");
            }


            //Negative X (WEST)
            if (x - 1 < 0 || actionChunk._blockData[(short)(x - 1), (short)y, (short)z] == 0)
            {
                int texLayer = (int)_assetLookup.GetModel(type).GetTextureLayer(BlockFace.WEST);
                BlockFace faceDir = BlockFace.WEST;
                actionChunk.AddOrUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing WEST Face");
            }

            //Negative Y (DOWN)
            if (y - 1 < 0 || actionChunk._blockData[(short)x, (short)(y - 1), (short)z] == 0)
            //If player is below the blocks Y level, render the bottom face
            // && Window.GetPlayer().GetPosition().Y < y)
            {
                int texLayer = (int)_assetLookup.GetModel(type).GetTextureLayer(BlockFace.DOWN);
                BlockFace faceDir = BlockFace.DOWN;
                actionChunk.AddOrUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing DOWN Face");
            }

            //Positive Z (NORTH)
            if (z + 1 >= bounds || actionChunk._blockData[(short)x, (short)(y), (short)(z + 1)] == 0)
            {
                int texLayer = (int)_assetLookup.GetModel(type).GetTextureLayer(BlockFace.NORTH);
                BlockFace faceDir = BlockFace.NORTH;
                actionChunk.AddOrUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing NORTH Face");
            }

            //Negative Z (SOUTH)
            if (z - 1 < 0 || actionChunk._blockData[(short)x, (short)(y), (short)(z - 1)] == 0)
            {
                int texLayer = (int)_assetLookup.GetModel(type).GetTextureLayer(BlockFace.SOUTH);
                BlockFace faceDir = BlockFace.SOUTH;
                actionChunk.AddOrUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing SOUTH Face");
            }

            //Set block emissiveness after addding faces
            if (type == BlockType.LAMP_BLOCK)
            {
                //Track emissive lighting for loading and deporpagation
                if (_lightHelper.GetLightTrackingList().ContainsKey(blockSpace))
                    return;

                ThreadPool.QueueUserWorkItem(new WaitCallback(delegate (object state)
                {
                    _lightHelper.TrackLighting(blockSpace, blockLight);

                    //Set all block faces to the same light levels
                    _lightHelper.SetBlockLight(blockSpace, blockLight, actionChunk, false, false);
                    PropagateBlockLight(blockSpace, BlockFace.UP, false, false);
                    PropagateBlockLight(blockSpace, BlockFace.DOWN, false, false);
                    PropagateBlockLight(blockSpace, BlockFace.EAST, false, false);
                    PropagateBlockLight(blockSpace, BlockFace.WEST, false, false);
                    PropagateBlockLight(blockSpace, BlockFace.NORTH, false, false);
                    PropagateBlockLight(blockSpace, BlockFace.SOUTH, false, false);
                }));

            }
        }



        /// <summary>
        /// Adds a block of the specified _type to the chunk at the given world-space coordinates and updates the corresponding chunk's
        /// rendering and lighting data as needed.
        /// </summary>
        /// <remarks>If the block _type is emissive, lighting data is updated and propagated
        /// asynchronously. Only the visible faces of the block are added to the chunk's rendering data, based on the
        /// presence of neighboring blocks. This method may trigger updates to lighting and rendering in adjacent chunks
        /// if the block is placed at a chunk boundary.</remarks>
        /// <param name="blockSpace">The world-space coordinates where the block will be added. Specifies the position within the global block
        /// grid.</param>
        /// <param name="type">The _type of block to add at the specified coordinates. Determines the block's appearance and behavior.</param>
        /// <param name="blockLight">The light color and intensity to assign to the block if it is emissive. Used for blocks that emit light,
        /// such as lamps.</param>
        /// <param name="colorOverride">Indicates whether to override the block's current light color with the specified blockLight color.</param>
        public void AddBlockToChunk(Vector3 blockSpace, BlockType type, ColorVector blockLight, bool colorOverride)
        {

            //The chunk that is added to
            Chunk actionChunk = GetAndLoadGlobalChunkFromCoords(blockSpace);

            //The block data index within that chunk to modify
            Vector3i blockDataIndex = GetChunkRelativeCoordinates(blockSpace);

            //Update block data in chunk 
            actionChunk._blockData[(short)blockDataIndex.X, (short)blockDataIndex.Y, (short)blockDataIndex.Z] = (short)type;

            /*=============================================
             * Add a single block to the SSBO for rendering
             *=============================================*/

            int bounds = CHUNK_BOUNDS;

            int x = blockDataIndex.X;
            int y = blockDataIndex.Y;
            int z = blockDataIndex.Z;

            //Positive Y (UP)
            if (y + 1 >= bounds || actionChunk._blockData[(short)x, (short)(y + 1), (short)z] == 0)
            {
                int texLayer = (int)_assetLookup.GetModel(type).GetTextureLayer(BlockFace.UP);
                BlockFace faceDir = BlockFace.UP;
                actionChunk.AddOrUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing UP Face");
            }
            // Positive X (EAST)
            if (x + 1 >= bounds || actionChunk._blockData[(short)(x + 1), (short)y, (short)z] == 0)
            {
                int texLayer = (int)_assetLookup.GetModel(type).GetTextureLayer(BlockFace.EAST);
                BlockFace faceDir = BlockFace.EAST;
                actionChunk.AddOrUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing EAST Face");
            }


            //Negative X (WEST)
            if (x - 1 < 0 || actionChunk._blockData[(short)(x - 1), (short)y, (short)z] == 0)
            {
                int texLayer = (int)_assetLookup.GetModel(type).GetTextureLayer(BlockFace.WEST);
                BlockFace faceDir = BlockFace.WEST;
                actionChunk.AddOrUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing WEST Face");
            }

            //Negative Y (DOWN)
            if (y - 1 < 0 || actionChunk._blockData[(short)x, (short)(y - 1), (short)z] == 0)
            //If player is below the blocks Y level, render the bottom face
            // && Window.GetPlayer().GetPosition().Y < y)
            {
                int texLayer = (int)_assetLookup.GetModel(type).GetTextureLayer(BlockFace.DOWN);
                BlockFace faceDir = BlockFace.DOWN;
                actionChunk.AddOrUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing DOWN Face");
            }

            //Positive Z (NORTH)
            if (z + 1 >= bounds || actionChunk._blockData[(short)x, (short)(y), (short)(z + 1)] == 0)
            {
                int texLayer = (int)_assetLookup.GetModel(type).GetTextureLayer(BlockFace.NORTH);
                BlockFace faceDir = BlockFace.NORTH;
                actionChunk.AddOrUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing NORTH Face");
            }

            //Negative Z (SOUTH)
            if (z - 1 < 0 || actionChunk._blockData[(short)x, (short)(y), (short)(z - 1)] == 0)
            {
                int texLayer = (int)_assetLookup.GetModel(type).GetTextureLayer(BlockFace.SOUTH);
                BlockFace faceDir = BlockFace.SOUTH;
                actionChunk.AddOrUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing SOUTH Face");
            }

            //Set block emissiveness after addding faces
            if (type == BlockType.LAMP_BLOCK)
            {
                //Track emissive lighting for loading and deporpagation
                if (_lightHelper.GetLightTrackingList().ContainsKey(blockSpace))
                    return;


                _lightHelper.TrackLighting(blockSpace, blockLight);

                //Set all block faces to the same light levels
                _lightHelper.SetBlockLight(blockSpace, blockLight, actionChunk, false, colorOverride);
                PropagateBlockLight(blockSpace, BlockFace.UP, false, colorOverride);
                PropagateBlockLight(blockSpace, BlockFace.DOWN, false, colorOverride);
                PropagateBlockLight(blockSpace, BlockFace.EAST, false, colorOverride);
                PropagateBlockLight(blockSpace, BlockFace.WEST, false, colorOverride);
                PropagateBlockLight(blockSpace, BlockFace.NORTH, false, colorOverride);
                PropagateBlockLight(blockSpace, BlockFace.SOUTH, false, colorOverride);

            }
        }


        /// <summary>
        /// Removes the provided block from its chunk, 
        /// updating the chunk's block data and rendering information accordingly. 
        /// If the removed block is emissive, this method also updates and propagates 
        /// lighting changes to adjacent blocks and chunks as needed.
        /// </summary>
        /// <param name="blockSpace">The block space.</param>
        public void RemoveBlockFromChunk(Vector3 blockSpace)
        {
            //The chunk that is added to
            Chunk actionChunk = GetAndLoadGlobalChunkFromCoords(blockSpace);

            //The block data index within that chunk to modify
            Vector3 blockDataIndex = GetChunkRelativeCoordinates(blockSpace);

            //Update block data in chunk  
            //   actionChunk.blockData[(short)blockDataIndex.X, (short)blockDataIndex.Y, (short)blockDataIndex.Z] = (int)BlockType.AIR;
            //actionChunk.voxelVisibility[(int)blockDataIndex.X, (int)blockDataIndex.Y, (int)blockDataIndex.Z] = false;

            /*==================================================
            * Set face instances to BlockType.AIR in SSBO
            *=================================================*/
            int bounds = CHUNK_BOUNDS;

            int x = (int)blockDataIndex.X;
            int y = (int)blockDataIndex.Y;
            int z = (int)blockDataIndex.Z;


            if ((BlockType)actionChunk._blockData[(short)blockDataIndex.X, (short)blockDataIndex.Y, (short)blockDataIndex.Z] == BlockType.LAMP_BLOCK)
            {
                ThreadPool.QueueUserWorkItem(new WaitCallback(delegate (object state)
                {
                    Console.WriteLine("Remove Lamp");

                    _lightHelper.SetBlockLight(blockSpace, new ColorVector(0, 0, 0), actionChunk, true, false);
                    PropagateBlockLight(blockSpace, BlockFace.UP, true, true);
                    PropagateBlockLight(blockSpace, BlockFace.DOWN, true, true);
                    PropagateBlockLight(blockSpace, BlockFace.EAST, true, true);
                    PropagateBlockLight(blockSpace, BlockFace.WEST, true, true);
                    PropagateBlockLight(blockSpace, BlockFace.NORTH, true, true);
                    PropagateBlockLight(blockSpace, BlockFace.SOUTH, true, true);

                    //Undtrack light source
                    _lightHelper.GetLightTrackingList().Remove(blockSpace);
                }));

            }

            //Update block data in chunk  
            actionChunk._blockData[(short)blockDataIndex.X, (short)blockDataIndex.Y, (short)blockDataIndex.Z] = (int)BlockType.AIR;

            AddBlockToChunk(blockSpace, BlockType.AIR, new(0, 0, 0));
            /*==================================================
            / Check for terrain holes after block removal
            *=================================================*/

            //==============================Positive Y (UP)==============================
            Vector3 u = new(blockSpace.X, (int)blockSpace.Y + 1, (int)blockSpace.Z);
            Vector3 blockDataIndexUP = GetChunkRelativeCoordinates(u);
            Chunk up = GetAndLoadGlobalChunkFromCoords(u);

            //If at the edge of a chunk, wrap to the next chunk
            if (blockDataIndexUP.Y + 1 == bounds)
            {
                blockDataIndexUP.Y = 0;
                up = GetAndLoadGlobalChunkFromCoords(new Vector3(u.X, u.Y + bounds, u.Z));
            }

            BlockType typeU = (BlockType)up._blockData[(short)blockDataIndexUP.X, (short)(blockDataIndexUP.Y + 1), (short)blockDataIndexUP.Z];
            //Only add block if its not air
            //Console.WriteLine("Up Block: " + typeU + " at " + u);
            if (typeU != BlockType.AIR)
            {
                //Console.WriteLine("Adding UP block");
                AddBlockToChunk(u, typeU, _lightHelper.GetBlockLight(u, BlockFace.UP, up));
            }
            //==============================Negative Y (DOWN)==============================
            Vector3 d = new(blockSpace.X, (int)blockSpace.Y - 1, (int)blockSpace.Z);
            Vector3 blockDataIndexDOWN = GetChunkRelativeCoordinates(d);
            Chunk down = GetAndLoadGlobalChunkFromCoords(d);

            //If at the edge of a chunk, wrap to the next chunk
            if (blockDataIndexDOWN.Y - 1 <= 0)
            {
                blockDataIndexDOWN.Y = bounds - 1;
                down = GetAndLoadGlobalChunkFromCoords(new Vector3(d.X, d.Y - bounds, d.Z));
            }

            BlockType typeD = (BlockType)down._blockData[(short)blockDataIndexDOWN.X, (short)(blockDataIndexDOWN.Y - 1), (short)blockDataIndexDOWN.Z];
            //Only add block if its not air
            //Console.WriteLine("Down Block: " + typeD + " at " + d);
            if (typeD != BlockType.AIR)
            {
                //Console.WriteLine("Adding DOWN block");
                AddBlockToChunk(d, typeD, _lightHelper.GetBlockLight(d, BlockFace.DOWN, down));
            }
            //==============================Positive X (EAST)==============================
            Vector3 e = new(blockSpace.X + 1, (int)blockSpace.Y, (int)blockSpace.Z);
            Vector3 blockDataIndexEAST = GetChunkRelativeCoordinates(e);
            Chunk east = GetAndLoadGlobalChunkFromCoords(e);

            //If at the edge of a chunk, wrap to the next chunk
            if (blockDataIndexEAST.X + 1 == bounds)
            {
                blockDataIndexEAST.X = 0;
                east = GetAndLoadGlobalChunkFromCoords(new Vector3(e.X + bounds, e.Y, e.Z));
            }

            BlockType typeE = (BlockType)east._blockData[(short)(blockDataIndexEAST.X + 1), (short)blockDataIndexEAST.Y, (short)blockDataIndexEAST.Z];
            //Only add block if its not air
            //Console.WriteLine("East Block: " + typeE + " at " + e);
            if (typeE != BlockType.AIR)
            {
                //Console.WriteLine("Adding EAST block");
                AddBlockToChunk(e, typeE, _lightHelper.GetBlockLight(e, BlockFace.EAST, east));
            }

            //==============================Negative X (WEST)==============================
            Vector3 w = new(blockSpace.X - 1, (int)blockSpace.Y, (int)blockSpace.Z);
            Vector3 blockDataIndexWEST = GetChunkRelativeCoordinates(w);
            Chunk west = GetAndLoadGlobalChunkFromCoords(w);

            //If at the edge of a chunk, wrap to the next chunk
            if (blockDataIndexWEST.X - 1 <= 0)
            {
                blockDataIndexWEST.X = bounds - 1;
                west = GetAndLoadGlobalChunkFromCoords(new Vector3(w.X - bounds, w.Y, w.Z));
            }

            BlockType typeW = (BlockType)west._blockData[(short)(blockDataIndexWEST.X - 1), (short)blockDataIndexWEST.Y, (short)blockDataIndexWEST.Z];
            //Only add block if its not air
            //Console.WriteLine("West Block: " + typeW + " at " + w);
            if (typeW != BlockType.AIR)
            {
                //Console.WriteLine("Adding WEST block");
                AddBlockToChunk(w, typeW, _lightHelper.GetBlockLight(w, BlockFace.WEST, west));
            }

            //==============================Positive Z (NORTH)==============================
            Vector3 n = new(blockSpace.X, (int)blockSpace.Y, (int)blockSpace.Z + 1);
            Vector3 blockDataIndexNORTH = GetChunkRelativeCoordinates(n);
            Chunk north = GetAndLoadGlobalChunkFromCoords(n);

            //If at the edge of a chunk, wrap to the next chunk
            if (blockDataIndexNORTH.Z + 1 == bounds)
            {
                blockDataIndexNORTH.Z = 0;
                north = GetAndLoadGlobalChunkFromCoords(new Vector3(n.X, n.Y, n.Z + bounds));
            }

            BlockType typeN = (BlockType)north._blockData[(short)blockDataIndexNORTH.X, (short)blockDataIndexNORTH.Y, (short)(blockDataIndexNORTH.Z + 1)];
            //Only add block if its not air
            //Console.WriteLine("North Block: " + typeN + " at " + n);
            if (typeN != BlockType.AIR)
            {
                //Console.WriteLine("Adding NORTH block");
                AddBlockToChunk(n, typeN, _lightHelper.GetBlockLight(n, BlockFace.NORTH, north));
            }
            //==============================Negative Z (SOUTH)==============================
            Vector3 s = new(blockSpace.X, (int)blockSpace.Y, (int)blockSpace.Z - 1);
            Vector3 blockDataIndexSOUTH = GetChunkRelativeCoordinates(s);
            Chunk south = GetAndLoadGlobalChunkFromCoords(s);

            //If at the edge of a chunk, wrap to the next chunk
            if (blockDataIndexSOUTH.Z - 1 <= 0)
            {
                blockDataIndexSOUTH.Z = bounds - 1;
                south = GetAndLoadGlobalChunkFromCoords(new Vector3(s.X, s.Y, s.Z - bounds));
            }

            BlockType typeS = (BlockType)south._blockData[(short)blockDataIndexSOUTH.X, (short)blockDataIndexSOUTH.Y, (short)(blockDataIndexSOUTH.Z - 1)];

            //Only add block if its not air
            //Console.WriteLine("South Block: " + typeS + " at " + s);
            if (typeS != BlockType.AIR)
            {
                //Console.WriteLine("Adding SOUTH block");
                AddBlockToChunk(s, typeS, _lightHelper.GetBlockLight(s, BlockFace.SOUTH, south));
            }
        }

        /// <summary>
        /// Gets the dictionary of regions loaded into memory.
        /// </summary>
        /// <returns>A dictionary containing the visible regions.</returns>
        public Dictionary<string, Region> GetVisibleRegions()
        {
            return VisibleRegions;
        }
        //================================================================================================
        //============================= Light Propagation and Depropagation ==============================
        //================================================================================================
        // Light depropagation does not currently work with multiple light sources in range of eachother. Need to fix

        /// <summary>
        /// Propagates or removes emissive block lighting from a starting location using a breadth-first search (BFS) traversal.
        /// Gathers nearby tracked light sources within the configured spread radius and updates affected blocks accordingly.
        /// </summary>
        /// <param name="location">
        /// The world-space position of the block where light propagation or depropagation begins.
        /// </param>
        /// <param name="faceDir">
        /// The block face direction used when reading and applying light values.
        /// </param>
        /// <param name="depropagate">
        /// If <see langword="false"/>, propagates light outward from the source block.
        /// If <see langword="true"/>, removes previously propagated light values.
        /// </param>
        /// <param name="colorOverride">
        /// Determines whether existing light color values should be overridden during propagation updates.
        /// </param>
        /// <remarks>
        /// This method:
        /// <list _type="bullet">
        /// <item>
        /// Collects nearby tracked emissive light sources that fall within the maximum light spread radius.
        /// </item>
        /// <item>
        /// Uses a BFS queue to process light updates incrementally.
        /// </item>
        /// <item>
        /// Executes propagation when the origin block contains light.
        /// </item>
        /// <item>
        /// Executes depropagation to remove light influence from affected nodes.
        /// </item>
        /// <item>
        /// Clears the visited node cache after processing completes.
        /// </item>
        /// </list>
        /// </remarks>
        public void PropagateBlockLight(Vector3 location, BlockFace faceDir, bool depropagate, bool colorOverride)
        {
            int x = (int)location.X;
            int y = (int)location.Y;
            int z = (int)location.Z;

            //Get all light sources within the vicinity of the source we are propagating/depropagating
            Dictionary<Vector3, ColorVector> lightingArea = [];
            foreach (KeyValuePair<Vector3, ColorVector> light in _lightHelper.GetLightTrackingList())
            {
                if (Utils.GetVectorDistance(light.Key, location) <= _lightHelper.GetMaxLightSpread() && !lightingArea.ContainsKey(light.Key))
                {
                    lightingArea.Add(light.Key, light.Value);
                }
            }

            // Check the light level of the current node before propagating          
            ColorVector originLightLevel = _lightHelper.GetBlockLight(location, faceDir, GetAndLoadGlobalChunkFromCoords(location));
            if ((originLightLevel.Red > 0 || originLightLevel.Green > 0 || originLightLevel.Blue > 0) && !depropagate)
            {
                string index = $"{x}|{y}|{z}";
                EnqueueEmissiveLightNode(new(index, GetAndLoadGlobalChunkFromCoords(location)));
                while (!BFSEmissivePropagationQueue.IsEmpty)
                {
                    // Get a reference to the front node.
                    LightNode node = DequeueEmissiveLightNode();
                    PropagateLightNode(lightingArea, location, faceDir, node, depropagate, colorOverride);
                }
            }

            else if (depropagate)
            {
                string index = $"{x}|{y}|{z}";

                EnqueueEmissiveLightNode(new(index, GetAndLoadGlobalChunkFromCoords(location)));
                while (!BFSEmissivePropagationQueue.IsEmpty)
                {
                    LightNode node = DequeueEmissiveLightNode();
                    string currentIdx = node.Index;
                    int xLight = int.Parse(currentIdx.Split('|')[0]);
                    int yLight = int.Parse(currentIdx.Split('|')[1]);
                    int zLight = int.Parse(currentIdx.Split('|')[2]);

                    // Grab the 16 bit light level of the current node
                    // ???? RRRR GGGG BBBB
                    ColorVector lightLevel = _lightHelper.GetBlockLight(new Vector3(xLight, yLight, zLight), faceDir, node.Chunk);

                    DepropagateLightNode(lightingArea, location, faceDir, node, depropagate, colorOverride);
                }
            }
            visited.Clear();
        }



        /// <summary>
        /// Processes a single light node during emissive light propagation by evaluating all six adjacent block positions
        /// and attempting to spread light into neighboring blocks.
        /// Handles chunk transitions automatically when propagation crosses chunk boundaries.
        /// </summary>
        /// <param name="lightingArea">
        /// Collection of tracked light sources and their color values that may influence propagation results.
        /// </param>
        /// <param name="sourceLocation">
        /// The original world-space position where the propagation operation began.
        /// </param>
        /// <param name="faceDir">
        /// The block face direction used when reading and applying light values.
        /// </param>
        /// <param name="node">
        /// The current light node being processed by the BFS propagation queue.
        /// Contains the block index and owning chunk.
        /// </param>
        /// <param name="depropagate">
        /// Indicates whether the operation is removing light instead of spreading it.
        /// </param>
        /// <param name="colorOverride">
        /// Determines whether propagated light values should replace existing color information.
        /// </param>
        /// <remarks>
        /// This method:
        /// <list _type="bullet">
        /// <item>
        /// Retrieves the current block's light level.
        /// </item>
        /// <item>
        /// Evaluates neighboring blocks in all six cardinal directions (±X, ±Y, ±Z).
        /// </item>
        /// <item>
        /// Detects and loads adjacent chunks when propagation crosses chunk boundaries.
        /// </item>
        /// <item>
        /// Delegates neighbor processing and light application logic to <c>PropagateLightNodeHelper</c>.
        /// </item>
        /// </list>
        /// </remarks>
        private void PropagateLightNode(Dictionary<Vector3, ColorVector> lightingArea, Vector3 sourceLocation, BlockFace faceDir, LightNode node, bool depropagate, bool colorOverride)
        {
            Chunk chunk = node.Chunk;
            string currentIdx = node.Index;
            int xLight = int.Parse(currentIdx.Split('|')[0]);
            int yLight = int.Parse(currentIdx.Split('|')[1]);
            int zLight = int.Parse(currentIdx.Split('|')[2]);

            // Grab the 16 bit light level of the current node
            // ???? RRRR GGGG BBBB
            Vector3 baseLoc = new(xLight, yLight, zLight);
            ColorVector lightLevel = _lightHelper.GetBlockLight(baseLoc, faceDir, chunk);

            //======================================
            //          NEGATIVE X (WEST)
            //======================================

            //Chunk bounds check
            Vector3 loc1 = new(xLight - 1, yLight, zLight);
            Chunk xMinusOne = GetAndLoadGlobalChunkFromCoords(loc1);
            if (!chunk.Equals(xMinusOne))
                chunk = xMinusOne;


            PropagateLightNodeHelper(lightingArea, loc1, sourceLocation, faceDir, lightLevel, chunk, depropagate, colorOverride);

            //======================================
            //          POSITIVE X (EAST)
            //======================================

            //Chunk bounds check
            Vector3 loc2 = new(xLight + 1, yLight, zLight);
            Chunk xPlusOne = GetAndLoadGlobalChunkFromCoords(loc2);
            if (!chunk.Equals(xPlusOne))
                chunk = xPlusOne;

            PropagateLightNodeHelper(lightingArea, loc2, sourceLocation, faceDir, lightLevel, chunk, depropagate, colorOverride);

            //======================================
            //          NEGATIVE Y (DOWN)
            //======================================

            //Chunk bounds check
            Vector3 loc3 = new(xLight, yLight - 1, zLight);
            Chunk yMinusOne = GetAndLoadGlobalChunkFromCoords(loc3);
            if (!chunk.Equals(yMinusOne))
                chunk = yMinusOne;


            PropagateLightNodeHelper(lightingArea, loc3, sourceLocation, faceDir, lightLevel, chunk, depropagate, colorOverride);

            //======================================
            //          POSITIVE Y (UP)
            //======================================

            //Chunk bounds check
            Vector3 loc4 = new(xLight, yLight + 1, zLight);
            Chunk yPlusOne = GetAndLoadGlobalChunkFromCoords(loc4);
            if (!chunk.Equals(yPlusOne))
                chunk = yPlusOne;

            PropagateLightNodeHelper(lightingArea, loc4, sourceLocation, faceDir, lightLevel, chunk, depropagate, colorOverride);

            //======================================
            //          NEGATIVE Z (SOUTH)
            //======================================

            //Chunk bounds check
            Vector3 loc5 = new(xLight, yLight, zLight - 1);
            Chunk zMinusOne = GetAndLoadGlobalChunkFromCoords(loc5);
            if (!chunk.Equals(zMinusOne))
                chunk = zMinusOne;

            PropagateLightNodeHelper(lightingArea, loc5, sourceLocation, faceDir, lightLevel, chunk, depropagate, colorOverride);

            //======================================
            //          POSITIVE Z (NORTH)
            //======================================

            //Chunk bounds check
            Vector3 loc6 = new(xLight, yLight, zLight + 1);
            Chunk zPlusOne = GetAndLoadGlobalChunkFromCoords(loc6);
            if (!chunk.Equals(zPlusOne))
                chunk = zPlusOne;

            PropagateLightNodeHelper(lightingArea, loc6, sourceLocation, faceDir, lightLevel, chunk, depropagate, colorOverride);
        }


        /// <summary>
        /// Work in progress - Helper method for processing a neighboring block during light propagation or depropagation.
        /// </summary>
        /// <param name="lightingArea"></param>
        /// <param name="sourceLocation"></param>
        /// <param name="location"></param>
        /// <param name="faceDir"></param>
        /// <param name="nodeLightLevel"></param>
        /// <param name="chunk"></param>
        /// <param name="depropagate"></param>
        /// <param name="colorOverride"></param>
        private void DepropagationLightNodeHelper(Dictionary<Vector3, ColorVector> lightingArea, Vector3 sourceLocation, Vector3 location, BlockFace faceDir,
            ColorVector nodeLightLevel, Chunk chunk, bool depropagate, bool colorOverride)
        {
            bool wasUpdated = false;

            // distance from the depropagation source to this neighbor
            int distFromSource = Utils.GetVectorDistance(location, sourceLocation);

            if (_lightHelper.GetBlockLight(location, faceDir, chunk).Blue > 0 && !visited.Contains(location))
            {
                _lightHelper.SetBlueLight(location, faceDir, 0, chunk, true);
                wasUpdated = true;
            }

            if (_lightHelper.GetBlockLight(location, faceDir, chunk).Green > 0 && !visited.Contains(location))
            {
                _lightHelper.SetGreenLight(location, faceDir, 0, chunk, true);
                wasUpdated = true;
            }

            if (_lightHelper.GetBlockLight(location, faceDir, chunk).Red > 0 && !visited.Contains(location))
            {
                _lightHelper.SetRedLight(location, faceDir, 0, chunk, true);
                wasUpdated = true;
            }


            //// if (GetBlockLight(location, faceDir, chunk).Blue > 0 && !visited.Contains(location) && distFromSource <= lightSourceLevel.Blue)
            // {
            //     Dictionary<Vector3, int> contributingBlueLightValues = [];
            //     foreach (KeyValuePair<Vector3, ColorVector> light in lightingArea)
            //     {
            //         int value = Utils.GetVectorDistance(location, light.Key);
            //         if (value <= lightSourceLevel.Blue)
            //         {
            //             //If block face (location) is within the range of any light source, accumulate light
            //             contributingBlueLightValues.Add(light.Key, value);
            //         }
            //     }
            //
            //     //If more than one light source is lighting the area
            //     if (contributingBlueLightValues.Count > 1)
            //     {
            //         int cumulativeLight = contributingBlueLightValues.Values.Sum() - contributingBlueLightValues[sourceLocation];
            //
            //         //The light source being depropagated is brighter than the cumulative light at the location
            //         //Do not depropagate light source block faces
            //         if (lightSourceLevel.Blue > cumulativeLight && !lightingArea.ContainsKey(location))
            //         {
            //             SetBlueLight(location, faceDir, lightSourceLevel.Blue - cumulativeLight, chunk, false);
            //   
            //         }
            //         //The light source being depropagated less bright than the cumulative light at the location
            //         //Do not depropagate light source block faces
            //         else if (lightSourceLevel.Blue < cumulativeLight && !lightingArea.ContainsKey(location))
            //         {
            //             SetBlueLight(location, faceDir, lightSourceLevel.Blue - (cumulativeLight - lightSourceLevel.Blue), chunk, false);
            //
            //         }
            //
            //         //Light source level is equal to accumulated light and outside the light range so they cancel out
            //         else if (lightSourceLevel.Blue == cumulativeLight && contributingBlueLightValues[sourceLocation] >= lightSourceLevel.Blue)
            //         {
            //             SetBlueLight(location, faceDir, 0, chunk, false);
            //            
            //         }
            //     }
            //     else
            //     {
            //         SetBlueLight(location, faceDir, 0, chunk, colorOverride);
            //     }
            //     wasUpdated = true;
            //
            // }
            ////
            //// if (GetBlockLight(location, faceDir, chunk).Green > 0 && !visited.Contains(location) && distFromSource <= lightSourceLevel.Green)
            // {
            //     Dictionary<Vector3, int> contributingGreenLightValues = [];
            //     foreach (KeyValuePair<Vector3, ColorVector> light in lightingArea)
            //     {
            //         int value = Utils.GetVectorDistance(location, light.Key);
            //         if (value <= lightSourceLevel.Green)
            //         {
            //             //If block face (location) is within the range of any light source, accumulate light
            //             contributingGreenLightValues.Add(light.Key, value);
            //         }
            //     }
            //
            //     //If more than one light source is lighting the area
            //     if (contributingGreenLightValues.Count > 1)
            //     {
            //         int cumulativeLight = contributingGreenLightValues.Values.Sum() - contributingGreenLightValues[sourceLocation];
            //
            //         //The light source being depropagated is brighter than the cumulative light at the location
            //         //Do not depropagate light source block faces
            //         if (lightSourceLevel.Green > cumulativeLight && !lightingArea.ContainsKey(location))
            //         {
            //             SetGreenLight(location, faceDir, lightSourceLevel.Green - cumulativeLight, chunk, false);
            //
            //         }
            //         //The light source being depropagated less bright than the cumulative light at the location
            //         //Do not depropagate light source block faces
            //         else if (lightSourceLevel.Green < cumulativeLight && !lightingArea.ContainsKey(location))
            //         {
            //             SetGreenLight(location, faceDir, lightSourceLevel.Green - (cumulativeLight - lightSourceLevel.Green), chunk, false);
            //
            //         }
            //
            //         //Light source level is equal to accumulated light and outside the light range so they cancel out
            //         else if (lightSourceLevel.Green == cumulativeLight && contributingGreenLightValues[sourceLocation] >= lightSourceLevel.Green)
            //         {
            //             SetGreenLight(location, faceDir, 0, chunk, false);
            //
            //         }
            //     }
            //     else
            //     {
            //         SetGreenLight(location, faceDir, 0, chunk, colorOverride);
            //     }
            // }
            //
            // if (GetBlockLight(location, faceDir, chunk).Red > 0 && !visited.Contains(location) && distFromSource <= lightSourceLevel.Red)
            // {
            //     Dictionary<Vector3, int> contributingRedLightValues = [];
            //     foreach (KeyValuePair<Vector3, ColorVector> light in lightingArea)
            //     {
            //         int value = Utils.GetVectorDistance(location, light.Key);
            //
            //         if (value <= lightSourceLevel.Red)
            //         {
            //             //If block face (location) is within the range of any light source, accumulate light
            //             contributingRedLightValues.Add(light.Key, value);
            //         }
            //     }
            //
            //     //If more than one light source is lighting the area
            //     if (contributingRedLightValues.Count > 1)
            //     {
            //         int cumulativeLight = contributingRedLightValues.Values.Sum() - contributingRedLightValues[sourceLocation];
            //
            //         //The light source being depropagated is brighter than the cumulative light at the location
            //         //Do not depropagate light source block faces
            //         if (lightSourceLevel.Red > cumulativeLight && !lightingArea.ContainsKey(location))
            //         {
            //             SetRedLight(location, faceDir, lightSourceLevel.Red - cumulativeLight, chunk, false);
            //
            //         }
            //         //The light source being depropagated less bright than the cumulative light at the location
            //         //Do not depropagate light source block faces
            //         else if (lightSourceLevel.Red < cumulativeLight && !lightingArea.ContainsKey(location))
            //         {
            //             SetRedLight(location, faceDir, lightSourceLevel.Red - (cumulativeLight - lightSourceLevel.Red), chunk, false);
            //
            //         }
            //
            //         //Light source level is equal to accumulated light and outside the light range so they cancel out
            //         else if (lightSourceLevel.Red == cumulativeLight && contributingRedLightValues[sourceLocation] >= lightSourceLevel.Red)
            //         {
            //             SetRedLight(location, faceDir, 0, chunk, false);
            //
            //         }
            //     }
            //     else 
            //     {
            //         SetRedLight(location, faceDir, 0, chunk, true);
            //
            //     }
            // }

            if (wasUpdated)
            {
                visited.Add(location);
                string idx = $"{location.X}|{location.Y}|{location.Z}";
                EnqueueEmissiveLightNode(new(idx, chunk));

            }
        }

        /// <summary>
        /// Evaluates a neighboring block during emissive light propagation and updates its RGB light values
        /// if the propagated light is stronger than the block's existing light levels.
        /// Queues updated blocks for continued breadth-first propagation.
        /// </summary>
        /// <param name="lightingArea">
        /// Collection of tracked light source locations and their associated color intensity values.
        /// </param>
        /// <param name="location">
        /// The world-space position of the neighboring block being evaluated.
        /// </param>
        /// <param name="sourceLocation">
        /// The original light source position used to calculate attenuation over distance.
        /// </param>
        /// <param name="faceDir">
        /// The block face direction used when reading and writing light values.
        /// </param>
        /// <param name="lightLevel">
        /// The current propagated RGB light level from the parent node.
        /// </param>
        /// <param name="chunk">
        /// The chunk containing the target block.
        /// </param>
        /// <param name="depropagate">
        /// Indicates whether the overall operation is performing light removal instead of propagation.
        /// </param>
        /// <param name="colorOverride">
        /// Determines whether existing light color values should be overwritten.
        /// </param>
        /// <remarks>
        /// This method:
        /// <list _type="bullet">
        /// <item>
        /// Calculates attenuation based on distance from the original light source.
        /// </item>
        /// <item>
        /// Compares propagated RGB values against existing block light levels.
        /// </item>
        /// <item>
        /// Updates only color channels that would increase the effective light intensity.
        /// </item>
        /// <item>
        /// Enqueues modified blocks so propagation continues outward recursively via BFS.
        /// </item>
        /// </list>
        /// </remarks>
        private void PropagateLightNodeHelper(Dictionary<Vector3, ColorVector> lightingArea, Vector3 location, Vector3 sourceLocation, BlockFace faceDir, ColorVector lightLevel, Chunk chunk, bool depropagate, bool colorOverride)
        {
            bool wasUpdated = false;


            // distance from the propagation source to this neighbor   
            int distFromSource = Utils.GetVectorDistance(location, sourceLocation);

            ColorVector lightSourceLevel = lightingArea[sourceLocation];

            if ((_lightHelper.GetRedLight(location, faceDir, chunk) + 1) < lightLevel.Red)
            {
                _lightHelper.SetRedLight(location, faceDir, lightSourceLevel.Red - distFromSource, chunk, colorOverride);
                wasUpdated = true;
            }

            if ((_lightHelper.GetGreenLight(location, faceDir, chunk) + 1) < lightLevel.Green)
            {
                _lightHelper.SetGreenLight(location, faceDir, lightSourceLevel.Green - distFromSource, chunk, colorOverride);
                wasUpdated = true;
            }

            if ((_lightHelper.GetBlueLight(location, faceDir, chunk) + 1) < lightLevel.Blue)
            {
                // Set blue light level
                _lightHelper.SetBlueLight(location, faceDir, lightSourceLevel.Blue - distFromSource, chunk, colorOverride);
                wasUpdated = true;
            }

            if (wasUpdated)
            {
                // Construct index
                string idx = $"{location.X}|{location.Y}|{location.Z}";
                // Emplace new node to queue.

                EnqueueEmissiveLightNode(new(idx, chunk));

            }
        }


        /// <summary>
        /// Processes a single light node during emissive light removal by evaluating all six adjacent block positions
        /// and removing propagated light influence from neighboring blocks as needed.
        /// Handles chunk transitions automatically when traversal crosses chunk boundaries.
        /// </summary>
        /// <param name="lightingArea">
        /// Collection of tracked light sources and their color values used to determine remaining light influence.
        /// </param>
        /// <param name="sourceLocation">
        /// The original world-space position where depropagation began.
        /// </param>
        /// <param name="faceDir">
        /// The block face direction used when reading and updating light values.
        /// </param>
        /// <param name="node">
        /// The current light node being processed by the BFS depropagation queue.
        /// Contains the block index and owning chunk.
        /// </param>
        /// <param name="depropagate">
        /// Indicates that light values should be removed rather than propagated.
        /// </param>
        /// <param name="colorOverride">
        /// Determines whether existing light color values should be overwritten during updates.
        /// </param>
        /// <remarks>
        /// This method:
        /// <list _type="bullet">
        /// <item>
        /// Retrieves the current node's RGB light values.
        /// </item>
        /// <item>
        /// Evaluates neighboring blocks in all six cardinal directions (±X, ±Y, ±Z).
        /// </item>
        /// <item>
        /// Resolves chunk transitions when neighboring blocks exist outside the current chunk.
        /// </item>
        /// <item>
        /// Delegates removal and cleanup logic to <c>DepropagationLightNodeHelper</c>.
        /// </item>
        /// <item>
        /// Supports breadth-first traversal to progressively remove light influence.
        /// </item>
        /// </list>
        /// </remarks>
        private void DepropagateLightNode(Dictionary<Vector3, ColorVector> lightingArea, Vector3 sourceLocation, BlockFace faceDir, LightNode node, bool depropagate, bool colorOverride)
        {
            Chunk chunk = node.Chunk;
            string currentIdx = node.Index;
            int xLight = int.Parse(currentIdx.Split('|')[0]);
            int yLight = int.Parse(currentIdx.Split('|')[1]);
            int zLight = int.Parse(currentIdx.Split('|')[2]);

            // Grab the 16 bit light level of the current node
            // ???? RRRR GGGG BBBB
            ColorVector nodeLightLevel = _lightHelper.GetBlockLight(new(xLight, yLight, zLight), faceDir, chunk);

            //======================================
            //          NEGATIVE X (WEST)
            //======================================

            //Chunk bounds check
            Vector3 loc1 = new(xLight - 1, yLight, zLight);
            Chunk xMinusOne = GetAndLoadGlobalChunkFromCoords(loc1);
            if (!chunk.Equals(xMinusOne))
                chunk = xMinusOne;

            DepropagationLightNodeHelper(lightingArea, sourceLocation, loc1, faceDir, nodeLightLevel, chunk, depropagate, colorOverride);

            //======================================
            //          POSITIVE X (EAST)
            //======================================

            //Chunk bounds check
            Vector3 loc2 = new(xLight + 1, yLight, zLight);
            Chunk xPlusOne = GetAndLoadGlobalChunkFromCoords(loc2);
            if (!chunk.Equals(xPlusOne))
                chunk = xPlusOne;

            DepropagationLightNodeHelper(lightingArea, sourceLocation, loc2, faceDir, nodeLightLevel, chunk, depropagate, colorOverride);

            //======================================
            //          NEGATIVE Y (DOWN)
            //======================================

            //Chunk bounds check
            Vector3 loc3 = new(xLight, yLight - 1, zLight);
            Chunk yMinusOne = GetAndLoadGlobalChunkFromCoords(loc3);
            if (!chunk.Equals(yMinusOne))
                chunk = yMinusOne;

            DepropagationLightNodeHelper(lightingArea, sourceLocation, loc3, faceDir, nodeLightLevel, chunk, depropagate, colorOverride);

            //======================================
            //          POSITIVE Y (UP)
            //======================================

            //Chunk bounds check
            Vector3 loc4 = new(xLight, yLight + 1, zLight);
            Chunk yPlusOne = GetAndLoadGlobalChunkFromCoords(loc4);
            if (!chunk.Equals(yPlusOne))
                chunk = yPlusOne;

            DepropagationLightNodeHelper(lightingArea, sourceLocation, loc4, faceDir, nodeLightLevel, chunk, depropagate, colorOverride);

            //======================================
            //          NEGATIVE Z (SOUTH)
            //======================================

            //Chunk bounds check
            Vector3 loc5 = new(xLight, yLight, zLight - 1);
            Chunk zMinusOne = GetAndLoadGlobalChunkFromCoords(loc5);
            if (!chunk.Equals(zMinusOne))
                chunk = zMinusOne;

            DepropagationLightNodeHelper(lightingArea, sourceLocation, loc5, faceDir, nodeLightLevel, chunk, depropagate, colorOverride);

            //======================================
            //          POSITIVE Z (NORTH)
            //======================================

            //Chunk bounds check
            Vector3 loc6 = new(xLight, yLight, zLight + 1);
            Chunk zPlusOne = GetAndLoadGlobalChunkFromCoords(loc6);
            if (!chunk.Equals(zPlusOne))
                chunk = zPlusOne;
            DepropagationLightNodeHelper(lightingArea, sourceLocation, loc6, faceDir, nodeLightLevel, chunk, depropagate, colorOverride);
        }
    }
}

