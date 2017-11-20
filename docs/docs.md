# Documentation

**Important note: Documentation is a work in progress and may be incomplete in some areas.**

RegexBot has no default behavior of its own. It is up to the bot operator to define all aspects of its function, down to its triggers and commands to be used by moderators.

Its configuration takes the form of a JSON file residing in the executable directory, named `settings.json`. Within this file should be one nameless JSON object, which itself should contain named values described near the bottom of this page. Additionally, the JSON parser treats text following double slashes as comments.

The following is a sample configuration file:
```
{
    "bot-token": "AbCDEf-gh1JKLmn0pqR$Tu.vwXyZ",
    "playing": "with my preferred text editor",
    "servers": [
        {
            // (server configuration goes here)
        }
    ]
}
```

The following is a list of all accepted values with links to pages explaining them in more detail.
* bot-token *(string)* - **Required.** Discord token used for connecting to the service.
* playing *(string)* - String to display as the bot's "now playing" status message.
* servers *(array)* - Takes an array of unnamed [server definition](serverdef.html) objects.