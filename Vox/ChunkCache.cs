
using OpenTK.Mathematics;
using Vox.World;

namespace Vox
{
    public class ChunkCache
    {
        private static int renderDistance = RegionManager.RENDER_DISTANCE;
        private static int bounds = RegionManager.CHUNK_BOUNDS;
        private static Chunk? playerChunk;
        private static readonly List<Region> regions = [];

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
        private static List<Chunk> GetQuadrantChunks()
        {
            List<Chunk> chunks = [];
            Region playerRegion = Window.GetPlayer().GetRegionWithPlayer();

            //Top left quadrant
            Vector3 TLstart = new Vector3(playerChunk.GetLocation().X - bounds, 0, playerChunk.GetLocation().Z + bounds);
            for (int x = (int)TLstart.X; x > TLstart.X - (renderDistance * bounds); x -= bounds)
            {
                for (int z = (int)TLstart.Z; z < TLstart.Z + (renderDistance * bounds); z += bounds)
                {
                    Chunk c = playerRegion.GetChunkWithLocation(new Vector3(x, 0, z));
                    if (c != null)
                    {
                        chunks.Add(c);
                        if (!regions.Contains(c.GetRegion()))
                            regions.Add(c.GetRegion());
                    }
                    else
                    {
                        //Attempts to get chunk from region
                        Chunk get = playerRegion.GetChunkWithLocation(new Vector3(x, 0, z));

                        //If chunk already exists in region, add it
                        if (get != null)
                        {
                            chunks.Add(get);
                            if (!regions.Contains(get.GetRegion()))
                                regions.Add(get.GetRegion());

                            //If chunk does not already exist in region, create and add it
                        }
                        else
                        {
                            Chunk w = playerRegion.BinaryInsertChunkWithLocation(0, playerRegion.Count - 1, new Vector3(x, 0, z));
                            if (w != null)
                            {
                                chunks.Add(w);

                                if (!regions.Contains(w.GetRegion()))
                                    regions.Add(w.GetRegion());
                            }
                        }
                    }
                }
            }


            //Top right quadrant
            Vector3 TRStart = new Vector3(playerChunk.GetLocation().X + bounds, 0, playerChunk.GetLocation().Z + bounds);
            for (int x = (int)TRStart.X; x < TRStart.X + (renderDistance * bounds); x += bounds)
            {
                for (int z = (int)TRStart.Z; z < TRStart.Z + (renderDistance * bounds); z += bounds)
                {
                    Chunk c = playerRegion.GetChunkWithLocation(new Vector3(x, 0, z));
                    if (c != null)
                    {
                        chunks.Add(c);
                        if (!regions.Contains(c.GetRegion()))
                            regions.Add(c.GetRegion());
                    }
                    else
                    {
                        //Attempts to get chunk from region
                        Chunk get = playerRegion.GetChunkWithLocation(new Vector3(x, 0, z));

                        //If chunk already exists in region, add it
                        if (get != null)
                        {
                            chunks.Add(get);
                            if (!regions.Contains(get.GetRegion()))
                                regions.Add(get.GetRegion());


                            //If chunk does not already exist in region, create and add it
                        }
                        else
                        {
                            Chunk w = playerRegion.BinaryInsertChunkWithLocation(0, playerRegion.Count - 1, new Vector3(x, 0, z));
                            if (w != null)
                            {
                                chunks.Add(w);

                                if (!regions.Contains(w.GetRegion()))
                                    regions.Add(w.GetRegion());
                            }
                        }
                    }
                }
            }

            //Bottom right quadrant
            Vector3 BRStart = new Vector3(playerChunk.GetLocation().X - bounds, 0, playerChunk.GetLocation().Z - bounds);
            for (int x = (int)BRStart.X; x > BRStart.X - (renderDistance * bounds); x -= bounds)
            {
                for (int z = (int)BRStart.Z; z > BRStart.Z - (renderDistance * bounds); z -= bounds)
                {
                    Chunk c = playerRegion.GetChunkWithLocation(new Vector3(x, 0, z));
                    if (c != null)
                    {
                        chunks.Add(c);
                        if (!regions.Contains(c.GetRegion()))
                            regions.Add(c.GetRegion());
                    }
                    else
                    {
                        //Attempts to get chunk from region
                        Chunk get = playerRegion.GetChunkWithLocation(new Vector3(x, 0, z));

                        //If chunk already exists in region, add it
                        if (get != null)
                        {
                            chunks.Add(get);
                            if (!regions.Contains(get.GetRegion()))
                                regions.Add(get.GetRegion());

                            //If chunk does not already exist in region, create and add it
                        }
                        else
                        {
                            Chunk w = playerRegion.BinaryInsertChunkWithLocation(0, playerRegion.Count - 1, new Vector3(x, 0, z));
                            if (w != null)
                            {
                                chunks.Add(w);

                                if (!regions.Contains(w.GetRegion()))
                                    regions.Add(w.GetRegion());
                            }
                        }
                    }
                }
            }

            //Bottom left quadrant
            Vector3 BLStart = new Vector3(playerChunk.GetLocation().X + bounds, 0, playerChunk.GetLocation().Z - bounds);
            for (int x = (int)BLStart.X; x < BLStart.X + (renderDistance * bounds); x += bounds)
            {
                for (int z = (int)BLStart.Z; z > BLStart.Z - (renderDistance * bounds); z -= bounds)
                {
                    Chunk c = playerRegion.GetChunkWithLocation(new Vector3(x, 0, z));
                    if (c != null)
                    {
                        chunks.Add(c);
                        if (!regions.Contains(c.GetRegion()))
                            regions.Add(c.GetRegion());
                    }
                    else
                    {
                        //Attempts to get chunk from region
                        Chunk get = playerRegion.GetChunkWithLocation(new Vector3(x, 0, z));

                        //If chunk already exists in region, add it
                        if (get != null)
                        {
                            chunks.Add(get);
                            if (!regions.Contains(get.GetRegion()))
                                regions.Add(get.GetRegion());


                            //If chunk does not already exist in region, create and add it
                        }
                        else
                        {
                            Chunk w = playerRegion.BinaryInsertChunkWithLocation(0, playerRegion.Count - 1, new Vector3(x, 0, z));
                            if (w != null)
                            {
                                chunks.Add(w);

                                if (!regions.Contains(w.GetRegion()))
                                    regions.Add(w.GetRegion());
                            }
                        }
                    }
                }
            }

            return chunks;
        }
        /**
         * Gets the chunks that should be rendered along the X And Z axis. E.X a renderer distance
         * of 2 would return 8 chunks, 2 on every side of the player in each cardinal direction
         *
         * @return A list of chunks that should be rendered in x, z, -x, and -z directions
         */
        private static List<Chunk> GetCardinalChunks()
        {
            List<Chunk> chunks = [];
            Region playerRegion = Window.GetPlayer().GetRegionWithPlayer();

            //Positive X
            for (int i = 1; i <= renderDistance; i++)
            {
                Vector3 p = new Vector3(playerChunk.GetLocation().X + (i * bounds), 0, playerChunk.GetLocation().Z);
                Chunk c = playerRegion.GetChunkWithLocation(p);
                if (c != null)
                {
                    chunks.Add(c);
                    if (!regions.Contains(c.GetRegion()))
                        regions.Add(c.GetRegion());
                }
                else
                {
                    //Search for existing chunk in region
                    Chunk get = playerRegion.BinarySearchChunkWithLocation(0, playerRegion.Count - 1, p);

                    //If chunk does not exist, create and add it
                    if (get == null)
                    {
                        Chunk d = playerRegion.BinaryInsertChunkWithLocation(0, playerRegion.Count - 1, p);
                        if (d != null)
                        {
                            chunks.Add(d);

                            if (!regions.Contains(d.GetRegion()))
                                regions.Add(d.GetRegion());
                        }
                        //If region already has chunk, get chunk do not add
                    }
                    else
                    {
                        chunks.Add(get);
                        if (!regions.Contains(get.GetRegion()))
                            regions.Add(get.GetRegion());
                    }
                }
            }

            //Negative X
            for (int i = 1; i <= renderDistance; i++)
            {
                Vector3 p = new Vector3(playerChunk.GetLocation().X - (i * bounds), 0, playerChunk.GetLocation().Z);
                Chunk c = playerRegion.GetChunkWithLocation(p);
                if (c != null)
                {
                    chunks.Add(c);
                    if (!regions.Contains(c.GetRegion()))
                        regions.Add(c.GetRegion());
                }
                else
                {
                    //Search for existing chunk in region
                    Chunk get = playerRegion.BinarySearchChunkWithLocation(0, playerRegion.Count - 1, p);

                    //If chunk does not exist, create and add it
                    if (get == null)
                    {
                        Chunk d = playerRegion.BinaryInsertChunkWithLocation(0, playerRegion.Count - 1, p);
                        if (d != null)
                        {
                            chunks.Add(d);

                            if (!regions.Contains(d.GetRegion()))
                                regions.Add(d.GetRegion());
                        }
                        //If region already has chunk, get chunk do not add
                    }
                    else
                    {
                        chunks.Add(get);
                        if (!regions.Contains(get.GetRegion()))
                            regions.Add(get.GetRegion());
                    }
                }
            }

            //Positive Y
            for (int i = 1; i <= renderDistance; i++)
            {
                Vector3 p = new Vector3(playerChunk.GetLocation().X, 0, playerChunk.GetLocation().Z + (i * bounds));
                Chunk c = playerRegion.GetChunkWithLocation(p);
                if (c != null)
                {
                    chunks.Add(c);
                    if (!regions.Contains(c.GetRegion()))
                        regions.Add(c.GetRegion());
                }
                else
                {
                    //Search for existing chunk in region
                    Chunk get = playerRegion.BinarySearchChunkWithLocation(0, playerRegion.Count - 1, p);

                    //If chunk does not exist, create and add it
                    if (get == null)
                    {
                        Chunk d = playerRegion.BinaryInsertChunkWithLocation(0, playerRegion.Count - 1, p);
                        if (d != null)
                        {
                            chunks.Add(d);

                            if (!regions.Contains(d.GetRegion()))
                                regions.Add(d.GetRegion());
                        }
                        //If region already has chunk, get chunk do not add
                    }
                    else
                    {
                        chunks.Add(get);
                        if (!regions.Contains(get.GetRegion()))
                            regions.Add(get.GetRegion());
                    }
                }
            }
            //Negative Y
            for (int i = 1; i <= renderDistance; i++)
            {
                Vector3 p = new Vector3(playerChunk.GetLocation().X, 0, playerChunk.GetLocation().Z - (i * bounds));
                Chunk c = playerRegion.GetChunkWithLocation(p);
                if (c != null)
                {
                    chunks.Add(c);
                    if (!regions.Contains(c.GetRegion()))
                        regions.Add(c.GetRegion());
                }
                else
                {
                    //Search for existing chunk in region
                    Chunk get = playerRegion.BinarySearchChunkWithLocation(0, playerRegion.Count - 1, p);

                    //If chunk does not exist, create and add it
                    if (get == null)
                    {
                        Chunk d = playerRegion.BinaryInsertChunkWithLocation(0, playerRegion.Count - 1, p);
                        if (d != null)
                        {
                            chunks.Add(d);

                            if (!regions.Contains(d.GetRegion()))
                                regions.Add(d.GetRegion());
                        }
                        //If region already has chunk, get chunk do not add
                    }
                    else
                    {
                        chunks.Add(get);
                        if (!regions.Contains(get.GetRegion()))
                            regions.Add(get.GetRegion());
                    }
                }
            }
            return chunks;
        }

        /**
         * Returns a list of chunks that should be rendered around a player based on a render distance value
         * and updates chunks that surround a player in a global scope.
         * @return The list of chunks that should be rendered.
         */
        public static List<Chunk> GetChunksToRender()
        {
            regions.Clear();
            List<Chunk> chunks = [];
            chunks.AddRange(GetQuadrantChunks());
            chunks.AddRange(GetCardinalChunks());
            chunks.Add(playerChunk);

            return chunks;
        }

        public static List<Region> getRegions()
        {
            return regions;
        }

    }

}
