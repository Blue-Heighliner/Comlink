namespace BlueHeighliner.Comlink.Tests.Unit.ViewModels;

/// <summary>Unit tests for <see cref="DeleteConfirmation"/>.</summary>
public sealed class DeleteConfirmationTests
{
    /// <summary>The first press arms it without confirming, and the second confirms and disarms.</summary>
    [Fact]
    public void Confirm_FirstArmsThenSecondConfirms()
    {
        List<bool> changes = [];
        DeleteConfirmation confirmation = new(changes.Add, TimeSpan.FromMinutes(1));

        Assert.False(confirmation.Confirm());
        Assert.True(confirmation.IsPending);
        Assert.True(confirmation.Confirm());
        Assert.False(confirmation.IsPending);

        Assert.Equal([true, false], changes);
    }

    /// <summary>An armed press that is not confirmed in time disarms itself, so the next press arms again instead of deleting.</summary>
    [Fact]
    public async Task Confirm_ExpiresWhenNotConfirmedInTime()
    {
        TaskCompletionSource expired = new();
        DeleteConfirmation confirmation = new(pending => { if (!pending) { expired.TrySetResult(); } }, TimeSpan.FromMilliseconds(50));

        Assert.False(confirmation.Confirm());
        await expired.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(confirmation.IsPending);
        Assert.False(confirmation.Confirm());
    }

    /// <summary>Confirming right away cancels the expiry, so it does not later report a spurious disarm.</summary>
    [Fact]
    public async Task Confirm_ConfirmedBeforeExpiry_DoesNotNotifyAgainLater()
    {
        List<bool> changes = [];
        DeleteConfirmation confirmation = new(changes.Add, TimeSpan.FromMilliseconds(50));
        confirmation.Confirm();
        confirmation.Confirm();

        await Task.Delay(200);

        Assert.Equal([true, false], changes);
    }
}
