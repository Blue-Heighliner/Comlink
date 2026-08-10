namespace BlueHeighliner.Comlink.Tests.ViewModels;

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

    /// <summary>Null value returns "Sort: Recent" (not-true fallback).</summary>
    [Fact]
    public void Convert_Null_ReturnsRecent()
    {
        object result = SortLabelConverter.Instance.Convert(null, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal("Sort: Recent", result);
    }

    /// <summary>Non-boolean value returns "Sort: Recent".</summary>
    [Fact]
    public void Convert_NonBoolean_ReturnsRecent()
    {
        object result = SortLabelConverter.Instance.Convert("yes", typeof(string), null, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal("Sort: Recent", result);
    }

    /// <summary>ConvertBack throws NotSupportedException.</summary>
    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        Assert.Throws<NotSupportedException>(() =>
            SortLabelConverter.Instance.ConvertBack("Sort: A-Z", typeof(bool), null, System.Globalization.CultureInfo.InvariantCulture));
    }
}
