## Database settings

RegexBot can make use of PostgreSQL to store certain information. Some components such as ModLogs requires database configuration to be present to function. Many other components make use of the database for caching of usernames.

Sample within [configuration](docs.html):
```
"database": {
	"database": "rbproduction",
	"username": "regexbot",
	"password": "a good password should go here"
}
```
### Values
The following values are accepted within the database object:
* hostname (*string*) - IP or hostname for establishing a connection. Defaults to localhost.
* database (*string*) - Name of database to use.
* username (*string*) - Name of PostgreSQL role (user).
* password (*string*) - Password value for the given PostgreSQL role. Only password authentication is supported.
Excluding `hostname`, all values are **required**.

### Remarks
RegexBot assumes that the PostgreSQL role it uses has full access to the given database. It will create all tables, indexes, etc. automatically on load. Should the SQL server go down during bot operation, the bot must be restarted.