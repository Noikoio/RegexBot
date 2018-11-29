using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Noikoio.RegexBot.ConfigItem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Noikoio.RegexBot.Module.EntryAutoRole
{
    /// <summary>
    /// Automatically sets a specified role 
    /// </summary>
    class EntryAutoRole : BotModule
    {
        private List<AutoRoleEntry> _roleWaitlist;
        private object _roleWaitLock = new object();

        // TODO make use of this later if/when some shutdown handler gets added
        // (else it continues running in debug after the client has been disposed)
        private readonly CancellationTokenSource _workerCancel;

        // Config:
        // Role: string - Name or ID of the role to apply. Takes EntityName format.
        // WaitTime: number - Amount of time in seconds to wait until applying the role to a new user.
        public EntryAutoRole(DiscordSocketClient client) : base(client)
        {
            client.GuildAvailable += Client_GuildAvailable;
            client.UserJoined += Client_UserJoined;
            client.UserLeft += Client_UserLeft;

            _roleWaitlist = new List<AutoRoleEntry>();

            _workerCancel = new CancellationTokenSource();
            Task.Factory.StartNew(Worker, _workerCancel.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
        
        public override Task<object> CreateInstanceState(JToken configSection)
        {
            if (configSection == null) return Task.FromResult<object>(null);
            if (configSection.Type != JTokenType.Object)
            {
                throw new RuleImportException("Configuration for this section is invalid.");
            }
            return Task.FromResult<object>(new ModuleConfig((JObject)configSection));
        }

        private Task Client_GuildAvailable(SocketGuild arg)
        {
            var conf = GetState<ModuleConfig>(arg.Id);
            if (conf == null) return Task.CompletedTask;
            SocketRole trole = GetRole(arg);
            if (trole == null) return Task.CompletedTask;

            lock (_roleWaitLock)
                foreach (var item in arg.Users)
                {
                    if (item.IsBot) continue;
                    if (item.IsWebhook) continue;
                    if (item.Roles.Contains(trole)) continue;
                    _roleWaitlist.Add(new AutoRoleEntry()
                    {
                        GuildId = arg.Id,
                        UserId = item.Id,
                        ExpireTime = DateTimeOffset.UtcNow.AddSeconds(conf.TimeDelay)
                    });
                }

            return Task.CompletedTask;
        }

        private Task Client_UserLeft(SocketGuildUser arg)
        {
            if (GetState<object>(arg.Guild.Id) == null) return Task.CompletedTask;
            lock (_roleWaitLock) _roleWaitlist.RemoveAll(m => m.GuildId == arg.Guild.Id && m.UserId == arg.Id);
            return Task.CompletedTask;
        }

        private Task Client_UserJoined(SocketGuildUser arg)
        {
            if (GetState<object>(arg.Guild.Id) == null) return Task.CompletedTask;
            lock (_roleWaitLock) _roleWaitlist.Add(new AutoRoleEntry()
            {
                GuildId = arg.Guild.Id,
                UserId = arg.Id,
                ExpireTime = DateTimeOffset.UtcNow.AddSeconds((GetState<ModuleConfig>(arg.Guild.Id)).TimeDelay)
            });
            return Task.CompletedTask;
        }
        
        // can return null
        private SocketRole GetRole(SocketGuild g)
        {
            var conf = GetState<ModuleConfig>(g.Id);
            if (conf == null) return null;
            var roleInfo = conf.Role;
            
            if (roleInfo.Id.HasValue)
            {
                var result = g.GetRole(roleInfo.Id.Value);
                if (result != null) return result;
            }
            else
            {
                foreach (var role in g.Roles)
                    if (string.Equals(roleInfo.Name, role.Name)) return role;
            }
            Log("Unable to find role in " + g.Name).Wait();
            return null;
        }

        struct AutoRoleEntry
        {
            public ulong GuildId;
            public ulong UserId;
            public DateTimeOffset ExpireTime;
        }
        
        async Task Worker()
        {
            while (!_workerCancel.IsCancellationRequested)
            {
                await Task.Delay(5000);

                AutoRoleEntry[] jobsList;
                lock (_roleWaitLock)
                {
                    var chk = DateTimeOffset.UtcNow;
                    // Attempt to avoid throttling: only 3 per run are processed
                    var jobs = _roleWaitlist.Where(i => chk > i.ExpireTime).Take(3);
                    jobsList = jobs.ToArray(); // force evaluation

                    // remove selected entries from current list
                    foreach (var item in jobsList)
                    {
                        _roleWaitlist.Remove(item);
                    }
                }

                // Temporary SocketRole cache. key = guild ID
                Dictionary<ulong, SocketRole> cr = new Dictionary<ulong, SocketRole>();
                foreach (var item in jobsList)
                {
                    if (_workerCancel.IsCancellationRequested) return;

                    // do we have the guild?
                    var g = Client.GetGuild(item.GuildId);
                    if (g == null) continue; // bot probably left the guild

                    // do we have the user?
                    var u = g.GetUser(item.UserId);
                    if (u == null) continue; // user is probably gone

                    // do we have the role?
                    SocketRole r;
                    if (!cr.TryGetValue(g.Id, out r))
                    {
                        r = GetRole(g);
                        if (r != null) cr[g.Id] = r;
                    }
                    if (r == null)
                    {
                        await Log($"Skipping {g.Name}/{u.ToString()}");
                        await Log("Was the role renamed or deleted?");
                    }

                    // do the work
                    try
                    {
                        await u.AddRoleAsync(r);
                    }
                    catch (Discord.Net.HttpException ex)
                    {
                        if (ex.HttpCode == System.Net.HttpStatusCode.Forbidden)
                        {
                            await Log($"WARNING: Cannot set roles! Skipping {g.Name}/{u.ToString()}");
                        }
                    }
                }
            }
        }
    }
}
