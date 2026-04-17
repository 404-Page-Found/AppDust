using AppDust.Core.Execution;
using AppDust.Core.Models;

namespace AppDust.Core.Tests;

public sealed class QuarantineServiceTests
{
    [Fact]
    public void QuarantineAndRestoreRoundTripsAFile()
    {
        var dataDirectory = Directory.CreateTempSubdirectory();
        var quarantineDirectory = Directory.CreateTempSubdirectory();
        var runId = Guid.NewGuid().ToString("N");

        try
        {
            var candidatePath = Path.Combine(dataDirectory.FullName, "cache.tmp");
            File.WriteAllText(candidatePath, "payload");
            File.SetLastWriteTimeUtc(candidatePath, DateTime.UtcNow.AddDays(-2));

            var candidateInfo = new FileInfo(candidatePath);
            var plan = new ScanPlan
            {
                RunId = runId,
                Profile = CleanupProfile.Default,
                Candidates =
                [
                    new ScanCandidate
                    {
                        OriginalPath = candidateInfo.FullName,
                        RootPath = dataDirectory.FullName,
                        Reason = "Test candidate",
                        LocationKind = ScanLocationKind.Temp,
                        OwnerLabel = "test",
                        SizeBytes = candidateInfo.Length,
                        LastWriteUtc = candidateInfo.LastWriteTimeUtc
                    }
                ],
                TotalBytes = candidateInfo.Length
            };

            var service = new QuarantineService();
            var manifest = service.Quarantine(plan, quarantineDirectory.FullName);

            Assert.Single(manifest.Entries);
            Assert.False(File.Exists(candidateInfo.FullName));
            Assert.True(File.Exists(manifest.Entries[0].QuarantinedPath));

            var restoreResult = service.Restore(runId, quarantineDirectory.FullName);

            Assert.Equal(1, restoreResult.RestoredCount);
            Assert.True(File.Exists(candidateInfo.FullName));
        }
        finally
        {
            if (dataDirectory.Exists)
            {
                dataDirectory.Delete(true);
            }

            if (quarantineDirectory.Exists)
            {
                quarantineDirectory.Delete(true);
            }
        }
    }

    [Fact]
    public void ListRunsReturnsExistingQuarantineManifests()
    {
        var dataDirectory = Directory.CreateTempSubdirectory();
        var quarantineDirectory = Directory.CreateTempSubdirectory();
        var olderRunId = Guid.NewGuid().ToString("N");
        var newerRunId = Guid.NewGuid().ToString("N");

        try
        {
            QuarantineSingleFileRun(dataDirectory.FullName, quarantineDirectory.FullName, olderRunId, "older.tmp", DateTime.UtcNow.AddMinutes(-10));
            QuarantineSingleFileRun(dataDirectory.FullName, quarantineDirectory.FullName, newerRunId, "newer.tmp", DateTime.UtcNow);

            var service = new QuarantineService();
            var runs = service.ListRuns(quarantineDirectory.FullName);

            Assert.Equal(2, runs.Count);
            Assert.Equal(newerRunId, runs[0].RunId);
            Assert.Equal(olderRunId, runs[1].RunId);
        }
        finally
        {
            if (dataDirectory.Exists)
            {
                dataDirectory.Delete(true);
            }

            if (quarantineDirectory.Exists)
            {
                quarantineDirectory.Delete(true);
            }
        }
    }

    private static void QuarantineSingleFileRun(string dataRoot, string quarantineRoot, string runId, string fileName, DateTime createdUtc)
    {
        var sourcePath = Path.Combine(dataRoot, fileName);
        File.WriteAllText(sourcePath, fileName);
        File.SetLastWriteTimeUtc(sourcePath, createdUtc.AddDays(-2));

        var sourceInfo = new FileInfo(sourcePath);
        var plan = new ScanPlan
        {
            RunId = runId,
            CreatedUtc = createdUtc,
            Profile = CleanupProfile.Default,
            Candidates =
            [
                new ScanCandidate
                {
                    OriginalPath = sourceInfo.FullName,
                    RootPath = dataRoot,
                    Reason = "Test candidate",
                    LocationKind = ScanLocationKind.Temp,
                    OwnerLabel = "test",
                    SizeBytes = sourceInfo.Length,
                    LastWriteUtc = sourceInfo.LastWriteTimeUtc
                }
            ],
            TotalBytes = sourceInfo.Length
        };

        var service = new QuarantineService();
        service.Quarantine(plan, quarantineRoot);
    }
}