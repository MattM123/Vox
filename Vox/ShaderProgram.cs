using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Compute.OpenCL;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Vox.Exceptions;

namespace Vox
{
    public class ShaderProgram
    {
        private int programId;
        private int vertexShaderId;
        private int fragmentShaderId;
        private Dictionary<string, int> uniforms;

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
        public void CreateVertexShader(string vertexShaderCode)
        {
            vertexShaderId = CreateShader(vertexShaderCode, ShaderType.VertexShader);
        }

        public void CreateFragmentShader(string fragmentShaderCode)
        {
            fragmentShaderId = CreateShader(fragmentShaderCode, ShaderType.FragmentShader);
        }

        /**
         * Loads texture into shader
         * @param varName Name of texture
         * @param slot Texture unit to use
         */
        public void UploadTexture(string varName, int slot)
        {
            int varLocation = GL.GetUniformLocation(GetProgramId(), varName);
            GL.Uniform1(varLocation, slot);
        }


        public void CreateUniform(string uniformName)
        {
            int uniformLocation = GL.GetUniformLocation(programId, uniformName);
            if (uniformLocation < 0)
                uniforms.Add(uniformName, uniformLocation);
            else     
                Logger.Debug("ShaderProgram.CreateUniform - Uniform already exists: " + uniformName);
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
        private int CreateShader(string shaderCode, ShaderType shaderType)
        {
            int shaderId = GL.CreateShader(shaderType);

            GL.ShaderSource(shaderId, shaderCode);

            GL.CompileShader(shaderId);

            int status;
            GL.GetShader(shaderId, ShaderParameter.CompileStatus, out status);

            // Check for compilation errors
            if (status != (int) All.True)
                Logger.Error(new Exception("ShaderProgram.CreateShader - Failed to compile shader:\n" + GL.GetShaderInfoLog(shaderId)));
            else
                Logger.Debug("ShaderProgram.CreateShader - Successfully compiled " + shaderType.ToString());


            GL.AttachShader(programId, shaderId);
            int[] shaders = new int[10];
            GL.GetAttachedShaders(programId, 10, out int count, shaders);

            Logger.Debug("ShaderProgram.CreateShader - There are " +  count + " attached shaders");

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

                // Check if linking was successful
                if (status != (int) All.True)
                {
                    if (vertexShaderId != 0)
                    {
                        GL.DetachShader(programId, vertexShaderId);
                        GL.DeleteShader(vertexShaderId);
                    } else
                    {
                        Logger.Debug("Successfully linked vertex shader with status " + status);
                    }

                    if (fragmentShaderId != 0)
                    {
                        GL.DetachShader(programId, fragmentShaderId);
                        GL.DeleteShader(fragmentShaderId);
                    }
                    else 
                        Logger.Debug("Successfully linked fragment shader with status " + status);

                    Logger.Error(new ShaderException("Failed to link program with status " + status + "\nError Code: " + GL.GetError()));
                    Logger.Debug(GetProgramLog());
                }
                

                // Validate the shader program
                GL.ValidateProgram(programId);

                // Check if validation was successful
                int[] validStatus = new int[1]; // Create an array to store the result
                GL.GetProgram(programId, GetProgramParameterName.LinkStatus, validStatus);
                int vStatus = validStatus[0]; // Access the value from the array

                if (vStatus != (int)All.True)
                {
                    Logger.Error(new ShaderException("Failed to validate program with status " + status + "\nError Code: " + GL.GetError()));
                }
                
            }
            catch (Exception e)
            {
                Logger.Error(e, "ShaderProgram.Link");
            }
        }

        public void Bind()
        {
            GL.UseProgram(programId);
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
