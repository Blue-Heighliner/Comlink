namespace BlueHeighliner.Comlink.Tests.Unit.Public.Utilities;

/// <summary>Unit tests for <see cref="TimestampExtensions"/>.</summary>
public sealed class TimestampExtensionsTests
{
    private static DateTime Epoch1980 { get; } = new(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static DateTime Unix { get; } = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Seconds since the epoch of the caller's choosing, in the integer type asked.</summary>
    [Fact]
    public void ToTimestamp_CountsSecondsSinceTheEpoch()
    {
        DateTime date = new(1980, 1, 2, 0, 0, 1, DateTimeKind.Utc);

        Assert.Equal(86401, date.ToTimestamp<int>(Epoch1980));
        Assert.Equal(86401L, date.ToTimestamp<long>(Epoch1980));
        Assert.Equal(315619201L, date.ToTimestamp<long>(Unix));
        Assert.Equal(0, Epoch1980.ToTimestamp<int>(Epoch1980));
    }

    /// <summary>A timestamp reads back to the date, and the two round-trip whatever the epoch.</summary>
    [Fact]
    public void FromTimestamp_ReadsBackTheDate()
    {
        Assert.Equal(new DateTime(1980, 1, 2, 0, 0, 1, DateTimeKind.Utc), 86401.FromTimestamp(Epoch1980));
        Assert.Equal(DateTimeKind.Utc, 0.FromTimestamp(Epoch1980).Kind);
        Assert.Equal(Unix.AddSeconds(1_700_000_000), 1_700_000_000L.FromTimestamp(Unix));
        Assert.Equal(Unix.AddSeconds(1_700_000_000), Unix.AddSeconds(1_700_000_000).ToTimestamp<long>(Unix).FromTimestamp(Unix));
    }

    /// <summary>A unit other than a second counts that unit, rounding down, and negative before the epoch.</summary>
    [Fact]
    public void Unit_CountsThatUnit_RoundingDown()
    {
        Assert.Equal(1500L, Unix.AddSeconds(1.5).ToTimestamp<long>(Unix, TimeSpan.FromMilliseconds(1)));
        Assert.Equal(1, Unix.AddSeconds(119).ToTimestamp<int>(Unix, TimeSpan.FromMinutes(1)));
        Assert.Equal(-1, Unix.AddSeconds(-0.5).ToTimestamp<int>(Unix));
        Assert.Equal(-3, Unix.AddSeconds(-2.5).ToTimestamp<int>(Unix));
        Assert.Equal(Unix.AddMinutes(3), 3.FromTimestamp(Unix, TimeSpan.FromMinutes(1)));
    }

    /// <summary>Dates not in UTC, a unit that is not positive, and a timestamp that does not fit are refused.</summary>
    [Fact]
    public void Errors_AreRefused()
    {
        DateTime local = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() => local.ToTimestamp<int>(Unix));
        Assert.Throws<ArgumentException>(() => Unix.ToTimestamp<int>(local));
        Assert.Throws<ArgumentException>(() => 1.FromTimestamp(local));
        Assert.Throws<ArgumentOutOfRangeException>(() => Unix.ToTimestamp<int>(Unix, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => 1.FromTimestamp(Unix, TimeSpan.FromSeconds(-1)));
        Assert.Throws<OverflowException>(() => Unix.AddYears(100).ToTimestamp<int>(Unix));
        Assert.Throws<OverflowException>(() => long.MaxValue.FromTimestamp(Unix));
    }
}
