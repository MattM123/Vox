
using System.Numerics;

namespace Vox.Model
{
    public class Settings
    {
        public float GuiScale { get; set; } = 100f;

        public float GuiScaleBuffer { get; set; } = 0f;

        public Vector4 UIColor { get; set; } = new(0.5f, 0.4f, 0.5f, 0.6f);
        public Vector4 UIColorBuffer { get; set; } 
    }
}
