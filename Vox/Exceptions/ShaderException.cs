
namespace Vox.Exceptions
{
    public class ShaderException : Exception
    {
        public ShaderException() { }
        public ShaderException(string message) : base(message) { }
        public ShaderException(string message, Exception innerException) : base(message, innerException) { }
    }
}
