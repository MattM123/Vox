using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL4;
using StbiSharp;
using System.IO;
using System.Drawing;
using Vox.Model;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Vox.Texturing
{
    public class TextureLoader
    {

        private static int texId = 0;
        private static StbiImage image;
        private static int width;
        private static int height;
        private static int channels;
        private static string assets = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\.voxelGame\\Assets\\";
        private static int numLayers = 3;

        static TextureLoader()
        {

            Directory.CreateDirectory(assets + "Textures");
        }

        public static int LoadTextures()
        {
            string[] tex = Directory.EnumerateFiles(assets + "Textures").ToArray();
            string[] projTex = Directory.EnumerateFiles("..\\..\\..\\Assets\\Textures").ToArray();

            width = 16;
            height = 16;

            //========================
            //Texture Setup
            //========================

            //Copies textures into foler if not present
            if (tex.Length == 0)
            {
                numLayers = projTex.Length;
                Logger.Info("Reloading default textures");
                for (int i = 0; i < projTex.Length; i++)
                {
                    File.Copy(projTex[i], assets + "Textures\\" + Path.GetFileName(projTex[i]));
                    Logger.Debug($"Loaded texture {Path.GetFileName(projTex[i])}");
                }
                //update int value
                tex = Directory.EnumerateFiles(assets + "Textures").ToArray();
            }
            numLayers = tex.Length;

            //Create and bind texture array
            int texId = GL.GenTexture();

            GL.BindTexture(TextureTarget.Texture2DArray, texId);
            GL.TexStorage3D(TextureTarget3d.Texture2DArray, 1, SizedInternalFormat.Rgba8, width, height, numLayers);

            // Setting texture parameters
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMaxLevel, 0);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);


            // Upload texture sub-images to each layer
            // string[] tex = Directory.EnumerateFiles(assets + "Textures").ToArray();
            for (int i = 0; i < numLayers; i++)
            {
                using var memoryStream = new MemoryStream();
                FileStream stream = File.OpenRead(tex[i]);
                stream.CopyTo(memoryStream);
                image = Stbi.LoadFromMemory(memoryStream, 4);

                byte[] subimageData = image.Data.ToArray();
                // Use TexSubImage3D to upload the image data to the specific layer
                GL.TexSubImage3D(
                    TextureTarget.Texture2DArray, //Target
                    0,                            //Level
                    0, 0, i,                      //XYZ offset
                    width, height, 1,             //Width, height, depth
                    PixelFormat.Rgba,             //Pixel Format
                    PixelType.UnsignedByte,       //Pixel Type
                    subimageData);                //Image data
                image.Dispose();
            }
           
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2DArray);
            return texId;
        }

        public static void Unbind()
        {
            GL.BindTexture(TextureTarget.Texture2DArray, 0);
            GL.DeleteTexture(texId);
        }
    }
}

