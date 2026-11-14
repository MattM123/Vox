
namespace Vox.Exceptions
{
    public class GlException : Exception
    {
        public GlException() { }
        public GlException(string message) : base(message) { }
        public GlException(string message, Exception innerException) : base(message, innerException) { }
    }
}
