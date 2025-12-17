using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using MessagePack;
using OpenTK.Mathematics;
using Vox.Enums;
using Vox.Model;
using Vox.Rendering;
namespace Vox.Genesis
{

    public class RegionManager : List<Region>
    {
        public static Dictionary<string, Region> VisibleRegions = [];
        private static string worldDir = "";
        public static readonly int CHUNK_HEIGHT = 384;
        private static int RENDER_DISTANCE = 3;
        public static readonly int REGION_BOUNDS = 512;
        public static readonly int CHUNK_BOUNDS = 32;
        public static long WORLD_SEED;
        private static object chunkLock = new();


        private static readonly ConcurrentQueue<LightNode> BFSEmissivePropagationQueue = new(new Queue<LightNode>((int)Math.Pow(CHUNK_BOUNDS, 3)));
        /**
         * The highest level object representation of a world. The RegionManager
         * contains an in-memory list of regions that are currently within
         * the players render distance. This region list is constantly updated each
         * frame and is used for reading regions from file and writing regions to file.
         *
         * @param path The path of this worlds directory
         */
        public RegionManager(string path)
        {
            worldDir = path;
            //WORLD_SEED = path.GetHashCode();

            byte[] buffer = new byte[8];
            RandomNumberGenerator.Fill(buffer); // Fills the buffer with random bytes
            WORLD_SEED = BitConverter.ToInt64(buffer, 0);

            Directory.CreateDirectory(Path.Combine(worldDir, "regions")); 

            ChunkCache.SetBounds(CHUNK_BOUNDS);
            ChunkCache.SetRenderDistance(RENDER_DISTANCE);
        }

        public string GetWorldDirectory()
        {
            return worldDir;
        }
        public static void EnqueueEmissiveLightNode(LightNode node)
        {
            BFSEmissivePropagationQueue.Enqueue(node);
        }

        public static LightNode DequeueEmissiveLightNode()
        {
            if (BFSEmissivePropagationQueue.TryDequeue(out LightNode node))
                return node;
            else
                return new LightNode("", null);
        }

        public static int GetEmissiveQueueCount()
        {
            return BFSEmissivePropagationQueue.Count;
        }
        /**
        * Retrieves the Y value for any given x,z column in any chunk
        * @param x coordinate of column
        * @param z coordinate of column
        * @return Returns the noise value which is scaled between 0 and CHUNK_HEIGHT
        */
        public static short GetGlobalHeightMapValue(int x, int z)
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

            return (short)(noise * CHUNK_HEIGHT);

        }

        /**
         * Given a 3D point in world space, convert to chunk relative coordinates within
         * the range of the chunks bounds.
         **/
        public static Vector3i GetChunkRelativeCoordinates(Vector3 v)
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
        public static BlockType GetGlobalBlockType(int x, int y, int z)
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
        public static BlockType GetGlobalBlockType(Vector3 blockSpace)
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
        public static int PollChunkMemory()
        {
            int count = 0;
            foreach (KeyValuePair<string, Region> pair in VisibleRegions)
                count += pair.Value.chunks.Count;
            return count;
        }
        public static void SetRenderDistance(int i)
        {
            RENDER_DISTANCE = i;
        }
        public static int GetRenderDistance() { return RENDER_DISTANCE; }
        /**
         * Removes a region from the visible regions once a player leaves a region and
         * their render distance no longer overlaps it. Writes region to file in the process
         * effectively saving the regions state for future use.
         *
         * @param r The region to leave
         */
        public static void LeaveRegion(string rIndex)
        {
            Logger.Info($"Leaving {VisibleRegions[rIndex]}");
            WriteRegion(rIndex);
            VisibleRegions.Remove(rIndex);

        }

        /**
         * Generates or loads an already generated region from filesystem when the players
         * render distance intersects with the regions bounds.
         */
        public static Region EnterRegion(string rIndex)
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
        public static void WriteRegion(string rIndex)
        {
            Region r = VisibleRegions[rIndex];

            string path = Path.Combine(worldDir, "regions", $"{r.GetBounds().X}.{r.GetBounds().Y}.dat");

            byte[] serializedRegion = MessagePackSerializer.Serialize(r);

            File.WriteAllBytes(path, serializedRegion);
        }

        /**
         * Read existing region from file
         */
        public static Region? ReadRegion(string path)
        {
            byte[] serializedData = File.ReadAllBytes(path);

            // Deserialize the byte array back into an object
            Region region = MessagePackSerializer.Deserialize<Region>(serializedData);
            Logger.Info($"Reading Region from file {region}");

            return region;

        }

