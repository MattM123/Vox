
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Vox.Exceptions;
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
        public void CreateVertexShader(string filename, string vertexShaderCode)
        {
            vertexShaderId = CreateShader(filename, vertexShaderCode, ShaderType.VertexShader);

        }

        public void CreateFragmentShader(string filename, string fragmentShaderCode)
        {
            fragmentShaderId = CreateShader(filename, fragmentShaderCode, ShaderType.FragmentShader);

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
        public void UploadAndBindTexture(string varName, int slot, int textureID, TextureTarget target)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + slot);
            GL.BindTexture(target, textureID);
            int varLocation = GL.GetUniformLocation(GetProgramId(), varName);
            GL.Uniform1(varLocation, slot);
        }


        public void CreateUniform(string uniformName)
        {
           
            int uniformLocation = GL.GetUniformLocation(programId, uniformName);
            if (uniformLocation < 0)
                uniforms.Add(uniformName, uniformLocation);
           // else
             //   Logger.Debug("ShaderProgram.CreateUniform - Uniform already exists: " + uniformName);
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
        public void SetMatrixUniform(string uniformName, Matrix4 matrix)
        {
            int uniformLocation = GL.GetUniformLocation(programId, uniformName);
            GL.UniformMatrix4(uniformLocation, true, ref matrix);
        }
        public void SetIntFloatUniform(string uniformName, int value)
        {
            int uniformLocation = GL.GetUniformLocation(programId, uniformName);
            GL.Uniform1(uniformLocation, value);
        }
        public void SetIntFloatUniform(string uniformName, float value)
        {
            int uniformLocation = GL.GetUniformLocation(programId, uniformName);
            GL.Uniform1(uniformLocation, value);
        }

        public void SetVector3Uniform(string uniformName, Vector3 value)
        {
            int uniformLocation = GL.GetUniformLocation(programId, uniformName);
            GL.Uniform3(uniformLocation, value);
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
            Console.ForegroundColor = ConsoleColor.Cyan;
            if (status != (int)All.True)
                Logger.Error(new Exception($"ShaderProgram.CreateShader - Failed to compile {shaderType} shader for {shaderIdentity[shaderId]}: {GL.GetShaderInfoLog(shaderId)}"));
            else
                Logger.Debug($"ShaderProgram.CreateShader - Successfully compiled {shaderType} {shaderIdentity[shaderId]}");
            Console.ResetColor();

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
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        GL.DetachShader(programId, vertexShaderId);
                        GL.DeleteShader(vertexShaderId);
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Logger.Debug("Successfully linked vertex shader with status " + status);
                        Console.ResetColor();

                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("============================================================================");
                        Console.WriteLine("                Failed to attach vertex shaders with ID of 0                ");
                        Console.WriteLine("============================================================================");
                        Logger.Error(new ShaderException("Failed to link program with status " + status + "\nError Code: " + GL.GetError()));
                        Logger.Debug(GetProgramLog());
                        Console.ResetColor();

                    }

                    if (fragmentShaderId != 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        GL.DetachShader(programId, fragmentShaderId);
                        GL.DeleteShader(fragmentShaderId);
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Logger.Debug("Successfully linked fragment shader with status " + status);
                        Console.ResetColor();

                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("============================================================================");
                        Console.WriteLine("               Failed to attach fragment shaders with ID of 0               ");
                        Console.WriteLine("============================================================================");
                        Logger.Error(new ShaderException("Failed to link program with status " + status + "\nError Code: " + GL.GetError()));
                        Logger.Debug(GetProgramLog());
                        Console.ResetColor();

                    }
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("============================================================================");
                    Console.WriteLine("                Shader processing failed with linking error                 ");
                    Console.WriteLine("============================================================================");
                    Logger.Error(new ShaderException("Failed to link program with status " + status + "\nError Code: " + GL.GetError()));
                    Logger.Debug(GetProgramLog());
                    Logger.Error(new ShaderException($"Shader program with ID of {programId} and shaders {shaderIdentity[vertexShaderId]} " +
                        $"and {shaderIdentity[fragmentShaderId]} failed to link properly"));
                    Console.ResetColor();
                    return;
                }
                // Linking succeeded
                Console.ForegroundColor = ConsoleColor.Cyan;
                Logger.Debug($"Shader program with ID of {programId} linked successfully.");
                Console.ResetColor();
                //GL.DetachShader(programId, vertexShaderId);
                //GL.DeleteShader(vertexShaderId);
                //
                //GL.DetachShader(programId, fragmentShaderId);
                //GL.DeleteShader(fragmentShaderId);
                // Free shader objects after successful link
                if (vertexShaderId != 0)
                {
                    GL.DetachShader(programId, vertexShaderId);
                    GL.DeleteShader(vertexShaderId);
                }
                if (fragmentShaderId != 0)
                {
                    GL.DetachShader(programId, fragmentShaderId);
                    GL.DeleteShader(fragmentShaderId);
                }


                // Validate the shader program
                GL.ValidateProgram(programId);

                // Check if validation was successful
                int[] validStatus = new int[1]; // Create an array to store the result
                GL.GetProgram(programId, GetProgramParameterName.ValidateStatus, validStatus);
                int vStatus = validStatus[0]; // Access the value from the array

                if (vStatus != (int)All.True)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("===============================================================================");
                    Console.WriteLine("               Shader processing failed with validation error                  ");
                    Console.WriteLine("===============================================================================");
                    Logger.Error(new ShaderException("Failed to validate program with status " + vStatus + "\nError Code: " + GL.GetError()));
                    Logger.Debug(GetProgramLog());
                    Console.ResetColor();

                }
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Logger.Error(e, e.Message);
                Console.ResetColor();
            }
            Console.ResetColor();
        }

        public void Bind()
        {
            GL.UseProgram(programId);
            GL.GetInteger((GetPName)All.CurrentProgram, out int currentProgram);
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
                GL.DeleteShader(vertexShaderId);
                GL.DeleteShader(fragmentShaderId);
                GL.DeleteProgram(programId);
            }
        }
    }
}
