namespace Vox.Rendering
{
    public interface IShaderManager
    {
        ShaderProgram GetShaderProgram(string name);
        ShaderProgram AddShaderProgram(string name, ShaderProgram shader);
        void CleanupShaders();
    }
}