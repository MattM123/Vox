using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Bson;
using Vox.AssetManagement;
using Vox.Enums;
using Vox.Model;

namespace Vox.UI
{

    public class InventoryStore
    {
        private List<BlockType> slots = new(36);

        public InventoryStore()
        {
            for (int i = 0; i < 36; i++)
            {
                slots.Add(BlockType.AIR);
            }
        }

        public void SetSlot(int index, BlockType blockType)
        {
            slots[index] = blockType;
        }

        public List<BlockType> GetSlots()
        {
            return slots;
        }
    }
}
