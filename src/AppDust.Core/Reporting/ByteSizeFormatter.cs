using System.Globalization;

namespace AppDust.Core.Reporting;

public static class ByteSizeFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];

    public static string Format(long bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);

        var size = (decimal)bytes;
        var unitIndex = 0;

        while (size >= 1024m && unitIndex < Units.Length - 1)
        {
            size /= 1024m;
            unitIndex++;
        }

        if (unitIndex == 0)
        {
            return $"{bytes} {Units[unitIndex]}";
        }

        var format = size >= 100m ? "0" : size >= 10m ? "0.#" : "0.##";
        return $"{size.ToString(format, CultureInfo.InvariantCulture)} {Units[unitIndex]}";
    }
}