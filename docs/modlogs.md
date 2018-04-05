## ModLogs

ModLogs is a work in progress and not all features are yet available.
When completed, it will be the component that records certain information and notifies moderators of actions on the server deemed important enough to show as they happen.

Sample within a [server definition](serverdef.html):
```
"ModLogs": {
	"AutoReporting": {
		"Channel": "#99999999:mod-events",
		"Events": "msgedit,msgdelete",
		"CacheIgnore": 1230000000000
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
* WebhookUrl (*string*) - **Required.** A webhook URL to be used by the bot for sending events.
* Events (*string*) - **Required** at the moment. A comma-separated list of event types to be sent to the reporting channel.
* CacheIgnore (*number*) - Channel (ID) to ignore for MsgEdit and MsgDelete autoreporting. (Optional.)
  * It is **highly recommended** that the reporting channel be specified, otherwise deleting a report within it will cause another report to appear.

#### Event types
All events fall into one of a number of categories.
* Custom - The catch-all term for all event types that are not built in, created either by an AutoMod response or an external module.
* (name) - (description)

Additionally, the following event types are also valid only for `AutoReporting` and are otherwise not logged:
* MsgEdit - Message was edited by the message author.
* MsgDelete - Message was deleted either by the message author or another user.