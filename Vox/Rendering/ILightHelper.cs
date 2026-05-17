using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Mathematics;
using Vox.Enums;
using Vox.Genesis;
using Vox.Model;

namespace Vox.Rendering
{
    public interface ILightHelper
    {
        int GetMaxLightSpread();
        ColorVector GetBlockLight(Vector3 location, BlockFace faceDir, Chunk chunk);
        void SetBlockLight(Vector3 location, ColorVector color, Chunk chunk, bool depropagate, bool colorOverride);
        void SetBlockLight(Vector3 location, int packedColor, Chunk chunk);
        void TrackLighting(Vector3 location, ColorVector color);
        Dictionary<Vector3, ColorVector> GetLightTrackingList();
        int GetBlueLight(Vector3 location, BlockFace faceDir, Chunk chunk);
        void SetBlueLight(Vector3 location, BlockFace faceDir, int val, Chunk chunk, bool colorOverride);
        int GetGreenLight(Vector3 location, BlockFace faceDir, Chunk chunk);
        void SetGreenLight(Vector3 location, BlockFace faceDir, int val, Chunk chunk, bool colorOverride);
        int GetRedLight(Vector3 location, BlockFace faceDir, Chunk chunk);
        void SetRedLight(Vector3 location, BlockFace faceDir, int val, Chunk chunk, bool colorOverride);
    }
}
