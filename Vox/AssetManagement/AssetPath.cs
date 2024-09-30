

namespace Vox.AssetManagement
{
    public class AssetPath(string inputPath) : IComparable<AssetPath>
    {

        private readonly string path = inputPath;
        private static readonly string assets = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\.voxelGame\\Assets\\";

        private AssetPath(string domain, string inputPath) : this(new AssetPathReader(domain, inputPath).ReadParts())
        {
        }

        private AssetPath(params string[] parts) : this(Path.Combine(assets, Path.Combine(parts)))
        {
        }

        public AssetPath Resolve(string[] parts)
        {
            return new AssetPath(Merge(path, Path.Combine("", string.Join(Path.DirectorySeparatorChar.ToString(), parts))));
        }
        public static bool IsChildOf(AssetPath child, AssetPath parent)
        {
            // Check if the current path starts with the parent's path
            return child.path.StartsWith(parent.path, StringComparison.OrdinalIgnoreCase);
        }

        public AssetPath WithExtension(string extension)
        {
            string name = Path.GetFileName(path);

            if (!name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                string directory = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(directory))
                {
                    return new AssetPath(name + extension); // No directory, just return name + extension
                }
                return new AssetPath(Path.Combine(directory, name + extension)); // Combine the directory with the new file name
            }
            return this;
        }

        public string GetName()
        {
            string fileName = Path.GetFileName(path);
            int index = fileName.LastIndexOf('.');
            return index > -1 ? fileName.Substring(0, index) : fileName;
        }

        public bool IsChildOf(AssetPath parent)
        {
            return path.StartsWith(parent.path);
        }

        public string Merge(string parent)
        {
            return Merge(parent, path);
        }

        public int CompareTo(AssetPath? pathIn)
        {
            return path.CompareTo(pathIn.path);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override bool Equals(object? obj)
        {
            // Check for null
            if (obj == null) return false;

            // Check if the object is of the same type
            if (GetType() != obj.GetType()) return false;

            // Compare hash codes
            return obj.GetHashCode() == GetHashCode();
        }

        public override string ToString()
        {
            return path;
        }

        public static AssetPath Domain(string domain)
        {
            return new AssetPath(domain, "");
        }

        public static AssetPath Of(string path)
        {
            return new AssetPath(path);
        }

        public static AssetPath Of(object input, string parent)
        {
            AssetPathReader reader = new(input.ToString());

            string domain = reader.GetDomain();
            string remaining = reader.Remaining();

            string route = Path.Combine("assets", domain, parent);
            string path = Merge(route, Path.Combine(remaining));

            return new AssetPath(path);
        }

        private static string Merge(string parent, string child)
        {
            string[] parentParts = parent.Split(Path.DirectorySeparatorChar);
            string childFirstPart = child.Split(Path.DirectorySeparatorChar)[0];

            for (int i = 0; i < parentParts.Length; i++)
            {
                string p = parentParts[i];
                if (p.Equals(childFirstPart, StringComparison.OrdinalIgnoreCase))
                {
                    if (i == 0)
                        return Path.Combine(Path.GetDirectoryName(parent), child);
                    else
                        return Path.Combine(string.Join(Path.DirectorySeparatorChar.ToString(), parentParts, 0, i), child);
                }
            }

            return Path.Combine(parent, child); // Default resolution
        }
    }
}
