
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Vox.Assets;
using Vox.Assets.Models;
using Vox.Enums;
using Vox.Model;
using Vox.Rendering;

namespace Vox.UI.MenuLogic
{

    public class InventoryStore : IInventoryStore
    {
        private readonly Dictionary<int, KeyValuePair<BlockType, int>> _slots = new(36);
        private readonly IssboManager? _ssboManager;
        private readonly IAssetLookup? _assetLookup;

        private readonly int _inventoryVao;
        private readonly int _inventoryDisplayFbo;
        private readonly int _inventoryDisplayTexture;

        private readonly int _inventoryIconSlotFbo;
        private readonly int _inventoryIconAtlas;

        private KeyValuePair<BlockType, int> _movedSlot = new(BlockType.Air, 0);
        private bool _isMoving = false;
        private int _hoveredSlotIndex = -1;
        private bool _pickupToggle = false;

        /// <summary>
        /// Initializes a new instance of the InventoryStore class with the specified SSBO manager.
        /// </summary>
        /// <param displayName="ssboManager">The inventory SSBO manager used to manage block icon rendering for inventory slots.</param>
        public InventoryStore(IssboManager ssboManager, IAssetLookup assetLookup)
        {
            _ssboManager = ssboManager;
            _assetLookup = assetLookup;
            _inventoryVao = GL.GenVertexArray();

            for (int i = 0; i < 36; i++)
                _slots.Add(i, new(BlockType.Air, 0));

            //------------------------Inventory Block Display---------------------------------
            //Inventory animation framebuffer
            _inventoryDisplayFbo = GL.GenFramebuffer();
            _inventoryDisplayTexture = GL.GenTexture();

            string displayName = "FrameBuffer: Inventory Display";
            GL.ObjectLabel(ObjectIdentifier.Framebuffer, _inventoryDisplayFbo, displayName.Length, displayName);
            string displayTexture = "Texture: Inventory Display";
            GL.ObjectLabel(ObjectIdentifier.Texture, _inventoryDisplayTexture, displayTexture.Length, displayTexture);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _inventoryDisplayFbo);

            GL.BindTexture(TextureTarget.Texture2D, _inventoryDisplayTexture);
            GL.TexImage2D(
                TextureTarget.Texture2D,
                0,
                InternalFormat.Rgba,
                256, 256,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                IntPtr.Zero
            );

            GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            //Attach color texture to frame buffer
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _inventoryDisplayFbo);
            GL.FramebufferTexture2D(
                FramebufferTarget.Framebuffer,
                FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D,
                _inventoryDisplayTexture,
                0
            );



            //------------------------Inventory Icon Display---------------------------------
            //Inventory icon framebuffer
            _inventoryIconSlotFbo = GL.GenFramebuffer();
            _inventoryIconAtlas = GL.GenTexture();

            string iconSlotFboName = "FrameBuffer: Inventory Icon";
            GL.ObjectLabel(ObjectIdentifier.Framebuffer, _inventoryIconSlotFbo, iconSlotFboName.Length, iconSlotFboName);

