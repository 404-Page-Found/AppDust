namespace AppDust.Core.Models;

public sealed record ScanCandidate
{
    public required string OriginalPath { get; init; }
    public required string RootPath { get; init; }
    public required string Reason { get; init; }
    public required ScanLocationKind LocationKind { get; init; }
    public string? OwnerLabel { get; init; }
    public long SizeBytes { get; init; }
    public DateTimeOffset LastWriteUtc { get; init; }
}

public sealed record ScanPlan
{
    public string RunId { get; init; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
    public CleanupProfile Profile { get; init; } = CleanupProfile.Default;
    public IReadOnlyList<ScanCandidate> Candidates { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public long TotalBytes { get; init; }
}

public sealed record CleanupResult
{
    public required string RunId { get; init; }
    public required CleanupMode Mode { get; init; }
    public DateTimeOffset StartedUtc { get; init; }
    public DateTimeOffset CompletedUtc { get; init; }
    public int ProcessedCount { get; init; }
    public long ProcessedBytes { get; init; }
    public string? QuarantineManifestPath { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed record RestoreResult
{
    public required string RunId { get; init; }
    public DateTimeOffset RestoredUtc { get; init; } = DateTimeOffset.UtcNow;
    public int RestoredCount { get; init; }
    public long RestoredBytes { get; init; }
    public string ManifestPath { get; init; } = string.Empty;
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed record QuarantineEntry
{
    public required string OriginalPath { get; init; }
    public required string QuarantinedPath { get; init; }
    public required ScanLocationKind LocationKind { get; init; }
    public string? OwnerLabel { get; init; }
    public long SizeBytes { get; init; }
    public DateTimeOffset LastWriteUtc { get; init; }
}

public sealed record QuarantineManifest
{
    public required string RunId { get; init; }
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
    public CleanupProfile Profile { get; init; } = CleanupProfile.Default;
    public string QuarantineRoot { get; init; } = string.Empty;
    public string ManifestPath { get; init; } = string.Empty;
    public IReadOnlyList<QuarantineEntry> Entries { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
