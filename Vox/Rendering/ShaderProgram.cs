
using System.Xml.Linq;
using OpenTK.Compute.OpenCL;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Vox.Exceptions;
using Vox.UI;
using TextureTarget = OpenTK.Graphics.OpenGL.TextureTarget;
namespace Vox.Rendering
{
    public class ShaderProgram
    {
        private int _programId;
        private int _vertexShaderId;
        private int _fragmentShaderId;
        private int _geometryShaderId;
        private Dictionary<string, int> _uniforms;
        private Dictionary<int, string> _shaderIdentity = [];

        public ShaderProgram()
        {
            GenerateProgramId();
            _uniforms = [];
        }

        public void GenerateProgramId()
        {
            _programId = GL.CreateProgram();
        }
        public int GetProgramId()
        {
            return _programId;
        }
        public ShaderProgram CreateVertexShader(string filename, string vertexShaderCode)
        {
            _vertexShaderId = CreateShader(filename, vertexShaderCode, ShaderType.VertexShader);
            ImGuiController.LabelObject(ObjectIdentifier.Shader, _vertexShaderId, $"Shader: {filename}");
            return this;
        }

        public ShaderProgram CreateFragmentShader(string filename, string fragmentShaderCode)
        {
            _fragmentShaderId = CreateShader(filename, fragmentShaderCode, ShaderType.FragmentShader);
            ImGuiController.LabelObject(ObjectIdentifier.Shader, _fragmentShaderId, $"Shader: {filename}");
            return this;
        }

        public void CreateGeometryShader(string filename, string geoShaderCode)
        {
            _geometryShaderId = CreateShader(filename, geoShaderCode, ShaderType.GeometryShader);
        }

        /**
         * Loads texture into shader
         * @param varName Name of texture
         * @param slot Texture unit to use
         */
        public ShaderProgram UploadAndBindTexture(string varName, int slot, int textureId, TextureTarget target)
        {
            GL.ActiveTexture((TextureUnit)((int)TextureUnit.Texture0 + slot));
            GL.BindTexture(target, textureId);
            int varLocation = GL.GetUniformLocation(GetProgramId(), varName);
            GL.Uniform1i(varLocation, slot);
            return this;
        }


        public ShaderProgram CreateUniform(string uniformName)
        {
           
            int uniformLocation = GL.GetUniformLocation(_programId, uniformName);
            if (uniformLocation < 0)
                _uniforms.Add(uniformName, uniformLocation);
           
            return this;
        }

        public static string LoadShaderFromFile(string filePath)
        {
            try
            {
                return File.ReadAllText(filePath);
            }
            catch (IOException e)
            {
                Logger.Error(e);
            }
            return null;
        }
        public ShaderProgram SetMatrixUniform(string uniformName, Matrix4 matrix)
        {
            int uniformLocation = GL.GetUniformLocation(_programId, uniformName);
            GL.UniformMatrix4f(uniformLocation, 1, true, ref matrix);
            return this;
        }
        public ShaderProgram SetIntFloatUniform(string uniformName, int value)
        {
            int uniformLocation = GL.GetUniformLocation(_programId, uniformName);
            GL.Uniform1i(uniformLocation, value);
            return this;
        }
        public ShaderProgram SetIntFloatUniform(string uniformName, float value)
        {
            int uniformLocation = GL.GetUniformLocation(_programId, uniformName);
            GL.Uniform1f(uniformLocation, value);
            return this;
        }

