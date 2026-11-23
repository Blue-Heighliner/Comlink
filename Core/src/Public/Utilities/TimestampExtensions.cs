namespace BlueHeighliner.Comlink;

/// <summary>
/// Extensions for converting between UTC dates and timestamps, whole numbers counting a unit of time (a second unless stated) since an epoch of the caller's choosing, such as the 1970 epoch of Unix time.
/// </summary>
public static class TimestampExtensions
{
    extension(DateTime date)
    {
        /// <summary>Converts the date to a timestamp: how many whole units have passed since the epoch, rounded down, negative before it.</summary>
        /// <typeparam name="T">The integer type of the timestamp, such as <see cref="int"/> for a 32-bit field.</typeparam>
        /// <param name="epoch">The UTC date the timestamp counts from.</param>
        /// <param name="unit">The length of time one step of the timestamp counts, or <see langword="null"/> for one second.</param>
        /// <returns>The timestamp.</returns>
        /// <exception cref="ArgumentException">The date or <paramref name="epoch"/> is not in UTC.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="unit"/> is not positive.</exception>
        /// <exception cref="OverflowException">The timestamp does not fit in <typeparamref name="T"/>.</exception>
        public T ToTimestamp<T>(DateTime epoch, TimeSpan? unit = null)
            where T : System.Numerics.IBinaryInteger<T>
        {
            RequireUtc(date, nameof(date));
            RequireUtc(epoch, nameof(epoch));
            long step = Step(unit);
            (long whole, long rest) = long.DivRem((date - epoch).Ticks, step);
            return T.CreateChecked(rest < 0 ? whole - 1 : whole);
        }
    }

    extension<T>(T timestamp)
        where T : System.Numerics.IBinaryInteger<T>
    {
        /// <summary>Converts the timestamp back to the UTC date it counts to.</summary>
        /// <param name="epoch">The UTC date the timestamp counts from.</param>
        /// <param name="unit">The length of time one step of the timestamp counts, or <see langword="null"/> for one second.</param>
        /// <returns>The date, in UTC.</returns>
        /// <exception cref="ArgumentException"><paramref name="epoch"/> is not in UTC.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="unit"/> is not positive, or the date is out of range.</exception>
        /// <exception cref="OverflowException">The timestamp does not fit in a 64-bit number of ticks.</exception>
        public DateTime FromTimestamp(DateTime epoch, TimeSpan? unit = null)
        {
            RequireUtc(epoch, nameof(epoch));
            long step = Step(unit);
            return epoch.AddTicks(checked(long.CreateChecked(timestamp) * step));
        }
    }

    private static long Step(TimeSpan? unit)
    {
        TimeSpan value = unit ?? TimeSpan.FromSeconds(1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero, nameof(unit));
        return value.Ticks;
    }

    private static void RequireUtc(DateTime date, string name)
    {
        if (date.Kind is not DateTimeKind.Utc)
        {
            throw new ArgumentException("The date must be in UTC.", name);
        }
    }
}
