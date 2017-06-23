using System;

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
            // Attempt to load basic configuration before setting up the client
            var config = new ConfigLoader();
            if (!config.LoadInitialConfig())
            {
#if DEBUG
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
#endif
                Environment.Exit(1);
            }

            RegexBot rb = new RegexBot(config);

            Console.CancelKeyPress += rb.Console_CancelKeyPress;
            //AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            rb.Start().GetAwaiter().GetResult();
        }

        // TODO Re-implement this once the framework allows for it again.
        //private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        //{
        //    var l = _logger.SetPrefix("Runtime");
        //    string[] lines = Regex.Split(e.ExceptionObject.ToString(), "\r\n|\r|\n");
        //    foreach (string line in lines)
        //    {
        //        l.Log(line).Wait();
        //    }
        //}
    }
}