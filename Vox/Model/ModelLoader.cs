using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;
using Newtonsoft.Json.Linq;
using Vox.Texturing;

namespace Vox.Model
{
    public static class ModelLoader
    {
        private static Dictionary<BlockType, BlockModel> models = [];
        static ModelLoader() { }
        public static void LoadModels()
        {
            Directory.CreateDirectory(Window.assets + "BlockModels");

            //Copies block models into foler if not present
            string[] projmodels = Directory.EnumerateFiles("..\\..\\..\\Assets\\BlockModels").ToArray();
            if (Directory.EnumerateFiles(Window.assets + "BlockModels").ToArray().Length == 0)
            {
                Logger.Info("Reloading block models");
                for (int i = 0; i < projmodels.Length; i++)
                {
                    File.Copy(projmodels[i], Window.assets + "BlockModels\\" + Path.GetFileName(projmodels[i]));
                    Logger.Debug($"Loaded model {Path.GetFileName(projmodels[i])}");
                }
            }

            //Populate models to store in memory
            models.Add(BlockType.GRASS_BLOCK, new(JObject.Parse(File.ReadAllText(Window.assets + "BlockModels\\grass.json"))));
            models.Add(BlockType.DIRT_BLOCK, new(JObject.Parse(File.ReadAllText(Window.assets + "BlockModels\\dirt.json"))));
            models.Add(BlockType.STONE_BLOCK, new(JObject.Parse(File.ReadAllText(Window.assets + "BlockModels\\stone.json"))));

        }
        public static Dictionary<BlockType, BlockModel> GetModels()
        {
            return models;
        }
        public static BlockModel GetModel(BlockType type)
        {
            return models[type];
        }
        public static void Destroy()
        {
            models.Clear();
        }
    }
}
