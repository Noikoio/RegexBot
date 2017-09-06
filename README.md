# RegexBot
RegexBot is a standalone Discord moderation bot that makes use of the
[Discord.Net](https://github.com/RogueException/Discord.Net) library.

The goal of this project is to provide a bot that can truly fit your unique needs in managing Discord server.
To that end, many aspects of the bot's behavior can be configured and fine-tuned, down to how it responds to
rules that you have implemented.

Are you satisfied with your current bot but wish that you could change *that one thing* to better suit your
needs? This project is an answer to that.

## Features
As of the time of this writing, the following features are supported:
* Automoderator-like expression matching and responses
  * Able to define one or more actions on a match
    * Send message to any channel or user
	* Report message to channel or user
	* Add or remove a role
	* Delete the message
	* Ban the user
  * Able to define extra constraints
    * Case sensitivity
	* Minimum or maximum message length
	* Match only for certain users, or within certain channels
	  * Or match everyone and everything *except* those
* Easy configuration for adding fun commands and autoresponses
  * Can also execute external scripts for dynamic responses
* Customizable mod-only commands
  * Custom command names
  * Multiple aliases
  * Per-command, per-alias options
    * Optionally force your moderators to specify reasons for certain actions
      * And place them in the Audit Log
    * Configure how many days of post history to remove when banning

## Running your own
(to be written later)

## Documentation
(coming soon?)
