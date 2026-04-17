# AppDust

AppDust scans and cleans Windows ProgramData, per-user AppData (Local, LocalLow, Roaming) and common temp/leftover files with safe defaults and a preview-first workflow. Built for system administrators and power users, AppDust provides detailed reports, dry-runs, quarantining, selective rules and scheduled runs so you can reclaim disk space without accidentally removing needed files.

## CLI Snapshot

The repository currently includes a Windows-first .NET 10 CLI implementation with these commands:

```text
AppDust scan [--profile path] [--report path] [--json] [--all-users] [--current-user] [--min-age-hours N] [--exclude-path path1;path2] [--no-crash-dumps]
AppDust clean [--profile path] [--report path] [--quarantine-root path] [--delete-permanently --force] [--dry-run] [--no-crash-dumps]
AppDust restore list [--quarantine-root path] [--json]
AppDust restore --run-id id [--quarantine-root path] [--report path] [--json]
AppDust report --run-id id [--quarantine-root path]
AppDust report --path path
AppDust schedule create --name task --profile profile.json [--daily HH:mm]
AppDust schedule list [--name task]
AppDust schedule remove --name task
```

## Included Profiles

- `profiles/default.json` keeps the default behavior.
- `profiles/safe-daily.json` disables crash-dump cleanup for lower-risk scheduled runs.

`--all-users` requires an elevated administrator session.

## Build

```powershell
dotnet restore AppDust.sln
dotnet build AppDust.sln
dotnet test AppDust.sln
```
