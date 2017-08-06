using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Noikoio.RegexBot.Feature.ModTools
{
    /// <summary>
    /// Specifies this class's corresponding value when being defined in configuration
    /// under a custom command's "type" value.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    class CommandTypeAttribute : Attribute
    {
        readonly string _type;
        public string TypeName => _type;
        public CommandTypeAttribute(string typeName) => _type = typeName;

        private static Dictionary<string, Type> _sTypes;
        /// <summary>
        /// Translates a command type defined from configuration into a usable
        /// <see cref="System.Type"/> deriving from CommandBase.
        /// </summary>
        internal static Type GetCommandType(string input)
        {
            if (_sTypes == null)
            {
                var newtypelist = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
                var ctypes = from type in Assembly.GetEntryAssembly().GetTypes()
                             where typeof(CommandBase).IsAssignableFrom(type)
                             select type;
                foreach (var type in ctypes)
                {
                    var attr = type.GetTypeInfo().GetCustomAttribute<CommandTypeAttribute>();
                    if (attr == null)
                    {
#if DEBUG
                        Console.WriteLine($"{type.FullName} does not define a {nameof(CommandTypeAttribute)}");
#endif
                        continue;
                    }
                    newtypelist.Add(attr.TypeName, type);
                }
                _sTypes = newtypelist;
            }

            if (_sTypes.TryGetValue(input, out var cmdtype))
            {
                return cmdtype;
            }
            return null;
        }
    }
}
