## ModLogs

ModLogs is a work in progress and not all features are yet available.
When completed, it will be the component that records certain information and notifies moderators of actions on the server deemed important enough to show as they happen.

Sample within a [server definition](serverdef.html):
```
"ModLogs": {
	"AutoReporting": {
		"Channel": "#99999999:mod-events",
		"Events": "msgedit,msgdelete"
	}
}
```

### Definition structure
Behavior of the ModLogs component is defined within a JSON object named `ModLogs`. Omitting this section from a server definition will disable the component for the given server.

The following values can be defined within the `ModLogs` object:
* AutoReporting (*object*) - See below for details
* QueryCommand (*object*) - Unavailable; Work in progress

#### AutoReporting
As its name implies, the `AutoReporting` section allows the bot operator to configure automatic reporting of one or more events as they occur to a designated reporting channel. Omitting this section in configuration disables this function.

The following values are accepted within this object:
* Channel (*string*) - **Required.** The channel name in which to report events.
  * The channel ID is currently required to be specified (see [EntityList](entitylist.html)). This limitation will be removed in a future update.
* Events (*string*) - **Required** for now. A comma-separated list of event types to be sent to the reporting channel.

#### Event types
All events fall into one of a number of categories.
* Custom - The catch-all term for all event types that are not built in, created either by an AutoMod response or an external module.
* (name) - (description)

Additionally, the following event types are also valid only for `AutoReporting` and are otherwise not logged:
* MsgEdit - Message was edited by the message author.
* MsgDelete - Message was deleted either by the message author or another user.