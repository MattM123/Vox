using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL4;

namespace Vox.Rendering
{
    public class SSBOManager
    {
        private static Dictionary<string, StorageBufferObject> ssboList = [];

        public SSBOManager() { }
        public StorageBufferObject AddSSBO(int size, int bindingIndex, string name)
        {
            //Override existing SSBO
            if (ssboList.TryGetValue(name, out StorageBufferObject? value))
            {
                ssboList[name] = new StorageBufferObject(size, bindingIndex, value.Handle);
                return ssboList[name];
            }
            // Else create new SSBO
            else
            {
                ssboList.Add(name, new StorageBufferObject(size, bindingIndex, GL.GenBuffer()));
                return ssboList[name];
            }
        }
        public StorageBufferObject GetSSBO(string name)
        {
            return ssboList[name];
        }
    }

    public class StorageBufferObject
    {
        public readonly IntPtr Pointer;
        public readonly int Size;
        public readonly int Handle;
        public readonly int BindingIndex;

        public StorageBufferObject(int sizeInBytes, int bindingIndex, int ssboHandle)
        {
            Size = sizeInBytes;
            Handle = ssboHandle;
            BindingIndex = bindingIndex;
            Handle = ssboHandle;

            //Bind buffer handle
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssboHandle);

            //Creates ssbo buffer
            GL.BufferStorage(BufferTarget.ShaderStorageBuffer, sizeInBytes, IntPtr.Zero, BufferStorageFlags.MapPersistentBit | BufferStorageFlags.MapCoherentBit | BufferStorageFlags.MapWriteBit);

            //Map binding for shader to use
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, bindingIndex, ssboHandle);

            //Creates pointer to SSBO buffer
            Pointer = GL.MapBufferRange(
                BufferTarget.ShaderStorageBuffer,
                IntPtr.Zero,
                sizeInBytes,
                BufferAccessMask.MapWriteBit |
                BufferAccessMask.MapPersistentBit |
                BufferAccessMask.MapCoherentBit
            );
        }
    }
}
