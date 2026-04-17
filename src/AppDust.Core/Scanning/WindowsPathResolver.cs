using AppDust.Core.Models;

namespace AppDust.Core.Scanning;

public sealed record ScanRoot(string Path, ScanLocationKind Kind, string OwnerLabel);

public sealed class WindowsPathResolver
{
    public IReadOnlyList<ScanRoot> ResolveRoots(CleanupProfile profile)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("AppDust only supports Windows hosts.");
        }

        var roots = new Dictionary<string, ScanRoot>(StringComparer.OrdinalIgnoreCase);

        void AddRoot(string? path, ScanLocationKind kind, string ownerLabel)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var normalized = NormalizePath(path);
            if (!Directory.Exists(normalized))
            {
                return;
            }

            roots[normalized] = new ScanRoot(normalized, kind, ownerLabel);
        }

        if (profile.IncludeProgramData)
        {
            AddRoot(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), ScanLocationKind.ProgramData, "machine");
        }

        var currentUser = Environment.UserName;
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localLow = GetCurrentUserLocalLowPath();

        AddRoot(localAppData, ScanLocationKind.LocalAppData, currentUser);
        AddRoot(localLow, ScanLocationKind.LocalLowAppData, currentUser);
        AddRoot(roamingAppData, ScanLocationKind.RoamingAppData, currentUser);

        if (profile.IncludeTemp)
        {
            AddRoot(Path.GetTempPath(), ScanLocationKind.Temp, currentUser);
        }

        if (profile.Scope == ScanScope.AllUsers)
        {
            foreach (var profileDirectory in EnumerateUserProfiles())
            {
                var ownerLabel = profileDirectory.Name;
                var appDataRoot = Path.Combine(profileDirectory.FullName, "AppData");
                AddRoot(Path.Combine(appDataRoot, "Local"), ScanLocationKind.LocalAppData, ownerLabel);
                AddRoot(Path.Combine(appDataRoot, "LocalLow"), ScanLocationKind.LocalLowAppData, ownerLabel);
                AddRoot(Path.Combine(appDataRoot, "Roaming"), ScanLocationKind.RoamingAppData, ownerLabel);

                if (profile.IncludeTemp)
                {
                    AddRoot(Path.Combine(appDataRoot, "Local", "Temp"), ScanLocationKind.Temp, ownerLabel);
                }
            }
        }

        return roots.Values
            .OrderBy(root => root.Kind)
            .ThenBy(root => root.OwnerLabel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(root => root.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? GetCurrentUserLocalLowPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var localDirectory = Directory.GetParent(localAppData);
        return localDirectory is null ? null : Path.Combine(localDirectory.FullName, "LocalLow");
    }

    private static IEnumerable<DirectoryInfo> EnumerateUserProfiles()
    {
        var currentUserProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var profilesRoot = Directory.GetParent(currentUserProfile);
        if (profilesRoot is null || !profilesRoot.Exists)
        {
            yield break;
        }

        foreach (var directory in profilesRoot.EnumerateDirectories())
        {
            if (!IsSkippableProfile(directory.Name))
            {
                yield return directory;
            }
        }
    }

    private static bool IsSkippableProfile(string profileName) =>
        profileName.Equals("All Users", StringComparison.OrdinalIgnoreCase)
        || profileName.Equals("Default", StringComparison.OrdinalIgnoreCase)
        || profileName.Equals("Default User", StringComparison.OrdinalIgnoreCase)
        || profileName.Equals("Public", StringComparison.OrdinalIgnoreCase)
        || profileName.Equals("defaultuser0", StringComparison.OrdinalIgnoreCase);

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
