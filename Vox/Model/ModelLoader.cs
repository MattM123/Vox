
using Newtonsoft.Json.Linq;

namespace Vox.Model
{
    public static class ModelLoader
    {
        private static readonly List<BlockModel> models = [];
        static ModelLoader() { }
        public static void LoadModels()
        {
            Directory.CreateDirectory(Window.assets + "BlockModels");

            //Copies block models into foler if not present
            string[] fileModels = Directory.EnumerateFiles(Path.Combine(Window.assets, "BlockModels")).ToArray();
            string[] projmodels = Directory.EnumerateFiles("..\\..\\..\\Assets\\BlockModels").ToArray();
            if (fileModels.Length != projmodels.Length)
            {
                Logger.Info("Reloading block models");
                for (int i = 0; i < projmodels.Length; i++)
                {
                    File.Copy(projmodels[i], Window.assets + "BlockModels\\" + Path.GetFileName(projmodels[i]), true);
                    Logger.Debug($"Loaded model {Path.GetFileName(projmodels[i])}");
                }
            }

            //Populate models to store in memory
            models.Add(new(JObject.Parse(File.ReadAllText(Window.assets + "BlockModels\\grass.json")), BlockType.GRASS_BLOCK));
            models.Add(new(JObject.Parse(File.ReadAllText(Window.assets + "BlockModels\\dirt.json")), BlockType.DIRT_BLOCK));
            models.Add(new(JObject.Parse(File.ReadAllText(Window.assets + "BlockModels\\stone.json")), BlockType.STONE_BLOCK));
            models.Add(new(JObject.Parse(File.ReadAllText(Window.assets + "BlockModels\\testblock.json")), BlockType.TEST_BLOCK));
            models.Add(new(JObject.Parse(File.ReadAllText(Window.assets + "BlockModels\\target.json")), BlockType.TARGET_BLOCK));
            models.Add(new(JObject.Parse(File.ReadAllText(Window.assets + "BlockModels\\lamp.json")), BlockType.LAMP_BLOCK));
        }
        public static List<BlockModel> GetModels()
        {
            return models;
        }
        public static BlockModel GetModel(BlockType type)
        {
            if (type == BlockType.AIR)
                return new();

            foreach (BlockModel model in models)
                if (model.GetBlockType() == type)
                    return model;

            return new();
        }
        public static BlockModel GetModel(int type)
        {
            if ((BlockType) type == BlockType.AIR)
                return new();

            foreach (BlockModel model in models)
                if (model.GetBlockType() == (BlockType) type)
                    return model;

            return new();
        }
    }
}
