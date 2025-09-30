
namespace Vox.Rendering
{
    public struct ColorVector(int red, int green, int blue)
    {
        public int Red { get; set; } = red;
        public int Green { get; set; } = green;
        public int Blue { get; set; } = blue;

        public override string ToString()
        {
            return $"[{Red}, {Green}, {Blue}]";
        }

    }
}
