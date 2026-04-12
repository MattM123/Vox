using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Bson;
using OpenTK.Mathematics;
using Vox.AssetManagement;
using Vox.Enums;
using Vox.Model;
using Vox.Rendering;
using static OpenTK.Audio.OpenAL.AL;

namespace Vox.UI
{

    public class InventoryStore
    {
        private readonly Dictionary<int, KeyValuePair<BlockType, int>> slots = new(36);

        public InventoryStore()
        {
            for (int i = 0; i < 36; i++)
                slots.Add(i, new(BlockType.AIR, 0));
        }

        public void SetSlot(int index, BlockType blockType, int quantity)
        {
            slots[index] = new(blockType, quantity);
        }

        public Dictionary<int, KeyValuePair<BlockType, int>> GetSlots()
        {
            return slots;
        }

        public void IncrementSlotQuantity(int increment, int slot)
        {
            BlockType blocktype = slots[slot].Key;
            int quantity = slots[slot].Value;

            slots[slot] = new(blocktype, quantity + increment);
        }

        public void DecrementSlotQuantity(int decrement, int slot)
        {
            BlockType blocktype = slots[slot].Key;
            int quantity = slots[slot].Value;

            slots[slot] = new(blocktype, quantity - decrement);
        }

        public static Matrix4 GetIconProjection()
        {
            //return Matrix4.CreateOrthographic(
            //    20.0f,  // width
            //    20.0f,  // height
            //    0.1f,
            //    16.0f
            //);
            return Matrix4.CreatePerspectiveOffCenter(
                -30f, 30f,  // left, right
                -30f, 30f,  // bottom, top
                0.1f,  // near plane
                100.0f  // extended far plane
            );
        }

        public static Matrix4 GetIconViewMatrix() 
        {
            //return Matrix4.LookAt(new(0, 0, -10), new(0, 0, 0), Vector3.UnitY);
            // Position camera at 45-degree angle for isometric view
            float distance = 15f;
            float angle = MathHelper.DegreesToRadians(45f);
            return Matrix4.LookAt(
                new Vector3(
                    distance * (float)Math.Sin(angle),    // X: 45-degree horizontal angle
                    0,//distance * (float)Math.Sin(angle),    // Y: 45-degree elevation
                    -distance * (float)Math.Cos(angle)    // Z: looking toward origin
                ),
                new Vector3(0, 0, 0),                     // look at center of block space (0-16 range)
                Vector3.UnitY                             // up direction
            );
        }

        /**
         * Uploads a single block face to the SSBO for rendering.
         * If the index is already present, updates the face data.
         */
        public void AddOrUpdateFaceInMemory(BlockFaceInstance face)
        {
            //Write face directly to SSBO
            unsafe
            {
                int offset = face.index * Marshal.SizeOf<BlockFaceInstance>();
                byte* basePtr = (byte*)Window.ssboManager.GetSSBO("Inventory").Pointer;

                BlockFaceInstance* instancePtr = (BlockFaceInstance*)(basePtr + offset);

                int instanceSize = Marshal.SizeOf<BlockFaceInstance>();

                if (offset + instanceSize > Window.ssboManager.GetSSBO("Inventory").Size)
                    throw new InvalidOperationException("SSBO overflow");

                *instancePtr = face;
            }
        }
    }
}
