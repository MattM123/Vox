
using System.Drawing;
using OpenTK.Mathematics;
using Vox.Genesis;

namespace Vox
{
    public class Player 
    {
        private static Vector3 position;
        private static float yaw = 0f;
        private static float pitch = 0f;
        // public static int REACH_DISTANCE = 4;

        /**
         * Default player object is initialized at a position of 0,0,0 within
         * Region 0,0.
         */
        public Player()
        {
            position = new Vector3(0, 200, 0);
            ChunkCache.SetPlayerChunk(GetChunkWithPlayer());
            RegionManager.EnterRegion(GetRegionWithPlayer());
        }

        public void SetPosition(Vector3 pos)
        {
            position = pos;
        }
        public Vector3 GetPosition()
        {
            return position;
        }

        public Vector2 GetRotation()
        {
            return new Vector2(yaw, pitch);
        }

        public void MoveBackwards(float inc)
        {
            position.Z += inc;
        }

        public void MoveDown(float inc)
        {
            position.Y -= inc;
        }

        public void MoveForward(float inc)
        {
            position.Z -= inc;
        }

        public void MoveLeft(float inc)
        {
            position.X -= inc;
        }

        public void MoveRight(float inc)
        {
            position.X += inc;
        }

        public void MoveUp(float inc)
        {
            position.Y += inc;
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

            returnRegion ??= new Genesis.Region(regionXCoord, regionZCoord);

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
            int x = (int)GetPosition().X;
            int xLowerLimit = ((x / RegionManager.CHUNK_BOUNDS) * RegionManager.CHUNK_BOUNDS);
            int xUpperLimit;
            if (x < 0)
                xUpperLimit = xLowerLimit - RegionManager.CHUNK_BOUNDS;
            else
                xUpperLimit = xLowerLimit + RegionManager.CHUNK_BOUNDS;


            int z = (int)GetPosition().Z;
            int zLowerLimit = ((z / RegionManager.CHUNK_BOUNDS) * RegionManager.CHUNK_BOUNDS);
            int zUpperLimit;
            if (z < 0)
                zUpperLimit = zLowerLimit - RegionManager.CHUNK_BOUNDS;
            else
                zUpperLimit = zLowerLimit + RegionManager.CHUNK_BOUNDS;


            //Calculates chunk coordinates player inhabits
            int chunkXCoord = xUpperLimit;
            int chunkZCoord = zUpperLimit;


            Genesis.Region r = GetRegionWithPlayer();
            Chunk c = r.GetChunkWithLocation(new Vector3(chunkXCoord, 0, chunkZCoord));

            if (c == null)
            {
                Chunk d = new Chunk().Initialize(chunkXCoord, chunkZCoord);
                r.Add(d);
                return d;
            }
            return c;
        }

        public void SetLookDir(float x, float y)
        {
            yaw = x;
            pitch = y;
        }

        public Vector2 GetLookDir()
        {
            return new Vector2(yaw, pitch);
        }

        private void GetTransformationMatrix(out Matrix4 matrix)
        {
            // Create rotation matrices
            Quaternion pitchRotation = Quaternion.FromAxisAngle(Vector3.UnitY, MathHelper.DegreesToRadians(GetLookDir().Y));
            Quaternion yawRotation = Quaternion.FromAxisAngle(Vector3.UnitX, MathHelper.DegreesToRadians(GetLookDir().X));
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
                Vector3 lookPoint = new(0f, 0f, -1f);
                //  Vector3 lookPoint = new Vector3(getPosition().X, 0f, getPosition().Z);

                // Create rotation matrices
                Quaternion pitchRotation = Quaternion.FromAxisAngle(Vector3.UnitY, MathHelper.DegreesToRadians(GetLookDir().Y));
                Quaternion yawRotation = Quaternion.FromAxisAngle(Vector3.UnitX, MathHelper.DegreesToRadians(GetLookDir().X));
                lookPoint += position;


                lookPoint = Vector3.Transform(lookPoint, pitchRotation * yawRotation);
                return Matrix4.LookAt(position, lookPoint, new Vector3(0, 1, 0));

            }
            else
            {
                Vector3 cameraUp = Vector3.UnitY;
                Vector3 forward = new(0, 0, -1);  // Z-axis (camera looks down negative Z)
                Vector3 right = Vector3.Normalize(Vector3.Cross(cameraUp, forward)); // X-axis
                Vector3 up = Vector3.Cross(forward, right);                          // Y-axis

                Vector3 lookPoint = new(0f, 0f, -1f);
                // Create rotation matrices
                Quaternion pitchRotation = Quaternion.FromAxisAngle(Vector3.UnitY, MathHelper.DegreesToRadians(GetLookDir().Y));
                Quaternion yawRotation = Quaternion.FromAxisAngle(Vector3.UnitX, MathHelper.DegreesToRadians(GetLookDir().X));
                lookPoint += position;

                lookPoint = Vector3.Transform(lookPoint, pitchRotation * yawRotation);
                //return Matrix4.LookAt(position, lookPoint, new Vector3(0, 1, 0));
                Matrix4 output = new();
                GetTransformationMatrix(out output);
                output.Invert();
                return output;
            }
        }



        public override string ToString()
        {
            return "Player at position (" + position.X + ", " + position.Y + ", " + position.Z + ")";
        }
    }

}
