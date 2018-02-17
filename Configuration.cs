using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Noikoio.RegexBot
{
    /// <summary>
    /// Configuration loader
    /// </summary>
    class Configuration
    {
        public const string LogPrefix = "Config";

        private readonly RegexBot _bot;
        private readonly string _configPath;
        private DatabaseConfig _dbConfig;
        private ServerConfig[] _servers;

        // The following values do not change on reload:
        private string _botToken;
        private string _currentGame;

        public string BotUserToken => _botToken;
        public string CurrentGame => _currentGame;

        public ServerConfig[] Servers => _servers;

        public Configuration(RegexBot bot)
        {
            _bot = bot;
            var dsc = Path.DirectorySeparatorChar;
            _configPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)
                + dsc + "settings.json";
        }

        private async Task<JObject> LoadFile()
        {
            var Log = Logger.GetLogger(LogPrefix);
            JObject pcfg;
            try
            {
                var ctxt = File.ReadAllText(_configPath);
                pcfg = JObject.Parse(ctxt);
                return pcfg;
            }
            catch (Exception ex) when (ex is DirectoryNotFoundException || ex is FileNotFoundException)
            {
                await Log("Config file not found! Check bot directory for settings.json file.");
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                await Log("Could not access config file. Check file permissions.");
                return null;
            }
            catch (JsonReaderException jex)
            {
                await Log("Failed to parse JSON.");
                await Log(jex.GetType().Name + " " + jex.Message);
                return null;
            }
        }

        /// <summary>
        /// Loads essential, unchanging values needed for bot startup. Returns false on failure.
        /// </summary>
        internal bool LoadInitialConfig()
        {
            var lt = LoadFile();
            lt.Wait();
            JObject conf = lt.Result;
            if (conf == null) return false;

            var log = Logger.GetLogger(LogPrefix);

            _botToken = conf["bot-token"]?.Value<string>();
            if (String.IsNullOrWhiteSpace(_botToken))
            {
                log("Error: Bot token not defined. Cannot continue.").Wait();
                return false;
            }
            _currentGame = conf["playing"]?.Value<string>();

            // Database configuration:
            // Either it exists or it doesn't. Read config, but also attempt to make a database connection
            // right here, or else make it known that database support is disabled for this instance.
            try
            {
                _dbConfig = new DatabaseConfig(conf["database"]);
                var conn = _dbConfig.GetOpenConnectionAsync().GetAwaiter().GetResult();
                conn.Dispose();
            }
            catch (DatabaseConfig.DatabaseConfigLoadException ex)
            {
                if (ex.Message == "") log("Database configuration not found.").Wait();
                else log("Error within database config: " + ex.Message).Wait();
                _dbConfig = null;
            }
            catch (Npgsql.NpgsqlException ex)
            {
                log("An error occurred while establishing initial database connection: " + ex.Message).Wait();
                _dbConfig = null;
            }
            // Modules that will not enable due to lack of database access should say so in their constructors.

            return true;
        }

        /// <summary>
        /// Reloads the server portion of the configuration file.
        /// </summary>
        /// <returns>False on failure. Specific reasons will have been sent to log.</returns>
        public async Task<bool> ReloadServerConfig()
        {
            var config = await LoadFile();
            if (config == null) return false;

            return await ProcessServerConfig(config);
        }

        /// <summary>
        /// Converts a json object containing bot configuration into data usable by this program.
        /// On success, updates the Servers values and returns true. Returns false on failure.
        /// </summary>
        private async Task<bool> ProcessServerConfig(JObject conf)
        {
            var Log = Logger.GetLogger(LogPrefix);
            if (!conf["servers"].HasValues)
            {
                await Log("Error: No server configurations are defined.");
                return false;
            }

            List<ServerConfig> newservers = new List<ServerConfig>();
            await Log("Reading server configurations...");
            foreach (JObject sconf in conf["servers"].Children<JObject>())
            {
                // Server name
                //if (sconf["id"] == null || sconf["id"].Type != JTokenType.Integer))
                if (sconf["id"] == null)
                {
                    await Log("Error: Server ID is missing within definition.");
                    return false;
                }
                ulong sid = sconf["id"].Value<ulong>();
                string sname = sconf["name"]?.Value<string>();
                
                var SLog = Logger.GetLogger(LogPrefix + "/" + (sname ?? sid.ToString()));

                // Load server moderator list
                EntityList mods = new EntityList(sconf["moderators"]);
                if (sconf["moderators"] != null) await SLog("Moderator " + mods.ToString());
                
                // Load module configurations
                Dictionary<BotModule, object> customConfs = new Dictionary<BotModule, object>();
                foreach (var item in _bot.Modules)
                {
                    var attr = item.GetType().GetTypeInfo()
                        .GetMethod("ProcessConfiguration").GetCustomAttribute<ConfigSectionAttribute>();
                    if (attr == null)
                    {
                        await SLog("No additional configuration for " + item.Name);
                        continue;
                    }
                    var section = sconf[attr.SectionName];
                    if (section == null)
                    {
                        await SLog("Additional configuration not defined for " + item.Name);
                        continue;
                    }

                    await SLog("Loading additional configuration for " + item.Name);
                    object result;
                    try
                    {
                        result = await item.ProcessConfiguration(section);
                    }
                    catch (RuleImportException ex)
                    {
                        await SLog($"{item.Name} failed to load configuration: " + ex.Message);
                        return false;
                    }

                    customConfs.Add(item, result);
                }


                // Switch to using new data
                List<Tuple<Regex, string[]>> rulesfinal = new List<Tuple<Regex, string[]>>();
                newservers.Add(new ServerConfig(sid, mods, new ReadOnlyDictionary<BotModule, object>(customConfs)));
            }

            _servers = newservers.ToArray();
            return true;
        }

        /// <summary>
        /// Gets a value stating if database access is available.
        /// Specifically, indicates if <see cref="GetOpenDatabaseConnectionAsync"/> will return a non-null value.
        /// </summary>
        /// <remarks>
        /// Ideally, this value remains constant on runtime. It does not take into account
        /// the possibility of the database connection failing during the program's run time.
        /// </remarks>
        public bool DatabaseAvailable => _dbConfig != null;
        /// <summary>
        /// Gets an opened connection to the SQL database, if available.
        /// </summary>
        /// <returns>
        /// An <see cref="Npgsql.NpgsqlConnection"/> in the opened state,
        /// or null if an SQL database is not available.
        /// </returns>
        public Task<Npgsql.NpgsqlConnection> GetOpenDatabaseConnectionAsync() => _dbConfig?.GetOpenConnectionAsync();
    }
}
