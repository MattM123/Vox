
using System.Diagnostics.CodeAnalysis;

namespace Vox.Model
{
    public struct ColorVector(int red, int green, int blue)
    {
        public int Red { get; } = red;
        public int Green { get; } = green;
        public int Blue { get; } = blue;

        public override string ToString()
        {
            return $"({Red}, {Green}, {Blue})";
        }

        public readonly override bool Equals([NotNullWhen(true)] object? obj)
        {
            if (obj is ColorVector other)
                return Red == other.Red && Green == other.Green && Blue == other.Blue;
            else
                return false;
        }

        public int Sum()
        {
            return Red + Green + Blue;
        }
        public readonly override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
