
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
        private int programId;
        private int vertexShaderId;
        private int fragmentShaderId;
        private int geometryShaderId;
        private Dictionary<string, int> uniforms;
        private Dictionary<int, string> shaderIdentity = [];

        public ShaderProgram()
        {
            GenerateProgramID();
            uniforms = [];
        }

        public void GenerateProgramID()
        {
            programId = GL.CreateProgram();
        }
        public int GetProgramId()
        {
            return programId;
        }
        public ShaderProgram CreateVertexShader(string filename, string vertexShaderCode)
        {
            vertexShaderId = CreateShader(filename, vertexShaderCode, ShaderType.VertexShader);
            ImGuiController.LabelObject((OpenTK.Graphics.OpenGL4.ObjectLabelIdentifier)ObjectLabelIdentifier.Shader, vertexShaderId, $"Shader: {filename}");
            return this;
        }

        public ShaderProgram CreateFragmentShader(string filename, string fragmentShaderCode)
        {
            fragmentShaderId = CreateShader(filename, fragmentShaderCode, ShaderType.FragmentShader);
            ImGuiController.LabelObject((OpenTK.Graphics.OpenGL4.ObjectLabelIdentifier)ObjectLabelIdentifier.Shader, fragmentShaderId, $"Shader: {filename}");
            return this;
        }

        public void CreateGeometryShader(string filename, string geoShaderCode)
        {
            geometryShaderId = CreateShader(filename, geoShaderCode, ShaderType.GeometryShader);
        }

        /**
         * Loads texture into shader
         * @param varName Name of texture
         * @param slot Texture unit to use
         */
        public ShaderProgram UploadAndBindTexture(string varName, int slot, int textureID, TextureTarget target)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + slot);
            GL.BindTexture(target, textureID);
            int varLocation = GL.GetUniformLocation(GetProgramId(), varName);
            GL.Uniform1(varLocation, slot);
            return this;
        }


        public ShaderProgram CreateUniform(string uniformName)
        {
           
            int uniformLocation = GL.GetUniformLocation(programId, uniformName);
            if (uniformLocation < 0)
                uniforms.Add(uniformName, uniformLocation);
           
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
            int uniformLocation = GL.GetUniformLocation(programId, uniformName);
            GL.UniformMatrix4(uniformLocation, true, ref matrix);
            return this;
        }
        public ShaderProgram SetIntFloatUniform(string uniformName, int value)
        {
            int uniformLocation = GL.GetUniformLocation(programId, uniformName);
            GL.Uniform1(uniformLocation, value);
            return this;
        }
        public ShaderProgram SetIntFloatUniform(string uniformName, float value)
        {
            int uniformLocation = GL.GetUniformLocation(programId, uniformName);
            GL.Uniform1(uniformLocation, value);
            return this;
        }

        public ShaderProgram SetVector3Uniform(string uniformName, Vector3 value)
        {
            int uniformLocation = GL.GetUniformLocation(programId, uniformName);
            GL.Uniform3(uniformLocation, value);
            return this;
        }
        public string GetProgramLog()
        {
            int[] maxlen = new int[1];
            // string log;

            GL.GetProgram(programId, GetProgramParameterName.InfoLogLength, maxlen);
            GL.GetProgramInfoLog(programId);//, maxlen[0], out int len, out log);


            return "Log Length: " + maxlen[0] + "\n" + GL.GetProgramInfoLog(programId);

        }
        private int CreateShader(string filename, string shaderCode, ShaderType shaderType)
        {
            int shaderId = GL.CreateShader(shaderType);

            GL.ShaderSource(shaderId, shaderCode);

            GL.CompileShader(shaderId);

            shaderIdentity.Add(shaderId, filename);

            int status;
            GL.GetShader(shaderId, ShaderParameter.CompileStatus, out status);

            // Check for compilation errors
            if (status != (int)All.True)
                Logger.Error(new Exception($"ShaderProgram.CreateShader - Failed to compile {shaderType} shader for {shaderIdentity[shaderId]}: {GL.GetShaderInfoLog(shaderId)}"), ConsoleColor.Red);
            else
                Logger.Success($"ShaderProgram.CreateShader - Successfully compiled {shaderType} {shaderIdentity[shaderId]}", ConsoleColor.Green);

            GL.AttachShader(programId, shaderId);

            return shaderId;
        }

        public void Link()
        {
            try
            {
                GL.LinkProgram(programId);

                int[] linkStatus = new int[1]; // Create an array to store the result
                GL.GetProgram(programId, GetProgramParameterName.LinkStatus, linkStatus);
                int status = linkStatus[0]; // Access the value from the array
                
                // If linking was unsuccessful
                if (status != (int)All.True)
                {
                    if (vertexShaderId != 0)
                    {
                        GL.DetachShader(programId, vertexShaderId);
                        GL.DeleteShader(vertexShaderId);
                        Logger.Success("Successfully linked vertex shader with status " + status);
                    }
                    else
                    {
                        Logger.Error("============================================================================");
                        Logger.Error("                Failed to attach vertex shaders with ID of 0                ");
                        Logger.Error("============================================================================");
                        Logger.Error(new ShaderException("Failed to link program with status " + status + "\nError Code: " + GL.GetError()));
                        Logger.Debug(GetProgramLog());             
                    }

                    if (fragmentShaderId != 0)
                    {
                        GL.DetachShader(programId, fragmentShaderId);
                        GL.DeleteShader(fragmentShaderId);
                        Logger.Success("Successfully linked fragment shader with status " + status);
                    }
                    else
                    {
                        Logger.Error("============================================================================");    
                        Logger.Error("               Failed to attach fragment shaders with ID of 0               ");
                        Logger.Error("============================================================================");
                        Logger.Error(new ShaderException("Failed to link program with status " + status + "\nError Code: " + GL.GetError()));
                        Logger.Debug(GetProgramLog());
                    }

                    Logger.Error("============================================================================");
                    Logger.Error("                Shader processing failed with linking error                 ");
                    Logger.Error("============================================================================");
                    Logger.Error(new ShaderException("Failed to link program with status " + status + "\nError Code: " + GL.GetError()));
                    Logger.Debug(GetProgramLog());

                    Logger.Error(new ShaderException($"Shader program with ID of {programId} and shaders {shaderIdentity[vertexShaderId]} " +
                        $"and {shaderIdentity[fragmentShaderId]} failed to link properly"));

                    return;
                }
                // Linking succeeded
                Logger.Success($"Shader program with ID of {programId} linked successfully.");

                // Validate the shader program
                GL.ValidateProgram(programId);

                // Check if validation was successful
                int[] validStatus = new int[1]; // Create an array to store the result
                GL.GetProgram(programId, GetProgramParameterName.ValidateStatus, validStatus);
                int vStatus = validStatus[0]; // Access the value from the array

                if (vStatus != (int)All.True)
                {
                    Logger.Error("===============================================================================");
                    Logger.Error("               Shader processing failed with validation error                  ");
                    Logger.Error("===============================================================================");
                    Logger.Error(new ShaderException("Failed to validate program with status " + vStatus + "\nError Code: " + GL.GetError()));
                    Logger.Debug(GetProgramLog());
                }
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }

        public ShaderProgram Bind()
        {
            GL.UseProgram(programId);
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
            if (programId != 0)
            {
                GL.DeleteProgram(programId);
            }
        }
    }
}
