using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using Noikoio.RegexBot.Feature.RegexResponder;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Noikoio.RegexBot
{
    /// <summary>
    /// Configuration loader
    /// </summary>
    class ConfigLoader
    {
        public const string LogPrefix = "Config";

        private readonly string _configPath;
        private Server[] _servers;

        private string _botToken;
        private string _currentGame;

        public string BotUserToken => _botToken;
        public string CurrentGame => _currentGame;
        public Server[] Servers => _servers;

        public ConfigLoader()
        {
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
        /// Called only on bot startup. Returns false on failure.
        /// </summary>
        public bool LoadInitialConfig()
        {
            var lt = LoadFile();
            lt.Wait();
            JObject conf = lt.Result;
            if (conf == null) return false;

            _botToken = conf["bot-token"]?.Value<string>();
            if (String.IsNullOrWhiteSpace(_botToken))
            {
                Logger.GetLogger(LogPrefix)("Error: Bot token not defined. Cannot continue.").Wait();
                return false;
            }
            _currentGame = conf["playing"]?.Value<string>();

            return ProcessServerConfig(conf).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Reloads the server portion of the configuration file.
        /// </summary>
        /// <returns>False on failure. Specific reasons will have been sent to log.</returns>
        public async Task<bool> ReloadServerConfig()
        {
            await Logger.GetLogger(LogPrefix)("Configuration reload currently not supported.");
            return false;
            // TODO actually implement this
            var lt = LoadFile();
            lt.Wait();
            JObject conf = lt.Result;
            if (conf == null) return false;

            return await ProcessServerConfig(conf);
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

            List<Server> newservers = new List<Server>();
            await Log("Reading server configurations...");
            foreach (JObject sconf in conf["servers"].Children<JObject>())
            {
                // Server name
                if (sconf["name"] == null || string.IsNullOrWhiteSpace(sconf["name"].Value<string>()))
                {
                    await Log("Error: Server definition is missing a name.");
                    return false;
                }
                string snamestr = sconf["name"].Value<string>();
                string sname;
                ulong? sid;

                int snseparator = snamestr.IndexOf("::");
                if (ulong.TryParse(snamestr.Substring(0, snseparator), out var id))
                {
                    sid = id;
                    sname = snamestr.Substring(snseparator + 2);
                }
                else
                {
                    sid = null;
                    sname = snamestr;
                }
                
                var SLog = Logger.GetLogger(LogPrefix + "/" + sname);

                // Load server moderator list
                EntityList mods = new EntityList(sconf["moderators"]);
                if (sconf["moderators"] != null) await SLog("Moderator " + mods.ToString());

                // Read rules
                // Also, parsed rules require a server reference. Creating it here.
                List<RuleConfig> rules = new List<RuleConfig>();
                Server newserver = new Server(sname, sid, rules, mods);

                foreach (JObject ruleconf in sconf["rules"])
                {
                    // Try and get at least the name before passing it to RuleItem
                    string name = ruleconf["name"]?.Value<string>();
                    if (name == null)
                    {
                        await SLog("Display name not defined within a rule section.");
                        return false;
                    }
                    await SLog($"Adding rule \"{name}\"");

                    RuleConfig rule;
                    try
                    {
                        rule = new RuleConfig(newserver, ruleconf);
                    } catch (RuleImportException ex)
                    {
                        await SLog("-> Error: " + ex.Message);
                        return false;
                    }
                    rules.Add(rule);
                }

                // Switch to using new data
                List<Tuple<Regex, string[]>> rulesfinal = new List<Tuple<Regex, string[]>>();
                newservers.Add(newserver);
            }

            _servers = newservers.ToArray();
            return true;
        }
    }

    
}
