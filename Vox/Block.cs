
using OpenTK.Mathematics;
using Vox.Model;

namespace Vox
{


    public class Block
    {
        private BlockType type;
        private Vector3 location;


        /**
         * Constructs a new Block with a specified BlockType
         * @param x coordinate of Block
         * @param y coordinate of Block
         * @param z coordinate of Block
         * @param b BlockType of Block
         */
        public Block(float x, float y, float z, BlockType b)
        {
            this.location = new Vector3(x, y, z);
            type = b;
        }

        /**
         * Constructs a new Block who's BlockType is specified
         * by a default value defined in a chunks initialization
         * method
         * @param x coordinate of Block
         * @param y coordinate of Block
         * @param z coordinate of Block
         */
        public Block(float x, float y, float z)
        {
            this.location = new Vector3(x, y, z);
        }

        public Block(Vector3 location, BlockType b)
        {
            this.location = location;
            this.type = b;
        }

        public BlockType GetBlockType()
        {
            return type;
        }

        public void SetBlockType(BlockType type)
        {
            this.type = type;
        }

        public Vector3 GetLocation()
        {
            return location;
        }
        public void SetLocation(Vector3 location)
        {
            this.location = location;
        }

        /*
        @Serial
        private void writeObject(ObjectOutputStream out) throws IOException {
            out.writeFloat(location.X);
            out.writeFloat(location.Y);
            out.writeFloat(location.Z);
            out.writeObject(GetBlockType());
        }

        @Serial
        private void readObject(ObjectInputStream in) throws IOException, ClassNotFoundException {
            this.location.X = in.readFloat();
            this.location.Y = in.readFloat();
            this.location.Z = in.readFloat();
            type = (BlockType) in.readObject();
        }
        */

        public override bool Equals(object? o)
        {
            if (o.GetType() == typeof(Block))
            {
                return GetLocation().X == ((Block)o).GetLocation().X && GetLocation().Y == ((Block)o).GetLocation().Y
                        && GetLocation().Z == ((Block)o).GetLocation().Z;// && this.GetBlockType() == ((Block) o).GetBlockType();
            }
            else return false;
        }

        public override string ToString()
        {
            return "[" + location.X + ", " + location.Y + ", " + location.Z + "]";

        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

    }
}