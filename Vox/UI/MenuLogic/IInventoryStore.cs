using OpenTK.Mathematics;
using Vox.Enums;
using Vox.Model;

namespace Vox.UI.MenuLogic
{
    public interface IInventoryStore
    {
        void SetSlot(int index, BlockType blockType, int quantity);
        Dictionary<int, KeyValuePair<BlockType, int>> GetSlots();
        void IncrementSlotQuantity(int increment, int slot);
        void DecrementSlotQuantity(int decrement, int slot);
        Matrix4 GetDisplayProjection();
        Matrix4 GetDisplayViewMatrix();
        Matrix4 GetDisplayModelMatrix();
        void UpdateSSBOBlock(BlockType modelblockType);
        int GetInventoryVAO();
        int GetInventoryDisplayFBO();
        int GetInventoryIconAtlas();
        int GetInventoryIconSlotFBO();
        void SetHandItem(KeyValuePair<BlockType, int> slot);
        KeyValuePair<BlockType, int> GetHandItem();
        bool IsItemBeingMoved();
        void SetHoveredSlotIndex(int index);
        int GetHoveredSlotIndex();
        bool GetPickupToggle();
        void SetPickupToggle(bool toggle);
    }
}
