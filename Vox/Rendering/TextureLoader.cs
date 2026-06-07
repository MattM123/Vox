
using System.IO;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using StbiSharp;
using Vox.Assets;
using Vox.Enums;
using Vox.Exceptions;
using Vox.Model;
using Vox.UI.MenuLogic;
using Buffer = System.Buffer;
namespace Vox.Rendering
{
    public class TextureLoader : ITextureLoader
    {
        private readonly string? _assetsPath;
        private readonly IAssetLookup? _assetLookup;
        private readonly IInventoryStore? _inventoryStore;

        private List<int> _textureIDs;
        private int textureSize = 16;
        private int numberOfTextures = 7;
        private readonly StbiImage _cachedAtlasData;
        private readonly IShaderManager? _shaderManager;

        public TextureLoader(string assetsPath, IAssetLookup assetLookup, IInventoryStore inventoryStore, IShaderManager shaderManager)
        {
            _textureIDs = [];
            _assetsPath = assetsPath;
            _assetLookup = assetLookup;
            _inventoryStore = inventoryStore;
            _shaderManager = shaderManager;


            Directory.CreateDirectory(_assetsPath + "Textures");
            Directory.CreateDirectory(_assetsPath + "BlockTextures");

            //Load atlas into memory
            using var memoryStream = new MemoryStream();
            using FileStream stream = File.OpenRead(Path.Combine(_assetsPath!, "Atlas.png"));
            stream.CopyTo(memoryStream);
            memoryStream.Position = 0;
            _cachedAtlasData = Stbi.LoadFromMemory(memoryStream, 4);
            stream.Close();
        }

        public int LoadTextures()
        {
            //========================
            //Texture Setup
            //========================

            //Create and bind texture array
            int textureArray = GL.GenTexture();         

            GL.ActiveTexture(TextureUnit.Texture4);
            GL.BindTexture(TextureTarget.Texture2DArray, textureArray);
            GL.TexStorage3D(TextureTarget3d.Texture2DArray, 1, SizedInternalFormat.Rgba8, textureSize, textureSize, numberOfTextures);

            // Setting texture parameters
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMaxLevel, 0);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);

            byte[] layerBuffer = new byte[
                numberOfTextures *
                textureSize *
                textureSize *
                4];

            byte[] atlas = _cachedAtlasData.Data.ToArray();
            int texturesPerRow = 64;
            int channels = 4;

            int rowSize = textureSize * channels;
            int copySize = textureSize * channels;

            List<Texture> textures = _assetLookup!.GetTexturesWithLayers();

            for (int i = 0; i < textures.Count; i++)
            {
                int layer = _assetLookup.GetTextureLayerFromTexture(textures[i]);
                int atlasWidth = texturesPerRow * textureSize;

                for (int y = 0; y < textureSize; y++)
                {
                    int tileX = i % texturesPerRow;
                    int tileY = i / texturesPerRow;

                    int srcOffset =
                        ((tileY * textureSize + y) * atlasWidth +
                         tileX * textureSize) * channels;

                    int dstOffset =
                        ((layer * textureSize * textureSize) +
                         (y * textureSize)) * channels;

                    Buffer.BlockCopy(
                        atlas,
                        srcOffset,
                        layerBuffer,
                        dstOffset,
                        copySize
                    );
                }
            }


            GL.TexSubImage3D(
                TextureTarget.Texture2DArray,
                0,
                0, 0, 0,
                textureSize,
                textureSize,
                numberOfTextures,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                layerBuffer
            );

