## ModCommands

ModCommands is the name of the component that provides the ability for one to create useful commands for moderation. These commands are defined based on a number of available template-like *type*s, which can then be customized with further configuration.

Sample within a [server definition](serverdef.html):
```
"ModCommands": {
    "Kick": { // a plain and simple kick command
        "type": "kick",
        "command": "!!kick"
    },
    "Party Ban": { // self-explanatory
        "type": "ban",
        "command": "!!party",
        "successmsg": "Yay! $target got banned!\nhttps://i.imgur.com/i4CIBtT.jpg"
    }
}
```

### Definition structure
Commands are defined within a `ModCommands` object, itself within a [server definition](serverdef.html). They are defined by means of name/value pairs, with the name serving as its label.

The following values are **required** in a definition:
* type (*string*) - Specifies the type of behavior that the command should take.
* command (*string*) - The text trigger for the command being defined. Must not contain spaces, and it is recommended to start it with an uncommon symbol, such as `!`.

### Command types
Each command type specifies the action taken by the bot when the command is invoked. Certain types offer additional configuration options.

#### Ban
* `"type": "ban"`
* Usage: (*command*) (*user name or ID*) [*reason*]
Bans the given user from the server the command was invoked in, and sends the reason, if any, to the server's audit log.

Additional behavior can be specified in its configuration:
* forcereason (*boolean*) - Forces the reason to be specified if set to true. If none is specified, the action is not taken. Defaults to *false*.
* purgedays (*integer*) - The number of days, between 0 and 7 inclusive, of the target's post history to erase during banning. Defaults to *0*.
* successmsg (*string*) - Custom message to display on a successful ban. If not specified, a default message is used.
  * The string *$target* can be used in the value to represent the ban target.
* notifymsg (*string*) - A message to send to the target user prior to banning, informing the user of the action taking place.
  * The strings *$s* and *$r* can be used in the value to represent the server name and ban reason, respectively.
  * Uses a default message if this configuration value is not specified.
  * To disable, specify a blank value.

#### Configuration Reload
* `"type": "confreload"`
* Usage: (*command*)
Reloads server configuration. The bot will reply indicating if the reload was successful.

#### Kick
* `"type": "kick"`
* Usage: (*command*) (*user name or ID*) [*reason*]
Removes the given user from the server the command was invoked in and sends the reason, if any, to the server's audit log.

Additional behavior can be specified in its configuration:
* forcereason (*boolean*) - Forces the reason to be specified if set to true. If none is specified, the action is not taken. Defaults to *false*.
* successmsg (*string*) - Custom message to display on a successful ban. If not specified, a default message is used.
  * The string *$target* can be used in the value to represent the ban target.
* notifymsg (*string*) - A message to send to the target user prior to banning, informing the user of the action taking place.
  * The strings *$s* and *$r* can be used in the value to represent the server name and ban reason, respectively.
  * Uses a default message if this configuration value is not specified.
  * To disable, specify a blank value.

#### Role manipulation
* `"type": "addrole"` or `"type": "delrole"
* Usage: (*command*) (*user name or ID*)
Sets or unsets a predefined role upon the given user.

Additional configuration:
* role (*string*) - The role that applies to this command. May be defined in the same type of format accepted within [entity lists](entitylist.html).
* successmsg (*string*) - Custom message to display on success. If not specified, a default message is used.
  * The string *$target* can be used in the value to represent the command target.

#### Say
* `"type": "say"`
* Usage: (*command*) (*channel name or ID*) (*message*)
Causes the bot to send the given message exactly as specified to the given channel.