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
        Vector2 GetForwardRight();
        List<Vector3> GetPlayerCollisionMesh();
        void Update(float deltaTime);
        void UpdateVelocity(float deltaTime);
        Vector3 GetPosition();
        Vector2 GetRotation();
        void SetPosition(Vector3 pos);
        InventoryStore GetInventory();
        Vector3 GetDesiredMovement();
        Vector3 GetVelocity();
        void MoveForward(float inc);
        void MoveRight(float inc);
        void MoveUp(float inc);
        Region GetRegionWithPlayer();
        Chunk GetChunkWithPlayer();
        void SetLookDir(float x, float y);
        Vector2 GetLookDir();
        Vector3 GetRightDirection();
        void UpdateBoundingBox();
        List<Vector3> GetBoundingBox();
        Matrix4 GetViewMatrix();
        void RotateYaw(float deltaYaw);
        Vector3 GetBlockedDirection();
        string ToString();
    }
}
