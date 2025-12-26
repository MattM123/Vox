using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vox.Exceptions;

namespace Vox.Rendering
{
    public class ShaderManager
    {
        private readonly Dictionary<string, ShaderProgram> shaders = [];

        public ShaderManager()
        {
        }

        public ShaderProgram GetShaderProgram(string name)
        {
            if (shaders.TryGetValue(name, out var shader))
            {
                return shader;
            }
            shader = shaders[name];
            shaders[name] = shader;
            return shader;
        }

        public ShaderProgram AddShaderProgram(string name, ShaderProgram shader)
        {
            if (!shaders.ContainsKey(name))
            {
                shaders[name] = shader;
                return shader;
            }
            else
                throw new ShaderException("Shader with the same name already exists: " + name);    
        }

        public void CleanupShaders()
        {
            foreach (var shader in shaders.Values)
            {
                shader.Cleanup();
            }
           // shaders.Clear();
        }
    }
}
