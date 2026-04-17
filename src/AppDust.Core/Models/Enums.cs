namespace AppDust.Core.Models;

public enum ScanScope
{
    CurrentUser,
    AllUsers
}

public enum CleanupMode
{
    Quarantine,
    PermanentDelete
}

public enum ScanLocationKind
{
    ProgramData,
    LocalAppData,
    LocalLowAppData,
    RoamingAppData,
    Temp
}
