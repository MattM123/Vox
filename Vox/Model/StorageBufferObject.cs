using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;

namespace Vox.Model
{
    public class StorageBufferObject
    {
        public readonly IntPtr Pointer;
        public readonly int Size;
        public readonly int Handle;
        public readonly int BindingIndex;
        private readonly List<int> freeIndices = [];
        private int nextIndex = 0;
        public int FreeIndexCount => freeIndices.Count;
        private readonly object _lock = new();

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

        /// <summary>
        ///  Gets the next free index or allocates a new one for use. 
        ///  If a free index exists, removes it from the free 
        ///  indicies list and returns it so it can be reused, 
        ///  else allocates a new index and returns it.
        /// </summary>
        /// <returns>The SSBO index</returns>
        public int AllocateIndex()
        {
            lock (_lock)
            {
                int max = Size / Marshal.SizeOf<BlockFaceInstance>();

                // Get and reuse first free index if one exists,
                // else allocate a new index
                if (freeIndices.Count > 0)
                {
                    int last = freeIndices[^1];
                    freeIndices.RemoveAt(freeIndices.Count - 1);
                    return last;
                }

                if (nextIndex >= max)
                    throw new InvalidOperationException($"Max: {max} CRASH INDEX: {nextIndex}");

                return nextIndex++;
            }
        }
        //Never gets called
        public void FreeIndex(int index)
        {
            freeIndices.Add(index);
        }
    }
}