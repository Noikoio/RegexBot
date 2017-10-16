##AutoMod

AutoMod takes some inspiration from Reddit's [Automoderator](https://www.reddit.com/wiki/automoderator). It allows the user to define one or more *rules* based on regular expression (regex) patterns, in order for the bot to execute a *response* when triggered. This was initially the main and only feature of this bot, hence the name RegexBot.

AutoMod rules are defined per-server within an object named `automod`. Each rule is defined as a name/value pair, with the name serving as its label.

Sample AutoMod rules:
```
"Delete bilingual pirates": {
    "regex": [ "pira(te|cy)", "pirat(a|ería)", ],
    "response": "delete"
},
"Selective trigger": {
    "regex": "secret",
    "response: [
        "delete",
        "say #_ Don't say the s word, @_!"
    ],
    whitelist: { "channels": [ "#dont-say-secret" ] }
}
```

#### Rules
The following is a list of accepted members within an AutoMod rule:
* regex (*string* or *string array*) - **Required.** Regular expression pattern(s) that trigger the defined rule.
* response (*string* or *string array*) - **Required.** Response, or list of responses to execute.
  * See the section below for more information on responses.
* whitelist *[(entity list)](entitylist.html)* - Entities to which the rule exclusively applies to.
* blacklist *[(entity list)](entitylist.html)* - Entities to which the rule does not apply to.
* exempt *[(entity list)](entitylist.html)* - Entities which are exempt from the whitelist or blacklist rules.
  * For example: If a particular role is blocked from triggering the rule, you may add an exemption for a single user within that role to be able to trigger the rule.
* AllowModBypass *(boolean)* - Specifies if those defined within the *moderators* list for the server should be exempt from triggering this rule. Defaults to *true*.

#### Responses
(to be written later)