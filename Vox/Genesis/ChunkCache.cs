
using OpenTK.Mathematics;
using Vox.Events;
using Vox.Exceptions;
using Vox.Rendering;

namespace Vox.Genesis
{
    public class ChunkCache : IChunkCache
    {
        private readonly IRegionManager _regionManager;
        private readonly CacheEventHandler _cacheEventHandler;

        private static int renderDistance;
        private static Chunk? _playerChunk;        
        private static readonly Dictionary<string, Chunk> chunks = [];
        private static readonly Dictionary<string, Region> regions = [];
        private static readonly object chunkLock = new();


        private Vector3 MAXnegativeCorner = Vector3.Zero;
        private Vector3 MAXpositiveCorner = Vector3.Zero;

        private Vector3 MAXnegativeCornerOld = Vector3.Zero;
        private Vector3 MAXpositiveCornerOld = Vector3.Zero;
        /**
         *
         * Updates, stores, and returns a list of in-memory
         * chunks that should be rendered around a player in a frame.
         * @param bounds The length, width, and height of the cubic chunk.
         * @param playerChunk The chunk a player inhabits.
         */
        public ChunkCache(Chunk? playerChunk, IRegionManager regionManager, CacheEventHandler cacheEventHandler)
        {
            _regionManager = regionManager;
            _cacheEventHandler = cacheEventHandler;

            renderDistance = _regionManager!.GetRenderDistance();
            _playerChunk = playerChunk;

        }
        public void SetRenderDistance(int renderDistance)
        {
            ChunkCache.renderDistance = renderDistance;
        }
        public void SetPlayerChunk(Chunk c)
        {
            _playerChunk = c;
        }

        /**
         * Returns a list of chunks around a player based on a render distance and radius value
         * and updates chunks that surround a player in a world space
         * @return The list of chunks that should be rendered.
         */
        public void GetRadialChunks()
        {
            ClearChunkCache();
            int bounds = _regionManager.GetChunkBounds();

            //Check each radius layer
            if (!chunks.ContainsKey($"{_playerChunk!.xLoc}|{_playerChunk.yLoc}|{_playerChunk.zLoc}"))
                chunks.Add($"{_playerChunk.xLoc}|{_playerChunk.yLoc}|{_playerChunk.zLoc}", _playerChunk);

            MAXnegativeCornerOld = MAXnegativeCorner;
            MAXpositiveCornerOld = MAXpositiveCorner;

            MAXnegativeCorner = new(_playerChunk.xLoc - (bounds * renderDistance), _playerChunk.yLoc - (bounds * renderDistance), _playerChunk.zLoc - (bounds * renderDistance));
            MAXpositiveCorner = new(_playerChunk.xLoc + (bounds * renderDistance), _playerChunk.yLoc + (bounds * renderDistance), _playerChunk.zLoc + (bounds * renderDistance));



            for (int radius = 1; radius <= renderDistance; radius++)
            {


                Vector3 negativeCorner = new(_playerChunk.xLoc - (bounds * radius), _playerChunk.yLoc - (bounds * radius), _playerChunk.zLoc - (bounds * radius));
                Vector3 positiveCorner = new(_playerChunk.xLoc + (bounds * radius), _playerChunk.yLoc + (bounds * radius), _playerChunk.zLoc + (bounds * radius));

                //Iterates from the farthest -X point to the farthest +X

                for (int x = (int)negativeCorner.X; x <= positiveCorner.X; x += bounds)
                {
                    //Iterates from the farthest -Y point to the farthest +Y
                    for (int y = (int)negativeCorner.Y; y <= positiveCorner.Y ; y += bounds)
                    {
                        //Iterates from the farthest -Z point to the farthest +Z
                        for (int z = (int)negativeCorner.Z; z <= positiveCorner.Z; z += bounds)
                        {
                            CacheHelper(x, y, z);
                        }
                    }
                }
            }
        }

