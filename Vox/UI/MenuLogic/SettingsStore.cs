using System.Drawing.Printing;
using System.Numerics;
using System.Text.Json.Nodes;
using Newtonsoft.Json;
using Vox.Model;

namespace Vox.UI.MenuLogic
{
    public class SettingsStore : ISettingsStore
    {
        private readonly string _appFolder;
        private Settings? _settings;
        public SettingsStore(string appFolder) 
        {

            _appFolder = appFolder;
            TryLoadSettingsFromFile();
            FillBuffersFromSettings();
        }

        /// <summary>
        /// Trys to load settings from a file, if it fails it creates 
        /// a new settings object with default values.
        /// </summary>
        public void TryLoadSettingsFromFile()
        {
            string settingsFilePath = Path.Combine(_appFolder, "settings.json");

            if (File.Exists(settingsFilePath))
            {
                string json = File.ReadAllText(settingsFilePath);
                _settings = JsonConvert.DeserializeObject<Settings>(json);
            }
            else
            {
                _settings = new Settings();
            }
        }
        public void FillBuffersFromSettings()
        {
            _settings!.GuiScaleBuffer = _settings!.GuiScale;
            _settings.UIColorBuffer = _settings!.UIColor;
        }

        /// <summary>
        /// Saves the current settings to a file in JSON format. 
        /// If the file already exists, it will be overwritten. 
        /// </summary>
        public void SaveSettingsToFile()
        {
            string settingsFilePath = Path.Combine(_appFolder, "settings.json");
            string json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
            File.WriteAllText(settingsFilePath, json);
        }
        public void SetGuiScale(float guiScale)
        {
            _settings!.GuiScaleBuffer = guiScale;
            _settings!.GuiScale = guiScale;
        }
        public void SetUIColor(Vector4 UIColor)
        {
            _settings!.UIColorBuffer = UIColor;
            _settings!.UIColor = UIColor;
        }
        public Settings GetSettings()
        {
            return _settings!;
        }
    }
}