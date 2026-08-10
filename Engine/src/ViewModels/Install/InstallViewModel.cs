namespace BlueHeighliner.Comlink.Engine.ViewModels.Install;

/// <summary>ViewModel interface for the site-installation screen.</summary>
public interface IInstallViewModel
{
    /// <summary>Gets or sets the site activation code entered by the user (auto-uppercased).</summary>
    string SiteCode { get; set; }
    /// <summary>Gets or sets the error message to display, or <see langword="null"/> when there is no error.</summary>
    string? ErrorMessage { get; set; }
    /// <summary>Gets or sets a value indicating whether an install operation is in progress.</summary>
    bool IsLoading { get; set; }
    /// <summary>Raised after a site is successfully installed, providing the resulting site information.</summary>
    event Func<SiteInfo, Task>? InstallSucceeded;
    /// <summary>Validates the site code and installs the site via the service connection.</summary>
    IAsyncRelayCommand InstallCommand { get; }
}

/// <summary>ViewModel for the site-installation screen, handling site code entry and the install command.</summary>
public sealed partial class InstallViewModel : ObservableObject, IInstallViewModel
{
    private readonly IServiceConnection _connection;

    [ObservableProperty] private string _siteCode = string.Empty;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isLoading;

    partial void OnSiteCodeChanged(string value)
    {
        string upper = value.ToUpperInvariant();
        if (value != upper) SiteCode = upper;
    }

    /// <summary>Raised after a site is successfully installed, providing the resulting site information.</summary>
    public event Func<SiteInfo, Task>? InstallSucceeded;

    /// <summary>Initializes a new <see cref="InstallViewModel"/> with the required service connection.</summary>
    /// <param name="connection">Service connection used to install the site.</param>
    public InstallViewModel(IServiceConnection connection)
    {
        _connection = connection;
    }

    [RelayCommand]
    private async Task Install()
    {
        if (string.IsNullOrWhiteSpace(SiteCode))
        {
            ErrorMessage = "Please enter a site code.";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            SiteInfo? siteInfo = await _connection.InstallSite(SiteCode.Trim());
            if (siteInfo is null)
            {
                ErrorMessage = "Invalid site code. Please try again.";
                return;
            }

            if (InstallSucceeded is not null)
                await InstallSucceeded(siteInfo);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Install failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
