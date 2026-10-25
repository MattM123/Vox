using OpenTK.Mathematics;
using Vox.Enums;
using Vox.Genesis;
using Vox.Model;
using Vox.UI.MenuLogic;

namespace Vox
{
    public interface IPlayer
    {
        TerrainVertex[] GetViewTargetForRendering();
        Vector3 UpdateViewTarget(out BlockFace playerFacing, out Vector3 blockFace, out Vector3 blockSpace);
        Vector3 GetForwardDirection();
        BlockType GetPlayerBlockType();
        bool IsPlayerGrounded();
        void Jump(float v);
        void Update(float deltaTime);
        Vector3 GetPosition();
        Vector2 GetRotation();
        void SetPosition(Vector3 pos);
        InventoryStore GetInventory();
        void MoveForward(float inc);
        void MoveRight(float inc);
        void MoveUp(float inc);
        Region GetRegionWithPlayer();
        Chunk GetChunkWithPlayer();
        void SetLookDir(float x, float y);
        Vector2 GetLookDir();
        Vector3 GetRightDirection();
        Box3 GetBoundingBox();
        Matrix4 GetViewMatrix();
        Vector3 GetBlockedDirection();
        string ToString();
    }
}
