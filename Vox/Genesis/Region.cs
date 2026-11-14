
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
        public Rectangle RegionBounds;

        [IgnoreMember]
        private IRegionManager? _regionManager;

        [Key(0)]
        public int XPos;

        [Key(1)]
        public int ZPos;

        [Key(2)]
        public bool DidChange = false;

        [Key(3)]
        public Dictionary<string, Chunk> Chunks = [];

        [SerializationConstructor]
        public Region() { }

        public Region(IRegionManager regionManager, int xPos, int zPos)
        {
            _regionManager = regionManager ?? throw new ShaderException(nameof(regionManager) + " is null in RegionManager");

            this.XPos = xPos;
            this.ZPos = zPos;
            RegionBounds = new(xPos, zPos, _regionManager.GetRegionBounds(), _regionManager.GetRegionBounds());
        }

        /// <summary>
        /// Re-Initializes the region with the given region manager. 
        /// This is used when deserializing a region from file, as the region manager is not serialized with the region.
        /// </summary>
        /// <param name="regionManager"></param>
        public void Initialize(IRegionManager regionManager)
        {
            _regionManager = regionManager ?? throw new ShaderException(nameof(regionManager) + " is null in RegionManager");
            RegionBounds = new(XPos, ZPos, _regionManager!.GetChunkBounds(), _regionManager.GetChunkBounds());
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
            return RegionBounds;
        }
        public override bool Equals(object? o)
        {
            if (o == null)
                return false;

            if (o.GetType() == typeof(Region))
            {
                return RegionBounds.X.Equals(((Region)o).RegionBounds.X)
                        && RegionBounds.Y.Equals(((Region)o).RegionBounds.Y);
            }
            return false;
        }

        public override string ToString()
        {

            if (Chunks.Count() > 0)
                return "(" + Chunks.Count() + " Chunks) Region[" + RegionBounds.X
                        + ", " + RegionBounds.Y + "]";
           else
                return "(Empty) Region[" + RegionBounds.X + ", " + RegionBounds.Y + "]";
        }

        //Gets the region index given chunk coordinates
        public string GetRegionIndex()
        {
            return $"{RegionBounds.X}|{RegionBounds.Y}";
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}

