using Newtonsoft.Json;

namespace Vox.World
{

    public class RegionManager : List<Region>
    {
        public static List<Region> VisibleRegions = new();
        private static string worldDir;
        public static readonly int CHUNK_HEIGHT = 320;
        public static readonly int RENDER_DISTANCE = 4;
        public static readonly int REGION_BOUNDS = 512;
        public static readonly int CHUNK_BOUNDS = 16;
        public static readonly long WORLD_SEED = 4472426645;
        private static Thread reagionWriterThread;

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
            try
            {
                worldDir = path;
                Directory.CreateDirectory(worldDir + "\\regions\\");
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }

            ChunkCache.SetBounds(CHUNK_BOUNDS);
            ChunkCache.SetRenderDistance(RENDER_DISTANCE);
        }

        /**
         * Removes a region from the visible regions once a player leaves a region and
         * their render distance no longer overlaps it. Writes region to file in the process
         * effectively saving the regions state for future use.
         *
         * @param r The region to leave
         */
        public static void LeaveRegion(Region r)
        {
            try
            {
                List<string> fileList = Directory.EnumerateFiles(worldDir + "\\regions\\").ToList();
                string? file = fileList.Find(filename => filename.Equals(r.GetBounds().X + "." + r.GetBounds().Y + ".dat"));


                //Writes region to file and removes from visibility
                ParameterizedThreadStart threadStart = new((object r) =>
                {
                    WriteRegion((Region) r, file); // Access the file path from the variable
                });
                reagionWriterThread.Start(threadStart);

                // Remove the region from the VisibleRegions list
                VisibleRegions.Remove(r);
                Logger.Debug("[Exiting Region2] " + r);
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }

        public static void WriteRegion(Region r, string path) {
            // Serialize the object to JSON
            string json = JsonConvert.SerializeObject(r);

            // Write the JSON to the file
            File.WriteAllText(path, json);
        }
        public static Region? ReadRegion(string path)
        {
            // Serialize the object to JSON
            Region? json = JsonConvert.DeserializeObject<Region>("Region");

            return json;

        }
        /**
         * Generates or loads an already generated region from filesystem when the players
         * render distance intersects with the regions bounds.
         */
        public static Region EnterRegion(Region r)
        {
            //If region is already visible
            if (VisibleRegions.Contains(r))
            {
                return r;
            }

            //Gets region from files if it's written to file but not visible
            try
            {
                //Check if region is already in files
                Directory.CreateDirectory(worldDir + "\\regions\\");
                List<string> fileList = Directory.EnumerateFiles(worldDir + "\\regions\\").ToList();
                string? file = fileList.Find(filename => filename.Equals(r.GetBounds().X + "." + r.GetBounds().Y + ".dat"));

             
                if (fileList.Count > 0 && file != null)
                {

                    //Reads region from file and adds to visibility
                    ParameterizedThreadStart threadStart = new((object r) =>
                    {
                        ReadRegion(file); // Access the file path from the variable
                    });
                    reagionWriterThread.Start(threadStart);

                    // Remove the region from the VisibleRegions list
                    VisibleRegions.Remove(r);
                    Logger.Debug("[Entering Region1] " + r); 
                }

                //if region is not visible and not written to files creates new region
                else if (file == null)
                {
                    VisibleRegions.Add(r);
                    Logger.Debug("[Entering Region2] " + r);
                    return r;
                }

            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
            return r;

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
            ChunkCache.GetChunksToRender();
            List<Region> updatedRegions = ChunkCache.getRegions();

            if (VisibleRegions.Count > 0)
            {
                Logger.Debug("[Updating Regions...]");

                //Retrieves from file or generates any region that is visible
                for (int i = 0; i < updatedRegions.Count; i++)
                {
                    if (!VisibleRegions.Contains(updatedRegions[i]))
                        EnterRegion(updatedRegions[i]);
                }

                //Write to file and de-render any regions that are no longer visible
                for (int i = 0; i < VisibleRegions.Count; i++)
                {
                    if (!updatedRegions.Contains(VisibleRegions[i]))
                    {
                        LeaveRegion(VisibleRegions[i]);
                    }
                }
            }
        }
    }
}
    
