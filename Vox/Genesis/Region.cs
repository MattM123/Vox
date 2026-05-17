
using System.Drawing;
using MessagePack;
using OpenTK.Mathematics;
using OpenTK.Platform.Windows;
using Vox.Exceptions;
using Vox.Rendering;

namespace Vox.Genesis
{
    [MessagePackObject]
    public class Region
    {
        [IgnoreMember]
        public readonly Rectangle regionBounds;

        [IgnoreMember]
        private IRegionManager _regionManager;

        [Key(0)]
        public int x;

        [Key(1)]
        public int z;

        [Key(2)]
        public bool didChange = false;

        [Key(3)]
        public Dictionary<string, Chunk> chunks = [];



        [SerializationConstructor]
        public Region(IRegionManager regionManager, int x, int z) {
            _regionManager = regionManager ?? throw new ShaderException(nameof(regionManager) + " is null in RegionManager");

            this.x = x;
            this.z = z;
            regionBounds = new(x, z, RegionManager.REGION_BOUNDS, RegionManager.REGION_BOUNDS);

        }

        //public bool IsChunkLoaded(Vector3 chunkLocation)
        //{
        //    string chunkIdx = 
        //        $"{Math.Floor(chunkLocation.X / _regionManager.GetChunkBounds()) * _regionManager.GetChunkBounds()}|" +
        //        $"{Math.Floor(chunkLocation.Y / _regionManager.GetChunkBounds()) * _regionManager.GetChunkBounds()}|" +
        //        $"{Math.Floor(chunkLocation.Z / _regionManager.GetChunkBounds()) * _regionManager.GetChunkBounds()}";
        //   
        //    int[] chunkIdxArray = chunkIdx.Split('|').Select(int.Parse).ToArray();
        //    string regionIdx = GetRegionIndex(chunkIdxArray[0], chunkIdxArray[2]);
        //
        //    try
        //    {
        //        Region r = _regionManager.GetVisibleRegions()[regionIdx];
        //        Chunk c = r.chunks[chunkIdx];
        //    } catch (KeyNotFoundException)
        //    {
        //        return false;
        //    }
        //    return  true;
        //}

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
        public string GetRegionIndex(int chunkX, int chunkZ)
        {
            Rectangle bounds = _regionManager.GetGlobalRegionFromChunkCoords(chunkX, chunkZ).GetBounds();
            return $"{bounds.X}|{bounds.Y}";
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}

