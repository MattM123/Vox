
namespace Vox.Texturing
{

    public class TextureCoordinates(float[] BOTTOM_LEFT, float[] BOTTOM_RIGHT,
                                  float[] TOP_LEFT, float[] TOP_RIGHT)
    {
        private readonly float[] BOTTOM_LEFT = BOTTOM_LEFT;
        private readonly float[] BOTTOM_RIGHT = BOTTOM_RIGHT;
        private readonly float[] TOP_LEFT = TOP_LEFT;
        private readonly float[] TOP_RIGHT = TOP_RIGHT;

        public float[] GetBottomLeft()
        {
            return BOTTOM_LEFT;
        }

        public float[] GetBottomRight()
        {
            return BOTTOM_RIGHT;
        }

        public float[] GetTopLeft()
        {
            return TOP_LEFT;
        }

        public float[] GetTopRight()
        {
            return TOP_RIGHT;
        }
    }

}