            _textureIDs.Add(textureArray);
            return textureArray;
        }
        public int LoadSingleTexture(string fileBlockName)
        {
            // Generate handle
            int texId = GL.GenTexture();

            // Bind the handle
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, texId);

            // Here we open a stream to the file and pass it to StbImageSharp to load.
            using (Stream stream = File.OpenRead(Path.Combine(_assetsPath!, "BlockTextures", fileBlockName)))
            {
                using var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
                using var image = Stbi.LoadFromMemory(memoryStream, 4);

                GL.TexImage2D(
                    TextureTarget.Texture2D,
                    0,
                    PixelInternalFormat.Rgba,
                    image.Width, image.Height,
                    0,
                    PixelFormat.Rgba,
                    PixelType.UnsignedByte,
                    image.Data.ToArray()
                );
            }

            // Now that our texture is loaded, we can set a few settings to affect how the image appears on rendering.

            // First, we set the min and mag filter. These are used for when the texture is scaled down and up, respectively.
            // Here, we use Linear for both. This means that OpenGL will try to blend pixels, meaning that textures scaled too far will look blurred.
            // You could also use (amongst other options) Nearest, which just grabs the nearest pixel, which makes the texture look pixelated if scaled too far.
            // NOTE: The default settings for both of these are LinearMipmap. If you leave these as default but don't generate mipmaps,
            // your image will fail to render at all (usually resulting in pure black instead).
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            // Now, set the wrapping mode. S is for the X axis, and T is for the Y axis.
            // We set this to Repeat so that textures will repeat when wrapped. Not demonstrated here since the texture coordinates exactly match
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

            _textureIDs.Add(texId);
            return texId;
        }
        public int UpdateExistingTexture(int texId, int layer)
        {

            //Setup the 2D Texture handle
            GL.BindTexture(TextureTarget.Texture2D, texId);

            //Calculate coordinates for the single texture
            int texturesPerRow = 64; // Adjust based on your Atlas.png width
            int channels = 4;
            int tileX = layer % texturesPerRow;
            int tileY = layer / texturesPerRow;

            // 4. Extract only the bytes for this specific tile
            byte[] tileBuffer = new byte[textureSize * textureSize * channels];
            int atlasWidthPixels = _cachedAtlasData.Width; // Assuming you have dimensions available

            for (int y = 0; y < textureSize; y++)
            {
                int srcOffset = ((tileY * textureSize + y) * atlasWidthPixels + (tileX * textureSize)) * channels;
                int dstOffset = (y * textureSize) * channels;

                Buffer.BlockCopy(_cachedAtlasData.Data.ToArray(), srcOffset, tileBuffer, dstOffset, textureSize * channels);
            }

            // Overwrite the existing texture pixels without creating a new handle
            GL.TexSubImage2D(
                TextureTarget.Texture2D,
                0,
                0, 0,
                textureSize,
                textureSize,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                tileBuffer // Your new data
            );

            return texId;
        }

        public int GenerateIconAtlas()
        {
            int atlasSize = 4096;

            //Save current state
            int prevTex = GL.GetInteger(GetPName.TextureBinding2D);
            int prevFBO = GL.GetInteger(GetPName.FramebufferBinding);
            int prevProgram = GL.GetInteger(GetPName.CurrentProgram);
            int prevVAO = GL.GetInteger(GetPName.VertexArrayBinding);
            int[] prevViewport = new int[4];
            GL.GetInteger(GetPName.Viewport, prevViewport);


            int texId = _inventoryStore!.GetInventoryIconAtlas();

            GL.BindTexture(TextureTarget.Texture2D, texId);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            // 
            Matrix4 modelMatrix = Matrix4.Identity *                                                                                  // Standard Identity
                                  Matrix4.CreateTranslation(-0.5f, -0.5f, -0.5f) *                                                    // Position block model center on the origin
                                  Matrix4.CreateFromAxisAngle(Vector3.UnitY, MathHelper.DegreesToRadians(Window.GetAngle() * 150)) *  // Rotated model around Y Axis
                                  Matrix4.CreateRotationX(MathHelper.DegreesToRadians(180));                                          // Make block model right side up instead of up side down



            float distance = 1.5f;
            Matrix4 viewMatrix = Matrix4.LookAt(
                new Vector3(distance, distance, distance),
                new Vector3(0f, 0f, 0f),
                Vector3.UnitY
            );

            _shaderManager?.GetShaderProgram("Inventory").Bind()
                .SetMatrixUniform("projectionMatrix", _inventoryStore!.GetDisplayProjection())
                .SetMatrixUniform("viewMatrix", _inventoryStore.GetDisplayViewMatrix())
                .SetMatrixUniform("modelMatrix", modelMatrix);

            // Bind frame buffer
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _inventoryStore!.GetInventoryIconSlotFBO());

            FramebufferErrorCode status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
            {
                Logger.Error($"Icon Atlas Framebuffer error: {status}");
            } else
            {
                Logger.Success("Successfully bound icon atlas framebuffer with status " + status);
            }

            GL.BindVertexArray(_inventoryStore.GetInventoryVAO());
            int viewportSize = 128;
            for (int i = 0; i < Enum.GetValues(typeof(BlockType)).Length; i++)
            {
                BlockType type = (BlockType)i;

                if (type == BlockType.AIR)
                    continue;

                int cols = atlasSize / viewportSize;

                // i - 1 to account for air being skipped, so that we start at the
                // top left of the atlas and fill in row by row
                int x = ((i - 1) % cols) * viewportSize;
                int y = ((i - 1) / cols) * viewportSize;

                // Set where to render the icon model on the atlas
                GL.Viewport(x, y, viewportSize, viewportSize);

                // Forces draws to be synchronous
                // Waits until the previous draw call completes before updating the SSBO with the next blocktype
                GL.Finish();

                // Draw the next icon model
                _inventoryStore?.UpdateSSBOBlock(type);
                
                //Drawing
                GL.DrawArraysInstanced(
                    PrimitiveType.TriangleStrip,  // Drawing a triangle strip
                    0,                            // Start from the first vertex in the base geometry
                    4,                            // 4 vertices per face (for triangle strip)
                    6                             // Instance count (number of faces to draw)
                );
            }

            

            //Restore state
            GL.BindTexture(TextureTarget.Texture2D, prevTex);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, prevFBO);
            GL.UseProgram(prevProgram);
            GL.BindVertexArray(prevVAO);
            GL.Viewport(prevViewport[0], prevViewport[1], prevViewport[2], prevViewport[3]);

            return texId;
        }

    }
}

