using AppDust.Core.Models;
using AppDust.Core.Rules;
using AppDust.Core.Scanning;
using AppDust.Core.Validation;

namespace AppDust.Core.Tests;

public sealed class CleanupRuleEngineTests
{
    private readonly CleanupRuleEngine _ruleEngine = new();
    private readonly CleanupRequestValidator _requestValidator = new();

    [Fact]
    public void CandidateDirectoryMatchesKnownKeyword()
    {
        var root = new ScanRoot(@"C:\Users\lucas\AppData\Local", ScanLocationKind.LocalAppData, "lucas");

        var result = _ruleEngine.IsCandidateDirectory(@"C:\Users\lucas\AppData\Local\Temp", root, CleanupProfile.Default);

        Assert.True(result);
    }

    [Fact]
    public void ProtectedPathRejectsCredentialStores()
    {
        var result = _ruleEngine.IsProtectedPath(@"C:\Users\lucas\AppData\Roaming\Microsoft\Credentials\secret.bin", CleanupProfile.Default);

        Assert.True(result);
    }

    [Fact]
    public void BlockedExtensionIsSkippedEvenWhenOld()
    {
        var root = new ScanRoot(@"C:\Temp", ScanLocationKind.Temp, "lucas");
        var temporaryDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var executablePath = Path.Combine(temporaryDirectory.FullName, "installer.exe");
            File.WriteAllText(executablePath, "payload");
            File.SetLastWriteTimeUtc(executablePath, DateTime.UtcNow.AddDays(-3));

            var include = _ruleEngine.ShouldIncludeFile(new FileInfo(executablePath), CleanupProfile.Default, root, out _);

            Assert.False(include);
        }
        finally
        {
            temporaryDirectory.Delete(true);
        }
    }

    [Fact]
    public void CrashDumpDirectoryIsIgnoredWhenDisabled()
    {
        var root = new ScanRoot(@"C:\Users\lucas\AppData\Local", ScanLocationKind.LocalAppData, "lucas");
        var profile = CleanupProfile.Default with { IncludeCrashDumps = false };

        var result = _ruleEngine.IsCandidateDirectory(@"C:\Users\lucas\AppData\Local\CrashDumps", root, profile);

        Assert.False(result);
    }

    [Fact]
    public void CrashDumpFileIsSkippedWhenDisabled()
    {
        var root = new ScanRoot(@"C:\Users\lucas\AppData\Local", ScanLocationKind.LocalAppData, "lucas");
        var profile = CleanupProfile.Default with { IncludeCrashDumps = false };
        var temporaryDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var crashDumpDirectory = Path.Combine(temporaryDirectory.FullName, "CrashDumps");
            Directory.CreateDirectory(crashDumpDirectory);
            var dumpPath = Path.Combine(crashDumpDirectory, "app.dmp");
            File.WriteAllText(dumpPath, "payload");
            File.SetLastWriteTimeUtc(dumpPath, DateTime.UtcNow.AddDays(-3));

            var include = _ruleEngine.ShouldIncludeFile(new FileInfo(dumpPath), profile, root, out _);

            Assert.False(include);
        }
        finally
        {
            temporaryDirectory.Delete(true);
        }
    }

    [Fact]
    public void AllUsersScopeRequiresElevation()
    {
        var profile = CleanupProfile.Default with { Scope = ScanScope.AllUsers };

        var action = () => _requestValidator.ValidateScanScope(profile, isElevatedAdministrator: false);

        var exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Contains("requires an elevated administrator session", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
