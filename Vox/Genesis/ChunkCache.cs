using System.Diagnostics;
using OpenTK.Mathematics;
using Vox.Model;
using Vox.Rendering;

namespace Vox.Genesis
{
    public class ChunkCache
    {
        private static int renderDistance = RegionManager.GetRenderDistance();
        private static int bounds = RegionManager.CHUNK_BOUNDS;
        private static Chunk? playerChunk;        
        private static bool reRenderFlag = false;
        private static Dictionary<string, Chunk> chunks = [];
        private static Dictionary<string, Region> regions = [];
        private static TerrainVertex[] cacheVertexRenderData;
        private static int[] cacheElementRenderData;
        private static int vertexDataSizeEstimation = 0;
        private static int elementDataSizeEstimation = 0;
        private static int vertexCounter = 0;
        private static int elementCount = 0;
        private static int elementCounterTotal = 0;
  

        private static readonly object lockObj = new();

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
         * Generates or regenerates this Chunks render data. Calling this method will, if
         * needed, automatically update this chunks vertex and element data updating
         * the next frames render data.
         *
         */

        public static TerrainRenderTask GenerateRenderData(Chunk chunk)
        {

            List<Vector3> nonInterpolated = [];
            List<TerrainVertex> vertices = [];
            List<int> elements = [];
        
            Array values = Enum.GetValues(typeof(BlockType));
        
            Random random = new();
        
            if (chunk.didChange || chunk.renderTask == null)
            {
                for (int x = 0; x < chunk.heightMap.GetLength(0); x++) //rows          
                    for (int z = 0; z < chunk.heightMap.GetLength(1); z++) //columns
                        nonInterpolated.Add(new Vector3(chunk.GetLocation().X + x, chunk.heightMap[z, x], chunk.GetLocation().Z + z));
        
        
        
                //Add any player placed blocks to the mesh before interpolating
                for (int i = 0; i < chunk.blocksToAdd.Count; i += 3)
                    nonInterpolated.Add(new(chunk.blocksToAdd[i], chunk.blocksToAdd[i + 1], chunk.blocksToAdd[i + 2]));
        
                //Interpolate chunk heightmap
                List<Vector3> interpolatedChunk = chunk.InterpolateChunk(nonInterpolated);
        
                //Remove any blocks from the mesh marked for exclusion.
                //(i.e player broke a block)
                for (int i = 0; i < chunk.blocksToExclude.Count; i += 3)
                    interpolatedChunk.Remove(new(chunk.blocksToExclude[i], chunk.blocksToExclude[i + 1], chunk.blocksToExclude[i + 2]));
                

                int randomIndex = random.Next(values.Length);
                BlockType? randomBlock;
                if (randomIndex > 1)
                    randomBlock = (BlockType?)values.GetValue(randomIndex - 2);
                else
                    randomBlock = (BlockType?)values.GetValue(randomIndex);
        
                BlockType blockType = (BlockType)randomBlock;

                for (int x = 0; x < RegionManager.CHUNK_BOUNDS; x++)
                {
                    for (int z = 0; z < RegionManager.CHUNK_BOUNDS; z++)
                    {
        
                        foreach (Vector3 v in interpolatedChunk)
                        {

                            //Top face check X and Z
                            if (v.X == (int)chunk.xLoc + x && v.Z == (int)chunk.zLoc + z)
                            {
                                bool up = interpolatedChunk.Contains(new(v.X, v.Y + 1, v.Z));
                                if (!up)
                                {
                                    TerrainVertex[] Vlist = ModelUtils.GetCuboidFace(blockType, Face.UP, new Vector3(x + chunk.xLoc, v.Y, z + chunk.zLoc), chunk);
                                    for (int i = 0; i < Vlist.Length; i++) 
                                        cacheVertexRenderData[vertexCounter + i] = Vlist[i];
                                    vertexCounter += Vlist.Length;
        
                                    int[] eList = [elementCount, elementCount + 1, elementCount + 2, elementCount + 3, Window.primRestart];
                                    for (int i = 0; i < eList.Length; i++)
                                        cacheElementRenderData[elementCounterTotal + i] = eList[i];
                                    
                                    lock (lockObj)
                                    {
                                        elementCount += 4;
                                        elementCounterTotal += 5;
                                    }
                                }
        
                                //East face check z + 1
                                bool east = interpolatedChunk.Contains(new(v.X, v.Y, v.Z + 1));
                                if (!east)
                                {
                                    TerrainVertex[] Vlist = ModelUtils.GetCuboidFace(blockType, Face.EAST, new Vector3(x + chunk.xLoc, v.Y, z + chunk.zLoc), chunk);
                                    for (int i = 0; i < Vlist.Length; i++)
                                        cacheVertexRenderData[vertexCounter + i] = Vlist[i];
                                    vertexCounter += Vlist.Length;
        
                                    int[] eList = [elementCount, elementCount + 1, elementCount + 2, elementCount + 3, Window.primRestart];
                                    for (int i = 0; i < eList.Length; i++)
                                        cacheElementRenderData[elementCounterTotal + i] = eList[i];
                                    
                                    lock (lockObj)
                                    {
                                        elementCount += 4;
                                        elementCounterTotal += 5;
                                    }
                                }
        
                                //West face check z - 1
                                bool west = interpolatedChunk.Contains(new(v.X, v.Y, v.Z - 1));
                                if (!west)
                                {
                                    TerrainVertex[] Vlist = ModelUtils.GetCuboidFace(blockType, Face.WEST, new Vector3(x + chunk.xLoc, v.Y, z + chunk.zLoc), chunk);
                                    for (int i = 0; i < Vlist.Length; i++)
                                        cacheVertexRenderData[vertexCounter + i] = Vlist[i];
                                    vertexCounter += Vlist.Length;
        
                                    int[] eList = [elementCount, elementCount + 1, elementCount + 2, elementCount + 3, Window.primRestart];
                                    for (int i = 0; i < eList.Length; i++)
                                        cacheElementRenderData[elementCounterTotal + i] = eList[i];

                                    lock (lockObj)
                                    {
                                        elementCount += 4;
                                        elementCounterTotal += 5;
                                    }
                                }
        
                                //North face check x + 1
                                bool north = interpolatedChunk.Contains(new(v.X + 1, v.Y, v.Z));
                                if (!north)
                                {
                                    TerrainVertex[] Vlist = ModelUtils.GetCuboidFace(blockType, Face.NORTH, new Vector3(x + chunk.xLoc, v.Y, z + chunk.zLoc), chunk);
                                    for (int i = 0; i < Vlist.Length; i++)
                                        cacheVertexRenderData[vertexCounter + i] = Vlist[i];
                                    vertexCounter += Vlist.Length;
        
                                    int[] eList = [elementCount, elementCount + 1, elementCount + 2, elementCount + 3, Window.primRestart];
                                    for (int i = 0; i < eList.Length; i++)
                                        cacheElementRenderData[elementCounterTotal + i] = eList[i];

                                    lock (lockObj)
                                    {
                                        elementCount += 4;
                                        elementCounterTotal += 5;
                                    }
                                }
        
                                //South face check x - 1
                                bool south = interpolatedChunk.Contains(new(v.X - 1, v.Y, v.Z));
                                {
                                    TerrainVertex[] Vlist = ModelUtils.GetCuboidFace(blockType, Face.SOUTH, new Vector3(x + chunk.xLoc, v.Y, z + chunk.zLoc), chunk);
                                    for (int i = 0; i < Vlist.Length; i++)
                                        cacheVertexRenderData[vertexCounter + i] = Vlist[i];
                                    vertexCounter += Vlist.Length;
        
                                    int[] eList = [elementCount, elementCount + 1, elementCount + 2, elementCount + 3, Window.primRestart];
                                    for (int i = 0; i < eList.Length; i++)
                                        cacheElementRenderData[elementCounterTotal + i] = eList[i];

                                    lock (lockObj)
                                    {
                                        elementCount += 4;
                                        elementCounterTotal += 5;
                                    }
                                }
                                //Bottom face
                                bool bottom = interpolatedChunk.Contains(new(v.X, v.Y - 1, v.Z));
                                if (Window.GetPlayer().GetPosition().Y < v.Y && !bottom)
                                {
                                    TerrainVertex[] Vlist = ModelUtils.GetCuboidFace(blockType, Face.DOWN, new Vector3(x + chunk.xLoc, v.Y, z + chunk.zLoc), chunk);
                                    for (int i = 0; i < Vlist.Length; i++)
                                        cacheVertexRenderData[vertexCounter + i] = Vlist[i];
                                    vertexCounter += Vlist.Length;
        
                                    int[] eList = [elementCount, elementCount + 1, elementCount + 2, elementCount + 3, Window.primRestart];
                                    for (int i = 0; i < eList.Length; i++)
                                        cacheElementRenderData[elementCounterTotal + i] = eList[i];

                                    lock (lockObj)
                                    {
                                        elementCount += 4;
                                        elementCounterTotal += 5;
                                    }
                                }
                            }
                        }
                    }
        
                }


                //Updates chunk data
                lock (lockObj)
                {
                    //O(n) because of ToArray
                    chunk.renderTask = new TerrainRenderTask(cacheVertexRenderData, cacheElementRenderData, chunk.GetVbo("Terrain"), chunk.GetEbo("Terrain"), chunk.GetVao("Terrain"));
                    chunk.didChange = false;
                }

               
            }

            

            return chunk.renderTask;
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
            for (int y = 0 - (renderDistance / 2); y <= 0 - (renderDistance / 2); y++)
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
            for (int y = 0 - (renderDistance / 2); y <= 0 - (renderDistance / 2); y++)
            {
                //Positive X
                for (int i = 1; i <= renderDistance; i++)
                {
                    int x = (int)playerChunk.GetLocation().X + (i * bounds);
                    int z = (int)playerChunk.GetLocation().Z;
                    CacheHelper(x, y, z);
                }

                //Negative X
                for (int i = 1; i <= renderDistance; i++)
                {
                    int x = (int)playerChunk.GetLocation().X - (i * bounds);
                    int z = (int)playerChunk.GetLocation().Z;
                    CacheHelper(x, y, z);
                }

                //Positive Z
                for (int i = 1; i <= renderDistance; i++)
                {
                    int x = (int)playerChunk.GetLocation().X;
                    int z = (int)playerChunk.GetLocation().Z + (i * bounds);
                    CacheHelper(x, y, z);
                }
                //Negative Z
                for (int i = 1; i <= renderDistance; i++)
                {

                    int x = (int)playerChunk.GetLocation().X;
                    int z = (int)playerChunk.GetLocation().Z - (i * bounds);
                    CacheHelper(x, y, z);
                }
            }
        }

        /**
         * Returns a list of chunks that should be rendered around a player based on a render distance value
         * and updates chunks that surround a player in a global scope.
         * @return The list of chunks that should be rendered.
         */
        public static Dictionary<string, Chunk> UpdateChunkCache()
        {
            chunks.Clear();
            regions.Clear();

            GetQuadrantChunks();
            GetCardinalChunks();
            chunks.Add($"{playerChunk.xLoc}|{playerChunk.yLoc}|{playerChunk.zLoc}", playerChunk);

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
