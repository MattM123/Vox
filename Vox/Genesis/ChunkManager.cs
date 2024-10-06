
using System.Collections;
using System.Text;
using OpenTK.Mathematics;
using Vox.Comparator;

namespace Vox.Genesis
{
    public class ChunkManager : List<Chunk>
    {

        /**
         * Since the order of chunks within a region matters the ChunkManager object
         * provides methods of chunk insertion and retrieval within a Region object
         */
        public ChunkManager()
        {
        }

        /**
         * Gets a chunk from the ChunkManager that is located in a specific position
         * in O(log n) time complexity. This location is the same location that
         * was used when the chunk was initialized. If no chunk is found with the location
         * null is returned
         *
         * @param loc The location of the chunk
         * @return Null if the chunk doesn't exist, else will return the chunk
         */
        public Chunk GetChunkWithLocation(Vector3 loc)
        {
            return BinarySearchChunkWithLocation(0, Count - 1, loc);
        }

        /**
         * Searches for an index to insert a new Chunk at in O(log n) time complexity.
         * Ensures the list is sorted by the Chunks location as new Chunks are inserted into it.
         *
         * @param l The farthest left index of the list
         * @param r The farthest right index of the list
         * @param c The chunk location to search for.
         * @return Returns the chunk object that was just inserted into the list.
         */
        public Chunk BinaryInsertChunkWithLocation(int l, int r, Vector3 c)
        {
           
            ChunkComparator pointCompare = new();
            Chunk q = new Chunk().Initialize(c.X, c.Z);

            //If chunk already exists in region
            if (Contains(q))
            {
                return q;
            }

            if (Count == 0)
            {
                Add(q);
            }

            if (Count == 1)
            {
                //Inserts element as first in list
                if (pointCompare.Compare(c, this[0].GetLocation()) < 0)
                {
                    Insert(0, q);
                    return q;
                }
                //Appends to end of list
                if (pointCompare.Compare(c, this[0].GetLocation()) > 0)
                {
                    Add(q);
                    return q;
                }
            }

            if (r >= l && Count > 1)
            {
                int mid = l + (r - l) / 2;

                // If an index is found where left and right are very close
                if (Math.Abs(r - l) == 1)
                {
                    Insert(r, q);
                    return q;
                }

                // Check if the element should be inserted at the front
                if (pointCompare.Compare(c, this[0].GetLocation()) < 0)
                {
                    Insert(0, q);
                    return q;
                }

                // Check if the element should be inserted at the end
                if (pointCompare.Compare(c, this[Count - 1].GetLocation()) > 0)
                {
                    Add(q);
                    return q;
                }

                // Check if it's near the middle, but ensure bounds are respected
                if (mid > 0 && pointCompare.Compare(c, this[mid - 1].GetLocation()) > 0
                        && pointCompare.Compare(c, this[mid].GetLocation()) < 0)
                {
                    Insert(mid, q);
                    return q;
                }

                if (mid < Count - 1 && pointCompare.Compare(c, this[mid + 1].GetLocation()) < 0
                        && pointCompare.Compare(c, this[mid].GetLocation()) > 0)
                {
                    Insert(mid + 1, q);
                    return q;
                }

                // If element is smaller than mid, search the left half
                if (pointCompare.Compare(c, this[mid].GetLocation()) < 0)
                {
                    return BinaryInsertChunkWithLocation(l, mid - 1, c);
                }

                // Otherwise, search the right half
                return BinaryInsertChunkWithLocation(mid + 1, r, c);
            }
            else
            {
                return null;
            }
        }

        /**
         * Searches for a chunk in O(log n) time complexity and returns it.
         *
         * @param l The farthest left index of the list
         * @param r The farthest right index of the list
         * @param c The chunk location to search for.
         * @return Returns the chunk if found. Else null.
         */
        public Chunk BinarySearchChunkWithLocation(int l, int r, Vector3 c)
        {
            ChunkComparator pointCompare = new();
            if (r >= l)
            {
                int mid = l + (r - l) / 2;

                // If the element is present at the middle
                if (pointCompare.Compare(c, this[mid].GetLocation()) == 0)
                {
                    return this[mid];
                }


                // If element is smaller than mid, then
                // it can only be present in left subarray
                if (pointCompare.Compare(c, this[mid].GetLocation()) < 0)
                {
                    return BinarySearchChunkWithLocation(l, mid - 1, c);
                }

                // Else the element can only be present
                // in right subarray
                if (pointCompare.Compare(c, this[mid].GetLocation()) > 0)
                {
                    return BinarySearchChunkWithLocation(mid + 1, r, c);
                }
            }
            return null;

        }

        /**
         * @return Returns a string containing all the chunks that are inside the
         * region associated with this ChunkManager.
         */
        public string GetChunks()
        {
            StringBuilder s = new();
            foreach (Chunk c in this)
                s.Append(c.ToString()).Append(", ");

            return s.ToString();
        }
    }
}
