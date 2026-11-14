using System.Numerics;
using Vox.Model;

namespace Vox.UI.MenuLogic
{
    public interface ISettingsStore
    {
        void SetGuiScale(float guiScale);
        void SetUiColor(Vector4 uiColor);
        void TryLoadSettingsFromFile();
        void SaveSettingsToFile();
        void FillBuffersFromSettings();
        Settings GetSettings();
    }
}