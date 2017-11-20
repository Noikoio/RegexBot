## AutoRespond

AutoRespond is a component that exists exclusively to allow your bot to respond to certain text triggers. An AutoRespond *definition* takes one or more regular expression patterns, a response string, and optionally several other options for adjusting the definition's behavior.

Although it is possible to create auto-response rules using AutoMod, defining them in AutoRespond allows you to not have to worry about adding extra configuration to ensure it works properly for all users.

Sample AutoRespond definitions:
```
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
    "exec": "python /home/autospam.py"
}
```

The following is a list of accepted members within an AutoRespond definition:
* regex (*string* or *string array*) - **Required.** Regular expression pattern(s) that will invoke the response.
* reply *(string)* - The message to send out to the channel in which the response was invoked.
* exec *(string)* - Command line path and optional parameters to an external program. The program's output will be sent to the channel in which the response was invoked.
  * It is **required** to have one of either *reply* or *exec* in a definition.
* ratelimit *(integer)* - The amount of time in seconds in which the response may not be triggered again within the same channel. Defaults to 20.