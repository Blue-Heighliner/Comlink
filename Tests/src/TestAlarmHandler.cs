namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test <see cref="IAlarmHandler"/> that stops the alarm sound after five seconds.</summary>
public sealed class TestAlarmHandler : IAlarmHandler
{
    /// <inheritdoc />
    public TimeSpan AlertDuration => TimeSpan.FromSeconds(5);
}
