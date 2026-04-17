using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Principal;
using System.Text.Json;
using AppDust.Core.Configuration;
using AppDust.Core.Execution;
using AppDust.Core.Models;
using AppDust.Core.Reporting;
using AppDust.Core.Rules;
using AppDust.Core.Scanning;
using AppDust.Core.Validation;

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("AppDust only supports Windows.");
    return 1;
}

return new AppDustCli().Run(args);

internal sealed class AppDustCli
{
    private readonly CleanupProfileLoader _profileLoader = new();
    private readonly QuarantineService _quarantineService = new();
    private readonly JsonReportWriter _reportWriter = new();
    private readonly ScanPlanner _scanPlanner = new(new WindowsPathResolver(), new CleanupRuleEngine());
    private readonly CleanupRequestValidator _requestValidator = new();
    private readonly CleanupExecutor _cleanupExecutor;

    public AppDustCli()
    {
        _cleanupExecutor = new CleanupExecutor(_quarantineService);
    }

    public int Run(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return 0;
        }

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "scan" => RunScan(args[1..]),
                "clean" => RunClean(args[1..]),
                "restore" => RunRestore(args[1..]),
                "report" => RunReport(args[1..]),
                "schedule" => RunSchedule(args[1..]),
                "help" or "--help" or "-h" => PrintAndReturnHelp(),
                _ => UnknownCommand(args[0])
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private int RunScan(string[] args)
    {
        var parsed = ParseArguments(args);
        var profile = BuildProfile(parsed);
        _requestValidator.ValidateScanScope(profile, IsElevatedAdministrator());
        var plan = _scanPlanner.CreatePlan(profile);

        string? reportPath = null;
        if (parsed.Has("report"))
        {
            reportPath = _reportWriter.WriteScanPlan(plan, parsed.Get("report"));
        }

        if (parsed.Has("json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(plan, JsonReportWriter.SerializerOptions));
            return 0;
        }

        Console.WriteLine($"Run ID: {plan.RunId}");
        Console.WriteLine($"Candidates: {plan.Candidates.Count}");
        Console.WriteLine($"Potential size: {ByteSizeFormatter.Format(plan.TotalBytes)}");

        foreach (var candidate in plan.Candidates.Take(10))
        {
            Console.WriteLine($"  {ByteSizeFormatter.Format(candidate.SizeBytes),10}  {candidate.OriginalPath}");
        }

        if (plan.Warnings.Count > 0)
        {
            Console.WriteLine($"Warnings: {plan.Warnings.Count}");
        }

        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            Console.WriteLine($"Report: {reportPath}");
        }

        return 0;
    }

    private int RunClean(string[] args)
    {
        var parsed = ParseArguments(args);
        if (parsed.Has("dry-run"))
        {
            return RunScan(args);
        }

        var profile = BuildProfile(parsed);
        _requestValidator.ValidateScanScope(profile, IsElevatedAdministrator());
        var plan = _scanPlanner.CreatePlan(profile);
        var mode = parsed.Has("delete-permanently") ? CleanupMode.PermanentDelete : profile.Mode;

        if (mode == CleanupMode.PermanentDelete && !parsed.Has("force"))
        {
            Console.Error.WriteLine("Permanent deletion requires --force.");
            return 2;
        }

        var result = _cleanupExecutor.Execute(plan, mode, parsed.Has("force"), parsed.Get("quarantine-root"));
        var reportPath = _reportWriter.WriteCleanupResult(result, parsed.Get("report"));

        Console.WriteLine($"Run ID: {result.RunId}");
        Console.WriteLine($"Mode: {result.Mode}");
        Console.WriteLine($"Processed files: {result.ProcessedCount}");
        Console.WriteLine($"Processed size: {ByteSizeFormatter.Format(result.ProcessedBytes)}");
        Console.WriteLine($"Report: {reportPath}");

        if (!string.IsNullOrWhiteSpace(result.QuarantineManifestPath))
        {
            Console.WriteLine($"Manifest: {result.QuarantineManifestPath}");
        }

        if (result.Warnings.Count > 0)
        {
            Console.WriteLine($"Warnings: {result.Warnings.Count}");
        }

        return 0;
    }

