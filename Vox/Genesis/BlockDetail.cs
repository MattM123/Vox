
using OpenTK.Mathematics;
using Vox.Model;

/**
 * Block object for storing more detailed block information on an as-needed basis
 */
namespace Vox.Genesis
{
    public class BlockDetail
    {
        private readonly TerrainVertex[] _north;
        private readonly TerrainVertex[] _south;
        private readonly TerrainVertex[] _east;
        private readonly TerrainVertex[] _west;
        private readonly TerrainVertex[] _up;
        private readonly TerrainVertex[] _down;
        private readonly Vector3 _upperCorner = Vector3.Zero;
        private readonly Vector3 _lowerCorner = Vector3.Zero;
        private readonly List<Vector3> _faceAdjacentBlocks = [];

        public BlockDetail() { }
        public BlockDetail(TerrainVertex[] north, TerrainVertex[] south, TerrainVertex[] up, TerrainVertex[] down, TerrainVertex[] east, TerrainVertex[] west)
        {
            this._up = up;
            this._down = down;
            this._south = south;
            this._east = east;
            this._west = west;
            this._north = north;


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
            _upperCorner = sharedEastNorth[0].y > sharedEastNorth[1].y ? sharedEastNorth[0].GetVector() : sharedEastNorth[1].GetVector();


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
            _lowerCorner = sharedWestSouth[0].y < sharedWestSouth[1].y ? sharedWestSouth[0].GetVector() : sharedWestSouth[1].GetVector();

            // Face-adjacent cubes
            _faceAdjacentBlocks.AddRange([
                _lowerCorner + new Vector3(1, 0, 0), _upperCorner + new Vector3(1, 0, 0),             // Positive X
                _lowerCorner + new Vector3(-1, 0, 0), _upperCorner + new Vector3(-1, 0, 0),           // Negative X
                _lowerCorner + new Vector3(0, 1, 0), _upperCorner + new Vector3(0, 1, 0),             // Positive Y
                _lowerCorner + new Vector3(0, -1, 0), _upperCorner + new Vector3(0, -1, 0),           // Negative Y
                _lowerCorner + new Vector3(0, 0, 1), _upperCorner + new Vector3(0, 0, 1),             // Positive Z
                _lowerCorner + new Vector3(0, 0, -1), _upperCorner + new Vector3(0, 0, -1)]           // Negative Z
            );
        }

        /*
         * Given a 3d point, checks weather it intersects the cube
         */
       // public bool IsIntersectingBlock(Vector3 v)
       // {
       //     return v.X >= lowerCorner.X && v.X <= upperCorner.X &&
       //            v.Y >= lowerCorner.Y && v.Y <= upperCorner.Y &&
       //            v.Z >= lowerCorner.Z && v.Z <= upperCorner.Z;
       // }

       // public Vector3 GetUpperCorner() { return upperCorner; }

        public Vector3 GetLowerCorner() { return _lowerCorner; }
        public TerrainVertex[] GetVertexData()
        {
            if (_north == null || _south == null || _east == null || _west == null || _up == null || _down == null)
                return [];

            return _north.Concat(_south).Concat(_west).Concat(_east).Concat(_up).Concat(_down).ToArray();
        }

       // public List<Vector3> GetFaceAdjacentBlocks()
       // {
       //     return faceAdjacentBlocks;
       // }
    }
}