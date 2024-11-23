
using System.Collections;
using System.Text;
using MessagePack;
using OpenTK.Mathematics;
using Vox.Comparator;

namespace Vox.Genesis
{
    [MessagePackObject]
    public class ChunkManager
    {
        [Key(3)]
        public Hashtable chunks = [];
        /**
         * Since the order of chunks within a region matters the ChunkManager object
         * provides methods of chunk chunks.Insertion and retrieval within a Region object
         */
        public ChunkManager()
        {
        }

        public Hashtable GetChunks()
        {
            return chunks;
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
      //  public Chunk GetChunkWithLocation(Vector3 loc)
      //  {
      //      return BinarySearchChunkWithLocation(0, chunks.Count - 1, loc);
      //  }

        /**
         * Searches for an index to insert a new Chunk at in O(log n) time complexity.
         * Ensures the list is sorted by the Chunks location as new Chunks are inserted into it.
         *
         * @param l The farthest left index of the list
         * @param r The farthest right index of the list
         * @param c The chunk location to search for.
         * @return Returns the chunk object that was just inserted into the list.
         */
      //  public Chunk BinaryInsertChunkWithLocation(int l, int r, Vector3 c)
      //  {
      //
      //      ChunkComparator pointCompare = new();
      //      Chunk q = new Chunk().Initialize(c.X, c.Z);
      //
      //      //If chunk already exists in region
      //      if (chunks.Contains(q))
      //      {
      //          return null;
      //      }
      //
      //      if (chunks.Count == 0)
      //      {
      //          chunks.Add(q);
      //          return q;
      //      }
      //
      //      if (chunks.Count == 1)
      //      {
      //          //chunks.Inserts element as first in list
      //          if (pointCompare.Compare(c, chunks[0].GetLocation()) < 0)
      //          {
      //              chunks.Insert(0, q);
      //              return q;
      //          }
      //          //Appends to end of list
      //          if (pointCompare.Compare(c, chunks[0].GetLocation()) > 0)
      //          {
      //              chunks.Add(q);
      //              return q;
      //          }
      //      }
      //
      //      if (r >= l && chunks.Count > 1)
      //      {
      //          int mid = l + (r - l) / 2;
      //
      //          // If an index is found where left and right are very close
      //          if (Math.Abs(r - l) == 1)
      //          {
      //              chunks.Insert(r, q);
      //              return q;
      //          }
      //
      //          // Check if the element should be chunks.Inserted at the front
      //          if (pointCompare.Compare(c, chunks[0].GetLocation()) < 0)
      //          {
      //              chunks.Insert(0, q);
      //              return q;
      //          }
      //
      //          // Check if the element should be chunks.Inserted at the end
      //          if (pointCompare.Compare(c, chunks[chunks.Count - 1].GetLocation()) > 0)
      //          {
      //              chunks.Add(q);
      //              return q;
      //          }
      //
      //          // Check if it's near the middle, but ensure bounds are respected
      //          if (mid > 0 && pointCompare.Compare(c, chunks[mid - 1].GetLocation()) > 0
      //                  && pointCompare.Compare(c, chunks[mid].GetLocation()) < 0)
      //          {
      //              chunks.Insert(mid, q);
      //              return q;
      //          }
      //
      //          if (mid < chunks.Count - 1 && pointCompare.Compare(c, chunks[mid + 1].GetLocation()) < 0
      //                  && pointCompare.Compare(c, chunks[mid].GetLocation()) > 0)
      //          {
      //              chunks.Insert(mid + 1, q);
      //              return q;
      //          }
      //
      //          // If element is smaller than mid, search the left half
      //          if (pointCompare.Compare(c, chunks[mid].GetLocation()) < 0)
      //          {
      //              return BinaryInsertChunkWithLocation(l, mid - 1, c);
      //          }
      //
      //          // Otherwise, search the right half
      //          return BinaryInsertChunkWithLocation(mid + 1, r, c);
      //      }
      //      else
      //      {
      //          return null;
      //      }
      //  }

        /**
         * Searches for a chunk in O(log n) time complexity and returns it.
         *
         * @param l The farthest left index of the list
         * @param r The farthest right index of the list
         * @param c The chunk location to search for.
         * @return Returns the chunk if found. Else null.
         */
      //  public Chunk BinarySearchChunkWithLocation(int l, int r, Vector3 c)
      //  {
      //      if (this == null || this.chunks.Count == 0) // Ensure the collection is initialized and not empty
      //      {
      //          return null;
      //      }
      //
      //      ChunkComparator pointCompare = new();
      //
      //      while (r >= l)
      //      {
      //          int mid = l + (r - l) / 2;
      //
      //          // Check if mid is a valid index
      //          if (mid < 0 || mid >= this.chunks.Count)
      //          {
      //              return null;
      //          }
      //
      //          Chunk midChunk = chunks[mid]; // Cache the chunk for reuse
      //
      //          // Ensure midChunk is not null before accessing its properties
      //          if (midChunk == null)
      //          {
      //              return null; // or throw an exception depending on your error handling strategy
      //          }
      //
      //          Vector3 midLocation = midChunk.GetLocation(); // Cache the location
      //
      //          // Ensure midLocation is not null
      //          if (midLocation == null)
      //          {
      //              return null; // Handle the case where the location is null
      //          }
      //
      //          // If the element is present at the middle
      //          if (pointCompare.Compare(c, midLocation) == 0)
      //          {
      //              return midChunk;
      //          }
      //
      //          // If element is smaller than mid, search the left subarray
      //          if (pointCompare.Compare(c, midLocation) < 0)
      //          {
      //              return BinarySearchChunkWithLocation(l, mid - 1, c);
      //          }
      //
      //          // Else the element can only be present in right subarray
      //          if (pointCompare.Compare(c, midLocation) > 0)
      //          {
      //              return BinarySearchChunkWithLocation(mid + 1, r, c);
      //          }
      //      }
      //
      //      return null; // Not found
      //  }

        /**
         * @return Returns a string containing all the chunks that are inside the
         * region associated with this ChunkManager.
         */
        public string GetChunkString()
        {
            StringBuilder s = new();
            foreach (Chunk c in chunks)
                s.Append(c.ToString()).Append(", ");

            return s.ToString();
        }
    }
}
