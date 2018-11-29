---
title: RegexBot
---
# Introduction
Perhaps you've already found a number of bots that cover your needs, configured in just the right way to handle the day-to-day activity of your Discord server. Yet, have you ever wanted to be able to change *that one detail* to finally make things perfect?

RegexBot offers a blank slate. It does nothing on its own and leaves every aspect of its behavior to its configuration. This means each instance of RegexBot is uniquely suited to the server(s) it is configured to run on.

# Features
* Define custom actions to perform in response to regular expression patterns
* Supports a handful of commands for making moderation easier
* Run external scripts and send the output to a text channel

# Running the bot
At the moment, there is no public instance of RegexBot that is ready and available to be sent to other servers. It has been considered, but is currently not a priority. Hosting your own instance will be necessary.

### Download
A link to all available downloads is availabe at the top of the page, through the "Releases" link. You may choose to either download a pre-compiled binary or compile it from source. See below for detailed instructions.

If using a pre-compiled binary, keep in mind the requirements:
* Windows: Windows 7 SP1, Windows 8.1, Windows 10 1607+ (Anniversary Update)
* macOS: Mac OS X 10.12 (Sierra) or higher
* Linux: Most major distributions supported. Dependencies required.
  * List of dependencies can be found [here](https://docs.microsoft.com/en-us/dotnet/core/linux-prerequisites?tabs=netcore2x).

Extract the contents to a folder, create a configuration file (see top of page, Documentation) and then run `RegexBot.exe` (Windows) or `./RegexBot` (Linux, macOS).

### Make a bot account
You will need to get a bot token. To do so, [create an app in Discord](https://discordapp.com/developers/applications/me) and convert it into a Bot User. Make a note of the token and Client ID. **Do not share this token under any circumstances.**

Once you have created your Discord app, go to the following URL after inserting your client ID where specified: `https://discordapp.com/oauth2/authorize?client_id=CLIENT_ID&scope=bot`.

### Configure
When starting or reloading, RegexBot looks for a file in its current directory named `settings.json`. The configuration file is too complex to explain simply here. [Check the documentation](docs.html) to find a sample configuration file, as well as more detailed information.

# Compiling from source
### Prerequisites
The following must be installed:
* Git
* [.NET Core SDK](https://www.microsoft.com/net/core) 2.0 or later

### Build
With the prerequisites installed, run the following commands:
```
$ git clone {{ site.github.clone_url }}
$ cd RegexBot
$ dotnet publish -c Release -o output
```
You may see some warnings during this last step, but the build should still succeed. These warnings will be corrected in future releases.

At this point, the bot has been compiled and all dependencies placed in the `output` directory. The bot may be run by issuing the following:
```
$ cd output
$ dotnet RegexBot.dll
```
You may move and rename the output directory at your convenience.