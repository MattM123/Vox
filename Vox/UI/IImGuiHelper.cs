using ImGuiNET;

namespace Vox.UI
{
    internal interface IImGuiHelper
    {
        void ShowWorldMenu(ImGuiIOPtr ioptr);
        void ShowDebugMenu(ImGuiIOPtr ioptr);
        void CreateBlockColorPicker(OpenTK.Mathematics.Vector3 blockspace);
        void CreatePlayerInventory(ImGuiController controller);
        bool IsAnyMenuActive();
        bool ShowBlockColorPicker();
        bool ShowPlayerInventory();
        void SetShowPlayerInventory(bool show);
    }
}
