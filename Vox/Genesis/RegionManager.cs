using System.ComponentModel;
using System.Drawing;
using System.Security.Cryptography;
using MessagePack;
using Newtonsoft.Json;
using OpenTK.Mathematics;

namespace Vox.Genesis
{

    public class RegionManager : List<Region>
    {
        public static List<Region> VisibleRegions = [];
        private static string worldDir = "";
        public static readonly int CHUNK_HEIGHT = 400;
        private static int RENDER_DISTANCE = 12;
        public static readonly int REGION_BOUNDS = 512;
        public static readonly int CHUNK_BOUNDS = 16;
        public static long WORLD_SEED;
        private static object lockObj = new();
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
        public static void LeaveRegion(Region r)
        {

            WriteRegion(r);
            Logger.Info($"Writing {r}");
            VisibleRegions.Remove(r);

            //mark for garbage collection
            r = null;
        }

        /**
         * Generates or loads an already generated region from filesystem when the players
         * render distance intersects with the regions bounds.
         */
        public static Region EnterRegion(Region r)
        {
            //If region is already visible
            if (VisibleRegions.Contains(r))
                return r;

            //Gets region from files if it's written to file but not visible
            Region region = TryGetRegionFromFile(r.x, r.z);
            VisibleRegions.Add(region);
            return r;

        }

        public static void WriteRegion(Region r)
        {
            string path = Path.Combine(worldDir, "regions", $"{r.GetBounds().X}.{r.GetBounds().Y}.dat");

            byte[] serializedRegion = MessagePackSerializer.Serialize(r);
            
            File.WriteAllBytes(path, serializedRegion);

            //mark for garbage collection
            r = null;
        }

        public static Region? ReadRegion(string path)
        {
            byte[] serializedData = File.ReadAllBytes(path);

            // Deserialize the byte array back into an object
            Region region = MessagePackSerializer.Deserialize<Region>(serializedData);

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
            List<Region> updatedRegions = ChunkCache.GetRegions();

            if (VisibleRegions.Count > 0)
            {

                //Retrieves from file or generates any region that is visible
                for (int i = 0; i < updatedRegions.Count; i++)
                {
                    //Enter region if not found in visible regions
                   if (!VisibleRegions.Contains(updatedRegions[i]))
                        EnterRegion(updatedRegions[i]);
                }

                //Write to file and de-render any regions that are no longer visible
                for (int i = 0; i < VisibleRegions.Count; i++)
                {
                    if (!updatedRegions.Contains(VisibleRegions[i]))
                        LeaveRegion(VisibleRegions[i]);
                }
            }
        }
      
        /**
         * Attempts to get a region from file.
         * Returns an empty region to write later if it theres no file to read.
         */
        public static Region TryGetRegionFromFile(int x, int z)
        {
            //Check if region is already in files
            string path = Path.Combine(worldDir, "regions", x + "." + z + ".dat");
            bool exists = File.Exists(path);

            if (exists)
            {
                Logger.Info($"Reading Existing Region at {x},{z}");
                return ReadRegion(path);
            }
            else
            {
               
                Logger.Info($"Generating New Region at   {x},{z}");
                return new Region(x, z);
                
            }
        }

        
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


            //Calculates region coordinates player inhabits
            int regionXCoord = xUpperLimit;
            int regionZCoord = zUpperLimit;

            lock (lockObj)
            {
                foreach (Region region in VisibleRegions)
                {
                    if (regionXCoord == region.x && regionZCoord == region.z)
                    {
                        returnRegion = region;
                    }
                }
            }

            returnRegion ??= new Region(xUpperLimit, zUpperLimit);

            return returnRegion;
        }

        public static Chunk GetGlobalChunkFromCoords(int x, int z)
        {
            //Calculates chunk coordinates
            int chunkXCoord = x / CHUNK_BOUNDS * CHUNK_BOUNDS;
            int chunkZCoord = z / CHUNK_BOUNDS * CHUNK_BOUNDS;

            Vector3 chunkLoc = new(chunkXCoord, 0, chunkZCoord);
            foreach (Region region in VisibleRegions)
            {
                Chunk output = region.BinarySearchChunkWithLocation(0, region.GetChunks().Count - 1, chunkLoc);
                try
                {
                    if (!output.Equals(null))
                    {
                        return output;
                    }
                }
                catch (NullReferenceException)
                {
                   continue;
                }
            }

            return new Chunk().Initialize(x, z);

        }


    }
}

