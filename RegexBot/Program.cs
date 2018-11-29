using System;
using System.Text.RegularExpressions;

namespace Noikoio.RegexBot
{
    /// <summary>
    /// Program entry point. Sets up handling of certain events and does initial
    /// configuration loading before starting the Discord client.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            RegexBot rb = new RegexBot();

            Console.CancelKeyPress += rb.Console_CancelKeyPress;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            rb.Start().GetAwaiter().GetResult();
        }
        
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var l = Logger.GetLogger("Runtime");
            string[] lines = Regex.Split(e.ExceptionObject.ToString(), "\r\n|\r|\n");
            foreach (string line in lines)
            {
                l(line).Wait();
            }
        }
    }
}