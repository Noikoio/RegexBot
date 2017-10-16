---
title: RegexBot
---
# Introduction

Perhaps you already have all the bots you need, configured in just the right way to handle the day-to-day activity of your Discord server. But have you ever wanted to modify *that one thing* in order to finally make things perfect?

RegexBot is a moderation bot that on its own, does nothing. All aspects of its behavior are defined through configuration. This means that each instance of RegexBot is uniquely suited to the server(s) it is configured to run on.

# Running the bot
There is no public instance of RegexBot ready to be invited to servers, and there are no plans to do so. Self-hosting the bot will be necessary to make use of it.

### Download
Pre-compiled executables will be available soon. Until then, see below for instructions on how to compile the bot from source.

### Make a bot account
You will need to get a bot token. To do so, [create an app in Discord](https://discordapp.com/developers/applications/me) and convert it into a Bot User. Make a note of the token and Client ID. **Do not share your bot token or put it in a public place.**

Once you have created your Discord app, go to the following URL after inserting your client ID where specified: `https://discordapp.com/oauth2/authorize?client_id=CLIENT_ID&scope=bot`.

### Configure
When starting or reloading, RegexBot looks for a file in its current directory named `settings.json`. The configuration file is too complex to explain simply here. [Check the documentation](./docs) to find a sample configuration file, as well as more detailed information.

# Compiling from source
### Prerequisites
The following must be installed:
* Git
* [.NET Core SDK](https://www.microsoft.com/net/core) 2.0 or later

### Build
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
You may move and rename the output directory at your convenience.