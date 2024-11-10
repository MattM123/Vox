﻿
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Reflection;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Vox.Genesis;
using Vox.Model;
using Vox.Rendering;
using Vox.Texturing;

namespace Vox
{
    public class Player
    {
        public static Vector3 position = Vector3.Zero;
        private static float yaw = 0f;
        private static float pitch = 0f;
        private static readonly float halfWidth = 1f;
        private static readonly float halfDepth = 1f;
        private static readonly float playerHeight = 20f;
        private static Vector3 prevPos = Vector3.Zero;
        private static Vector3 playerMin;
        private static Vector3 playerMax;
        private static Vector3 velocity = Vector3.Zero;
        public readonly float moveSpeed = 5.0f;
        private Vector3 desiredMovement = Vector3.Zero;
        private float forward = 0f;
        private float right = 0f;
        private bool IsGrounded = false;
        private Vector3 lastDesiredMovement = Vector3.Zero;
        private Vector3 blockedDirection = Vector3.Zero;
        private List<Vector3> viewBlock = [];
        public readonly float reachDistance = 5f;
        private Vertex[] viewTarget = [];
        private BlockType playerSelectedBlock = BlockType.TEST_BLOCK;
        private Vector3 targetVertex = Vector3.Zero;

        private static float gravity = 0f;// 9.8f;      // Gravity constant
        private static float terminalVelocity = -40f;  // Maximum falling speed (Y velocity)
        /**
         * Default player object is initialized at a position of 0,0,0 within
         * Region 0,0.
         */
        public Player()
        {

            ChunkCache.SetPlayerChunk(GetChunkWithPlayer());

            position = new(0, GetChunkWithPlayer().GetHeightmap()[0, 0] + 10, 0);
            prevPos = position;
            playerMin = new Vector3(position.X - halfWidth, position.Y, position.Z - halfDepth);
            playerMax = new Vector3(position.X + halfWidth, position.Y + playerHeight, position.Z + halfDepth);
        }

        public Vertex[] GetViewTargetForRendering()
        {
            return viewTarget;
        }

        /*
         * Updates the outline on the block the player is looking at.
         * Returns the Vector3 that the block mesh is created from.
         */
        public Vector3 UpdateViewTarget()
        {
            //Update the player picked block based on view target
            BlockModel model = ModelLoader.GetModel(playerSelectedBlock);
            Vector3 direction = GetForwardDirection().Normalized();

            float stepSize = 0.01f;  // Distance to step along the ray each iteration
            float maxDistance = reachDistance;  // Maximum reach distance for the ray
            Vector3 currentPosition = position;

            for (float distance = 0; distance < maxDistance; distance += stepSize)
            {
                // Move the current position along the forward direction
                currentPosition += direction * stepSize;

                // I cooked hard with this one
                targetVertex = new(
                    (float)Math.Round(currentPosition.X + (direction.X * reachDistance)),
                    (float)Math.Round(currentPosition.Y + (direction.Y * reachDistance)),
                    (float)Math.Round(currentPosition.Z + (direction.Z * reachDistance))
                );
                
                //populate the block vertexes for the draw call
                List<Vertex> viewBlock = [];
                viewBlock.AddRange(ModelUtils.GetCuboidFace(model, Face.SOUTH, targetVertex, null));
                viewBlock.AddRange(ModelUtils.GetCuboidFace(model, Face.NORTH, targetVertex, null));
                viewBlock.AddRange(ModelUtils.GetCuboidFace(model, Face.UP,    targetVertex, null));
                viewBlock.AddRange(ModelUtils.GetCuboidFace(model, Face.DOWN,  targetVertex, null));
                viewBlock.AddRange(ModelUtils.GetCuboidFace(model, Face.WEST,  targetVertex, null));
                viewBlock.AddRange(ModelUtils.GetCuboidFace(model, Face.EAST,  targetVertex, null));

                viewTarget = [.. viewBlock];
                return targetVertex;
            }
            //Returns the last block the player was looking at
            return targetVertex;
        }

