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

        Assert.Equal(("OFFLINE", "#D35400", false), (vm.Label, vm.ColorHex, vm.IsOnline));

        indicator.Set(true);

        Assert.Equal(("ONLINE", "#2E7D32", true), (vm.Label, vm.ColorHex, vm.IsOnline));
    }

    /// <summary>A label and color the display handler states replace the defaults, for each state separately.</summary>
    [Fact]
    public void DisplayHandlerLabelsAndColors_ReplaceTheDefaults()
    {
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.GetNetworkIndicatorLabel(true)).Returns("LINKED");
        controller.Setup(c => c.GetNetworkIndicatorColor(true)).Returns("#112233");
        NetworkIndicator indicator = new(LoggerFactory.Create(_ => { }));
        indicator.Set(true);

        NetworkIndicatorViewModel vm = new(indicator, controller.Object);

        Assert.Equal(("LINKED", "#112233"), (vm.Label, vm.ColorHex));
    }
}
