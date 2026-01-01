using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Bson;
using Vox.AssetManagement;
using Vox.Enums;
using Vox.Model;
using Vox.Rendering;

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
