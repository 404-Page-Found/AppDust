# AppDust

AppDust is a Windows-first cleanup CLI for ProgramData, per-user AppData (Local, LocalLow, Roaming), temp folders, and common leftover files. The default operating model is preview first, quarantine second, restore if needed.

See also:

- [Project overview](../README.md)
- [简体中文说明](README.zh_CN.md)

## Requirements

- Windows.
- .NET 10 SDK to build or run from source.
- An elevated administrator session for `--all-users`. Scheduled task creation or removal may also require elevation.

## Build From Source

```powershell
dotnet restore AppDust.sln
dotnet build AppDust.sln
dotnet test AppDust.sln
```

## Recommended Workflow

1. Run `scan` or `clean --dry-run` to preview candidates and optionally write a JSON report.
2. Run `clean` to quarantine files with the selected profile.
3. Use `restore list` to enumerate available quarantine runs.
4. Use `restore --run-id <id>` if a cleanup run needs to be rolled back.
5. Use `report --path` or `report --run-id` to inspect stored JSON output.

## Command Reference

| Command | Purpose | Notes |
| --- | --- | --- |
| `scan` | Build a cleanup plan without touching files. | Supports JSON output and report generation. `--all-users` requires elevation. |
| `clean` | Execute the cleanup plan. | Quarantine is the default. Permanent deletion requires `--delete-permanently --force`. `--dry-run` behaves like a scan preview. |
| `restore list` | Enumerate saved quarantine manifests. | Useful before choosing a run to restore. Supports `--json`. |
| `restore --run-id` | Restore one cleanup run from quarantine. | Can write a restore report and emit JSON to stdout. |
| `report` | Print a saved JSON report or quarantine manifest. | Use `--path` for a specific file or `--run-id` to resolve a manifest under the quarantine root. |
| `schedule create` | Register a daily Windows scheduled task. | Runs `clean --profile <path>`. Defaults to `02:00` if `--daily` is omitted. |
| `schedule list` | Show matching scheduled tasks. | Without `--name`, AppDust task names are listed. |
| `schedule remove` | Delete a scheduled task. | Task names are normalized to the `AppDust-` prefix. |

Exact CLI syntax:

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
| `profiles/default.json` | Current user | Quarantine | 24 hours | Mirrors the built-in defaults, including crash-dump cleanup. |
| `profiles/safe-daily.json` | Current user | Quarantine | 48 hours | Disables crash-dump cleanup for lower-risk scheduled runs. |

## Default Paths And Output

- If `--report` is omitted, AppDust writes reports under `%LocalAppData%\AppDust\Reports`.
- If `--quarantine-root` is omitted, quarantine data is stored under `%LocalAppData%\AppDust\Quarantine`.
- `report --run-id <id>` prints the quarantine manifest for that run.

## Examples

Preview a scan and save the JSON report:

```powershell
dotnet run --project src/AppDust.Cli -- scan --profile profiles/default.json --report artifacts/scan.json
```

Run a safer quarantine-based cleanup:

```powershell
dotnet run --project src/AppDust.Cli -- clean --profile profiles/safe-daily.json --report artifacts/clean.json
```

Inspect restore candidates before rolling anything back:

```powershell
dotnet run --project src/AppDust.Cli -- restore list
```

Create a daily scheduled cleanup at 02:00:

```powershell
dotnet run --project src/AppDust.Cli -- schedule create --name DailySafe --profile profiles/safe-daily.json --daily 02:00
```