        public BlockType GetPlayerSelectedBlock()
        {
            return playerSelectedBlock;
        }

        public bool IsPlayerGrounded()
        {
            return IsGrounded;
        }
        public Vector2 GetForwardRight()
        {
            return new Vector2(forward, right);
        }

        public static List<Vector3> GetPlayerCollisionMesh()
        {
            Vector3 playerPos = new((float)Math.Floor(position.X),
                Chunk.GetGlobalHeightMapValue((int)Math.Floor(position.X), (int)Math.Floor(position.Z)),
                (float)Math.Floor(position.Z));

            List<Vector3> collisionMesh = [];
            collisionMesh.Add(playerPos);

            //Block at players feet
            collisionMesh.Add(new(playerPos.X, playerPos.Y - 1, playerPos.Z));

            collisionMesh.Add(new(playerPos.X + 1, playerPos.Y, playerPos.Z));
            collisionMesh.Add(new(playerPos.X + 1, playerPos.Y, playerPos.Z + 1));
            collisionMesh.Add(new(playerPos.X + 1, playerPos.Y, playerPos.Z - 1));

            collisionMesh.Add(new(playerPos.X, playerPos.Y, playerPos.Z + 1));
            collisionMesh.Add(new(playerPos.X, playerPos.Y, playerPos.Z - 1));

            collisionMesh.Add(new(playerPos.X - 1, playerPos.Y, playerPos.Z));
            collisionMesh.Add(new(playerPos.X - 1, playerPos.Y, playerPos.Z - 1));
            collisionMesh.Add(new(playerPos.X - 1, playerPos.Y, playerPos.Z + 1));


            collisionMesh.Add(new(playerPos.X + 1, playerPos.Y + 1, playerPos.Z));
            collisionMesh.Add(new(playerPos.X + 1, playerPos.Y + 1, playerPos.Z + 1));
            collisionMesh.Add(new(playerPos.X + 1, playerPos.Y + 1, playerPos.Z - 1));

            collisionMesh.Add(new(playerPos.X, playerPos.Y + 1, playerPos.Z + 1));
            collisionMesh.Add(new(playerPos.X, playerPos.Y + 1, playerPos.Z - 1));

            collisionMesh.Add(new(playerPos.X - 1, playerPos.Y + 1, playerPos.Z));
            collisionMesh.Add(new(playerPos.X - 1, playerPos.Y + 1, playerPos.Z - 1));
            collisionMesh.Add(new(playerPos.X - 1, playerPos.Y + 1, playerPos.Z + 1));

            return collisionMesh;

        }

