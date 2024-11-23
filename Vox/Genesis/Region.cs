
using System.Drawing;
using MessagePack;

namespace Vox.Genesis
{
    [MessagePackObject]
    public class Region
    {
        [Key(0)]
        public int x;

        [Key(1)]
        public int z;

        [Key(2)]
        public bool didChange = false;

        [IgnoreMember]
        public readonly Rectangle regionBounds;

        [Key(3)]
        public Dictionary<string, Chunk> chunks = [];

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
            foreach (KeyValuePair<string, Chunk> c in chunks)
            {
                if (c.Value.DidChange())
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

        public override string ToString()
        {

            if (chunks.Count() > 0)
                return "(" + chunks.Count() + " Chunks) Region[" + regionBounds.X
                        + ", " + regionBounds.Y + "]";
           else
                return "(Empty) Region[" + regionBounds.X + ", " + regionBounds.Y + "]";
        }

        //Gets the region index given chunk coordinates
        public static string GetRegionIndex(int chunkX, int chunkZ)
        {
            Rectangle bounds = RegionManager.GetGlobalRegionFromChunkCoords(chunkX, chunkZ).GetBounds();
            return $"{bounds.X}|{bounds.Y}";
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}

