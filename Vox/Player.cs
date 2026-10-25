using System.Numerics;
using OpenTK.Mathematics;
using Vox.Enums;
using Vox.Genesis;
using Vox.Model;
using Vox.Rendering;
using Vox.UI.MenuLogic;
using Quaternion = OpenTK.Mathematics.Quaternion;
using Region = Vox.Genesis.Region;
using Vector2 = OpenTK.Mathematics.Vector2;
using Vector3 = OpenTK.Mathematics.Vector3;
using Vector4 = OpenTK.Mathematics.Vector4;

namespace Vox
{
    public class Player : IPlayer
    {
        private readonly IInventoryStore? _inventoryStore;
        private readonly ISSBOManager? _ssboManager;
        private readonly IRegionManager? _regionManager;
        private readonly IChunkCache? _chunkCache;
        private readonly IShaderManager? _shaderManager;
        
        private static Vector3 _position = Vector3.Zero;
        private static float yaw = 0f;
        private static float pitch = 0f;
        private const float PlayerHalfWidth = 0.5f;
        private const float PlayerHalfDepth = 0.5f;
        private const float PlayerHeight = 2f;
        private static Vector3 prevPos = Vector3.Zero;

        private static Vector3 velocity = Vector3.Zero;
        public readonly float moveSpeed = 5.0f;
        private Vector3 desiredMovement = Vector3.Zero;
        private float forward = 0f;
        private float right = 0f;
        private Vector3 lastDesiredMovement = Vector3.Zero;
        private Vector3 blockedDirection = Vector3.Zero;
        public readonly float reachDistance = 60;
        public TerrainVertex[] viewTarget = [];
        private Box3 playerAabb;
        private readonly BlockType playerSelectedBlock = BlockType.LAMP_BLOCK;
        private const float CollisionSkin = 0.01f;
        private const float CollisionEpsilon = 0.0001f;

        private static readonly float gravity = 9.8f;      // Gravity constant
        private static readonly float terminalVelocity = -40f;  // Maximum falling speed (Y velocity)
        /**
         * Default player object is initialized at a position of 0,0,0 within
         * Region 0,0.
         */
        public Player(ISSBOManager ssboManager, IInventoryStore inventoryStore, IRegionManager regionManager, IChunkCache chunkCache, IShaderManager shaderManager)
        {
            _ssboManager = ssboManager;
            _inventoryStore = inventoryStore;
            _regionManager = regionManager;
            _chunkCache = chunkCache;
            _shaderManager = shaderManager;


            // Test / debug values for inventory, can be removed later
            _inventoryStore.SetSlot(0, BlockType.LAMP_BLOCK, 5);
            _inventoryStore.SetSlot(1, BlockType.DIRT_BLOCK, 3);
            _inventoryStore.SetSlot(2, BlockType.GRASS_BLOCK, 16);
            _inventoryStore.SetSlot(3, BlockType.TEST_BLOCK, 55);
            _inventoryStore.SetSlot(4, BlockType.TARGET_BLOCK, 64);
            _inventoryStore.SetSlot(5, BlockType.STONE_BLOCK, 10);

            _chunkCache.SetPlayerChunk(GetChunkWithPlayer());      
            _position = new(0, _regionManager.GetGlobalHeightMapValue((int)_position.X, (int)_position.Z) + 1, 0);
            prevPos = _position;

            UpdateBoundingBox();
        }

        public TerrainVertex[] GetViewTargetForRendering()
        {
            return viewTarget;
        }

