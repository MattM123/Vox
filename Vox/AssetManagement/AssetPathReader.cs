namespace Vox.AssetManagement
{
    public class AssetPathReader
    {

        private static string domainName = "";
        private static string input = "";
        private static int pos = -1;

        public AssetPathReader(string inPath)
        {
            input = inPath;
            domainName = "vox";
        }

        public AssetPathReader(string domain, string inPath)
        {
            input = inPath;
            domainName = domain;
        }

        public static void SetPath(string inputPath)
        {
            input = inputPath;
        }
        public string Remaining()
        {
            //???
            return input.Substring(pos + 1, input.Length);
        }

        private static bool HasNext()
        {
            return pos + 1 < input.Length;
        }

        public string[] ReadParts()
        {
            string[] parts = new string[9];
            parts[0] = GetDomain();
            int index = 1;

            while (HasNext() && index < parts.Length)
            {
                parts[index++] = NextPart();
            }

            // Use LINQ to return a new array with only the filled elements
            return parts.Take(index).ToArray();
        }

        public string GetDomain()
        {
            pos = -1;
            int poss = pos;
            string domain = domainName;
            while (++pos < input.Length)
            {
                if (input[pos] == ':')
                {
                    string s = input.Substring(0, pos);
                    domain = s.Length == 0 ? domain : s;
                    pos = poss;
                    break;
                }
            }
            return domain;
        }

        private static string NextPart()
        {
            int start = pos + 1;
            while (++pos < input.Length && input[pos] != '/') ;
            return input.Substring(start, pos);
        }
    }
}
