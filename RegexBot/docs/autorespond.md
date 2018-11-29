## AutoRespond

AutoRespond is a component that deals exclusively with responding to text triggers. An AutoRespond *definition* takes one or more regular expression patterns, a response string, and optionally several other options for adjusting the definition's behavior.

This may seem like a redundant feature, given that the same can be accomplished with AutoMod. However, this feature spares you of having to insert special cases within AutoMod rules that would prevent all users from having equal access to triggers. Additionally, it allows for extra features that would not be possible in AutoMod, such as rate limiting.

Sample within a [server definition](serverdef.html):
```
"AutoRespond": {
    "Help command": {
        "regex": "^!help$",
        "reply": "You can't be helped. Try again in 45 minutes.",
        "ratelimit": 2700
    },
    "Spam trigger": {
        "regex": [
            "spam post",
            "dumb",
            "productive conversation"
        ],
        "exec": "python /home/bot/did-someone-say-botspam.py",
		"RandomChance": 0.5
    }
}
```

### Definition structure
The following is a list of accepted members within an AutoRespond definition:
* regex (*string* or *string array*) - **Required.** Regular expression pattern(s) that will invoke the response.
* reply (*string*) - The message to send out to the channel in which the response was invoked.<sup>1</sup>
* exec (*string*) - Command line path and optional parameters to an external program. The program's output will be sent to the channel in which the response was invoked.<sup>1</sup>
* ratelimit (*integer*) - The amount of time in seconds in which the response may not be triggered again within the same channel. Defaults to 20.
* RandomChance (*number*) - A value between 0 and 1 representing the percent chance for the bot to respond to the corresponding trigger. Defaults to 1.0 (100%).

<sup>1</sup> It is **required** to have either *reply* or *exec* specified in a definition.