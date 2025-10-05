using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Mathematics;
using Vox.Enums;
using Vox.Genesis;

namespace Vox.Rendering
{
    public static class LightHelper
    {
        //========================
        //Light helper functions
        //========================

        //Given the block data index of a chunk, returns the light level for red green and blue channels
        public static ColorVector GetBlockLightVector(Vector3 location, BlockFace faceDir, Chunk chunk)
        {
            return new(GetRedLight(location, faceDir, chunk), GetGreenLight(location, faceDir, chunk), GetBlueLight(location, faceDir, chunk));

        }
        public static ushort GetBlockLight(Vector3 location, BlockFace faceDir, Chunk chunk)
        {
            try
            {
                return (ushort)chunk.SSBOdata[new(location.X, location.Y, location.Z, (int)faceDir)].lighting;
            }
            catch (KeyNotFoundException)
            {
                return 0;
            }

        }
        // Set emissive RGB values
        public static void SetBlockFaceLight(Vector3 location, BlockFace faceDir, ColorVector color, Chunk chunk)
        {
            SetRedLight(location, faceDir, color.Red, chunk);
            SetGreenLight(location, faceDir, color.Green, chunk);
            SetBlueLight(location, faceDir, color.Blue, chunk);
        }

        // Get the bits XXXX0000
        //public int GetSunlight(int x, int y, int z)
        //{
        //    return 0;// (lightmap[GetIndex(new(x + (int)xLoc, y + (int)yLoc, z + (int)zLoc))] >> 4) & 0xF;
        //}
        //public int GetSunlight(Vector3i v)
        //{
        //    return 0;// (lightmap[GetIndex(new(v.X + (int)xLoc, v.Y + (int)yLoc, v.Z + (int)zLoc))] >> 4) & 0xF;
        //}
        //
        //// Set the bits XXXX0000
        //public void SetSunlight(int x, int y, int z, int val)
        //{
        //   // lightmap[GetIndex(new(x + (int)xLoc, y + (int)yLoc, z + (int)zLoc))] = (byte) ((lightmap[GetIndex(new(x + (int)xLoc, y + (int)yLoc, z + (int)zLoc))] & 0xF000) | (val << 4));
        //}
        // ================ Blue component (bits 0-3) =================
        public static int GetBlueLight(Vector3 location, BlockFace faceDir, Chunk chunk)
        {
            if (chunk.SSBOdata.ContainsKey(new(location.X, location.Y, location.Z, (int)faceDir)))
            {
                int blue = chunk.SSBOdata[new(location.X, location.Y, location.Z, (int)faceDir)].lighting;
                return blue & 0x0F;
            }
            else
                return 0;
        }
        public static void SetBlueLight(Vector3 location, BlockFace faceDir, int val, Chunk chunk)
        {
            if (val < 0)
                val = 0;
            else if (val > 15)
                val = 15;

            // Apply mask to new value to ensure only relevant bits are set
            int newValue = val & 0x000F;

            Vector3 facePos = new(location.X, location.Y, location.Z);

            if (faceDir == BlockFace.ALL)
            {
                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.UP)))
                    chunk.UpdateEmissiveLighting(facePos, BlockFace.UP, newValue | (ushort)chunk.SSBOdata[new(facePos, (int)BlockFace.UP)].lighting);
                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.DOWN)))
                    chunk.UpdateEmissiveLighting(facePos, BlockFace.DOWN, newValue | (ushort)chunk.SSBOdata[new(facePos, (int)BlockFace.DOWN)].lighting);
                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.EAST)))
                    chunk.UpdateEmissiveLighting(facePos, BlockFace.EAST, newValue | (ushort)chunk.SSBOdata[new(facePos, (int)BlockFace.EAST)].lighting);
                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.WEST)))
                    chunk.UpdateEmissiveLighting(facePos, BlockFace.WEST, newValue | (ushort)chunk.SSBOdata[new(facePos, (int)BlockFace.WEST)].lighting);
                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.NORTH)))
                    chunk.UpdateEmissiveLighting(facePos, BlockFace.NORTH, newValue | (ushort)chunk.SSBOdata[new(facePos, (int)BlockFace.NORTH)].lighting);
                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.SOUTH)))
                    chunk.UpdateEmissiveLighting(facePos, BlockFace.SOUTH, newValue | (ushort)chunk.SSBOdata[new(facePos, (int)BlockFace.SOUTH)].lighting);
            }
            else
            {
                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)faceDir)))
                    chunk.UpdateEmissiveLighting(facePos, faceDir, newValue | (ushort)chunk.SSBOdata[new(facePos, (int)faceDir)].lighting);
            }
        }
        // ===========================================================
        // ================ Green component (bits 4-7) ================
        public static int GetGreenLight(Vector3 location, BlockFace faceDir, Chunk chunk)
        {
            if (chunk.SSBOdata.ContainsKey(new(location.X, location.Y, location.Z, (int)faceDir)))
            {
                int green = chunk.SSBOdata[new(location.X, location.Y, location.Z, (int)faceDir)].lighting;
                return ((char)green >> 4) & 0x0F;
            }
            else
                return 0;
        }
        public static void SetGreenLight(Vector3 location, BlockFace faceDir, int val, Chunk chunk)
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
                    chunk.UpdateEmissiveLighting(facePos, BlockFace.UP, newValue | (ushort)chunk.SSBOdata[new(facePos, (int)BlockFace.UP)].lighting);
                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.DOWN)))
                    chunk.UpdateEmissiveLighting(facePos, BlockFace.DOWN, newValue | (ushort)chunk.SSBOdata[new(facePos, (int)BlockFace.DOWN)].lighting);
                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.EAST)))
                    chunk.UpdateEmissiveLighting(facePos, BlockFace.EAST, newValue | (ushort)chunk.SSBOdata[new(facePos, (int)BlockFace.EAST)].lighting);
                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.WEST)))
                    chunk.UpdateEmissiveLighting(facePos, BlockFace.WEST, newValue | (ushort)chunk.SSBOdata[new(facePos, (int)BlockFace.WEST)].lighting);
                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.NORTH)))
                    chunk.UpdateEmissiveLighting(facePos, BlockFace.NORTH, newValue | (ushort)chunk.SSBOdata[new(facePos, (int)BlockFace.NORTH)].lighting);
                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.SOUTH)))
                    chunk.UpdateEmissiveLighting(facePos, BlockFace.SOUTH, newValue | (ushort)chunk.SSBOdata[new(facePos, (int)BlockFace.SOUTH)].lighting);

            }
            else
            {
                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)faceDir)))
                    chunk.UpdateEmissiveLighting(facePos, faceDir, newValue | (ushort)chunk.SSBOdata[new(facePos, (int)faceDir)].lighting);
            }
        }
        // ===========================================================
        // ================ Red component (bits 8-11) ================
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
        public static void SetRedLight(Vector3 location, BlockFace faceDir, int val, Chunk chunk)
        {
            if (val < 0)
                val = 0;
            else if (val > 15)
                val = 15;

            // Apply mask to new value to ensure only relevant bits are set
            int newValue = (val << 8) & 0x0F00;

            Vector3 facePos = new(location.X, location.Y, location.Z);

            if (faceDir == BlockFace.ALL)
            {
                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.UP)))
                    chunk.UpdateEmissiveLighting(facePos, BlockFace.UP, newValue | (ushort)chunk.SSBOdata[new(facePos, (int)BlockFace.UP)].lighting);
                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.DOWN)))
                    chunk.UpdateEmissiveLighting(facePos, BlockFace.DOWN, newValue | (ushort)chunk.SSBOdata[new(facePos, (int)BlockFace.DOWN)].lighting);
                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.EAST)))
                    chunk.UpdateEmissiveLighting(facePos, BlockFace.EAST, newValue | (ushort)chunk.SSBOdata[new(facePos, (int)BlockFace.EAST)].lighting);
                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.WEST)))
                    chunk.UpdateEmissiveLighting(facePos, BlockFace.WEST, newValue | (ushort)chunk.SSBOdata[new(facePos, (int)BlockFace.WEST)].lighting);
                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.NORTH)))
                    chunk.UpdateEmissiveLighting(facePos, BlockFace.NORTH, newValue | (ushort)chunk.SSBOdata[new(facePos, (int)BlockFace.NORTH)].lighting);
                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)BlockFace.SOUTH)))
                    chunk.UpdateEmissiveLighting(facePos, BlockFace.SOUTH, newValue | (ushort)chunk.SSBOdata[new(facePos, (int)BlockFace.SOUTH)].lighting);

            }
            else
            {
                if (chunk.SSBOdata.ContainsKey(new(facePos, (int)faceDir)))
                    chunk.UpdateEmissiveLighting(facePos, faceDir, newValue | (ushort)chunk.SSBOdata[new(facePos, (int)faceDir)].lighting);
            }
        }
        // ============================================================
    }
}
