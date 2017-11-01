using Newtonsoft.Json.Linq;
using Npgsql;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.ConfigItem
{
    class DatabaseConfig
    {
        private readonly bool _enabled;
        private readonly string _host;
        private readonly string _user;
        private readonly string _pass;
        private readonly string _dbname;
        private readonly string _parsemsg;

        /// <summary>
        /// Gets whether database features are enabled.
        /// </summary>
        public bool Enabled => _enabled;
        /// <summary>
        /// Constructor error message (only if not enabled)
        /// </summary>
        public string ParseMsg => _parsemsg;

        public DatabaseConfig(JToken ctok)
        {
            if (ctok == null || ctok.Type != JTokenType.Object)
            {
                _enabled = false;
                _parsemsg = "Database configuration not defined.";
                return;
            }
            var conf = (JObject)ctok;

            _host = conf["hostname"]?.Value<string>() ?? "localhost"; // default to localhost
            _user = conf["username"]?.Value<string>();
            _pass = conf["password"]?.Value<string>();
            _dbname = conf["database"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(_user) || string.IsNullOrWhiteSpace(_pass) || string.IsNullOrWhiteSpace(_dbname))
            {
                _parsemsg = "One or more required values are invalid or not defined.";
                _enabled = false;
            }

            _parsemsg = null;
            _enabled = true;
        }

        private async Task<NpgsqlConnection> P_OpenConnectionAsync(ulong? guildId = null)
        {
            if (!Enabled) return null;

            var cs = new NpgsqlConnectionStringBuilder()
            {
                Host = _host,
                Username = _user,
                Password = _pass,
                Database = _dbname
            };
            if (guildId.HasValue) cs.SearchPath = "g_" + guildId.Value.ToString();
            
            var db = new NpgsqlConnection(cs.ToString());
            await db.OpenAsync();
            return db;
        }
        public Task<NpgsqlConnection> OpenConnectionAsync(ulong guildId) => P_OpenConnectionAsync(guildId);


        public async Task CreateGuildSchemaAsync(ulong gid)
        {
            if (!Enabled) return;

            const string cs = "CREATE SCHEMA IF NOT EXISTS {0}";

            string sn = "g_" + gid.ToString();
            using (var db = await P_OpenConnectionAsync())
            {
                using (var c = db.CreateCommand())
                {
                    c.CommandText = string.Format(cs, sn);
                    await c.ExecuteNonQueryAsync();
                }
            }
        }
    }
}
