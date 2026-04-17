using AppDust.Core.Reporting;

namespace AppDust.Core.Tests;

public sealed class ByteSizeFormatterTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1, "1 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1048576, "1 MB")]
    [InlineData(11010048, "10.5 MB")]
    [InlineData(1073741824, "1 GB")]
    public void Format_UsesScaledUnits(long bytes, string expected)
    {
        var result = ByteSizeFormatter.Format(bytes);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Format_RejectsNegativeValues()
    {
        var action = () => ByteSizeFormatter.Format(-1);

        Assert.Throws<ArgumentOutOfRangeException>(action);
    }
}