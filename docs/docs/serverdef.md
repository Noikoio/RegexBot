## Server definition

Server definitions are defined within the `servers` array. Each definition represents unique configuration for a single server. Defining multiple servers allows for a single bot instance to be uniquely configured for use in several servers at once.

The following is a list of accepted members within a server definition.
* name *(string)* - **Required.** A string containing the server ID, and optionally the name, of the server that this definition represents.
  * If you wish to enter both a name and ID, you must first enter the ID, followed by two colon (:) characters, followed by the name. For example: `"285450825525927585::My Testing Server"`
* moderators *[(entity list)](entitylist.html)* - A list of entities to consider as moderators. Actions done by the members of those in this list are able to execute *ModTools* commands and are exempt from certain *AutoMod* rules if a particular rule has its *AllowModBypass* setting set to *false*.
* [AutoMod](automod.html) *(name/value pairs)* - Auto-moderation matching and response definitions.
* [AutoResponses](autorespond.html) *(name/value pairs)* - Definitions for automatic responses.
* [ModTools](modtools.html) *(name/value pairs)* - Moderation command definitions.