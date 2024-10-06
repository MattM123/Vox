using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vox.Comparator;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Vox.Genesis
{
    public class Region(int x, int z) : ChunkManager
    {

        private readonly Rectangle regionBounds = new(x, z, RegionManager.REGION_BOUNDS, RegionManager.REGION_BOUNDS);
        private bool didChange = false;

        /**
         * Returns true if at least one chunk in a region has changed. If true,
         * the region as a whole is also marked as having changed therefore
         * should be re-written to file.
         *
         * @return True if at least one chunk has changed, false if not
         */
        public bool DidChange()
        {
            foreach (Chunk c in this)
            {
                if (c.ShouldRerender())
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

        /**
         * Writes to file only if the didChange flag of the region is true. This
         * flag would only be true if at least one chunks didChange flag was also true.
         */
        /*
        @Serial
        private void writeObject(ObjectOutputStream o)
        {
            if (didChange())
            {
                writeRegion(o, this);
                didChange = false;
            }
        }

        @Serial
        private void readObject(ObjectInputStream o)
        {
            this.replaceAll(c->readRegion(o).Get(indexOf(c)));
        }

        private void writeRegion(OutputStream stream, Region r)
        {
            System.out.println("Writing " + r);

            Main.executor.execute(()-> {
                try
                {
                    FSTObjectOutput out = Main.GetInstance().GetObjectOutput(stream);
                    out.writeObject(r, Region.class);
                    r.clear();
                } catch (Exception e) {
                    e.printStackTrace();
    System.exit(-1);
                }
            });
        }

        private Region readRegion(InputStream stream)
    {

        AtomicReference<Region> r = new AtomicReference<>();
        FSTObjectInput in = Main.GetInstance().GetObjectInput(stream);

        try
        {
            r.set((Region) in.readObject(Region.class));
    System.out.println("Reading Region: " + r);
    stream.close();
            } catch (Exception e) {
                logger.warning(e.GetMessage());
            }

            try
    { in.close();
    }
    catch (Exception e)
    {
        e.printStackTrace();
    }

    return r.Get();
        }
        */

        public override bool Equals(object? o)
        {
            if (o.GetType() == typeof(Region))
            {
                return regionBounds.X == ((Region)o).regionBounds.X
                        && regionBounds.Y == ((Region)o).regionBounds.Y;
            }
            return false;
        }

        //Faster contains function to deal with chunks with large regions
        public bool Contains(Chunk c)
        {
            int low = 0;
            int high = Count - 1;
            ChunkComparator compare = new();

            while (low <= high)
            {
                // Find the middle element
                int mid = (low + high) / 2;

                // Check if the middle element is the target
                if (this[mid].Equals(c))
                {
                    return true;
                }
                // If target is smaller, ignore the right half
                else if (compare.Compare(this[mid].GetLocation(), c.GetLocation()) == 1)
                {
                    high = mid - 1;
                }
                // If target is larger, ignore the left half
                else
                {
                    low = mid + 1;
                }
            }

            // If we reach here, the target is not in the array
            return false;
        }
        public override string ToString()
        {

            if (Count > 0)
                return "(" + Count + " Chunks) Region: (" + regionBounds.X
                        + ", " + regionBounds.Y + ")";
           else
                return "(Empty) Region: (" + regionBounds.X + ", " + regionBounds.Y + ")";
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}

