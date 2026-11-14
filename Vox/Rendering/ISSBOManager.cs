using Vox.Enums;
using Vox.Model;

namespace Vox.Rendering
{
    public interface IssboManager
    {
        StorageBufferObject AddSsbo(int size, int bindingIndex, Ssbo type);
        StorageBufferObject GetSsbo(Ssbo type);
    }
}