
using System.Text.RegularExpressions;

namespace Vox.AssetManagement
{
    public class AssetPack
    {

        private static byte[] EMPTY = [];

        private Dictionary<AssetPath, byte[]> assets;
        private int bytes;

        private AssetPack(AssetBuilder builder)
        {
            assets = builder.GetAssets();
            bytes = builder.GetBytes();
        }

        public void Clear()
        {
            assets.Clear();
        }

        public List<AssetPath> GetEntries()
        {
            return [.. assets.Keys];
        }

        public List<AssetPath> MatchChildren(AssetPath path)
        {
            return assets.Keys.ToList().Where(p=>p.IsChildOf(path)).ToList();
        }

        public void TransferChildren(AssetPath path, string root)
        {
            MatchChildren(path).ForEach(match=> {
                try
                {
                    Transfer(match, root);
                }
                catch (IOException e)
                {
                    Logger.Error(e);
                }
            });
        }

        public void Transfer(AssetPath path, string rootDir)
        {
            if (assets.TryGetValue(path, out byte[] data) && data != EMPTY)
            {
                string output = path.Merge(rootDir);

                // Create the directory if it doesn't exist
                string parentDirectory = Path.GetDirectoryName(output);
                if (!string.IsNullOrEmpty(parentDirectory))
                {
                    Directory.CreateDirectory(parentDirectory);
                }

                // Write the data to the output file
                using (FileStream outputStream = new FileStream(output, FileMode.Create, FileAccess.Write))
                {
                    outputStream.Write(data, 0, data.Length);
                    outputStream.Flush();
                }
            }
        }

        public byte[] GetBytes(AssetPath path)
        {
            // Try to get the value from the dictionary; return EMPTY if not found
            return assets.TryGetValue(path, out byte[] data) ? data : EMPTY;
        }

        public int GetCount()
        {
            return assets.Count;
        }

        public int GetSize()
        {
            return bytes;
        }

        public static AssetPack Of(Regex filter, string[] containers)
        {
            return Of(filter, containers);
        }

        public static AssetPack Of(Regex filter, List<string> containers)
        {
            AssetBuilder builder = new(filter);
            foreach (string file in containers)
            {
                builder.Add(file);
            }
            return new AssetPack(builder);
        }

        public static AssetPack Of(Regex filter)
        {
            AssetBuilder builder = new(filter);
            return new AssetPack(builder);
        }
    }
}
