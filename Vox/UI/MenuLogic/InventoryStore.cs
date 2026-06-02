
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
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
        private readonly Dictionary<int, KeyValuePair<BlockType, int>> slots = new(36);
        private readonly ISSBOManager? _ssboManager;
        private readonly IAssetLookup? _assetLookup;

        private readonly int _inventoryVAO;
        private readonly int _inventoryDisplayFBO;
        private readonly int _inventoryDisplayTexture;

        private readonly int _inventoryIconSlotFBO;
        private readonly int _inventoryIconAtlas;


        /// <summary>
        /// Initializes a new instance of the InventoryStore class with the specified SSBO manager.
        /// </summary>
        /// <param displayName="ssboManager">The inventory SSBO manager used to manage block icon rendering for inventory slots.</param>
        public InventoryStore(ISSBOManager ssboManager, IAssetLookup assetLookup)
        {
            _ssboManager = ssboManager;
            _assetLookup = assetLookup;
            _inventoryVAO = GL.GenVertexArray();

            for (int i = 0; i < 36; i++)
                slots.Add(i, new(BlockType.AIR, 0));




            //------------------------Inventory Block Display---------------------------------
            //Inventory animation framebuffer
            _inventoryDisplayFBO = GL.GenFramebuffer();
            _inventoryDisplayTexture = GL.GenTexture();

            string displayName = "FrameBuffer: Inventory Display";
            GL.ObjectLabel(ObjectLabelIdentifier.Framebuffer, _inventoryDisplayFBO, displayName.Length, displayName);
            string displayTexture = "Texture: Inventory Display";
            GL.ObjectLabel(ObjectLabelIdentifier.Texture, _inventoryDisplayTexture, displayTexture.Length, displayTexture);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _inventoryDisplayFBO);

            GL.BindTexture(TextureTarget.Texture2D, _inventoryDisplayTexture);
            GL.TexImage2D(
                TextureTarget.Texture2D,
                0,
                PixelInternalFormat.Rgba,
                256, 256,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                IntPtr.Zero
            );

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            //Attach color texture to frame buffer
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _inventoryDisplayFBO);
            GL.FramebufferTexture2D(
                FramebufferTarget.Framebuffer,
                FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D,
                _inventoryDisplayTexture,
                0
            );



            //------------------------Inventory Icon Display---------------------------------
            //Inventory icon framebuffer
            _inventoryIconSlotFBO = GL.GenFramebuffer();
            _inventoryIconAtlas = GL.GenTexture();
            Console.WriteLine("Atlas: " + _inventoryIconAtlas);
            string iconSlotFBOName = "FrameBuffer: Inventory Icon";
            GL.ObjectLabel(ObjectLabelIdentifier.Framebuffer, _inventoryIconSlotFBO, iconSlotFBOName.Length, iconSlotFBOName);

            string iconAtlasName = "Texture: Icon Atlas";
            GL.ObjectLabel(ObjectLabelIdentifier.Texture, _inventoryIconAtlas, iconAtlasName.Length, iconAtlasName);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _inventoryIconSlotFBO);

            GL.BindTexture(TextureTarget.Texture2D, _inventoryIconAtlas);
            GL.TexImage2D(
                TextureTarget.Texture2D,
                0,
                PixelInternalFormat.Rgba,
                1024, 1024,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                IntPtr.Zero
            );

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            //Attach color texture to frame buffer
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _inventoryIconSlotFBO);
            GL.FramebufferTexture2D(
                FramebufferTarget.Framebuffer,
                FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D,
                _inventoryIconAtlas,
                0
            );

        }

        /// <summary>
        /// Sets the block _type and quantity for the slot at the specified index.
        /// </summary>
        /// <param displayName="index">The zero-based index of the slot to update.</param>
        /// <param displayName="blockType">The _type of block to assign to the slot.</param>
        /// <param displayName="quantity">The number of blocks to set for the slot. If less than zero, the quantity is set to zero.</param>
        public void SetSlot(int index, BlockType blockType, int quantity)
        {
            if (quantity < 0)
                quantity = 0;

            slots[index] = new(blockType, quantity);
        }

        /// <summary>
        /// Gets the collection of slots in this inventory store.
        /// </summary>
        /// <returns>A dictionary mapping slot indices to key-value pairs, where each key is a block _type and each value is the
        /// count of that block _type in the slot.</returns>
        public Dictionary<int, KeyValuePair<BlockType, int>> GetSlots()
        {
            return slots;
        }

        /// <summary>
        /// Increases the quantity of items in the specified slot by the given increment value.
        /// </summary>
        /// <param displayName="increment">The amount by which to increase the quantity in the slot. Must be greater than zero to have an effect.</param>
        /// <param displayName="slot">The zero-based index of the slot whose quantity will be incremented.</param>
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
        /// <param displayName="decrement">The number of items to remove from the slot. Must be greater than zero to have an effect.</param>
        /// <param displayName="slot">The zero-based index of the slot whose quantity will be decremented.</param>
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
                byte* basePtr = (byte*)_ssboManager!.GetSSBO(SSBO.Inventory).Pointer;

                BlockFaceInstance* instancePtr = (BlockFaceInstance*)(basePtr + offset);

                int instanceSize = Marshal.SizeOf<BlockFaceInstance>();

                if (offset + instanceSize > _ssboManager!.GetSSBO(SSBO.Inventory).Size)
                    throw new InvalidOperationException("SSBO overflow");

                *instancePtr = face;
            }
        }

        /// <summary>
        /// Updates the SSBO with the block face data for the specified <see cref="BlockType"/>. 
        /// This method retrieves the block model from the asset lookup for the given block type.
        /// </summary>
        /// <param displayName="modelblockType">The <see cref="BlockType"> to update the SSBO with</see></param>
        public void UpdateSSBOBlock(BlockType modelblockType)
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
                    textureLayer = (int)model.GetTextureLayer(BlockFace.NORTH),
                    faceDirection = (int)BlockFace.NORTH
                });
            AddOrUpdateFaceInMemory(
                new BlockFaceInstance
                {
                    facePosition = new Vector3(x, y, z),
                    index = 1,
                    lighting = 56149,
                    textureLayer = (int)model.GetTextureLayer(BlockFace.SOUTH),
                    faceDirection = (int)BlockFace.SOUTH
                });
            AddOrUpdateFaceInMemory(
                new BlockFaceInstance
                {
                    facePosition = new Vector3(x, y, z),
                    index = 2,
                    lighting = 56149,
                    textureLayer = (int)model.GetTextureLayer(BlockFace.EAST),
                    faceDirection = (int)BlockFace.EAST
                });
            AddOrUpdateFaceInMemory(
                new BlockFaceInstance
                {
                    facePosition = new Vector3(x, y, z),
                    index = 3,
                    lighting = 56149,
                    textureLayer = (int)model.GetTextureLayer(BlockFace.WEST),
                    faceDirection = (int)BlockFace.WEST
                });
            AddOrUpdateFaceInMemory(
                new BlockFaceInstance
                {
                    facePosition = new Vector3(x, y, z),
                    index = 4,
                    lighting = 56149,
                    textureLayer = (int)model.GetTextureLayer(BlockFace.UP),
                    faceDirection = (int)BlockFace.UP
                });
            AddOrUpdateFaceInMemory(
                new BlockFaceInstance
                {
                    facePosition = new Vector3(x, y, z),
                    index = 5,
                    lighting = 56149,
                    textureLayer = (int)model.GetTextureLayer(BlockFace.DOWN),
                    faceDirection = (int)BlockFace.DOWN
                });
        }

        public int GetInventoryVAO()
        {
            return _inventoryVAO;
        }
        public int GetInventoryDisplayFBO()
        {
            return _inventoryDisplayFBO;
        }
        public int GetInventoryDisplayTexture()
        {
            return _inventoryDisplayTexture;
        }
        public int GetInventoryIconAtlas()
        {
            return _inventoryIconAtlas;
        }
        public int GetInventoryIconSlotFBO()
        {
            return _inventoryIconSlotFBO;
        }
    }
}
