using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Noikoio.RegexBot
{
    /// <summary>
    /// Logging helper class. Receives logging messages and handles them accordingly.
    /// </summary>
    class Logger
    {
        private static Logger _instance;
        private readonly string _logBasePath;
        private bool _fileLogEnabled;
        private static readonly object FileLogLock = new object();

        /// <summary>
        /// Gets if the instance is logging all messages to a file.
        /// </summary>
        public bool FileLoggingEnabled => _fileLogEnabled;
        private Logger()
        {
            // top level - determine path to use for logging and see if it's usable
            var dc = Path.DirectorySeparatorChar;
            _logBasePath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + dc + "logs";
            try
            {
                if (!Directory.Exists(_logBasePath)) Directory.CreateDirectory(_logBasePath);
                _fileLogEnabled = true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Console.Error.WriteLine("Unable to create log directory. File logging disabled.");
                _fileLogEnabled = false;
            }
        }
        
        /// <summary>
        /// Requests a delegate to be used for sending log messages.
        /// </summary>
        /// <param name="prefix">String used to prefix log messages sent using the given delegate.</param>
        /// <returns></returns>
        public static AsyncLogger GetLogger(string prefix)
        {
            if (_instance == null) _instance = new Logger();
            return (async delegate (string line) { await _instance.ProcessLog(prefix, line); });
        }

        protected Task ProcessLog(string source, string input)
        {
            var timestamp = DateTime.Now;
            string filename = _logBasePath + Path.DirectorySeparatorChar + $"{timestamp:yyyy-MM}.log";

            List<string> result = new List<string>();
            foreach (var line in Regex.Split(input, "\r\n|\r|\n"))
            {
                string finalLine = $"{timestamp:u} [{source}] {line}";
                result.Add(finalLine);
                Console.WriteLine(finalLine);
            }

            if (FileLoggingEnabled)
            {
                try
                {
                    lock (FileLogLock) File.AppendAllLines(filename, result);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    Console.Error.WriteLine("Unable to write to log file. File logging disabled.");
                    _fileLogEnabled = false;
                }
            }

            return Task.CompletedTask;
        }
    }

    public delegate Task AsyncLogger(string prefix);
}
