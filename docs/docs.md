# Documentation

RegexBot has no default behavior of its own. It is up to the bot operator to define all aspects of its function, and this is all done by means of a configuration file.

Configuration takes the form of a [JSON](https://json.org/) file residing in the same directory as the executable, named `settings.json`. The sample provided below should give you an idea of how the file is structured. Additionally, the JSON parser used by RegexBot allows for comments within the file.

Sample configuration file:
```
{
    "bot-token": "AbCDEf-gh1JKLmn0pqR$Tu.vwXyZ",
    "playing": "with my preferred text editor", // not saying which!
    "servers": [
        {
            // server definition(s) defined here
        }
    ]
}
```
### Structure
The following is a list of all accepted members at the top level of the configuration file. Links given below and in subsequent pages will further explain additional values that are allowed within them.

Value names are **case sensitive**. Keep this in mind, as case is not consistent throughout the list of member names. This will be corrected in future updates.

* bot-token (*string*) - Discord token for connecting to the service. **Required.**
* playing (*string*) - Optional text to display for the bot's status message.
* servers (*array*) - Takes an array of unnamed [server definition](serverdef.html) objects.