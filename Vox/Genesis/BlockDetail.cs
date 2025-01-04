

using System.Drawing;
using OpenTK.Mathematics;
using Vox.Genesis;
using Vox.Rendering;

/**
 * Block object for storing more detailed block information on an as-needed basis
 */
public class BlockDetail
{
    private Vertex[] north;
    private Vertex[] south;
    private Vertex[] east;
    private Vertex[] west;
    private Vertex[] up;
    private Vertex[] down;
    private Vector3 upperCorner = Vector3.Zero;
    private Vector3 lowerCorner = Vector3.Zero;

    public BlockDetail() { }
    public BlockDetail(Vertex[] north, Vertex[] south, Vertex[] up, Vertex[] down, Vertex[] east, Vertex[] west)
    {
        this.up = up;
        this.down = down;
        this.south = south;
        this.east = east;
        this.west = west;
        this.north = north;


        //Get points shared by east and north face
        List<Vertex> sharedEastNorth = [];
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
        List<Vertex> sharedWestSouth = [];
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
    public Vertex[] GetVertexData()
    {
        return north.Concat(south).Concat(west).Concat(east).Concat(up).Concat(down).ToArray();
    }

    /*
     * Returns true if this block is adjacent to at least one other block
     */
    public bool IsSurrounded()
    {
        List<Vertex> vertList = [.. RegionManager.GetGlobalChunkFromCoords((int)lowerCorner.X, (int)lowerCorner.Z).GetRenderTask().GetVertexData()];

        for (int i = 0; i < vertList.Count; i += 24)
        {
            if (vertList[i].GetVector().Equals(lowerCorner))
                return true;
        }
        return false;   
    }
}