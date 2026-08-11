# Logging

The engine configures two logging providers, both registered in `EngineExtensions.UseEngine`. All other providers (console, debug, etc.) are cleared.

## DailyFileLoggerProvider

Writes all log output to a daily rotating file and to stdout.

**File location**: `{AppDataPath}/Logs/yyyy-MM-dd.log`

**Log format**:
```
[dd-MMM-yyyy HH:mm.fff] [LEVEL] [CATEGORY] [USER] message
```

Example:
```
[30-JUL-2026 14:23.051] [INFO] [APP] [MYUSER] Engine started
[30-JUL-2026 14:23.102] [ERROR] [ACTIVITY] [MYUSER] Failed to load: System.IO.IOException: ...
```

Level tokens: `INFO` for `Information`; all others uppercased (`DEBUG`, `WARNING`, `ERROR`, `CRITICAL`).

`USER` is the current value of `CurrentUserProvider.UserName`, or `----` before a user is loaded.

**Multi-process safety**: The provider uses a named Windows Mutex (`Local\PCLog_{md5(logdir)}`) so that multiple processes writing to the same log directory serialize their writes. File is opened with `FileShare.ReadWrite` so all processes can hold handles simultaneously. Within a single process, a `lock` serializes all loggers (one per category) through a single shared `StreamWriter`.

**Day rollover**: The `StreamWriter` is reopened on the first write of a new day.

## ActivityLoggerProvider

Writes structured activity events to the LiteDB database. Active in `Client` mode only.

**Filter**: only handles the `"ACTIVITY"` log category at `LogLevel.Information` and above. All other log calls are no-ops.

**Storage**: appends one `ActivityLogEntry { At, Message }` to today's `ActivityLogEntity` via `ActivityLogRepository.AppendEventAsync`. Creates the day's record if it doesn't exist.

## Usage

Log categories follow the existing convention:

| Category | Usage |
|----------|-------|
| `"APP"` | General application events (startup, install, errors) |
| `"ACTIVITY"` | User-visible activity events written to the in-app log |

Any other category string is valid and will appear in the file log.

```csharp
// Inject ILoggerFactory, then:
var logger = loggerFactory.CreateLogger("APP");
logger.LogInformation("Engine started");

var activityLogger = loggerFactory.CreateLogger("ACTIVITY");
activityLogger.LogInformation("Message sent to {User}", userName);
```
