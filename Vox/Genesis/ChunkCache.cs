
using System.Linq;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Vox.Genesis
{
    public class ChunkCache
    {
        private static int renderDistance = RegionManager.RENDER_DISTANCE;
        private static int bounds = RegionManager.CHUNK_BOUNDS;
        private static Chunk? playerChunk;
        private static readonly List<Region> regions = [];
        private static int uboModelMatrices;
        private static int maxMatrices = 0;
        private static bool reRenderFlag = false;
        private static List<Chunk> chunks = [];
        Matrix4[] modelMatrices;

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

        private static List<Chunk> GetChunksInCache()
        {
            if (reRenderFlag)
            {
                List<Chunk> allChunks = [];
                Region playerRegion = Window.GetPlayer().GetRegionWithPlayer();

                int startX = (int)(playerChunk.GetLocation().X / bounds) * bounds;
                int startZ = (int)(playerChunk.GetLocation().Z / bounds) * bounds;
                int renderMax = renderDistance * 2 + 1;

                for (int i = startX; i < renderMax * bounds; i += bounds)
                {
                    for (int j = startZ; j < renderMax * bounds; j += bounds)
                    {

                        //   Chunk c = new Chunk().Initialize(i, j);

                        Region r = RegionManager.GetGlobalRegionFromChunkCoords(i, j);


                        if (playerRegion.GetChunkWithLocation(new(i, 0, j)) == null)
                            playerRegion.BinaryInsertChunkWithLocation(0, playerRegion.Count - 1, new(i, 0, j));
                        else
                            allChunks.Add(playerRegion.GetChunkWithLocation(new(i, 0, j)));

                       
                        if (!regions.Contains(r))
                            regions.Add(r);
                        // if (!regions.Contains(c.GetRegion()))
                        //     regions.Add(c.GetRegion());

                    }

                }
                chunks = allChunks;
            }
            else { return chunks; }

            reRenderFlag = false;
            return chunks;
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
                    chunks.AddRange(QuadrantHelper(x, z));
                }
            }


            //Top right quadrant
            Vector3 TRStart = new Vector3(playerChunk.GetLocation().X + bounds, 0, playerChunk.GetLocation().Z + bounds);
            for (int x = (int)TRStart.X; x < TRStart.X + (renderDistance * bounds); x += bounds)
            {
                for (int z = (int)TRStart.Z; z < TRStart.Z + (renderDistance * bounds); z += bounds)
                {
                    chunks.AddRange(QuadrantHelper(x, z));
                }
            }

            //Bottom right quadrant
            Vector3 BRStart = new Vector3(playerChunk.GetLocation().X - bounds, 0, playerChunk.GetLocation().Z - bounds);
            for (int x = (int)BRStart.X; x > BRStart.X - (renderDistance * bounds); x -= bounds)
            {
                for (int z = (int)BRStart.Z; z > BRStart.Z - (renderDistance * bounds); z -= bounds)
                {
                    chunks.AddRange(QuadrantHelper(x, z));
                }
            }

            //Bottom left quadrant
            Vector3 BLStart = new Vector3(playerChunk.GetLocation().X + bounds, 0, playerChunk.GetLocation().Z - bounds);
            for (int x = (int)BLStart.X; x < BLStart.X + (renderDistance * bounds); x += bounds)
            {
                for (int z = (int)BLStart.Z; z > BLStart.Z - (renderDistance * bounds); z -= bounds)
                {
                    chunks.AddRange(QuadrantHelper(x, z));
                }
            }

            return chunks;
        }

        private static List<Chunk> QuadrantHelper(int x, int z)
        {
            Region chunkRegion = RegionManager.GetGlobalRegionFromChunkCoords(x, z);
            Chunk chunk = chunkRegion.GetChunkWithLocation(new Vector3(x, 0, z));
            List<Chunk> chunks = [];

            if (chunk != null)
            {
                chunks.Add(chunk);
                if (!regions.Contains(chunkRegion))
                    regions.Add(chunkRegion);

                if (!chunkRegion.Contains(chunk))
                    chunkRegion.BinaryInsertChunkWithLocation(0, chunkRegion.Count - 1, chunk.GetLocation());

            }
            else
            {
                chunk = new Chunk().Initialize(x, z);
                chunks.Add(chunk);
                chunkRegion.BinaryInsertChunkWithLocation(0, chunkRegion.Count - 1, chunk.GetLocation());
                if (!regions.Contains(chunk.GetRegion()))
                    regions.Add(chunkRegion);
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
            List<Chunk> chunks = [];
            regions.Clear();
            chunks.AddRange(GetQuadrantChunks());
            chunks.AddRange(GetCardinalChunks());
            chunks.Add(playerChunk);

            foreach (Chunk c in chunks)
              {
             //   Logger.Debug(c.ToString());
               }

            return chunks;
        }
        public static void UpdateChunkModelBuffer()
        {
            Matrix4[] modelMatrices = new Matrix4[maxMatrices];


            int uniformBlockIndex = GL.GetUniformBlockIndex(Window.GetShaders().GetProgramId(), "chunModelUBO");
            GL.UniformBlockBinding(Window.GetShaders().GetProgramId(), uniformBlockIndex, 0);
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, uboModelMatrices);

            // Update the buffer with new data (dynamic updates)
            IntPtr ptr = GL.MapBuffer(BufferTarget.UniformBuffer, BufferAccess.WriteOnly);
            if (ptr != IntPtr.Zero)
            {
                // Calculate the size to copy
                int sizeToCopy = modelMatrices.Length * (sizeof(float) * 16);

                // Copy the new matrices into the buffer
                // Use Marshal to copy the array to unmanaged memory
                GCHandle handle = GCHandle.Alloc(modelMatrices, GCHandleType.Pinned);
                unsafe
                {
                    try
                    {
                        // Copy the data from the managed array to the unmanaged buffer
                        IntPtr sourcePtr = handle.AddrOfPinnedObject();
                        System.Buffer.MemoryCopy(
                            sourcePtr.ToPointer(),
                            ptr.ToPointer(),
                            sizeToCopy,
                            sizeToCopy
                        );
                    }
                    finally
                    {
                        handle.Free(); // Free the handle after copying
                    }
                }
            }



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
           // foreach (Region region in regions)
         //   {
           //     Logger.Debug(region.ToString());
         //   }
            return regions;
        }

    }

}
