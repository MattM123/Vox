
using System.IO.Compression;
using System.Text.RegularExpressions;


namespace Vox.AssetManagement
{
    public class AssetBuilder
    {

        private Dictionary<AssetPath, byte[]> assets = [];
        private int bytes = 0;
        private Regex filter;

        public AssetBuilder(Regex pattern)
        {
            filter = pattern;
        }

        public void Add(string path)
        {
            if (!File.Exists(path))
            {
                Logger.Info(string.Format("AssetBuilder.Add - Skipping AssetContainer: {}", path));
                return;
            }

            try
            {
                if (Directory.Exists(path))
                {
                    AddFile(path);
                }
                else
                {
                    AddZip(path);
                }

                Logger.Info(string.Format("AssetBuilder.Add - Added AssetContainer: {}", path));
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }

        private void AddFile(string path)
        {
            if (Directory.Exists(path))
            {
                try
                {
                    // Get all files in the directory and its subdirectories
                    var files = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories);

                    foreach (var file in files)
                    {
                        AddFile(file);
                    }
                }
                catch (IOException e)
                {
                    Logger.Error(e);
                }
            }
            else
            {
                AddFileEntry(path);
            }
        }
        private void AddZip(string filePath)
        {
            try
            {
                // Open the ZIP file
                using (ZipArchive zip = ZipFile.OpenRead(filePath))
                {
                    // Iterate through each entry in the ZIP file
                    foreach (ZipArchiveEntry entry in zip.Entries)
                    {
                        // Process each entry
                        AddZipEntry(entry);
                    }
                }
            }
            catch (IOException e)
            {
                Logger.Error(e);
            }
        }

        private void AddFileEntry(string filePath)
        {
            if (filter.IsMatch(filePath))
            {
                AssetPath path = new(filePath);

                if (!assets.ContainsKey(path))
                {
                    try
                    {
                        // Read all bytes from the file
                        byte[] data = File.ReadAllBytes(filePath);
                        AddBytes(path, data);
                    }
                    catch (IOException e)
                    {
                        Logger.Error(e);
                    }
                }
            }
        }

        public int GetBytes() { return bytes; }

        public Dictionary<AssetPath, byte[]> GetAssets() { return assets; }

        private void AddZipEntry(ZipArchiveEntry entry)
        {
            // Check if the entry is not a directory and matches the regex filter
            if (!entry.FullName.EndsWith("/") && filter.IsMatch(entry.FullName))
            {
                AssetPath path = new(entry.FullName);

                // Check if the asset path already exists
                if (!assets.ContainsKey(path))
                {
                    try
                    {
                        // Open the input stream for the zip entry
                        using (Stream inputStream = entry.Open())
                        {
                            byte[] data = ReadAll(inputStream); // Assuming ReadAll is a defined method
                            AddBytes(path, data); // Assuming AddBytes is defined elsewhere
                        }
                    }
                    catch (IOException e)
                    {
                        Logger.Error(e);
                    }
                }
            }
        }

        private void AddBytes(AssetPath path, byte[] data)
        {
            assets.Add(path, data);
            Interlocked.Add(ref bytes, data.Length); 
        }

        private byte[] ReadAll(Stream inputStream)
        {
            using (MemoryStream buffer = new MemoryStream())
            {
                byte[] bytes = new byte[2048];
                int read;

                while ((read = inputStream.Read(bytes, 0, bytes.Length)) != 0)
                {
                    buffer.Write(bytes, 0, read);
                }

                return buffer.ToArray(); // Return the byte array
            }
        }
    }
}
