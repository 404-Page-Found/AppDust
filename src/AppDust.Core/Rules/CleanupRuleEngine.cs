using AppDust.Core.Models;
using AppDust.Core.Scanning;

namespace AppDust.Core.Rules;

public sealed class CleanupRuleEngine
{
    private static readonly string[] CrashDumpFragments =
    [
        @"\CrashDumps\",
        @"\ReportArchive\",
        @"\ReportQueue\"
    ];

    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".appx",
        ".bat",
        ".cmd",
        ".dll",
        ".exe",
        ".msi",
        ".msix",
        ".ps1",
        ".psd1",
        ".psm1",
        ".sys",
        ".vhd",
        ".vhdx"
    };

    private static readonly string[] ProtectedFragments =
    [
        @"\Microsoft\Credentials",
        @"\Microsoft\Crypto",
        @"\Microsoft\Protect",
        @"\Microsoft\Vault",
        @"\Windows\Start Menu"
    ];

    public bool IsProtectedPath(string path, CleanupProfile profile)
    {
        var normalized = NormalizePath(path);

        foreach (var excludedPrefix in profile.ExcludePathPrefixes)
        {
            if (!string.IsNullOrWhiteSpace(excludedPrefix)
                && normalized.StartsWith(NormalizePath(excludedPrefix), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (normalized.Contains(@"\Packages\", StringComparison.OrdinalIgnoreCase)
            && (normalized.Contains(@"\AC\", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains(@"\SystemAppData\", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return ProtectedFragments.Any(fragment => normalized.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsCandidateDirectory(string path, ScanRoot root, CleanupProfile profile)
    {
        if (IsProtectedPath(path, profile))
        {
            return false;
        }

        if (!profile.IncludeCrashDumps && IsCrashDumpPath(path))
        {
            return false;
        }

        if (root.Kind == ScanLocationKind.Temp)
        {
            return true;
        }

        var segments = NormalizePath(path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            if (!profile.IncludeCrashDumps && IsCrashDumpKeyword(segment))
            {
                continue;
            }

            if (profile.IncludePathKeywords.Any(keyword => segment.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    public bool ShouldIncludeFile(FileInfo fileInfo, CleanupProfile profile, ScanRoot root, out string reason)
    {
        reason = string.Empty;

        if (!fileInfo.Exists)
        {
            return false;
        }

        if (fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            return false;
        }

        if (IsProtectedPath(fileInfo.FullName, profile))
        {
            return false;
        }

        if (!profile.IncludeCrashDumps
            && (IsCrashDumpPath(fileInfo.FullName)
                || fileInfo.Extension.Equals(".dmp", StringComparison.OrdinalIgnoreCase)
                || fileInfo.Extension.Equals(".mdmp", StringComparison.OrdinalIgnoreCase)
                || fileInfo.Extension.Equals(".hdmp", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (BlockedExtensions.Contains(fileInfo.Extension))
        {
            return false;
        }

        var ageThreshold = DateTimeOffset.UtcNow.AddHours(-Math.Abs(profile.MinimumFileAgeHours));
        if (fileInfo.LastWriteTimeUtc >= ageThreshold.UtcDateTime)
        {
            return false;
        }

        reason = $"Matched {root.Kind} cleanup rule and exceeded the {profile.MinimumFileAgeHours}-hour age threshold.";
        return true;
    }

    private static bool IsCrashDumpPath(string path)
    {
        var normalized = NormalizePath(path);
        return CrashDumpFragments.Any(fragment => normalized.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsCrashDumpKeyword(string segment) =>
        segment.Contains("crashdump", StringComparison.OrdinalIgnoreCase)
        || segment.Contains("reportarchive", StringComparison.OrdinalIgnoreCase)
        || segment.Contains("reportqueue", StringComparison.OrdinalIgnoreCase)
        || segment.Equals("dumps", StringComparison.OrdinalIgnoreCase);

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
