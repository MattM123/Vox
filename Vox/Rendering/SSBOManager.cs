
using MessagePack.Resolvers;
using OpenTK.Graphics.OpenGL4;
using Vox.Enums;
using Vox.Exceptions;
using Vox.Model;

namespace Vox.Rendering
{
    public class SSBOManager : ISSBOManager
    {
        private readonly Dictionary<string, StorageBufferObject> _ssboList = new();
        public SSBOManager() { }

        public StorageBufferObject AddSSBO(int size, int bindingIndex, SSBO type)
        {
            string name = type.ToString();

            // Override existing SSBO
            if (_ssboList.TryGetValue(name, out StorageBufferObject? value))
            {
                Logger.Debug("Updating existing SSBO: " + type);
                _ssboList[name] = new StorageBufferObject(size, bindingIndex, value.Handle);
                GL.ObjectLabel(ObjectLabelIdentifier.Buffer, _ssboList[name].Handle, name.Length, "SSBO: " + name);
                return _ssboList[name];
            }
            Logger.Debug($"Creating new SSBO: {name}");
            // Else create new SSBO
            var newBuffer = new StorageBufferObject(size, bindingIndex, GL.GenBuffer());
            _ssboList.Add(name, newBuffer);
            GL.ObjectLabel(ObjectLabelIdentifier.Buffer, newBuffer.Handle, name.Length, "SSBO: " + name);
            return newBuffer;
        }

        public StorageBufferObject GetSSBO(SSBO type)
        {
            if (!_ssboList.TryGetValue(type.ToString(), out StorageBufferObject? ssbo))
            {
                throw new ShaderException($"SSBO '{type}' not found. Did you add it?");
            }
            return ssbo;
        }
    }
}