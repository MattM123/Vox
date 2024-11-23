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
        public static Dictionary<string, Region> VisibleRegions = [];
        private static string worldDir = "";
        public static readonly int CHUNK_HEIGHT = 400;
        private static int RENDER_DISTANCE = 6;
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
        public static void LeaveRegion(string rIndex)
        {

            WriteRegion(VisibleRegions[rIndex]);
            Logger.Info($"Writing {VisibleRegions[rIndex]}");
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

        public static void WriteRegion(Region r)
        {

            string path = Path.Combine(worldDir, "regions", $"{r.GetBounds().X}.{r.GetBounds().Y}.dat");

            byte[] serializedRegion = MessagePackSerializer.Serialize(r);
            
            File.WriteAllBytes(path, serializedRegion);
        }

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
            Dictionary<string, Region> updatedRegions = ChunkCache.GetRegions();

            if (VisibleRegions.Count() > 0)
            {

                //Retrieves from file or generates any region that is visible
                for (int i = 0; i < updatedRegions.Count(); i++)
                {
                    //Enter region if not found in visible regions
                   if (!VisibleRegions.ContainsKey(updatedRegions.Keys.ElementAt(i)))
                        EnterRegion(updatedRegions.Keys.ElementAt(i));
                }

                //Write to file and de-render any regions that are no longer visible
                for (int i = 0; i < VisibleRegions.Count(); i++)
                {
                    if (!updatedRegions.ContainsKey(VisibleRegions.Keys.ElementAt(i)))
                        LeaveRegion(VisibleRegions.Keys.ElementAt(i));
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
                Logger.Info($"Generating new region {rIndex}");
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
        

            // returnRegion ??= new Region(xUpperLimit, zUpperLimit);
            return new Region(xUpperLimit, zUpperLimit);
          }

          public static Chunk GetGlobalChunkFromCoords(int x, int z)
          {
              //Calculates chunk coordinates
              int chunkXCoord = x / CHUNK_BOUNDS * CHUNK_BOUNDS;
              int chunkZCoord = z / CHUNK_BOUNDS * CHUNK_BOUNDS;

              string regionIdx = Region.GetRegionIndex(chunkXCoord, chunkZCoord);

            return TryGetRegionFromFile(regionIdx).chunks[$"{chunkXCoord}|{chunkZCoord}"];
        
          }
    }
}