        /**
         * Clears and resets cache without repopulating it
         */
        public void ClearChunkCache()
        {
            chunks.Clear();
            regions.Clear();
        }
        private void CacheHelper(int x, int y, int z)
        {

            string regionIdx = _regionManager.GetRegionIndexFromChunkCoords(x, z);

            //Look for region in loaded regions
            _regionManager.GetVisibleRegions().TryGetValue(regionIdx, out Region? chunkRegion);

            //if region is still null, try to get from file
            if (chunkRegion == null)
            {
                chunkRegion = _regionManager.TryGetRegionFromFile(regionIdx);
                _regionManager.EnterRegion(chunkRegion); //cache region in memory for future additions to chunk list
            }

            lock (chunkLock)
            {
                Chunk c = _regionManager.GetAndLoadGlobalChunkFromCoords(x, y, z);

                if (!chunkRegion.chunks.ContainsKey($"{x}|{y}|{z}"))
                    chunkRegion.chunks.Add($"{x}|{y}|{z}", c);

                if (!chunks.ContainsKey($"{x}|{y}|{z}"))
                    chunks.Add($"{x}|{y}|{z}", c);            
            }
            if (!regions.ContainsKey(_regionManager.GetRegionIndexFromChunkCoords(x, z))) 
                regions.Add(_regionManager.GetRegionIndexFromChunkCoords(x, z), chunkRegion);
        }

        /**
         * Returns a list of chunks that should be rendered around a player based on a render distance value
         * and updates chunks that surround a player in a global scope.
         * @return The list of chunks that should be rendered.
         */
        public Dictionary<string, Chunk> UpdateChunkCache()
        {
            GetRadialChunks();

            Box3 prevCache = new(MAXnegativeCornerOld, MAXpositiveCornerOld);
            Box3 newCache = new(MAXnegativeCorner, MAXpositiveCorner);

            foreach (var region in _regionManager.GetVisibleRegions())
            {
                foreach (var chunk in region.Value.chunks)
                { 
                    Vector3 pos = chunk.Value.GetLocation();
                    bool wasInCache = prevCache.ContainsInclusive(pos);
                    bool nowInCache = newCache.ContainsInclusive(pos);

                    if ((!wasInCache && !nowInCache) || (wasInCache && nowInCache))
                        continue;

                    TriggerCacheEvent(chunk.Value, wasInCache, nowInCache);
                }
            }

            return chunks;
        }

        private void TriggerCacheEvent(Chunk chunk, bool wasInCache, bool nowInCache)
        {

            //If in previous cache but not in new cache, player has left the chunk and it should be de-cached
            if (wasInCache && !nowInCache)
            {
                _cacheEventHandler.RemoveFromCache(chunk);
                return;
            }
            //If in new cache but not in previous cache, player has entered the chunk and it should be cached
            else if (!wasInCache && nowInCache)
            {
                _cacheEventHandler.CacheChunk(chunk);
                return;
            }
        }

        public Dictionary<string, Region> GetRegions()
        {
            return regions;
        }

        /**
         * The ChunkCache will update the regions in memory, storing them as potentially blank objects
         * if the region was not already in memory. This method is responsible for reading region data
         * into these blank region objects when in memory and writing data to the file
         * system for future use when the player no longer inhabits them.
         *
         */
        public void UpdateVisibleRegions()
        {
            //Updates regions within render distance
            UpdateChunkCache();
            Dictionary<string, Region> updatedRegions = GetRegions();

            if (_regionManager.GetVisibleRegions().Count > 0)
            {

                //Retrieves from file or generates any region that is visible
                for (int i = 0; i < updatedRegions.Count; i++)
                {
                    //Enter region if not found in visible regions
                    if (!_regionManager.GetVisibleRegions().ContainsKey(updatedRegions.Keys.ElementAt(i)))
                        _regionManager.EnterRegion(updatedRegions.Keys.ElementAt(i));
                }

                //Write to file and de-render any regions that are no longer visible
                for (int i = 0; i < _regionManager.GetVisibleRegions().Count; i++)
                {
                    if (!updatedRegions.ContainsKey(_regionManager.GetVisibleRegions().Keys.ElementAt(i)))
                    {
                        _regionManager.LeaveRegion(_regionManager.GetVisibleRegions().Keys.ElementAt(i));
                    }
                }
            }
        }


    }

}
