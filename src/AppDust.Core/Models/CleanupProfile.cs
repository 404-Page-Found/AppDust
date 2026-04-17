namespace AppDust.Core.Models;

public sealed record CleanupProfile
{
    public static CleanupProfile Default { get; } = new();

    public string Name { get; init; } = "default";
    public ScanScope Scope { get; init; } = ScanScope.CurrentUser;
    public CleanupMode Mode { get; init; } = CleanupMode.Quarantine;
    public bool IncludeProgramData { get; init; } = true;
    public bool IncludeTemp { get; init; } = true;
    public bool IncludeCrashDumps { get; init; } = true;
    public int MinimumFileAgeHours { get; init; } = 24;
    public string[] IncludePathKeywords { get; init; } = ["temp", "cache", "logs", "crashdumps", "reportqueue", "reportarchive", "dumps", "tmp"];
    public string[] ExcludePathPrefixes { get; init; } = [];
}
