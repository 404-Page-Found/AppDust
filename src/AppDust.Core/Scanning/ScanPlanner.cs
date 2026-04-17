using AppDust.Core.Models;
using AppDust.Core.Rules;

namespace AppDust.Core.Scanning;

public sealed class ScanPlanner
{
    private const int DiscoveryDepthLimit = 5;

    private readonly WindowsPathResolver _pathResolver;
    private readonly CleanupRuleEngine _ruleEngine;

    public ScanPlanner(WindowsPathResolver pathResolver, CleanupRuleEngine ruleEngine)
    {
        _pathResolver = pathResolver;
        _ruleEngine = ruleEngine;
    }

    public ScanPlan CreatePlan(CleanupProfile profile)
    {
        var warnings = new List<string>();
        var candidates = new List<ScanCandidate>();
        var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in _pathResolver.ResolveRoots(profile))
        {
            if (!Directory.Exists(root.Path))
            {
                continue;
            }

            DiscoverRoot(root, profile, candidates, warnings, seenFiles);
        }

        return new ScanPlan
        {
            Profile = profile,
            Candidates = candidates
                .OrderByDescending(candidate => candidate.SizeBytes)
                .ThenBy(candidate => candidate.OriginalPath, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Warnings = warnings,
            TotalBytes = candidates.Sum(candidate => candidate.SizeBytes)
        };
    }

    private void DiscoverRoot(
        ScanRoot root,
        CleanupProfile profile,
        List<ScanCandidate> candidates,
        List<string> warnings,
        HashSet<string> seenFiles)
    {
        var pendingDirectories = new Queue<(string DirectoryPath, int Depth)>();
        pendingDirectories.Enqueue((root.Path, 0));

        while (pendingDirectories.Count > 0)
        {
            var (directoryPath, depth) = pendingDirectories.Dequeue();
            if (_ruleEngine.IsProtectedPath(directoryPath, profile))
            {
                continue;
            }

            if ((depth == 0 && root.Kind == ScanLocationKind.Temp) || _ruleEngine.IsCandidateDirectory(directoryPath, root, profile))
            {
                ScanCandidateDirectory(root, directoryPath, profile, candidates, warnings, seenFiles);
                continue;
            }

            if (depth >= DiscoveryDepthLimit)
            {
                continue;
            }

            foreach (var childDirectory in SafeEnumerateDirectories(directoryPath, warnings))
            {
                if (!IsTraversableDirectory(childDirectory))
                {
                    continue;
                }

                pendingDirectories.Enqueue((childDirectory, depth + 1));
            }
        }
    }

    private void ScanCandidateDirectory(
        ScanRoot root,
        string candidateDirectory,
        CleanupProfile profile,
        List<ScanCandidate> candidates,
        List<string> warnings,
        HashSet<string> seenFiles)
    {
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(candidateDirectory);

        while (pendingDirectories.Count > 0)
        {
            var currentDirectory = pendingDirectories.Pop();
            if (_ruleEngine.IsProtectedPath(currentDirectory, profile))
            {
                continue;
            }

            foreach (var childDirectory in SafeEnumerateDirectories(currentDirectory, warnings))
            {
                if (IsTraversableDirectory(childDirectory))
                {
                    pendingDirectories.Push(childDirectory);
                }
            }

            foreach (var filePath in SafeEnumerateFiles(currentDirectory, warnings))
            {
                if (!seenFiles.Add(filePath))
                {
                    continue;
                }

                try
                {
                    var fileInfo = new FileInfo(filePath);
                    if (_ruleEngine.ShouldIncludeFile(fileInfo, profile, root, out var reason))
                    {
                        candidates.Add(new ScanCandidate
                        {
                            OriginalPath = fileInfo.FullName,
                            RootPath = root.Path,
                            Reason = reason,
                            LocationKind = root.Kind,
                            OwnerLabel = root.OwnerLabel,
                            SizeBytes = fileInfo.Length,
                            LastWriteUtc = fileInfo.LastWriteTimeUtc
                        });
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    warnings.Add($"Skipped file '{filePath}': {exception.Message}");
                }
            }
        }
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string directoryPath, List<string> warnings)
    {
        try
        {
            return Directory.EnumerateDirectories(directoryPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            warnings.Add($"Skipped directory '{directoryPath}': {exception.Message}");
            return [];
        }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string directoryPath, List<string> warnings)
    {
        try
        {
            return Directory.EnumerateFiles(directoryPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            warnings.Add($"Skipped files in '{directoryPath}': {exception.Message}");
            return [];
        }
    }

    private static bool IsTraversableDirectory(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return !attributes.HasFlag(FileAttributes.ReparsePoint);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
