using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Net.NetworkInformation;
using Newtonsoft.Json.Linq;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Vox.Enums;
using Vox.Genesis;

namespace Vox.Rendering
{
    public static class LightHelper
    {

        private static readonly ConcurrentQueue<LightNode> BFSEmissivePropagationQueue = new(new Queue<LightNode>((int)Math.Pow(RegionManager.CHUNK_BOUNDS, 3)));
        private static Dictionary<Vector3, ColorVector> lightingList = [];
        private static List<Vector3> visited = [];
        private static int maxLightValue = 15;
        private static int maxLightSpread = 15;

        public static int GetMaxLightSpread()
        {
            return maxLightSpread;
        }
        public static ColorVector GetBlockLight(Vector3 location, BlockFace faceDir, Chunk chunk)
        {
            return new(GetRedLight(location, faceDir, chunk), GetGreenLight(location, faceDir, chunk), GetBlueLight(location, faceDir, chunk));

        }

        public static void SetBlockLight(Vector3 location, ColorVector color, Chunk chunk, bool depropagate, bool colorOverride)
        {
            SetRedLight(location, BlockFace.ALL, color.Red, chunk, colorOverride);
            SetGreenLight(location, BlockFace.ALL, color.Green, chunk, colorOverride);
            SetBlueLight(location, BlockFace.ALL, color.Blue, chunk, colorOverride);
        }

        public static void SetBlockLight(Vector3 location, int packedColor, Chunk chunk)
        {
            UpdateEmissiveLighting(location, BlockFace.ALL, (ushort)packedColor, chunk);
        }

        /**
         * Combines two light values together using a max blend formula
         * and updates the emissive lighting for the block face instance.
         * 
         * If two light emitting blocks are placed next to eachother, 
         * combines the light values together on overlapping faces.
         */
        public static void CombineLightValues(Vector3 location, BlockFace faceDir, ushort currentLightLevels, ushort toCombine, Chunk chunk)
        {
            // Extract components
            int r = (currentLightLevels >> 8) & 0xF;
            int g = (currentLightLevels >> 4) & 0xF;
            int b = currentLightLevels & 0xF;

            // Extract new components
            int newR = (toCombine >> 8) & 0xF;
            int newG = (toCombine >> 4) & 0xF;
            int newB = toCombine & 0xF;

            // Max blend formula
            r = Math.Max(r, newR);
            g = Math.Max(g, newG);
            b = Math.Max(b, newB);


            // Recombine
            currentLightLevels = (ushort)((r << 8) | (g << 4) | b);
            UpdateEmissiveLighting(location, faceDir, currentLightLevels, chunk);

        }

        /**
        * Update the lighting value in the correct BlockFaceInstance within 
        * the chunks SSBO. The graphics are then updated once the GPU recieves the 
        * new SSBO data.
        * 
        */
        public static void UpdateEmissiveLighting(Vector3 facePos, BlockFace faceDir, ushort lighting, Chunk chunk)
        {
            Vector4 key = new(facePos.X, facePos.Y, facePos.Z, (float)faceDir);
            if(faceDir == BlockFace.ALL)
            {
                UpdateEmissiveLighting(facePos, BlockFace.UP, lighting, chunk);
                UpdateEmissiveLighting(facePos, BlockFace.DOWN, lighting, chunk);
                UpdateEmissiveLighting(facePos, BlockFace.EAST, lighting, chunk);
                UpdateEmissiveLighting(facePos, BlockFace.WEST, lighting, chunk);
                UpdateEmissiveLighting(facePos, BlockFace.NORTH, lighting, chunk);
                UpdateEmissiveLighting(facePos, BlockFace.SOUTH, lighting, chunk);
                return;
            }


            if (chunk.SSBOdata.TryGetValue(key, out BlockFaceInstance existingFace))
            {
                //Update lighting
                existingFace.lighting = lighting;

                //Update entire instance
                chunk.SSBOdata[key] = existingFace;

                //Update data for GPU
                chunk.AddOrUpdateFaceInMemory(chunk.SSBOdata[key]);
            }
            else
            {
                chunk.SSBOdata.TryAdd(key, new BlockFaceInstance(facePos, faceDir, 0, Window.GetAndIncrementNextFaceIndex(), lighting));
            }
        }


