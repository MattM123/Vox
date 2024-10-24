using System.Drawing;
using MessagePack;
using Vox.Comparator;

namespace Vox.Genesis
{
    [MessagePackObject]
    public class Region : ChunkManager
    {
        [Key(0)]
        public int x;

        [Key(1)]
        public int z;

        [Key(2)]
        public bool didChange = false;

        [IgnoreMember]
        public readonly Rectangle regionBounds;



        [SerializationConstructor]
        public Region(int x, int z) {
            this.x = x;
            this.z = z;
            regionBounds = new(x, z, RegionManager.REGION_BOUNDS, RegionManager.REGION_BOUNDS);

        }
        /**
         * Returns true if at least one chunk in a region has changed. If true,
         * the region as a whole is also marked as having changed therefore
         * should be re-written to file.
         *
         * @return True if at least one chunk has changed, false if not
         */
        public bool DidChange()
        {
            foreach (Chunk c in GetChunks())
            {
                if (c.DidChange())
                {
                    didChange = true;
                    break;
                }
            }
            return didChange;
        }

        public Rectangle GetBounds()
        {
            return regionBounds;
        }
        public override bool Equals(object? o)
        {
            if (o == null)
                return false;

            if (o.GetType() == typeof(Region))
            {
                return regionBounds.X.Equals(((Region)o).regionBounds.X)
                        && regionBounds.Y.Equals(((Region)o).regionBounds.Y);
            }
            return false;
        }

        //Faster contains function to deal with chunks with large regions
        public bool Contains(int low, int high, Chunk c)
        {
            ChunkComparator compare = new();

            while (low <= high)
            {
                // Find the middle element
                int mid = (low + high) / 2;

                // Check if the middle element is the target
                if (GetChunks()[mid].Equals(c))
                {
                    return true;
                }
                // If target is smaller, ignore the right half
                else if (compare.Compare(GetChunks()[mid].GetLocation(), c.GetLocation()) == 1)
                {
                    Contains(low, mid - 1, c);
                  //  high = mid - 1;
                }
                // If target is larger, ignore the left half
                else
                {
                    Contains(mid + 1, high, c);
                   // low = mid + 1;
                }
            }

            // If we reach here, the target is not in the array
            return false;
        }
        public override string ToString()
        {

            if (GetChunks().Count > 0)
                return "(" + GetChunks().Count + " Chunks) Region[" + regionBounds.X
                        + ", " + regionBounds.Y + "]";
           else
                return "(Empty) Region[" + regionBounds.X + ", " + regionBounds.Y + "]";
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}

