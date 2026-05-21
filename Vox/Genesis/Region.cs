
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
        public Rectangle _regionBounds;

        [IgnoreMember]
        private IRegionManager? _regionManager;

        [Key(0)]
        public int _xPos;

        [Key(1)]
        public int _zPos;

        [Key(2)]
        public bool didChange = false;

        [Key(3)]
        public Dictionary<string, Chunk> chunks = [];

        [SerializationConstructor]
        public Region() { }

        public Region(IRegionManager regionManager, int xPos, int zPos)
        {
            _regionManager = regionManager ?? throw new ShaderException(nameof(regionManager) + " is null in RegionManager");

            this._xPos = xPos;
            this._zPos = zPos;
            _regionBounds = new(xPos, zPos, _regionManager.GetRegionBounds(), _regionManager.GetRegionBounds());
        }

        /// <summary>
        /// Re-Initializes the region with the given region manager. 
        /// This is used when deserializing a region from file, as the region manager is not serialized with the region.
        /// </summary>
        /// <param name="regionManager"></param>
        public void Initialize(IRegionManager regionManager)
        {
            _regionManager = regionManager ?? throw new ShaderException(nameof(regionManager) + " is null in RegionManager");
            _regionBounds = new(_xPos, _zPos, _regionManager!.GetChunkBounds(), _regionManager.GetChunkBounds());
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
            return _regionBounds;
        }
        public override bool Equals(object? o)
        {
            if (o == null)
                return false;

            if (o.GetType() == typeof(Region))
            {
                return _regionBounds.X.Equals(((Region)o)._regionBounds.X)
                        && _regionBounds.Y.Equals(((Region)o)._regionBounds.Y);
            }
            return false;
        }

        public override string ToString()
        {

            if (chunks.Count() > 0)
                return "(" + chunks.Count() + " Chunks) Region[" + _regionBounds.X
                        + ", " + _regionBounds.Y + "]";
           else
                return "(Empty) Region[" + _regionBounds.X + ", " + _regionBounds.Y + "]";
        }

        //Gets the region index given chunk coordinates
        public string GetRegionIndex()
        {
            return $"{_regionBounds.X}|{_regionBounds.Y}";
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}

