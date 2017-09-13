---
title: RegexBot
---
# Introduction

Perhaps you already have all the bots you need in your Discord server, configured just the right way to deal with day-to-day activity. But have you ever wanted to modify *that one thing* in order to finally make things perfect?

This project is an answer to that. RegexBot is a moderation bot that does nothing on its own. All aspects of its behavior are defined in its configuration. What this means is that each instance of RegexBot is uniquely suited to the server(s) it is configured to run on.

# Running the bot
There are no plans for the developer to run a public bot at the moment. There is no "main" instance of RegexBot. Self-hosting is necessary.

## Prerequisites
Unfortunately, precompiled executables are not yet available, so you'll need to compile it. To do so, you'll need the following installed on the device that will run the bot:

* Git
* [.NET Core SDK](https://www.microsoft.com/net/core) >= 2.0.0

In addition to that, you will need a bot token. To do so, [create an app in Discord](https://discordapp.com/developers/applications/me) and convert it into a Bot User. Make a note of the token and Client ID. **Do not share your bot token or put it in a public place.**

## Build the bot
With the prerequisites installed, run the following commands:
```
$ git clone git@github.com:Noikoio/RegexBot.git
$ cd RegexBot
$ dotnet public -c Release -o output
```
You may see some warnings during this last step, but the build should still succeed. These warnings will be corrected in future releases.

At this point, the bot has been compiled and all dependencies placed in the `output` directory. You may run the bot at this point by issuing the following:
```
$ cd output
$ dotnet RegexBot.dll
```

You may place the output directory wherever it's most convenient for you.

## Configure
The bot will not function without a configuration file.