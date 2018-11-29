using Newtonsoft.Json.Linq;
using Npgsql;
using System;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.ConfigItem
{
    class DatabaseConfig
    {
        private readonly string _host;
        private readonly string _user;
        private readonly string _pass;
        private readonly string _dbname;

        public DatabaseConfig(JToken ctok)
        {
            if (ctok == null || ctok.Type != JTokenType.Object)
            {
                throw new DatabaseConfigLoadException("");
            }
            var conf = (JObject)ctok;

            _host = conf["hostname"]?.Value<string>() ?? "localhost"; // default to localhost

            _user = conf["username"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(_user))
                throw new DatabaseConfigLoadException("Value for username is not defined.");

            _pass = conf["password"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(_pass))
                throw new DatabaseConfigLoadException(
                    $"Value for password is not defined. {nameof(RegexBot)} only supports password authentication.");

            _dbname = conf["database"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(_dbname))
                throw new DatabaseConfigLoadException("Value for database name is not defined.");
        }

        internal async Task<NpgsqlConnection> GetOpenConnectionAsync()
        {
            var cs = new NpgsqlConnectionStringBuilder()
            {
                Host = _host,
                Username = _user,
                Password = _pass,
                Database = _dbname
            };
            
            var db = new NpgsqlConnection(cs.ToString());
            await db.OpenAsync();
            return db;
        }

        internal class DatabaseConfigLoadException : Exception
        {
            public DatabaseConfigLoadException(string message) : base(message) { }
        }
    }
}
