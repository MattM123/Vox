
using Newtonsoft.Json.Linq;
using Vox.Enums;

namespace Vox.Model
{
    public static class ModelLoader
    {
        private static readonly Dictionary<BlockType, BlockModel> models = [];
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
            models.Add(BlockType.GRASS_BLOCK  , new(JObject.Parse(File.ReadAllText(Window.assets + "BlockModels\\grass.json"))      ,   BlockType.GRASS_BLOCK   ));
            models.Add(BlockType.DIRT_BLOCK   , new(JObject.Parse(File.ReadAllText(Window.assets + "BlockModels\\dirt.json"))       ,   BlockType.DIRT_BLOCK    ));
            models.Add(BlockType.STONE_BLOCK  , new(JObject.Parse(File.ReadAllText(Window.assets + "BlockModels\\stone.json"))      ,   BlockType.STONE_BLOCK   ));
            models.Add(BlockType.TEST_BLOCK   , new(JObject.Parse(File.ReadAllText(Window.assets + "BlockModels\\testblock.json"))  ,   BlockType.TEST_BLOCK    ));
            models.Add(BlockType.TARGET_BLOCK , new(JObject.Parse(File.ReadAllText(Window.assets + "BlockModels\\target.json"))     ,   BlockType.TARGET_BLOCK  ));
            models.Add(BlockType.LAMP_BLOCK   , new(JObject.Parse(File.ReadAllText(Window.assets + "BlockModels\\lamp.json"))       ,   BlockType.LAMP_BLOCK    ));
        }
        public static Dictionary<BlockType, BlockModel> GetModels()
        {
            return models;
        }
        public static BlockModel GetModel(BlockType type)
        {
            if (type == BlockType.AIR)
                return new();

            if (models.TryGetValue(type, out BlockModel? value))
                return value;

            return new();
        }
        public static BlockModel GetModel(int type)
        {
            if ((BlockType) type == BlockType.AIR)
                return new();

            if (models.TryGetValue((BlockType) type, out BlockModel? value))
                return value;

            return new();
        }
    }
}
