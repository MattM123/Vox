using OpenTK.Mathematics;
using Vox.Enums;
using Vox.Model;

namespace Vox.UI
{
    public interface IInventoryStore
    {
        void SetSlot(int index, BlockType blockType, int quantity);
        Dictionary<int, KeyValuePair<BlockType, int>> GetSlots();
        void IncrementSlotQuantity(int increment, int slot);
        void DecrementSlotQuantity(int decrement, int slot);
        Matrix4 GetIconProjection();
        Matrix4 GetIconViewMatrix();
        Matrix4 GetIconModelMatrix();
        void AddOrUpdateFaceInMemory(BlockFaceInstance face);
    }
}
