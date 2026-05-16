
using System.Runtime.InteropServices;
using OpenTK.Mathematics;
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

        public static Matrix4 GetIconProjection()
        {
            return Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(45f),
                1.0f,   // square aspect ratio matches the 256x256 FBO
                0.1f,
                100.0f
            );
        }

        public static Matrix4 GetIconViewMatrix()
        {
            float distance = 1.5f;
            return Matrix4.LookAt(
                new Vector3(distance, -distance, distance),
                new Vector3(0f, 0.3f, 0f),
                Vector3.UnitY
            );
        }

        public static Matrix4 GetIconModelMatrix()
        {
            return 
                Matrix4.Identity *                                                                                  // Standard Identity
                Matrix4.CreateTranslation(-0.5f, -0.5f, -0.5f) *                                                    // Position block model center on the origin
                Matrix4.CreateFromAxisAngle(Vector3.UnitY, MathHelper.DegreesToRadians(Window.GetAngle() * 150)) *  // Rotated model around Y Axis
                Matrix4.CreateRotationX(MathHelper.DegreesToRadians(180));                                          // Make block model right side up instead of up side down
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
