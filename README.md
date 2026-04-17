# AppDust

AppDust scans and cleans Windows ProgramData, per-user AppData (Local, LocalLow, Roaming) and common temp/leftover files with safe defaults and a preview-first workflow. Built for system administrators and power users, AppDust provides detailed reports, dry-runs, quarantining, selective rules and scheduled runs so you can reclaim disk space without accidentally removing needed files.

## Status

The repository now contains an initial C# implementation targeting .NET SDK 10.0:

- `src/AppDust.Core` provides scan planning, cleanup rules, quarantine and restore, JSON report writing, and profile loading.
- `src/AppDust.Cli` provides the Windows CLI surface for scan, clean, restore, report, and schedule commands.
- `tests/AppDust.Core.Tests` contains initial rule-engine and quarantine round-trip coverage.

## Build

Install the .NET 10 SDK, then run:

```powershell
dotnet restore AppDust.sln
dotnet build AppDust.sln
dotnet test AppDust.sln
```

## Commands

```text
AppDust scan [--profile path] [--report path] [--json] [--all-users] [--current-user] [--min-age-hours N] [--exclude-path path1;path2] [--no-crash-dumps]
AppDust clean [--profile path] [--report path] [--quarantine-root path] [--delete-permanently --force] [--dry-run] [--no-crash-dumps]
AppDust restore --run-id id [--quarantine-root path] [--report path] [--json]
AppDust restore list [--quarantine-root path] [--json]
AppDust report --run-id id [--quarantine-root path]
AppDust report --path path
AppDust schedule create --name task --profile profile.json [--daily HH:mm]
AppDust schedule list [--name task]
AppDust schedule remove --name task
```

## Profiles

Sample JSON profiles are available in `profiles/`:

- `profiles/default.json` mirrors the current default cleanup behavior.
- `profiles/safe-daily.json` is intended for scheduled daily runs and disables crash-dump cleanup.

`--all-users` is supported, but the current process must be running in an elevated administrator session before AppDust will start the scan.

Create a report-only preview:

```powershell
dotnet run --project src/AppDust.Cli -- scan --profile profiles/default.json --report artifacts/scan.json
```

Run a safe cleanup to quarantine:

```powershell
dotnet run --project src/AppDust.Cli -- clean --profile profiles/safe-daily.json --report artifacts/clean.json
```

Create a daily scheduled task at 02:00:

```powershell
dotnet run --project src/AppDust.Cli -- schedule create --name DailySafe --profile profiles/safe-daily.json --daily 02:00
```

List available quarantine runs before restoring:

```powershell
dotnet run --project src/AppDust.Cli -- restore list
```
