
using System.Drawing;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Vox.Genesis
{
    public class ChunkCache
    {
        private static int renderDistance = RegionManager.GetRenderDistance();
        private static int bounds = RegionManager.CHUNK_BOUNDS;
        private static Chunk? playerChunk;        
        private static bool reRenderFlag = false;
        private static Dictionary<string, Chunk> chunks = new();
        private static Dictionary<string, Region> regions = new();

        private static readonly object chunkLock = new();

        /**
         *
         * Updates, stores, and returns a list of in-memory
         * chunks that should be rendered around a player at
         * a certain point in time.
         * @param bounds The length and width of the square chunk.
         * @param playerChunk The chunk a player inhabits.
         */
        public ChunkCache(int bounds, Chunk playerChunk)
        {
            ChunkCache.bounds = bounds;
            ChunkCache.playerChunk = playerChunk;
            reRenderFlag = true;
        }

        public static void SetBounds(int bounds)
        {
            ChunkCache.bounds = bounds;
        }
        public static void SetRenderDistance(int renderDistance)
        {
            ChunkCache.renderDistance = renderDistance;
        }
        public static void SetPlayerChunk(Chunk c)
        {
            playerChunk = c;
        }

        /**
         * Gets the chunks diagonally oriented from the chunk the player is in.
         * This includes each 4 quadrants surrounding the player. This does not include the chunks
         * aligned straight out from the player.
         *
         * @return A list of chunks that should be rendered diagonally from the chunk the
         * player is in.
         */
        private static void GetQuadrantChunks()
        {
            Region playerRegion = Window.GetPlayer().GetRegionWithPlayer();

            for (int y = 0; y < 0 - (renderDistance / 2); y++)
            {
                //Top left quadrant
                Vector3 TLstart = new(playerChunk.GetLocation().X - bounds, 0, playerChunk.GetLocation().Z + bounds);
                for (int x = (int)TLstart.X; x > TLstart.X - (renderDistance * bounds); x -= bounds)
                {
                    for (int z = (int)TLstart.Z; z < TLstart.Z + (renderDistance * bounds); z += bounds)
                    {
                        CacheHelper(x, y, z);
                    }
                }

                //Top right quadrant
                Vector3 TRStart = new(playerChunk.GetLocation().X + bounds, 0, playerChunk.GetLocation().Z + bounds);
                for (int x = (int)TRStart.X; x < TRStart.X + (renderDistance * bounds); x += bounds)
                {
                    for (int z = (int)TRStart.Z; z < TRStart.Z + (renderDistance * bounds); z += bounds)
                    {
                        CacheHelper(x, y, z);
                    }
                }

                //Bottom right quadrant
                Vector3 BRStart = new(playerChunk.GetLocation().X - bounds, 0, playerChunk.GetLocation().Z - bounds);
                for (int x = (int)BRStart.X; x > BRStart.X - (renderDistance * bounds); x -= bounds)
                {
                    for (int z = (int)BRStart.Z; z > BRStart.Z - (renderDistance * bounds); z -= bounds)
                    {
                        CacheHelper(x, y, z);
                    }
                }

                //Bottom left quadrant
                Vector3 BLStart = new(playerChunk.GetLocation().X + bounds, 0, playerChunk.GetLocation().Z - bounds);
                for (int x = (int)BLStart.X; x < BLStart.X + (renderDistance * bounds); x += bounds)
                {
                    for (int z = (int)BLStart.Z; z > BLStart.Z - (renderDistance * bounds); z -= bounds)
                    {
                        CacheHelper(x, y, z);
                    }
                }
            }
        }


        //This gets called A LOT.
        private static void CacheHelper(int x, int y, int z)
        {
            string regionIdx = Region.GetRegionIndex(x, z);

            //Look for region in loaded regions
            RegionManager.VisibleRegions.TryGetValue(regionIdx, out Region? chunkRegion);

            //if region is still null, try get from file system
            if (chunkRegion == null)
            {
                chunkRegion = RegionManager.TryGetRegionFromFile(regionIdx);
                RegionManager.EnterRegion(regionIdx); //cache region in memory for future additions to chunk list
            }


            Chunk? chunk = chunkRegion.chunks.ContainsKey($"{x}|{y}|{z}") ? chunkRegion.chunks[$"{x}|{y}|{z}"] : null;

            if (chunk != null)
            {
                if (!chunks.ContainsKey($"{x}|{y}|{z}"))
                    chunks.Add($"{x}|{y}|{z}", chunk);
    
                if (!chunkRegion.chunks.ContainsKey($"{x}|{y}|{z}"))
                    chunkRegion.chunks.Add($"{x}|{y}|{z}", chunk);
                    
            }
            else
            {
                Chunk c = new Chunk().Initialize(x, y, z);

                if (!chunkRegion.chunks.ContainsKey($"{x}|{y}|{z}"))
                    chunkRegion.chunks.Add($"{x}|{y}|{z}", c);

                if (!chunks.ContainsKey($"{x}|{y}|{z}"))
                    chunks.Add($"{x}|{y}|{z}", c);

               
            }
            if (!regions.ContainsKey(Region.GetRegionIndex(x, z))) 
                regions.Add(Region.GetRegionIndex(x, z), chunkRegion);
        }

        /**
         * Gets the chunks that should be rendered along the X And Z axis. E.X a renderer distance
         * of 2 would return 8 chunks, 2 on every side of the player in each cardinal direction
         *
         * @return A list of chunks that should be rendered in x, z, -x, and -z directions
         */
        private static void GetCardinalChunks()
        {
            Region playerRegion = Window.GetPlayer().GetRegionWithPlayer();

            //Positive X
            for (int i = 1; i <= renderDistance; i++)
            {
                int x = (int)playerChunk.GetLocation().X + (i * bounds);
                int z = (int)playerChunk.GetLocation().Z;
                int y = (int)playerChunk.GetLocation().Y;
                CacheHelper(x, y, z);
            }

            //Negative X
            for (int i = 1; i <= renderDistance; i++)
            {
                int x = (int)playerChunk.GetLocation().X - (i * bounds);
                int z = (int)playerChunk.GetLocation().Z;
                int y = (int)playerChunk.GetLocation().Y;
                CacheHelper(x, y, z);
            }

            //Positive Y
            for (int i = 1; i <= renderDistance; i++)
            {
                int x = (int)playerChunk.GetLocation().X;
                int z = (int)playerChunk.GetLocation().Z + (i * bounds);
                int y = (int)playerChunk.GetLocation().Y;
                CacheHelper(x, y, z);
            }
            //Negative Y
            for (int i = 1; i <= renderDistance; i++)
            {

                int x = (int)playerChunk.GetLocation().X;
                int z = (int)playerChunk.GetLocation().Z - (i * bounds);
                int y = (int)playerChunk.GetLocation().Y;
                CacheHelper(x, y, z);
            }
        }

        /**
         * Returns a list of chunks that should be rendered around a player based on a render distance value
         * and updates chunks that surround a player in a global scope.
         * @return The list of chunks that should be rendered.
         */
        public static Dictionary<string, Chunk> GetChunksToRender()
        {
              chunks.Clear();
              regions.Clear();

              GetQuadrantChunks();
              GetCardinalChunks();
              chunks.Add($"{playerChunk.xLoc}|{playerChunk.zLoc}", playerChunk);

            return chunks;
        }
        public static void ReRender(bool b)
        {
            reRenderFlag = b;
        }
        public static Dictionary<string, Region> GetRegions()
        {
            return regions;
        }

    }

}
