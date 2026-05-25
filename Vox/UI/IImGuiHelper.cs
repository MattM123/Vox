using ImGuiNET;

namespace Vox.UI
{
    public interface IImGuiHelper
    {
        void CreateDebugMenu();
        void CreateBlockColorPicker(OpenTK.Mathematics.Vector3 blockspace);
        void CreatePlayerInventory();
        void CreateMainMenu();
    }
}
