
using OpenTK.Graphics.OpenGL;
using Vox.Enums;
using Vox.Exceptions;
using Vox.Model;

namespace Vox.Rendering
{
    public class SsboManager : IssboManager
    {
        private readonly Dictionary<string, StorageBufferObject> _ssboList = new();
        public SsboManager() { }

        public StorageBufferObject AddSsbo(int size, uint bindingIndex, Ssbo type)
        {
            string name = type.ToString();

            // Override existing SSBO
            if (_ssboList.TryGetValue(name, out StorageBufferObject? value))
            {
                _ssboList[name] = new StorageBufferObject(size, bindingIndex, value.Handle);
                GL.ObjectLabel(ObjectIdentifier.Buffer, _ssboList[name].Handle, name.Length, "SSBO: " + name);
                return _ssboList[name];
            }

            // Else create new SSBO
            var newBuffer = new StorageBufferObject(size, bindingIndex, GL.GenBuffer());
            _ssboList.Add(name, newBuffer);
            GL.ObjectLabel(ObjectIdentifier.Buffer, newBuffer.Handle, name.Length, "SSBO: " + name);
            return newBuffer;
        }

        public StorageBufferObject GetSsbo(Ssbo type)
        {
            if (!_ssboList.TryGetValue(type.ToString(), out StorageBufferObject? ssbo))
            {
                throw new ShaderException($"SSBO '{type}' not found. Did you add it?");
            }
            return ssbo;
        }
    }
}