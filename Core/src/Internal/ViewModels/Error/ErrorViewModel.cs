namespace BlueHeighliner.Comlink;

/// <summary>ViewModel interface for the screen shown in place of the app when the node cannot be identified.</summary>
internal interface IErrorViewModel
{
    /// <summary>Gets or sets why the node cannot be identified, as the screen shows it.</summary>
    string Message { get; set; }
}

/// <summary>ViewModel for the screen shown in place of the app when the node's certificate is missing or invalid, or does not name a user of the network.</summary>
internal sealed partial class ErrorViewModel : ObservableObject, IErrorViewModel
{
    [ObservableProperty] private string message = string.Empty;
}
