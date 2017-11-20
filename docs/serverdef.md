## Server definition

Server definitions are defined within the `servers` array. Each definition represents unique configuration for a single server. Defining multiple servers allows for a single bot instance to be uniquely configured for use in several servers at once.

The following is a list of accepted members within a server definition.
* id *(integer)* - **Required.** A value containing the server ID, and optionally the name, of the server that this definition represents.
* name *(string)* - The server name. Only used during configuration (re)load to make logs more readable.
* moderators *[(entity list)](entitylist.html)* - A list of entities to consider as moderators. Actions done by the members of those in this list are able to execute *ModTools* commands and are exempt from certain *AutoMod* rules if a particular rule has its *AllowModBypass* setting set to *false*.
* [AutoMod](automod.html) *(name/value pairs)* - Auto-moderation matching and response definitions.
* [AutoResponses](autorespond.html) *(name/value pairs)* - Definitions for automatic responses.
* [ModTools](modtools.html) *(name/value pairs)* - Moderation command definitions.