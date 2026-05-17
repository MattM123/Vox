using Vox.Enums;
using Vox.Model;

namespace Vox.Rendering
{
    public interface ISSBOManager
    {
        StorageBufferObject AddSSBO(int size, int bindingIndex, SSBO type);
        StorageBufferObject GetSSBO(SSBO type);
    }
}