        /// <summary>
        /// Updates the outline on the block the player is currently looking at and determines relevant block
        /// interaction information.
        /// </summary>
        /// <remarks>This method performs a raycast from the player's position in the direction they are
        /// facing, up to the maximum reach distance, to identify the targeted block and related interaction data. The
        /// output parameters provide additional context for block placement and interaction logic.</remarks>
        /// <param name="playerFacing">When this method returns, contains the direction the player is facing, as a value of the BlockFace
        /// enumeration.</param>
        /// <param name="blockFace">When this method returns, contains the block face the player is looking at, represented as a Vector3.</param>
        /// <param name="blockSpace">When this method returns, contains the coordinates of the empty block space immediately preceding the
        /// intersection with terrain, indicating where a block would be placed if the player initiates a placement
        /// action.</param>
        /// <returns>A <see cref="Vector3"/> representing the coordinates of the block mesh that the player is targeting. If no block is
        /// targeted, the returned vector may be zero.</returns>
        public Vector3 UpdateViewTarget(out BlockFace playerFacing, out Vector3 blockFace, out Vector3 blockSpace)
        {
            playerFacing = BlockFace.ALL;
            blockFace = Vector3.Zero;
            blockSpace = Vector3.Zero;

            float stepSize = 0.2f;  // Distance to step along the ray each iteration
            float maxDistance = reachDistance;  // Maximum reach distance for the ray
            Vector3 rayOrigin = _position;
            Vector3 target = Vector3.Zero;
            Vector3 currentPosition = Vector3.Zero;
            Vector3 previousBlock = Vector3.Zero;
            Vector3 rayDirection = GetForwardDirection();
            for (float distance = 0; distance < maxDistance; distance += stepSize)
            {

                // Calculate the current position along the ray, round to make divisible by stepSize
                currentPosition = rayOrigin + rayDirection * distance;

                target = new(
                    (float)Math.Floor(currentPosition.X),
                    (float)Math.Floor(currentPosition.Y),
                    (float)Math.Floor(currentPosition.Z)
                );

                //If the ray steps into an air block, continue to the next step iteration
                Chunk actionChunk = _regionManager!.GetAndLoadGlobalChunkFromCoords((int)target.X, (int)target.Y, (int)target.Z);
                Vector3 blockDataIndex = _regionManager.GetChunkRelativeCoordinates(target);
                if (actionChunk._blockData[(short)blockDataIndex.X, (short)blockDataIndex.Y, (short)blockDataIndex.Z] == (int)BlockType.AIR) {
                    previousBlock = target;
                    blockSpace = target;
                    continue;
                }   
                //else, render block selection target and break the loop
                else
                {
                    blockSpace = previousBlock;

                    //calculate direction player is facing from their view matrix
                    Vector3 absoluteDirection = new(Math.Abs(rayDirection.X), Math.Abs(rayDirection.Y), Math.Abs(rayDirection.Z));
                    if (absoluteDirection.X > absoluteDirection.Z && absoluteDirection.X > absoluteDirection.Y)
                        playerFacing = (rayDirection.X > 0 ? BlockFace.EAST : BlockFace.WEST);

                    else if (absoluteDirection.Y > absoluteDirection.X && absoluteDirection.Y > absoluteDirection.Z)
                        playerFacing = (rayDirection.Y > 0 ? BlockFace.UP : BlockFace.DOWN);

                    else
                        playerFacing = (rayDirection.Z > 0 ? BlockFace.NORTH : BlockFace.SOUTH);



                    Vector3 blockCenter = Vector3.Add(target, new(0.5f, 0.5f, 0.5f));

                    //Create block view matrix to calculate blockface player is looking at
                    Matrix4 blockViewMat = Matrix4.LookAt(blockCenter, currentPosition, new Vector3(0.0f, 1f, 0.0f));

                    Vector3 blockForwardDir = Vector3.Normalize(new(-blockViewMat.Column2.Xyz));
                    Vector3 absBlockForwardDirection = new(Math.Abs(blockForwardDir.X), Math.Abs(blockForwardDir.Y), Math.Abs(blockForwardDir.Z));

                    Vector3 blockFaceToAdd = new((float)Math.Round(Math.Abs(blockForwardDir.X)), (float)Math.Round(Math.Abs(blockForwardDir.Y)), (float)Math.Round(Math.Abs(blockForwardDir.Z)));

                    if (absBlockForwardDirection.X > absBlockForwardDirection.Z && absBlockForwardDirection.X > absBlockForwardDirection.Y)
                        blockFace = new(blockFaceToAdd.X, 0, 0);


                    else if (absBlockForwardDirection.Y > absBlockForwardDirection.X && absBlockForwardDirection.Y > absBlockForwardDirection.Z)
                        blockFace = new(0, blockFaceToAdd.Y, 0);

                    else
                        blockFace = new(0, 0, blockFaceToAdd.Z);

                    break;
                }
            }
            //Returns the last block the player was looking at only if no menu is open
            if (!Window.IsAnyMenuShowing())
                _shaderManager.GetShaderProgram("Terrain").SetVector3Uniform("targetVertex", target);

            return target;
        }

