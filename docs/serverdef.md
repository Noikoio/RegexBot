## Server definition

Server definitions are defined within the `servers` array. Each definition represents unique configuration values for a single server. It is possible to specify multiple servers, thus allowing for a single bot instance to be used in several servers at once.

Sample definition within [configuration](docs.html):
```
servers: [
    {
        "id": 100000000000,
        "name": "Fun Server",
        "autoresponses": {
            // ...
        }
    },
    {
        "id": 100000000001,
        "name": "Serious Business",
        "automod": {
            // ...
        }
    }
]
```

The following is a list of accepted members within a server definition.
* id (*integer*) - **Required.** A value containing the server's [unique ID](https://support.discordapp.com/hc/en-us/articles/206346498-Where-can-I-find-my-User-Server-Message-ID-).
* name (*string*) - Preferably a readable version of the server's name. Not used for anything other than internal logging.
* moderators (*[entity list](entitylist.html)*) - A list of entities to consider as moderators. Actions done by members of this list are able to execute *ModTools* commands and are exempt from certain *AutoMod* rules. See their respective pages for more details.
* [automod](automod.html) (*name/value pairs*) - See respective page.
* [autoresponses](autorespond.html) (*name/value pairs*) - See respective page.
* [ModCommands](modcommands.html) (*name/value pairs*) - See respective page.