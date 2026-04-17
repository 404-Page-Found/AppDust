using AppDust.Core.Models;

namespace AppDust.Core.Validation;

public sealed class CleanupRequestValidator
{
    public void ValidateScanScope(CleanupProfile profile, bool isElevatedAdministrator)
    {
        if (profile.Scope == ScanScope.AllUsers && !isElevatedAdministrator)
        {
            throw new InvalidOperationException("The all-users scan scope requires an elevated administrator session.");
        }
    }
}
