using System.Text.Json;
using System.Text.Json.Serialization;
using AppDust.Core.Models;

namespace AppDust.Core.Execution;

public sealed class QuarantineService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string GetQuarantineRoot(string? configuredRoot = null)
    {
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            return Path.GetFullPath(configuredRoot);
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "AppDust", "Quarantine");
    }

    public string GetManifestPath(string runId, string? quarantineRoot = null) =>
        Path.Combine(GetQuarantineRoot(quarantineRoot), runId, "manifest.json");

    public IReadOnlyList<QuarantineManifest> ListRuns(string? quarantineRoot = null)
    {
        var root = GetQuarantineRoot(quarantineRoot);
        if (!Directory.Exists(root))
        {
            return [];
        }

        var manifests = new List<QuarantineManifest>();

        foreach (var runDirectory in Directory.EnumerateDirectories(root))
        {
            var manifestPath = Path.Combine(runDirectory, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                var manifest = JsonSerializer.Deserialize<QuarantineManifest>(File.ReadAllText(manifestPath), SerializerOptions);
                if (manifest is not null)
                {
                    manifests.Add(manifest);
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
            {
                // Skip invalid or unreadable manifests so one bad run does not block discovery.
            }
        }

        return manifests
            .OrderByDescending(manifest => manifest.CreatedUtc)
            .ThenByDescending(manifest => manifest.RunId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public QuarantineManifest Quarantine(ScanPlan plan, string? quarantineRoot = null)
    {
        var root = GetQuarantineRoot(quarantineRoot);
        var runDirectory = Path.Combine(root, plan.RunId);
        Directory.CreateDirectory(runDirectory);

        var warnings = new List<string>();
        var entries = new List<QuarantineEntry>();
        var index = 0;

        foreach (var candidate in plan.Candidates)
        {
            if (!File.Exists(candidate.OriginalPath))
            {
                warnings.Add($"Skipped missing file '{candidate.OriginalPath}'.");
                continue;
            }

            var storedPath = Path.Combine(runDirectory, $"{index:D6}{Path.GetExtension(candidate.OriginalPath)}");

            try
            {
                File.Move(candidate.OriginalPath, storedPath);
                entries.Add(new QuarantineEntry
                {
                    OriginalPath = candidate.OriginalPath,
                    QuarantinedPath = storedPath,
                    LocationKind = candidate.LocationKind,
                    OwnerLabel = candidate.OwnerLabel,
                    SizeBytes = candidate.SizeBytes,
                    LastWriteUtc = candidate.LastWriteUtc
                });
                index++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                warnings.Add($"Failed to quarantine '{candidate.OriginalPath}': {exception.Message}");
            }
        }

        var manifestPath = Path.Combine(runDirectory, "manifest.json");
        var manifest = new QuarantineManifest
        {
            RunId = plan.RunId,
            Profile = plan.Profile,
            QuarantineRoot = root,
            ManifestPath = manifestPath,
            Entries = entries,
            Warnings = warnings
        };

        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, SerializerOptions));
        return manifest;
    }

    public RestoreResult Restore(string runId, string? quarantineRoot = null)
    {
        var manifestPath = GetManifestPath(runId, quarantineRoot);
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException($"No quarantine manifest exists for run '{runId}'.", manifestPath);
        }

        var manifest = JsonSerializer.Deserialize<QuarantineManifest>(File.ReadAllText(manifestPath), SerializerOptions)
            ?? throw new InvalidDataException($"Quarantine manifest '{manifestPath}' is invalid.");

        var warnings = new List<string>();
        var restoredCount = 0;
        var restoredBytes = 0L;

        foreach (var entry in manifest.Entries)
        {
            if (!File.Exists(entry.QuarantinedPath))
            {
                warnings.Add($"Missing quarantined file '{entry.QuarantinedPath}'.");
                continue;
            }

            if (File.Exists(entry.OriginalPath))
            {
                warnings.Add($"Skipped restore for '{entry.OriginalPath}' because the destination already exists.");
                continue;
            }

            var destinationDirectory = Path.GetDirectoryName(entry.OriginalPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            try
            {
                File.Move(entry.QuarantinedPath, entry.OriginalPath);
                restoredCount++;
                restoredBytes += entry.SizeBytes;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                warnings.Add($"Failed to restore '{entry.OriginalPath}': {exception.Message}");
            }
        }

        return new RestoreResult
        {
            RunId = runId,
            RestoredCount = restoredCount,
            RestoredBytes = restoredBytes,
            ManifestPath = manifestPath,
            Warnings = warnings
        };
    }
}
