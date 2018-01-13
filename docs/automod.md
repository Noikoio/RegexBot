## AutoMod

AutoMod is a component that takes inspiration from Reddit's [Automoderator](https://www.reddit.com/wiki/automoderator). It was the original feature of RegexBot. It allows the operator to define one or more *rules* based on regular expression (regex) patterns. When a particular rule is matched, the bot executes the appropriate *response*.

AutoMod is set up by defining rules within a JSON object named `automod` within a server definition. Rules are defined by means of name/value pairs, with the name serving as its label.

Sample within a [server definition](serverdef.html):
```
"automod": {
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
}
```

### Rule structure
The following is a list of accepted members within an AutoMod rule:
* regex (*string* or *string array*) - **Required.** Regular expression pattern(s) that trigger the defined rule.
* response (*string* or *string array*) - **Required.** Response, or list of responses to execute.
  * See the section below for more information on defining responses.
* whitelist (*[entity list](entitylist.html)*) - Entities to which the rule exclusively applies to.<sup>1</sup>
* blacklist (*[entity list](entitylist.html)*) - Entities to which the rule does not apply to.<sup>1</sup>
* exempt (*[entity list](entitylist.html)*) - Entities which are exempt from whitelist or blacklist rules.<sup>2</sup>
  * For example: It would allow for a specific user to trigger a rule, despite being a member of a blocked role.
* AllowModBypass *(boolean)* - Specifies if those defined within the *moderators* list for the server should be exempt from triggering this rule. Defaults to *true*.

<sup>1</sup> A rule may either contain a whitelist or blacklist, but not both.
<sup>2</sup> Used only if a whitelist or blacklist have been specified.

### Responses
A response is the action, or list of actions, to be executed when a rule is matched. Defining a response could be considered to be similar to typing out a command or a batch script, with certain responses requiring a number of parameters.

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

In regards to responses that accept *target* parameters, it is possible to specify the target to be the user who triggered the rule or the channel in which the rule was triggered in. This is done by specifying the parameter as `@_` or `#_`, respectively. The sample above shows an example of this.

Additionally, targets may be defined in the same type of format accepted within [entity lists](entitylist.html). This is also shown in the above example.