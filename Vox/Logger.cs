
namespace Vox
{
    internal class Logger
    {
        public Logger() { }

        public static void Info(object message, ConsoleColor color = ConsoleColor.White)
        {
            Console.ForegroundColor = color;
            Console.WriteLine("[INFO] " + DateTime.Now + " :: " + message.ToString());
            Console.ResetColor();
        }
        public static void Error(Exception e, ConsoleColor color = ConsoleColor.White)
        {
            Console.ForegroundColor = color;
            Console.WriteLine("[ERROR] " + DateTime.Now + " :: " + e.GetType() + " :: " + e.Message);
            Console.WriteLine(e.StackTrace);
            Console.ResetColor();
        }
        public static void Error(string e, ConsoleColor color = ConsoleColor.White)
        {
            Console.ForegroundColor = color;
            Console.WriteLine("[ERROR] " + e);
            Console.ResetColor();
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
        public static void Debug(object message, ConsoleColor color = ConsoleColor.White)
        {
            if (message == null)
                message = $"Object is null";

            Console.ForegroundColor = color;
            Console.WriteLine("[DEBUG] " + DateTime.Now + " :: " + message.ToString());
            Console.ResetColor();
        }
    }
}
