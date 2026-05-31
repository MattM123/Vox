using System.Numerics;
using Vox.Model;

namespace Vox.UI.MenuLogic
{
    public interface ISettingsStore
    {
        void SetGuiScale(float guiScale);
        void SetUIColor(Vector4 UIColor);
        void TryLoadSettingsFromFile();
        void SaveSettingsToFile();
        void FillBuffersFromSettings();
        Settings GetSettings();
    }
}