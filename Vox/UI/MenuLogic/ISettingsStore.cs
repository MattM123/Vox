using Vox.Model;

namespace Vox.UI.MenuLogic
{
    public interface ISettingsStore
    {
        void SetGuiScale(float guiScale);
        void TryLoadSettingsFromFile();
        void SaveSettingsToFile();
        void FillBuffersFromSettings();
        Settings GetSettings();
    }
}