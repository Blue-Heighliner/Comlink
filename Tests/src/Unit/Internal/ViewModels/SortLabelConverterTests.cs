namespace BlueHeighliner.Comlink.Tests.Unit.Internal.ViewModels;

/// <summary>Unit tests for <see cref="SortLabelConverter"/>.</summary>
public sealed class SortLabelConverterTests
{
    /// <summary>True value returns "Sort: A-Z".</summary>
    [Fact]
    public void Convert_True_ReturnsAZ()
    {
        object result = SortLabelConverter.Instance.Convert(true, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal("Sort: A-Z", result);
    }

    /// <summary>False value returns "Sort: Recent".</summary>
    [Fact]
    public void Convert_False_ReturnsRecent()
    {
        object result = SortLabelConverter.Instance.Convert(false, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal("Sort: Recent", result);
    }
}
