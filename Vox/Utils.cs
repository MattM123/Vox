

namespace Vox
{
    public class Utils()
    {
        public static int FloatCompare(float a, float b)
        {
            // If both numbers are equal, return 0.
            if (a == b)
            {
                return 0;
            }

            // If 'a' is greater than 'b', return 1.
            if (a > b)
            {
                return 1;
            }

            // Otherwise, 'a' is less than 'b', return -1.
            return -1;
        }
        public static string FormatSize(long v)
        {
            if (v < 1024) return v + " B";
            int z = (int)(63 - long.LeadingZeroCount(v)) / 10;
            return string.Format("{0:0.0} {1}B", (double)v / (1L << (z * 10)), " KMGTPE"[z]);
        }
        public static void CopyDirectory(string sourceDir, string destinationDir, bool copySubDirs = true)
        {
            // Get the source directory info
            DirectoryInfo dir = new DirectoryInfo(sourceDir);

            // Check if the source directory exists
            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException($"Source directory does not exist: {sourceDir}");
            }

            // If the destination directory doesn't exist, create it
            Directory.CreateDirectory(destinationDir);

            // Copy all files in the directory
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string tempPath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(tempPath, overwrite: true);
            }

            // If copying subdirectories, recursively copy them
            if (copySubDirs)
            {
                DirectoryInfo[] subDirs = dir.GetDirectories();
                foreach (DirectoryInfo subDir in subDirs)
                {
                    string tempPath = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, tempPath, copySubDirs);
                }
            }
        }

    }
}
