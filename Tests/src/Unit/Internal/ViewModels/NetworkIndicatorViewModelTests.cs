namespace BlueHeighliner.Comlink.Tests.Unit.Internal.ViewModels;

/// <summary>Unit tests for <see cref="NetworkIndicatorViewModel"/>.</summary>
public sealed class NetworkIndicatorViewModelTests
{
    /// <summary>The indicator shows the engine's default labels and colors for each state, following the indicator.</summary>
    [Fact]
    public void FollowsTheIndicatorWithDefaultLabelsAndColors()
    {
        NetworkIndicator indicator = new(LoggerFactory.Create(_ => { }));
        NetworkIndicatorViewModel vm = new(indicator, new TestEngineController());

        Assert.Equal(("OFFLINE", "#D35400", "#FFFFFF", false), (vm.Label, vm.BackgroundHex, vm.ForegroundHex, vm.IsOnline));

        indicator.Set(true);

        Assert.Equal(("ONLINE", "#2E7D32", "#FFFFFF", true), (vm.Label, vm.BackgroundHex, vm.ForegroundHex, vm.IsOnline));
    }

    /// <summary>A label and colors the display handler states replace the defaults, for each state separately.</summary>
    [Fact]
    public void DisplayHandlerLabelsAndColors_ReplaceTheDefaults()
    {
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.GetNetworkIndicatorLabel(true)).Returns("LINKED");
        controller.Setup(c => c.GetNetworkIndicatorBackground(true)).Returns("#112233");
        controller.Setup(c => c.GetNetworkIndicatorForeground(true)).Returns("#FFEEDD");
        NetworkIndicator indicator = new(LoggerFactory.Create(_ => { }));
        indicator.Set(true);

        NetworkIndicatorViewModel vm = new(indicator, controller.Object);

        Assert.Equal(("LINKED", "#112233", "#FFEEDD"), (vm.Label, vm.BackgroundHex, vm.ForegroundHex));
    }
}
