using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Diagnostics.Metrics;
using System.Net.NetworkInformation;
using Newtonsoft.Json.Linq;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Vox.Enums;
using Vox.Genesis;
using Vox.Model;

namespace Vox.Rendering
{
    public class LightHelper : ILightHelper
    {
        private static Dictionary<Vector3, ColorVector> lightingList = [];

        private static int maxLightValue = 15;
        private static int maxLightSpread = 15;

        public LightHelper() { }
        public int GetMaxLightSpread()
        {
            return maxLightSpread;
        }
        public ColorVector GetBlockLight(Vector3 location, BlockFace faceDir, Chunk chunk)
        {
            return new(GetRedLight(location, faceDir, chunk), GetGreenLight(location, faceDir, chunk), GetBlueLight(location, faceDir, chunk));

        }

        public void SetBlockLight(Vector3 location, ColorVector color, Chunk chunk, bool depropagate, bool colorOverride)
        {
            SetRedLight(location, BlockFace.ALL, color.Red, chunk, colorOverride);
            SetGreenLight(location, BlockFace.ALL, color.Green, chunk, colorOverride);
            SetBlueLight(location, BlockFace.ALL, color.Blue, chunk, colorOverride);
        }

        public void SetBlockLight(Vector3 location, int packedColor, Chunk chunk)
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
        private void CombineLightValues(Vector3 location, BlockFace faceDir, ushort currentLightLevels, ushort toCombine, Chunk chunk)
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
        private void UpdateEmissiveLighting(Vector3 facePos, BlockFace faceDir, ushort lighting, Chunk chunk)
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
        public int GetBlueLight(Vector3 location, BlockFace faceDir, Chunk chunk)
        {
            if (chunk.SSBOdata.ContainsKey(new(location.X, location.Y, location.Z, (int)faceDir)))
            {
                uint blue = chunk.SSBOdata[new(location.X, location.Y, location.Z, (int)faceDir)].lighting;
                return (char) (blue & 0xF);
            }
            else
                return 0;
        }
        public void SetBlueLight(Vector3 location, BlockFace faceDir, int val, Chunk chunk, bool colorOverride)
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
        public int GetGreenLight(Vector3 location, BlockFace faceDir, Chunk chunk)
        {
            if (chunk.SSBOdata.ContainsKey(new(location.X, location.Y, location.Z, (int)faceDir)))
            {
                int green = chunk.SSBOdata[new(location.X, location.Y, location.Z, (int)faceDir)].lighting;
                return ((char)green >> 4) & 0xF;
            }
            else
                return 0;
        }
        public void SetGreenLight(Vector3 location, BlockFace faceDir, int val, Chunk chunk, bool colorOverride)
        {
            if (val < 0)
                val = 0;
            else if (val > maxLightValue)
                val = maxLightValue;

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
        public int GetRedLight(Vector3 location, BlockFace faceDir, Chunk chunk)
        {

            if (chunk.SSBOdata.ContainsKey(new(location.X, location.Y, location.Z, (int)faceDir)))
            {
                int red = chunk.SSBOdata[new(location.X, location.Y, location.Z, (int)faceDir)].lighting;
                return ((char)red >> 8) & 0x0F;
            }
            else
                return 0;

        }
        public void SetRedLight(Vector3 location, BlockFace faceDir, int val, Chunk chunk, bool colorOverride)
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

        public void TrackLighting(Vector3 location, ColorVector color)
        {
            lightingList.Add(location, color);
        }

        public Dictionary<Vector3, ColorVector> GetLightTrackingList()
        {
            return lightingList;
        }
    }
}