        //Does not work
        private void CheckChunkCollision(float deltaTime)
        {
            int collisionIterations = 1;

            Vector3 totalNormal = Vector3.Zero;
            float highestVoxelMaxY = float.MinValue; // Track the highest voxel max Y
            List<Vector3> collMesh = GetPlayerCollisionMesh();

            // Store the min and max of the last voxel that caused a collision
            Vector3 highestOverlap = Vector3.Zero;
            Vector3 highestNormal = Vector3.Zero;

            for (int i = 0; i < collMesh.Count; i++) 
            {

                // Get bounding box of voxel;
                Vector3 voxelMin = collMesh.ElementAt(i);
                Vector3 voxelMax = collMesh.ElementAt(i) + Vector3.One;
                Vector3 collisionNormal = HandleCollision(playerMin, playerMax, voxelMin, voxelMax);

                // If no collision, Vector3.Zero is returned
                if (collisionNormal.LengthSquared > 0)
                {
                    // Calculate overlaps for each axis
                    Vector3 overlap = new Vector3(
                        Math.Min(playerMax.X, voxelMax.X) - Math.Max(playerMin.X, voxelMin.X),
                        Math.Min(playerMax.Y, voxelMax.Y) - Math.Max(playerMin.Y, voxelMin.Y),
                        Math.Min(playerMax.Z, voxelMax.Z) - Math.Max(playerMin.Z, voxelMin.Z)
                    );

                    // Only keep the collision normal if the overlap is greater than the previous highest overlap
                    if (overlap.LengthSquared > highestOverlap.LengthSquared)
                    {
                        highestOverlap = overlap;
                        highestNormal = collisionNormal;
                    }
                    // Logger.Debug("Collision with " + collMesh.ElementAt(i));
                    totalNormal += collisionNormal;

                }
            }

            if (highestNormal != Vector3.Zero)
            {
      
                highestNormal.Normalize();
                if (highestNormal.Y > 0.5f || Math.Abs(highestNormal.Y) > Math.Abs(highestNormal.X) && Math.Abs(highestNormal.Y) > Math.Abs(highestNormal.Z))
                {
                    IsGrounded = true;   // Set the grounded flag
                    velocity.Y = 0;      // Stop vertical movement (falling)
                    blockedDirection.Y = Math.Sign(highestNormal.Y);
                }

                // Stop movement along the wall's axis
                if (Math.Abs(highestNormal.X) > 0.5f || (Math.Abs(highestNormal.X) > Math.Abs(highestNormal.Y) && Math.Abs(highestNormal.X) > Math.Abs(highestNormal.Z)))
                {

                    // Block movement only in the direction of the wall (positive or negative X)
                    if (highestNormal.X > 0 && velocity.X > 0) velocity.X = 0; // Block positive X movement
                    else if (highestNormal.X < 0 && velocity.X < 0) velocity.X = 0; // Block negative X movement

                    // Set blocked direction to the wall's side
                    blockedDirection.X = Math.Sign(highestNormal.X);
                    velocity.X = -velocity.X;

                }

                if (Math.Abs(highestNormal.Z) > 0.5f || (Math.Abs(highestNormal.Z) > Math.Abs(highestNormal.Y) && Math.Abs(highestNormal.Z) > Math.Abs(highestNormal.X)))
                {
                    // Block movement only in the direction of the wall (positive or negative Z)
                    if (highestNormal.Z > 0 && velocity.Z > 0) velocity.Z = 0; // Block positive Z movement
                    else if (highestNormal.Z < 0 && velocity.Z < 0) velocity.Z = 0; // Block negative Z movement

                    // Set blocked direction to the wall's side
                    blockedDirection.Z = Math.Sign(highestNormal.Z);
                    velocity.Z = -velocity.Z;
                }

                Vector3 correction = new Vector3(
                    highestNormal.X != 0 ? highestOverlap.X : 0,
                    highestNormal.Y != 0 ? highestOverlap.Y : 0,
                    highestNormal.Z != 0 ? highestOverlap.Z : 0
                );

                // Move the player out of the wall
                position.Y -= correction.Y * velocity.Y;// * velocity * deltaTime;

                //Get the direction behind the player in X and Z directions and use it to update position
                //on collision with X and Z walls
                float behindPlayerX = Vector3.Normalize(GetViewMatrix().Column2.Xyz).X * -1f;
                float behindPlayerZ = Vector3.Normalize(GetViewMatrix().Column2.Xyz).Z * -1f;

                //If player walk into wall
                if (Math.Sign(GetForwardDirection().X) == Math.Sign(blockedDirection.X)
                    || Math.Sign(GetForwardDirection().Y) == Math.Sign(blockedDirection.Y)
                    || Math.Sign(GetForwardDirection().Z) == Math.Sign(blockedDirection.Z))
                {
                    position.X -= correction.X * behindPlayerX * deltaTime;
                    position.Z -= correction.Z * behindPlayerZ * deltaTime;
                }

                //if player backs up into wall
                if (Math.Sign(-GetForwardDirection().X) == Math.Sign(blockedDirection.X)
                    || Math.Sign(-GetForwardDirection().Y) == Math.Sign(blockedDirection.Y)
                    || Math.Sign(-GetForwardDirection().Z) == Math.Sign(blockedDirection.Z))
                {
                    position.X += correction.X * behindPlayerX * deltaTime;
                    position.Z += correction.Z * behindPlayerZ * deltaTime;
                }

                // Update position based on the corrected velocity
                      position.Y += velocity.Y * deltaTime;
            }
            else
            {
                IsGrounded = false; // If no collision, the player is in the air
            }
          //  blockedDirection = Vector3.Zero;
        }

