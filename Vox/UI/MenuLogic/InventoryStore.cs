
using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using Vox.Enums;
using Vox.Model;
using Vox.Rendering;

namespace Vox.UI.MenuLogic
{

    public class InventoryStore : IInventoryStore
    {
        private readonly Dictionary<int, KeyValuePair<BlockType, int>> slots = new(36);
        private readonly ISSBOManager? _ssboManager;


        /// <summary>
        /// Initializes a new instance of the InventoryStore class with the specified SSBO manager.
        /// </summary>
        /// <param name="ssboManager">The inventory SSBO manager used to manage block icon rendering for inventory slots.</param>
        public InventoryStore(ISSBOManager ssboManager)
        {
            _ssboManager = ssboManager ?? throw new Exception(nameof(ssboManager) + " is null in InventoryStore");

            for (int i = 0; i < 36; i++)
                slots.Add(i, new(BlockType.AIR, 0));
        }

        /// <summary>
        /// Sets the block type and quantity for the slot at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the slot to update.</param>
        /// <param name="blockType">The type of block to assign to the slot.</param>
        /// <param name="quantity">The number of blocks to set for the slot. If less than zero, the quantity is set to zero.</param>
        public void SetSlot(int index, BlockType blockType, int quantity)
        {
            if (quantity < 0)
                quantity = 0;

            slots[index] = new(blockType, quantity);
        }

        /// <summary>
        /// Gets the collection of slots in this inventory store.
        /// </summary>
        /// <returns>A dictionary mapping slot indices to key-value pairs, where each key is a block type and each value is the
        /// count of that block type in the slot.</returns>
        public Dictionary<int, KeyValuePair<BlockType, int>> GetSlots()
        {
            return slots;
        }

        /// <summary>
        /// Increases the quantity of items in the specified slot by the given increment value.
        /// </summary>
        /// <param name="increment">The amount by which to increase the quantity in the slot. Must be greater than zero to have an effect.</param>
        /// <param name="slot">The zero-based index of the slot whose quantity will be incremented.</param>
        public void IncrementSlotQuantity(int increment, int slot)
        {
            BlockType blocktype = slots[slot].Key;
            int quantity = slots[slot].Value;

            if (increment > 0)
                slots[slot] = new(blocktype, quantity + increment);
        }

        /// <summary>
        /// Decreases the quantity of items in the specified slot by the given amount.
        /// </summary>
        /// <param name="decrement">The number of items to remove from the slot. Must be greater than zero to have an effect.</param>
        /// <param name="slot">The zero-based index of the slot whose quantity will be decremented.</param>
        public void DecrementSlotQuantity(int decrement, int slot)
        {
            BlockType blocktype = slots[slot].Key;
            int quantity = slots[slot].Value;

            if (decrement > 0)
                slots[slot] = new(blocktype, quantity - decrement);
        }

        /// <summary>
        /// Gets the perspective projection for rendering the inventory slot icon.
        /// </summary>
        /// <remarks>All inventory icons use the same projection matrix for a consistent perspective.</remarks>
        /// <returns>
        /// <see cref="Matrix4"/> configured with a 45-degree field of view, a 1:1 aspect
        /// ratio, and near and far clipping planes at 0.1 and 100.0 units, respectively.
        /// </returns>
        public Matrix4 GetIconProjection()
        {
            return Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(45f),
                1.0f,   // square aspect ratio matches the 256x256 FBO
                0.1f,
                100.0f
            );
        }

        /// <summary>
        /// Gets the view matrix for rendering the inventory slot icon.
        /// </summary>
        /// <remarks>All inventory icons use the same view matrix for a consistent perspective.</remarks>
        /// <returns>A <see cref="Matrix4"/> representing the view transformation for the icon perspective.</returns>
        public Matrix4 GetIconViewMatrix()
        {
            float distance = 1.5f;
            return Matrix4.LookAt(
                new Vector3(distance, -distance, distance),
                new Vector3(0f, 0f, 0f),
                Vector3.UnitY
            );
        }

        /// <summary>
        /// Get the model matrix for the inventory slot icon.
        /// </summary>
        /// <remarks>All inventory icons use the same model matrix for a consistent orientation</remarks>
        /// <returns></returns>
        public Matrix4 GetIconModelMatrix()
        {
            return 
                Matrix4.Identity *                                                                                  // Standard Identity
                Matrix4.CreateTranslation(-0.5f, -0.5f, -0.5f) *                                                    // Position block model center on the origin
                Matrix4.CreateFromAxisAngle(Vector3.UnitY, MathHelper.DegreesToRadians(Window.GetAngle() * 150)) *  // Rotated model around Y Axis
                Matrix4.CreateRotationX(MathHelper.DegreesToRadians(180));                                          // Make block model right side up instead of up side down
        }

        /// <summary>   
        /// Uploads a single block face to the SSBO for rendering.
        /// If the index is already present, updates the face data at that index.
        /// </summary>
        /// <param name="face"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public void AddOrUpdateFaceInMemory(BlockFaceInstance face)
        {
            //Write face directly to SSBO
            unsafe
            {
                int offset = face.index * Marshal.SizeOf<BlockFaceInstance>();
                byte* basePtr = (byte*)_ssboManager!.GetSSBO(SSBO.Inventory).Pointer;

                BlockFaceInstance* instancePtr = (BlockFaceInstance*)(basePtr + offset);

                int instanceSize = Marshal.SizeOf<BlockFaceInstance>();

                if (offset + instanceSize > _ssboManager!.GetSSBO(SSBO.Inventory).Size)
                    throw new InvalidOperationException("SSBO overflow");

                *instancePtr = face;
            }
        }
    }
}