        public ShaderProgram SetVector3Uniform(string uniformName, Vector3 value)
        {
            int uniformLocation = GL.GetUniformLocation(_programId, uniformName);
            GL.Uniform3f(uniformLocation, value.X, value.Y, value.Z);
            return this;
        }
        public string GetProgramLog()
        {
            int[] maxlen = new int[1];
            // string log;

            GL.GetProgrami(_programId, ProgramProperty.InfoLogLength, maxlen);
            GL.GetProgramInfoLog(_programId);//, maxlen[0], out int len, out log);
    


            return "Log Length: " + maxlen[0] + "\n" + GL.GetProgramInfoLog(_programId);

        }
        private int CreateShader(string filename, string shaderCode, ShaderType shaderType)
        {
            int shaderId = GL.CreateShader(shaderType);

            GL.ShaderSource(shaderId, shaderCode);
            GL.CompileShader(shaderId);

            _shaderIdentity.Add(shaderId, filename);

            int status;
            GL.GetShaderi(shaderId, ShaderParameterName.CompileStatus, out status);

            // Check for compilation errors
            if (status != (int)All.True)
                Logger.Error(new Exception($"ShaderProgram.CreateShader - Failed to compile {shaderType} shader for {_shaderIdentity[shaderId]}: {GL.GetShaderInfoLog(shaderId)}"), ConsoleColor.Red);
            else
                Logger.Success($"ShaderProgram.CreateShader - Successfully compiled {shaderType} {_shaderIdentity[shaderId]}", ConsoleColor.Green);

            GL.AttachShader(_programId, shaderId);

            return shaderId;
        }

        public void Link()
        {
            try
            {
                GL.LinkProgram(_programId);

                int[] linkStatus = new int[1]; // Create an array to store the result
                GL.GetProgrami(_programId, ProgramProperty.LinkStatus, linkStatus);
                int status = linkStatus[0]; // Access the value from the array
                
                // If linking was unsuccessful
                if (status != (int)All.True)
                {
                    if (_vertexShaderId != 0)
                    {
                        GL.DetachShader(_programId, _vertexShaderId);
                        GL.DeleteShader(_vertexShaderId);
                        Logger.Success("Successfully linked vertex shader with status " + status);
                    }
                    else
                    {
                        Logger.Error("============================================================================");
                        Logger.Error("                Failed to attach vertex shaders with ID of 0                ");
                        Logger.Error("============================================================================");
                        Logger.Error(new ShaderException("Failed to link program with status " + status + "\nError Code: " + GL.GetError()));
                        Logger.Error(GetProgramLog());             
                    }

                    if (_fragmentShaderId != 0)
                    {
                        GL.DetachShader(_programId, _fragmentShaderId);
                        GL.DeleteShader(_fragmentShaderId);
                        Logger.Success("Successfully linked fragment shader with status " + status);
                    }
                    else
                    {
                        Logger.Error("============================================================================");    
                        Logger.Error("               Failed to attach fragment shaders with ID of 0               ");
                        Logger.Error("============================================================================");
                        Logger.Error(new ShaderException("Failed to link program with status " + status + "\nError Code: " + GL.GetError()));
                        Logger.Error(GetProgramLog());
                    }

                    Logger.Error("============================================================================");
                    Logger.Error("                Shader processing failed with linking error                 ");
                    Logger.Error("============================================================================");
                    Logger.Error(new ShaderException("Failed to link program with status " + status + "\nError Code: " + GL.GetError()));
                    Logger.Error(GetProgramLog());

                    Logger.Error(new ShaderException($"Shader program with ID of {_programId} and shaders {_shaderIdentity[_vertexShaderId]} " +
                        $"and {_shaderIdentity[_fragmentShaderId]} failed to link properly"));

                    return;
                }
                // Linking succeeded
                Logger.Success($"Shader program with ID of {_programId} linked successfully.");

                // Validate the shader program
                GL.ValidateProgram(_programId);

                // Check if validation was successful
                int[] validStatus = new int[1]; // Create an array to store the result
                GL.GetProgrami(_programId, ProgramProperty.ValidateStatus, validStatus);
                int vStatus = validStatus[0]; // Access the value from the array

                if (vStatus != (int)All.True)
                {
                    Logger.Error("===============================================================================");
                    Logger.Error("               Shader processing failed with validation error                  ");
                    Logger.Error("===============================================================================");
                    Logger.Error(new ShaderException("Failed to validate program with status " + vStatus + "\nError Code: " + GL.GetError()));
                    Logger.Error(GetProgramLog());
                }
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }

        public ShaderProgram Bind()
        {
            GL.UseProgram(_programId);
            GL.GetInteger((GetPName)All.CurrentProgram, out int currentProgram);
            return this;
        }

        public void Unbind()
        {
            GL.UseProgram(0);
        }

        public void Cleanup()
        {
            Unbind();
            if (_programId != 0)
            {
                GL.DeleteProgram(_programId);
            }
        }
    }
}
