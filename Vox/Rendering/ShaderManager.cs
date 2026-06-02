using OpenTK.Graphics.OpenGL4;
using Vox.Exceptions;
using Vox.UI;

namespace Vox.Rendering
{
    public class ShaderManager : IShaderManager
    {
        private readonly Dictionary<string, ShaderProgram> _shaders = [];

        
        public ShaderManager()
        {
        }

        public ShaderProgram GetShaderProgram(string name)
        {
            if (!_shaders.TryGetValue(name, out var shader))
            {
                throw new ShaderException($"Shader '{name}' not found. Ensure it was registered via AddShaderProgram.");
            }
            return shader;
        }

        public ShaderProgram AddShaderProgram(string name, ShaderProgram shader)
        {
            ImGuiController.LabelObject(ObjectLabelIdentifier.Program, shader.GetProgramId(), $"Program: {name}");

            if (_shaders.TryAdd(name, shader))
            {
                return shader;
            }

            throw new ShaderException($"Shader with the same name already exists: {name}");
        }

        public void CleanupShaders()
        {
            foreach (var shader in _shaders.Values)
            {
                shader.Cleanup();
            }
            _shaders.Clear();
        }
    }
}
