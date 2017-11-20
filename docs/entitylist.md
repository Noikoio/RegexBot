## Entity list

An entity list is a JSON object with multiple values each containing arrays of strings. They are used in various places in the configuration to specify a number of users, roles, channels, or any combination thereof.

The following is a sample of an entity list:
```
{
    "users": [ "@000000000000::MyName", "That Guy Over There" ],
    "roles": [ "99999999999::Trusted", "Bots" ],
    "channels": [ "#378237823782::random", "#usual" ]
}
```

As can be seen, all entities defined may either be specified using their name, or otherwise have their unique ID along with a label, separated by two colon (:) characters. Additionally, the ID should be prefixed with `@` if referring to a user or `#` if referring to a channel.

Each individual property is optional within an entity list, and is not necessary if your configuration does not require it. For example, this is a valid definition for an empty entity list:
```
{ }
```

It is **strongly recommended** to use unique IDs when defining entities with names that could change at any given time, such as users. Certain servers that frequently change role and channel names also benefit from having those IDs specified.