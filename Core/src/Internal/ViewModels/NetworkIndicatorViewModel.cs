namespace BlueHeighliner.Comlink;

/// <summary>ViewModel interface for the network indicator in the top bar of a client: a box that always shows, online or offline, in the label and color the display handler states for each.</summary>
internal interface INetworkIndicatorViewModel
{
    /// <summary>Gets a value indicating whether the indicator shows online.</summary>
    bool IsOnline { get; }
    /// <summary>Gets the label shown in the indicator, for online or offline.</summary>
    string Label { get; }
    /// <summary>Gets the hex color of the indicator, for online or offline.</summary>
    string ColorHex { get; }
}

/// <summary>Follows <see cref="INetworkIndicator"/> and states how it looks through the display handler's labels and colors.</summary>
internal sealed partial class NetworkIndicatorViewModel : ObservableObject, INetworkIndicatorViewModel
{
    /// <summary>Initializes a new <see cref="NetworkIndicatorViewModel"/> showing the indicator's current state and following its changes.</summary>
    /// <param name="indicator">The state the indicator shows.</param>
    /// <param name="engineController">Supplies the label and color for each state.</param>
    public NetworkIndicatorViewModel(INetworkIndicator indicator, IEngineController engineController)
    {
        this.engineController = engineController;
        Apply(indicator.IsOnline);
        indicator.Changed += isOnline => UiThread.Run(() =>
        {
            Apply(isOnline);
            return Task.CompletedTask;
        });
    }

    private readonly IEngineController engineController;

    [ObservableProperty] private bool isOnline;
    [ObservableProperty] private string label = string.Empty;
    [ObservableProperty] private string colorHex = string.Empty;

    private void Apply(bool online)
    {
        IsOnline = online;
        Label = engineController.GetNetworkIndicatorLabel(online);
        ColorHex = engineController.GetNetworkIndicatorColor(online);
    }
}
