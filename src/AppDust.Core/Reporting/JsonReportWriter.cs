using System.Text.Json;
using System.Text.Json.Serialization;
using AppDust.Core.Models;

namespace AppDust.Core.Reporting;

public sealed class JsonReportWriter
{
    public static JsonSerializerOptions SerializerOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string WriteScanPlan(ScanPlan plan, string? path = null) =>
        WriteDocument(plan, path, "scan", plan.RunId);

    public string WriteCleanupResult(CleanupResult result, string? path = null) =>
        WriteDocument(result, path, "clean", result.RunId);

    public string WriteRestoreResult(RestoreResult result, string? path = null) =>
        WriteDocument(result, path, "restore", result.RunId);

    private static string WriteDocument<T>(T document, string? path, string reportType, string runId)
    {
        var destinationPath = ResolvePath(path, reportType, runId);
        var directory = Path.GetDirectoryName(destinationPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(destinationPath, JsonSerializer.Serialize(document, SerializerOptions));
        return destinationPath;
    }

    private static string ResolvePath(string? path, string reportType, string runId)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            return Path.GetFullPath(path);
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "AppDust", "Reports", $"{reportType}-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{runId}.json");
    }
}