    private int RunRestore(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            return RunRestoreList(args[1..]);
        }

        var parsed = ParseArguments(args);
        var runId = RequireOption(parsed, "run-id");
        var result = _cleanupExecutor.Restore(runId, parsed.Get("quarantine-root"));
        var reportPath = _reportWriter.WriteRestoreResult(result, parsed.Get("report"));

        if (parsed.Has("json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonReportWriter.SerializerOptions));
            return 0;
        }

        Console.WriteLine($"Run ID: {result.RunId}");
        Console.WriteLine($"Restored files: {result.RestoredCount}");
        Console.WriteLine($"Restored size: {ByteSizeFormatter.Format(result.RestoredBytes)}");
        Console.WriteLine($"Report: {reportPath}");

        if (result.Warnings.Count > 0)
        {
            Console.WriteLine($"Warnings: {result.Warnings.Count}");
        }

        return 0;
    }

    private int RunRestoreList(string[] args)
    {
        var parsed = ParseArguments(args);
        var manifests = _quarantineService.ListRuns(parsed.Get("quarantine-root"));

        if (parsed.Has("json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(manifests, JsonReportWriter.SerializerOptions));
            return 0;
        }

        if (manifests.Count == 0)
        {
            Console.WriteLine("No quarantine runs were found.");
            return 0;
        }

        foreach (var manifest in manifests)
        {
            var totalBytes = manifest.Entries.Sum(entry => entry.SizeBytes);
            Console.WriteLine($"{manifest.RunId}  {manifest.CreatedUtc:yyyy-MM-dd HH:mm:ss zzz}  {manifest.Entries.Count} files  {ByteSizeFormatter.Format(totalBytes)}  profile={manifest.Profile.Name}");
        }

        return 0;
    }

    private int RunReport(string[] args)
    {
        var parsed = ParseArguments(args);
        var reportPath = parsed.Get("path");

        if (string.IsNullOrWhiteSpace(reportPath))
        {
            var runId = RequireOption(parsed, "run-id");
            reportPath = _quarantineService.GetManifestPath(runId, parsed.Get("quarantine-root"));
        }

        var absolutePath = Path.GetFullPath(reportPath);
        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException($"Report '{absolutePath}' does not exist.", absolutePath);
        }

        Console.WriteLine(File.ReadAllText(absolutePath));
        return 0;
    }