        public void Update(float deltaTime)
        {
            //Update pick block
            UpdateViewTarget();

            // Apply gravity to Y-velocity
            if (velocity.Y > terminalVelocity && !IsGrounded) // Prevent exceeding terminal velocity
            {
                velocity.Y -= gravity * deltaTime; // Apply gravity
            }


            // Update velocity based on desired movement
            if (desiredMovement.LengthSquared > float.Epsilon && desiredMovement != Vector3.Zero)
            {
                velocity = desiredMovement.Normalized() * moveSpeed;
            }

            desiredMovement = Vector3.Zero;
            
            UpdateBoundingBox();

            // Update the player's state in the game world, like checking for collisions
         //   CheckChunkCollision(deltaTime);


            // Calculate new velocity based on position change
            UpdateVelocity(deltaTime);

            // Apply friction or damping to velocity
            ApplyFriction(deltaTime);

            // Update position based on current velocity
            position += velocity * deltaTime;

        }

        public void UpdateVelocity(float deltaTime)
        {

            if (desiredMovement != lastDesiredMovement)
            {
                // Only normalize desiredMovement if it has a non-zero length
                Vector3 inputDirection = desiredMovement.LengthSquared > float.Epsilon ? desiredMovement.Normalized() : Vector3.Zero;

                // Update lastDesiredMovement to the current desired movement
                lastDesiredMovement = desiredMovement;

                // Check if the player is attempting to move in a blocked direction
                   if (blockedDirection.X != 0 && Math.Sign(desiredMovement.X) == Math.Sign(blockedDirection.X))
                   {
                       inputDirection.X = 0; // Prevent movement along the blocked X axis
                   }
                   if (blockedDirection.Z != 0 && Math.Sign(desiredMovement.Z) == Math.Sign(blockedDirection.Z))
                   {
                       inputDirection.Z = 0; // Prevent movement along the blocked Z axis
                   }
                   if (blockedDirection.Y != 0 && Math.Sign(desiredMovement.Y) == Math.Sign(blockedDirection.Y))
                   {
                       inputDirection.Y = 0; // Prevent movement along the blocked Z axis
                   }

                // Apply the input direction to velocity if movement is allowed
                velocity = inputDirection != Vector3.Zero ? inputDirection * moveSpeed : Vector3.Zero;
            }

            prevPos = position;
        }
        /**
         * Returns a normal representing the block face that the player collides into.
         * This is used to calulate slide behaviour on block collision
         */
        private Vector3 HandleCollision(Vector3 boxAMin, Vector3 boxAMax, Vector3 boxBMin, Vector3 boxBMax)
        {
            // Calculate the overlap on each axis
            float overlapX = Math.Min(boxAMax.X, boxBMax.X) - Math.Max(boxAMin.X, boxBMin.X);
            float overlapY = Math.Min(boxAMax.Y, boxBMax.Y) - Math.Max(boxAMin.Y, boxBMin.Y);
            float overlapZ = Math.Min(boxAMax.Z, boxBMax.Z) - Math.Max(boxAMin.Z, boxBMin.Z);

            int x = (boxAMin.X < boxBMin.X) ? -1 : 1;
            int y = (boxAMin.Y < boxBMin.Y) ? -1 : 1;
            int z = (boxAMin.Z < boxBMin.Z) ? -1 : 1;

            // If there's no overlap on any axis, return zero vector
            if (overlapX < 0 || overlapY < 0 || overlapZ < 0)
                return Vector3.Zero;

            //Single wall collision
            if (overlapX != overlapY && overlapY != overlapZ)
            {
                // Determine which axis has the smallest overlap
                if (overlapX < overlapY && overlapX < overlapZ)
                {
                    // Return the collision normal on the X-axis
                    return new(x, 0, 0);
                }
                else if (overlapY < overlapX && overlapY < overlapZ)
                {
                    // Return the collision normal on the Y-axis
                    return new(0, y, 0);
                }
                else
                {
                    // Return the collision normal on the Z-axis
                    return new(0, 0, z);
                }
            }

            // For multiple axis collisions, combine the normal directions based on the smallest overlaps
            if (overlapX == overlapY && overlapX < overlapZ)
            {
                return new(x, y, 0);
            }
            else if (overlapY == overlapZ && overlapY < overlapX)
            {
                return new(0, y, z);
            }
            else if (overlapX == overlapZ && overlapX < overlapY)
            {
                return new(x, 0, z);
            }

            return new(x, y, z);
        }

