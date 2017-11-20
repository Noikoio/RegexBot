## AutoMod

AutoMod is a component that takes inspiration from Reddit's [Automoderator](https://www.reddit.com/wiki/automoderator). It allows the user to define one or more *rules* based on regular expression (regex) patterns. When the rule is matched, the bot will proceed to to execute a *response*. This was initially the main and only feature of this bot, hence its name RegexBot.

AutoMod rules are defined per-server within the `automod` object. Each rule is defined as a name/value pair, with the name serving as its label.

Sample AutoMod rules:
```
"Delete bilingual pirates": {
    "regex": [ "pira(te|cy)", "pirat(a|ería)", ],
    "response": [
        "delete"
        "report #0000000::mod-queue"
    ]
},
"Selective trigger": {
    "regex": "secret",
    "response: "say #_ Don't say the s word, @_!",
    whitelist: { "channels": [ "#dont-say-secret" ] }
}
```

### Rule structure
The following is a list of accepted members within an AutoMod rule:
* regex (*string* or *string array*) - **Required.** Regular expression pattern(s) that trigger the defined rule.
* response (*string* or *string array*) - **Required.** Response, or list of responses to execute.
  * See the section below for more information on responses.
* whitelist *[(entity list)](entitylist.html)* - Entities to which the rule exclusively applies to.
* blacklist *[(entity list)](entitylist.html)* - Entities to which the rule does not apply to.
* exempt *[(entity list)](entitylist.html)* - Entities which are exempt from whitelist or blacklist rules.
  * For example: It would allow for a specific user to trigger a rule, despite being a member of a blocked role.
* AllowModBypass *(boolean)* - Specifies if those defined within the *moderators* list for the server should be exempt from triggering this rule. Defaults to *true*.

### Responses
Responses are the actions executed when a rule is matched. They take the form of one or more strings defined within a single rule. Defining a response could be considered to be similar to typing out a command, with particular responses requiring a number of parameters.

The following responses are currently implemented:
* ban - Immediately bans the user that triggered the rule.
* kick - Immediately kicks the user that triggered the rule.
* remove - Removes the message that triggered the rule.
  * Aliases: delete
* report *target_entity* - Sends a copy of the message that triggered the rule to the specified *target*.
* grantrole *target_user* *role_ID* - Adds the *target_user* to the given role defined by *role_ID*.
  * Aliases: addrole
* revokerole *target_user* *role_ID* - Removes the *target_user* from the given role defined by *role_ID*.
  * Aliases: delrole, removerole
* say *target_entity* *message* - Sends *message* to the given *target_entity*.
  * Aliases: send

For responses that support parameters, it is possible to specify the matching user or the channel in which the match occurred as a parameter. This is done using `@_` and `#_`, respectively. Additionally it is possible to define a user or channel in the same way as items in an entity list, by using either the entity's ID, name, or both.