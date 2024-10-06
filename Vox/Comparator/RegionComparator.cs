

using Vox.Genesis;

namespace Vox.Comparator
{

    /**
     * Sorts blocks by coordinate for use in binary search algorithm
     */
    public class RegionComparator : Comparer<Region>
    { 

        public override int Compare(Region? a, Region? b)
        {

            if (a.GetBounds().Y == b.GetBounds().Y && a.GetBounds().X == b.GetBounds().X)
            {
                return 0;
            }
            else
            {
                //X takes precedence
                if (a.GetBounds().X != b.GetBounds().X)
                {
                    if (a.GetBounds().X < b.GetBounds().X)
                        return -1;

                    if (a.GetBounds().X > b.GetBounds().X)
                        return 1;
                }
                else
                { //If X is equal, compare Y
                    if (a.GetBounds().Y < b.GetBounds().Y)
                        return -1;

                    if (a.GetBounds().Y > b.GetBounds().Y)
                        return 1;
                }
            }
            return 0;
        }
    }
}
