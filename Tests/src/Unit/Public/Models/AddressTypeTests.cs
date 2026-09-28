namespace BlueHeighliner.Comlink.Tests.Unit.Public.Models;

/// <summary>Unit tests for <see cref="AddressType"/> and its parsing.</summary>
public sealed class AddressTypeTests
{
    /// <summary>Each role parses from its name, ignoring case, including the external role.</summary>
    [Theory]
    [InlineData("To", AddressType.To)]
    [InlineData("cc", AddressType.Cc)]
    [InlineData("External", AddressType.External)]
    [InlineData("EXTERNAL", AddressType.External)]
    public void ParseAddressType_KnownRole_ParsesIgnoringCase(string text, AddressType expected)
        => Assert.Equal(expected, text.ParseAddressType());

    /// <summary>An unrecognized role falls back to To.</summary>
    [Fact]
    public void ParseAddressType_Unknown_DefaultsToTo()
        => Assert.Equal(AddressType.To, "Outside".ParseAddressType());
}
