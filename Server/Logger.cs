using System;

namespace MultiplayerSFS.Server
{
    public static class Logger
    {
        static string Date() => DateTime.Now.ToString();
        
        public static void Info(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[{0}] [INFO]: {1}", Date(), msg);
            Console.ResetColor();
        }

        public static void Warning(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[{0}] [INFO]: {1}", Date(), msg);
            Console.ResetColor();
        }

        public static void Error(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[{0}] [ERROR]: {1}", Date(), message);
            Console.ResetColor();
        }

        public static void Error(Exception exception)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[{0}] [ERROR]: {1}", Date(), exception);
            Console.ResetColor();
        }
    }
}