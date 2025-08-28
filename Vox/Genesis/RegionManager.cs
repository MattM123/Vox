
using System.Reflection.Metadata;
using System.Security.Cryptography;
using MessagePack;
using OpenTK.Mathematics;
using Vox.Model;
using Vox.Rendering;
namespace Vox.Genesis
{

    public class RegionManager : List<Region>
    {
        public static Dictionary<string, Region> VisibleRegions = [];
        private static string worldDir = "";
        public static readonly int CHUNK_HEIGHT = 384;
        private static int RENDER_DISTANCE = 4;
        public static readonly int REGION_BOUNDS = 512;
        public static readonly int CHUNK_BOUNDS = 32;
        public static long WORLD_SEED;
        private static object chunkLock = new();
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
            float var1 = 12;

            //Affects coalescence of terrain. A higher value will result in more condensed, sharp peaks and a lower value will result in
            //more smooth, spread out hills.
            double var2 = 0.01;

            float f = 1 * OpenSimplex2.Noise2(seed, x * var2, z * var2) / (var1 + 2) //Noise Octave 1
                    + (float)(0.5 * OpenSimplex2.Noise2(seed, x * (var2 * 2), z * (var2 * 2)) / (var1 + 4)) //Noise Octave 2
                    + (float)(0.25 * OpenSimplex2.Noise2(seed, x * (var2 * 2), z * (var2 * 2)) / (var1 + 6)); //Noise Octave 3

            //Normalized teh noise value
            float noise = (f * 0.5f) + 0.5f;

            return  (short)(noise * CHUNK_HEIGHT);

        }