        /**
         * The ChunkCache will update the regions in memory, storing them as potentially blank objects
         * if the region was not already in memory. This method is responsible for reading region data
         * into these blank region objects when in memory and writing data to the file
         * system for future use when the player no longer inhabits them.
         *
         */
        public static void UpdateVisibleRegions()
        {
            //Updates regions within render distance
            ChunkCache.UpdateChunkCache();
            Dictionary<string, Region> updatedRegions = ChunkCache.GetRegions();

            if (VisibleRegions.Count > 0)
            {

                //Retrieves from file or generates any region that is visible
                for (int i = 0; i < updatedRegions.Count; i++)
                {
                    //Enter region if not found in visible regions
                    if (!VisibleRegions.ContainsKey(updatedRegions.Keys.ElementAt(i)))
                        EnterRegion(updatedRegions.Keys.ElementAt(i));
                }

                //Write to file and de-render any regions that are no longer visible
                for (int i = 0; i < VisibleRegions.Count; i++)
                {
                    if (!updatedRegions.ContainsKey(VisibleRegions.Keys.ElementAt(i)))
                    {
                        Console.WriteLine($"Unloaded Region: {VisibleRegions.Keys.ElementAt(i)}");
                        LeaveRegion(VisibleRegions.Keys.ElementAt(i));
                    }
                }
            }
        }

        /**
         * Attempts to get a region from file.
         * Returns an empty region to write later if it theres no file to read.
         */
        public static Region TryGetRegionFromFile(string rIndex)
        {
            int[] index = [.. rIndex.Split('|').Select(int.Parse)];
            string path = Path.Combine(worldDir, "regions", index[0] + "." + index[1] + ".dat");


            if (!VisibleRegions.TryGetValue(rIndex, out Region? value) && !File.Exists(path))
            {
                // Logger.Info($"Generating new region {rIndex}");
                return new Region(index[0], index[1]);

            }
            else if (VisibleRegions.TryGetValue(rIndex, out Region? val) && !File.Exists(path))
            {
                return val;

            }
            else if (!VisibleRegions.TryGetValue(rIndex, out Region? v) && File.Exists(path))
            {
                return ReadRegion(path);
            }
            else
            {
                return VisibleRegions[rIndex];
            }

        }

