## VoiceRoleSync

VoiceRoleSync directs the bot to apply specific roles to users when they are listening in to a voice channel.

Sample within a [server definition](serverdef.html):
```
"VoiceRoleSync": {
	//"1000001::General": "1000002::In General VC",
	//"1000003::Video Games": "1000004::In Gaming VC"
	"1000001": "1000002",
	"1000003": "1000004"
}
```

### Definition structure
A list of voice channels and their respective roles are defined in a `VoiceRoleSync` object. They are defined as JSON properties, with the name portion (before the colon) representing the voice channel and the value portion (after the colon) representing the role to be associated.

At the moment, only voice channel and role ID values are accepted in this configuration. EntityList-like names will be supported at a later date.