        /**
         * Given a 3D point in world space, convert to chunk relative coordinates within
         * the range of the chunks bounds.
         **/
        public static Vector3 GetChunkRelativeCoordinates(Vector3 v)
        {
            int bounds = RegionManager.CHUNK_BOUNDS;
            return new Vector3(
                Math.Abs(v.X % bounds),
                Math.Abs(v.Y % bounds),
                Math.Abs(v.Z % bounds)
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
            index = Math.Clamp(index, 1, blocktypes - 3);

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
            int x = (int) blockSpace.X;
            int y = (int) blockSpace.Y;
            int z = (int) blockSpace.Z;

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
            index = Math.Clamp(index, 1, blocktypes - 3);

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
            if (VisibleRegions.ContainsKey(rIndex))
                return VisibleRegions[rIndex];

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
            int[] index = rIndex.Split('|').Select(int.Parse).ToArray();
            string path = Path.Combine(worldDir, "regions", index[0] + "." + index[1] + ".dat");


            if (!VisibleRegions.TryGetValue(rIndex, out Region? value) && !File.Exists(path)) {
               // Logger.Info($"Generating new region {rIndex}");
                return new Region(index[0], index[1]);

            } else if (VisibleRegions.TryGetValue(rIndex, out Region? val) && !File.Exists(path))
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
                $"{Math.Floor((float) x / CHUNK_BOUNDS) * CHUNK_BOUNDS}|" +
                $"{Math.Floor((float) y / CHUNK_BOUNDS) * CHUNK_BOUNDS}|" +
                $"{Math.Floor((float) z / CHUNK_BOUNDS) * CHUNK_BOUNDS}";

            int[] chunkIdxArray = playerChunkIdx.Split('|').Select(int.Parse).ToArray();
            string playerRegionIdx = Region.GetRegionIndex(chunkIdxArray[0], chunkIdxArray[2]);
            Region r = EnterRegion(playerRegionIdx);

            if (!r.chunks.TryGetValue(playerChunkIdx, out Chunk? value))
            {
                value = new Chunk().Initialize(chunkIdxArray[0], chunkIdxArray[1], chunkIdxArray[2]);
                lock (chunkLock)
                {
                    r.chunks.Add(playerChunkIdx, value);
                }
            }
            return value;
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
        public static void AddBlockToChunk(Vector3 blockSpace)
        {
            //The chunk that is added to
            Chunk actionChunk = GetAndLoadGlobalChunkFromCoords(blockSpace);

            //The block data index within that chunk to modify
            Vector3 blockDataIndex = GetChunkRelativeCoordinates(blockSpace);

            //Update block type in chunk data
            short playerBlockType = (short) Window.GetPlayer().GetPlayerBlockType();

            actionChunk.blockData[(int)blockDataIndex.X, (int)blockDataIndex.Y, (int)blockDataIndex.Z] = playerBlockType;
           
            /*=============================================
             * Add a single block to the SSBO for rendering
             *=============================================*/

            int bounds = CHUNK_BOUNDS;

            int x = (int)blockDataIndex.X;
            int y = (int)blockDataIndex.Y;
            int z = (int)blockDataIndex.Z;

            //Positive Y (UP)
            if (y + 1 >= bounds || actionChunk.blockData[x, y + 1, z] == 0)
            {
                int texLayer = (int)ModelLoader.GetModel(playerBlockType).GetTexture(Face.UP);
                Face faceDir = Face.UP;
                actionChunk.AddBlockFace(blockSpace, texLayer, faceDir);
            }
            // Positive X (EAST)
            if (x + 1 >= bounds || actionChunk.blockData[x + 1, y, z] == 0)
            {
                int texLayer = (int)ModelLoader.GetModel(playerBlockType).GetTexture(Face.EAST);
                Face faceDir = Face.EAST;
                actionChunk.AddBlockFace(blockSpace, texLayer, faceDir);
            }


            //Negative X (WEST)
            if (x - 1 < 0 || actionChunk.blockData[x - 1, y, z] == 0)
            {
                int texLayer = (int)ModelLoader.GetModel(playerBlockType).GetTexture(Face.WEST);
                Face faceDir = Face.WEST;
                actionChunk.AddBlockFace(blockSpace, texLayer, faceDir);
            }

            //Negative Y (DOWN)
            if ((y - 1 < 0 || actionChunk.blockData[x, y - 1, z] == 0)
                 //If player is below the blocks Y level, render the bottom face
                 && Window.GetPlayer().GetPosition().Y < y)
            {
                int texLayer = (int)ModelLoader.GetModel(playerBlockType).GetTexture(Face.DOWN);
                Face faceDir = Face.DOWN;
                actionChunk.AddBlockFace(blockSpace, texLayer, faceDir);
            }

            //Positive Z (NORTH)
            if (z + 1 >= bounds || actionChunk.blockData[x, y, z + 1] == 0)
            {
                int texLayer = (int)ModelLoader.GetModel(playerBlockType).GetTexture(Face.NORTH);
                Face faceDir = Face.NORTH;
                actionChunk.AddBlockFace(blockSpace, texLayer, faceDir);
            }

            //Negative Z (SOUTH)
            if (z - 1 < 0 || actionChunk.blockData[x, y, z - 1] == 0)
            {
                int texLayer = (int)ModelLoader.GetModel(playerBlockType).GetTexture(Face.SOUTH);
                Face faceDir = Face.SOUTH;
                actionChunk.AddBlockFace(blockSpace, texLayer, faceDir);
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

            //Update block type in chunk data to AIR
            actionChunk.blockData[(int)blockDataIndex.X, (int)blockDataIndex.Y, (int)blockDataIndex.Z] = (int) BlockType.AIR;

            /*==================================================
            * Set face instances to BlockType.AIR in SSBO
            *=================================================*/
            int bounds = CHUNK_BOUNDS;
            
            int x = (int)blockDataIndex.X;
            int y = (int)blockDataIndex.Y;
            int z = (int)blockDataIndex.Z;

            actionChunk.AddBlockFace(blockSpace, 0, Face.UP);   //Positive Y (UP)        
            actionChunk.AddBlockFace(blockSpace, 0, Face.EAST); //Positive X (EAST)
            actionChunk.AddBlockFace(blockSpace, 0, Face.WEST); //Negative X (WEST)    
            actionChunk.AddBlockFace(blockSpace, 0, Face.DOWN); //Negative Y (DOWN)      
            actionChunk.AddBlockFace(blockSpace, 0, Face.NORTH);//Positive Z (NORTH)
            actionChunk.AddBlockFace(blockSpace, 0, Face.SOUTH);//Negative Z (SOUTH)  


            /*==================================================
            / Check for terrain holes after block removal
            *=================================================*/
            foreach (int type in actionChunk.blockData) { }


        }
    }
}

