
using OpenTK.Mathematics;
using Vox.Rendering;

/**
 * Block object for storing more detailed block information on an as-needed basis
 */
namespace Vox.Genesis
{
    public class BlockDetail
    {
        private readonly TerrainVertex[] north;
        private readonly TerrainVertex[] south;
        private readonly TerrainVertex[] east;
        private readonly TerrainVertex[] west;
        private readonly TerrainVertex[] up;
        private readonly TerrainVertex[] down;
        private readonly Vector3 upperCorner = Vector3.Zero;
        private readonly Vector3 lowerCorner = Vector3.Zero;
        private readonly List<Vector3> faceAdjacentBlocks = [];

        public BlockDetail() { }
        public BlockDetail(TerrainVertex[] north, TerrainVertex[] south, TerrainVertex[] up, TerrainVertex[] down, TerrainVertex[] east, TerrainVertex[] west)
        {
            this.up = up;
            this.down = down;
            this.south = south;
            this.east = east;
            this.west = west;
            this.north = north;


            //Get points shared by east and north face
            List<TerrainVertex> sharedEastNorth = [];
            for (int i = 0; i < north.Length; i++)
            {
                for (int j = 0; j < east.Length; j++)
                {
                    if (north[j].GetVector().Equals(east[i].GetVector()))
                        sharedEastNorth.Add(north[j]);
                }
            }
            //Determine upper corder of cube
            upperCorner = sharedEastNorth[0].y > sharedEastNorth[1].y ? sharedEastNorth[0].GetVector() : sharedEastNorth[1].GetVector();


            //Get points shared by west and south faces
            List<TerrainVertex> sharedWestSouth = [];
            for (int i = 0; i < south.Length; i++)
            {
                for (int j = 0; j < west.Length; j++)
                {
                    if (south[j].GetVector().Equals(west[i].GetVector()))
                        sharedWestSouth.Add(south[j]);
                }
            }
            //Determine lower corder of cube
            lowerCorner = sharedWestSouth[0].y < sharedWestSouth[1].y ? sharedWestSouth[0].GetVector() : sharedWestSouth[1].GetVector();

            // Face-adjacent cubes
            faceAdjacentBlocks.AddRange([
                lowerCorner + new Vector3(1, 0, 0), upperCorner + new Vector3(1, 0, 0),             // Positive X
                lowerCorner + new Vector3(-1, 0, 0), upperCorner + new Vector3(-1, 0, 0),           // Negative X
                lowerCorner + new Vector3(0, 1, 0), upperCorner + new Vector3(0, 1, 0),             // Positive Y
                lowerCorner + new Vector3(0, -1, 0), upperCorner + new Vector3(0, -1, 0),           // Negative Y
                lowerCorner + new Vector3(0, 0, 1), upperCorner + new Vector3(0, 0, 1),             // Positive Z
                lowerCorner + new Vector3(0, 0, -1), upperCorner + new Vector3(0, 0, -1)]           // Negative Z
            );
        }

        /*
         * Given a 3d point, checks weather it intersects the cube
         */
        public bool IsIntersectingBlock(Vector3 v)
        {
            return v.X >= lowerCorner.X && v.X <= upperCorner.X &&
                   v.Y >= lowerCorner.Y && v.Y <= upperCorner.Y &&
                   v.Z >= lowerCorner.Z && v.Z <= upperCorner.Z;
        }

        public Vector3 GetUpperCorner() { return upperCorner; }

        public Vector3 GetLowerCorner() { return lowerCorner; }
        public TerrainVertex[] GetVertexData()
        {
            return north.Concat(south).Concat(west).Concat(east).Concat(up).Concat(down).ToArray();
        }

        /*
         * Returns true if this block is adjacent to at least one other block
         */
        public bool IsSurrounded()
        {
            List<TerrainVertex> vertList = [.. RegionManager.GetAndLoadGlobalChunkFromCoords((int)lowerCorner.X, (int)lowerCorner.Y, (int)lowerCorner.Z).GetTerrainRenderTask().GetVertexData()];
            List<Vector3> surroundingCubes =
            [
                // Edge-adjacent (diagonal) cubes
                lowerCorner + new Vector3(1, 1, 0), upperCorner + new Vector3(1, 1, 0),             // +X +Y
            lowerCorner + new Vector3(-1, 1, 0), upperCorner + new Vector3(-1, 1, 0),           // -X +Y
            lowerCorner + new Vector3(1, -1, 0), upperCorner + new Vector3(1, -1, 0),           // +X -Y
            lowerCorner + new Vector3(-1, -1, 0), upperCorner + new Vector3(-1, -1, 0)          // -X -Y
            ];

            //Face adjacent cubes
            surroundingCubes.AddRange(faceAdjacentBlocks);

            for (int i = 0; i < vertList.Count; i += 24)
            {
                for (int j = 0; j < surroundingCubes.Count; j++)
                    if (vertList[i].GetVector().Equals(surroundingCubes[j]))
                        return true;
            }
            return false;
        }

        public bool IsRendered()
        {
            List<TerrainVertex> vertList = [.. RegionManager.GetAndLoadGlobalChunkFromCoords((int)lowerCorner.X, (int)lowerCorner.Y, (int)lowerCorner.Z).GetTerrainRenderTask().GetVertexData()];
            for (int i = 0; i < vertList.Count; i += 24)
            {
                if (vertList[i].GetVector().Equals(lowerCorner))
                    return true;
            }
            return false;
        }

        public List<Vector3> GetFaceAdjacentBlocks()
        {
            return faceAdjacentBlocks;
        }
    }
}