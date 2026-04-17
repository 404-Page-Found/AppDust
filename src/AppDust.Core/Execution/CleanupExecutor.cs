using AppDust.Core.Models;

namespace AppDust.Core.Execution;

public sealed class CleanupExecutor
{
    private readonly QuarantineService _quarantineService;

    public CleanupExecutor(QuarantineService quarantineService)
    {
        _quarantineService = quarantineService;
    }

    public CleanupResult Execute(ScanPlan plan, CleanupMode mode, bool forcePermanentDelete, string? quarantineRoot = null)
    {
        if (mode == CleanupMode.PermanentDelete && !forcePermanentDelete)
        {
            throw new InvalidOperationException("Permanent deletion requires the --force flag.");
        }

        var startedUtc = DateTimeOffset.UtcNow;
        var warnings = new List<string>();
        var processedCount = 0;
        var processedBytes = 0L;
        string? manifestPath = null;

        if (mode == CleanupMode.Quarantine)
        {
            var manifest = _quarantineService.Quarantine(plan, quarantineRoot);
            processedCount = manifest.Entries.Count;
            processedBytes = manifest.Entries.Sum(entry => entry.SizeBytes);
            manifestPath = manifest.ManifestPath;
            warnings.AddRange(manifest.Warnings);
        }
        else
        {
            foreach (var candidate in plan.Candidates)
            {
                try
                {
                    if (!File.Exists(candidate.OriginalPath))
                    {
                        warnings.Add($"Skipped missing file '{candidate.OriginalPath}'.");
                        continue;
                    }

                    File.Delete(candidate.OriginalPath);
                    processedCount++;
                    processedBytes += candidate.SizeBytes;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    warnings.Add($"Failed to delete '{candidate.OriginalPath}': {exception.Message}");
                }
            }
        }

        return new CleanupResult
        {
            RunId = plan.RunId,
            Mode = mode,
            StartedUtc = startedUtc,
            CompletedUtc = DateTimeOffset.UtcNow,
            ProcessedCount = processedCount,
            ProcessedBytes = processedBytes,
            QuarantineManifestPath = manifestPath,
            Warnings = warnings
        };
    }

    public RestoreResult Restore(string runId, string? quarantineRoot = null) =>
        _quarantineService.Restore(runId, quarantineRoot);
}
