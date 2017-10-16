# Documentation
On its own, RegexBot has no default behavior. It is up to the user to define everything, including triggers and commands for use by moderators.

Configuration takes the form of a JSON file name `settings.json`. Within this file should be one object, which itself contained named values.

[Sample file for quick reference.](sample.html)

The following is a list of all accepted values with links to pages explaining them in more detail.
* bot-token *(string)* - **Required.** Discord token used for connecting to the service.
* playing *(string)* - String to display as the bot's "now playing" status message.
* servers *(array)* - Takes an array of unnamed [server definition](serverdef.html) objects.