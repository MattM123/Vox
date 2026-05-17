using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Drawing.Printing;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using MessagePack;
using OpenTK.Mathematics;
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
        private readonly List<Region> _regions = [];
        private static List<Vector3> visited = [];

        private readonly ISSBOManager _ssboManager;
        private readonly ILightHelper _lightHelper;

        private static string worldDir = "";
        public static readonly int WORLD_HEIGHT = 500;
        private static int RENDER_DISTANCE = 3;
        public static readonly int REGION_BOUNDS = 512;
        private readonly int CHUNK_BOUNDS = 32;
        public static long WORLD_SEED;
        private static object chunkLock = new();

        private readonly ConcurrentQueue<LightNode> BFSEmissivePropagationQueue;
        /**
         * The highest level object representation of a world. The RegionManager
         * contains an in-memory list of regions that are currently within
         * the players render distance. This region list is constantly updated each
         * frame and is used for reading regions from file and writing regions to file.
         *
         * @param path The path of this worlds directory
         */
        public RegionManager(string path, ISSBOManager ssboManager, ILightHelper lightHelper)
        {
            _lightHelper = lightHelper ?? throw new ShaderException(nameof(lightHelper) + " is null in RegionManager");
            _ssboManager = ssboManager ?? throw new ShaderException(nameof(ssboManager) + " is null in RegionManager");

            BFSEmissivePropagationQueue = new(new Queue<LightNode>((int)Math.Pow(CHUNK_BOUNDS, 3)));
            worldDir = path;
            WORLD_SEED = path.GetHashCode();

            byte[] buffer = new byte[8];
            RandomNumberGenerator.Fill(buffer); // Fills the buffer with random bytes
            WORLD_SEED = BitConverter.ToInt64(buffer, 0);

            SetWorldDir(path);
        }

        public void SetWorldDir(string path)
        {
            worldDir = path;
            if (Directory.Exists(path))
                Directory.CreateDirectory(Path.Combine(worldDir, "regions"));
        }
        public int GetWorldHeight() { return WORLD_HEIGHT; }
        public int GetChunkBounds() { return CHUNK_BOUNDS; }
        private void EnqueueEmissiveLightNode(LightNode node)
        {
            BFSEmissivePropagationQueue.Enqueue(node);
        }

        private LightNode DequeueEmissiveLightNode()
        {
            if (BFSEmissivePropagationQueue.TryDequeue(out LightNode node))
                return node;
            else
                return new LightNode("", null);
        }

        /**
        * Retrieves the Y value for any given x,z column in any chunk
        * @param x coordinate of column
        * @param z coordinate of column
        * @return Returns the noise value which is scaled between 0 and CHUNK_HEIGHT
        */
        public short GetGlobalHeightMapValue(int x, int z)
        {
            long seed;
            if (Window.IsMenuRendered())
                seed = Window.GetMenuSeed();
            else
                seed = WORLD_SEED;

            //Affects height of terrain. A higher value will result in lower, smoother terrain while a lower value will result in
            // a rougher, raised terrain
            float var1 = 100;

            //Affects coalescence of terrain. A higher value will result in more condensed, sharp peaks and a lower value will result in
            //more smooth, spread out hills.
            double var2 = 0.001;

            float f = 1 * OpenSimplex2.Noise2(seed, x * var2, z * var2) / (var1 + 2) //Noise Octave 1
                    + (float)(0.5 * OpenSimplex2.Noise2(seed, x * (var2 * 2), z * (var2 * 2)) / (var1 + 4)) //Noise Octave 2
                    + (float)(0.25 * OpenSimplex2.Noise2(seed, x * (var2 * 2), z * (var2 * 2)) / (var1 + 6)); //Noise Octave 3

            //Normalized teh noise value
            float noise = (f * 0.5f) + 0.5f;

            return (short)(noise * WORLD_HEIGHT);

        }

        /**
         * Given a 3D point in world space, convert to chunk relative coordinates within
         * the range of the chunks bounds.
         **/
        public Vector3i GetChunkRelativeCoordinates(Vector3 v)
        {
            return new Vector3i(
                (int)Math.Abs(v.X % CHUNK_BOUNDS),
                (int)Math.Abs(v.Y % CHUNK_BOUNDS),
                (int)Math.Abs(v.Z % CHUNK_BOUNDS)
            );
        }

        /**
        * Given an x, y, and z value for a blocks location, generates the blocks type using
        * Simplex noise.
        * @param x coordinate of block
        * @param y coordinate of block
        * @param z coordinate of block
        * @return Returns the noise value which is an enum BlockType scaled between 0 and the number of block types.
        */
        public BlockType GetGlobalBlockType(int x, int y, int z)
        {
            long seed;
            if (Window.IsMenuRendered())
                seed = Window.GetMenuSeed();
            else
                seed = WORLD_SEED;

            //Generate noise and normalize to 0-1 from -1-1
            float noise = (OpenSimplex2.Noise3_ImproveXZ(seed, x, y, z) * 0.5f) + 0.5f;

            //how many different types of blocks are there?
            int blocktypes = Enum.GetValues(typeof(BlockType)).Length;

            // Multiply normalized noise to get an index
            int index = (int)(noise * blocktypes);

            // Clamp to exlude "non-blocks"
            index = Math.Clamp(index, 1, blocktypes - 4);

            //Manually define ranges for certain block types like this:
            //if (noise < 0.3f)
            //    return BlockType.GRASS_BLOCK;
            //else if (noise < 0.6f)
            //    return BlockType.DIRT_BLOCK;
            //    return BlockType.DIRT_BLOCK;
            //else
            //    return BlockType.STONE_BLOCK;

            // Convert index to BlockType
            return (BlockType)index;

        }
        private BlockType GetGlobalBlockType(Vector3 blockSpace)
        {
            int x = (int)blockSpace.X;
            int y = (int)blockSpace.Y;
            int z = (int)blockSpace.Z;

            long seed;
            if (Window.IsMenuRendered())
                seed = Window.GetMenuSeed();
            else
                seed = WORLD_SEED;

            //Generate noise and normalize to 0-1 from -1-1
            float noise = (OpenSimplex2.Noise3_ImproveXZ(seed, x, y, z) * 0.5f) + 0.5f;

            //how many different types of blocks are there?
            int blocktypes = Enum.GetValues(typeof(BlockType)).Length;

            // Multiply normalized noise to get an index
            int index = (int)(noise * blocktypes);

            // Clamp to exlude "non-blocks"
            index = Math.Clamp(index, 1, blocktypes - 4);

            //Manually define ranges for certain block types like this:
            //if (noise < 0.3f)
            //    return BlockType.GRASS_BLOCK;
            //else if (noise < 0.6f)
            //    return BlockType.DIRT_BLOCK;
            //    return BlockType.DIRT_BLOCK;
            //else
            //    return BlockType.STONE_BLOCK;

            // Convert index to BlockType
            return (BlockType)index;

        }
        /**
         * Gets the current memory usage for the visibile chunks 
         * surrounding the player withinin render distance
         */
        public int PollChunkMemory()
        {
            int count = 0;
            foreach (KeyValuePair<string, Region> pair in VisibleRegions)
                count += pair.Value.chunks.Count;
            return count;
        }

        public int GetRenderDistance() { return RENDER_DISTANCE; }
        /**
         * Removes a region from the visible regions once a player leaves a region and
         * their render distance no longer overlaps it. Writes region to file in the process
         * effectively saving the regions state for future use.
         *
         * @param r The region to leave
         */
        public void LeaveRegion(string rIndex)
        {
            Logger.Info($"Leaving {VisibleRegions[rIndex]}");
            WriteRegion(rIndex);
            VisibleRegions.Remove(rIndex);

        }

        /**
         * Generates or loads an already generated region from filesystem when the players
         * render distance intersects with the regions bounds.
         */
        public Region EnterRegion(string rIndex)
        {
            //If region is already visible
            if (VisibleRegions.TryGetValue(rIndex, out Region? value))
                return value;

            //Gets region from files if it's written to file but not visible
            Region region = TryGetRegionFromFile(rIndex);
            VisibleRegions.Add(rIndex, region);
            return region;

        }

        /**
         * Write new or existing region to file
         */
        private void WriteRegion(string rIndex)
        {
            Region r = VisibleRegions[rIndex];

            string path = Path.Combine(worldDir, "regions", $"{r.GetBounds().X}.{r.GetBounds().Y}.dat");

            byte[] serializedRegion = MessagePackSerializer.Serialize(r);

            File.WriteAllBytes(path, serializedRegion);
        }

        /**
         * Read existing region from file
         */
        private Region? ReadRegion(string path)
        {
            byte[] serializedData = File.ReadAllBytes(path);

            // Deserialize the byte array back into an object
            Region region = MessagePackSerializer.Deserialize<Region>(serializedData);
            Logger.Info($"Reading Region from file {region}");

            return region;

        }

        /**
         * Attempts to get a region from file.
         * Returns an empty region to write later if it theres no file to read.
         */
        public Region TryGetRegionFromFile(string rIndex)
        {
            int[] index = [.. rIndex.Split('|').Select(int.Parse)];
            string path = Path.Combine(worldDir, "regions", index[0] + "." + index[1] + ".dat");


            if (!VisibleRegions.TryGetValue(rIndex, out Region? value) && !File.Exists(path))
            {
                return new Region(this, index[0], index[1]);
            }
            else if (VisibleRegions.TryGetValue(rIndex, out Region? val) && !File.Exists(path))
            {
                return val;
            }
            else if (!VisibleRegions.TryGetValue(rIndex, out Region? v) && File.Exists(path))
            {
                return ReadRegion(path)!;
            }
            else
            {
                return VisibleRegions[rIndex];
            }
        }

        /**
         * Get new empty region given any x,z coordinate pair.
         */
        public Region GetGlobalRegionFromChunkCoords(int x, int z)
        {
            Region returnRegion = null;

            int xLowerLimit = ((x / REGION_BOUNDS) * REGION_BOUNDS);
            int xUpperLimit;
            if (x < 0)
                xUpperLimit = xLowerLimit - REGION_BOUNDS;
            else
                xUpperLimit = xLowerLimit + REGION_BOUNDS;


            int zLowerLimit = ((z / REGION_BOUNDS) * REGION_BOUNDS);
            int zUpperLimit;
            if (z < 0)
                zUpperLimit = zLowerLimit - REGION_BOUNDS;
            else
                zUpperLimit = zLowerLimit + REGION_BOUNDS;

            //new empty region used for coordinate comparisons
            return new Region(this, xUpperLimit, zUpperLimit);
        }

        /**
         * Get any chunk given a x,y,z coordinate trio and load it into memory
         */
        public Chunk GetAndLoadGlobalChunkFromCoords(int x, int y, int z)
        {

            string playerChunkIdx =
                $"{Math.Floor((float)x / CHUNK_BOUNDS) * CHUNK_BOUNDS}|" +
                $"{Math.Floor((float)y / CHUNK_BOUNDS) * CHUNK_BOUNDS}|" +
                $"{Math.Floor((float)z / CHUNK_BOUNDS) * CHUNK_BOUNDS}";

            int[] chunkIdxArray = [.. playerChunkIdx.Split('|').Select(int.Parse)];
            string playerRegionIdx = GetRegionIndex(chunkIdxArray[0], chunkIdxArray[2]);
            Region r = EnterRegion(playerRegionIdx);

            //If chunk has not yet been generated, generate it
            if (!r.chunks.TryGetValue(playerChunkIdx, out Chunk? value))
            {
                value = new Chunk(_ssboManager, this).Initialize(chunkIdxArray[0], chunkIdxArray[1], chunkIdxArray[2]);
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

            string regionIdx = GetRegionIndex(chunkIdxArray[0], chunkIdxArray[2]);
            Region r = EnterRegion(regionIdx);

            if (!r.chunks.TryGetValue(chunkIdx, out Chunk? value))
            {
                value = new Chunk(_ssboManager, this).Initialize(chunkIdxArray[0], chunkIdxArray[1], chunkIdxArray[2]);
                r.chunks.Add(chunkIdx, value);
            }
            return value;
        }

        public string GetRegionIndex(int chunkX, int chunkZ)
        {
            Rectangle bounds = GetGlobalRegionFromChunkCoords(chunkX, chunkZ).GetBounds();
            return $"{bounds.X}|{bounds.Y}";
        }

        /**
         * Given an x,y,z coordinate trio representing a block location,
         * get the chunk that block is supposed to be in and add it to the chunk
         */
        public void AddBlockToChunk(Vector3 blockSpace, BlockType type, ColorVector blockLight)
        {

            //The chunk that is added to
            Chunk actionChunk = GetAndLoadGlobalChunkFromCoords(blockSpace);

            //The block data index within that chunk to modify
            Vector3i blockDataIndex = GetChunkRelativeCoordinates(blockSpace);

            //Update block data in chunk 
            actionChunk.blockData[(short)blockDataIndex.X, (short)blockDataIndex.Y, (short)blockDataIndex.Z] = (short)type;

            /*=============================================
             * Add a single block to the SSBO for rendering
             *=============================================*/

            int bounds = CHUNK_BOUNDS;

            int x = blockDataIndex.X;
            int y = blockDataIndex.Y;
            int z = blockDataIndex.Z;

            //Positive Y (UP)
            if (y + 1 >= bounds || actionChunk.blockData[(short)x, (short)(y + 1), (short)z] == 0)
            {
                int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.UP);
                BlockFace faceDir = BlockFace.UP;
                actionChunk.AddOrUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing UP Face");
            }
            // Positive X (EAST)
            if (x + 1 >= bounds || actionChunk.blockData[(short)(x + 1), (short)y, (short)z] == 0)
            {
                int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.EAST);
                BlockFace faceDir = BlockFace.EAST;
                actionChunk.AddOrUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing EAST Face");
            }


            //Negative X (WEST)
            if (x - 1 < 0 || actionChunk.blockData[(short)(x - 1), (short)y, (short)z] == 0)
            {
                int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.WEST);
                BlockFace faceDir = BlockFace.WEST;
                actionChunk.AddOrUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing WEST Face");
            }

            //Negative Y (DOWN)
            if (y - 1 < 0 || actionChunk.blockData[(short)x, (short)(y - 1), (short)z] == 0)
            //If player is below the blocks Y level, render the bottom face
            // && Window.GetPlayer().GetPosition().Y < y)
            {
                int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.DOWN);
                BlockFace faceDir = BlockFace.DOWN;
                actionChunk.AddOrUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing DOWN Face");
            }

            //Positive Z (NORTH)
            if (z + 1 >= bounds || actionChunk.blockData[(short)x, (short)(y), (short)(z + 1)] == 0)
            {
                int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.NORTH);
                BlockFace faceDir = BlockFace.NORTH;
                actionChunk.AddOrUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing NORTH Face");
            }

            //Negative Z (SOUTH)
            if (z - 1 < 0 || actionChunk.blockData[(short)x, (short)(y), (short)(z - 1)] == 0)
            {
                int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.SOUTH);
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

        /**
         * Given an x,y,z coordinate trio representing a block location,
         * get the chunk that block is supposed to be in and add it to the chunk
         */
        public void AddBlockToChunk(Vector3 blockSpace, BlockType type, ColorVector blockLight, bool colorOverride)
        {

            //The chunk that is added to
            Chunk actionChunk = GetAndLoadGlobalChunkFromCoords(blockSpace);

            //The block data index within that chunk to modify
            Vector3i blockDataIndex = GetChunkRelativeCoordinates(blockSpace);

            //Update block data in chunk 
            actionChunk.blockData[(short)blockDataIndex.X, (short)blockDataIndex.Y, (short)blockDataIndex.Z] = (short)type;

            /*=============================================
             * Add a single block to the SSBO for rendering
             *=============================================*/

            int bounds = CHUNK_BOUNDS;

            int x = blockDataIndex.X;
            int y = blockDataIndex.Y;
            int z = blockDataIndex.Z;

            //Positive Y (UP)
            if (y + 1 >= bounds || actionChunk.blockData[(short)x, (short)(y + 1), (short)z] == 0)
            {
                int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.UP);
                BlockFace faceDir = BlockFace.UP;
                actionChunk.AddOrUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing UP Face");
            }
            // Positive X (EAST)
            if (x + 1 >= bounds || actionChunk.blockData[(short)(x + 1), (short)y, (short)z] == 0)
            {
                int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.EAST);
                BlockFace faceDir = BlockFace.EAST;
                actionChunk.AddOrUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing EAST Face");
            }


            //Negative X (WEST)
            if (x - 1 < 0 || actionChunk.blockData[(short)(x - 1), (short)y, (short)z] == 0)
            {
                int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.WEST);
                BlockFace faceDir = BlockFace.WEST;
                actionChunk.AddOrUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing WEST Face");
            }

            //Negative Y (DOWN)
            if (y - 1 < 0 || actionChunk.blockData[(short)x, (short)(y - 1), (short)z] == 0)
            //If player is below the blocks Y level, render the bottom face
            // && Window.GetPlayer().GetPosition().Y < y)
            {
                int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.DOWN);
                BlockFace faceDir = BlockFace.DOWN;
                actionChunk.AddOrUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing DOWN Face");
            }

            //Positive Z (NORTH)
            if (z + 1 >= bounds || actionChunk.blockData[(short)x, (short)(y), (short)(z + 1)] == 0)
            {
                int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.NORTH);
                BlockFace faceDir = BlockFace.NORTH;
                actionChunk.AddOrUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing NORTH Face");
            }

            //Negative Z (SOUTH)
            if (z - 1 < 0 || actionChunk.blockData[(short)x, (short)(y), (short)(z - 1)] == 0)
            {
                int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.SOUTH);
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

        /**
         * Given an x,y,z coordinate trio representing a block location,
         * get the chunk that block is supposed to be in and remove the block
         * if it is there
         */
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


            if ((BlockType)actionChunk.blockData[(short)blockDataIndex.X, (short)blockDataIndex.Y, (short)blockDataIndex.Z] == BlockType.LAMP_BLOCK)
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
            actionChunk.blockData[(short)blockDataIndex.X, (short)blockDataIndex.Y, (short)blockDataIndex.Z] = (int)BlockType.AIR;

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

            BlockType typeU = (BlockType)up.blockData[(short)blockDataIndexUP.X, (short)(blockDataIndexUP.Y + 1), (short)blockDataIndexUP.Z];
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

            BlockType typeD = (BlockType)down.blockData[(short)blockDataIndexDOWN.X, (short)(blockDataIndexDOWN.Y - 1), (short)blockDataIndexDOWN.Z];
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

            BlockType typeE = (BlockType)east.blockData[(short)(blockDataIndexEAST.X + 1), (short)blockDataIndexEAST.Y, (short)blockDataIndexEAST.Z];
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

            BlockType typeW = (BlockType)west.blockData[(short)(blockDataIndexWEST.X - 1), (short)blockDataIndexWEST.Y, (short)blockDataIndexWEST.Z];
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

            BlockType typeN = (BlockType)north.blockData[(short)blockDataIndexNORTH.X, (short)blockDataIndexNORTH.Y, (short)(blockDataIndexNORTH.Z + 1)];
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

            BlockType typeS = (BlockType)south.blockData[(short)blockDataIndexSOUTH.X, (short)blockDataIndexSOUTH.Y, (short)(blockDataIndexSOUTH.Z - 1)];

            //Only add block if its not air
            //Console.WriteLine("South Block: " + typeS + " at " + s);
            if (typeS != BlockType.AIR)
            {
                //Console.WriteLine("Adding SOUTH block");
                AddBlockToChunk(s, typeS, _lightHelper.GetBlockLight(s, BlockFace.SOUTH, south));
            }
        }

        private BlockType GetBlocktypeFromLocation(Vector3 location)
        {
            Chunk chunk = GetAndLoadGlobalChunkFromCoords(location);
            Vector3i idx = GetChunkRelativeCoordinates(location);
            return (BlockType)chunk.blockData[idx.X, idx.Y, idx.Z];
        }
        public Dictionary<string, Region> GetVisibleRegions()
        {
            return VisibleRegions;
        }
        //================================================================================================
        //============================= Light Propagation and Depropagation ==============================
        //================================================================================================
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
        /**
        * Propagates light using Breadth First Search.
        *
        * @Param faceDir The block face to apply the light to
        * @Param node The current light node being processed
        * @Param colorOverride Whether to combine the color with the existing light or override it
        * 
       */
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

