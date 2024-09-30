
using System.Text;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using Newtonsoft.Json;

namespace Vox.AssetManagement
{
    public class AssetManager
    {

        private static AssetManager INSTANCE = new();
        private static JObject EMPTY_OBJ = new();
        private static readonly string RegexPattern = @"\bassets\b(.*?)\b(blockstates|BlockModels|Textures)\b";
        private static readonly Regex Pattern = new(RegexPattern, RegexOptions.Compiled);

        private AssetPack assets = AssetPack.Of(Pattern);
        private ICollection<string> domains = new List<string>();
        private ConcurrentDictionary<AssetPath, JObject> jsonCache = new ConcurrentDictionary<AssetPath, JObject>();

        public static AssetManager GetInstance()
        {
            return INSTANCE;
        }

        public void Clear()
        {
            assets.Clear();
            jsonCache.Clear();
            assets = null;
            jsonCache = null;
        }

        public void FindAssets()
        {
            string defaultPack = "";
            List<string> sources = [];


            if (!defaultPack.ToString().Equals(""))
            {
                sources.Add(defaultPack);
            }

            assets = AssetPack.Of(Pattern, sources);
            jsonCache = [];

            Logger.Info("Found " + assets.GetCount() + " assets");
            for (int i = 0; i < assets.GetCount(); i++)
            {
                Logger.Info("Loaded asset: " + assets.GetEntries()[i].GetName());
            } 
        }

        public JObject GetJson(AssetPath path)
        {
            // Make sure the path has ".json" extension
            path = path.WithExtension(".json");

            return JObject.Parse(File.ReadAllText(path.ToString()));

        }

        public void ExtractAssets(Func<string, AssetPath> pathFunction, string root)
        {
            foreach (string domain in domains)
            {
                AssetPath path = pathFunction(domain);
                ExtractAssets(path, root);
            }
        }

        public void ExtractAssets(AssetPath match, string root)
        {
            assets.TransferChildren(match, root);
        }
    }
}
