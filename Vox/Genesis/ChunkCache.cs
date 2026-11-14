
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

        private static int _renderDistance;
        private static Chunk? _playerChunk;        
        private static readonly Dictionary<string, Chunk> Chunks = [];
        private static readonly Dictionary<string, Region> Regions = [];
        private static readonly object ChunkLock = new();


        private Vector3 _maXnegativeCorner = Vector3.Zero;
        private Vector3 _maXpositiveCorner = Vector3.Zero;

        private Vector3 _maXnegativeCornerOld = Vector3.Zero;
        private Vector3 _maXpositiveCornerOld = Vector3.Zero;
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

            _renderDistance = _regionManager!.GetRenderDistance();
            _playerChunk = playerChunk;

        }
        public void SetRenderDistance(int renderDistance)
        {
            ChunkCache._renderDistance = renderDistance;
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
            if (!Chunks.ContainsKey($"{_playerChunk!.XLoc}|{_playerChunk.YLoc}|{_playerChunk.ZLoc}"))
                Chunks.Add($"{_playerChunk.XLoc}|{_playerChunk.YLoc}|{_playerChunk.ZLoc}", _playerChunk);

            _maXnegativeCornerOld = _maXnegativeCorner;
            _maXpositiveCornerOld = _maXpositiveCorner;

            _maXnegativeCorner = new(_playerChunk.XLoc - (bounds * _renderDistance), _playerChunk.YLoc - (bounds * _renderDistance), _playerChunk.ZLoc - (bounds * _renderDistance));
            _maXpositiveCorner = new(_playerChunk.XLoc + (bounds * _renderDistance), _playerChunk.YLoc + (bounds * _renderDistance), _playerChunk.ZLoc + (bounds * _renderDistance));



            for (int radius = 1; radius <= _renderDistance; radius++)
            {
                
                Vector3 negativeCorner = new(
                    _playerChunk!.XLoc - (bounds * radius),
                    _playerChunk.YLoc - (bounds * radius),
                    _playerChunk.ZLoc - (bounds * radius));

                Vector3 positiveCorner = new(
                    _playerChunk.XLoc + (bounds * radius),
                    _playerChunk.YLoc + (bounds * radius),
                    _playerChunk.ZLoc + (bounds * radius));
                
                
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
            Chunks.Clear();
            Regions.Clear();
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

            lock (ChunkLock)
            {
                Chunk c = _regionManager.GetAndLoadGlobalChunkFromCoords(x, y, z);
                
                if (!chunkRegion.Chunks.ContainsKey($"{x}|{y}|{z}"))
                    chunkRegion.Chunks.Add($"{x}|{y}|{z}", c);
                


                if (!Chunks.ContainsKey($"{x}|{y}|{z}"))
                    Chunks.Add($"{x}|{y}|{z}", c);
                

            }
            if (!Regions.ContainsKey(_regionManager.GetRegionIndexFromChunkCoords(x, z))) 
                Regions.Add(_regionManager.GetRegionIndexFromChunkCoords(x, z), chunkRegion);
        }

        /**
         * Returns a list of chunks that should be rendered around a player based on a render distance value
         * and updates chunks that surround a player in a global scope.
         * @return The list of chunks that should be rendered.
         */
        public Dictionary<string, Chunk> UpdateChunkCache()
        {
            GetRadialChunks();

            Box3 prevCache = new(_maXnegativeCornerOld, _maXpositiveCornerOld);
            Box3 newCache = new(_maXnegativeCorner, _maXpositiveCorner);

            foreach (var region in _regionManager.GetVisibleRegions())
            {
                foreach (var chunk in region.Value.Chunks)
                { 
                    Vector3 pos = chunk.Value.GetLocation();
                    bool wasInCache = prevCache.ContainsInclusive(pos);
                    bool nowInCache = newCache.ContainsInclusive(pos);

                    if ((!wasInCache && !nowInCache) || (wasInCache && nowInCache))
                        continue;
                    
                    TriggerCacheEvent(chunk.Value, wasInCache, nowInCache);
                }
            }

            return Chunks;
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
            return Regions;
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
