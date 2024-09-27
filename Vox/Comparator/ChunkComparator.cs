

using OpenTK.Mathematics;

namespace Vox.Comparator
{
    /**
     * Since each chunk is identified by the three-dimensional point its located at
     * this object is used to sort the chunks for use with
     * binary search algorithms.
     */
    public class ChunkComparator : Comparer<Vector3> 
    {
     
        public override int Compare(Vector3 a, Vector3 b)
        {
            if (Utils.FloatCompare(a.X, b.X) == -1)
            {
                return -1;
            }
            else if (Utils.FloatCompare(a.X, b.X) == 1)
            {
                return 1;
            }

            //If x coordinates are equal
            else
            {
                float epsilon = float.Epsilon;
                if (Math.Abs(a.Z - b.Z) < epsilon)
                    return 0;
                else
                    return Utils.FloatCompare(a.Z, b.Z);
            }
        }
    }

}
