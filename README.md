# AppDust

AppDust is a Windows-first cleanup CLI for ProgramData, per-user AppData (Local, LocalLow, Roaming), temp folders, and common leftover files. The default workflow is preview first, quarantine second, restore if needed.

Detailed guides:

- [English reference](docs/README.en_US.md)
- [简体中文说明](docs/README.zh_CN.md)

## Why AppDust

- Preview-only scans before any deletion.
- Quarantine is the default cleanup mode; permanent deletion requires an explicit force path.
- Built-in restore and report commands make cleanup runs auditable and reversible.
- JSON profiles make scheduled and repeatable cleanup runs easier to standardize.

## Requirements

- Windows.
- .NET 10 SDK to build or run from source.
- An elevated administrator session for `--all-users`. Scheduled task creation or removal may also require elevation.

## Quick Start

```powershell
dotnet restore AppDust.sln
dotnet build AppDust.sln
dotnet test AppDust.sln
dotnet run --project src/AppDust.Cli -- scan --profile profiles/default.json --report artifacts/scan.json
```

Preview a cleanup without touching files:

```powershell
dotnet run --project src/AppDust.Cli -- clean --dry-run --profile profiles/default.json
```

Run a safer quarantine-based cleanup:

```powershell
dotnet run --project src/AppDust.Cli -- clean --profile profiles/safe-daily.json --report artifacts/clean.json
```

List available restore points:

```powershell
dotnet run --project src/AppDust.Cli -- restore list
```

## Command Snapshot

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

| Profile | Scope | Mode | Minimum age | Notes |
| --- | --- | --- | --- | --- |
| `profiles/default.json` | Current user | Quarantine | 24 hours | Mirrors the built-in defaults. |
| `profiles/safe-daily.json` | Current user | Quarantine | 48 hours | Disables crash-dump cleanup for lower-risk scheduled runs. |

If `--report` is omitted, AppDust writes reports under `%LocalAppData%\AppDust\Reports`.

If `--quarantine-root` is omitted, quarantine manifests are stored under `%LocalAppData%\AppDust\Quarantine`.

## Repository Layout

- `src/AppDust.Core` contains scan planning, cleanup rules, quarantine and restore logic, profile loading, and JSON reporting.
- `src/AppDust.Cli` contains the Windows CLI for scan, clean, restore, report, and schedule operations.
- `tests/AppDust.Core.Tests` contains coverage for the rule engine and quarantine round-trips.
- `profiles/` contains sample JSON profiles for default and scheduled-cleanup scenarios.