        /**
         * Get new empty region given any x,z coordinate pair.
         */
        public static Region GetGlobalRegionFromChunkCoords(int x, int z)
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
            return new Region(xUpperLimit, zUpperLimit);
        }

        /**
         * Get any chunk given a x,y,z coordinate trio and load it into memory
         */
        public static Chunk GetAndLoadGlobalChunkFromCoords(int x, int y, int z)
        {

            string playerChunkIdx =
                $"{Math.Floor((float)x / CHUNK_BOUNDS) * CHUNK_BOUNDS}|" +
                $"{Math.Floor((float)y / CHUNK_BOUNDS) * CHUNK_BOUNDS}|" +
                $"{Math.Floor((float)z / CHUNK_BOUNDS) * CHUNK_BOUNDS}";

            int[] chunkIdxArray = [.. playerChunkIdx.Split('|').Select(int.Parse)];
            string playerRegionIdx = Region.GetRegionIndex(chunkIdxArray[0], chunkIdxArray[2]);
            Region r = EnterRegion(playerRegionIdx);

            //If chunk has not yet been generated, generate it
            if (!r.chunks.TryGetValue(playerChunkIdx, out Chunk? value))
            {
                value = new Chunk().Initialize(chunkIdxArray[0], chunkIdxArray[1], chunkIdxArray[2]);
                lock (chunkLock)
                {
                    r.chunks.Add(playerChunkIdx, value);
                }
                return value;
            } else
            {
                return r.chunks[$"{chunkIdxArray[0]}|{chunkIdxArray[1]}|{chunkIdxArray[2]}"];
            }

        }
        public static Chunk GetAndLoadGlobalChunkFromCoords(Vector3 v)
        {
            int x = (int)v.X;
            int y = (int)v.Y;
            int z = (int)v.Z;

            string playerChunkIdx =
                $"{Math.Floor((float)x / CHUNK_BOUNDS) * CHUNK_BOUNDS}|" +
                $"{Math.Floor((float)y / CHUNK_BOUNDS) * CHUNK_BOUNDS}|" +
                $"{Math.Floor((float)z / CHUNK_BOUNDS) * CHUNK_BOUNDS}";

            int[] chunkIdxArray = playerChunkIdx.Split('|').Select(int.Parse).ToArray();
            string playerRegionIdx = Region.GetRegionIndex(chunkIdxArray[0], chunkIdxArray[2]);
            Region r = EnterRegion(playerRegionIdx);

            if (!r.chunks.TryGetValue(playerChunkIdx, out Chunk? value))
            {
                value = new Chunk().Initialize(chunkIdxArray[0], chunkIdxArray[1], chunkIdxArray[2]);
                r.chunks.Add(playerChunkIdx, value);
            }
            return value;
        }

        /**
         * Given an x,y,z coordinate trio representing a block location,
         * get the chunk that block is supposed to be in and add it to the chunk
         */
        public static void AddBlockToChunk(Vector3 blockSpace, BlockType type, ColorVector blockLight)
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
                actionChunk.AddUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing UP Face");
            }
            // Positive X (EAST)
            if (x + 1 >= bounds || actionChunk.blockData[(short)(x + 1), (short)y, (short)z] == 0)
            {
                int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.EAST);
                BlockFace faceDir = BlockFace.EAST;
                actionChunk.AddUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing EAST Face");
            }


            //Negative X (WEST)
            if (x - 1 < 0 || actionChunk.blockData[(short)(x - 1), (short)y, (short)z] == 0)
            {
                int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.WEST);
                BlockFace faceDir = BlockFace.WEST;
                actionChunk.AddUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing WEST Face");
            }

            //Negative Y (DOWN)
            if (y - 1 < 0 || actionChunk.blockData[(short)x, (short)(y - 1), (short)z] == 0)
            //If player is below the blocks Y level, render the bottom face
            // && Window.GetPlayer().GetPosition().Y < y)
            {
                int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.DOWN);
                BlockFace faceDir = BlockFace.DOWN;
                actionChunk.AddUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing DOWN Face");
            }

            //Positive Z (NORTH)
            if (z + 1 >= bounds || actionChunk.blockData[(short)x, (short)(y), (short)(z + 1)] == 0)
            {
                int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.NORTH);
                BlockFace faceDir = BlockFace.NORTH;
                actionChunk.AddUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing NORTH Face");
            }

            //Negative Z (SOUTH)
            if (z - 1 < 0 || actionChunk.blockData[(short)x, (short)(y), (short)(z - 1)] == 0)
            {
                int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.SOUTH);
                BlockFace faceDir = BlockFace.SOUTH;
                actionChunk.AddUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing SOUTH Face");
            }

            //Set block emissiveness after addding faces
            if (type == BlockType.LAMP_BLOCK)
            {
                //Track emissive lighting for loading and deporpagation
                if (LightHelper.GetLightTrackingList().ContainsKey(blockSpace))
                    return;

                ThreadPool.QueueUserWorkItem(new WaitCallback(delegate (object state)
                {
                    LightHelper.TrackLighting(blockSpace, blockLight);

                    //Set all block faces to the same light levels
                    LightHelper.SetBlockLight(blockSpace, blockLight, actionChunk, false, false);
                    LightHelper.PropagateBlockLight(blockSpace, BlockFace.UP, false, false);
                    LightHelper.PropagateBlockLight(blockSpace, BlockFace.DOWN, false, false);
                    LightHelper.PropagateBlockLight(blockSpace, BlockFace.EAST, false, false);
                    LightHelper.PropagateBlockLight(blockSpace, BlockFace.WEST, false, false);
                    LightHelper.PropagateBlockLight(blockSpace, BlockFace.NORTH, false, false);
                    LightHelper.PropagateBlockLight(blockSpace, BlockFace.SOUTH, false, false);
                }));

            }
        }

        /**
 * Given an x,y,z coordinate trio representing a block location,
 * get the chunk that block is supposed to be in and add it to the chunk
 */
        public static void AddBlockToChunk(Vector3 blockSpace, BlockType type, ColorVector blockLight, bool colorOverride)
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
                actionChunk.AddUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing UP Face");
            }
            // Positive X (EAST)
            if (x + 1 >= bounds || actionChunk.blockData[(short)(x + 1), (short)y, (short)z] == 0)
            {
                int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.EAST);
                BlockFace faceDir = BlockFace.EAST;
                actionChunk.AddUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing EAST Face");
            }


            //Negative X (WEST)
            if (x - 1 < 0 || actionChunk.blockData[(short)(x - 1), (short)y, (short)z] == 0)
            {
                int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.WEST);
                BlockFace faceDir = BlockFace.WEST;
                actionChunk.AddUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing WEST Face");
            }

            //Negative Y (DOWN)
            if (y - 1 < 0 || actionChunk.blockData[(short)x, (short)(y - 1), (short)z] == 0)
            //If player is below the blocks Y level, render the bottom face
            // && Window.GetPlayer().GetPosition().Y < y)
            {
                int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.DOWN);
                BlockFace faceDir = BlockFace.DOWN;
                actionChunk.AddUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing DOWN Face");
            }

            //Positive Z (NORTH)
            if (z + 1 >= bounds || actionChunk.blockData[(short)x, (short)(y), (short)(z + 1)] == 0)
            {
                int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.NORTH);
                BlockFace faceDir = BlockFace.NORTH;
                actionChunk.AddUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing NORTH Face");
            }

            //Negative Z (SOUTH)
            if (z - 1 < 0 || actionChunk.blockData[(short)x, (short)(y), (short)(z - 1)] == 0)
            {
                int texLayer = (int)ModelLoader.GetModel(type).GetTexture(BlockFace.SOUTH);
                BlockFace faceDir = BlockFace.SOUTH;
                actionChunk.AddUpdateBlockFace(blockSpace, texLayer, faceDir);
                Console.WriteLine("Updaing SOUTH Face");
            }

            //Set block emissiveness after addding faces
            if (type == BlockType.LAMP_BLOCK)
            {
                //Track emissive lighting for loading and deporpagation
                if (LightHelper.GetLightTrackingList().ContainsKey(blockSpace))
                    return;


                LightHelper.TrackLighting(blockSpace, blockLight);

                //Set all block faces to the same light levels
                LightHelper.SetBlockLight(blockSpace, blockLight, actionChunk, false, colorOverride);
                LightHelper.PropagateBlockLight(blockSpace, BlockFace.UP, false, colorOverride);
                LightHelper.PropagateBlockLight(blockSpace, BlockFace.DOWN, false, colorOverride);
                LightHelper.PropagateBlockLight(blockSpace, BlockFace.EAST, false, colorOverride);
                LightHelper.PropagateBlockLight(blockSpace, BlockFace.WEST, false, colorOverride);
                LightHelper.PropagateBlockLight(blockSpace, BlockFace.NORTH, false, colorOverride);
                LightHelper.PropagateBlockLight(blockSpace, BlockFace.SOUTH, false, colorOverride);

            }
        }

        /**
         * Given an x,y,z coordinate trio representing a block location,
         * get the chunk that block is supposed to be in and remove the block
         * if it is there
         */
        public static void RemoveBlockFromChunk(Vector3 blockSpace)
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

                    LightHelper.SetBlockLight(blockSpace, new ColorVector(0, 0, 0), actionChunk, true, false);
                    LightHelper.PropagateBlockLight(blockSpace, BlockFace.UP, true, true);
                    LightHelper.PropagateBlockLight(blockSpace, BlockFace.DOWN, true, true);
                    LightHelper.PropagateBlockLight(blockSpace, BlockFace.EAST, true, true);
                    LightHelper.PropagateBlockLight(blockSpace, BlockFace.WEST, true, true);
                    LightHelper.PropagateBlockLight(blockSpace, BlockFace.NORTH, true, true);
                    LightHelper.PropagateBlockLight(blockSpace, BlockFace.SOUTH, true, true);

                    //Undtrack light source
                    LightHelper.GetLightTrackingList().Remove(blockSpace);
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
                AddBlockToChunk(u, typeU, LightHelper.GetBlockLight(u, BlockFace.UP, up));
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
                AddBlockToChunk(d, typeD, LightHelper.GetBlockLight(d, BlockFace.DOWN, down));
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
                AddBlockToChunk(e, typeE, LightHelper.GetBlockLight(e, BlockFace.EAST, east));
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
                AddBlockToChunk(w, typeW, LightHelper.GetBlockLight(w, BlockFace.WEST, west));
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
                AddBlockToChunk(n, typeN, LightHelper.GetBlockLight(n, BlockFace.NORTH, north));
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
                AddBlockToChunk(s, typeS, LightHelper.GetBlockLight(s, BlockFace.SOUTH, south));
            }
        }

        public static BlockType GetBlocktypeFromLocation(Vector3 location)
        {
            Chunk chunk = GetAndLoadGlobalChunkFromCoords(location);
            Vector3i idx = GetChunkRelativeCoordinates(location);
            return (BlockType)chunk.blockData[idx.X, idx.Y, idx.Z];
        } 
    }
}

