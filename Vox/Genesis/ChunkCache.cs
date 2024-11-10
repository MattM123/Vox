
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Vox.Genesis
{
    public class ChunkCache
    {
        private static int renderDistance = RegionManager.GetRenderDistance();
        private static int bounds = RegionManager.CHUNK_BOUNDS;
        private static Chunk? playerChunk;
        private static List<Region> regions = [];
        private static int uboModelMatrices;
        private static int maxMatrices = 0;
        private static bool reRenderFlag = false;
        private static List<Chunk> chunks = [];
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
            uboModelMatrices = GL.GenBuffer();
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

            //Top left quadrant
            Vector3 TLstart = new(playerChunk.GetLocation().X - bounds, 0, playerChunk.GetLocation().Z + bounds);
            for (int x = (int)TLstart.X; x > TLstart.X - (renderDistance * bounds); x -= bounds)
            {
                for (int z = (int)TLstart.Z; z < TLstart.Z + (renderDistance * bounds); z += bounds)
                {
                    CacheHelper(x, z);
                }
            }

            //Top right quadrant
            Vector3 TRStart = new(playerChunk.GetLocation().X + bounds, 0, playerChunk.GetLocation().Z + bounds);
            for (int x = (int)TRStart.X; x < TRStart.X + (renderDistance * bounds); x += bounds)
            {
                for (int z = (int)TRStart.Z; z < TRStart.Z + (renderDistance * bounds); z += bounds)
                {
                    CacheHelper(x, z);
                }
            }

            //Bottom right quadrant
            Vector3 BRStart = new(playerChunk.GetLocation().X - bounds, 0, playerChunk.GetLocation().Z - bounds);
            for (int x = (int)BRStart.X; x > BRStart.X - (renderDistance * bounds); x -= bounds)
            {
                for (int z = (int)BRStart.Z; z > BRStart.Z - (renderDistance * bounds); z -= bounds)
                {
                    CacheHelper(x, z);
                }
            }

            //Bottom left quadrant
            Vector3 BLStart = new(playerChunk.GetLocation().X + bounds, 0, playerChunk.GetLocation().Z - bounds);
            for (int x = (int)BLStart.X; x < BLStart.X + (renderDistance * bounds); x += bounds)
            {
                for (int z = (int)BLStart.Z; z > BLStart.Z - (renderDistance * bounds); z -= bounds)
                {
                    CacheHelper(x, z);
                }
            }
        }

        //This gets called A LOT.
        private static void CacheHelper(int x, int z)
        {
            Region chunkRegion = RegionManager.GetGlobalRegionFromChunkCoords(x, z);
            Chunk chunk = chunkRegion.GetChunkWithLocation(new(x, 0, z));

            chunks ??= [];
            regions ??= [];

            if (chunk != null)
            {

                if (!chunks.Contains(chunk))
                    chunks.Add(chunk);

                if (!regions.Contains(chunkRegion))
                        regions.Add(chunkRegion);

                    if (!chunkRegion.GetChunks().Contains(chunk))
                        chunkRegion.BinaryInsertChunkWithLocation(0, chunkRegion.GetChunks().Count - 1, chunk.GetLocation());
                    
            }
            else
            {
                chunk = new Chunk().Initialize(x, z);
                Region region = chunk.GetRegion();

         
                if (!chunks.Contains(chunk))
                    chunks.Add(chunk);
                    

                region.BinaryInsertChunkWithLocation(0, region.GetChunks().Count - 1, chunk.GetLocation());

                if (!regions.Contains(region))
                    regions.Add(region);


            }
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
                int x = (int) playerChunk.GetLocation().X + (i * bounds);
                int z = (int) playerChunk.GetLocation().Z;
                CacheHelper(x, z);
            }

            //Negative X
            for (int i = 1; i <= renderDistance; i++)
            {
                int x = (int)playerChunk.GetLocation().X - (i * bounds);
                int z = (int)playerChunk.GetLocation().Z;
                CacheHelper(x, z);
            }

            //Positive Y
            for (int i = 1; i <= renderDistance; i++)
            {
                int x = (int)playerChunk.GetLocation().X;
                int z = (int)playerChunk.GetLocation().Z + (i * bounds);
                CacheHelper(x, z);
            }
            //Negative Y
            for (int i = 1; i <= renderDistance; i++)
            {

                int x = (int)playerChunk.GetLocation().X;
                int z = (int)playerChunk.GetLocation().Z - (i * bounds);
                CacheHelper(x, z);
            }
        }

        /**
         * Returns a list of chunks that should be rendered around a player based on a render distance value
         * and updates chunks that surround a player in a global scope.
         * @return The list of chunks that should be rendered.
         */
        public static List<Chunk> GetChunksToRender()
        {
            chunks.Clear();
            chunks = null;

            regions.Clear();
            regions = null;

            GetQuadrantChunks();
            GetCardinalChunks();
            Region chunkRegion = RegionManager.GetGlobalRegionFromChunkCoords((int) playerChunk.xLoc, (int) playerChunk.zLoc);
            Chunk chunk = chunkRegion.GetChunkWithLocation(new(playerChunk.xLoc, 0, playerChunk.zLoc));
            if (!chunkRegion.GetChunks().Contains(playerChunk))
            {
                chunkRegion.BinaryInsertChunkWithLocation(0, chunkRegion.GetChunks().Count - 1, new(playerChunk.xLoc, 0, playerChunk.zLoc));
                chunks.Add(playerChunk);
            }


            return chunks;
        }

        public static int GetModeBuffer()
        {
            return uboModelMatrices;
        }
        public static void ReRender(bool b)
        {
            reRenderFlag = b;
        }
        public static List<Region> GetRegions()
        {
            return regions;
        }

    }

}
