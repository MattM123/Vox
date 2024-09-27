using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vox
{
    internal class Logger
    {
        public Logger() { }

        public static void Log(string message)
        {
            Console.WriteLine("[LOG] " + DateTime.Now + " :: " + message);
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
        public static void Debug(string message)
        {
            Console.WriteLine("[DEBUG] " + DateTime.Now + " :: " + message);
        }
    }
}
