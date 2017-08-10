# RegexBot
RegexBot is a standalone Discord moderation bot that makes use of the [Discord.Net](https://github.com/RogueException/Discord.Net) library.

The goal for this project is to provide the most useful bot that fits the exact needs for your Discord server. It should do exactly what it needs to do by your needs. No more, and no less.

As of the time this file was last updated, the following features are supported:
* Regular expression matching
  * Able to define one or more actions to take on a match
    * Send message to channel or user
    * Report to channel or user
    * Add or remove a role
    * Delete the message
    * Ban the user
    * Run an external program and send the output to a user or channel
  * Matching constraints
    * Minimum/maximum message length constraints
    * Whitelisting and blacklisting per-user, per-role, and/or per-channel
      * Exemptions to whitelisting and blacklisting defined per-user, per-role, and/or per-channel
    * Match embed content instead of message content
* Mod commands
  * Custom command names
  * Multiple aliases for the same command
  * Per-command, per-alias options
    * Optionally force your moderators to specify reasons for certain actions
      * And place them in the Audit Log
    * Configure how many days of post history to remove when banning

## Running your own
(to be written later)

## Documentation
(coming soon?)