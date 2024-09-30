
namespace Vox
{
    internal class Logger
    {
        public Logger() { }

        public static void Info(object message)
        {
            Console.WriteLine("[INFO] " + DateTime.Now + " :: " + message.ToString());
        }
        public static void Error(Exception e)
        {
            Console.WriteLine("[ERROR] " + DateTime.Now + " :: " + e.GetType() + " :: " + e.Message);
            Console.WriteLine(e.StackTrace);
        }
        public static void Error(Exception e, string location)
        {
              
            Console.WriteLine("[ERROR] " + DateTime.Now + " :: " + e.GetType() + " :: " + e.Message + " :: " + location);
            Console.WriteLine(e.StackTrace);
        }
        public static void Warn(string message)
        {
            Console.WriteLine("[WARN] " + DateTime.Now + " :: " + message);
        }
        public static void Debug(object message)
        {
            if (message == null)
                message = $"Object is null";

            Console.WriteLine("[DEBUG] " + DateTime.Now + " :: " + message.ToString());
        }
    }
}
