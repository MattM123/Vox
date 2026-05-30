
using System.IO;
using OpenTK.Graphics.OpenGL4;
using StbiSharp;
using Vox.Assets;
using Vox.Exceptions;
namespace Vox.Rendering
{
    public class TextureLoader : ITextureLoader
    {
        private readonly string? _assetsPath;
        private readonly IAssetLookup? _assetLookup;
        private List<int> _textureIDs;

        public TextureLoader(string assetsPath, IAssetLookup assetLookup)
        {
            _textureIDs = [];
            _assetsPath = assetsPath ?? throw new ShaderException(nameof(assetsPath) + " is null in TextureLoader");
            _assetLookup = assetLookup ?? throw new ShaderException(nameof(assetLookup) + " is null in TextureLoader" );

            Directory.CreateDirectory(_assetsPath + "Textures");
            Directory.CreateDirectory(_assetsPath + "BlockTextures");
        }

        public int LoadTextures(int slot)
        {

            string[] tex = [.. Directory.EnumerateFiles(_assetsPath + "BlockTextures")];
 
            int width = 16;
            int height = 16;

            //========================
            //Texture Setup
            //========================

            int numLayers = tex.Length;

            //Create and bind texture array
            int texId = GL.GenTexture();

            GL.ActiveTexture(TextureUnit.Texture0 + slot);
            GL.BindTexture(TextureTarget.Texture2DArray, texId);
            GL.TexStorage3D(TextureTarget3d.Texture2DArray, 1, SizedInternalFormat.Rgba8, width, height, numLayers);

            // Setting texture parameters
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMaxLevel, 0);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);

            for (int i = 0; i < numLayers; i++)
            {
                using var memoryStream = new MemoryStream();
                using FileStream stream = File.OpenRead(tex[i]);
                stream.CopyTo(memoryStream);
                memoryStream.Position = 0;
                using var image = Stbi.LoadFromMemory(memoryStream, 4);

                string filename = stream.Name[(stream.Name.LastIndexOf('\\') + 1)..];

                byte[] subimageData = image.Data.ToArray();
                
                // Use TexSubImage3D to upload the image data to the specific layer
                GL.TexSubImage3D(
                    TextureTarget.Texture2DArray,                           //Target
                    0,                                                      //Level
                    0, 0,                                                   //XY Offset
                    _assetLookup!.GetTextureValueFromFilename(filename),    //Z offset
                    width, height, 1,                                       //Width, height, depth
                    PixelFormat.Rgba,                                       //Pixel Format
                    PixelType.UnsignedByte,                                 //Pixel Type
                    subimageData);                                          //Image data
            }

            GL.GenerateMipmap(GenerateMipmapTarget.Texture2DArray);
            _textureIDs.Add(texId);
            return texId;
        }

        /*
         * Loads a single texture such as a diffuse or specular map
         */
        public int LoadSingleTexture(string fileBlockName)
        {
            // Generate handle
            int texId = GL.GenTexture();

            // Bind the handle
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, texId);

            //string[] tex = [.. Directory.EnumerateFiles(Path.Combine(assets, "BlockTextures", fileBlockName))];
           // string[] projTex = Directory.EnumerateFiles("..\\..\\..\\Assets\\Textures").ToArray();

            //Copies textures into foler if not present
            //if (tex.Length != projTex.Length)
            //{
            //    numLayers = projTex.Length;
            //    Logger.Info("Reloading default textures");
            //    for (int i = 0; i < projTex.Length; i++)
            //    {
            //        File.Copy(projTex[i], Path.Combine(assets, "Textures", Path.GetFileName(projTex[i])), true);
            //        Logger.Debug($"Loaded texture {Path.GetFileName(projTex[i])}");
            //    }
            //    //update int value
            //    tex = Directory.EnumerateFiles(Path.Combine(assets, "Textures")).ToArray();
            //}

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
        public void CleanupTextures()
        {
            GL.BindTexture(TextureTarget.Texture2DArray, 0);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            foreach (int id in _textureIDs)
            {
                GL.DeleteTexture(id);
                _textureIDs.Remove(id);
            }
        }
        public void CleanupTexture(int texid)
        {
            GL.BindTexture(TextureTarget.Texture2DArray, 0);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            foreach (int id in _textureIDs)
            {
                if (id == texid)
                {
                    GL.DeleteTexture(id);
                    _textureIDs.Remove(id);
                    break;
                }
            }
        }
    }
}