        public Vector3 GetPosition()
        {
            return position;
        }

        public Vector2 GetRotation()
        {
            return new Vector2(yaw, pitch);
        }

        public void SetPosition(Vector3 pos)
        {
            position = pos;
        }
        public Vector3 GetDesiredMovement() { return desiredMovement; }
        public Vector3 GetVelocity()
        {
            return velocity;
        }
        private void ApplyFriction(float deltaTime)
        {
            float friction = 0.5f; // Damping factor, adjust as needed
            velocity *= (1 - friction * deltaTime);
        }
        public void MoveForward(float inc)
        {
            forward += inc;
            desiredMovement += GetForwardDirection() * inc;

        }

        public void MoveRight(float inc)
        {
            if (GetRightDirection().X != blockedDirection.X)
            {
                right += inc;
                desiredMovement += GetRightDirection() * inc;
            }
        }

        public void MoveUp(float inc)
        {
            desiredMovement.Y += inc;
        }

        /**
         * Gets the region that the player currently inhabits.
         * If the region doesn't exist yet, generates and adds a new region
         * to the visible regions list
         *
         * @return The region that the player is in
         */
        public Genesis.Region GetRegionWithPlayer()
        {
            Genesis.Region? returnRegion = null;

            int x = (int)GetPosition().X;
            int xLowerLimit = ((x / RegionManager.REGION_BOUNDS) * RegionManager.REGION_BOUNDS);
            int xUpperLimit;
            if (x < 0)
                xUpperLimit = xLowerLimit - RegionManager.REGION_BOUNDS;
            else
                xUpperLimit = xLowerLimit + RegionManager.REGION_BOUNDS;


            int z = (int)GetPosition().Z;
            int zLowerLimit = ((z / RegionManager.REGION_BOUNDS) * RegionManager.REGION_BOUNDS);
            int zUpperLimit;
            if (z < 0)
                zUpperLimit = zLowerLimit - RegionManager.REGION_BOUNDS;
            else
                zUpperLimit = zLowerLimit + RegionManager.REGION_BOUNDS;


            //Calculates region coordinates player inhabits
            int regionXCoord = xUpperLimit;
            int regionZCoord = zUpperLimit;

            foreach (Genesis.Region region in RegionManager.VisibleRegions)
            {
                Rectangle regionBounds = region.GetBounds();
                if (regionXCoord == regionBounds.X && regionZCoord == regionBounds.Y)
                {
                    returnRegion = region;
                }
            }
            returnRegion ??= new(regionXCoord, regionZCoord);

            return returnRegion;
        }


        /**
         * Gets the chunk that the player currently inhabits.
         * If the chunk doesn't exist yet, generates and adds a new chunk
         * to the region
         *
         * @return The chunk that the player is in
         */
        public Chunk GetChunkWithPlayer()
        {
            //Calculates chunk coordinates player inhabits
            int chunkXCoord = (int)GetPosition().X / RegionManager.CHUNK_BOUNDS * RegionManager.CHUNK_BOUNDS;
            int chunkZCoord = (int)GetPosition().Z / RegionManager.CHUNK_BOUNDS * RegionManager.CHUNK_BOUNDS;


            Genesis.Region r = GetRegionWithPlayer();
            Chunk c = r.GetChunkWithLocation(new(chunkXCoord, 0, chunkZCoord));

            if (c == null)
            {
                Chunk d = new Chunk().Initialize(chunkXCoord, chunkZCoord);
                r.BinaryInsertChunkWithLocation(0, r.GetChunks().Count - 1, d.GetLocation());
                return d;
            }
            return c;
        }