        public Vector3 GetForwardDirection()
        {
            Matrix4 viewMatrix = GetViewMatrix();

            //Gets Z Axis from player matrix
            return -viewMatrix.Column2.Xyz;

        }

        public BlockType GetPlayerBlockType()
        {
            return playerSelectedBlock;
        }

        /// <summary>
        /// Retrieves the positions of all non-air blocks that overlap the player's
        /// AABB. Calculates the minimum and maximum block
        /// coordinates on each axis, using a small boundary epsilon to avoid
        /// including blocks that only touch the bounding box's maximum boundaries.
        /// </summary>
        /// <returns>
        /// A list of <see cref="Vector3"/> positions representing solid block
        /// candidates for player collision detection.
        /// </returns>
        private List<Vector3> GetPlayerCollisionCandidates()
        {
            List<Vector3> candidates = [];
            const float boundaryEpsilon = 0.0001f;

            int minX = (int)MathF.Floor(playerAabb.Min.X);
            int minY = (int)MathF.Floor(playerAabb.Min.Y);
            int minZ = (int)MathF.Floor(playerAabb.Min.Z);
            
            int maxX = (int)MathF.Floor(playerAabb.Max.X - boundaryEpsilon);
            int maxY = (int)MathF.Floor(playerAabb.Max.Y - boundaryEpsilon);
            int maxZ = (int)MathF.Floor(playerAabb.Max.Z - boundaryEpsilon);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        //Only solid blocks are valid candidates
                        if (_regionManager?.GetBlockTypeAtCoords(x, y, z) != BlockType.AIR)
                            candidates.Add(new Vector3(x, y, z));
                    }
                }
            }
            return candidates;
        }

        public bool IsPlayerGrounded()
        {
            
            // Don't consider the player grounded while jumping upward.
            if (velocity.Y > 0)
                return false;
            
            const float groundProbe = 0.05f;

            Vector3 probeMin = playerAabb.Min -
                               new Vector3(0, groundProbe, 0);

            Vector3 probeMax = playerAabb.Min;

            int minX = (int)MathF.Floor(probeMin.X);
            int maxX = (int)MathF.Floor(playerAabb.Max.X - CollisionEpsilon);
            int minZ = (int)MathF.Floor(probeMin.Z);
            int maxZ = (int)MathF.Floor(playerAabb.Max.Z - CollisionEpsilon);
            int y = (int)MathF.Floor(probeMin.Y);

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    if (_regionManager!.GetBlockTypeAtCoords(x, y, z) != BlockType.AIR)
                        return true;
                }
            }

            return false;
        }
     
        /// <summary>
        /// Updates the player's movement and physics for the current frame.
        /// Applies collision and physics to the player based on the
        /// requested movements initiated by key press events.
        /// </summary>
        /// <param name="deltaTime">The elapsed time in seconds since the previous update.</param>
        public void Update(float deltaTime) 
        { 
            // -----------------------------
            // Gravity 
            // -----------------------------
            if (!IsPlayerGrounded() && velocity.Y > terminalVelocity)// Prevent exceeding terminal velocity
            { 
                velocity.Y -= gravity * deltaTime; // Apply gravity
            } 
            
            // -----------------------------
            // Horizontal movement
            // -----------------------------
            if (desiredMovement.LengthSquared > float.Epsilon && desiredMovement != Vector3.Zero)
            { 
                Vector3 movement = desiredMovement.Normalized() * moveSpeed; 
                velocity.Xz = movement.Xz; 
            } 
            desiredMovement = Vector3.Zero; 
            
            
            // ---------------------------------
            // Check collision
            // ---------------------------------
            Vector3 displacement = velocity * deltaTime;

            const float maxStepDistance = 0.2f;

            int steps = Math.Max(
                1,
                (int)MathF.Ceiling(displacement.Length / maxStepDistance)
            );

            float stepDeltaTime = deltaTime / steps;

            for (int i = 0; i < steps; i++)
            {
                Vector3 step = velocity * stepDeltaTime;

                // Resolve each axis independently.
                MoveAndCollide(step.X, step.Y, step.Z);
            }
            
            // ---------------------------------
            // Apply friction to player velocity
            // ---------------------------------
            ApplyFriction(deltaTime);
            
        }
        
        public Vector3 GetPosition()
        {
            return _position;
        }

        public Vector2 GetRotation()
        {
            return new Vector2(yaw, pitch);
        }

        public InventoryStore GetInventory()
        {
            return (InventoryStore) _inventoryStore!;
        }

        public void SetPosition(Vector3 pos)
        {
            _position = pos;
            UpdateBoundingBox();
        }

        /// <summary>
        /// Applies friction to the player's velocity, simulating resistance and preventing perpetual motion. 
        /// The friction is applied as a damping factor that reduces the velocity over time, creating a more realistic movement experience. 
        /// Adjust the friction value as needed to achieve the desired feel for player movement.
        /// 
        /// A higher value will result in a floating movement feel, while a lower value will allow for more momentum and sliding. 
        /// The friction is applied proportionally to the deltaTime to ensure consistent behavior regardless of frame rate.
        /// </summary>
        /// <param name="deltaTime"></param>
        private void ApplyFriction(float deltaTime)
        {
            float friction = 0.5f;
            velocity *= (1 - friction * deltaTime);
        }
        
        /// <summary>
        /// Adds movement in the direction the player is facing to the desired movement vector.
        /// The accumulated movement is processed during the next update cycle.
        /// </summary>
        /// <param name="inc">The amount of forward or backward movement to request.</param>
        public void MoveForward(float inc)
        {
            forward += inc;
            desiredMovement += GetForwardDirection() * inc;
            UpdateBoundingBox();
        }

        /// <summary>
        /// Adds movement along the player's right direction to the desired movement vector,
        /// provided the direction passes the current blocked-direction check.
        /// The accumulated movement is processed during the next update cycle.
        /// </summary>
        /// <param name="inc">The amount of rightward or leftward movement to request.</param>
        public void MoveRight(float inc)
        {
            if (GetRightDirection().X != blockedDirection.X)
            {
                right += inc;
                desiredMovement += GetRightDirection() * inc;
                UpdateBoundingBox();
            }

        }

        /// <summary>
        /// Adds vertical movement to the desired movement vector.
        /// The accumulated movement is processed during the next update cycle.
        /// </summary>
        /// <param name="inc">The amount of upward or downward movement to request.</param>
        public void MoveUp(float inc)
        {
            desiredMovement.Y += inc;
            UpdateBoundingBox();
        }
        /// <summary>
        /// Applies an upward velocity to the player if the player is grounded.
        /// The supplied velocity is used to initiate the jump.
        /// </summary>
        /// <param name="v">The vertical velocity to apply when jumping.</param>
        public void Jump(float v)
        {
            if (IsPlayerGrounded())
            {
                velocity.Y = v;
            }
        }

        /**
         * Gets the region that the player currently inhabits.
         * If the region doesn't exist yet, generates and adds a new region
         * to the visible regions list
         *
         * @return The region that the player is in
         */
        public Region GetRegionWithPlayer()
        {

            string playerChunkIdx = $"{Math.Floor(_position.X / _regionManager!.GetChunkBounds()) * _regionManager.GetChunkBounds()}|{Math.Floor(_position.Y / _regionManager.GetChunkBounds()) * _regionManager.GetChunkBounds()}|{Math.Floor(_position.Z / _regionManager.GetChunkBounds()) * _regionManager.GetChunkBounds()}";
            int[] index = playerChunkIdx.Split('|').Select(int.Parse).ToArray();
            string playerRegionIdx = _regionManager.GetRegionIndexFromChunkCoords(index[0], index[2]);
            return _regionManager.TryGetRegionFromFile(playerRegionIdx);
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
            return _regionManager!.GetAndLoadGlobalChunkFromCoords((int)_position.X, (int)_position.Y, (int)_position.Z);
        }

        public void SetLookDir(float x, float y)
        {
            yaw = -x;
            pitch = -y;
        }

        public Vector2 GetLookDir()
        {
            return new Vector2(yaw, pitch);
        }

        public Vector3 GetRightDirection()
        {
            Matrix4 viewMatrix = GetViewMatrix();

            // Get the X Axis (right direction) from the player matrix
            return Vector3.Normalize(viewMatrix.Column0.Xyz);
        }

        private void UpdateBoundingBox()
        {

            Vector3 playerMin = new Vector3(
               _position.X - PlayerHalfWidth,
               _position.Y,
               _position.Z - PlayerHalfDepth
            );
            Vector3 playerMax = new Vector3(
                _position.X + PlayerHalfWidth,
                _position.Y + PlayerHeight,
                _position.Z + PlayerHalfDepth
            );

            playerAabb = new Box3(playerMin, playerMax);
        }

        public Box3 GetBoundingBox()
        {
            return playerAabb;
        }
        private void GetTransformationMatrix(out Matrix4 matrix)
        {
            float minPitch = -80.0f;  // Prevent looking too far up
            float maxPitch = 80.0f;   // Prevent looking too far down
            float minYaw = -170.0f;   // Optional: Limit the yaw to a certain range
            float maxYaw = 170.0f;    // Optional: You can leave yaw unrestricted if you want
            Vector2 lookDir = GetLookDir();

            // Clamp the pitch and yaw
            float clampedPitch = Math.Clamp(lookDir.X, minPitch, maxPitch); // Pitch (up/down)
            float clampedYaw = Math.Clamp(lookDir.Y, minYaw, maxYaw);       // Yaw (left/right)

            // Create rotation matrices
            Quaternion pitchRotation = Quaternion.FromAxisAngle(Vector3.UnitX, MathHelper.DegreesToRadians(clampedPitch));
            Quaternion yawRotation = Quaternion.FromAxisAngle(Vector3.UnitY, MathHelper.DegreesToRadians(clampedYaw));
            Quaternion localRotation = yawRotation * pitchRotation;

            Matrix3.CreateFromQuaternion(localRotation, out Matrix3 rotation);

            matrix.Row0 = new Vector4(rotation.Row0, 0);
            matrix.Row1 = new Vector4(rotation.Row1, 0);
            matrix.Row2 = new Vector4(rotation.Row2, 0);
            matrix.Row3 = new Vector4(GetPosition(), 1);
        }
        
        /**
         * Instantiates the players view matrix witch is later
         * multiplied by the projection and model matrix.
         *
         * @return The players view matrix
         */
        public Matrix4 GetViewMatrix()
        {
            if (!Window.IsMainMenuScreenDisplayed())
            {
                // Starting with a forward direction of -Z axis in local space
                Vector3 forward = new Vector3(0, 0, -1);

                // Get look direction in radians
                Vector2 lookDir = GetLookDir();
                float yaw = MathHelper.DegreesToRadians(lookDir.Y); // Pitch (up/down)
                float pitch = -MathHelper.DegreesToRadians(lookDir.X);   // Yaw (left/right)


                // Apply yaw around Y-axis and pitch around X-axis
                Quaternion yawRotation = Quaternion.FromAxisAngle(Vector3.UnitY, yaw);     
                Quaternion pitchRotation = Quaternion.FromAxisAngle(Vector3.UnitX, pitch);

                // Combine rotations
                Quaternion rotation = yawRotation * pitchRotation;
                Vector3 direction = Vector3.Transform(forward, rotation);

                // Position the camera at eye level
                Vector3 eyePosition = _position + new Vector3(0f, PlayerHeight, 0f);

                // Calculate the look point from the eye position
                Vector3 lookPoint = eyePosition + direction;

                // Create the view matrix
                return Matrix4.LookAt(eyePosition, lookPoint, Vector3.UnitY);
            }
            else
            {
                GetTransformationMatrix(out Matrix4 output);
                output.Invert();
                return output;
            }
        }

        public Vector3 GetBlockedDirection()
        { 
            return blockedDirection; 
        }

        public override string ToString()
        {
            return "Player at position (" + _position.X + ", " + _position.Y + ", " + _position.Z + ")";
        }

        /// <summary>
        /// Applies the specified displacement to the player one axis at a time,
        /// resolving horizontal collisions before vertical collisions.
        /// Updates the player's bounding box after movement is complete.
        /// </summary>
        /// <param name="dx">The displacement along the X-axis.</param>
        /// <param name="dy">The displacement along the Y-axis.</param>
        /// <param name="dz">The displacement along the Z-axis.</param>
        private void MoveAndCollide(float dx, float dy, float dz)
        {
            // Resolve horizontal axes independently so corners don't
            // interfere with vertical landing detection.
            MoveAndCollideX(dx);
            MoveAndCollideZ(dz);
            MoveVertical(dy);

            UpdateBoundingBox();
        }

        private bool IsSolidBlock(int x, int y, int z)
        {
            BlockType type = _regionManager!.GetBlockTypeAtCoords(x, y, z);
            return type != BlockType.AIR;
        }

        /// <summary>
        /// Moves the player along the X-axis and resolves block collisions by correcting
        /// the player's position and stopping horizontal velocity when a collision occurs.
        /// </summary>
        /// <param name="dx">The horizontal displacement along the X-axis.</param>
        private void MoveAndCollideX(float dx)
        {
            if (MathF.Abs(dx) < CollisionEpsilon)
                return;

            _position.X += dx;
            UpdateBoundingBox();

            const int maxIterations = 8;

            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                bool foundCollision = false;

                foreach (Vector3 block in GetPlayerCollisionCandidates())
                {
                    Box3 blockBox = new(block, block + Vector3.One);

                    bool overlapY =
                        playerAabb.Max.Y > blockBox.Min.Y + CollisionEpsilon &&
                        playerAabb.Min.Y < blockBox.Max.Y - CollisionEpsilon;

                    bool overlapZ =
                        playerAabb.Max.Z > blockBox.Min.Z + CollisionEpsilon &&
                        playerAabb.Min.Z < blockBox.Max.Z - CollisionEpsilon;

                    bool overlapX =
                        playerAabb.Max.X > blockBox.Min.X &&
                        playerAabb.Min.X < blockBox.Max.X;

                    if (!overlapX || !overlapY || !overlapZ)
                        continue;

                    if (dx > 0f)
                    {
                        _position.X = MathF.Min(
                            _position.X,
                            blockBox.Min.X - PlayerHalfWidth - CollisionSkin);
                    }
                    else
                    {
                        _position.X = MathF.Max(
                            _position.X,
                            blockBox.Max.X + PlayerHalfWidth + CollisionSkin);
                    }

                    velocity.X = 0f;
                    UpdateBoundingBox();

                    foundCollision = true;
                    break;
                }

                if (!foundCollision)
                    break;
            }
        }

        /// <summary>
        /// Moves the player along the Z-axis and resolves block collisions by correcting
        /// the player's position and stopping horizontal velocity when a collision occurs.
        /// </summary>
        /// <param name="dz">The horizontal displacement along the X-axis.</param>
        private void MoveAndCollideZ(float dz)
        {
            if (MathF.Abs(dz) < CollisionEpsilon)
                return;

            _position.Z += dz;
            UpdateBoundingBox();

            const int maxIterations = 8;

            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                bool foundCollision = false;

                foreach (Vector3 block in GetPlayerCollisionCandidates())
                {
                    Box3 blockBox = new(block, block + Vector3.One);

                    bool overlapY =
                        playerAabb.Max.Y > blockBox.Min.Y + CollisionEpsilon &&
                        playerAabb.Min.Y < blockBox.Max.Y - CollisionEpsilon;

                    bool overlapX =
                        playerAabb.Max.X > blockBox.Min.X + CollisionEpsilon &&
                        playerAabb.Min.X < blockBox.Max.X - CollisionEpsilon;

                    bool overlapZ =
                        playerAabb.Max.Z > blockBox.Min.Z &&
                        playerAabb.Min.Z < blockBox.Max.Z;

                    if (!overlapX || !overlapY || !overlapZ)
                        continue;

                    if (dz > 0f)
                    {
                        _position.Z = MathF.Min(
                            _position.Z,
                            blockBox.Min.Z - PlayerHalfDepth - CollisionSkin);
                    }
                    else
                    {
                        _position.Z = MathF.Max(
                            _position.Z,
                            blockBox.Max.Z + PlayerHalfDepth + CollisionSkin);
                    }

                    velocity.Z = 0f;
                    UpdateBoundingBox();

                    foundCollision = true;
                    break;
                }

                if (!foundCollision)
                    break;
            }
        }
        
        /// <summary>
        /// Moves the player vertically and resolves collisions with block floors and ceilings,
        /// stopping vertical velocity upon landing or hitting an overhead block.
        /// </summary>
        /// <param name="dy">The vertical displacement.</param>
        private void MoveVertical(float dy)
        {
            
            if (MathF.Abs(dy) < CollisionEpsilon)
                return;

            float oldBottom = _position.Y;
            float oldTop = oldBottom + PlayerHeight;
            float newBottom = oldBottom + dy;
            float newTop = newBottom + PlayerHeight;

            // Apply vertical movement, then test the swept vertical interval.
            _position.Y = newBottom;
            UpdateBoundingBox();

            int minX = (int)MathF.Floor(playerAabb.Min.X);
            int maxX = (int)MathF.Floor(playerAabb.Max.X - CollisionEpsilon);
            int minZ = (int)MathF.Floor(playerAabb.Min.Z);
            int maxZ = (int)MathF.Floor(playerAabb.Max.Z - CollisionEpsilon);

            int minY = (int)MathF.Floor(
                MathF.Min(oldBottom, newBottom));

            int maxY = (int)MathF.Floor(
                MathF.Max(oldTop, newTop) - CollisionEpsilon);

            if (dy < 0)
            {
                // Falling: find the highest block top crossed by the player's feet.
                float landingY = float.NegativeInfinity;

                for (int x = minX; x <= maxX; x++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        for (int y = minY; y <= maxY; y++)
                        {
                            if (!IsSolidBlock(x, y, z))
                                continue;

                            float blockTop = y + 1f;

                            bool crossedTop =
                                oldBottom >= blockTop - CollisionEpsilon &&
                                newBottom <= blockTop;

                            if (crossedTop && blockTop > landingY)
                                landingY = blockTop;
                        }
                    }
                }

                if (!float.IsNegativeInfinity(landingY))
                {
                    
                    _position.Y = landingY + CollisionSkin;
                    velocity.Y = 0f;

                    UpdateBoundingBox();

                }
            }
            else
            {
                // Jumping: stop when the player's head reaches a block underside.
                float ceilingY = float.PositiveInfinity;

                for (int x = minX; x <= maxX; x++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        for (int y = minY; y <= maxY; y++)
                        {
                            if (!IsSolidBlock(x, y, z))
                                continue;
                            
                            
                            float blockBottom = y;

                            bool crossedBottom =
                                oldTop <= blockBottom + CollisionEpsilon &&
                                newTop >= blockBottom;

                            if (crossedBottom && blockBottom < ceilingY)
                                ceilingY = blockBottom;
                        }
                    }
                }

                if (!float.IsPositiveInfinity(ceilingY))
                {
                    _position.Y = ceilingY - PlayerHeight - CollisionSkin;
                    velocity.Y = 0f;
                }
            }

            UpdateBoundingBox();
        }
    }
}
