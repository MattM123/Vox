
using OpenTK.Mathematics;

namespace Vox.Genesis
{
    public class ChunkCache
    {
        private static int renderDistance = RegionManager.GetRenderDistance();
        private static int bounds;
        private static Chunk? playerChunk;        
        private static Dictionary<string, Chunk> chunks = [];
        private static Dictionary<string, Region> regions = [];
        private static object chunkLock = new();

        /**
         *
         * Updates, stores, and returns a list of in-memory
         * chunks that should be rendered around a player in a frame.
         * @param bounds The length, width, and height of the cubic chunk.
         * @param playerChunk The chunk a player inhabits.
         */
        public ChunkCache(int bounds, Chunk playerChunk)
        {
            ChunkCache.bounds = bounds;
            ChunkCache.playerChunk = playerChunk;

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
         * Returns a list of chunks around a player based on a render distance and radius value
         * and updates chunks that surround a player in a world space
         * @return The list of chunks that should be rendered.
         */
        public static void GetRadialChunks()
        {
            int bounds = RegionManager.CHUNK_BOUNDS;

            //Check each radius layer
            if (!chunks.ContainsKey($"{playerChunk.xLoc}|{playerChunk.yLoc}|{playerChunk.zLoc}"))
                chunks.Add($"{playerChunk.xLoc}|{playerChunk.yLoc}|{playerChunk.zLoc}", playerChunk);

            for (int radius = 1; radius <= renderDistance; radius++)
            {
                Vector3 negativeCorner = new(playerChunk.xLoc - (bounds * radius), playerChunk.yLoc - (bounds * radius), playerChunk.zLoc - (bounds * radius));
                Vector3 positiveCorner = new(playerChunk.xLoc + (bounds * radius), playerChunk.yLoc + (bounds * radius), playerChunk.zLoc + (bounds * radius));

                //Iterates from the farthest -X point to the farthest +X

                for (int x = (int)negativeCorner.X; x <= positiveCorner.X; x += bounds)
                {
                    //Iterates from the farthest -Y point to the farthest +Y
                    for (int y = (int)negativeCorner.Y; y <= positiveCorner.Y ; y += bounds)
                    {
                        //Iterates from the farthest -Z point to the farthest +Z
                        for (int z = (int)negativeCorner.Z; z <= positiveCorner.Z; z += bounds)
                        {
                            //If the chunk is surrounded by surrounded by ungenerated chunks, dont cache it
                            //if (!RegionManager.GetGlobalRegionFromChunkCoords(x, z).chunks.ContainsKey($"{x + bounds}|{y}|{z}") ||
                            //    !RegionManager.GetGlobalRegionFromChunkCoords(x, z).chunks.ContainsKey($"{x}|{y + bounds}|{z}") ||
                            //    !RegionManager.GetGlobalRegionFromChunkCoords(x, z).chunks.ContainsKey($"{x}|{y}|{z + bounds}") ||
                            //    
                            //    !RegionManager.GetGlobalRegionFromChunkCoords(x, z).chunks.ContainsKey($"{x - bounds}|{y}|{z}") ||
                            //    !RegionManager.GetGlobalRegionFromChunkCoords(x, z).chunks.ContainsKey($"{x}|{y - bounds}|{z}") ||
                            //    !RegionManager.GetGlobalRegionFromChunkCoords(x, z).chunks.ContainsKey($"{x}|{y}|{z - bounds}"))
                            //{
                                CacheHelper(x, y, z);
                           // }
                        }
                    }
                }
            }
        }
        /**
         * Clears CPU SSBO storage for chunk and regenerates chunks in cache and update.
         * Used only if a block is removed and has to account for buffer data removal/update.
         */
        public static void RegenerateCache()
        {

            CountdownEvent countdown = new(chunks.Count);
            foreach (KeyValuePair<string, Chunk> c in chunks)
            {
                ThreadPool.QueueUserWorkItem(new WaitCallback(delegate (object state)
                {
                    c.Value.Reset();
                    countdown.Signal();
                }));
            }
            countdown.Wait();
        }

        /**
         * Clears and resets cache without repopulating it
         */
        public static void ClearChunkCache()
        {
            chunks.Clear();
            regions.Clear();
        }
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

            lock (chunkLock)
            {
                Chunk c = RegionManager.GetAndLoadGlobalChunkFromCoords(x, y, z);
                
                if (!chunkRegion.chunks.ContainsKey($"{x}|{y}|{z}"))
                    chunkRegion.chunks.Add($"{x}|{y}|{z}", c);
                
                if (!chunks.ContainsKey($"{x}|{y}|{z}"))
                    chunks.Add($"{x}|{y}|{z}", c);
            }
            if (!regions.ContainsKey(Region.GetRegionIndex(x, z))) 
                regions.Add(Region.GetRegionIndex(x, z), chunkRegion);
        }

        /**
         * Returns a list of chunks that should be rendered around a player based on a render distance value
         * and updates chunks that surround a player in a global scope.
         * @return The list of chunks that should be rendered.
         */
        public static Dictionary<string, Chunk> UpdateChunkCache()
        {
            GetRadialChunks();
            return chunks;
   }

        public static Dictionary<string, Region> GetRegions()
        {
            return regions;
        }

    }

}