        public static void SetLookDir(float x, float y)
        {
            yaw = x;
            pitch = y;
        }

        public Vector2 GetLookDir()
        {
            return new Vector2(yaw, pitch);
        }
        public Vector3 GetForwardDirection()
        {
            Matrix4 viewMatrix = GetViewMatrix();

            //Gets Z Axis from player matrix
            return Vector3.Normalize(new(-viewMatrix.Column2.Xyz));

        }

        public Vector3 GetRightDirection()
        {
            Matrix4 viewMatrix = GetViewMatrix();

            // Get the X Axis (right direction) from the player matrix
            return Vector3.Normalize(viewMatrix.Column0.Xyz);
        }

        private static void UpdateBoundingBox()
        {
            playerMin = new Vector3(position.X - halfWidth, position.Y, position.Z - halfDepth);
            playerMax = new Vector3(position.X + halfWidth, position.Y + playerHeight, position.Z + halfDepth);
        }

        public List<Vector3> GetBoundingBox()
        {
            return [playerMin, playerMax];
        }
        private void GetTransformationMatrix(out Matrix4 matrix)
        {
            float minPitch = -80.0f;  // Prevent looking too far up
            float maxPitch = 80.0f;   // Prevent looking too far down
            float minYaw = -170.0f;   // Optional: Limit the yaw to a certain range
            float maxYaw = 170.0f;    // Optional: You can leave yaw unrestricted if you want
            Vector2 lookDir = GetLookDir();

            // Clamp the pitch and yaw
            float clampedYaw = Math.Clamp(lookDir.X, minPitch, maxPitch); // X-axis controls pitch (up/down)
            float clampedPitch = Math.Clamp(lookDir.Y, minYaw, maxYaw);


            // Create rotation matrices
            Quaternion pitchRotation = Quaternion.FromAxisAngle(Vector3.UnitY, MathHelper.DegreesToRadians(clampedPitch));
            Quaternion yawRotation = Quaternion.FromAxisAngle(Vector3.UnitX, MathHelper.DegreesToRadians(clampedYaw));
            Quaternion localRotation = pitchRotation * yawRotation;

            Vector3 localScale = new(1, 1, 1);

            Matrix3.CreateFromQuaternion(localRotation, out Matrix3 rotation);

            matrix.Row0 = new(rotation.Row0 * localScale.X, 0);
            matrix.Row1 = new(rotation.Row1 * localScale.Y, 0);
            matrix.Row2 = new(rotation.Row2 * localScale.Z, 0);
            matrix.Row3 = new(GetPosition(), 1);
        }
        /**
         * Instantiates the players view matrix witch is later
         * multiplied by the projection and model matrix.
         *
         * @return The players view matrix
         */
        public Matrix4 GetViewMatrix()
        {
            if (!Window.IsMenuRendered())
            {
                Vector3 lookPoint = new(yaw, 0f, pitch);

                // Create rotation matrices
                Quaternion pitchRotation = Quaternion.FromAxisAngle(Vector3.UnitY, MathHelper.DegreesToRadians(GetLookDir().Y));
                Quaternion yawRotation = Quaternion.FromAxisAngle(Vector3.UnitX, MathHelper.DegreesToRadians(GetLookDir().X));
                lookPoint += position;


                lookPoint = Vector3.Transform(lookPoint, pitchRotation * yawRotation);
                return Matrix4.LookAt(position, lookPoint, new(0, 1, 0));

            }
            else
            {
                GetTransformationMatrix(out Matrix4 output);
                output.Invert();
                return output;
            }
        }

        public void RotateYaw(float deltaYaw)
        {
            yaw += deltaYaw;
        }

        public Vector3 GetBlockedDirection()
        { 
            return blockedDirection; 
        }

        public override string ToString()
        {
            return "Player at position (" + position.X + ", " + position.Y + ", " + position.Z + ")";
        }
    }

}
