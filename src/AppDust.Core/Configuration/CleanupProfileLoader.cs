using System.Text.Json;
using System.Text.Json.Serialization;
using AppDust.Core.Models;

namespace AppDust.Core.Configuration;

public sealed class CleanupProfileLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public CleanupProfile Load(string path)
    {
        var absolutePath = Path.GetFullPath(path);

        using var stream = File.OpenRead(absolutePath);
        var loadedProfile = JsonSerializer.Deserialize<CleanupProfile>(stream, SerializerOptions)
            ?? throw new InvalidDataException($"Profile '{absolutePath}' was empty or invalid.");

        return MergeWithDefaults(loadedProfile);
    }

    public string Save(string path, CleanupProfile profile)
    {
        var absolutePath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(absolutePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(absolutePath, JsonSerializer.Serialize(MergeWithDefaults(profile), SerializerOptions));
        return absolutePath;
    }

    private static CleanupProfile MergeWithDefaults(CleanupProfile loadedProfile)
    {
        var defaults = CleanupProfile.Default;

        return defaults with
        {
            Name = string.IsNullOrWhiteSpace(loadedProfile.Name) ? defaults.Name : loadedProfile.Name,
            Scope = loadedProfile.Scope,
            Mode = loadedProfile.Mode,
            IncludeProgramData = loadedProfile.IncludeProgramData,
            IncludeTemp = loadedProfile.IncludeTemp,
            IncludeCrashDumps = loadedProfile.IncludeCrashDumps,
            MinimumFileAgeHours = loadedProfile.MinimumFileAgeHours > 0 ? loadedProfile.MinimumFileAgeHours : defaults.MinimumFileAgeHours,
            IncludePathKeywords = loadedProfile.IncludePathKeywords.Length > 0 ? loadedProfile.IncludePathKeywords : defaults.IncludePathKeywords,
            ExcludePathPrefixes = loadedProfile.ExcludePathPrefixes
        };
    }
}
