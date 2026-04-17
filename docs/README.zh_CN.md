# AppDust

AppDust 是一个面向 Windows 的清理 CLI，用于扫描 ProgramData、每个用户的 AppData（Local、LocalLow、Roaming）、临时目录以及常见残留文件。默认工作流是先预览、后隔离，必要时再恢复。

另请参阅：

- [项目概览](../README.md)
- [English reference](README.en_US.md)

## 运行要求

- Windows。
- 从源码构建或运行需要 .NET 10 SDK。
- 使用 `--all-users` 必须在提升权限的管理员会话中运行。创建或删除计划任务通常也需要管理员权限。

## 从源码构建

```powershell
dotnet restore AppDust.sln
dotnet build AppDust.sln
dotnet test AppDust.sln
```

## 推荐工作流

1. 先运行 `scan` 或 `clean --dry-run` 预览候选文件，并按需输出 JSON 报告。
2. 再运行 `clean`，按照所选配置文件将文件隔离。
3. 使用 `restore list` 查看可恢复的隔离运行记录。
4. 如果需要回滚，使用 `restore --run-id <id>` 恢复指定运行。
5. 使用 `report --path` 或 `report --run-id` 查看保存的 JSON 输出。

## 命令参考

| 命令 | 用途 | 说明 |
| --- | --- | --- |
| `scan` | 生成清理计划，但不改动文件。 | 支持 JSON 输出和报告写入。`--all-users` 需要管理员权限。 |
| `clean` | 执行清理计划。 | 默认模式是隔离。永久删除必须显式传入 `--delete-permanently --force`。`--dry-run` 的行为等同于预览扫描。 |
| `restore list` | 枚举已保存的隔离清单。 | 在选择恢复运行前很有用，支持 `--json`。 |
| `restore --run-id` | 从隔离区恢复一次清理运行。 | 可写入恢复报告，也可将 JSON 输出到标准输出。 |
| `report` | 打印已保存的 JSON 报告或隔离清单。 | 使用 `--path` 读取指定文件，或使用 `--run-id` 按隔离根目录解析清单。 |
| `schedule create` | 注册一个每日运行的 Windows 计划任务。 | 实际执行 `clean --profile <path>`。如果省略 `--daily`，默认时间为 `02:00`。 |
| `schedule list` | 显示匹配的计划任务。 | 未指定 `--name` 时，会列出 AppDust 相关任务。 |
| `schedule remove` | 删除计划任务。 | 任务名会自动规范为 `AppDust-` 前缀。 |

精确 CLI 语法如下：

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

## 内置示例配置

| 配置文件 | 范围 | 模式 | 最小文件年龄 | 说明 |
| --- | --- | --- | --- | --- |
| `profiles/default.json` | 当前用户 | 隔离 | 24 小时 | 对应内置默认行为，包含崩溃转储清理。 |
| `profiles/safe-daily.json` | 当前用户 | 隔离 | 48 小时 | 关闭崩溃转储清理，更适合低风险的日常定时运行。 |

## 默认路径与输出

- 如果未提供 `--report`，AppDust 会将报告写入 `%LocalAppData%\AppDust\Reports`。
- 如果未提供 `--quarantine-root`，隔离数据会写入 `%LocalAppData%\AppDust\Quarantine`。
- `report --run-id <id>` 会打印该运行对应的隔离清单。

## 示例

预览扫描并保存 JSON 报告：

```powershell
dotnet run --project src/AppDust.Cli -- scan --profile profiles/default.json --report artifacts/scan.json
```

执行一个更保守的隔离清理：

```powershell
dotnet run --project src/AppDust.Cli -- clean --profile profiles/safe-daily.json --report artifacts/clean.json
```

在恢复前先查看可用的隔离运行：

```powershell
dotnet run --project src/AppDust.Cli -- restore list
```

创建一个每天 02:00 运行的计划清理任务：

```powershell
dotnet run --project src/AppDust.Cli -- schedule create --name DailySafe --profile profiles/safe-daily.json --daily 02:00
```