    private int RunSchedule(string[] args)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException("schedule requires create, list, or remove.");
        }

        return args[0].ToLowerInvariant() switch
        {
            "create" => RunScheduleCreate(args[1..]),
            "list" => RunScheduleList(args[1..]),
            "remove" or "delete" => RunScheduleRemove(args[1..]),
            _ => throw new ArgumentException($"Unknown schedule command '{args[0]}'.")
        };
    }

    private static int RunScheduleCreate(string[] args)
    {
        var parsed = ParseArguments(args);
        var taskName = NormalizeTaskName(RequireOption(parsed, "name"));
        var profilePath = Path.GetFullPath(RequireOption(parsed, "profile"));

        if (!File.Exists(profilePath))
        {
            throw new FileNotFoundException($"Profile '{profilePath}' does not exist.", profilePath);
        }

        var scheduleTime = parsed.Get("daily") ?? "02:00";
        if (!TimeOnly.TryParseExact(scheduleTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTime))
        {
            throw new ArgumentException("Use --daily HH:mm with a 24-hour value such as 02:00.");
        }

        var taskCommand = BuildScheduledCleanCommand(profilePath);
        var startInfo = new ProcessStartInfo("schtasks.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("/Create");
        startInfo.ArgumentList.Add("/TN");
        startInfo.ArgumentList.Add(taskName);
        startInfo.ArgumentList.Add("/SC");
        startInfo.ArgumentList.Add("DAILY");
        startInfo.ArgumentList.Add("/ST");
        startInfo.ArgumentList.Add(parsedTime.ToString("HH:mm", CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("/TR");
        startInfo.ArgumentList.Add(taskCommand);
        startInfo.ArgumentList.Add("/F");

        var exitCode = RunProcess(startInfo, out var stdout, out var stderr);
        if (exitCode != 0)
        {
            Console.Error.WriteLine(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
            return exitCode;
        }

        Console.WriteLine(string.IsNullOrWhiteSpace(stdout) ? $"Created task '{taskName}'." : stdout.Trim());
        return 0;
    }

    private static int RunScheduleList(string[] args)
    {
        var parsed = ParseArguments(args);
        var filter = parsed.Get("name");

        var startInfo = new ProcessStartInfo("schtasks.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("/Query");
        startInfo.ArgumentList.Add("/FO");
        startInfo.ArgumentList.Add("LIST");
        startInfo.ArgumentList.Add("/V");

        var exitCode = RunProcess(startInfo, out var stdout, out var stderr);
        if (exitCode != 0)
        {
            Console.Error.WriteLine(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
            return exitCode;
        }

        var normalizedFilter = string.IsNullOrWhiteSpace(filter) ? "AppDust-" : NormalizeTaskName(filter);
        var matchingBlocks = stdout
            .Split(Environment.NewLine + Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Where(block => block.Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (matchingBlocks.Length == 0)
        {
            Console.WriteLine("No AppDust scheduled tasks were found.");
            return 0;
        }

        Console.WriteLine(string.Join(Environment.NewLine + Environment.NewLine, matchingBlocks));
        return 0;
    }

    private static int RunScheduleRemove(string[] args)
    {
        var parsed = ParseArguments(args);
        var taskName = NormalizeTaskName(RequireOption(parsed, "name"));

        var startInfo = new ProcessStartInfo("schtasks.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("/Delete");
        startInfo.ArgumentList.Add("/TN");
        startInfo.ArgumentList.Add(taskName);
        startInfo.ArgumentList.Add("/F");

        var exitCode = RunProcess(startInfo, out var stdout, out var stderr);
        if (exitCode != 0)
        {
            Console.Error.WriteLine(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
            return exitCode;
        }

        Console.WriteLine(string.IsNullOrWhiteSpace(stdout) ? $"Removed task '{taskName}'." : stdout.Trim());
        return 0;
    }

    private CleanupProfile BuildProfile(CommandArguments parsed)
    {
        var profile = parsed.Get("profile") is { Length: > 0 } profilePath
            ? _profileLoader.Load(profilePath)
            : CleanupProfile.Default;

        if (parsed.Has("all-users"))
        {
            profile = profile with { Scope = ScanScope.AllUsers };
        }

        if (parsed.Has("current-user"))
        {
            profile = profile with { Scope = ScanScope.CurrentUser };
        }

        if (parsed.Has("delete-permanently"))
        {
            profile = profile with { Mode = CleanupMode.PermanentDelete };
        }

        if (parsed.Has("quarantine"))
        {
            profile = profile with { Mode = CleanupMode.Quarantine };
        }

        if (parsed.Has("no-programdata"))
        {
            profile = profile with { IncludeProgramData = false };
        }

        if (parsed.Has("no-temp"))
        {
            profile = profile with { IncludeTemp = false };
        }

        if (parsed.Has("no-crash-dumps"))
        {
            profile = profile with { IncludeCrashDumps = false };
        }

        if (parsed.Get("min-age-hours") is { Length: > 0 } ageValue)
        {
            if (!int.TryParse(ageValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minimumFileAgeHours) || minimumFileAgeHours <= 0)
            {
                throw new ArgumentException("--min-age-hours must be a positive integer.");
            }

            profile = profile with { MinimumFileAgeHours = minimumFileAgeHours };
        }

        if (parsed.Get("exclude-path") is { Length: > 0 } excludePathValue)
        {
            var extraExcludes = excludePathValue
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Path.GetFullPath)
                .ToArray();

            profile = profile with
            {
                ExcludePathPrefixes = profile.ExcludePathPrefixes.Concat(extraExcludes).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            };
        }

        return profile;
    }

    private static int RunProcess(ProcessStartInfo startInfo, out string stdout, out string stderr)
    {
        using var process = new Process { StartInfo = startInfo };
        process.Start();
        stdout = process.StandardOutput.ReadToEnd();
        stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode;
    }

    private static string BuildScheduledCleanCommand(string profilePath)
    {
        var entryAssemblyPath = Assembly.GetEntryAssembly()?.Location;
        var processPath = Environment.ProcessPath;

        if (!string.IsNullOrWhiteSpace(entryAssemblyPath)
            && entryAssemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(processPath))
        {
            return $"\"{processPath}\" \"{entryAssemblyPath}\" clean --profile \"{profilePath}\"";
        }

        var executablePath = !string.IsNullOrWhiteSpace(entryAssemblyPath)
            ? entryAssemblyPath
            : processPath ?? throw new InvalidOperationException("Could not determine the AppDust entry assembly.");

        return $"\"{executablePath}\" clean --profile \"{profilePath}\"";
    }

    private static string NormalizeTaskName(string name) =>
        name.StartsWith("AppDust-", StringComparison.OrdinalIgnoreCase) ? name : $"AppDust-{name}";

    private static bool IsElevatedAdministrator()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static CommandArguments ParseArguments(string[] args)
    {
        var parsed = new CommandArguments();

        for (var index = 0; index < args.Length; index++)
        {
            var token = args[index];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                parsed.Positionals.Add(token);
                continue;
            }

            var option = token[2..];
            string? value = null;
            var separatorIndex = option.IndexOf('=');
            if (separatorIndex >= 0)
            {
                value = option[(separatorIndex + 1)..];
                option = option[..separatorIndex];
            }
            else if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = args[++index];
            }
            else
            {
                value = "true";
            }

            parsed.Options[option] = value;
        }

        return parsed;
    }

    private static string RequireOption(CommandArguments parsed, string name) =>
        parsed.Get(name) is { Length: > 0 } value ? value : throw new ArgumentException($"Missing required option --{name}.");

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        PrintHelp();
        return 1;
    }

    private static int PrintAndReturnHelp()
    {
        PrintHelp();
        return 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("AppDust");
        Console.WriteLine("  scan [--profile path] [--report path] [--json] [--all-users] [--current-user] [--min-age-hours N] [--exclude-path path1;path2] [--no-crash-dumps]");
        Console.WriteLine("  clean [--profile path] [--report path] [--quarantine-root path] [--delete-permanently --force] [--dry-run] [--no-crash-dumps]");
        Console.WriteLine("  restore list [--quarantine-root path] [--json]");
        Console.WriteLine("  restore --run-id id [--quarantine-root path] [--report path] [--json]");
        Console.WriteLine("  report --run-id id [--quarantine-root path]");
        Console.WriteLine("  report --path path");
        Console.WriteLine("  schedule create --name task --profile profile.json [--daily HH:mm]");
        Console.WriteLine("  schedule list [--name task]");
        Console.WriteLine("  schedule remove --name task");
        Console.WriteLine("  note: --all-users requires an elevated administrator session");
    }
}

internal sealed class CommandArguments
{
    public Dictionary<string, string?> Options { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Positionals { get; } = [];

    public bool Has(string name) => Options.ContainsKey(name);

    public string? Get(string name) => Options.TryGetValue(name, out var value) ? value : null;
}