        /**
         * Get and set functions for each color component for a given blockface with the SSBO.
         * Each color component is stored in a 32 bit uint as follows:
         * 
         * Bits 0-7: Blue
         * Bits 8-15: Green
         * Bits 16-23: Red
         * Bits 24-31: Unused for now
         * 
         * Each color component can have a value from 0 to 15 (4 bits)
         */

        // ================ Blue component (bits 0-7) =================
        public static int GetBlueLight(Vector3 location, BlockFace faceDir, Chunk chunk)
        {
            if (chunk.SSBOdata.ContainsKey(new(location.X, location.Y, location.Z, (int)faceDir)))
            {
                uint blue = chunk.SSBOdata[new(location.X, location.Y, location.Z, (int)faceDir)].lighting;
                return (char) (blue & 0xF);
            }
            else
                return 0;
        }
        public static void SetBlueLight(Vector3 location, BlockFace faceDir, int val, Chunk chunk, bool colorOverride)
        {
            if (val < 0)
                val = 0;
            else if (val > maxLightValue)
                val = maxLightValue;


            // Apply mask to new value to ensure only relevant bits are set
            int newValue = val & 0x000F;

            Vector3 facePos = new(location.X, location.Y, location.Z);

            if (faceDir == BlockFace.ALL)
            {
                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.UP)))
                    CombineLightValues(facePos, BlockFace.UP, chunk.SSBOdata[new(facePos, (int)BlockFace.UP)].lighting, (ushort)newValue, chunk);
                  
                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.DOWN)))
                    CombineLightValues(facePos, BlockFace.DOWN, chunk.SSBOdata[new(facePos, (int)BlockFace.DOWN)].lighting, (ushort)newValue, chunk);

                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.EAST)))
                    CombineLightValues(facePos, BlockFace.EAST, chunk.SSBOdata[new(facePos, (int)BlockFace.EAST)].lighting, (ushort)newValue, chunk);

                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.WEST)))
                    CombineLightValues(facePos, BlockFace.WEST, chunk.SSBOdata[new(facePos, (int)BlockFace.WEST)].lighting, (ushort)newValue, chunk);

                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.NORTH)))
                    CombineLightValues(facePos, BlockFace.NORTH, chunk.SSBOdata[new(facePos, (int)BlockFace.NORTH)].lighting, (ushort)newValue, chunk);

                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.SOUTH)))
                    CombineLightValues(facePos, BlockFace.SOUTH, chunk.SSBOdata[new(facePos, (int)BlockFace.SOUTH)].lighting, (ushort)newValue, chunk);
            }
            else
            {
                if (chunk.SSBOdata.TryGetValue(new(facePos, (int)faceDir), out var data) && !colorOverride)
                {  
                    CombineLightValues(facePos, faceDir, data.lighting, (ushort)newValue, chunk);
                }
                else if (colorOverride)
                {
                    UpdateEmissiveLighting(facePos, faceDir, (ushort)newValue, chunk);
                }
            }
        }

        // ===========================================================
        // ================ Green component (bits 8-15) ================
        public static int GetGreenLight(Vector3 location, BlockFace faceDir, Chunk chunk)
        {
            if (chunk.SSBOdata.ContainsKey(new(location.X, location.Y, location.Z, (int)faceDir)))
            {
                int green = chunk.SSBOdata[new(location.X, location.Y, location.Z, (int)faceDir)].lighting;
                return ((char)green >> 4) & 0xF;
            }
            else
                return 0;
        }
        public static void SetGreenLight(Vector3 location, BlockFace faceDir, int val, Chunk chunk, bool colorOverride)
        {
            if (val < 0)
                val = 0;
            else if (val > 15)
                val = 15;

            // Apply mask to new value to ensure only relevant bits are set
            int newValue = (val << 4) & 0x00F0;

            Vector3 facePos = new(location.X, location.Y, location.Z);

            if (faceDir == BlockFace.ALL)
            {
                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.UP)))
                    CombineLightValues(facePos, BlockFace.UP, chunk.SSBOdata[new(facePos, (int)BlockFace.UP)].lighting, (ushort)newValue, chunk);

                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.DOWN)))
                    CombineLightValues(facePos, BlockFace.DOWN, chunk.SSBOdata[new(facePos, (int)BlockFace.DOWN)].lighting, (ushort)newValue, chunk);

                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.EAST)))
                    CombineLightValues(facePos, BlockFace.EAST, chunk.SSBOdata[new(facePos, (int)BlockFace.EAST)].lighting, (ushort)newValue, chunk);

                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.WEST)))
                    CombineLightValues(facePos, BlockFace.WEST, chunk.SSBOdata[new(facePos, (int)BlockFace.WEST)].lighting, (ushort)newValue, chunk);

                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.NORTH)))
                    CombineLightValues(facePos, BlockFace.NORTH, chunk.SSBOdata[new(facePos, (int)BlockFace.NORTH)].lighting, (ushort)newValue, chunk);

                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.SOUTH)))
                    CombineLightValues(facePos, BlockFace.SOUTH, chunk.SSBOdata[new(facePos, (int)BlockFace.SOUTH)].lighting, (ushort)newValue, chunk);
            }
            else
            {
                if (chunk.SSBOdata.TryGetValue(new(facePos, (int)faceDir), out var data) && !colorOverride)
                {
                    CombineLightValues(facePos, faceDir, data.lighting, (ushort)newValue, chunk);
                }
                else
                {
                    UpdateEmissiveLighting(facePos, faceDir, (ushort)newValue, chunk);
                }
            }
        }
        // ===========================================================
        // ================ Red component (bits 16-23) ================
        public static int GetRedLight(Vector3 location, BlockFace faceDir, Chunk chunk)
        {

            if (chunk.SSBOdata.ContainsKey(new(location.X, location.Y, location.Z, (int)faceDir)))
            {
                int red = chunk.SSBOdata[new(location.X, location.Y, location.Z, (int)faceDir)].lighting;
                return ((char)red >> 8) & 0x0F;
            }
            else
                return 0;

        }
        public static void SetRedLight(Vector3 location, BlockFace faceDir, int val, Chunk chunk, bool colorOverride)
        {

            if (val < 0)
                val = 0;
            else if (val > maxLightValue)
                val = maxLightValue;

            // Apply mask to new value to ensure only relevant bits are set
            int newValue = (val << 8) & 0x0F00;

            Vector3 facePos = new(location.X, location.Y, location.Z);

            if (faceDir == BlockFace.ALL)
            {
                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.UP)))
                    CombineLightValues(facePos, BlockFace.UP, chunk.SSBOdata[new(facePos, (int)BlockFace.UP)].lighting, (ushort)newValue, chunk);

                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.DOWN)))
                    CombineLightValues(facePos, BlockFace.DOWN, chunk.SSBOdata[new(facePos, (int)BlockFace.DOWN)].lighting, (ushort)newValue, chunk);

                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.EAST)))
                    CombineLightValues(facePos, BlockFace.EAST, chunk.SSBOdata[new(facePos, (int)BlockFace.EAST)].lighting, (ushort)newValue, chunk);

                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.WEST)))
                    CombineLightValues(facePos, BlockFace.WEST, chunk.SSBOdata[new(facePos, (int)BlockFace.WEST)].lighting, (ushort)newValue, chunk);

                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.NORTH)))
                    CombineLightValues(facePos, BlockFace.NORTH, chunk.SSBOdata[new(facePos, (int)BlockFace.NORTH)].lighting, (ushort)newValue, chunk);

                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.SOUTH)))
                    CombineLightValues(facePos, BlockFace.SOUTH, chunk.SSBOdata[new(facePos, (int)BlockFace.SOUTH)].lighting, (ushort)newValue, chunk);
            }
            else
            {
                if (chunk.SSBOdata.TryGetValue(new(facePos, (int)faceDir), out var data) && !colorOverride)
                {
                    CombineLightValues(facePos, faceDir, data.lighting, (ushort)newValue, chunk);
                }
                else if (colorOverride)
                {
                    UpdateEmissiveLighting(facePos, faceDir, (ushort)newValue, chunk);
                }
            }
        }
        // ============================================================


        public static void TrackLighting(Vector3 location, ColorVector color)
        {
            lightingList.Add(location, color);
        }

        public static Dictionary<Vector3, ColorVector> GetLightTrackingList()
        {
            return lightingList;
        }

        //================================================================================================
        //============================= Light Propagation and Depropagation ==============================
        //================================================================================================
        public static void PropagateBlockLight(Vector3 location, BlockFace faceDir, bool depropagate, bool colorOverride)
        {
            int x = (int)location.X;
            int y = (int)location.Y;
            int z = (int)location.Z;

            //Get all light sources within the vicinity of the source we are propagating/depropagating
            Dictionary<Vector3, ColorVector> lightingArea = [];
            foreach (KeyValuePair<Vector3, ColorVector> light in GetLightTrackingList())
            {
                if ((light.Key - location).Length <= GetMaxLightSpread() && !lightingArea.ContainsKey(light.Key)) ;
                {
                    lightingArea.Add(light.Key, light.Value);
                }
            }

            // Check the light level of the current node before propagating          
            ColorVector originLightLevel = GetBlockLight(location, faceDir, RegionManager.GetAndLoadGlobalChunkFromCoords(location));
            if ((originLightLevel.Red > 0 || originLightLevel.Green > 0 || originLightLevel.Blue > 0) && !depropagate)
            {
                string index = $"{x}|{y}|{z}";
                EnqueueEmissiveLightNode(new(index, RegionManager.GetAndLoadGlobalChunkFromCoords(location)));
                while (GetEmissiveQueueCount() > 0)
                {
                    // Get a reference to the front node.
                    LightNode node = DequeueEmissiveLightNode();
                    PropagateLightNode(lightingArea, location, faceDir, node, depropagate, colorOverride);
                }
            }

            else if (depropagate)
            {
                string index = $"{x}|{y}|{z}";

                EnqueueEmissiveLightNode(new(index, RegionManager.GetAndLoadGlobalChunkFromCoords(location)));
                while (GetEmissiveQueueCount() > 0)
                {
                    LightNode node = DequeueEmissiveLightNode();
                    string currentIdx = node.Index;
                    int xLight = int.Parse(currentIdx.Split('|')[0]);
                    int yLight = int.Parse(currentIdx.Split('|')[1]);
                    int zLight = int.Parse(currentIdx.Split('|')[2]);

                    // Grab the 16 bit light level of the current node
                    // ???? RRRR GGGG BBBB
                    ColorVector lightLevel = GetBlockLight(new Vector3(xLight, yLight, zLight), faceDir, node.Chunk);

                    DepropagateLightNode(lightingArea, location, faceDir, node, depropagate, true);
                }
            }
            visited.Clear();
        }

        //======================================================================================
        //============================= Light Propagation Functions ============================
        //======================================================================================
        public static int GetEmissiveQueueCount()
        {
            return BFSEmissivePropagationQueue.Count;
        }
        public static void EnqueueEmissiveLightNode(LightNode node)
        {
            BFSEmissivePropagationQueue.Enqueue(node);
        }

        /**
         * Propagates light using Breadth First Search.
         *
         * @Param faceDir The block face to apply the light to
         * @Param node The current light node being processed
         * @Param colorOverride Whether to combine the color with the existing light or override it
         * 
         */
        /**
       * Propagates light using Breadth First Search.
       *
       * @Param faceDir The block face to apply the light to
       * @Param node The current light node being processed
       * @Param colorOverride Whether to combine the color with the existing light or override it
       * 
       */
        public static void PropagateLightNode(Dictionary<Vector3, ColorVector> lightingArea, Vector3 sourceLocation, BlockFace faceDir, LightNode node, bool depropagate, bool colorOverride)
        {
            Chunk chunk = node.Chunk;
            string currentIdx = node.Index;
            int xLight = int.Parse(currentIdx.Split('|')[0]);
            int yLight = int.Parse(currentIdx.Split('|')[1]);
            int zLight = int.Parse(currentIdx.Split('|')[2]);

            // Grab the 16 bit light level of the current node
            // ???? RRRR GGGG BBBB
            Vector3 baseLoc = new(xLight, yLight, zLight);
            ColorVector lightLevel = GetBlockLight(baseLoc, faceDir, chunk);

            //======================================
            //          NEGATIVE X (WEST)
            //======================================

            //Chunk bounds check
            Vector3 loc1 = new(xLight - 1, yLight, zLight);
            Chunk xMinusOne = RegionManager.GetAndLoadGlobalChunkFromCoords(loc1);
            if (!chunk.Equals(xMinusOne))
                chunk = xMinusOne;


            PropagateLightNodeHelper(lightingArea, loc1, sourceLocation, faceDir, lightLevel, chunk, depropagate, colorOverride);

            //======================================
            //          POSITIVE X (EAST)
            //======================================

            //Chunk bounds check
            Vector3 loc2 = new(xLight + 1, yLight, zLight);
            Chunk xPlusOne = RegionManager.GetAndLoadGlobalChunkFromCoords(loc2);
            if (!chunk.Equals(xPlusOne))
                chunk = xPlusOne;

            PropagateLightNodeHelper(lightingArea, loc2, sourceLocation, faceDir, lightLevel, chunk, depropagate, colorOverride);

            //======================================
            //          NEGATIVE Y (DOWN)
            //======================================

            //Chunk bounds check
            Vector3 loc3 = new(xLight, yLight - 1, zLight);
            Chunk yMinusOne = RegionManager.GetAndLoadGlobalChunkFromCoords(loc3);
            if (!chunk.Equals(yMinusOne))
                chunk = yMinusOne;


            PropagateLightNodeHelper(lightingArea, loc3, sourceLocation, faceDir, lightLevel, chunk, depropagate, colorOverride);

            //======================================
            //          POSITIVE Y (UP)
            //======================================

            //Chunk bounds check
            Vector3 loc4 = new(xLight, yLight + 1, zLight);
            Chunk yPlusOne = RegionManager.GetAndLoadGlobalChunkFromCoords(loc4);
            if (!chunk.Equals(yPlusOne))
                chunk = yPlusOne;

            PropagateLightNodeHelper(lightingArea, loc4, sourceLocation, faceDir, lightLevel, chunk, depropagate, colorOverride);

            //======================================
            //          NEGATIVE Z (SOUTH)
            //======================================

            //Chunk bounds check
            Vector3 loc5 = new(xLight, yLight, zLight - 1);
            Chunk zMinusOne = RegionManager.GetAndLoadGlobalChunkFromCoords(loc5);
            if (!chunk.Equals(zMinusOne))
                chunk = zMinusOne;

            PropagateLightNodeHelper(lightingArea, loc5, sourceLocation, faceDir, lightLevel, chunk, depropagate, colorOverride);

            //======================================
            //          POSITIVE Z (NORTH)
            //======================================

            //Chunk bounds check
            Vector3 loc6 = new(xLight, yLight, zLight + 1);
            Chunk zPlusOne = RegionManager.GetAndLoadGlobalChunkFromCoords(loc6);
            if (!chunk.Equals(zPlusOne))
                chunk = zPlusOne;

            PropagateLightNodeHelper(lightingArea, loc6, sourceLocation, faceDir, lightLevel, chunk, depropagate, colorOverride);
        }

        public static void PropagateLightNodeHelper(Dictionary<Vector3, ColorVector> lightingArea, Vector3 location, Vector3 sourceLocation, BlockFace faceDir, ColorVector lightLevel, Chunk chunk, bool depropagate, bool colorOverride)
        {
            bool wasUpdated = false;


            // distance from the propagation source to this neighbor   
            int distFromSource = Utils.GetVectorDistance(location, sourceLocation);

            ColorVector lightSourceLevel = lightingArea[sourceLocation];


            if ((GetRedLight(location, faceDir, chunk) + 1) < lightLevel.Red)
            {
          
                // Set red light level
                SetRedLight(location, faceDir, lightSourceLevel.Red - distFromSource, chunk, colorOverride);
               // Console.WriteLine("Red: " + GetRedLight(location, faceDir, chunk));
              //  Console.WriteLine("Setting red to " + (lightSourceLevel.Red - distFromSource));
                wasUpdated = true;
            }

            //  Console.WriteLine("test: " + lightLevel + "Y: " + location.Y);
            if ((GetGreenLight(location, faceDir, chunk) + 1) < lightLevel.Green)
            {
                // Set green light level
                //    Console.WriteLine("Setting light: " + lightLevel + "Y: " + location.Y);
                SetGreenLight(location, faceDir, lightSourceLevel.Green - distFromSource, chunk, colorOverride);
                wasUpdated = true;
            }

            if ((GetBlueLight(location, faceDir, chunk) + 1) < lightLevel.Blue)
            {
                // Set blue light level
                SetBlueLight(location, faceDir, lightSourceLevel.Blue - distFromSource, chunk, colorOverride);
                wasUpdated = true;
            }

            if (wasUpdated)
            {
                // Construct index
                string idx = $"{location.X}|{location.Y}|{location.Z}";
                // visited.Add(location);
                // Emplace new node to queue.

                EnqueueEmissiveLightNode(new(idx, chunk));

            }
        }

        //========================================================================================
        //============================= Light Depropagation Functions ============================
        //========================================================================================
        public static LightNode DequeueEmissiveLightNode()
        {
            if (BFSEmissivePropagationQueue.TryDequeue(out LightNode node))
                return node;
            else
                return new LightNode("", null);
        }

        private static void DepropagateLightNode(Dictionary<Vector3, ColorVector> lightingArea, Vector3 sourceLocation, BlockFace faceDir, LightNode node, bool depropagate, bool colorOverride)
        {
            Chunk chunk = node.Chunk;
            string currentIdx = node.Index;
            int xLight = int.Parse(currentIdx.Split('|')[0]);
            int yLight = int.Parse(currentIdx.Split('|')[1]);
            int zLight = int.Parse(currentIdx.Split('|')[2]);

            // Grab the 16 bit light level of the current node
            // ???? RRRR GGGG BBBB
            ColorVector nodeLightLevel = GetBlockLight(new(xLight, yLight, zLight), faceDir, chunk);

            //======================================
            //          NEGATIVE X (WEST)
            //======================================

            //Chunk bounds check
            Vector3 loc1 = new(xLight - 1, yLight, zLight);
            Chunk xMinusOne = RegionManager.GetAndLoadGlobalChunkFromCoords(loc1);
            if (!chunk.Equals(xMinusOne))
                chunk = xMinusOne;

            DepropagationLightNodeHelper(lightingArea, sourceLocation, loc1, faceDir, nodeLightLevel, chunk, depropagate, colorOverride);

            //======================================
            //          POSITIVE X (EAST)
            //======================================

            //Chunk bounds check
            Vector3 loc2 = new(xLight + 1, yLight, zLight);
            Chunk xPlusOne = RegionManager.GetAndLoadGlobalChunkFromCoords(loc2);
            if (!chunk.Equals(xPlusOne))
                chunk = xPlusOne;

            DepropagationLightNodeHelper(lightingArea, sourceLocation, loc2, faceDir, nodeLightLevel, chunk, depropagate, colorOverride);

            //======================================
            //          NEGATIVE Y (DOWN)
            //======================================

            //Chunk bounds check
            Vector3 loc3 = new(xLight, yLight - 1, zLight);
            Chunk yMinusOne = RegionManager.GetAndLoadGlobalChunkFromCoords(loc3);
            if (!chunk.Equals(yMinusOne))
                chunk = yMinusOne;

            DepropagationLightNodeHelper(lightingArea, sourceLocation, loc3, faceDir, nodeLightLevel, chunk, depropagate, colorOverride);

            //======================================
            //          POSITIVE Y (UP)
            //======================================

            //Chunk bounds check
            Vector3 loc4 = new(xLight, yLight + 1, zLight);
            Chunk yPlusOne = RegionManager.GetAndLoadGlobalChunkFromCoords(loc4);
            if (!chunk.Equals(yPlusOne))
                chunk = yPlusOne;

            DepropagationLightNodeHelper(lightingArea, sourceLocation, loc4, faceDir, nodeLightLevel, chunk, depropagate, colorOverride);

            //======================================
            //          NEGATIVE Z (SOUTH)
            //======================================

            //Chunk bounds check
            Vector3 loc5 = new(xLight, yLight, zLight - 1);
            Chunk zMinusOne = RegionManager.GetAndLoadGlobalChunkFromCoords(loc5);
            if (!chunk.Equals(zMinusOne))
                chunk = zMinusOne;

            DepropagationLightNodeHelper(lightingArea, sourceLocation, loc5, faceDir, nodeLightLevel, chunk, depropagate, colorOverride);

            //======================================
            //          POSITIVE Z (NORTH)
            //======================================

            //Chunk bounds check
            Vector3 loc6 = new(xLight, yLight, zLight + 1);
            Chunk zPlusOne = RegionManager.GetAndLoadGlobalChunkFromCoords(loc6);
            if (!chunk.Equals(zPlusOne))
                chunk = zPlusOne;
            DepropagationLightNodeHelper(lightingArea, sourceLocation, loc6, faceDir, nodeLightLevel, chunk, depropagate, colorOverride);
        }

        private static void DepropagationLightNodeHelper(Dictionary<Vector3, ColorVector> lightingArea, Vector3 sourceLocation, Vector3 location, BlockFace faceDir,
            ColorVector nodeLightLevel, Chunk chunk, bool depropagate, bool colorOverride)
        {
            bool wasUpdated = false;

            // distance from the depropagation source to this neighbor
            int distFromSource = Utils.GetVectorDistance(location, sourceLocation);

            ColorVector lightSourceLevel = lightingArea[sourceLocation];
    
            if (GetBlueLight(location, faceDir, chunk) > 0 && !visited.Contains(location) && distFromSource <= lightSourceLevel.Blue)
            {
                Dictionary<Vector3, int> contributingBlueLightValues = [];
                //  int cumulativeLight = 0;
                foreach (KeyValuePair<Vector3, ColorVector> light in lightingArea)
                {
                    int value = Utils.GetVectorDistance(location, light.Key);
                    if (value <= lightSourceLevel.Blue)
                    {
                        //If block face (location) is within the range of any light source, accumulate light
                        contributingBlueLightValues.Add(light.Key, value);
                    }
                }

                //If more than one light source is lighting the area
                if (contributingBlueLightValues.Count > 1)
                {
                    //If blockface isnt within range of any light sources, make sure light is set to 0 on depropagation
                    // if (cumulativeLight == 0 && !lightingArea.ContainsKey(location))
                    //    cumulativeLight = lightSourceLevel.Blue;

                    //start with the block being deleted, then delete its neighbors if they have lower light
                    //if a neighbor has higher light, add it to a list of lights to propagate after the recursive removal is complete

                    //if (cumulativeLight > 0)
                    //{
                    //    Console.WriteLine($"Setting to {lightSourceLevel.Blue} - ({cumulativeLight} - {GetBlueLight(location, faceDir, chunk)}) = " +
                    //        $"{lightSourceLevel.Blue - (cumulativeLight - GetBlueLight(location, faceDir, chunk))}");
                    //    SetBlueLight(location, faceDir, lightSourceLevel.Blue - (cumulativeLight - GetBlueLight(location, faceDir, chunk)), chunk, depropagate, true);
                    //}

                    //  Console.WriteLine(cumulativeLight + " " + GetBlueLight(location, faceDir, chunk));
                    //  if (cumulativeLight >= GetBlueLight(location, faceDir, chunk) && GetBlueLight(location, faceDir, chunk) > 0)


                    int cumulativeLight = contributingBlueLightValues.Values.Sum() - contributingBlueLightValues[sourceLocation];

                    //Keep
                    //The light source being depropagated is brighter than the cumulative light at the location
                    //Do not depropagate light source block faces
                    if (lightSourceLevel.Blue > cumulativeLight && !lightingArea.ContainsKey(location))
                    {
                        SetBlueLight(location, faceDir, lightSourceLevel.Blue - cumulativeLight, chunk, false);
              
                    }
                    //Keep
                    //The light source being depropagated less bright than the cumulative light at the location
                    //Do not depropagate light source block faces
                    else if (lightSourceLevel.Blue < cumulativeLight && !lightingArea.ContainsKey(location))
                    {
                        SetBlueLight(location, faceDir, lightSourceLevel.Blue - (cumulativeLight - lightSourceLevel.Blue), chunk, false);

                    }

                    //Keep
                    //Light source level is equal to accumulated light and outside the light range so they cancel out
                    else if (lightSourceLevel.Blue == cumulativeLight && contributingBlueLightValues[sourceLocation] >= lightSourceLevel.Blue)
                    {
                        SetBlueLight(location, faceDir, 0, chunk, false);
                       
                    }

                    //If there is still more than one contributing light values for the block face, 
                    //if (contributingBlueLightValues.Count > 1)
                    //{
                    //    SetRedLight(location, faceDir, 3, chunk, depropagate, true);
                    //}
 

                }
                else
                {
                    SetBlueLight(location, faceDir, 0, chunk, colorOverride);
                }

                visited.Add(location);
                wasUpdated = true;
  
            }

            if (wasUpdated)
            {
                string idx = $"{location.X}|{location.Y}|{location.Z}";
                EnqueueEmissiveLightNode(new(idx, chunk));

            }
            
        }
    }
}