            string iconAtlasName = "Texture: Icon Atlas";
            GL.ObjectLabel(ObjectIdentifier.Texture, _inventoryIconAtlas, iconAtlasName.Length, iconAtlasName);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _inventoryIconSlotFbo);

            GL.BindTexture(TextureTarget.Texture2D, _inventoryIconAtlas);
            GL.TexImage2D(
                TextureTarget.Texture2D,
                0,
                InternalFormat.Rgba,
                4096, 4096,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                IntPtr.Zero
            );

            GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            //Attach color texture to frame buffer
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _inventoryIconSlotFbo);
            GL.FramebufferTexture2D(
                FramebufferTarget.Framebuffer,
                FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D,
                _inventoryIconAtlas,
                0
            );

        }

        /// <summary>
        /// Sets the block type and quantity for the slot at the specified index.
        /// </summary>
        /// <param displayName="index">The zero-based index of the slot to update.</param>
        /// <param displayName="blockType">The _type of block to assign to the slot.</param>
        /// <param displayName="quantity">The number of blocks to set for the slot. If less than zero, the quantity is set to zero.</param>
        public void SetSlot(int index, BlockType blockType, int quantity)
        {
            if (quantity < 0)
                quantity = 0;

            _slots[index] = new(blockType, quantity);
        }

        /// <summary>
        /// Gets the collection of slots in this inventory store.
        /// </summary>
        /// <returns>A dictionary mapping slot indices to key-value pairs, where each key is a block _type and each value is the
        /// count of that block _type in the slot.</returns>
        public Dictionary<int, KeyValuePair<BlockType, int>> GetSlots()
        {
            return _slots;
        }

        /// <summary>
        /// Increases the quantity of items in the specified slot by the given increment value.
        /// </summary>
        /// <param displayName="increment">The amount by which to increase the quantity in the slot. Must be greater than zero to have an effect.</param>
        /// <param displayName="slot">The zero-based index of the slot whose quantity will be incremented.</param>
        public void IncrementSlotQuantity(int increment, int slot)
        {
            BlockType blocktype = _slots[slot].Key;
            int quantity = _slots[slot].Value;

            if (increment > 0)
                _slots[slot] = new(blocktype, quantity + increment);
        }

        /// <summary>
        /// Decreases the quantity of items in the specified slot by the given amount.
        /// </summary>
        /// <param displayName="decrement">The number of items to remove from the slot. Must be greater than zero to have an effect.</param>
        /// <param displayName="slot">The zero-based index of the slot whose quantity will be decremented.</param>
        public void DecrementSlotQuantity(int decrement, int slot)
        {
            BlockType blocktype = _slots[slot].Key;
            int quantity = _slots[slot].Value;

            if (decrement > 0)
                _slots[slot] = new(blocktype, quantity - decrement);
        }

        /// <summary>
        /// Gets the perspective projection for rendering the inventory slot icon.
        /// </summary>
        /// <remarks>All inventory icons use the same projection matrix for a consistent perspective.</remarks>
        /// <returns>
        /// <see cref="Matrix4"/> configured with a 45-degree field of view, a 1:1 aspect
        /// ratio, and near and far clipping planes at 0.1 and 100.0 units, respectively.
        /// </returns>
        public Matrix4 GetDisplayProjection()
        {
            return Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(45f),
                1.0f,
                0.1f,
                100.0f
            );
        }

        /// <summary>
        /// Gets the view matrix for rendering the inventory slot icon.
        /// </summary>
        /// <remarks>All inventory icons use the same view matrix for a consistent perspective.</remarks>
        /// <returns>A <see cref="Matrix4"/> representing the view transformation for the icon perspective.</returns>
        public Matrix4 GetDisplayViewMatrix()
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
        public Matrix4 GetDisplayModelMatrix()
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
        /// <param displayName="face"></param>
        /// <exception cref="InvalidOperationException"></exception>
        private void AddOrUpdateFaceInMemory(BlockFaceInstance face)
        {
            //Write face directly to SSBO
            unsafe
            {
                int offset = face.index * Marshal.SizeOf<BlockFaceInstance>();
                byte* basePtr = (byte*)_ssboManager!.GetSsbo(Ssbo.Inventory).Pointer;

                BlockFaceInstance* instancePtr = (BlockFaceInstance*)(basePtr + offset);

                int instanceSize = Marshal.SizeOf<BlockFaceInstance>();

                if (offset + instanceSize > _ssboManager!.GetSsbo(Ssbo.Inventory).Size)
                    throw new InvalidOperationException("SSBO overflow");

                *instancePtr = face;
            }
        }

        /// <summary>
        /// Updates the SSBO with the block face data for the specified <see cref="BlockType"/>. 
        /// This method retrieves the block model from the asset lookup for the given block type.
        /// </summary>
        /// <param displayName="modelblockType">The <see cref="BlockType"> to update the SSBO with</see></param>
        public void UpdateSsboBlock(BlockType modelblockType)
        {
            BlockModel model = _assetLookup!.GetModel(modelblockType);
            string modelElements = model.GetElements().ElementAt(0).ToString();

            int start = modelElements.IndexOf("from=(") + 6;
            int end = modelElements.IndexOf(")", start);

            string coords = modelElements[start..end];
            string[] parts = coords.Split(',');


            int x = int.Parse(parts[0].Trim());
            int y = int.Parse(parts[1].Trim());
            int z = int.Parse(parts[2].Trim());

            AddOrUpdateFaceInMemory(
                new BlockFaceInstance
                {
                    facePosition = new Vector3(x, y, z),
                    index = 0,
                    lighting = 56149,
                    textureLayer = (int)model.GetTextureLayer(BlockFace.North),
                    faceDirection = (int)BlockFace.North
                });
            AddOrUpdateFaceInMemory(
                new BlockFaceInstance
                {
                    facePosition = new Vector3(x, y, z),
                    index = 1,
                    lighting = 56149,
                    textureLayer = (int)model.GetTextureLayer(BlockFace.South),
                    faceDirection = (int)BlockFace.South
                });
            AddOrUpdateFaceInMemory(
                new BlockFaceInstance
                {
                    facePosition = new Vector3(x, y, z),
                    index = 2,
                    lighting = 56149,
                    textureLayer = (int)model.GetTextureLayer(BlockFace.East),
                    faceDirection = (int)BlockFace.East
                });
            AddOrUpdateFaceInMemory(
                new BlockFaceInstance
                {
                    facePosition = new Vector3(x, y, z),
                    index = 3,
                    lighting = 56149,
                    textureLayer = (int)model.GetTextureLayer(BlockFace.West),
                    faceDirection = (int)BlockFace.West
                });
            AddOrUpdateFaceInMemory(
                new BlockFaceInstance
                {
                    facePosition = new Vector3(x, y, z),
                    index = 4,
                    lighting = 56149,
                    textureLayer = (int)model.GetTextureLayer(BlockFace.Up),
                    faceDirection = (int)BlockFace.Up
                });
            AddOrUpdateFaceInMemory(
                new BlockFaceInstance
                {
                    facePosition = new Vector3(x, y, z),
                    index = 5,
                    lighting = 56149,
                    textureLayer = (int)model.GetTextureLayer(BlockFace.Down),
                    faceDirection = (int)BlockFace.Down
                });
        }

        public int GetInventoryVao()
        {
            return _inventoryVao;
        }
        public int GetInventoryDisplayFbo()
        {
            return _inventoryDisplayFbo;
        }
        public int GetInventoryDisplayTexture()
        {
            return _inventoryDisplayTexture;
        }
        public int GetInventoryIconAtlas()
        {
            return _inventoryIconAtlas;
        }
        public int GetInventoryIconSlotFbo()
        {
            return _inventoryIconSlotFbo;
        }
        public KeyValuePair<BlockType, int> GetHandItem()
        {
            return _movedSlot;
        }
        public bool IsItemBeingMoved()
        {
            return _isMoving;
        }
        public void SetHandItem(KeyValuePair<BlockType, int> slot)
        {
            _movedSlot = slot;
        }
        public void SetIsItemBeingMoved(bool isBeingMoved)
        {
            _isMoving = isBeingMoved;
        }
        public int GetHoveredSlotIndex()
        {
            return _hoveredSlotIndex;
        }
        public void SetHoveredSlotIndex(int index)
        {
            _hoveredSlotIndex = index;
        }
        public bool GetPickupToggle()
        {
            return _pickupToggle;
        }
        public void SetPickupToggle(bool toggle)
        {
            _pickupToggle = toggle;
        }
    }
}
