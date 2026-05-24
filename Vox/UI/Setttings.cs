namespace Vox.UI
{
    public class Settings : ISettings
    {
        private float _guiScale;
        public Settings() { }


        public void SetGuiScale(float guiScale)
        {
            _guiScale = guiScale;
        }
        public float GetGuiScale()
        {
            return _guiScale;
        }
    }
}