using System;

namespace MultiplayerSFS.Server
{
    public static class Logger
    {
        static string Date => DateTime.Now.ToString();

        public static void Debug(object obj)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("[{0}] [DEBUG]: {1}", Date, obj);
            Console.ResetColor();
        }
        
        public static void Info(string msg, bool important = false)
        {
            if (important)
                Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[{0}] [INFO]: {1}", Date, msg);
            Console.ResetColor();
        }

        public static void Warning(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[{0}] [WARN]: {1}", Date, msg);
            Console.ResetColor();
        }

        public static void Error(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[{0}] [ERROR]: {1}", Date, message);
            Console.ResetColor();
        }

        public static void Error(Exception exception)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[{0}] [ERROR]: {1}", Date, exception);
            Console.ResetColor();
        }
    }
}