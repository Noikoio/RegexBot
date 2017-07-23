using System;

namespace Noikoio.RegexBot.ConfigItem
{
    /// <summary>
    /// Exception thrown during an attempt to read rule configuration.
    /// </summary>
    public class RuleImportException : Exception
    {
        public RuleImportException(string message) : base(message) { }
    }
}
