
namespace Vox.Comparator
{

    /**
     * Sorts blocks by coordinate for use in binary search algorithm
     */
    public class BlockComparator : Comparer<Block> {

        public override int Compare(Block? a, Block? b)
        {
            if (Utils.FloatCompare(a.GetLocation().X, b.GetLocation().X) == -1)
            {
                return -1;
            }
            else if (Utils.FloatCompare(a.GetLocation().X, b.GetLocation().X) == 1)
            {
                return 1;
            }

            //If x coordinates are equal
            else
            {
                float epsilon = float.Epsilon;
                if (Math.Abs(a.GetLocation().Z - b.GetLocation().Z) < epsilon)
                {
                    if (Math.Abs(a.GetLocation().Y - b.GetLocation().Y) < epsilon)
                        return 0;
                    else
                        return Utils.FloatCompare(a.GetLocation().Y, b.GetLocation().Y);
                }
                else
                {
                    return Utils.FloatCompare(a.GetLocation().Z, b.GetLocation().Z);
                }
            }
        }
    }
}
