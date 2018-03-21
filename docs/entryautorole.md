## EntryAutoRole

EntryAutoRole is a component that automatically assigns a role to users after a set amount of time. It is useful for limiting access to incoming users and as a basic means of controlling raids.

Roles set by this component do not persist. Should a user leave the server and rejoin, they will not be given the role again immediately and must wait to have it reassigned.

Sample within a [server definition](serverdef.html):
```
"EntryAutoRole": {
    "Role": "123451234512345::Newbie",
	"WaitTime": 600
}
```

### Configuration options
EntryAutoRole is simple to configure. All the following values are **required**.
* Role (*string*) - The role to set. If specified by string, it will search for a role matching that name. If specified by ID, the ID will be used and server managers are free to edit the role name without modifying this value.
  * If a name is given, then an role matching the name will be applied. Renaming the role will cause the component to fail to make use of the role until configuration is updated.
  * If an ID is specified, server managers are free to rename the role and still have it be used by the bot.
  * To find your role IDs, you may use a tool such as [Role ID Query Bot](https://discordapp.com/oauth2/authorize?client_id=425050329068077057&scope=bot).
* WaitTime (*number*) - Amount of time in seconds to wait until a new user is applied the role.