
namespace Vox.Comparator
{

    /**
     * Sorts blocks by coordinate for use in binary search algorithm
     */
    public class BlockComparator : Comparer<Block> {

        public override int Compare(Block? a, Block? b)
        {
            if (Utils.FloatCompare(a.getLocation().X, b.getLocation().X) == -1)
            {
                return -1;
            }
            else if (Utils.FloatCompare(a.getLocation().X, b.getLocation().X) == 1)
            {
                return 1;
            }

            //If x coordinates are equal
            else
            {
                float epsilon = float.Epsilon;
                if (Math.Abs(a.getLocation().Z - b.getLocation().Z) < epsilon)
                {
                    if (Math.Abs(a.getLocation().Y - b.getLocation().Y) < epsilon)
                        return 0;
                    else
                        return Utils.FloatCompare(a.getLocation().Y, b.getLocation().Y);
                }
                else
                {
                    return Utils.FloatCompare(a.getLocation().Z, b.getLocation().Z);
                }
            }
        }
    }
}
