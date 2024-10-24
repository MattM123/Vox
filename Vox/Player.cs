
using System.Data;
using System.Drawing;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Vox.Genesis;
using Vox.Texturing;

namespace Vox
{
    public class Player 
    {
        public static Vector3 position = Vector3.Zero;
        private static float yaw = 0f;
        private static float pitch = 0f;
        private static float halfWidth = 0.5f;
        private static float halfDepth = 0.5f;
        private static float playerHeight = 2.0f;
        public static Vector3 prevPos = Vector3.Zero;
        private static Vector3 playerMin; 
        private static Vector3 playerMax;
        public static Vector3 velocity = Vector3.Zero;
        public readonly float moveSpeed = 15.0f;
        private Vector3 desiredMovement = Vector3.Zero;
        private float forward = 0f;
        private float right = 0f;
        /**
         * Default player object is initialized at a position of 0,0,0 within
         * Region 0,0.
         */
        public Player()
        {
            
            ChunkCache.SetPlayerChunk(GetChunkWithPlayer());

            position = new(0, GetChunkWithPlayer().GetHeightmap()[0,0] + 10, 0);
            prevPos = position;
            playerMin = new Vector3(position.X - halfWidth, position.Y, position.Z - halfDepth);
            playerMax = new Vector3(position.X + halfWidth, position.Y + playerHeight, position.Z + halfDepth);
        }

        public Vector2 GetForwardRight()
        {
            return new Vector2(forward, right);
        }
        //Does not work
        private void CheckChunkCollision(float deltaTime)
        {
            Logger.Debug(GetChunkWithPlayer().GetLocation());
            float[] verts = GetChunkWithPlayer().GetRenderTask().GetVertexData();
            List<Vector3> vectors = [];

            for (int i = 0; i < verts.Length; i += 8)
            {
                vectors.Add(new(verts[i], verts[i + 1], verts[i + 2]));
            }


            foreach (Vector3 vertex in vectors)
            {
                // Get bounding box of voxel
                Vector3 voxelMin = vertex;
                Vector3 voxelMax = vertex + new Vector3(1, 1, 1);
                Vector3 collisionNormal = HandleCollision(playerMin, playerMax, voxelMin, voxelMax);

                // If no collision, Vector3.Zero is returned
                if(collisionNormal != Vector3.Zero)
                {
                    Logger.Debug("Collision with " + vertex);
                    float dotProduct = Vector3.Dot(velocity, collisionNormal);

                    Vector3 slideVelocity = velocity - (dotProduct * collisionNormal);

                    // Move the player based on sliding velocity and deltaTime
                    position += slideVelocity;

                    // Update velocity for the next frame, also accounting for deltaTime if needed
                    velocity = slideVelocity;

                    ApplyFriction(deltaTime);
                }
            }
        }

        public void Update(float deltaTime)
        {
            // Update velocity based on desired movement
            if (desiredMovement.LengthSquared > 0 && desiredMovement != Vector3.Zero)
                velocity = desiredMovement.Normalized() * moveSpeed;

            desiredMovement = Vector3.Zero;

            // Update position based on current velocity
            position += velocity * deltaTime;

            // Calculate new velocity based on position change
            UpdateVelocity(deltaTime);

            // Apply friction or damping to velocity
            ApplyFriction(deltaTime);

            // Update the player's state in the game world, like checking for collisions
            CheckChunkCollision(deltaTime);


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
        private void UpdateVelocity(float deltaTime)
        {
            if (deltaTime > 0 && position != prevPos)
            {
                velocity = (position - prevPos) / deltaTime;
                prevPos = position;
            } else
            {
                velocity = Vector3.Zero;
            }
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

            // If there's no overlap on any axis, return zero vector
            if (overlapX < 0 || overlapY < 0 || overlapZ < 0)
                return Vector3.Zero;

            // Determine which axis has the smallest overlap
            if (overlapX < overlapY && overlapX < overlapZ)
            {
                // Return the collision normal on the X-axis
                return new Vector3(overlapX > 0 ? 1 : -1, 0, 0);
            }
            else if (overlapY < overlapX && overlapY < overlapZ)
            {
                // Return the collision normal on the Y-axis
                return new Vector3(0, overlapY > 0 ? 1 : -1, 0);
            }
            else
            {
                // Return the collision normal on the Z-axis
                return new Vector3(0, 0, overlapZ > 0 ? 1 : -1);
            }
        }
        public Vector3 GetPosition()
        {
            return position;
        }

        public Vector2 GetRotation()
        {
            return new Vector2(yaw, pitch);
        }


        public void MoveForward(float inc)
        {
            forward += inc;
            desiredMovement += GetForwardDirection() * inc;
        }


        public void MoveRight(float inc)
        {
            right += inc;
            desiredMovement += GetRightDirection() * inc;
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


        public override string ToString()
        {
            return "Player at position (" + position.X + ", " + position.Y + ", " + position.Z + ")";
        }
    }

}
